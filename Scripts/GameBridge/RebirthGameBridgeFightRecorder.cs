using InControl;
using System;
using System.IO;
using UnityEngine;

#nullable disable

/// <summary>
/// One CSV row per game tick of a fight, written to Tools/GameBridge/out/fights/. The API result stays small (it is cut at 64 KB); everything needed
/// to understand a fight - distances, the zombie's attack state and speed, what the bot was doing and which inputs it held, stamina, health, and
/// events (swing presses, hit frames, damage taken) - is in the file. The human recorder (REBIRTH_BRIDGE_HUMAN=1) writes the same columns, so a
/// bot fight and a recorded player can be compared side by side.
/// </summary>
public static class RebirthGameBridgeFightRecorder
{
    private static StreamWriter w;
    private static float t0;
    public static string Path = "";
    private static string pendingEvent = "";

    public const string Header = "t,state,action,dist,zDy,zSwing,zStun,zSpeed,zHp,myHp,stamina,swingBusy,yaw,pitch,fwd,back,left,right,run,attack,power,event";

    public static void Begin(string label)
    {
        End();
        try
        {
            string dir = System.IO.Path.Combine(RebirthGameBridge.OutputDir, "fights");
            Directory.CreateDirectory(dir);
            Path = System.IO.Path.Combine(dir, DateTime.Now.ToString("yyyyMMdd-HHmmss") + "_" + (label ?? "fight") + ".csv");
            w = new StreamWriter(Path, false) { AutoFlush = false };
            w.WriteLine(Header);
            t0 = Time.realtimeSinceStartup;
        }
        catch { w = null; }
    }

    public static void Event(string text) { pendingEvent = string.IsNullOrEmpty(pendingEvent) ? text : pendingEvent + "|" + text; }

    public static void Row(string state, string action, EntityPlayerLocal p, EntityAlive z, float dist, bool zSwing, bool zStun, float zSpeed, float stamina, bool swingBusy, PlayerActionsLocal a)
    {
        if (w == null) return;
        try
        {
            string ev = pendingEvent; pendingEvent = "";
            w.WriteLine(string.Join(",", new[] {
                (Time.realtimeSinceStartup - t0).ToString("0.000"), state ?? "", (action ?? "").Replace(",", " "), dist.ToString("0.00"), (z.position.y - p.position.y).ToString("0.00"),
                zSwing ? "1" : "0", zStun ? "1" : "0", zSpeed.ToString("0.0"), z.Health.ToString("0"), p.Health.ToString("0"), stamina.ToString("0"), swingBusy ? "1" : "0",
                p.rotation.y.ToString("0.0"), p.rotation.x.ToString("0.0"),
                Held(a.MoveForward), Held(a.MoveBack), Held(a.MoveLeft), Held(a.MoveRight), Held(a.Run), Held(a.Primary), Held(a.Secondary), ev.Replace(",", " ") }));
        }
        catch { }
    }

    private static string Held(PlayerAction x) { return RebirthGameBridgeInput.IsHeld(x) ? "1" : "0"; }

    public static void End()
    {
        try { if (w != null) { w.Flush(); w.Dispose(); } } catch { }
        w = null;
    }
}
