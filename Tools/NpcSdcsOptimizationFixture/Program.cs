using System;

internal static class Program
{
    private static int checks;
    private static void Check(bool condition, string name)
    { if (!condition) throw new Exception(name); checks++; }
    private static bool Near(float a, float b) => Math.Abs(a - b) < 0.0001f;

    private static void Main()
    {
        var gate = new RebirthSdcsMeshRefreshGate();
        Check(!gate.TakeRequest(), "no work before an event");
        for (int i = 0; i < 20; i++) gate.Request();
        Check(gate.TakeRequest() && !gate.TakeRequest(), "twenty slot/snapshot events coalesce");
        Check(gate.BeginBuild(), "mesh build admitted");
        gate.Request(); gate.Request();
        Check(!gate.TakeRequest() && !gate.BeginBuild(), "native cosmetic callbacks cannot recursively rebuild");
        gate.EndBuild();
        Check(!gate.TakeRequest(), "callbacks inside a build do not schedule a duplicate");
        gate.Request();
        Check(gate.BeginBuild(), "direct appearance build consumes outstanding refresh");
        gate.EndBuild();
        Check(!gate.TakeRequest(), "initial appearance build consumes pending equipment event");
        try { Check(gate.BeginBuild(), "next build admitted"); throw new InvalidOperationException(); }
        catch (InvalidOperationException) { }
        finally { gate.EndBuild(); }
        gate.Request();
        Check(gate.TakeRequest() && gate.BeginBuild(), "exception cleanup releases build admission");
        gate.EndBuild();

        var sampler = new RebirthSdcsLocomotionSampler();
        Check(!sampler.Sample(0, 0, 0, 0, out _, out _), "first sample uses commanded fallback");
        Check(!sampler.Sample(0, 0.02f, 0, 0.02f, out _, out _), "tick interpolation does not create a sample early");
        Check(sampler.Sample(0, 0.2f, 0, 0.1f, out var fwd, out var strafe) && fwd > 0 && Near(strafe, 0), "forward travel at yaw zero");
        Check(sampler.Sample(0, 0.2f, 90, 0.11f, out var turnedFwd, out var turnedStrafe) &&
            Near(turnedFwd, 0) && Near(turnedStrafe, -fwd), "turn between windows reprojects world velocity");
        Check(sampler.Sample(0, 0.2f, 180, 0.12f, out var back, out _) && Near(back, -fwd), "backpedal sign survives zero displacement frame");
        sampler.Reset();
        sampler.Sample(0, 0, 90, 0, out _, out _);
        Check(sampler.Sample(0.2f, 0, 90, 0.1f, out fwd, out strafe) && fwd > 0 && Near(strafe, 0), "yaw uses degrees rather than radians");
        sampler.Reset(); sampler.Sample(0, 0, 0, 0, out _, out _);
        Check(sampler.Sample(0.2f, 0, 0, 0.1f, out fwd, out strafe) && Near(fwd, 0) && strafe > 0, "right strafe sign");
        for (int i = 2; i <= 30; i++) sampler.Sample(0.2f, 0, 0, i * 0.11f, out fwd, out strafe);
        Check(Near(fwd, 0) && Near(strafe, 0), "standing still settles exactly to idle");
        Check(!sampler.Sample(100, 100, 0, 3.4f, out fwd, out strafe) && Near(fwd, 0) && Near(strafe, 0), "teleport clears previous movement");
        Check(!sampler.Sample(100, 100, 0, 3.41f, out _, out _), "teleport needs a new complete window");
        Check(sampler.Sample(100, 100, 0, 3.51f, out fwd, out strafe) && Near(fwd, 0) && Near(strafe, 0), "teleport does not leak stale velocity");
        Check(!sampler.Sample(100, 100, 0, 2, out _, out _), "time rewind reanchors");
        Check(!sampler.Sample(100, 100, 0, 4, out _, out _), "long paused frame reanchors");
        Check(!sampler.Sample(float.NaN, 0, 0, 5, out _, out _), "invalid coordinates reset sample");
        Check(!sampler.Sample(0, 0, float.PositiveInfinity, 5, out _, out _), "invalid yaw reset sample");
        Check(!sampler.Sample(0, 0, 0, float.NaN, out _, out _), "invalid clock reset sample");
        sampler.Sample(0, 0, 0, 0, out _, out _);
        sampler.Sample(0, 0.2f, 0, 0.1f, out fwd, out _);
        Check(Near(fwd, 2f * (1f - (float)Math.Exp(-0.1 / 0.15))), "smoothing coefficient matches elapsed time");
        Console.WriteLine("PASS: " + checks + " SDCS refresh admission and locomotion checks (production source, no game input).");
    }
}
