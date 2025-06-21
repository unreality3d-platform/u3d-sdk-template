using UnityEngine;
using Fusion;
using U3D;

namespace U3D.Networking
{
    /// <summary>
    /// Networked player component that synchronizes U3DPlayerController over network
    /// Handles position, rotation, and animation state sync optimized for WebGL
    /// </summary>
    public class U3DNetworkedPlayer : NetworkBehaviour
    {
        [Header("Network Synchronization")]
        [SerializeField] private bool syncPosition = true;
        [SerializeField] private bool syncRotation = true;
        [SerializeField] private bool syncAnimation = true;
        [SerializeField] private bool syncVoiceActivity = false;

        [Header("WebGL Optimization")]
        [SerializeField] private float positionThreshold = 0.1f;
        [SerializeField] private float rotationThreshold = 1f;
        [SerializeField] private bool useClientPrediction = true;
        [SerializeField] private float interpolationSpeed = 10f;

        [Header("Visual Components")]
        [SerializeField] private GameObject localPlayerVisuals;
        [SerializeField] private GameObject remotePlayerVisuals;
        [SerializeField] private Transform nametagAnchor;
        [SerializeField] private Canvas playerUI;

        // Networked Properties
        [Networked] public Vector3 NetworkPosition { get; set; }
        [Networked] public Quaternion NetworkRotation { get; set; }
        [Networked] public bool IsMoving { get; set; }
        [Networked] public bool IsSprinting { get; set; }
        [Networked] public bool IsCrouching { get; set; }
        [Networked] public bool IsFlying { get; set; }
        [Networked] public float CameraPitch { get; set; }
        [Networked] public bool IsInteracting { get; set; }

        // User Information
        [Networked] public NetworkString<_16> PlayerName { get; set; }
        [Networked] public NetworkString<_8> UserType { get; set; } // "creator", "visitor", "seller"
        [Networked] public bool PayPalConnected { get; set; }

        // Components
        private U3DPlayerController _playerController;
        private CharacterController _characterController;
        private Camera _playerCamera;
        private Animator _animator;
        private U3DPlayerNametag _nametag;

        // Network State
        private Vector3 _networkPositionBuffer;
        private Quaternion _networkRotationBuffer;
        private bool _isLocalPlayer;

        // Performance tracking
        private float _lastPositionSendTime;
        private Vector3 _lastSentPosition;
        private Quaternion _lastSentRotation;

        // Change detection tracking
        private NetworkString<_16> _previousPlayerName;
        private NetworkString<_8> _previousUserType;
        private bool _previousPayPalConnected;

        public override void Spawned()
        {
            // Determine if this is the local player
            _isLocalPlayer = Object.HasInputAuthority;

            // Get components
            _playerController = GetComponent<U3DPlayerController>();
            _characterController = GetComponent<CharacterController>();
            _playerCamera = GetComponentInChildren<Camera>();
            _animator = GetComponentInChildren<Animator>();

            // Configure for local vs remote player
            ConfigurePlayerType();

            // Initialize nametag
            InitializeNametag();

            // Set initial network state for local player
            if (_isLocalPlayer)
            {
                NetworkPosition = transform.position;
                NetworkRotation = transform.rotation;

                // Set player info from Firebase (will be implemented)
                SetPlayerInfo("Player", "visitor", false);
            }

            // Initialize change detection
            _previousPlayerName = PlayerName;
            _previousUserType = UserType;
            _previousPayPalConnected = PayPalConnected;

            Debug.Log($"Networked player spawned - Local: {_isLocalPlayer}");
        }

        void ConfigurePlayerType()
        {
            if (_isLocalPlayer)
            {
                // Local player configuration
                if (localPlayerVisuals != null)
                    localPlayerVisuals.SetActive(true);
                if (remotePlayerVisuals != null)
                    remotePlayerVisuals.SetActive(false);

                // Enable player controller for local player
                if (_playerController != null)
                    _playerController.enabled = true;

                // Keep local camera active
                if (_playerCamera != null)
                    _playerCamera.enabled = true;

                // Show local UI
                if (playerUI != null)
                    playerUI.enabled = true;
            }
            else
            {
                // Remote player configuration
                if (localPlayerVisuals != null)
                    localPlayerVisuals.SetActive(false);
                if (remotePlayerVisuals != null)
                    remotePlayerVisuals.SetActive(true);

                // Disable player controller for remote players
                if (_playerController != null)
                    _playerController.enabled = false;

                // Disable remote camera
                if (_playerCamera != null)
                    _playerCamera.enabled = false;

                // Hide remote player UI
                if (playerUI != null)
                    playerUI.enabled = false;

                // Disable character controller for remote players (we'll interpolate position)
                if (_characterController != null)
                    _characterController.enabled = false;
            }
        }

        void InitializeNametag()
        {
            if (nametagAnchor != null)
            {
                // Create nametag component
                var nametagObject = new GameObject("Nametag");
                nametagObject.transform.SetParent(nametagAnchor);
                nametagObject.transform.localPosition = Vector3.zero;

                _nametag = nametagObject.AddComponent<U3DPlayerNametag>();
                _nametag.Initialize(this);
            }
        }

