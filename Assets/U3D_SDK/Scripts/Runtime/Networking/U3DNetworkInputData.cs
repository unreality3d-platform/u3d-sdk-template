using Fusion;
using UnityEngine;

/// <summary>
/// Network input data structure for Fusion 2
/// This replaces direct Unity Input System calls in networked gameplay
/// </summary>
public struct U3DNetworkInputData : INetworkInput
{
    public Vector2 MovementInput;
    public Vector2 LookInput;
    public NetworkButtons Buttons;
}

/// <summary>
/// Button definitions for network input
/// </summary>
public enum U3DInputButtons
{
    Jump = 0,
    Sprint = 1,
    Crouch = 2,
    Fly = 3,
    AutoRun = 4,
    Interact = 5,
    Teleport = 6,
    Zoom = 7,
    PerspectiveSwitch = 8
}