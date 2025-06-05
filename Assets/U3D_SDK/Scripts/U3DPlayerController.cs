using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController), typeof(PlayerInput))]
public class U3DPlayerController : MonoBehaviour
{
    [Header("Basic Movement")]
    [SerializeField] private bool enableMovement = true;
    [SerializeField] private float walkSpeed = 4f;
    [SerializeField] private float runSpeed = 8f;
    [SerializeField] private float gravity = -20f;

    // Hidden advanced fields with default values
    [HideInInspector][SerializeField] private float groundCheckDistance = 0.1f;

    public enum PerspectiveMode { FirstPersonOnly, ThirdPersonOnly, SmoothScroll }

    [Header("Perspective Control")]
    [SerializeField] private PerspectiveMode perspectiveMode = PerspectiveMode.SmoothScroll;

    // Hidden advanced fields with default values
    [HideInInspector][SerializeField] private float thirdPersonDistance = 5f;
    [HideInInspector][SerializeField] private float perspectiveTransitionSpeed = 8f;
    [HideInInspector][SerializeField] private bool enableCameraCollision = true;
    [HideInInspector][SerializeField] private bool enableSmoothTransitions = true;
    [HideInInspector][SerializeField] private float mouseSensitivity = 2f;
    [HideInInspector][SerializeField] private float lookUpLimit = 80f;
    [HideInInspector][SerializeField] private float lookDownLimit = -80f;
    [HideInInspector][SerializeField] private float cameraCollisionRadius = 0.2f;
    [HideInInspector][SerializeField] private float cameraCollisionBuffer = 0.1f;

    [Header("Advanced Movement")]
    [SerializeField] private bool enableSprintToggle = true;
    [SerializeField] private KeyCode sprintKey = KeyCode.LeftShift;
    [SerializeField] private bool enableAutoRun = true;
    [SerializeField] private KeyCode autoRunKey = KeyCode.Numlock;
    [SerializeField] private bool enableFlying = true;
    [SerializeField] private KeyCode flyKey = KeyCode.F;
    [SerializeField] private bool enableCrouchToggle = true;
    [SerializeField] private KeyCode crouchKey = KeyCode.C;
    [SerializeField] private bool enableTeleport = true;
    [SerializeField] private bool enableViewZoom = true;
    [SerializeField] private bool enableFOVAdjustment = true;

    [Header("Jump Settings")]
    [SerializeField] private bool enableJumping = true;
    [SerializeField] private KeyCode jumpKey = KeyCode.Space;
    [SerializeField] private float jumpHeight = 2f;
    [SerializeField] private float[] additionalJumps = new float[] { 4f }; 

    [Header("Interaction")]
    [SerializeField] private KeyCode interactKey = KeyCode.E;

    // Hidden zoom settings with default values
    [HideInInspector][SerializeField] private float zoomFOV = 30f;
    [HideInInspector][SerializeField] private float defaultFOV = 60f;
    [HideInInspector][SerializeField] private float zoomSpeed = 5f;

    // Core Components
    private CharacterController characterController;
    private PlayerInput playerInput;
    private Camera playerCamera;

    // Movement State
    private Vector3 velocity;
    private Vector2 moveInput;
    private Vector2 lookInput;
    private bool isGrounded;
    private int jumpCount;
    private bool isSprinting;
    private bool isCrouching;
    private bool isFlying;
    private bool isAutoRunning;
    private bool isZooming;

    // Camera State
    private float cameraPitch;
    private bool isFirstPerson = true;
    private Vector3 firstPersonPosition;
    private Vector3 thirdPersonPosition;
    private float currentCameraDistance;
    private float targetFOV;
    private bool lookInverted;
    private float originalCameraHeight;
    private float crouchCameraOffset = -0.5f;

    // Input Actions (cached for performance)
    private InputAction moveAction;
    private InputAction lookAction;
    private InputAction jumpAction;
    private InputAction sprintAction;
    private InputAction crouchAction;
    private InputAction zoomAction;
    private InputAction flyAction;
    private InputAction autoRunAction;
    private InputAction perspectiveSwitchAction;
    private InputAction interactAction;
    private InputAction teleportAction;

