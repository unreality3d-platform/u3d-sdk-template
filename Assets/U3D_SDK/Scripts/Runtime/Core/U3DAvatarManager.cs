using U3D;
using UnityEngine;
using UnityEngine.InputSystem;
using Fusion;

[RequireComponent(typeof(U3DPlayerController))]
public class U3DAvatarManager : NetworkBehaviour
{
    [Header("Avatar Configuration")]
    [SerializeField] private GameObject avatarFBX;
    [SerializeField] private bool autoConfigureHumanoid = true;
    [SerializeField] private float avatarScaleMultiplier = 1f;

    [Header("Animation Settings")]
    [SerializeField] private RuntimeAnimatorController defaultAnimatorController;
    [SerializeField] private bool enableNetworkedAnimations = true;
    [SerializeField] private float animationSmoothTime = 0.1f;

    [Header("Avatar Positioning")]
    [SerializeField] private Vector3 avatarOffset = Vector3.zero;
    [SerializeField] private bool followPlayerRotation = true;
    [SerializeField] private bool hideInFirstPerson = true;

    // Networked Avatar State
    [Networked] public bool NetworkIsMoving { get; set; }
    [Networked] public bool NetworkIsSprinting { get; set; }
    [Networked] public bool NetworkIsCrouching { get; set; }
    [Networked] public bool NetworkIsFlying { get; set; }
    [Networked] public bool NetworkIsGrounded { get; set; }
    [Networked] public float NetworkMoveSpeed { get; set; }
    [Networked] public Vector2 NetworkMoveDirection { get; set; }

    // Core Components
    private U3DPlayerController playerController;
    private GameObject avatarInstance;
    private Animator avatarAnimator;
    private Avatar avatarAsset;
    private SkinnedMeshRenderer[] avatarRenderers;

    // Animation State
    private bool isInitialized = false;
    private Vector3 lastPosition;
    private float currentMoveSpeed;
    private Vector2 currentMoveDirection;

    // Animation Parameter IDs (Unity 6+ optimization)
    private int animIdIsMoving;
    private int animIdIsSprinting;
    private int animIdIsCrouching;
    private int animIdIsFlying;
    private int animIdIsGrounded;
    private int animIdMoveSpeed;
    private int animIdMoveX;
    private int animIdMoveY;

    public override void Spawned()
    {
        // Initialize components
        playerController = GetComponent<U3DPlayerController>();

        if (playerController == null)
        {
            Debug.LogError("U3DAvatarManager: U3DPlayerController not found!");
            return;
        }

        // Initialize avatar if FBX is assigned
        if (avatarFBX != null)
        {
            InitializeAvatar();
        }

        // Cache animation parameter IDs for performance
        CacheAnimationParameters();

        Debug.Log($"U3DAvatarManager spawned for {(Object.HasStateAuthority ? "Local" : "Remote")} player");
    }

    void InitializeAvatar()
    {
        try
        {
            // Instantiate avatar FBX
            avatarInstance = Instantiate(avatarFBX, transform);
            avatarInstance.transform.localPosition = avatarOffset;
            avatarInstance.transform.localRotation = Quaternion.identity;
            avatarInstance.transform.localScale = Vector3.one * avatarScaleMultiplier;

            // Configure humanoid Avatar if auto-configuration is enabled
            if (autoConfigureHumanoid)
            {
                ConfigureHumanoidAvatar();
            }

            // Get or add Animator component
            avatarAnimator = avatarInstance.GetComponent<Animator>();
            if (avatarAnimator == null)
            {
                avatarAnimator = avatarInstance.AddComponent<Animator>();
            }

            // Apply default animator controller
            if (defaultAnimatorController != null)
            {
                avatarAnimator.runtimeAnimatorController = defaultAnimatorController;
            }

            // Configure Animator for networking
            if (avatarAnimator != null)
            {
                avatarAnimator.applyRootMotion = false; // Prevent animation from overriding network movement
                avatarAnimator.updateMode = AnimatorUpdateMode.Normal;
                avatarAnimator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            }

            // Get all SkinnedMeshRenderers for visibility control
            avatarRenderers = avatarInstance.GetComponentsInChildren<SkinnedMeshRenderer>();

            isInitialized = true;
            Debug.Log("✅ Avatar initialized successfully");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ Failed to initialize avatar: {e.Message}");
        }
    }

    void ConfigureHumanoidAvatar()
    {
        // In Unity 6+, Avatar configuration happens at import time
        // This method handles runtime Avatar assignment

        if (avatarInstance == null) return;

        // Try to find existing Avatar asset from the FBX
        var avatarAssetFromFBX = avatarFBX.GetComponent<Animator>()?.avatar;

        if (avatarAssetFromFBX != null && avatarAssetFromFBX.isHuman)
        {
            avatarAsset = avatarAssetFromFBX;
            Debug.Log("✅ Using existing Humanoid Avatar from FBX");
        }
        else
        {
            // Create a runtime Avatar if needed (advanced use case)
            Debug.LogWarning("⚠️ No Humanoid Avatar found in FBX. Please configure Avatar in Import Settings:\n" +
                           "1. Select your FBX in Project window\n" +
                           "2. Go to Rig tab in Inspector\n" +
                           "3. Set Animation Type to Humanoid\n" +
                           "4. Set Avatar Definition to 'Create From This Model'\n" +
                           "5. Click Apply");
        }
    }

    void CacheAnimationParameters()
    {
        // Cache animation parameter IDs for Unity 6+ performance optimization
        animIdIsMoving = Animator.StringToHash("IsMoving");
        animIdIsSprinting = Animator.StringToHash("IsSprinting");
        animIdIsCrouching = Animator.StringToHash("IsCrouching");
        animIdIsFlying = Animator.StringToHash("IsFlying");
        animIdIsGrounded = Animator.StringToHash("IsGrounded");
        animIdMoveSpeed = Animator.StringToHash("MoveSpeed");
        animIdMoveX = Animator.StringToHash("MoveX");
        animIdMoveY = Animator.StringToHash("MoveY");
    }