        public override void FixedUpdateNetwork()
        {
            if (!_isLocalPlayer) return;

            // Send position/rotation updates only when changed significantly
            if (syncPosition && Vector3.Distance(transform.position, _lastSentPosition) > positionThreshold)
            {
                NetworkPosition = transform.position;
                _lastSentPosition = transform.position;
            }

            if (syncRotation && Quaternion.Angle(transform.rotation, _lastSentRotation) > rotationThreshold)
            {
                NetworkRotation = transform.rotation;
                _lastSentRotation = transform.rotation;
            }

            // Sync player state from controller
            if (_playerController != null)
            {
                IsMoving = _playerController.Velocity.magnitude > 0.1f;
                IsSprinting = _playerController.IsSprinting;
                IsCrouching = _playerController.IsCrouching;
                IsFlying = _playerController.IsFlying;

                // Sync camera pitch for remote players to see where player is looking
                if (_playerCamera != null)
                {
                    CameraPitch = _playerCamera.transform.localEulerAngles.x;
                }
            }
        }

        public override void Render()
        {
            if (_isLocalPlayer)
            {
                // Check for property changes on local player (for nametag updates)
                CheckForPropertyChanges();
                return;
            }

            // Interpolate remote player position and rotation
            if (syncPosition)
            {
                transform.position = Vector3.Lerp(transform.position, NetworkPosition,
                    Time.deltaTime * interpolationSpeed);
            }

            if (syncRotation)
            {
                transform.rotation = Quaternion.Lerp(transform.rotation, NetworkRotation,
                    Time.deltaTime * interpolationSpeed);
            }

            // Apply camera pitch for remote players (head movement)
            if (_playerCamera != null && syncRotation)
            {
                Vector3 cameraRotation = _playerCamera.transform.localEulerAngles;
                cameraRotation.x = CameraPitch;
                _playerCamera.transform.localEulerAngles = cameraRotation;
            }

            // Update animations
            UpdateAnimations();

            // Check for property changes on remote players
            CheckForPropertyChanges();
        }

        void CheckForPropertyChanges()
        {
            bool changed = false;

            // Check if player name changed
            if (!_previousPlayerName.Equals(PlayerName))
            {
                _previousPlayerName = PlayerName;
                changed = true;
            }

            // Check if user type changed
            if (!_previousUserType.Equals(UserType))
            {
                _previousUserType = UserType;
                changed = true;
            }

            // Check if PayPal status changed
            if (_previousPayPalConnected != PayPalConnected)
            {
                _previousPayPalConnected = PayPalConnected;
                changed = true;
            }

            // Update nametag if any properties changed
            if (changed && _nametag != null)
            {
                _nametag.UpdateDisplay();
            }
        }

        void UpdateAnimations()
        {
            if (_animator == null || !syncAnimation) return;

            // Set animation parameters based on network state
            _animator.SetBool("IsMoving", IsMoving);
            _animator.SetBool("IsSprinting", IsSprinting);
            _animator.SetBool("IsCrouching", IsCrouching);
            _animator.SetBool("IsFlying", IsFlying);
            _animator.SetBool("IsInteracting", IsInteracting);
        }

        /// <summary>
        /// Set player information for display
        /// Called from Firebase integration after authentication
        /// </summary>
        public void SetPlayerInfo(string playerName, string userType, bool paypalConnected)
        {
            if (!_isLocalPlayer) return;

            PlayerName = playerName;
            UserType = userType;
            PayPalConnected = paypalConnected;

            Debug.Log($"Player info set: {playerName}, {userType}, PayPal: {paypalConnected}");
        }

        /// <summary>
        /// Trigger interaction animation/state
        /// Called from U3DPlayerController.OnInteract()
        /// </summary>
        public void TriggerInteraction()
        {
            if (!_isLocalPlayer) return;

            IsInteracting = true;

            // Reset interaction flag after short duration
            StartCoroutine(ResetInteractionFlag());
        }

        System.Collections.IEnumerator ResetInteractionFlag()
        {
            yield return new WaitForSeconds(0.5f);
            IsInteracting = false;
        }

        /// <summary>
        /// Get player display information for UI
        /// </summary>
        public string GetDisplayName()
        {
            return !string.IsNullOrEmpty(PlayerName.ToString()) ? PlayerName.ToString() : "Unknown Player";
        }

        public string GetUserTypeDisplayName()
        {
            string userType = !string.IsNullOrEmpty(UserType.ToString()) ? UserType.ToString() : "visitor";

            return userType switch
            {
                "creator" => "Creator",
                "seller" => PayPalConnected ? "Verified Seller" : "Seller",
                "visitor" => "Visitor",
                _ => "Visitor"
            };
        }

        public Color GetUserTypeColor()
        {
            string userType = !string.IsNullOrEmpty(UserType.ToString()) ? UserType.ToString() : "visitor";

            return userType switch
            {
                "creator" => Color.cyan,
                "seller" => PayPalConnected ? Color.green : Color.yellow,
                "visitor" => Color.white,
                _ => Color.gray
            };
        }

        /// <summary>
        /// Handle network disconnect
        /// </summary>
        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            Debug.Log($"Networked player despawned - Local: {_isLocalPlayer}");

            // Cleanup nametag
            if (_nametag != null)
            {
                Destroy(_nametag.gameObject);
            }
        }

        // Debug visualization
        void OnDrawGizmos()
        {
            if (!Application.isPlaying) return;

            // Draw network position vs actual position for debugging
            if (!_isLocalPlayer && syncPosition)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(NetworkPosition, 0.2f);

                Gizmos.color = Color.blue;
                Gizmos.DrawWireSphere(transform.position, 0.15f);
            }
        }
    }
}