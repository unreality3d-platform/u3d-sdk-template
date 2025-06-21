using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace U3D.Networking
{
    /// <summary>
    /// Player nametag component that displays above networked players
    /// Shows username, user type badge, and PayPal verification status
    /// </summary>
    public class U3DPlayerNametag : MonoBehaviour
    {
        [Header("Nametag Configuration")]
        [SerializeField] private float displayDistance = 20f;
        [SerializeField] private float fadeDistance = 15f;
        [SerializeField] private bool alwaysVisible = false;
        [SerializeField] private Vector3 worldOffset = new Vector3(0, 2.5f, 0);

        [Header("UI References")]
        [SerializeField] private Canvas nametagCanvas;
        [SerializeField] private TextMeshProUGUI playerNameText;
        [SerializeField] private TextMeshProUGUI userTypeText;
        [SerializeField] private Image userTypeBadge;
        [SerializeField] private Image paypalBadge;
        [SerializeField] private CanvasGroup canvasGroup;

        // Runtime references
        private U3DNetworkedPlayer _networkedPlayer;
        private Camera _localPlayerCamera;
        private RectTransform _nametagRect;

        // State tracking
        private bool _isInitialized = false;
        private float _currentAlpha = 1f;

        public void Initialize(U3DNetworkedPlayer networkedPlayer)
        {
            _networkedPlayer = networkedPlayer;
            CreateNametagUI();
            FindLocalPlayerCamera();
            _isInitialized = true;
        }

        void CreateNametagUI()
        {
            // Create canvas if not assigned
            if (nametagCanvas == null)
            {
                var canvasObject = new GameObject("NametagCanvas");
                canvasObject.transform.SetParent(transform);
                canvasObject.transform.localPosition = Vector3.zero;

                nametagCanvas = canvasObject.AddComponent<Canvas>();
                nametagCanvas.renderMode = RenderMode.WorldSpace;
                nametagCanvas.worldCamera = null; // Will be set dynamically

                var canvasScaler = canvasObject.AddComponent<CanvasScaler>();
                canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
                canvasScaler.scaleFactor = 0.01f; // Scale down for world space

                canvasGroup = canvasObject.AddComponent<CanvasGroup>();
            }

            // Create main panel
            var panelObject = new GameObject("NametagPanel");
            panelObject.transform.SetParent(nametagCanvas.transform);

            _nametagRect = panelObject.AddComponent<RectTransform>();
            _nametagRect.anchorMin = new Vector2(0.5f, 0.5f);
            _nametagRect.anchorMax = new Vector2(0.5f, 0.5f);
            _nametagRect.sizeDelta = new Vector2(200, 60);
            _nametagRect.anchoredPosition = Vector2.zero;

            var panelImage = panelObject.AddComponent<Image>();
            panelImage.color = new Color(0, 0, 0, 0.7f);
            panelImage.raycastTarget = false;

            // Create player name text
            var nameObject = new GameObject("PlayerName");
            nameObject.transform.SetParent(panelObject.transform);

            var nameRect = nameObject.AddComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0, 0.5f);
            nameRect.anchorMax = new Vector2(1, 1);
            nameRect.offsetMin = new Vector2(5, 0);
            nameRect.offsetMax = new Vector2(-5, -2);

            playerNameText = nameObject.AddComponent<TextMeshProUGUI>();
            playerNameText.text = "Player";
            playerNameText.fontSize = 14;
            playerNameText.color = Color.white;
            playerNameText.alignment = TextAlignmentOptions.Center;
            playerNameText.raycastTarget = false;

            // Create user type text
            var typeObject = new GameObject("UserType");
            typeObject.transform.SetParent(panelObject.transform);

            var typeRect = typeObject.AddComponent<RectTransform>();
            typeRect.anchorMin = new Vector2(0, 0);
            typeRect.anchorMax = new Vector2(0.8f, 0.5f);
            typeRect.offsetMin = new Vector2(5, 2);
            typeRect.offsetMax = new Vector2(-5, 0);

            userTypeText = typeObject.AddComponent<TextMeshProUGUI>();
            userTypeText.text = "Visitor";
            userTypeText.fontSize = 10;
            userTypeText.color = Color.gray;
            userTypeText.alignment = TextAlignmentOptions.Left;
            userTypeText.raycastTarget = false;

            // Create PayPal badge
            var paypalObject = new GameObject("PayPalBadge");
            paypalObject.transform.SetParent(panelObject.transform);

            var paypalRect = paypalObject.AddComponent<RectTransform>();
            paypalRect.anchorMin = new Vector2(0.8f, 0);
            paypalRect.anchorMax = new Vector2(1, 0.5f);
            paypalRect.offsetMin = new Vector2(-20, 2);
            paypalRect.offsetMax = new Vector2(-2, 0);

            paypalBadge = paypalObject.AddComponent<Image>();
            paypalBadge.color = new Color(0, 0.5f, 1f, 0.8f); // PayPal blue
            paypalBadge.raycastTarget = false;

            // Add "PP" text to PayPal badge
            var ppTextObject = new GameObject("PPText");
            ppTextObject.transform.SetParent(paypalObject.transform);

            var ppTextRect = ppTextObject.AddComponent<RectTransform>();
            ppTextRect.anchorMin = Vector2.zero;
            ppTextRect.anchorMax = Vector2.one;
            ppTextRect.offsetMin = Vector2.zero;
            ppTextRect.offsetMax = Vector2.zero;

            var ppText = ppTextObject.AddComponent<TextMeshProUGUI>();
            ppText.text = "PP";
            ppText.fontSize = 8;
            ppText.color = Color.white;
            ppText.alignment = TextAlignmentOptions.Center;
            ppText.raycastTarget = false;
        }

        void FindLocalPlayerCamera()
        {
            // Find the local player's camera
            var localPlayer = FindAnyObjectByType<U3DPlayerController>();
            if (localPlayer != null)
            {
                _localPlayerCamera = localPlayer.GetComponentInChildren<Camera>();
            }

            // Fallback to main camera
            if (_localPlayerCamera == null)
            {
                _localPlayerCamera = Camera.main;
            }

            // Set canvas camera
            if (nametagCanvas != null && _localPlayerCamera != null)
            {
                nametagCanvas.worldCamera = _localPlayerCamera;
            }
        }

        void Update()
        {
            if (!_isInitialized || _networkedPlayer == null || _localPlayerCamera == null)
                return;

            // Update nametag position and rotation
            UpdatePosition();
            UpdateVisibility();
            UpdateRotation();
        }

        void UpdatePosition()
        {
            // Position nametag above player with world offset
            transform.position = _networkedPlayer.transform.position + worldOffset;
        }

        void UpdateVisibility()
        {
            if (alwaysVisible)
            {
                SetAlpha(1f);
                return;
            }

            // Calculate distance to local player camera
            float distance = Vector3.Distance(transform.position, _localPlayerCamera.transform.position);

            if (distance > displayDistance)
            {
                SetAlpha(0f);
            }
            else if (distance > fadeDistance)
            {
                // Fade out as distance increases
                float fadeAmount = 1f - ((distance - fadeDistance) / (displayDistance - fadeDistance));
                SetAlpha(fadeAmount);
            }
            else
            {
                SetAlpha(1f);
            }

            // Hide if behind camera or too close
            Vector3 directionToNametag = (transform.position - _localPlayerCamera.transform.position).normalized;
            float dotProduct = Vector3.Dot(_localPlayerCamera.transform.forward, directionToNametag);

            if (dotProduct < 0.1f || distance < 1f) // Behind camera or too close
            {
                SetAlpha(0f);
            }
        }

        void UpdateRotation()
        {
            // Always face the local player camera
            if (_localPlayerCamera != null)
            {
                Vector3 directionToCamera = _localPlayerCamera.transform.position - transform.position;
                directionToCamera.y = 0; // Keep nametag upright

                if (directionToCamera != Vector3.zero)
                {
                    transform.rotation = Quaternion.LookRotation(-directionToCamera);
                }
            }
        }

        void SetAlpha(float alpha)
        {
            _currentAlpha = alpha;

            if (canvasGroup != null)
            {
                canvasGroup.alpha = alpha;
            }
        }

        /// <summary>
        /// Update nametag display based on networked player data
        /// Called when network properties change
        /// </summary>
        public void UpdateDisplay()
        {
            if (_networkedPlayer == null) return;

            // Update player name
            if (playerNameText != null)
            {
                playerNameText.text = _networkedPlayer.GetDisplayName();
            }

            // Update user type
            if (userTypeText != null)
            {
                userTypeText.text = _networkedPlayer.GetUserTypeDisplayName();
                userTypeText.color = _networkedPlayer.GetUserTypeColor();
            }

            // Update PayPal badge visibility
            if (paypalBadge != null)
            {
                paypalBadge.gameObject.SetActive(_networkedPlayer.PayPalConnected);
            }
        }

        void OnDestroy()
        {
            // Cleanup any coroutines
            StopAllCoroutines();
        }
    }
}