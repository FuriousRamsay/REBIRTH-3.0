using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEngine;

public static partial class RebirthDogLifecycleService
{
    private sealed class HireOwnerCheckpoint
    {
        internal HireSettlement Original;internal string Phase,Root,Name,Path;internal byte[] Native;
        internal bool Accepted,Saved,Archived;internal int Workers;
    }
    private sealed class HireOwnerCapture {internal HireOwnerCheckpoint Checkpoint;internal PlayerDataFile Data;}
    private sealed class HireOwnerSave {internal HireOwnerCheckpoint Checkpoint;internal byte[] Native;internal string Path;internal bool Finished;}
    private static readonly object HireOwnerGate=new object();
    private static readonly ConditionalWeakTable<PlayerDataFile,HireOwnerCapture> HireOwnerCaptures=new ConditionalWeakTable<PlayerDataFile,HireOwnerCapture>();
    private static readonly Dictionary<HireSettlement,Dictionary<string,HireOwnerCheckpoint>> HireOwnerFiles=new Dictionary<HireSettlement,Dictionary<string,HireOwnerCheckpoint>>();
    private static readonly Dictionary<HireSettlement,IDisposable> HireOwnerBudgets=new Dictionary<HireSettlement,IDisposable>();
    private static long nextHirePump;
    private static bool ReserveHireOwner(HireSettlement original)
    {
        RetireReplacedHireWorld();
        RebirthProductiveEarnedUiOwnerFile.Install();
        lock(HireOwnerGate)
        {
            if(HireOwnerBudgets.Count>=4||!RebirthProductiveEarnedObservationBudget.TryRetainDetached(96L*1024*1024,out var budget))return false;
            HireOwnerBudgets.Add(original,budget);HireOwnerFiles.Add(original,new Dictionary<string,HireOwnerCheckpoint>());return true;
        }
    }
    private static void ReleaseHireOwner(HireSettlement original)
    {
        lock(HireOwnerGate)
        {
            if(!HireOwnerFiles.TryGetValue(original,out var files)||files.Values.Any(file=>file.Workers!=0))return;
            HireOwnerFiles.Remove(original);if(HireOwnerBudgets.TryGetValue(original,out var budget)){HireOwnerBudgets.Remove(original);budget.Dispose();}
            foreach(var file in files.Values){file.Native=null;file.Original=null;}
            if(original.ObservationRetired)DropRetiredHireGraph(original);
        }
    }
    private static void PumpHireSettlements()
    {
        if(!ThreadManager.IsMainThread()||System.Diagnostics.Stopwatch.GetTimestamp()<nextHirePump)return;nextHirePump=System.Diagnostics.Stopwatch.GetTimestamp()+System.Diagnostics.Stopwatch.Frequency/2;
        RetireReplacedHireWorld();PumpRemoteHireRestart();PumpColdHireOriginals();
        foreach(var original in HireSettlements.Values.Take(4).ToArray())
            try{if(!original.Busy&&HireCurrent(original))FinishHireSettlement(original,out var reason);}catch{}
    }
    private static bool HireOwnerExpected(HireSettlement original,PlayerDataFile data,string phase)
    {
        if(data==null||data.id!=original.Player.entityId||data.deaths!=original.Deaths||data.bDead)return false;
        if(IsRemoteHire(original)&&!HirePdsStamp(data,original.RemoteNonce,original.State.StableId.ToString(),phase=="before"?1:phase=="refund"?5:3))return false;
        var belt=RebirthGearPlayerDataInventory.ReadSlots(data,false);var bag=RebirthGearPlayerDataInventory.ReadSlots(data,true);
        foreach(var source in original.Sources)
        {
            var slots=source.Belt?belt:bag;if(slots==null||source.Slot>=slots.Length||!RebirthStationGridIngredients.IsSameStackSnapshot(slots[source.Slot],(phase=="after"||phase=="settled")?source.After:source.Before))return false;
        }
        return true;
    }
    private static string HireOwnerPhase(HireSettlement original)=>original.Refunded?"refund":original.OwnershipSaved?"settled":original.Debited?"after":"before";
    private static HireOwnerCheckpoint CaptureHireOwner(HireSettlement original,PlayerDataFile data,string root,string name,bool accepted)
    {
        if(!HireCurrent(original)||original.Terminal||!HireOwnerFiles.TryGetValue(original,out var files))return null;
        string phase=HireOwnerPhase(original);if(!HireOwnerExpected(original,data,phase))return null;
        if(files.TryGetValue(phase,out var prior)){if(prior.Archived||prior.Workers!=0)return null;prior.Native=null;prior.Original=null;}
        root=Path.GetFullPath(root);string path=Path.GetFullPath(Path.Combine(root,name+"."+PlayerDataFile.EXT));
        if(!RebirthProductiveEarnedUiOwnerFile.DeferredNativeOwnerPathSafe(path))return null;
        var native=RebirthProductiveEarnedUiOwnerFile.DeferredNativeOwnerImage(data);if(native.Length<1||native.Length>16*1024*1024)return null;
        var checkpoint=new HireOwnerCheckpoint{Original=original,Phase=phase,Root=root,Name=name,Path=path,Native=native,Accepted=accepted};
        files[phase]=checkpoint;HireOwnerCaptures.Remove(data);HireOwnerCaptures.Add(data,new HireOwnerCapture{Checkpoint=checkpoint,Data=data});return checkpoint;
    }
    internal static void ObserveHireOwnerFromPlayer(PlayerDataFile data,EntityPlayer player,bool run)
    {
        if(!run||!ThreadManager.IsMainThread()||data==null)return;
        lock(HireOwnerGate)try
        {
            foreach(var original in HireSettlements.Values)
            {
                if(!ReferenceEquals(original.Player,player)||!ReferenceEquals(original.World.GetPrimaryPlayer(),player)||!HireCurrent(original))continue;
                if(!RebirthStablePlayerIdentity.TryFromLocalPlatform(out var identity)||identity.CanonicalId!=original.Account)continue;
                CaptureHireOwner(original,data,GameIO.GetPlayerDataDir(),identity.CanonicalId,true);break;
            }
        }catch{}
    }
    // Must be called ONLY inside sole authenticated NetPackagePlayerData exact sender/file scope.
    internal static void EnterHireAcceptedOwner(ClientInfo peer,PlayerDataFile data,bool run,out object state)
    {
        state=null;if(!run||!ThreadManager.IsMainThread()||peer==null||data==null)return;
        lock(HireOwnerGate)try
        {
            if(!RebirthStablePlayerIdentity.TryFromClientInfo(peer,out var identity))return;
            foreach(var original in HireSettlements.Values)
            {
                if(original.Player.entityId!=peer.entityId||original.Account!=identity.CanonicalId||!HireCurrent(original))continue;
                state=CaptureHireOwner(original,data,GameIO.GetPlayerDataDir(),identity.CanonicalId,false);break;
            }
        }catch{}
    }
    internal static void ObserveHireAcceptedOwner(ClientInfo peer,PlayerDataFile data,bool run,object state)
    {
        if(!ThreadManager.IsMainThread()||state is not HireOwnerCheckpoint checkpoint)return;
        lock(HireOwnerGate)try
        {
            checkpoint.Accepted=run&&ReferenceEquals(peer.latestPlayerData,data)&&checkpoint.Original!=null&&HireCurrent(checkpoint.Original)&&
                HireOwnerExpected(checkpoint.Original,data,checkpoint.Phase)&&checkpoint.Native.SequenceEqual(RebirthProductiveEarnedUiOwnerFile.DeferredNativeOwnerImage(data));
        }catch{checkpoint.Accepted=false;}
    }
    internal static void EnterHireOwnerSave(PlayerDataFile data,string directory,string name,bool run,out object state)
    {
        state=null;if(!run||data==null)return;lock(HireOwnerGate)try
        {
            if(!HireOwnerCaptures.TryGetValue(data,out var capture))return;var checkpoint=capture.Checkpoint;
            if(checkpoint.Original==null||checkpoint.Original.Terminal||checkpoint.Original.ObservationRetired||checkpoint.Workers>=2||checkpoint.Archived||Path.GetFullPath(directory)!=checkpoint.Root||name!=checkpoint.Name||
                !checkpoint.Native.SequenceEqual(RebirthProductiveEarnedUiOwnerFile.DeferredNativeOwnerImage(data)))return;
            checkpoint.Workers++;state=new HireOwnerSave{Checkpoint=checkpoint,Native=checkpoint.Native,Path=checkpoint.Path};
        }catch{}
    }
    internal static void ObserveHireOwnerSave(bool run,object state)
    {
        if(state is not HireOwnerSave saved||saved.Finished)return;saved.Finished=true;
        try
        {
            bool exact=run&&RebirthProductiveEarnedUiOwnerFile.DeferredNativeOwnerFileMatches(saved.Path,saved.Native);
            lock(HireOwnerGate)if(exact&&saved.Checkpoint.Original?.ObservationRetired==false&&ReferenceEquals(saved.Checkpoint.Native,saved.Native))saved.Checkpoint.Saved=true;
        }
        catch{}
        finally
        {
            lock(HireOwnerGate)
            {
                var checkpoint=saved.Checkpoint;checkpoint.Workers--;if(checkpoint.Original!=null&&(checkpoint.Original.Terminal||checkpoint.Original.ObservationRetired))ReleaseHireOwner(checkpoint.Original);
                saved.Native=null;saved.Checkpoint=null;
            }
        }
    }
    private static string HireOwnerDigest(byte[] native)
    {using(var hash=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(hash.ComputeHash(native)).Replace("-",string.Empty);}
    private static bool HireColdOwnerIntegrity(string directory,string phase)
    {
        string path=Path.Combine(directory,"owner-"+phase+".native"),digest=path+".digest";RequireHirePath(directory);
        var info=new FileInfo(path);var proof=new FileInfo(digest);
        if(!info.Exists||info.Length<6||info.Length>16*1024*1024||(info.Attributes&FileAttributes.ReparsePoint)!=0||!proof.Exists||proof.Length!=64||(proof.Attributes&FileAttributes.ReparsePoint)!=0)return false;
        var native=File.ReadAllBytes(path);
        return native[0]=='t'&&native[1]=='t'&&native[2]=='p'&&native[3]==0&&native[4]==PlayerDataFile.cFileVersion&&HireOwnerDigest(native)==File.ReadAllText(digest);
    }
    private static bool EnsureHireOwnerCheckpoint(HireSettlement original,string phase)
    {
        if(!HireCurrent(original))return false;
        lock(HireOwnerGate)if(HireOwnerFiles.TryGetValue(original,out var known)&&known.TryGetValue(phase,out var prior)&&prior.Archived)return true;
        if(ReferenceEquals(original.World.GetPrimaryPlayer(),original.Player))GameManager.Instance.SaveLocalPlayerData();
        lock(HireOwnerGate)
        {
            if(!HireOwnerFiles.TryGetValue(original,out var files)||!files.TryGetValue(phase,out var file))return false;
            if(file.Archived)return true;
            if(!file.Accepted||!file.Saved||file.Native==null||!RebirthProductiveEarnedUiOwnerFile.DeferredNativeOwnerFileMatches(file.Path,file.Native)||!HireCurrent(original))return false;
            string path=Path.Combine(original.Directory,"owner-"+phase+".native");
            RequireHirePath(original.Directory);
            if(File.Exists(path)){if(!File.ReadAllBytes(path).SequenceEqual(file.Native))return false;}
            else using(var output=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None)){output.Write(file.Native,0,file.Native.Length);output.Flush(true);}
            if(!File.ReadAllBytes(path).SequenceEqual(file.Native))return false;
            string digest=path+".digest";byte[] proof=System.Text.Encoding.ASCII.GetBytes(HireOwnerDigest(file.Native));
            if(File.Exists(digest)){if(!File.ReadAllBytes(digest).SequenceEqual(proof))return false;}else using(var output=new FileStream(digest,FileMode.CreateNew,FileAccess.Write,FileShare.None)){output.Write(proof,0,proof.Length);output.Flush(true);}
            WriteHireRecord(original,"owner-"+phase+"-saved");file.Archived=true;return true;
        }
    }
}
