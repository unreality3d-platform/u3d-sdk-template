using System;
using System.Runtime.InteropServices;
using TMPro;
using U3D.Editor;
using UnityEngine;
using UnityEngine.UI;

namespace U3D
{
    /// <summary>
    /// Core component for PayPal dual transaction processing.
    /// Handles automatic 95%/5% payment splitting between creator and platform.
    /// </summary>
    public class PayPalDualTransaction : MonoBehaviour
    {
        [Header("Transaction Configuration")]
        [SerializeField] private string itemName = "Premium Content";
        [SerializeField] private string itemDescription = "Creator content purchase";
        [SerializeField] private float itemPrice = 5.00f;
        [SerializeField] private bool allowVariableAmount = false;
        [SerializeField] private float minimumAmount = 1.00f;
        [SerializeField] private float maximumAmount = 100.00f;

        [Header("UI References")]
        [SerializeField] private Button paymentButton;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private TMP_Text priceText;
        [SerializeField] private TMP_InputField amountInputField;
        [SerializeField] private GameObject loadingIndicator;

        [Header("Events")]
        public UnityEngine.Events.UnityEvent OnPaymentSuccess;
        public UnityEngine.Events.UnityEvent OnPaymentFailed;
        public UnityEngine.Events.UnityEvent<string> OnStatusChanged;

        // JavaScript bridge imports
        [DllImport("__Internal")]
        private static extern void UnityStartDualTransaction(string itemName, string itemDescription, string price, string transactionId);

        [DllImport("__Internal")]
        private static extern void UnityCheckAuthenticationStatus();

        private string currentTransactionId;
        private bool isProcessing = false;
        private string creatorPayPalEmail;

        private void Start()
        {
            InitializeComponent();
            ValidateSetup();
        }

        private void InitializeComponent()
        {
            // Get creator PayPal email from setup
            creatorPayPalEmail = SetupTab.GetCreatorPayPalEmail();

            // Setup UI
            if (paymentButton != null)
            {
                paymentButton.onClick.AddListener(StartPayment);
            }

            if (amountInputField != null)
            {
                amountInputField.gameObject.SetActive(allowVariableAmount);
                amountInputField.onValueChanged.AddListener(OnAmountChanged);
                amountInputField.text = itemPrice.ToString("F2");
            }

            UpdateUI();
        }

        private void ValidateSetup()
        {
            if (string.IsNullOrEmpty(creatorPayPalEmail))
            {
                SetStatus("⚠️ PayPal email not configured. Please complete setup first.");
                if (paymentButton != null)
                    paymentButton.interactable = false;
                return;
            }

            if (itemPrice < 0.01f)
            {
                SetStatus("⚠️ Invalid price configuration.");
                if (paymentButton != null)
                    paymentButton.interactable = false;
                return;
            }

            SetStatus("💳 Ready to accept payments");
        }

        private void UpdateUI()
        {
            if (priceText != null)
            {
                if (allowVariableAmount)
                {
                    priceText.text = $"${minimumAmount:F2} - ${maximumAmount:F2}";
                }
                else
                {
                    priceText.text = $"${itemPrice:F2}";
                }
            }

            if (paymentButton != null)
            {
                var buttonText = paymentButton.GetComponentInChildren<TMP_Text>();
                if (buttonText != null)
                {
                    if (allowVariableAmount)
                    {
                        buttonText.text = "Send Payment";
                    }
                    else
                    {
                        buttonText.text = $"Pay ${itemPrice:F2}";
                    }
                }
            }
        }

        public void StartPayment()
        {
            if (isProcessing)
            {
                Debug.LogWarning("Payment already in progress");
                return;
            }

            float finalAmount = GetFinalAmount();
            if (!ValidateAmount(finalAmount))
            {
                return;
            }

            if (string.IsNullOrEmpty(creatorPayPalEmail))
            {
                SetStatus("❌ Creator PayPal email not configured");
                return;
            }

            // Generate unique transaction ID
            currentTransactionId = Guid.NewGuid().ToString();

            SetProcessingState(true);
            SetStatus("🔄 Initializing payment...");

            try
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                // Check authentication first
                UnityCheckAuthenticationStatus();
#else
                // Editor testing
                Debug.Log($"[EDITOR] Would start dual transaction: {itemName} - ${finalAmount:F2}");
                Debug.Log($"[EDITOR] Creator email: {creatorPayPalEmail}");
                Debug.Log($"[EDITOR] Creator amount: ${(finalAmount * 0.95f):F2}");
                Debug.Log($"[EDITOR] Platform amount: ${(finalAmount * 0.05f):F2}");

                // Simulate success in editor
                StartCoroutine(SimulateEditorPayment());
#endif
            }
            catch (Exception ex)
            {
                Debug.LogError($"Payment initialization failed: {ex.Message}");
                SetStatus("❌ Payment initialization failed");
                SetProcessingState(false);
            }
        }

        private System.Collections.IEnumerator SimulateEditorPayment()
        {
            yield return new UnityEngine.WaitForSeconds(2f);
            OnPaymentComplete("true");
        }

