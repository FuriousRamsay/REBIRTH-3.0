using System;
// Internal inventory capture disabled after player verified the performance fix.
public static class RebirthInventoryTiming
{
    public static void Start() { }
    public static Scope Measure(int id) => default;
    public struct Scope : IDisposable { public void Dispose() { } }
}