    void Awake()
    {
        // Get required components
        characterController = GetComponent<CharacterController>();
        playerInput = GetComponent<PlayerInput>();
        playerCamera = GetComponentInChildren<Camera>();

        if (playerCamera == null)
        {
            Debug.LogError("U3DPlayerController: No Camera found in children. Please add a Camera as a child object.");
            enabled = false;
            return;
        }

        // Initialize camera settings
        firstPersonPosition = playerCamera.transform.localPosition;
        thirdPersonPosition = firstPersonPosition + Vector3.back * thirdPersonDistance;
        currentCameraDistance = 0f;
        targetFOV = defaultFOV;
        playerCamera.fieldOfView = defaultFOV;

        // Load player preferences
        LoadPlayerPreferences();

        // Cache input actions for performance
        CacheInputActions();

        // Lock cursor for FPS controls
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Start()
    {
        // Set initial perspective based on mode
        switch (perspectiveMode)
        {
            case PerspectiveMode.FirstPersonOnly:
                SetFirstPerson();
                break;
            case PerspectiveMode.ThirdPersonOnly:
                SetThirdPerson();
                break;
            case PerspectiveMode.SmoothScroll:
                SetFirstPerson(); // Start in first person
                break;
        }
    }

    void Update()
    {
        HandleGroundCheck();
        HandleMovement();
        HandleLook();
        HandlePerspectiveSwitch();
        HandleZoom();
        HandleTeleport();
        HandleCameraPositioning();
        ApplyGravity();
    }

    void CacheInputActions()
    {
        var actionMap = playerInput.actions.FindActionMap("Player");
        if (actionMap == null)
        {
            Debug.LogError("U3DPlayerController: 'Player' action map not found in Input Actions asset.");
            return;
        }

        moveAction = actionMap.FindAction("Move");
        lookAction = actionMap.FindAction("Look");
        jumpAction = actionMap.FindAction("Jump");
        sprintAction = actionMap.FindAction("Sprint");
        crouchAction = actionMap.FindAction("Crouch");
        zoomAction = actionMap.FindAction("Zoom");
        flyAction = actionMap.FindAction("Fly");
        autoRunAction = actionMap.FindAction("AutoRun");
        perspectiveSwitchAction = actionMap.FindAction("PerspectiveSwitch");
        interactAction = actionMap.FindAction("Interact");
        teleportAction = actionMap.FindAction("Teleport");
    }

    void HandleGroundCheck()
    {
        isGrounded = characterController.isGrounded;

        if (isGrounded && velocity.y < 0)
        {
            velocity.y = -2f; // Small negative value to keep grounded
            jumpCount = 0; // Reset jump count when grounded
        }
    }

    void HandleMovement()
    {
        if (!enableMovement) return;

        // Get movement input
        moveInput = moveAction?.ReadValue<Vector2>() ?? Vector2.zero;

        // Handle auto-run
        if (isAutoRunning)
        {
            moveInput.y = 1f; // Force forward movement
        }

        // Calculate movement direction relative to camera
        Vector3 forward = playerCamera.transform.forward;
        Vector3 right = playerCamera.transform.right;

        // Remove Y component for ground movement (unless flying)
        if (!isFlying)
        {
            forward.y = 0f;
            right.y = 0f;
            forward.Normalize();
            right.Normalize();
        }

        Vector3 moveDirection = (forward * moveInput.y + right * moveInput.x).normalized;

        // Apply speed based on current state
        float currentSpeed = GetCurrentSpeed();
        Vector3 moveVelocity = moveDirection * currentSpeed;

        // Apply movement
        if (isFlying)
        {
            // 6DOF movement when flying
            Vector3 flyDirection = moveDirection;
            if (jumpAction?.IsPressed() == true)
                flyDirection += Vector3.up;
            if (crouchAction?.IsPressed() == true)
                flyDirection += Vector3.down;

            characterController.Move(flyDirection * currentSpeed * Time.deltaTime);
        }
        else
        {
            // Ground-based movement
            characterController.Move(moveVelocity * Time.deltaTime);
        }
    }

    void HandleLook()
    {
        if (!enableMovement) return;

        // Get look input
        lookInput = lookAction?.ReadValue<Vector2>() ?? Vector2.zero;

        // Apply mouse sensitivity
        lookInput *= mouseSensitivity * 0.1f;

        // Apply look inversion if enabled
        if (lookInverted)
            lookInput.y = -lookInput.y;

        // Horizontal rotation (Y-axis)
        transform.Rotate(Vector3.up, lookInput.x);

        // Vertical rotation (X-axis) - camera pitch
        cameraPitch -= lookInput.y;
        cameraPitch = Mathf.Clamp(cameraPitch, lookDownLimit, lookUpLimit);

        // Apply camera rotation
        Vector3 cameraRotation = playerCamera.transform.localEulerAngles;
        cameraRotation.x = cameraPitch;
        playerCamera.transform.localEulerAngles = cameraRotation;
    }

    void LoadPlayerPreferences()
    {
        // Load look inversion preference (player-specific, not creator setting)
        lookInverted = PlayerPrefs.GetInt("U3D_LookInverted", 0) == 1;
    }

    void HandlePerspectiveSwitch()
    {
        if (perspectiveMode != PerspectiveMode.SmoothScroll) return;

        float scrollInput = perspectiveSwitchAction?.ReadValue<float>() ?? 0f;

        if (scrollInput > 0.1f && !isFirstPerson)
        {
            SetFirstPerson();
        }
        else if (scrollInput < -0.1f && isFirstPerson)
        {
            SetThirdPerson();
        }
    }

    void HandleZoom()
    {
        if (!enableViewZoom) return;

        bool isZoomPressed = zoomAction?.IsPressed() == true;

        if (isZoomPressed && !isZooming)
        {
            isZooming = true;
            targetFOV = zoomFOV;
        }
        else if (!isZoomPressed && isZooming)
        {
            isZooming = false;
            targetFOV = defaultFOV;
        }

        // Smooth FOV transition
        playerCamera.fieldOfView = Mathf.Lerp(playerCamera.fieldOfView, targetFOV, Time.deltaTime * zoomSpeed);
    }

    void HandleTeleport()
    {
        if (!enableTeleport) return;

        // Only teleport when MultiTap interaction is performed (double-click completed)
        if (teleportAction?.WasPerformedThisFrame() == true)
        {
            PerformTeleport();
        }
    }

    void PerformTeleport()
    {
        // Raycast from center of screen
        Ray ray = playerCamera.ScreenPointToRay(new Vector3(Screen.width / 2, Screen.height / 2, 0));

        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            // Check if hit point is valid for teleportation
            Vector3 teleportPosition = hit.point;

            // Ensure we teleport to a walkable surface (add small offset above ground)
            teleportPosition.y += characterController.height / 2f;

            // Perform the teleport
            SetPosition(teleportPosition);
        }
    }

