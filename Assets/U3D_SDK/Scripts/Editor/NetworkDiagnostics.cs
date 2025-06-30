using System;
using System.Net.Http;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;

public static class NetworkDiagnostics
{
    [MenuItem("U3D/Debug/Test Network Connection")]
    public static async void TestNetworkConnection()
    {
        Debug.Log("🔍 Starting Unity Editor network diagnostics...");

        // Test 1: Simple HttpClient to Google (should work)
        await TestSimpleConnection();

        // Test 2: Firebase config endpoint (your actual issue)
        await TestFirebaseConnection();

        // Test 3: Try alternative HTTP methods
        await TestAlternativeApproaches();
    }

    private static async Task TestSimpleConnection()
    {
        Debug.Log("📡 Test 1: Simple HTTP to Google...");
        try
        {
            using (var client = new HttpClient())
            {
                client.Timeout = TimeSpan.FromSeconds(10);
                var response = await client.GetAsync("https://www.google.com");
                Debug.Log($"✅ Google test: {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"❌ Google test failed: {ex.GetType().Name} - {ex.Message}");
            if (ex.InnerException != null)
            {
                Debug.LogError($"Inner: {ex.InnerException.GetType().Name} - {ex.InnerException.Message}");
            }
        }
    }

    private static async Task TestFirebaseConnection()
    {
        Debug.Log("🔥 Test 2: Firebase config endpoint...");
        try
        {
            using (var client = new HttpClient())
            {
                client.Timeout = TimeSpan.FromSeconds(10);
                var response = await client.GetAsync("https://unreality3d.web.app/api/config");
                Debug.Log($"✅ Firebase test: {response.StatusCode}");

                var content = await response.Content.ReadAsStringAsync();
                Debug.Log($"📄 Response length: {content.Length} characters");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"❌ Firebase test failed: {ex.GetType().Name} - {ex.Message}");
            if (ex.InnerException != null)
            {
                Debug.LogError($"Inner: {ex.InnerException.GetType().Name} - {ex.InnerException.Message}");
            }
        }
    }

    private static async Task TestAlternativeApproaches()
    {
        Debug.Log("🔬 Test 3: Alternative HTTP approaches...");

        // Test WebRequest (old but sometimes works when HttpClient doesn't)
        try
        {
            var request = System.Net.WebRequest.Create("https://unreality3d.web.app/api/config");
            request.Method = "GET";
            request.Timeout = 10000;

            using (var response = await request.GetResponseAsync())
            {
                Debug.Log($"✅ WebRequest test: Success");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"❌ WebRequest test failed: {ex.GetType().Name} - {ex.Message}");
        }

        // Test with different HttpClient configuration
        try
        {
            var handler = new HttpClientHandler()
            {
                UseProxy = false,
                UseCookies = false
            };

            using (var client = new HttpClient(handler))
            {
                client.Timeout = TimeSpan.FromSeconds(10);
                client.DefaultRequestHeaders.Add("User-Agent", "Unity-Test");

                var response = await client.GetAsync("https://unreality3d.web.app/api/config");
                Debug.Log($"✅ Alternative HttpClient test: {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"❌ Alternative HttpClient test failed: {ex.GetType().Name} - {ex.Message}");
        }
    }
}