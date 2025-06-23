// Create: Assets/Scripts/NetworkTesting/MultiPeerTester.cs
using UnityEngine;
using Fusion;

public class MultiPeerTester : MonoBehaviour
{
    [SerializeField] private int extraPeers = 2;
    [SerializeField] private KeyCode switchPeerKey = KeyCode.Tab;

    void Start()
    {
        // Only in Editor for testing
#if UNITY_EDITOR
        if (Application.isEditor)
        {
            Debug.Log($"🎮 Multi-Peer Testing Mode: {extraPeers + 1} players");
        }
#endif
    }

    void Update()
    {
#if UNITY_EDITOR
        // Switch between peers with number keys
        if (Input.GetKeyDown(KeyCode.Alpha1))
            UnityEditor.EditorApplication.ExecuteMenuItem("Tools/Network Project Config/Set Simulation Peer/0");
        if (Input.GetKeyDown(KeyCode.Alpha2))
            UnityEditor.EditorApplication.ExecuteMenuItem("Tools/Network Project Config/Set Simulation Peer/1");
        if (Input.GetKeyDown(KeyCode.Alpha3))
            UnityEditor.EditorApplication.ExecuteMenuItem("Tools/Network Project Config/Set Simulation Peer/2");
#endif
    }
}