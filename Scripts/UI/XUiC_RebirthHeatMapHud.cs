using UnityEngine;
using UnityEngine.Scripting;

#nullable disable

public static class RebirthHeatMapHudState
{
    public static float Activity;
    public static float Cooldown;
    public static bool Ready;
    public static bool Available;
    public static int RegionX;
    public static int RegionZ;
    public static int OwnerEntityId = -1;
    public static int LastRequestId;
    public static float LastUpdateTime;

    public static void ResetForOwner(int ownerEntityId)
    {
        Activity = 0f;
        Cooldown = 0f;
        Ready = false;
        Available = false;
        RegionX = 0;
        RegionZ = 0;
        OwnerEntityId = ownerEntityId;
        LastRequestId = 0;
        LastUpdateTime = 0f;
    }

    public static bool Receive(int ownerEntityId, int requestId, float activity, float cooldown, bool ready, bool available, int regionX, int regionZ)
    {
        if (OwnerEntityId != ownerEntityId)
            ResetForOwner(ownerEntityId);
        if (requestId < LastRequestId)
            return false;

        LastRequestId = requestId;
        Activity = activity;
        Cooldown = cooldown;
        Ready = ready;
        Available = available;
        RegionX = regionX;
        RegionZ = regionZ;
        LastUpdateTime = Time.time;
        return true;
    }
}

public static class RebirthHeatMapReader
{
    public static bool Read(World world, Vector3 position, out float activity, out float cooldown, out bool ready, out int regionX, out int regionZ)
    {
        activity = 0f; cooldown = 0f; ready = false;
        int chunkX = World.toChunkXZ((int)position.x);
        int chunkZ = World.toChunkXZ((int)position.z);
        regionX = FloorDiv(chunkX, 5); regionZ = FloorDiv(chunkZ, 5);
        if (world == null) return false;
        AIDirector director = world.GetAIDirector();
        if (director == null) return false;
        AIDirectorChunkData data;
        if (!RebirthFireHeatMapDestinationProbe.TryGetChunkData(director, new Vector3i(position), false, out data) || data == null)
            return false;
        activity = data.ActivityLevel;
        cooldown = data.cooldownDelay > 0f ? data.cooldownDelay : 0f;
        ready = data.IsReady;
        return true;
    }

    private static int FloorDiv(int value, int divisor)
    {
        int quotient = value / divisor;
        int remainder = value % divisor;
        return remainder != 0 && value < 0 ? quotient - 1 : quotient;
    }
}

[Preserve]
public sealed class XUiC_RebirthHeatMapHud : XUiController
{
    private XUiV_Label heatText;
    private float nextPoll;
    private float nextCountdownRender;
    private int requestSequence;
    private int ownerEntityId = -1;
    private string lastProjection;
    private const float PollSeconds = 3f;
    private const float ScoutThreshold = 25f;

    public override void Init()
    {
        base.Init();
        XUiController child = GetChildById("heatText");
        heatText = child != null ? child.ViewComponent as XUiV_Label : null;
    }

    public override void Update(float dt)
    {
        base.Update(dt);
        EntityPlayerLocal player = xui != null && xui.playerUI != null ? xui.playerUI.entityPlayer : null;
        bool enabled = player != null && player.Buffs.GetCustomVar("$ModHeatMapDetection") == 1f;
        if (ViewComponent != null) ViewComponent.IsVisible = enabled;
        if (!enabled) return;

        if (ownerEntityId != player.entityId)
        {
            ownerEntityId = player.entityId;
            requestSequence = 0;
            nextPoll = 0f;
            lastProjection = null;
            RebirthHeatMapHudState.ResetForOwner(ownerEntityId);
        }

        if (Time.time >= nextPoll)
        {
            nextPoll = Time.time + PollSeconds;
            int requestId = ++requestSequence;
            World world = player.world as World;
            ConnectionManager connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
            if (connection != null && connection.IsClient && !connection.IsServer)
                connection.SendToServer(NetPackageManager.GetPackage<NetPackageRebirthHeatMapRequest>().Setup(player.entityId, requestId));
            else
            {
                float activity, cooldown; bool ready; int regionX, regionZ;
                bool available = RebirthHeatMapReader.Read(world, player.position, out activity, out cooldown, out ready, out regionX, out regionZ);
                RebirthHeatMapHudState.Receive(player.entityId, requestId, activity, cooldown, ready, available, regionX, regionZ);
            }
        }

        // Cooldown is time-sensitive; all other formatting is change-driven.
        if (Time.time >= nextCountdownRender)
        {
            nextCountdownRender = Time.time + 1f;
            RefreshProjection();
        }
    }

    private void RefreshProjection()
    {
        if (heatText == null) return;
        string projection;
        if (RebirthHeatMapHudState.OwnerEntityId != ownerEntityId || !RebirthHeatMapHudState.Available)
        {
            projection = "Heat: [cc6b64]regional data unavailable[-]";
        }
        else
        {
            float value = RebirthHeatMapHudState.Activity;
            float pct = Mathf.Clamp(value / ScoutThreshold * 100f, 0f, 999f);
            string color = value >= ScoutThreshold ? "[a466d4]" : value >= 18.75f ? "[cc6b64]" : value >= 12.5f ? "[d99b5d]" : value >= 6.25f ? "[d6c978]" : "[ffffff]";
            string status = RebirthHeatMapHudState.Cooldown > 0f
                ? "[85b9c7]Cooldown[-]: " + FormatTime(RebirthHeatMapHudState.Cooldown)
                : (RebirthHeatMapHudState.Ready ? "[8fd18f]Ready[-]" : "[d99b5d]Not Ready[-]");
            projection = "Heat: " + color + value.ToString("F2") + "[-] / " + ScoutThreshold.ToString("F0") +
                " (" + pct.ToString("F0") + "%)  " + status + "  Region: " + RebirthHeatMapHudState.RegionX + "," + RebirthHeatMapHudState.RegionZ;
        }
        if (projection == lastProjection) return;
        lastProjection = projection;
        heatText.Text = projection;
    }

    private static string FormatTime(float seconds)
    {
        int total = Mathf.Max(0, Mathf.CeilToInt(seconds));
        return (total / 60).ToString("00") + ":" + (total % 60).ToString("00");
    }
}
