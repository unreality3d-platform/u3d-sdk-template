using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;

namespace U3D.Editor
{
    public class MonetizationToolsCategory : IToolCategory
    {
        public string CategoryName => "Monetization";
        private List<CreatorTool> tools;

        public MonetizationToolsCategory()
        {
            tools = new List<CreatorTool>
            {
                new CreatorTool("💳 Add Purchase Button", "Single item PayPal purchase with dual transaction (95% to creator)", CreatePurchaseButton, true),
                new CreatorTool("🎁 Add Tip Jar", "Accept variable donations with dual transaction splitting", CreateTipJar, true),
                new CreatorTool("🚪 Add Scene Gate", "Scene entry payment gate with PayPal dual transaction", CreateSceneGate, true),
                new CreatorTool("🛍️ Add Shop Object", "3D world PayPal shop with multiple items and dual transactions", CreateShopObject, true),
                new CreatorTool("🎫 Add Event Gate", "Timed event access with PayPal dual transaction", CreateEventGate, true),
                new CreatorTool("📺 Add Screen Shop", "Screen overlay PayPal shop interface with dual transactions", CreateScreenShop, true)
            };
        }

        public List<CreatorTool> GetTools() => tools;

        public void DrawCategory()
        {
            EditorGUILayout.LabelField("💰 Monetization Tools", EditorStyles.boldLabel);

            // Check PayPal setup status
            string paypalEmail = SetupTab.GetCreatorPayPalEmail();
            bool paypalConfigured = !string.IsNullOrEmpty(paypalEmail);

            if (paypalConfigured)
            {
                EditorGUILayout.HelpBox(
                    $"✅ PayPal Connected: {paypalEmail}\n\n" +
                    "🚀 Dual Transaction System Ready:\n" +
                    "• You keep 95% of all earnings\n" +
                    "• Platform fee: 5% (for hosting & infrastructure)\n" +
                    "• Automatic payment splitting\n" +
                    "• Direct payments to your PayPal account",
                    MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "⚠️ PayPal Not Configured\n\n" +
                    "To enable monetization:\n" +
                    "1. Go to the Setup tab\n" +
                    "2. Add your PayPal email address\n" +
                    "3. Return here to add payment tools\n\n" +
                    "You'll keep 95% of all earnings!",
                    MessageType.Warning);

                EditorGUILayout.Space(5);

                if (GUILayout.Button("🔧 Go to Setup Tab", GUILayout.Height(30)))
                {
                    Debug.Log("Navigate to Setup tab to configure PayPal");
                }

                EditorGUILayout.Space(10);
            }

            EditorGUILayout.Space(10);

            foreach (var tool in tools)
            {
                // Only show disabled state visually - don't modify the tool object
                bool originallyEnabled = paypalConfigured;

                if (!paypalConfigured)
                {
                    EditorGUI.BeginDisabledGroup(true);
                }

                ProjectToolsTab.DrawCategoryTool(tool);

                if (!paypalConfigured)
                {
                    EditorGUI.EndDisabledGroup();
                }
            }

            if (!paypalConfigured)
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.HelpBox(
                    "💡 All monetization tools require PayPal configuration to function.",
                    MessageType.Info);
            }
        }

        #region Tool Creation Methods

        private void CreatePurchaseButton()
        {
            // Check PayPal configuration
            if (!ValidatePayPalSetup()) return;

            GameObject buttonObject = CreatePaymentUI("Purchase Button", "💳", CreatePurchaseButtonUI);

            // Add the dual transaction component
            var dualTransaction = buttonObject.AddComponent<PayPalDualTransaction>();

            // Configure for single purchase
            dualTransaction.SetItemDetails("Premium Content", "Creator content purchase", 5.00f);
            dualTransaction.SetVariableAmount(false);

            LogToolCreation("Purchase Button", "Single-purchase payment button with 95%/5% split");
        }

