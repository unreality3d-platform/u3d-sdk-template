using UnityEngine;
using UnityEngine.InputSystem;

namespace U3D
{
    /// <summary>
    /// WebGL-specific cursor management for handling locked/unlocked states
    /// Enables Tab to unlock for UI, click to re-lock for FPS controls
    /// Manages double-click teleportation across cursor states
    /// </summary>
    public class U3DWebGLCursorManager : MonoBehaviour
    {
        [Header("WebGL Cursor Configuration")]
        [SerializeField] private bool enableWebGLCursorManagement = true;
        [SerializeField] private bool startWithLockedCursor = true;

        [Header("Input Actions")]
        [SerializeField] private InputActionReference pauseAction; // Tab key
        [SerializeField] private InputActionReference escapeAction; // Esc key  
        [SerializeField] private InputActionReference clickAction;
        [SerializeField] private InputActionReference teleportAction;

        [Header("UI References")]
        [SerializeField] private GameObject pauseMenu;
        [SerializeField] private Canvas gameUI;

        // Cursor state management
        private bool _isCursorLocked = true;
        private bool _isUIMode = false; // Tab mode - cursor free but WebGL has focus
        private bool _isEscapedMode = false; // Esc mode - cursor free and WebGL lost focus
        private Vector2 _savedMousePosition;

        // Teleport tracking for unlocked cursor mode
        private float _lastClickTime = 0f;
        private Vector2 _lastClickPosition;
        private bool _waitingForSecondClick = false;
        private const float DOUBLE_CLICK_TIME = 0.5f;
        private const float DOUBLE_CLICK_DISTANCE = 50f; // pixels

        // Events
        public static event System.Action<bool> OnCursorLockStateChanged;
        public static event System.Action OnTeleportRequested;

        // Public properties
        public bool IsCursorLocked => _isCursorLocked;
        public bool IsUIMode => _isUIMode;
        public bool IsEscapedMode => _isEscapedMode;

        void Awake()
        {
            // Only enable on WebGL builds
            if (Application.platform != RuntimePlatform.WebGLPlayer)
            {
                enableWebGLCursorManagement = false;
                enabled = false;
                return;
            }

            // Setup initial cursor state
            if (startWithLockedCursor)
            {
                SetCursorLocked(true);
            }
        }

        void OnEnable()
        {
            if (!enableWebGLCursorManagement) return;

            // Subscribe to input actions
            if (pauseAction != null)
            {
                pauseAction.action.performed += OnTabPressed;
                pauseAction.action.Enable();
            }

            if (escapeAction != null)
            {
                escapeAction.action.performed += OnEscapePressed;
                escapeAction.action.Enable();
            }

            if (clickAction != null)
            {
                clickAction.action.performed += OnClickPressed;
                clickAction.action.Enable();
            }

            // Handle teleport action differently based on cursor state
            if (teleportAction != null)
            {
                teleportAction.action.performed += OnTeleportActionPerformed;
                teleportAction.action.Enable();
            }
        }

        void OnDisable()
        {
            if (!enableWebGLCursorManagement) return;

            // Unsubscribe from input actions
            if (pauseAction != null)
            {
                pauseAction.action.performed -= OnTabPressed;
                pauseAction.action.Disable();
            }

            if (escapeAction != null)
            {
                escapeAction.action.performed -= OnEscapePressed;
                escapeAction.action.Disable();
            }

            if (clickAction != null)
            {
                clickAction.action.performed -= OnClickPressed;
                clickAction.action.Disable();
            }

            if (teleportAction != null)
            {
                teleportAction.action.performed -= OnTeleportActionPerformed;
                teleportAction.action.Disable();
            }
        }

        void Update()
        {
            if (!enableWebGLCursorManagement) return;

            // Handle manual double-click detection when cursor is unlocked
            if (!_isCursorLocked && _waitingForSecondClick)
            {
                if (Time.time - _lastClickTime > DOUBLE_CLICK_TIME)
                {
                    _waitingForSecondClick = false;
                }
            }

            // Detect when player clicks back into WebGL window after Esc
            if (_isEscapedMode && Input.GetMouseButtonDown(0))
            {
                OnWebGLWindowRegainedFocus();
            }
        }

        void OnTabPressed(InputAction.CallbackContext context)
        {
            ToggleUIMode();
        }

        void OnEscapePressed(InputAction.CallbackContext context)
        {
            // Esc key - release cursor and lose WebGL focus
            SetEscapedMode(true);
        }