    void HandleCameraPositioning()
    {
        if (!enableSmoothTransitions) return;

        Vector3 targetPosition = isFirstPerson ? firstPersonPosition : thirdPersonPosition;

        // Apply crouch offset to camera position
        if (isCrouching)
        {
            targetPosition.y += crouchCameraOffset;
        }

        if (enableCameraCollision && !isFirstPerson)
        {
            targetPosition = GetCollisionSafeCameraPosition(targetPosition);
        }

        // Smooth camera position transition
        playerCamera.transform.localPosition = Vector3.Lerp(
            playerCamera.transform.localPosition,
            targetPosition,
            Time.deltaTime * perspectiveTransitionSpeed
        );
    }

    Vector3 GetCollisionSafeCameraPosition(Vector3 desiredPosition)
    {
        Vector3 playerHead = transform.position + firstPersonPosition;

        // Use the camera's actual current world position as target
        Vector3 cameraWorldTarget = transform.TransformPoint(desiredPosition);
        Vector3 direction = (cameraWorldTarget - playerHead).normalized;

        float maxDistance = Vector3.Distance(playerHead, cameraWorldTarget);

        if (Physics.SphereCast(playerHead, cameraCollisionRadius, direction, out RaycastHit hit, maxDistance))
        {
            float safeDistance = Mathf.Max(0.1f, hit.distance - cameraCollisionBuffer);
            Vector3 safeWorldPosition = playerHead + direction * safeDistance;
            return transform.InverseTransformPoint(safeWorldPosition);
        }

        return desiredPosition;
    }

    void ApplyGravity()
    {
        if (isFlying || isGrounded) return;

        velocity.y += gravity * Time.deltaTime;
        characterController.Move(velocity * Time.deltaTime);
    }

