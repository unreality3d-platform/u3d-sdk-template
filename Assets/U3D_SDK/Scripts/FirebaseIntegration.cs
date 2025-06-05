using UnityEngine;
using UnityEngine.UI;
using System.Runtime.InteropServices;

public class FirebaseIntegration : MonoBehaviour
{
    [Header("UI References")]
    public Button testButton;
    public Button paymentButton;
    public Text statusText;

    [Header("Content Settings")]
    public string contentId = "test-area-1";
    public float contentPrice = 5.99f;

    // JavaScript function imports - these will be checked at runtime
    [DllImport("__Internal")]
    private static extern void UnityCallTestFunction();

    [DllImport("__Internal")]
    private static extern void UnityCheckContentAccess(string contentId);

    [DllImport("__Internal")]
    private static extern void UnityRequestPayment(string contentId, string price);

    void Start()
    {
        // Set up button listeners
        if (testButton != null)
            testButton.onClick.AddListener(TestFirebaseConnection);

        if (paymentButton != null)
            paymentButton.onClick.AddListener(RequestPayment);

        // Check content access on start
        CheckContentAccess();
    }

    public void TestFirebaseConnection()
    {
        UpdateStatus("Testing Firebase connection...");

        try
        {
            UnityCallTestFunction();
        }
        catch (System.Exception e)
        {
            UpdateStatus("Firebase function not available: " + e.Message);
        }
    }

    public void CheckContentAccess()
    {
        UpdateStatus("Checking content access...");

        try
        {
            UnityCheckContentAccess(contentId);
        }
        catch (System.Exception e)
        {
            UpdateStatus("Access check failed: " + e.Message);
        }
    }

    public void RequestPayment()
    {
        UpdateStatus("Processing payment request...");

        try
        {
            UnityRequestPayment(contentId, contentPrice.ToString());
        }
        catch (System.Exception e)
        {
            UpdateStatus("Payment system not available: " + e.Message);
        }
    }

    public void UpdateStatus(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
        }

        Debug.Log($"Firebase Integration: {message}");
    }

    // Called by JavaScript when test function completes
    public void OnTestComplete(string result)
    {
        UpdateStatus($"Test Result: {result}");
    }

    // Called by JavaScript when access check completes
    public void OnAccessCheckComplete(string hasAccess)
    {
        if (hasAccess == "true")
        {
            UpdateStatus("Access granted - Welcome!");
            if (paymentButton != null)
                paymentButton.gameObject.SetActive(false);
        }
        else
        {
            UpdateStatus($"Payment required: ${contentPrice}");
            if (paymentButton != null)
                paymentButton.gameObject.SetActive(true);
        }
    }

    // Called by JavaScript when payment completes
    public void OnPaymentComplete(string success)
    {
        if (success == "true")
        {
            UpdateStatus("Payment successful! Access granted.");
            if (paymentButton != null)
                paymentButton.gameObject.SetActive(false);
        }
        else
        {
            UpdateStatus("Payment failed. Please try again.");
        }
    }
}