        private float GetFinalAmount()
        {
            if (allowVariableAmount && amountInputField != null)
            {
                if (float.TryParse(amountInputField.text, out float userAmount))
                {
                    return userAmount;
                }
                return minimumAmount;
            }
            return itemPrice;
        }

        private bool ValidateAmount(float amount)
        {
            if (allowVariableAmount)
            {
                if (amount < minimumAmount)
                {
                    SetStatus($"❌ Minimum amount is ${minimumAmount:F2}");
                    return false;
                }
                if (amount > maximumAmount)
                {
                    SetStatus($"❌ Maximum amount is ${maximumAmount:F2}");
                    return false;
                }
            }

            if (amount < 0.01f)
            {
                SetStatus("❌ Amount must be at least $0.01");
                return false;
            }

            return true;
        }

        private void OnAmountChanged(string value)
        {
            if (float.TryParse(value, out float amount))
            {
                UpdateUI();
            }
        }

        // Called by JavaScript when authentication check completes
        public void OnAuthenticationChecked(string isAuthenticated)
        {
            if (isAuthenticated == "true")
            {
                ContinueWithPayment();
            }
            else
            {
                SetStatus("❌ Please log in to make payments");
                SetProcessingState(false);
            }
        }

        private void ContinueWithPayment()
        {
            float finalAmount = GetFinalAmount();

            SetStatus("💳 Starting PayPal payment...");

            try
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                UnityStartDualTransaction(
                    itemName,
                    itemDescription,
                    finalAmount.ToString("F2"),
                    currentTransactionId
                );
#endif
            }
            catch (Exception ex)
            {
                Debug.LogError($"PayPal payment failed: {ex.Message}");
                SetStatus("❌ Payment failed to start");
                SetProcessingState(false);
            }
        }

        // Called by JavaScript when payment completes
        public void OnPaymentComplete(string success)
        {
            SetProcessingState(false);

            if (success == "true")
            {
                SetStatus("✅ Payment successful!");
                OnPaymentSuccess?.Invoke();

                // Disable payment button if this is a one-time purchase
                if (!allowVariableAmount && paymentButton != null)
                {
                    paymentButton.interactable = false;
                    var buttonText = paymentButton.GetComponentInChildren<TMP_Text>();
                    if (buttonText != null)
                    {
                        buttonText.text = "✅ Paid";
                    }
                }

                Debug.Log($"Dual transaction completed successfully for {itemName}");
            }
            else
            {
                SetStatus("❌ Payment failed. Please try again.");
                OnPaymentFailed?.Invoke();
                Debug.LogWarning($"Dual transaction failed for {itemName}");
            }
        }

        // Called by JavaScript with transaction details
        public void OnTransactionDetails(string transactionData)
        {
            try
            {
                var data = JsonUtility.FromJson<TransactionDetails>(transactionData);
                Debug.Log($"Transaction details received: Creator: {data.creatorTransactionId}, Platform: {data.platformTransactionId}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Failed to parse transaction details: {ex.Message}");
            }
        }

        private void SetStatus(string message)
        {
            if (statusText != null)
            {
                statusText.text = message;
            }
            OnStatusChanged?.Invoke(message);
            Debug.Log($"PayPal Status: {message}");
        }

        private void SetProcessingState(bool processing)
        {
            isProcessing = processing;

            if (paymentButton != null)
            {
                paymentButton.interactable = !processing && !string.IsNullOrEmpty(creatorPayPalEmail);
            }

            if (loadingIndicator != null)
            {
                loadingIndicator.SetActive(processing);
            }

            if (amountInputField != null)
            {
                amountInputField.interactable = !processing;
            }
        }

        // Public methods for external configuration
        public void SetItemDetails(string name, string description, float price)
        {
            itemName = name;
            itemDescription = description;
            itemPrice = price;
            UpdateUI();
        }

        public void SetVariableAmount(bool enabled, float min = 1.00f, float max = 100.00f)
        {
            allowVariableAmount = enabled;
            minimumAmount = min;
            maximumAmount = max;

            if (amountInputField != null)
            {
                amountInputField.gameObject.SetActive(enabled);
            }

            UpdateUI();
        }

        public void SetCreatorPayPalEmail(string email)
        {
            creatorPayPalEmail = email;
            ValidateSetup();
        }

        public string GetCreatorPayPalEmail()
        {
            return creatorPayPalEmail;
        }

        public float GetCreatorAmount(float totalAmount)
        {
            return totalAmount * 0.95f;
        }

        public float GetPlatformAmount(float totalAmount)
        {
            return totalAmount * 0.05f;
        }

        // Helper class for transaction data
        [Serializable]
        private class TransactionDetails
        {
            public string creatorTransactionId;
            public string platformTransactionId;
            public float creatorAmount;
            public float platformAmount;
            public float totalAmount;
        }

        // Inspector validation
        private void OnValidate()
        {
            if (itemPrice < 0)
                itemPrice = 0;

            if (minimumAmount < 0.01f)
                minimumAmount = 0.01f;

            if (maximumAmount < minimumAmount)
                maximumAmount = minimumAmount;
        }
    }
}