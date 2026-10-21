using System;
using System.Collections.Generic;
using HarmonyLib;
using Newtonsoft.Json.Linq;
using UnityEngine;

// Bridge-only observation of native workstation packets. It never changes the
// packet, station or player and is installed only with -rebirthbridge.
internal static class RebirthGameBridgeStationPacketProbe
{
    private sealed class Sample
    {
        internal World World;
        internal Vector3i Position;
        internal int Sender,Before,After,Handle;
        internal bool BeforeWaiting,AfterWaiting;
        internal bool Ran;
        internal float At;
    }
    private sealed class Outgoing
    {
        internal Vector3i Position;
        internal int Queue,Bytes;
        internal bool Ran;
        internal float At;
    }
    private sealed class ReadSample
    {
        internal Vector3i Position;
        internal int Before,After;
        internal bool WaitingBefore,WaitingAfter,Ran;
        internal float At;
    }
    private static readonly List<Sample> Samples=new List<Sample>();
    private static readonly List<Outgoing> OutgoingSamples=new List<Outgoing>();
    private static readonly List<ReadSample> ReadSamples=new List<ReadSample>();
    private static World current;
    private static int Count(TileEntityWorkstation station)
    {
        int result=0;
        if(station?.Queue!=null)foreach(var row in station.Queue)if(row?.Recipe!=null)result++;
        return result;
    }
    internal static void Install()
    {
        var harmony=new Harmony("rebirth.gamebridge.stationpacket");
        RebirthHarmonyBootstrap.PatchClassOnce(harmony,typeof(Hook));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony,typeof(SetupHook));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony,typeof(ReadHook));
    }
    internal static JObject Capture(World world,Vector3i position)
    {
        if(!ReferenceEquals(current,world)){current=world;Samples.Clear();}
        var rows=new JArray();
        foreach(var sample in Samples)
            if(ReferenceEquals(sample.World,world)&&sample.Position==position)
                rows.Add(new JObject{["sender"]=sample.Sender,["before"]=sample.Before,["after"]=sample.After,
                    ["handle"]=sample.Handle,["beforeWaiting"]=sample.BeforeWaiting,["afterWaiting"]=sample.AfterWaiting,
                    ["ranOriginal"]=sample.Ran,["at"]=sample.At});
        var outgoing=new JArray();
        foreach(var sample in OutgoingSamples)
            if(sample.Position==position)
                outgoing.Add(new JObject{["queueAtSetup"]=sample.Queue,["bytes"]=sample.Bytes,
                    ["ranOriginal"]=sample.Ran,["at"]=sample.At});
        var reads=new JArray();
        foreach(var sample in ReadSamples)
            if(sample.Position==position)
                reads.Add(new JObject{["before"]=sample.Before,["after"]=sample.After,
                    ["waitingBefore"]=sample.WaitingBefore,["waitingAfter"]=sample.WaitingAfter,
                    ["ranOriginal"]=sample.Ran,["at"]=sample.At});
        return new JObject{["available"]=true,["packets"]=rows,["outgoing"]=outgoing,["reads"]=reads};
    }
    [HarmonyPatch(typeof(TileEntityWorkstation),nameof(TileEntityWorkstation.read))]
    private static class ReadHook
    {
        private static void Prefix(TileEntityWorkstation __instance,StreamModeRead _eStreamMode,out ReadSample __state)
        {
            __state=null;
            if(_eStreamMode!=StreamModeRead.FromClient||__instance?.Queue==null||!ThreadManager.IsMainThread()||
                GameManager.Instance?.World?.IsRemote()!=false)return;
            __state=new ReadSample{Position=__instance.ToWorldPos(),Before=Count(__instance),
                WaitingBefore=__instance.bWaitingForServerResponse,At=Time.realtimeSinceStartup};
        }
        private static void Postfix(TileEntityWorkstation __instance,bool __runOriginal,ReadSample __state)
        {
            if(__state==null)return;
            __state.After=Count(__instance);
            __state.WaitingAfter=__instance.bWaitingForServerResponse;
            __state.Ran=__runOriginal;
            if(ReadSamples.Count==32)ReadSamples.RemoveAt(0);
            ReadSamples.Add(__state);
        }
    }
    [HarmonyPatch(typeof(NetPackageTileEntity),nameof(NetPackageTileEntity.Setup),new[]{typeof(TileEntity),typeof(StreamModeWrite),typeof(byte)})]
    private static class SetupHook
    {
        private static void Prefix(TileEntity _te,StreamModeWrite _eStreamMode,out Outgoing __state)
        {
            __state=null;
            if(_eStreamMode!=StreamModeWrite.ToServer||_te is not TileEntityWorkstation station||
                station.Queue==null||!ThreadManager.IsMainThread())return;
            __state=new Outgoing{Position=station.ToWorldPos(),Queue=Count(station),At=Time.realtimeSinceStartup};
        }
        private static void Postfix(NetPackageTileEntity __instance,bool __runOriginal,Outgoing __state)
        {
            if(__state==null)return;
            __state.Ran=__runOriginal;
            __state.Bytes=(int)(__instance.ms?.Length??0);
            if(OutgoingSamples.Count==32)OutgoingSamples.RemoveAt(0);
            OutgoingSamples.Add(__state);
        }
    }
    [HarmonyPatch(typeof(NetPackageTileEntity),nameof(NetPackageTileEntity.ProcessPackage))]
    private static class Hook
    {
        private static void Prefix(NetPackageTileEntity __instance,World _world,out Sample __state)
        {
            __state=null;
            if(!ThreadManager.IsMainThread()||_world==null||_world.IsRemote()||
                !ReferenceEquals(GameManager.Instance?.World,_world))return;
            var station=_world.GetTileEntity(__instance.teWorldPos) as TileEntityWorkstation;
            if(station?.Queue==null||Array.FindIndex(station.Queue,row=>RebirthCookingHeat.Managed(row?.Recipe))<0)return;
            __state=new Sample{World=_world,Position=__instance.teWorldPos,Sender=__instance.Sender?.entityId??-1,
                Before=Count(station),BeforeWaiting=station.bWaitingForServerResponse,Handle=__instance.handle,
                At=Time.realtimeSinceStartup};
        }
        private static void Postfix(NetPackageTileEntity __instance,World _world,bool __runOriginal,Sample __state)
        {
            if(__state==null)return;
            var station=_world.GetTileEntity(__state.Position) as TileEntityWorkstation;
            __state.After=Count(station);
            __state.AfterWaiting=station?.bWaitingForServerResponse??false;
            __state.Ran=__runOriginal;
            if(!ReferenceEquals(current,_world)){current=_world;Samples.Clear();}
            if(Samples.Count==32)Samples.RemoveAt(0);
            Samples.Add(__state);
        }
    }
}