    public override void FixedUpdateNetwork()
    {
        if (!isInitialized || playerController == null) return;

        // Only the State Authority updates networked animation state
        if (Object.HasStateAuthority)
        {
            UpdateNetworkedAnimationState();
        }
    }

    void UpdateNetworkedAnimationState()
    {
        // Sync animation state with PlayerController
        NetworkIsMoving = playerController.NetworkIsMoving;
        NetworkIsSprinting = playerController.NetworkIsSprinting;
        NetworkIsCrouching = playerController.NetworkIsCrouching;
        NetworkIsFlying = playerController.NetworkIsFlying;
        NetworkIsGrounded = playerController.IsGrounded;
        NetworkMoveSpeed = playerController.CurrentSpeed;

        // Calculate movement direction based on velocity
        Vector3 velocity = playerController.Velocity;
        Vector3 localVelocity = transform.InverseTransformDirection(velocity);
        NetworkMoveDirection = new Vector2(localVelocity.x, localVelocity.z);
    }

    public override void Render()
    {
        if (!isInitialized || avatarAnimator == null) return;

        // Update avatar visibility based on perspective
        UpdateAvatarVisibility();

        // Update animator parameters for smooth animation
        if (enableNetworkedAnimations)
        {
            UpdateAnimatorParameters();
        }

        // Ensure avatar follows player rotation
        if (followPlayerRotation && avatarInstance != null)
        {
            avatarInstance.transform.localRotation = Quaternion.identity;
        }
    }

    void UpdateAvatarVisibility()
    {
        if (avatarRenderers == null || !Object.HasStateAuthority) return;

        bool shouldHide = hideInFirstPerson && playerController.IsFirstPerson;

        foreach (var renderer in avatarRenderers)
        {
            if (renderer != null)
            {
                renderer.enabled = !shouldHide;
            }
        }
    }

    void UpdateAnimatorParameters()
    {
        if (avatarAnimator == null) return;

        // Use cached parameter IDs for Unity 6+ performance
        avatarAnimator.SetBool(animIdIsMoving, NetworkIsMoving);
        avatarAnimator.SetBool(animIdIsSprinting, NetworkIsSprinting);
        avatarAnimator.SetBool(animIdIsCrouching, NetworkIsCrouching);
        avatarAnimator.SetBool(animIdIsFlying, NetworkIsFlying);
        avatarAnimator.SetBool(animIdIsGrounded, NetworkIsGrounded);
        avatarAnimator.SetFloat(animIdMoveSpeed, NetworkMoveSpeed);
        avatarAnimator.SetFloat(animIdMoveX, NetworkMoveDirection.x);
        avatarAnimator.SetFloat(animIdMoveY, NetworkMoveDirection.y);
    }

    // Public methods for external control
    public void SetAvatarFBX(GameObject newAvatarFBX)
    {
        if (!Object.HasStateAuthority)
        {
            Debug.LogWarning("Only the State Authority can change avatar FBX");
            return;
        }

        // Destroy existing avatar
        if (avatarInstance != null)
        {
            DestroyImmediate(avatarInstance);
        }

        // Set new avatar and reinitialize
        avatarFBX = newAvatarFBX;
        if (avatarFBX != null)
        {
            InitializeAvatar();
        }
    }

    public void SetAnimatorController(RuntimeAnimatorController controller)
    {
        defaultAnimatorController = controller;

        if (avatarAnimator != null)
        {
            avatarAnimator.runtimeAnimatorController = controller;
        }
    }

    public void SetAvatarScale(float scale)
    {
        avatarScaleMultiplier = scale;

        if (avatarInstance != null)
        {
            avatarInstance.transform.localScale = Vector3.one * scale;
        }
    }

    public void SetAvatarOffset(Vector3 offset)
    {
        avatarOffset = offset;

        if (avatarInstance != null)
        {
            avatarInstance.transform.localPosition = offset;
        }
    }

    // Utility methods
    public bool IsAvatarInitialized => isInitialized;
    public GameObject GetAvatarInstance() => avatarInstance;
    public Animator GetAvatarAnimator() => avatarAnimator;
    public Avatar GetAvatarAsset() => avatarAsset;

    // Animation trigger methods for external systems
    public void TriggerAnimation(string triggerName)
    {
        if (avatarAnimator != null && Object.HasStateAuthority)
        {
            avatarAnimator.SetTrigger(triggerName);
        }
    }

    public void SetAnimationBool(string parameterName, bool value)
    {
        if (avatarAnimator != null && Object.HasStateAuthority)
        {
            avatarAnimator.SetBool(parameterName, value);
        }
    }

    public void SetAnimationFloat(string parameterName, float value)
    {
        if (avatarAnimator != null && Object.HasStateAuthority)
        {
            avatarAnimator.SetFloat(parameterName, value);
        }
    }

    public void SetAnimationInt(string parameterName, int value)
    {
        if (avatarAnimator != null && Object.HasStateAuthority)
        {
            avatarAnimator.SetInteger(parameterName, value);
        }
    }

    void OnDestroy()
    {
        if (avatarInstance != null)
        {
            DestroyImmediate(avatarInstance);
        }
    }

    // Editor-friendly validation
    void OnValidate()
    {
        // Ensure scale multiplier stays positive
        if (avatarScaleMultiplier <= 0f)
        {
            avatarScaleMultiplier = 1f;
        }

        // Validate animation smooth time
        if (animationSmoothTime < 0f)
        {
            animationSmoothTime = 0.1f;
        }
    }
}