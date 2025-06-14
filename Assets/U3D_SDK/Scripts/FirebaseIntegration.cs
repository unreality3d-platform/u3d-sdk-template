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

    [DllImport("__Internal")]
    private static extern void UnityCallTestFunction();

    [DllImport("__Internal")]
    private static extern void UnityCheckContentAccess(string contentId);

    [DllImport("__Internal")]
    private static extern void UnityRequestPayment(string contentId, string price);

    void Start()
    {
        if (testButton != null)
            testButton.onClick.AddListener(TestFirebaseConnection);

        if (paymentButton != null)
            paymentButton.onClick.AddListener(RequestPayment);

        CheckContentAccess();
    }

    public void TestFirebaseConnection()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
            UpdateStatus("Testing Firebase connection...");
            try
            {
                UnityCallTestFunction();
            }
            catch (System.Exception e)
            {
                UpdateStatus("Firebase function not available: " + e.Message);
            }
#else
        UpdateStatus("Firebase testing requires WebGL build");
#endif
    }

    public void CheckContentAccess()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
            UpdateStatus("Checking content access...");
            try
            {
                UnityCheckContentAccess(contentId);
            }
            catch (System.Exception e)
            {
                UpdateStatus("Access check failed: " + e.Message);
            }
#else
        UpdateStatus("Ready for WebGL build - Firebase integration active");
#endif
    }

    public void RequestPayment()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
            UpdateStatus("Processing payment request...");
            try
            {
                UnityRequestPayment(contentId, contentPrice.ToString());
            }
            catch (System.Exception e)
            {
                UpdateStatus("Payment system not available: " + e.Message);
            }
#else
        UpdateStatus("PayPal integration requires WebGL build");
#endif
    }

    public void UpdateStatus(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
        }

        Debug.Log($"Firebase Integration: {message}");
    }

    public void OnTestComplete(string result)
    {
        UpdateStatus($"Test Result: {result}");
    }

    public void OnAccessCheckComplete(string hasAccess)
    {
        if (hasAccess == "true")
        {
            UpdateStatus("Access granted - Welcome!");
        }
        else
        {
            UpdateStatus("Payment required for access");
        }
    }

    public void OnPaymentComplete(string success)
    {
        if (success == "true")
        {
            UpdateStatus("Payment successful - Access granted!");
            CheckContentAccess();
        }
        else
        {
            UpdateStatus("Payment failed - Please try again");
        }
    }
}