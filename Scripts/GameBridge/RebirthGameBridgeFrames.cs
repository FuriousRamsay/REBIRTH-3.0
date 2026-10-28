using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

// A bounded fallback for profiler sessions where Windows supplies no DXGI Present
// events. It records only while explicitly requested by the local Game Bridge.
internal static class RebirthGameBridgeFrames
{
    private const int Capacity = 32768;
    private static readonly float[] milliseconds = new float[Capacity];
    private static readonly float[] gameSeconds = new float[Capacity];
    private static readonly int[] frameIds = new int[Capacity];
    private static readonly long[] frameUtcFileTimes = new long[Capacity];
    private static int count;
    private static bool recording;
    private static bool graphicsTimingEnabled;
    private const int EnergyRefreshCapacity = 256;
    private static readonly double[] energyRefreshMs = new double[EnergyRefreshCapacity];
    private static readonly double[,] energyRefreshStages = new double[EnergyRefreshCapacity,4];
    private static readonly float[] energyRefreshSeconds = new float[EnergyRefreshCapacity];
    private static readonly int[] energyRefreshFrames = new int[EnergyRefreshCapacity];
    private static readonly long[] energyRefreshUtcFileTimes = new long[EnergyRefreshCapacity];
    private static int energyRefreshCount;

    internal static bool IsRecording => recording;
    private static readonly string[] uiNames = { "Crafting", "Camper storage", "Camper stations", "Energy", "Journal" };
    private static readonly int[] uiCalls = new int[5];
    private static readonly double[] uiTotalMs = new double[5], uiMaxMs = new double[5];
    private static int captureEpoch;
    internal static UiScope MeasureUi(int kind) => recording ? new UiScope(kind, captureEpoch) : default;
    internal readonly struct UiScope : IDisposable
    {
        private readonly int kind, epoch;
        private readonly long started;
        internal UiScope(int kind, int epoch)
        { this.kind = kind; this.epoch = epoch; started = System.Diagnostics.Stopwatch.GetTimestamp(); }
        public void Dispose()
        {
            if (started == 0 || !recording || epoch != captureEpoch || uiCalls[kind] >= Capacity) return;
            double ms = (System.Diagnostics.Stopwatch.GetTimestamp() - started) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            uiCalls[kind]++; uiTotalMs[kind] += ms; uiMaxMs[kind] = Math.Max(uiMaxMs[kind], ms);
        }
    }
    internal static void RecordEnergyRefresh(double durationMs,double runtimeMs,double poiMs,double statusMs,double presentMs)
    {
        if (!recording || energyRefreshCount >= EnergyRefreshCapacity) return;
        int at = energyRefreshCount++;
        energyRefreshMs[at] = durationMs;
        energyRefreshStages[at,0]=runtimeMs;energyRefreshStages[at,1]=poiMs;
        energyRefreshStages[at,2]=statusMs;energyRefreshStages[at,3]=presentMs;
        energyRefreshSeconds[at] = Time.realtimeSinceStartup;
        energyRefreshFrames[at] = Time.frameCount;
        energyRefreshUtcFileTimes[at] = DateTime.UtcNow.ToFileTimeUtc();
    }

    internal static void Tick()
    {
        if (!recording) return;
        if (count >= Capacity) { recording = false; return; }
        if (graphicsTimingEnabled)
        {
            try { FrameTimingManager.CaptureFrameTimings(); }
            catch { graphicsTimingEnabled = false; }
        }
        milliseconds[count] = Time.unscaledDeltaTime * 1000f;
        gameSeconds[count] = Time.realtimeSinceStartup;
        frameIds[count] = Time.frameCount;
        frameUtcFileTimes[count] = DateTime.UtcNow.ToFileTimeUtc();
        count++;
    }