        private void CreateTipJar()
        {
            if (!ValidatePayPalSetup()) return;

            GameObject tipJarObject = CreatePaymentUI("Tip Jar", "🎁", CreateTipJarUI);

            var dualTransaction = tipJarObject.AddComponent<PayPalDualTransaction>();

            // Configure for variable donations
            dualTransaction.SetItemDetails("Creator Tip", "Support this creator's work", 5.00f);
            dualTransaction.SetVariableAmount(true, 1.00f, 100.00f);

            LogToolCreation("Tip Jar", "Variable donation system with 95%/5% split");
        }

        private void CreateSceneGate()
        {
            if (!ValidatePayPalSetup()) return;

            GameObject gateObject = CreatePaymentUI("Scene Gate", "🚪", CreateSceneGateUI);

            var dualTransaction = gateObject.AddComponent<PayPalDualTransaction>();
            var gateController = gateObject.AddComponent<SceneGateController>();

            // Configure for scene entry
            dualTransaction.SetItemDetails("Scene Access", "Premium scene entry fee", 3.00f);
            dualTransaction.SetVariableAmount(false);

            // Connect payment success to gate opening
            dualTransaction.OnPaymentSuccess.AddListener(gateController.OpenGate);

            LogToolCreation("Scene Gate", "Entry payment gate with automatic unlocking");
        }

        private void CreateShopObject()
        {
            if (!ValidatePayPalSetup()) return;

            GameObject shopObject = CreatePaymentUI("Shop Object", "🛍️", CreateShopObjectUI);

            var shopController = shopObject.AddComponent<ShopController>();

            LogToolCreation("Shop Object", "Multi-item 3D shop with individual dual transactions");
        }

        private void CreateEventGate()
        {
            if (!ValidatePayPalSetup()) return;

            GameObject eventObject = CreatePaymentUI("Event Gate", "🎫", CreateEventGateUI);

            var dualTransaction = eventObject.AddComponent<PayPalDualTransaction>();
            var eventController = eventObject.AddComponent<EventGateController>();

            // Configure for event access
            dualTransaction.SetItemDetails("Event Access", "Special event ticket", 10.00f);
            dualTransaction.SetVariableAmount(false);

            // Connect payment success to event access
            dualTransaction.OnPaymentSuccess.AddListener(eventController.GrantAccess);

            LogToolCreation("Event Gate", "Timed event access with payment verification");
        }

        private void CreateScreenShop()
        {
            if (!ValidatePayPalSetup()) return;

            GameObject screenShop = CreateScreenOverlayUI("Screen Shop", "📺", CreateScreenShopUI);

            var screenShopController = screenShop.AddComponent<ScreenShopController>();

            LogToolCreation("Screen Shop", "Overlay shop interface with dual transaction support");
        }

        #endregion

        #region UI Creation Helpers

        private GameObject CreatePaymentUI(string name, string icon, System.Action<GameObject> customSetup)
        {
            // Find or create Canvas using Unity 6+ method
            Canvas canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                var canvasObject = new GameObject("Canvas");
                canvas = canvasObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                canvasObject.AddComponent<CanvasScaler>();
                canvasObject.AddComponent<GraphicRaycaster>();
            }

            // Create main container using Unity's built-in methods
            var uiResources = new DefaultControls.Resources();
            GameObject container = DefaultControls.CreatePanel(uiResources);
            container.name = name;
            container.transform.SetParent(canvas.transform, false);

            // Configure container
            var containerRect = container.GetComponent<RectTransform>();
            containerRect.sizeDelta = new Vector2(300, 200);
            containerRect.anchoredPosition = Vector2.zero;

            // Create header with icon and title
            CreateHeaderUI(container, icon, name);

            // Custom setup for specific tool type
            customSetup?.Invoke(container);

            // Select the created object
            Selection.activeGameObject = container;

