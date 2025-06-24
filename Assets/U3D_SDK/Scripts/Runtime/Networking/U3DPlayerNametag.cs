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
        private U3DPlayerController _playerController;
        private Camera _localPlayerCamera;
        private RectTransform _nametagRect;

        // State tracking
        private bool _isInitialized = false;
        private float _currentAlpha = 1f;

        public void Initialize(U3DPlayerController playerController)
        {
            _playerController = playerController;
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

                // Set canvas size and scale properly for world space
                var canvasRect = nametagCanvas.GetComponent<RectTransform>();
                canvasRect.sizeDelta = new Vector2(200, 60);
                canvasRect.localScale = new Vector3(0.002f, 0.002f, 0.002f);

                canvasGroup = canvasObject.AddComponent<CanvasGroup>();
            }

            // Use Unity's built-in method to create panel
            var uiResources = new DefaultControls.Resources();
            var panelObject = DefaultControls.CreatePanel(uiResources);
            panelObject.name = "NametagPanel";
            panelObject.transform.SetParent(nametagCanvas.transform, false);

            _nametagRect = panelObject.GetComponent<RectTransform>();
            _nametagRect.anchorMin = Vector2.zero;
            _nametagRect.anchorMax = Vector2.one;
            _nametagRect.offsetMin = Vector2.zero;
            _nametagRect.offsetMax = Vector2.zero;

            // Set panel background
            var panelImage = panelObject.GetComponent<Image>();
            panelImage.color = new Color(0, 0, 0, 0.7f);
            panelImage.raycastTarget = false;

            // Use Unity's built-in method to create player name text
            var nameTextObject = DefaultControls.CreateText(uiResources);
            nameTextObject.name = "PlayerName";
            nameTextObject.transform.SetParent(panelObject.transform, false);

            var nameRect = nameTextObject.GetComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0, 0.5f);
            nameRect.anchorMax = new Vector2(1, 1);
            nameRect.offsetMin = new Vector2(5, 0);
            nameRect.offsetMax = new Vector2(-5, -2);

            // Replace Text with TextMeshPro
            DestroyImmediate(nameTextObject.GetComponent<Text>());
            playerNameText = nameTextObject.AddComponent<TextMeshProUGUI>();
            playerNameText.text = "Player";
            playerNameText.fontSize = 14;
            playerNameText.color = Color.white;
            playerNameText.alignment = TextAlignmentOptions.Center;
            playerNameText.raycastTarget = false;

            // Use Unity's built-in method to create user type text
            var typeTextObject = DefaultControls.CreateText(uiResources);
            typeTextObject.name = "UserType";
            typeTextObject.transform.SetParent(panelObject.transform, false);

            var typeRect = typeTextObject.GetComponent<RectTransform>();
            typeRect.anchorMin = new Vector2(0, 0);
            typeRect.anchorMax = new Vector2(0.8f, 0.5f);
            typeRect.offsetMin = new Vector2(5, 2);
            typeRect.offsetMax = new Vector2(-5, 0);

            // Replace Text with TextMeshPro
            DestroyImmediate(typeTextObject.GetComponent<Text>());
            userTypeText = typeTextObject.AddComponent<TextMeshProUGUI>();
            userTypeText.text = "Visitor";
            userTypeText.fontSize = 10;
            userTypeText.color = Color.gray;
            userTypeText.alignment = TextAlignmentOptions.Left;
            userTypeText.raycastTarget = false;

            // Use Unity's built-in method to create PayPal badge image
            var paypalBadgeObject = DefaultControls.CreateImage(uiResources);
            paypalBadgeObject.name = "PayPalBadge";
            paypalBadgeObject.transform.SetParent(panelObject.transform, false);

            var paypalRect = paypalBadgeObject.GetComponent<RectTransform>();
            paypalRect.anchorMin = new Vector2(0.8f, 0);
            paypalRect.anchorMax = new Vector2(1, 0.5f);
            paypalRect.offsetMin = new Vector2(-20, 2);
            paypalRect.offsetMax = new Vector2(-2, 0);

            paypalBadge = paypalBadgeObject.GetComponent<Image>();
            paypalBadge.color = new Color(0, 0.5f, 1f, 0.8f);
            paypalBadge.raycastTarget = false;

            // Use Unity's built-in method to create PP text
            var ppTextObject = DefaultControls.CreateText(uiResources);
            ppTextObject.name = "PPText";
            ppTextObject.transform.SetParent(paypalBadgeObject.transform, false);

            var ppTextRect = ppTextObject.GetComponent<RectTransform>();
            ppTextRect.anchorMin = Vector2.zero;
            ppTextRect.anchorMax = Vector2.one;
            ppTextRect.offsetMin = Vector2.zero;
            ppTextRect.offsetMax = Vector2.zero;

            // Replace Text with TextMeshPro
            DestroyImmediate(ppTextObject.GetComponent<Text>());
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
            if (!_isInitialized || _playerController == null || _localPlayerCamera == null)
                return;

            // Update nametag position and rotation
            UpdatePosition();
            UpdateVisibility();
            UpdateRotation();
        }

        void UpdatePosition()
        {
            // Position nametag above player with world offset
            transform.position = _playerController.transform.position + worldOffset;
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
            if (_playerController == null) return;

            // Update player name - placeholder until networking info is available
            if (playerNameText != null)
            {
                playerNameText.text = "Player"; // Will be updated when networking is integrated
            }

            // Update user type - placeholder
            if (userTypeText != null)
            {
                userTypeText.text = "Visitor";
                userTypeText.color = Color.white;
            }

            // Update PayPal badge visibility - placeholder
            if (paypalBadge != null)
            {
                paypalBadge.gameObject.SetActive(false);
            }
        }

        void OnDestroy()
        {
            // Cleanup any coroutines
            StopAllCoroutines();
        }
    }
}