    internal static void Handle(BridgeRequest request)
    {
        if (request.Method == "POST")
        {
            string action = request.QueryString("action");
            if (action == "start")
            {
                count = 0;
                energyRefreshCount = 0;
                unchecked { captureEpoch++; }
                Array.Clear(uiCalls, 0, uiCalls.Length);
                Array.Clear(uiTotalMs, 0, uiTotalMs.Length);
                Array.Clear(uiMaxMs, 0, uiMaxMs.Length);
                try { graphicsTimingEnabled = request.QueryString("graphics") == "1" && FrameTimingManager.IsFeatureEnabled(); }
                catch { graphicsTimingEnabled = false; }
                recording = true;
                request.Complete(new JObject { ["recording"] = true, ["capacity"] = Capacity, ["graphicsTimingEnabled"] = graphicsTimingEnabled });
                return;
            }
            if (action != "stop") { request.Fail("Expected action=start or action=stop", 400); return; }
            recording = false;
        }
        else if (request.Method != "GET") { request.Fail("GET or POST required", 405); return; }

        int n = count;
        if (n == 0)
        {
            request.Complete(new JObject { ["recording"] = recording, ["frames"] = 0 });
            return;
        }
        var sorted = new float[n];
        Array.Copy(milliseconds, sorted, n);
        Array.Sort(sorted);
        double sum = 0;
        int over33 = 0, over50 = 0, over100 = 0;
        for (int i = 0; i < n; i++)
        {
            float ms = milliseconds[i];
            sum += ms;
            if (ms >= 33.333f) over33++;
            if (ms >= 50f) over50++;
            if (ms >= 100f) over100++;
        }
        var worstFrames = new JArray();
        var top = new int[Math.Min(8, n)];
        for (int i = 0; i < top.Length; i++) top[i] = -1;
        for (int i = 0; i < n; i++)
        {
            int position = 0;
            while (position < top.Length && top[position] >= 0 && milliseconds[top[position]] >= milliseconds[i]) position++;
            if (position >= top.Length) continue;
            for (int j = top.Length - 1; j > position; j--) top[j] = top[j - 1];
            top[position] = i;
        }
        for (int i = 0; i < top.Length && top[i] >= 0; i++)
        {
            int at = top[i];
            worstFrames.Add(new JObject { ["ms"] = milliseconds[at], ["gameSeconds"] = gameSeconds[at], ["frame"] = frameIds[at],
                ["utcFileTime"] = frameUtcFileTimes[at], ["utc"] = DateTime.FromFileTimeUtc(frameUtcFileTimes[at]).ToString("o") });
        }
        var uiUpdates = new JArray();
        for (int i = 0; i < uiNames.Length; i++)
            if (uiCalls[i] > 0) uiUpdates.Add(new JObject { ["window"] = uiNames[i], ["calls"] = uiCalls[i],
                ["totalMs"] = uiTotalMs[i], ["averageMs"] = uiTotalMs[i] / uiCalls[i], ["maximumMs"] = uiMaxMs[i],
                ["capped"] = uiCalls[i] >= Capacity });
        var energyRefreshTop = new JArray();
        var refreshTop = new int[Math.Min(8, energyRefreshCount)];
        for (int i = 0; i < refreshTop.Length; i++) refreshTop[i] = -1;
        for (int i = 0; i < energyRefreshCount; i++)
        {
            int position = 0;
            while (position < refreshTop.Length && refreshTop[position] >= 0 && energyRefreshMs[refreshTop[position]] >= energyRefreshMs[i]) position++;
            if (position >= refreshTop.Length) continue;
            for (int j = refreshTop.Length - 1; j > position; j--) refreshTop[j] = refreshTop[j - 1];
            refreshTop[position] = i;
        }
        for (int i = 0; i < refreshTop.Length && refreshTop[i] >= 0; i++)
        {
            int at = refreshTop[i];
            energyRefreshTop.Add(new JObject { ["ms"] = energyRefreshMs[at], ["gameSeconds"] = energyRefreshSeconds[at], ["frame"] = energyRefreshFrames[at],
                ["runtimeMs"]=energyRefreshStages[at,0],["poiMs"]=energyRefreshStages[at,1],
                ["statusMs"]=energyRefreshStages[at,2],["presentMs"]=energyRefreshStages[at,3],
                ["utcFileTime"] = energyRefreshUtcFileTimes[at], ["utc"] = DateTime.FromFileTimeUtc(energyRefreshUtcFileTimes[at]).ToString("o") });
        }
        int graphicsFrames = 0, validGpuFrames = 0;
        double cpuMainMs = 0, cpuRenderMs = 0, gpuMs = 0;
        if (graphicsTimingEnabled)
        {
            try
            {
                var timing = new FrameTiming[64];
                graphicsFrames = (int)FrameTimingManager.GetLatestTimings((uint)timing.Length, timing);
                for (int i = 0; i < graphicsFrames; i++)
                {
                    cpuMainMs += timing[i].cpuMainThreadFrameTime;
                    cpuRenderMs += timing[i].cpuRenderThreadFrameTime;
                    if (timing[i].gpuFrameTime > 0) { gpuMs += timing[i].gpuFrameTime; validGpuFrames++; }
                }
            }
            catch { graphicsFrames = 0; validGpuFrames = 0; }
        }
        var uiPanels = new JArray();
        int panelCount = 0, widgetCount = 0, visibleWidgetCount = 0, drawCallCount = 0;
        foreach (var panel in UIPanel.list)
        {
            if (!panel || !panel.enabled || !panel.gameObject.activeInHierarchy) continue;
            int widgets = panel.widgets?.Count ?? 0;
            int visible = 0;
            if (panel.widgets != null)
                foreach (var widget in panel.widgets)
                    if (widget && widget.enabled && widget.gameObject.activeInHierarchy && widget.isVisible) visible++;
            int draws = panel.drawCalls?.Count ?? 0;
            panelCount++; widgetCount += widgets; visibleWidgetCount += visible; drawCallCount += draws;
            uiPanels.Add(new JObject { ["name"] = panel.name, ["widgets"] = widgets, ["visibleWidgets"] = visible, ["drawCalls"] = draws });
        }
        int camperRenderers = 0, camperMaterialSlots = 0;
        var camperGroups = new Dictionary<string, int[]>();
        var world = GameManager.Instance?.World;
        if (world?.Entities?.list != null)
            foreach (var entity in world.Entities.list)
                if (entity is EntityVehicle vehicle && RebirthCamperRadial.IsCamper(vehicle) && vehicle.RootTransform != null)
                    foreach (var renderer in vehicle.RootTransform.GetComponentsInChildren<Renderer>(false))
                        if (renderer && renderer.enabled && renderer.gameObject.activeInHierarchy)
                        {
                            camperRenderers++;
                            int materials = renderer.sharedMaterials.Length;
                            camperMaterialSlots += materials;
                            string group = "Other camper geometry";
                            for (var node = renderer.transform; node != null && node != vehicle.RootTransform; node = node.parent)
                                if (node.name == "REBIRTHCampRoofStations" || node.name == "REBIRTHCampInteriorStations" ||
                                    node.name == "REBIRTHCampPowerEquipment" || node.name == "REBIRTHCampStairVisuals" ||
                                    node.name == "REBIRTHCamperNativeRelays")
                                { group = node.name; break; }
                            if (!camperGroups.TryGetValue(group, out var totals)) camperGroups.Add(group, totals = new int[2]);
                            totals[0]++;
                            totals[1] += materials;
                        }
        var camperGroupRows = new JArray();
        foreach (var group in camperGroups)
            camperGroupRows.Add(new JObject { ["name"] = group.Key, ["renderers"] = group.Value[0], ["materialSlots"] = group.Value[1] });
        request.Complete(new JObject
        {
            ["recording"] = recording,
            ["frames"] = n,
            ["avgMs"] = sum / n,
            ["p95Ms"] = sorted[Math.Min(n - 1, (int)Math.Ceiling(n * .95) - 1)],
            ["p99Ms"] = sorted[Math.Min(n - 1, (int)Math.Ceiling(n * .99) - 1)],
            ["worstMs"] = sorted[n - 1],
            ["worstFrames"] = worstFrames,
            ["energyRefreshCount"] = energyRefreshCount,
            ["uiUpdates"] = uiUpdates,
            ["energyRefreshTop"] = energyRefreshTop,
            ["graphicsTimingEnabled"] = graphicsTimingEnabled,
            ["graphicsFrames"] = graphicsFrames,
            ["cpuMainMsLatestAvg"] = graphicsFrames > 0 ? (double?)(cpuMainMs / graphicsFrames) : null,
            ["cpuRenderMsLatestAvg"] = graphicsFrames > 0 ? (double?)(cpuRenderMs / graphicsFrames) : null,
            ["gpuFrameMsLatestAvg"] = validGpuFrames > 0 ? (double?)(gpuMs / validGpuFrames) : null,
            ["uiPanelCount"] = panelCount,
            ["uiWidgetCount"] = widgetCount,
            ["uiVisibleWidgetCount"] = visibleWidgetCount,
            ["uiDrawCallCount"] = drawCallCount,
            ["uiPanels"] = uiPanels,
            ["camperActiveRendererCount"] = camperRenderers,
            ["camperActiveMaterialSlots"] = camperMaterialSlots,
            ["camperRendererGroups"] = camperGroupRows,
            ["over33Ms"] = over33,
            ["over50Ms"] = over50,
            ["over100Ms"] = over100,
            ["full"] = n == Capacity
        });
    }
}