            return container;
        }

        private GameObject CreateScreenOverlayUI(string name, string icon, System.Action<GameObject> customSetup)
        {
            // Find or create Canvas (Screen Space Overlay) using Unity 6+ method
            Canvas canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                var canvasObject = new GameObject("UI Canvas");
                canvas = canvasObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasObject.AddComponent<CanvasScaler>();
                canvasObject.AddComponent<GraphicRaycaster>();
            }

            var uiResources = new DefaultControls.Resources();
            GameObject container = DefaultControls.CreatePanel(uiResources);
            container.name = name;
            container.transform.SetParent(canvas.transform, false);

            // Configure for screen overlay
            var containerRect = container.GetComponent<RectTransform>();
            containerRect.anchorMin = new Vector2(0.5f, 0.5f);
            containerRect.anchorMax = new Vector2(0.5f, 0.5f);
            containerRect.sizeDelta = new Vector2(400, 300);
            containerRect.anchoredPosition = Vector2.zero;

            CreateHeaderUI(container, icon, name);
            customSetup?.Invoke(container);

            Selection.activeGameObject = container;
            return container;
        }

        private void CreateHeaderUI(GameObject parent, string icon, string title)
        {
            var uiResources = new DefaultControls.Resources();

            // Create header panel
            GameObject header = DefaultControls.CreatePanel(uiResources);
            header.name = "Header";
            header.transform.SetParent(parent.transform, false);

            var headerRect = header.GetComponent<RectTransform>();
            headerRect.anchorMin = new Vector2(0, 0.8f);
            headerRect.anchorMax = new Vector2(1, 1);
            headerRect.offsetMin = Vector2.zero;
            headerRect.offsetMax = Vector2.zero;

            // Set header color
            var headerImage = header.GetComponent<Image>();
            if (headerImage != null)
                headerImage.color = new Color(0.2f, 0.3f, 0.4f, 0.8f);

            // Create title text
            GameObject titleText = DefaultControls.CreateText(uiResources);
            titleText.name = "Title";
            titleText.transform.SetParent(header.transform, false);

            var titleRect = titleText.GetComponent<RectTransform>();
            titleRect.anchorMin = Vector2.zero;
            titleRect.anchorMax = Vector2.one;
            titleRect.offsetMin = new Vector2(10, 0);
            titleRect.offsetMax = new Vector2(-10, 0);

            var titleTMP = SetupTextMeshPro(titleText, $"{icon} {title}", 16);
            titleTMP.alignment = TextAlignmentOptions.MidlineLeft;
            titleTMP.color = Color.white;
        }

        private void CreatePurchaseButtonUI(GameObject container)
        {
            var uiResources = new DefaultControls.Resources();

            // Create price text
            GameObject priceText = DefaultControls.CreateText(uiResources);
            priceText.name = "PriceText";
            priceText.transform.SetParent(container.transform, false);

            var priceRect = priceText.GetComponent<RectTransform>();
            priceRect.anchorMin = new Vector2(0, 0.5f);
            priceRect.anchorMax = new Vector2(1, 0.7f);
            priceRect.offsetMin = new Vector2(10, 0);
            priceRect.offsetMax = new Vector2(-10, 0);

            var priceTMP = SetupTextMeshPro(priceText, "$5.00", 18);
            priceTMP.alignment = TextAlignmentOptions.Center;

            // Create payment button
            GameObject button = DefaultControls.CreateButton(uiResources);
            button.name = "PaymentButton";
            button.transform.SetParent(container.transform, false);

            var buttonRect = button.GetComponent<RectTransform>();
            buttonRect.anchorMin = new Vector2(0.1f, 0.1f);
            buttonRect.anchorMax = new Vector2(0.9f, 0.4f);
            buttonRect.offsetMin = Vector2.zero;
            buttonRect.offsetMax = Vector2.zero;

            var buttonTMP = SetupTextMeshPro(button.transform.GetChild(0).gameObject, "💳 Purchase", 14);

            // Create status text
            CreateStatusText(container);
        }

        private void CreateTipJarUI(GameObject container)
        {
            var uiResources = new DefaultControls.Resources();

            // Create amount input field
            GameObject inputField = DefaultControls.CreateInputField(uiResources);
            inputField.name = "AmountInput";
            inputField.transform.SetParent(container.transform, false);

            var inputRect = inputField.GetComponent<RectTransform>();
            inputRect.anchorMin = new Vector2(0.1f, 0.6f);
            inputRect.anchorMax = new Vector2(0.9f, 0.75f);
            inputRect.offsetMin = Vector2.zero;
            inputRect.offsetMax = Vector2.zero;

            var inputField_component = inputField.GetComponent<TMP_InputField>();
            if (inputField_component == null)
            {
                // Convert legacy InputField to TMP_InputField
                var legacyInput = inputField.GetComponent<InputField>();
                if (legacyInput != null)
                {
                    Object.DestroyImmediate(legacyInput);
                }
                inputField_component = inputField.AddComponent<TMP_InputField>();
            }

            inputField_component.text = "5.00";
            inputField_component.contentType = TMP_InputField.ContentType.DecimalNumber;

            // Add placeholder
            var placeholder = inputField.transform.Find("Placeholder")?.GetComponent<TextMeshProUGUI>();
            if (placeholder != null)
            {
                placeholder.text = "Enter amount...";
            }

            // Create tip button
            GameObject button = DefaultControls.CreateButton(uiResources);
            button.name = "TipButton";
            button.transform.SetParent(container.transform, false);

            var buttonRect = button.GetComponent<RectTransform>();
            buttonRect.anchorMin = new Vector2(0.1f, 0.35f);
            buttonRect.anchorMax = new Vector2(0.9f, 0.55f);
            buttonRect.offsetMin = Vector2.zero;
            buttonRect.offsetMax = Vector2.zero;

            SetupTextMeshPro(button.transform.GetChild(0).gameObject, "🎁 Send Tip", 14);

            CreateStatusText(container);
        }

        private void CreateSceneGateUI(GameObject container)
        {
            var uiResources = new DefaultControls.Resources();

            // Create description text
            GameObject descText = DefaultControls.CreateText(uiResources);
            descText.name = "DescriptionText";
            descText.transform.SetParent(container.transform, false);

            var descRect = descText.GetComponent<RectTransform>();
            descRect.anchorMin = new Vector2(0, 0.6f);
            descRect.anchorMax = new Vector2(1, 0.75f);
            descRect.offsetMin = new Vector2(10, 0);
            descRect.offsetMax = new Vector2(-10, 0);

            var descTMP = SetupTextMeshPro(descText, "Premium Scene Access Required", 12);
            descTMP.alignment = TextAlignmentOptions.Center;

            // Create unlock button
            GameObject button = DefaultControls.CreateButton(uiResources);
            button.name = "UnlockButton";
            button.transform.SetParent(container.transform, false);

            var buttonRect = button.GetComponent<RectTransform>();
            buttonRect.anchorMin = new Vector2(0.1f, 0.35f);
            buttonRect.anchorMax = new Vector2(0.9f, 0.55f);
            buttonRect.offsetMin = Vector2.zero;
            buttonRect.offsetMax = Vector2.zero;

            SetupTextMeshPro(button.transform.GetChild(0).gameObject, "🚪 Unlock Scene", 14);

            CreateStatusText(container);
        }

        private void CreateShopObjectUI(GameObject container)
        {
            var uiResources = new DefaultControls.Resources();

            // Create shop title
            GameObject shopTitle = DefaultControls.CreateText(uiResources);
            shopTitle.name = "ShopTitle";
            shopTitle.transform.SetParent(container.transform, false);

            var titleRect = shopTitle.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0, 0.7f);
            titleRect.anchorMax = new Vector2(1, 0.85f);
            titleRect.offsetMin = new Vector2(10, 0);
            titleRect.offsetMax = new Vector2(-10, 0);

            var titleTMP = SetupTextMeshPro(shopTitle, "🛍️ Creator Shop", 14);
            titleTMP.alignment = TextAlignmentOptions.Center;

            // Create scroll view for items
            GameObject scrollView = DefaultControls.CreateScrollView(uiResources);
            scrollView.name = "ItemScrollView";
            scrollView.transform.SetParent(container.transform, false);

            var scrollRect = scrollView.GetComponent<RectTransform>();
            scrollRect.anchorMin = new Vector2(0.05f, 0.2f);
            scrollRect.anchorMax = new Vector2(0.95f, 0.65f);
            scrollRect.offsetMin = Vector2.zero;
            scrollRect.offsetMax = Vector2.zero;

            CreateStatusText(container);
        }

        private void CreateEventGateUI(GameObject container)
        {
            var uiResources = new DefaultControls.Resources();

            // Create event info text
            GameObject eventInfo = DefaultControls.CreateText(uiResources);
            eventInfo.name = "EventInfo";
            eventInfo.transform.SetParent(container.transform, false);

            var infoRect = eventInfo.GetComponent<RectTransform>();
            infoRect.anchorMin = new Vector2(0, 0.6f);
            infoRect.anchorMax = new Vector2(1, 0.75f);
            infoRect.offsetMin = new Vector2(10, 0);
            infoRect.offsetMax = new Vector2(-10, 0);

            var infoTMP = SetupTextMeshPro(eventInfo, "🎫 Special Event Access", 12);
            infoTMP.alignment = TextAlignmentOptions.Center;

            // Create timer text
            GameObject timerText = DefaultControls.CreateText(uiResources);
            timerText.name = "TimerText";
            timerText.transform.SetParent(container.transform, false);

            var timerRect = timerText.GetComponent<RectTransform>();
            timerRect.anchorMin = new Vector2(0, 0.45f);
            timerRect.anchorMax = new Vector2(1, 0.6f);
            timerRect.offsetMin = new Vector2(10, 0);
            timerRect.offsetMax = new Vector2(-10, 0);

            var timerTMP = SetupTextMeshPro(timerText, "⏰ Event Active", 10);
            timerTMP.alignment = TextAlignmentOptions.Center;
            timerTMP.color = Color.green;

            // Create access button
            GameObject button = DefaultControls.CreateButton(uiResources);
            button.name = "AccessButton";
            button.transform.SetParent(container.transform, false);

            var buttonRect = button.GetComponent<RectTransform>();
            buttonRect.anchorMin = new Vector2(0.1f, 0.2f);
            buttonRect.anchorMax = new Vector2(0.9f, 0.4f);
            buttonRect.offsetMin = Vector2.zero;
            buttonRect.offsetMax = Vector2.zero;

            SetupTextMeshPro(button.transform.GetChild(0).gameObject, "🎫 Buy Ticket", 14);

            CreateStatusText(container);
        }

        private void CreateScreenShopUI(GameObject container)
        {
            var uiResources = new DefaultControls.Resources();

            // Create close button
            GameObject closeButton = DefaultControls.CreateButton(uiResources);
            closeButton.name = "CloseButton";
            closeButton.transform.SetParent(container.transform, false);

            var closeRect = closeButton.GetComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(0.9f, 0.9f);
            closeRect.anchorMax = new Vector2(1f, 1f);
            closeRect.offsetMin = Vector2.zero;
            closeRect.offsetMax = Vector2.zero;

            SetupTextMeshPro(closeButton.transform.GetChild(0).gameObject, "✕", 16);

            // Create shop content area
            GameObject contentArea = DefaultControls.CreateScrollView(uiResources);
            contentArea.name = "ShopContent";
            contentArea.transform.SetParent(container.transform, false);

            var contentRect = contentArea.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0.05f, 0.1f);
            contentRect.anchorMax = new Vector2(0.95f, 0.85f);
            contentRect.offsetMin = Vector2.zero;
            contentRect.offsetMax = Vector2.zero;

            CreateStatusText(container);
        }

        private void CreateStatusText(GameObject container)
        {
            var uiResources = new DefaultControls.Resources();

            GameObject statusText = DefaultControls.CreateText(uiResources);
            statusText.name = "StatusText";
            statusText.transform.SetParent(container.transform, false);

            var statusRect = statusText.GetComponent<RectTransform>();
            statusRect.anchorMin = new Vector2(0, 0);
            statusRect.anchorMax = new Vector2(1, 0.15f);
            statusRect.offsetMin = new Vector2(10, 5);
            statusRect.offsetMax = new Vector2(-10, -5);

            var statusTMP = SetupTextMeshPro(statusText, "💳 Ready to accept payments", 10);
            statusTMP.alignment = TextAlignmentOptions.Center;
            statusTMP.color = new Color(0.7f, 0.7f, 0.7f);
        }

        private TextMeshProUGUI SetupTextMeshPro(GameObject textObject, string text, float fontSize)
        {
            // Remove legacy Text component if present
            var legacyText = textObject.GetComponent<Text>();
            if (legacyText != null)
            {
                Object.DestroyImmediate(legacyText);
            }

            // Add or get TextMeshProUGUI component
            var tmp = textObject.GetComponent<TextMeshProUGUI>();
            if (tmp == null)
            {
                tmp = textObject.AddComponent<TextMeshProUGUI>();
            }

            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.MidlineLeft;

            return tmp;
        }

        #endregion

        #region Helper Components

        // Scene Gate Controller Component
        public class SceneGateController : MonoBehaviour
        {
            [SerializeField] private GameObject gateObject;
            [SerializeField] private bool isOpen = false;

            public void OpenGate()
            {
                isOpen = true;
                if (gateObject != null)
                {
                    gateObject.SetActive(false);
                }
                Debug.Log("Scene gate opened - access granted!");

                // Update UI
                var statusText = GetComponentInChildren<TextMeshProUGUI>();
                if (statusText != null && statusText.name == "StatusText")
                {
                    statusText.text = "✅ Access granted!";
                    statusText.color = Color.green;
                }
            }

            public bool IsOpen() => isOpen;
        }

        // Shop Controller Component
        public class ShopController : MonoBehaviour
        {
            [System.Serializable]
            public class ShopItem
            {
                public string itemName = "Item";
                public string description = "Shop item description";
                public float price = 5.00f;
                public Sprite icon;
            }

            [SerializeField] private List<ShopItem> shopItems = new List<ShopItem>();
            [SerializeField] private Transform itemContainer;

            private void Start()
            {
                SetupDefaultItems();
                CreateItemUI();
            }

            private void SetupDefaultItems()
            {
                if (shopItems.Count == 0)
                {
                    shopItems.Add(new ShopItem { itemName = "Premium Avatar", description = "Exclusive creator avatar", price = 10.00f });
                    shopItems.Add(new ShopItem { itemName = "Special Effect", description = "Unique visual effect", price = 5.00f });
                    shopItems.Add(new ShopItem { itemName = "Creator Pack", description = "Bundle of creator content", price = 15.00f });
                }
            }

            private void CreateItemUI()
            {
                // Find the scroll view content area
                var scrollView = GetComponentInChildren<ScrollRect>();
                if (scrollView != null)
                {
                    itemContainer = scrollView.content;
                }

                if (itemContainer == null) return;

                foreach (var item in shopItems)
                {
                    CreateShopItemButton(item);
                }
            }

            private void CreateShopItemButton(ShopItem item)
            {
                var uiResources = new DefaultControls.Resources();
                GameObject itemButton = DefaultControls.CreateButton(uiResources);
                itemButton.name = $"Item_{item.itemName}";
                itemButton.transform.SetParent(itemContainer, false);

                // Configure button layout
                var buttonRect = itemButton.GetComponent<RectTransform>();
                buttonRect.sizeDelta = new Vector2(250, 40);

                // Create dual transaction component for this item
                var dualTransaction = itemButton.AddComponent<PayPalDualTransaction>();
                dualTransaction.SetItemDetails(item.itemName, item.description, item.price);
                dualTransaction.SetVariableAmount(false);

                // Update button text
                var buttonText = itemButton.GetComponentInChildren<TextMeshProUGUI>();
                if (buttonText != null)
                {
                    buttonText.text = $"{item.itemName} - ${item.price:F2}";
                }
            }
        }

        // Event Gate Controller Component
        public class EventGateController : MonoBehaviour
        {
            [SerializeField] private float eventDuration = 3600f; // 1 hour
            [SerializeField] private bool eventActive = true;
            [SerializeField] private bool accessGranted = false;

            private float timeRemaining;
            private TextMeshProUGUI timerText;

            private void Start()
            {
                timeRemaining = eventDuration;
                timerText = transform.Find("TimerText")?.GetComponent<TextMeshProUGUI>();
            }

            private void Update()
            {
                if (eventActive && timeRemaining > 0)
                {
                    timeRemaining -= Time.deltaTime;
                    UpdateTimerDisplay();

                    if (timeRemaining <= 0)
                    {
                        EndEvent();
                    }
                }
            }

            private void UpdateTimerDisplay()
            {
                if (timerText != null)
                {
                    int minutes = Mathf.FloorToInt(timeRemaining / 60);
                    int seconds = Mathf.FloorToInt(timeRemaining % 60);
                    timerText.text = $"⏰ {minutes:00}:{seconds:00} remaining";
                }
            }

            private void EndEvent()
            {
                eventActive = false;
                if (timerText != null)
                {
                    timerText.text = "⏰ Event Ended";
                    timerText.color = Color.red;
                }

                // Disable payment button
                var button = GetComponentInChildren<Button>();
                if (button != null)
                {
                    button.interactable = false;
                    var buttonText = button.GetComponentInChildren<TextMeshProUGUI>();
                    if (buttonText != null)
                    {
                        buttonText.text = "Event Ended";
                    }
                }
            }

            public void GrantAccess()
            {
                if (eventActive)
                {
                    accessGranted = true;
                    Debug.Log("Event access granted!");

                    var statusText = transform.Find("StatusText")?.GetComponent<TextMeshProUGUI>();
                    if (statusText != null)
                    {
                        statusText.text = "✅ Event access granted!";
                        statusText.color = Color.green;
                    }
                }
            }

            public bool HasAccess() => accessGranted && eventActive;
        }

        // Screen Shop Controller Component
        public class ScreenShopController : MonoBehaviour
        {
            [SerializeField] private bool isVisible = true;

            private void Start()
            {
                // Setup close button functionality
                var closeButton = transform.Find("CloseButton")?.GetComponent<Button>();
                if (closeButton != null)
                {
                    closeButton.onClick.AddListener(CloseShop);
                }

                // Setup shop content
                SetupShopContent();
            }

            private void SetupShopContent()
            {
                var scrollView = GetComponentInChildren<ScrollRect>();
                if (scrollView != null)
                {
                    var content = scrollView.content;
                    // Add shop items to content area
                    // This would be similar to ShopController.CreateItemUI()
                }
            }

            public void CloseShop()
            {
                gameObject.SetActive(false);
                isVisible = false;
            }

            public void OpenShop()
            {
                gameObject.SetActive(true);
                isVisible = true;
            }

            public bool IsVisible() => isVisible;
        }

        #endregion

        #region Validation and Logging

        private bool ValidatePayPalSetup()
        {
            string paypalEmail = SetupTab.GetCreatorPayPalEmail();
            if (string.IsNullOrEmpty(paypalEmail))
            {
                EditorUtility.DisplayDialog(
                    "PayPal Setup Required",
                    "Please configure your PayPal email in the Setup tab before creating monetization tools.\n\n" +
                    "This enables the dual transaction system where you keep 95% of earnings.",
                    "OK"
                );
                return false;
            }
            return true;
        }

        private void LogToolCreation(string toolName, string description)
        {
            Debug.Log($"✅ Created {toolName}: {description}");
            Debug.Log($"💰 Dual transaction system ready - Creator keeps 95%, Platform fee: 5%");

            EditorUtility.DisplayDialog(
                "Monetization Tool Created!",
                $"{toolName} has been created successfully.\n\n" +
                "✅ Dual Transaction System:\n" +
                "• You keep 95% of all payments\n" +
                "• Platform fee: 5%\n" +
                "• Automatic payment splitting\n" +
                "• Direct PayPal payments\n\n" +
                "Configure the component settings to customize pricing and behavior.",
                "Great!"
            );
        }

        #endregion
    }
}