    float GetCurrentSpeed()
    {
        if (isSprinting)
            return runSpeed;
        else if (isCrouching)
            return walkSpeed * 0.5f; // Crouching is slower
        else
            return walkSpeed;
    }

    void SetFirstPerson()
    {
        isFirstPerson = true;
        currentCameraDistance = 0f;
    }

    void SetThirdPerson()
    {
        isFirstPerson = false;
        currentCameraDistance = thirdPersonDistance;
    }

    // Input Action Callbacks (Unity Events from PlayerInput component)
    public void OnMove(InputAction.CallbackContext context)
    {
        // Movement is handled in Update() via cached action reference
    }

    public void OnLook(InputAction.CallbackContext context)
    {
        // Look is handled in Update() via cached action reference
    }

    public void OnJump(InputAction.CallbackContext context)
    {
        if (!enableJumping || !context.performed) return;

        if (isFlying)
        {
            // Flying mode - jump moves up
            return; // Handled in HandleMovement
        }

        // Ground jump logic
        if (isGrounded || jumpCount < additionalJumps.Length + 1) // +1 for base jump
        {
            float jumpForce;
            if (jumpCount == 0)
            {
                // First jump uses base jumpHeight
                jumpForce = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }
            else if (jumpCount - 1 < additionalJumps.Length)
            {
                // Additional jumps use array values
                jumpForce = Mathf.Sqrt(additionalJumps[jumpCount - 1] * -2f * gravity);
            }
            else
            {
                return; // No more jumps available
            }

            velocity.y = jumpForce;
            jumpCount++;
        }
    }

    public void OnSprint(InputAction.CallbackContext context)
    {
        if (!enableSprintToggle || !context.performed) return;

        isSprinting = !isSprinting; // Toggle sprint
    }

    public void OnCrouch(InputAction.CallbackContext context)
    {
        if (!enableCrouchToggle || !context.performed) return;

        isCrouching = !isCrouching; // Toggle crouch

        // Adjust character controller height
        if (isCrouching)
        {
            characterController.height = 1f; // Crouch height
            characterController.center = new Vector3(0, 0.5f, 0);
        }
        else
        {
            characterController.height = 2f; // Standing height
            characterController.center = new Vector3(0, 1f, 0);
        }
    }

    public void OnZoom(InputAction.CallbackContext context)
    {
        // Zoom is handled in Update() via cached action reference
    }

    public void OnFly(InputAction.CallbackContext context)
    {
        if (!enableFlying || !context.performed) return;

        isFlying = !isFlying; // Toggle flying

        if (isFlying)
        {
            velocity = Vector3.zero; // Reset velocity when starting to fly
        }
    }

    public void OnAutoRun(InputAction.CallbackContext context)
    {
        if (!enableAutoRun || !context.performed) return;

        isAutoRunning = !isAutoRunning; // Toggle auto-run
    }

    public void OnPerspectiveSwitch(InputAction.CallbackContext context)
    {
        // Perspective switching is handled in Update() via cached action reference
    }

    public void OnInteract(InputAction.CallbackContext context)
    {
        if (!context.performed) return;

        // Interaction logic will be implemented in future phases
        Debug.Log("Interact pressed - placeholder for future implementation");
    }

    public void OnPause(InputAction.CallbackContext context)
    {
        if (!context.performed) return;

        // Pause logic will be implemented in future phases
        Debug.Log("Pause pressed - placeholder for future implementation");
    }

    public void OnTeleport(InputAction.CallbackContext context)
    {
        if (!enableTeleport || !context.performed) return;

        PerformTeleport();
    }

    // Public methods for external access (e.g., UI, networking)
    public bool IsGrounded => isGrounded;
    public bool IsSprinting => isSprinting;
    public bool IsCrouching => isCrouching;
    public bool IsFlying => isFlying;
    public bool IsAutoRunning => isAutoRunning;
    public bool IsFirstPerson => isFirstPerson;
    public Vector3 Velocity => velocity;
    public float CurrentSpeed => GetCurrentSpeed();

    // Methods for networking preparation
    public void SetPosition(Vector3 position)
    {
        characterController.enabled = false;
        transform.position = position;
        characterController.enabled = true;
    }

    public void SetRotation(float yRotation)
    {
        transform.rotation = Quaternion.Euler(0, yRotation, 0);
    }

    public void SetCameraPitch(float pitch)
    {
        cameraPitch = pitch;
    }
}