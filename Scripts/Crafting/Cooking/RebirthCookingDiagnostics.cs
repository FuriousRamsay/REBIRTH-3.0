using System;
using System.Diagnostics;
using UnityEngine;

/// <summary>Opt-in, bounded measurements. No per-frame output or persistent logging.</summary>
public static class RebirthCookingDiagnostics
{
    private static double deadline,nextReport;
    private static long stationTicks,renderTicks,heatTicks,inventoryTicks;
    private static double worstFrame;
    private static int frames,renders,heatUpdates;
    private static double frameSeconds;
    private static readonly System.Collections.Generic.Dictionary<string,long> sections=new System.Collections.Generic.Dictionary<string,long>();
    public static void Section(string name,long start){if(start==0 || !Active)return;sections.TryGetValue(name,out long ticks);sections[name]=ticks+Stopwatch.GetTimestamp()-start;}
    public static bool Active=>deadline>0 && Time.realtimeSinceStartupAsDouble<deadline;
    public static long Begin()=>Active?Stopwatch.GetTimestamp():0;
    public static void Start()
    {
        stationTicks=renderTicks=heatTicks=inventoryTicks=0;worstFrame=0;frames=renders=heatUpdates=0;frameSeconds=0;
        sections.Clear();
        deadline=Time.realtimeSinceStartupAsDouble+20;nextReport=Time.realtimeSinceStartupAsDouble+5;
        Log.Out("[REBIRTH Cooking profile] Started; collecting 20 seconds with the cooking window open.");
        SdtdConsole.Instance.Output("Cooking profile started: keep the cooking window open for 20 seconds.");
    }
    public static void Render(long start){if(start==0 || !Active)return;renderTicks+=Stopwatch.GetTimestamp()-start;renders++;}
    public static void Inventory(long start){if(start!=0 && Active)inventoryTicks+=Stopwatch.GetTimestamp()-start;}
    public static void Heat(long start){if(start==0 || !Active)return;heatTicks+=Stopwatch.GetTimestamp()-start;heatUpdates++;}
    public static void Frame(long start)
    {
        if(start==0 || !Active)return;
        stationTicks+=Stopwatch.GetTimestamp()-start;frames++;frameSeconds+=Time.unscaledDeltaTime;
        worstFrame=Math.Max(worstFrame,Time.unscaledDeltaTime*1000);
    }
    public static void Stop()
    {
        deadline=nextReport=0;
        sections.Clear();
        stationTicks=renderTicks=heatTicks=inventoryTicks=0;
        frames=renders=heatUpdates=0; frameSeconds=worstFrame=0;
    }
    // Called by the existing mod lifecycle, independent of a window's Frame callback.
    public static void Tick()
    {
        if(deadline<=0)return;
        double now=Time.realtimeSinceStartupAsDouble;
        if(now<nextReport && now<deadline)return;
        bool finished=now>=deadline;
        nextReport=now+5;
        Report();
        if(finished)Stop();
    }
    private static void Report()
    {
        double ms=1000.0/Stopwatch.Frequency;
        string report="[REBIRTH Cooking profile] FPS="+(frames/Math.Max(.001,frameSeconds)).ToString("0.0")+
            "; station UI CPU="+(stationTicks*ms/Math.Max(1,frames)).ToString("0.00")+"ms/frame (includes children)"+
            "; workspace="+(renderTicks*ms/Math.Max(1,renders)).ToString("0.00")+"ms/render, "+renders+" renders"+
            "; heat="+(heatTicks*ms/Math.Max(1,heatUpdates)).ToString("0.000")+"ms/update"+
            "; inventory availability="+(inventoryTicks*ms/Math.Max(1,renders)).ToString("0.00")+"ms/render; worst frame="+worstFrame.ToString("0.0")+"ms";
        foreach(var section in sections)report+="; "+section.Key+"="+(section.Value*ms/Math.Max(1,frames)).ToString("0.00")+"ms/frame";
        Log.Out(report);SdtdConsole.Instance.Output(report);
    }
}
