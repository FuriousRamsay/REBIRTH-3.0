using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Xml.Linq;

internal static partial class RebirthNpcMetabolismCustody
{
    private sealed class OwnerFile
    {internal Original Original;internal byte[] Native;internal string Path,Phase;internal bool Accepted,Saved,Archived;internal int Workers;}
    private sealed class OwnerSave {internal OwnerFile File;internal byte[] Native;internal string Path;internal bool Finished;}
    private static readonly object ownerGate=new object();
    private static readonly ConditionalWeakTable<PlayerDataFile,OwnerFile> ownerData=new ConditionalWeakTable<PlayerDataFile,OwnerFile>();
    private static readonly Dictionary<Original,Dictionary<string,OwnerFile>> ownerFiles=new Dictionary<Original,Dictionary<string,OwnerFile>>();
    private static string OwnerReceipt(Guid operation)=>"rbNpcConsume_"+operation.ToString("N");
    private static void CaptureOwner(Original original,PlayerDataFile data,bool accepted)
    {
        if(!Current(original)||data==null||data.id!=original.Actor.entityId||data.deaths!=original.Deaths||data.bDead||data.buffData==null||data.buffData.Length>1048576)return;
        if(!RebirthRemoteResourcePlacedStationSavedReceipt.TryRead(data,OwnerReceipt(original.Operation),out float marker,out bool present))return;
        if(!RebirthRemoteResourcePlacedStationSavedReceipt.TryRead(data,BeforeReceipt(original.Operation),out float beforeMarker,out bool beforePresent)||!beforePresent||beforeMarker!=1f)return;
        string phase=original.OwnerReturned?"after":original.OwnerSecondaryReturned?"secondary":"before";
        if(phase!="after"&&(present&&marker!=0f)||phase=="after"&&(!present||marker!=1f))return;
        if(phase=="secondary"&&(!RebirthRemoteResourcePlacedStationSavedReceipt.TryRead(data,SecondaryReceipt(original.Operation),out float secondary,out bool secondaryPresent)||!secondaryPresent||secondary!=1f))return;
        if(!ownerFiles.TryGetValue(original,out var phases))ownerFiles.Add(original,phases=new Dictionary<string,OwnerFile>());
        if(phases.TryGetValue(phase,out var prior)){if(prior.Archived||prior.Workers!=0)return;prior.Native=null;prior.Original=null;}
        string path=Path.GetFullPath(Path.Combine(GameIO.GetPlayerDataDir(),original.Account+"."+PlayerDataFile.EXT));if(!RebirthProductiveEarnedUiOwnerFile.DeferredNativeOwnerPathSafe(path))return;
        var native=CaptureNativeOwnerImage(data);if(native.Length<6||native.Length>16*1024*1024)return;
        var capture=new OwnerFile{Original=original,Native=native,Path=path,Phase=phase,Accepted=accepted};phases[phase]=capture;ownerData.Remove(data);ownerData.Add(data,capture);
    }
    internal static void ObserveOwnerFromPlayer(PlayerDataFile data,EntityPlayer actor,bool run)
    {
        if(!run||!ThreadManager.IsMainThread())return;
        lock(ownerGate)try
        {
            foreach(var original in pending.Values)
                if(ReferenceEquals(original.Actor,actor)&&ReferenceEquals(original.World.GetPrimaryPlayer(),actor)&&RebirthStablePlayerIdentity.TryFromLocalPlatform(out var account)&&account.CanonicalId==original.Account)
                {CaptureOwner(original,data,true);return;}
        }catch{}
    }
    internal static void EnterAcceptedOwner(ClientInfo peer,PlayerDataFile data,bool run,out object state)
    {
        state=null;if(!run||!ThreadManager.IsMainThread()||peer==null||data==null)return;
        lock(ownerGate)try
        {
            if(!RebirthStablePlayerIdentity.TryFromClientInfo(peer,out var account))return;
            foreach(var original in pending.Values)
                if(original.Actor.entityId==peer.entityId&&original.Account==account.CanonicalId)
                {CaptureOwner(original,data,false);if(ownerData.TryGetValue(data,out var capture))state=capture;return;}
        }catch{}
    }
    internal static void ObserveAcceptedOwner(ClientInfo peer,PlayerDataFile data,bool run,object state)
    {
        if(!ThreadManager.IsMainThread()||state is not OwnerFile file)return;
        lock(ownerGate)try
        {
            var original=file.Original;if(!run||original==null||!Current(original)||!ReferenceEquals(peer.latestPlayerData,data)||!ReferenceEquals(peer,SingletonMonoBehaviour<ConnectionManager>.Instance?.Clients.ForEntityId(original.Actor.entityId))||
                !file.Native.SequenceEqual(CaptureNativeOwnerImage(data)))return;
            file.Accepted=true;
        }catch{}
    }
    internal static void EnterOwnerSave(PlayerDataFile data,string directory,string name,bool run,out object state)
    {
        state=null;if(!run||data==null)return;
        lock(ownerGate)try
        {
            if(!ownerData.TryGetValue(data,out var file)||file.Original==null||file.Original.Terminal||file.Original.ObservationRetired||file.Workers>=2)return;
            string path=Path.GetFullPath(Path.Combine(directory,name+"."+PlayerDataFile.EXT));
            if(path!=file.Path||!RebirthProductiveEarnedUiOwnerFile.DeferredNativeOwnerPathSafe(path)||!file.Native.SequenceEqual(CaptureNativeOwnerImage(data)))return;
            var save=new OwnerSave{File=file,Native=file.Native,Path=path};file.Workers++;state=save;
        }catch{}
    }
    internal static void ObserveOwnerSave(bool run,object state)
    {
        if(state is not OwnerSave save)return;
        lock(ownerGate)
        {
            if(save.Finished)return;save.Finished=true;
            try{if(run&&save.File.Original?.ObservationRetired==false&&RebirthProductiveEarnedUiOwnerFile.DeferredNativeOwnerFileMatches(save.Path,save.Native))save.File.Saved=true;}
            catch{}
            finally{var original=save.File.Original;save.File.Workers--;save.File=null;save.Native=null;if(original?.Terminal==true||original?.ObservationRetired==true)ReleaseOwner(original);}
        }
    }
    private static bool ArchiveOwner(Original original,string phase)
    {
        if(!Current(original))return false;
        if(ReferenceEquals(original.World.GetPrimaryPlayer(),original.Actor))GameManager.Instance.SaveLocalPlayerData();
        lock(ownerGate)
        {
            if(!ownerFiles.TryGetValue(original,out var phases)||!phases.TryGetValue(phase,out var file)||!file.Accepted||!file.Saved)return false;
            if(file.Archived)return true;
            if(!RebirthProductiveEarnedUiOwnerFile.DeferredNativeOwnerFileMatches(file.Path,file.Native))return false;
            string path=Path.Combine(original.Directory,"owner-"+phase+".native");RequirePath(original.Directory);
            if(File.Exists(path)){var info=new FileInfo(path);if((info.Attributes&FileAttributes.ReparsePoint)!=0||info.Length!=file.Native.Length||!ReadBoundedStageBytes(path,file.Native.Length).SequenceEqual(file.Native))return false;}
            else using(var output=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None)){output.Write(file.Native,0,file.Native.Length);output.Flush(true);}
            if(!ReadBoundedStageBytes(path,file.Native.Length).SequenceEqual(file.Native))return false;Record(original,"owner-"+phase+"-saved",new XElement("native",new XAttribute("length",file.Native.Length),new XAttribute("integrity",Integrity(file.Native))));file.Archived=true;return true;
        }
    }
    private static bool EnsureOwnerBefore(Original original)
    {
        bool local=ReferenceEquals(original.World.GetPrimaryPlayer(),original.Actor);
        if(local)original.Actor.Buffs.SetCustomVar(BeforeReceipt(original.Operation),1f,true);
        if(!local&&(!original.OwnerReady||!HistoricalOwner(original.Directory,"before",original.Text)))SendOwnerPurpose(original,0);
        return ArchiveOwner(original,"before")&&(local||original.OwnerReady);
    }
    private static bool CompleteNativeOwnerEffect(Original original)
    {
        if(!original.OwnerEntered)
        {
            ObserveScenario(original,"before-owner-effect-permit");
            Record(original,"owner-effect-intent");original.OwnerEntered=true;
            if(ReferenceEquals(original.World.GetPrimaryPlayer(),original.Actor))
            {
                var local=original.Actor as EntityPlayerLocal;
                if(local==null||!ApplyOwnerEvent(original.Operation,local,original.Plan.NativeItem.itemValue))return false;
                original.OwnerReturned=true;Record(original,"owner-effect-returned");
            }
            else SendOwnerPurpose(original,1);
        }
        if(!ReferenceEquals(original.World.GetPrimaryPlayer(),original.Actor)&&(!original.OwnerReturned||!original.OwnerSaved))SendOwnerPurpose(original,1);
        if(!original.OwnerReturned)return false;Record(original,"owner-effect-returned");
        if(!ArchiveOwner(original,"after"))return false;original.OwnerSaved=true;return true;
    }
    private static bool CompleteNativeSecondary(Original original)
    {
        if(!original.OwnerSecondaryReturned)
        {
            if(ReferenceEquals(original.World.GetPrimaryPlayer(),original.Actor))
            {
                if(original.Actor is not EntityPlayerLocal local||!original.DeferredReturned)return false;
                local.Buffs.SetCustomVar(SecondaryReceipt(original.Operation),1f,true);
                original.OwnerSecondaryReturned=true;Record(original,"owner-secondary-returned");
            }
            else
            {
                if(!original.SecondaryPdsRequested){Record(original,"owner-secondary-intent");original.SecondaryPdsRequested=true;}SendOwnerPurpose(original,4);
                return false;
            }
        }
        Record(original,"owner-secondary-returned");bool saved=ArchiveOwner(original,"secondary");
        if(!saved&&!ReferenceEquals(original.World.GetPrimaryPlayer(),original.Actor))SendOwnerPurpose(original,4);return saved;
    }
    private static void ReleaseOwner(Original original)
    {
        lock(ownerGate)
        {
            if(ownerFiles.TryGetValue(original,out var phases))
            {
                if(phases.Values.Any(file=>file.Workers!=0))return;
                ownerFiles.Remove(original);foreach(var file in phases.Values){file.Native=null;file.Original=null;}
            }
            original.Memory?.Dispose();original.Memory=null;original.Plan=null;original.Before=null;original.AfterNative=null;original.AfterQuantities=null;original.Source=null;original.Metabolism=null;original.Actor=null;original.Npc=null;original.World=null;
        }
    }
}