        void OnClickPressed(InputAction.CallbackContext context)
        {
            if (_isEscapedMode)
            {
                // Click detected - WebGL regaining focus
                OnWebGLWindowRegainedFocus();
                return;
            }

            if (_isUIMode)
            {
                // Check if we clicked on UI elements
                if (!IsPointerOverUI())
                {
                    // Clicked on game area - return to game mode
                    SetUIMode(false);
                }
                else
                {
                    // Handle manual double-click detection for teleportation
                    HandleManualDoubleClick();
                }
            }
            else if (!_isCursorLocked)
            {
                // Cursor unlocked but not in UI mode - handle teleportation
                HandleManualDoubleClick();
            }
        }

        void OnTeleportActionPerformed(InputAction.CallbackContext context)
        {
            // This is called when MultiTap interaction succeeds (cursor locked mode)
            if (_isCursorLocked)
            {
                OnTeleportRequested?.Invoke();
            }
        }

        void HandleManualDoubleClick()
        {
            Vector2 currentMousePos = Mouse.current.position.ReadValue();
            float currentTime = Time.time;

            if (_waitingForSecondClick)
            {
                // Check if this is a valid second click
                float timeDelta = currentTime - _lastClickTime;
                float distanceDelta = Vector2.Distance(currentMousePos, _lastClickPosition);

                if (timeDelta <= DOUBLE_CLICK_TIME && distanceDelta <= DOUBLE_CLICK_DISTANCE)
                {
                    // Valid double-click detected!
                    OnTeleportRequested?.Invoke();
                    _waitingForSecondClick = false;
                    Debug.Log("✅ Manual double-click teleport detected");
                }
                else
                {
                    // Reset and start new click sequence
                    _lastClickTime = currentTime;
                    _lastClickPosition = currentMousePos;
                    _waitingForSecondClick = true;
                }
            }
            else
            {
                // First click
                _lastClickTime = currentTime;
                _lastClickPosition = currentMousePos;
                _waitingForSecondClick = true;
            }
        }

        public void ToggleUIMode()
        {
            if (_isEscapedMode) return; // Can't toggle while escaped
            SetUIMode(!_isUIMode);
        }

        public void SetUIMode(bool uiMode)
        {
            if (_isEscapedMode) return; // Can't change UI mode while escaped

            _isUIMode = uiMode;

            if (_isUIMode)
            {
                // Entering UI mode - unlock cursor but keep WebGL focus
                _savedMousePosition = Mouse.current.position.ReadValue();
                SetCursorLocked(false);

                // Show pause menu
                if (pauseMenu != null)
                    pauseMenu.SetActive(true);

                Debug.Log("🎯 Entered UI Mode (Tab) - Cursor free, WebGL focused");
            }
            else
            {
                // Exiting UI mode - lock cursor
                SetCursorLocked(true);

                // Hide pause menu
                if (pauseMenu != null)
                    pauseMenu.SetActive(false);

                Debug.Log("🎯 Exited UI Mode - Cursor locked for FPS controls");
            }
        }

        public void SetEscapedMode(bool escapedMode)
        {
            _isEscapedMode = escapedMode;

            if (_isEscapedMode)
            {
                // Esc pressed - release cursor completely
                _isUIMode = false; // Clear UI mode
                SetCursorLocked(false);

                // Hide all game UI
                if (pauseMenu != null)
                    pauseMenu.SetActive(false);
                if (gameUI != null)
                    gameUI.enabled = false;

                Debug.Log("🚪 Escaped WebGL - Cursor free, browser has control");
            }
            else
            {
                // Returning from escape - restore game UI
                if (gameUI != null)
                    gameUI.enabled = true;

                Debug.Log("🎯 Returned to WebGL - Ready to resume");
            }
        }

        void OnWebGLWindowRegainedFocus()
        {
            if (_isEscapedMode)
            {
                // Player clicked back into WebGL window
                SetEscapedMode(false);
                SetCursorLocked(true); // Resume FPS mode
                Debug.Log("🎯 WebGL Window Regained Focus - Resuming FPS controls");
            }
        }

        void SetCursorLocked(bool locked)
        {
            _isCursorLocked = locked;

            if (locked)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            else
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            OnCursorLockStateChanged?.Invoke(locked);
        }

        bool IsPointerOverUI()
        {
            // Check if mouse is over UI elements
            return UnityEngine.EventSystems.EventSystem.current != null &&
                   UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();
        }

        // Public methods for external control
        public void RequestCursorLock()
        {
            if (!_isUIMode)
            {
                SetCursorLocked(true);
            }
        }

        public void RequestCursorUnlock()
        {
            SetCursorLocked(false);
        }

        // Method for NetworkManager integration
        public bool ShouldProcessTeleportInput()
        {
            // Only process teleport input when in active game modes
            return enableWebGLCursorManagement && !_isEscapedMode;
        }

        // Public method to check if game input should be processed
        public bool ShouldProcessGameInput()
        {
            return !_isEscapedMode && (!_isUIMode || _isCursorLocked);
        }
    }
}