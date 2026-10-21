using System;
using UnityEngine;

internal static class RebirthCamperCctvMouseInput
{
    // Explicit disposable GameBridge input transport only; absent in ordinary play.
    internal static Func<Vector2?> BridgeRawMouse;
    internal static Vector2 Read()
    {
        var supplied=BridgeRawMouse?.Invoke();
        return supplied??new Vector2(Input.GetAxis("Mouse X"),Input.GetAxis("Mouse Y"));
    }
}
