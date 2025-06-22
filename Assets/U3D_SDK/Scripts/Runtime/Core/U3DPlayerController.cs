using U3D;
using UnityEngine;
using UnityEngine.InputSystem;
using Fusion;

[RequireComponent(typeof(CharacterController), typeof(PlayerInput))]
public class U3DPlayerController : NetworkBehaviour
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

    [Header("Network Synchronization")]
    [SerializeField] private float networkSendRate = 20f;
    [SerializeField] private float positionThreshold = 0.1f;
    [SerializeField] private float rotationThreshold = 1f;

    // Hidden zoom settings with default values
    [HideInInspector][SerializeField] private float zoomFOV = 30f;
    [HideInInspector][SerializeField] private float defaultFOV = 60f;
    [HideInInspector][SerializeField] private float zoomSpeed = 5f;

    // Networked Properties for Multiplayer
    [Networked] public Vector3 NetworkPosition { get; set; }
    [Networked] public Quaternion NetworkRotation { get; set; }
    [Networked] public bool NetworkIsMoving { get; set; }
    [Networked] public bool NetworkIsSprinting { get; set; }
    [Networked] public bool NetworkIsCrouching { get; set; }
    [Networked] public bool NetworkIsFlying { get; set; }
    [Networked] public float NetworkCameraPitch { get; set; }
    [Networked] public bool NetworkIsInteracting { get; set; }

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

    // Network State
    private bool _isLocalPlayer;
    private float _lastNetworkSendTime;
    private Vector3 _lastSentPosition;
    private Quaternion _lastSentRotation;

    // FUSION INPUT TRACKING
    private NetworkButtons _buttonsPrevious;

    public override void Spawned()
    {
        // In Shared Mode, each client has authority over their own player
        _isLocalPlayer = Object.HasStateAuthority;

        // Initialize components
        InitializeComponents();

        // Configure for local vs remote player
        ConfigurePlayerForNetworking();

        Debug.Log($"Player spawned - Local: {_isLocalPlayer} - StateAuthority: {Object.HasStateAuthority}");
    }

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
    }

    void InitializeComponents()
    {
        if (!_isLocalPlayer) return;

        // Lock cursor for FPS controls (local player only)
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void ConfigurePlayerForNetworking()
    {
        if (_isLocalPlayer)
        {
            // Local player - enable all controls
            if (playerInput != null)
                playerInput.enabled = true;
            if (playerCamera != null)
            {
                playerCamera.enabled = true;
                playerCamera.tag = "MainCamera";
            }
        }
        else
        {
            // Remote player - disable input and camera
            if (playerInput != null)
                playerInput.enabled = false;
            if (playerCamera != null)
            {
                playerCamera.enabled = false;
                playerCamera.tag = "Untagged";
            }

            // Disable character controller for remote players (we'll interpolate position)
            if (characterController != null)
                characterController.enabled = false;
        }
    }

    void Start()
    {
        // Set initial perspective based on mode (local player only)
        if (_isLocalPlayer)
        {
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
        else
        {
            // CREATE NAMETAG FOR REMOTE PLAYERS ONLY
            CreateNametag();
        }
    }

    void CreateNametag()
    {
        // Create nametag anchor above player head
        var nametagAnchor = new GameObject("NametagAnchor");
        nametagAnchor.transform.SetParent(transform);
        nametagAnchor.transform.localPosition = Vector3.up * 2.5f; // Above player head

        // Add and initialize nametag component
        var nametag = nametagAnchor.AddComponent<U3D.Networking.U3DPlayerNametag>();
        nametag.Initialize(this);
    }

    // FUSION 2 REQUIRED: Replace Update with FixedUpdateNetwork
    public override void FixedUpdateNetwork()
    {
        // Only process for local player (StateAuthority in Shared Mode)
        if (!_isLocalPlayer) return;

        // Get Fusion input instead of Unity Input System
        if (GetInput<U3DNetworkInputData>(out var input))
        {
            // Process all input in the fixed network update
            HandleGroundCheck();
            HandleMovementFusion(input);
            HandleLookFusion(input);
            HandleButtonInputsFusion(input);
            HandleTeleportFusion(input);
            HandleCameraPositioning();
            ApplyGravity();
        }
    }

    // FUSION RENDER: For camera and visual updates
    public override void Render()
    {
        // Only apply interpolation for remote players
        if (_isLocalPlayer)
        {
            // Local player - handle camera and visual effects
            HandleZoom();
            return;
        }

        // Remote player interpolation
        // Validate NetworkRotation before using it
        if (NetworkRotation == Quaternion.identity ||
            float.IsNaN(NetworkRotation.x) || float.IsNaN(NetworkRotation.y) ||
            float.IsNaN(NetworkRotation.z) || float.IsNaN(NetworkRotation.w))
        {
            return; // Skip this frame if rotation is invalid
        }

        // Smooth interpolation for remote players only
        transform.position = Vector3.Lerp(transform.position, NetworkPosition, Time.deltaTime * 10f);

        // Use Slerp instead of Lerp for quaternions, and validate angle difference
        float angleDifference = Quaternion.Angle(transform.rotation, NetworkRotation);
        if (angleDifference > 0.1f && angleDifference < 180f) // Valid range
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, NetworkRotation, Time.deltaTime * 10f);
        }

        // Apply camera pitch for remote players (head movement)
        if (playerCamera != null)
        {
            Vector3 cameraRotation = playerCamera.transform.localEulerAngles;
            cameraRotation.x = NetworkCameraPitch;
            playerCamera.transform.localEulerAngles = cameraRotation;
        }
    }

    void HandleGroundCheck()
    {
        if (!_isLocalPlayer) return;

        isGrounded = characterController.isGrounded;

        if (isGrounded && velocity.y < 0)
        {
            velocity.y = -2f; // Small negative value to keep grounded
            jumpCount = 0; // Reset jump count when grounded
        }
    }

    void HandleMovementFusion(U3DNetworkInputData input)
    {
        if (!enableMovement || !_isLocalPlayer) return;

        // Get movement input from Fusion
        moveInput = input.MovementInput;

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
            if (input.Buttons.IsSet(U3DInputButtons.Jump))
                flyDirection += Vector3.up;
            if (input.Buttons.IsSet(U3DInputButtons.Crouch))
                flyDirection += Vector3.down;

            characterController.Move(flyDirection * currentSpeed * Runner.DeltaTime);
        }
        else
        {
            // Ground-based movement
            characterController.Move(moveVelocity * Runner.DeltaTime);
        }

        // Update networked position
        NetworkPosition = transform.position;
        NetworkRotation = transform.rotation;
        NetworkIsMoving = velocity.magnitude > 0.1f;
    }

    void HandleLookFusion(U3DNetworkInputData input)
    {
        if (!enableMovement || !_isLocalPlayer) return;

        // Get look input from Fusion
        lookInput = input.LookInput;

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

        // Update networked camera pitch
        NetworkCameraPitch = cameraPitch;
    }

    void HandleButtonInputsFusion(U3DNetworkInputData input)
    {
        if (!_isLocalPlayer) return;

        // Get button press/release states
        var pressed = input.Buttons.GetPressed(_buttonsPrevious);
        var released = input.Buttons.GetReleased(_buttonsPrevious);

        // Jump
        if (enableJumping && pressed.IsSet(U3DInputButtons.Jump))
        {
            HandleJumpFusion();
        }

        // Sprint (toggle)
        if (enableSprintToggle && pressed.IsSet(U3DInputButtons.Sprint))
        {
            isSprinting = !isSprinting;
            NetworkIsSprinting = isSprinting;
        }

        // Crouch (toggle)
        if (enableCrouchToggle && pressed.IsSet(U3DInputButtons.Crouch))
        {
            isCrouching = !isCrouching;
            NetworkIsCrouching = isCrouching;

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

        // Fly (toggle)
        if (enableFlying && pressed.IsSet(U3DInputButtons.Fly))
        {
            isFlying = !isFlying;
            NetworkIsFlying = isFlying;

            if (isFlying)
            {
                velocity = Vector3.zero; // Reset velocity when starting to fly
            }
        }

        // Interact
        if (pressed.IsSet(U3DInputButtons.Interact))
        {
            NetworkIsInteracting = true;
            U3DInteractionManager.Instance?.OnPlayerInteract();
        }

        // Zoom
        isZooming = input.Buttons.IsSet(U3DInputButtons.Zoom);
        targetFOV = isZooming ? zoomFOV : defaultFOV;

        // Handle perspective switching with scroll input
        if (perspectiveMode == PerspectiveMode.SmoothScroll && Mathf.Abs(input.PerspectiveScroll) > 0.1f)
        {
            if (input.PerspectiveScroll > 0.1f && !isFirstPerson)
            {
                SetFirstPerson();
            }
            else if (input.PerspectiveScroll < -0.1f && isFirstPerson)
            {
                SetThirdPerson();
            }
        }

        // Store current buttons for next frame
        _buttonsPrevious = input.Buttons;
    }

    void HandleJumpFusion()
    {
        if (isFlying)
        {
            // Flying mode - jump moves up (handled in movement)
            return;
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

    void HandleTeleportFusion(U3DNetworkInputData input)
    {
        if (!enableTeleport || !_isLocalPlayer) return;

        // Check for teleport button press
        var pressed = input.Buttons.GetPressed(_buttonsPrevious);
        if (pressed.IsSet(U3DInputButtons.Teleport))
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
        if (!enableSmoothTransitions || !_isLocalPlayer) return;

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
            Runner.DeltaTime * perspectiveTransitionSpeed
        );
    }

    void HandleZoom()
    {
        if (!enableViewZoom || !_isLocalPlayer) return;

        // Smooth FOV transition
        playerCamera.fieldOfView = Mathf.Lerp(playerCamera.fieldOfView, targetFOV, Time.deltaTime * zoomSpeed);
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
        if (isFlying || isGrounded || !_isLocalPlayer) return;

        velocity.y += gravity * Runner.DeltaTime;
        characterController.Move(velocity * Runner.DeltaTime);
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

    void LoadPlayerPreferences()
    {
        // Load look inversion preference (player-specific, not creator setting)
        lookInverted = PlayerPrefs.GetInt("U3D_LookInverted", 0) == 1;
    }

    // Legacy Unity Input System callbacks - now disabled for Fusion
    public void OnMove(InputAction.CallbackContext context) { }
    public void OnLook(InputAction.CallbackContext context) { }
    public void OnJump(InputAction.CallbackContext context) { }
    public void OnSprint(InputAction.CallbackContext context) { }
    public void OnCrouch(InputAction.CallbackContext context) { }
    public void OnZoom(InputAction.CallbackContext context) { }
    public void OnFly(InputAction.CallbackContext context) { }
    public void OnAutoRun(InputAction.CallbackContext context) { }
    public void OnPerspectiveSwitch(InputAction.CallbackContext context) { }
    public void OnInteract(InputAction.CallbackContext context) { }
    public void OnPause(InputAction.CallbackContext context) { }
    public void OnTeleport(InputAction.CallbackContext context) { }

    // Public methods for external access (e.g., UI, networking)
    public bool IsGrounded => isGrounded;
    public bool IsSprinting => isSprinting;
    public bool IsCrouching => isCrouching;
    public bool IsFlying => isFlying;
    public bool IsAutoRunning => isAutoRunning;
    public bool IsFirstPerson => isFirstPerson;
    public Vector3 Velocity => velocity;
    public float CurrentSpeed => GetCurrentSpeed();
    public bool IsLocalPlayer => _isLocalPlayer;

    // Methods for networking preparation
    public void SetPosition(Vector3 position)
    {
        if (!_isLocalPlayer) return;

        characterController.enabled = false;
        transform.position = position;
        characterController.enabled = true;
    }

    public void SetRotation(float yRotation)
    {
        if (!_isLocalPlayer) return;

        transform.rotation = Quaternion.Euler(0, yRotation, 0);
    }

    public void SetCameraPitch(float pitch)
    {
        if (!_isLocalPlayer) return;

        cameraPitch = pitch;
    }
}