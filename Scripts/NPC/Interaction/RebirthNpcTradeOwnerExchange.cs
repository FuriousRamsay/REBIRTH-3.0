using System;
using System.Collections.Generic;
using System.Linq;

// Own native exchange purpose. Other domains supply only their existing
// conflict fences; their receipts and permissions are never used for payment.
internal static class RebirthNpcTradeOwnerExchange
{
    private sealed class Change{internal bool Bag;internal int Slot;internal ItemStack Value;}
    private sealed class Hold
    {
        internal EntityPlayerLocal Player;internal World World;internal ItemStackGrid Bag,Belt;
        internal RebirthNpcInteractionNetworkClient.NativeTradeGrant Grant;
        internal RebirthNpcTradeService.OwnerAdmission.LocalPermission LocalGrant;
        internal RebirthNpcTradeOriginal Original;internal List<Change> Changes;
        internal int Progress;internal bool Entered,NeedsSave;
    }
    private sealed class Permit{internal Hold Hold;internal ItemStackGrid Grid;internal int Slot;internal ItemStack Value;internal bool Used;}
    internal sealed class LocalUnusedBefore
    {
        private readonly Hold hold;
        private LocalUnusedBefore(Hold hold){this.hold=hold;}
        internal RebirthNpcTradeOriginal Original=>hold.Original;
        internal bool Current()
        {
            try
            {
                return ThreadManager.IsMainThread()&&permit==null&&releasing==null&&
                    ReferenceEquals(GameManager.Instance?.World,hold.World)&&hold.World?.IsRemote()==false&&
                    ReferenceEquals(hold.World.GetPrimaryPlayer(),hold.Player)&&ReferenceEquals(hold.World.GetEntity(hold.Player.entityId),hold.Player)&&
                    !hold.Player.IsDead()&&hold.Player.IsSpawned()&&hold.Player.Died==Original.Deaths&&hold.World.worldState?.Guid==Original.World&&
                    holds.TryGetValue(Original.Operation,out var actual)&&ReferenceEquals(actual,hold)&&
                    hold.Progress==0&&!hold.Entered&&!hold.NeedsSave&&hold.Grant==null&&hold.LocalGrant==null&&
                    MetadataCurrent(hold)&&Prefix(hold,0)&&
                    RebirthNpcTradeOwnerCheckpoint.TryReadRetainedStage(hold.Player,Original,out int step,out int phase)&&step==0&&phase==1;
            }
            catch{return false;}
        }
        internal static bool TryCapture(EntityPlayerLocal player,RebirthNpcTradeOriginal original,out LocalUnusedBefore source)
        {
            source=null;
            if(original==null||!holds.TryGetValue(original.Operation,out var hold)||!ReferenceEquals(hold.Player,player)||
                !ReferenceEquals(hold.Original,original))return false;
            var value=new LocalUnusedBefore(hold);if(!value.Current())return false;source=value;return true;
        }
        internal bool TryRelease(RebirthNpcTradeService.HostUnused unused)
        {
            if(!Current()||unused==null||unused.Original.Image!=Original.Image||!unused.ReadRetained()||
                !RebirthNpcTradeOwnerOriginalStore.TryRetainReceipt(hold.Player,Original,"unused",
                    RebirthNpcTradeOwnerReceipt.WriteUnused(Original.Operation,Original.Digest,unused.Digest))||
                !unused.ReadRetained()||!Current()||!ClearHeldStamp(hold))return false;
            holds.Remove(Original.Operation);return true;
        }
    }
    private static Dictionary<Guid,Hold> holds=new Dictionary<Guid,Hold>();
    internal static bool HasLifecycleCustody=>holds.Count!=0||permit!=null||releasing!=null;
    internal static RebirthNpcSaveQuarantineDomain PrepareQuarantine(World world)
    {
        if(!ThreadManager.IsMainThread()||world==null||permit!=null||releasing!=null||holds.Count>64||
            holds.Values.Any(value=>value?.Original==null||!ReferenceEquals(value.World,world)||!ReferenceEquals(value.Player?.world,world)||
                (world.IsRemote()?value.LocalGrant!=null:value.Grant!=null)))throw new InvalidOperationException("Trade owner original scope unavailable");
        var records=holds;var empty=new Dictionary<Guid,Hold>();var doc=LifecycleImage();string image=doc.OuterXml;
        var identity=records.Select(pair=>new{Key=pair.Key,Value=pair.Value,Player=pair.Value.Player,World=pair.Value.World,Bag=pair.Value.Bag,Belt=pair.Value.Belt,
            Original=pair.Value.Original,Grant=pair.Value.Grant,LocalGrant=pair.Value.LocalGrant,Changes=pair.Value.Changes}).ToArray();
        return new RebirthNpcSaveQuarantineDomain("trade-owner-holds",doc,()=>permit==null&&releasing==null&&ReferenceEquals(holds,records)&&records.Count==identity.Length&&
            identity.All(row=>records.TryGetValue(row.Key,out var value)&&ReferenceEquals(value,row.Value)&&ReferenceEquals(value.Player,row.Player)&&
                ReferenceEquals(value.World,row.World)&&ReferenceEquals(value.Bag,row.Bag)&&ReferenceEquals(value.Belt,row.Belt)&&ReferenceEquals(value.Original,row.Original)&&
                ReferenceEquals(value.Grant,row.Grant)&&ReferenceEquals(value.LocalGrant,row.LocalGrant)&&ReferenceEquals(value.Changes,row.Changes))&&LifecycleImage().OuterXml==image,
            ()=>holds=empty,()=>holds=records);
    }
    private static System.Xml.XmlDocument LifecycleImage()
    {
        var doc=new System.Xml.XmlDocument{XmlResolver=null};var root=doc.CreateElement("tradeOwnerHolds");doc.AppendChild(root);
        foreach(var pair in holds.OrderBy(value=>value.Key))
        {
            var hold=pair.Value;if(hold?.Original==null||hold.Original.Operation!=pair.Key||hold.Progress<0||hold.Progress>hold.Changes.Count)throw new InvalidOperationException("Trade hold original unavailable");
            var row=doc.CreateElement("hold");root.AppendChild(row);row.SetAttribute("operation",pair.Key.ToString("N"));row.SetAttribute("digest",hold.Original.Digest);
            row.SetAttribute("progress",hold.Progress.ToString(System.Globalization.CultureInfo.InvariantCulture));row.SetAttribute("entered",hold.Entered?"1":"0");
            row.SetAttribute("needsSave",hold.NeedsSave?"1":"0");row.SetAttribute("grant",hold.Grant?.GrantDigest??string.Empty);
            row.SetAttribute("localGrant",hold.LocalGrant!=null?RebirthRemoteResourceDurableJournal.Digest(hold.LocalGrant.Grant.Image):string.Empty);
            row.InnerText=hold.Original.Image;
        }
        return doc;
    }
    [ThreadStatic]private static Permit permit;
    [ThreadStatic]private static Hold releasing;
    internal sealed class Release
    {
        private readonly Hold hold;
        private Release(Hold hold){this.hold=hold;}
        internal static Release CaptureCurrent()=>releasing!=null?new Release(releasing):null;
        internal bool Matches(RebirthNpcTradeOriginal original)=>original!=null&&hold.Original.Image==original.Image;
        internal bool MatchesNative(Guid operation,string account,string creation,string world,int deaths)=>hold.Original.Operation==operation&&
            hold.Original.Account==account&&hold.Original.Creation==creation&&hold.Original.World==world&&hold.Original.Deaths==deaths;
        internal bool Current()
        {
            try
            {
                var player=hold.Player;
                if(!ThreadManager.IsMainThread()||!ReferenceEquals(releasing,hold)||!holds.TryGetValue(hold.Original.Operation,out var actual)||!ReferenceEquals(actual,hold)||
                    hold.Entered||hold.NeedsSave||!MetadataCurrent(hold)||!Prefix(hold,hold.Progress)||
                    !RebirthNpcTradeOwnerOriginalStore.TryReadFinalDisposition(player,hold.Original,out bool unused)||
                    !RebirthNpcTradeOwnerCheckpoint.TryReadRetainedStage(player,hold.Original,out int step,out int phase)||step!=hold.Progress*2||
                    (unused?phase!=1||hold.Progress!=0||hold.Grant!=null||hold.LocalGrant!=null:phase!=3||hold.Progress!=hold.Changes.Count)||
                    !RebirthStablePlayerIdentity.TryFromLocalPlatform(out var owner)||!RebirthGearNativePlayerFile.TryRead(owner,out var saved)||
                    saved.id!=player.entityId||saved.deaths!=hold.Original.Deaths||saved.bDead||
                    !RebirthNpcTradeService.OwnerAdmission.MatchesNativePolicy(saved,hold.Original,player)||
                    !RebirthRemoteResourcePlacedStationSavedReceipt.TryRead(saved,RebirthNpcTradeOwnerCheckpoint.Marker(hold.Original.Operation),out float savedPhase,out bool hasPhase)||!hasPhase||savedPhase!=phase||
                    !RebirthRemoteResourcePlacedStationSavedReceipt.TryRead(saved,Step(hold.Original.Operation),out float savedStep,out bool hasStep)||(hasStep?savedStep:0f)!=step)return false;
                var expected=Expected(hold,hold.Progress);
                if(!Equal(RebirthGearPlayerDataInventory.ReadSlots(saved,true),expected[0])||!Equal(RebirthGearPlayerDataInventory.ReadSlots(saved,false),expected[1]))return false;
                foreach(string key in new[]{RebirthNpcTradeOwnerCheckpoint.Held,RebirthNpcTradeOwnerCheckpoint.HoldMarker(hold.Original.Operation)})
                    if(player.Buffs.GetCustomVar(key)!=0f||!RebirthRemoteResourcePlacedStationSavedReceipt.TryRead(saved,key,out float value,out bool present)||present&&value!=0f)return false;
                return MetadataCurrent(hold)&&Prefix(hold,hold.Progress)&&ReferenceEquals(releasing,hold);
            }
            catch{return false;}
        }
    }
    internal static Func<Release,bool>[] CaptureRetirement()
    {
        if(!ThreadManager.IsMainThread()||permit!=null||releasing!=null)throw new InvalidOperationException("Trade setter/release still active");
        return holds.Values.Select(value=>{var original=value.Original;return new Func<Release,bool>(release=>release.Matches(original));}).ToArray();
    }
    internal static RebirthNpcTradeOriginal[] CaptureClientNativeReentryMembership(World originalWorld)
    {
        if(!ThreadManager.IsMainThread()||originalWorld?.IsRemote()!=true||permit!=null||releasing!=null||holds.Count>64||
            holds.Values.Any(value=>value?.Original==null||!ReferenceEquals(value.World,originalWorld)||!ReferenceEquals(value.Player?.world,originalWorld)))
            throw new InvalidOperationException("Exact original client native holds required");
        return holds.Values.Select(value=>value.Original).ToArray();
    }
    internal static RebirthNpcTradeOriginal[] CaptureLocalNativeReentryMembership(World originalWorld)
    {
        if(!ThreadManager.IsMainThread()||originalWorld?.IsRemote()!=false||permit!=null||releasing!=null||holds.Count>64||
            holds.Values.Any(value=>value?.Original==null||!ReferenceEquals(value.World,originalWorld)||!ReferenceEquals(value.Player?.world,originalWorld)))
            throw new InvalidOperationException("Exact outgoing native trade holds required");
        return holds.Values.Select(value=>value.Original).ToArray();
    }
    private static void PublishRelease(Hold hold)
    {
        if(releasing!=null)throw new InvalidOperationException("Nested trade release");
        releasing=hold;try{var proof=Release.CaptureCurrent();RebirthNpcSaveQuarantineBundle.ObserveTradeRelease(proof);RebirthNpcTradeLocalOwnerLoad.ObserveRelease(proof);}finally{releasing=null;}
    }
    internal static object CaptureAuditState(EntityPlayerLocal player)
    {
        if(!ThreadManager.IsMainThread())return new{available=false};
        return new{available=player!=null,held=IsHeld(player),holds=holds.Values.Where(value=>ReferenceEquals(value.Player,player)).Select(value=>
        {
            bool saved=RebirthNpcTradeOwnerCheckpoint.TryReadRetainedStage(player,value.Original,out int step,out int phase);
            return new{operation=value.Original.Operation.ToString("N"),original=value.Original.Digest,progress=value.Progress,
                changes=value.Changes.Count,entered=value.Entered,needsSave=value.NeedsSave,hasGrant=value.Grant!=null,hasLocalGrant=value.LocalGrant!=null,
                prefix=Prefix(value,value.Progress),saved,step,phase};
        }).ToArray()};
    }
    internal static string Step(Guid operation)=>"rbNpcTradeStep_"+operation.ToString("N");
    private static string Receipt(RebirthNpcInteractionNetworkClient.NativeTradeResult result)=>
        RebirthNpcTradeOwnerReceipt.Write(result.Original.Operation,result.Original.Digest,result.TerminalDigest,result.Closed);
    internal static bool TryAcknowledgeLocal(EntityPlayerLocal player,RebirthNpcTradeService.OwnerAdmission.LocalPermission permission,RebirthNpcTradeService.HostTerminal terminal)
    {
        if(permission==null||!permission.Current()||terminal==null||!ReferenceEquals(terminal.Prepared,permission.Grant.Prepared)||!terminal.IsRetained()||
            !holds.TryGetValue(permission.Original.Operation,out var hold)||!ReferenceEquals(hold.Player,player)||!ReferenceEquals(hold.LocalGrant,permission)||hold.Grant!=null||
            hold.Entered||hold.NeedsSave||hold.Progress!=hold.Changes.Count||!MetadataCurrent(hold)||!Prefix(hold,hold.Progress)||
            !RebirthNpcTradeOwnerCheckpoint.TryReadRetainedStage(player,hold.Original,out int step,out int phase)||step!=hold.Progress*2||phase!=2&&phase!=3||
            !RebirthNpcTradeOwnerOriginalStore.TryRetainReceipt(player,hold.Original,"ack",RebirthNpcTradeOwnerReceipt.Write(hold.Original.Operation,hold.Original.Digest,terminal.Digest,false))||
            !SavePrefix(hold,hold.Progress*2,3f,true))return false;
        return permission.AcknowledgementCurrent();
    }
    internal static bool TryReleaseLocal(EntityPlayerLocal player,RebirthNpcTradeService.OwnerAdmission.LocalPermission permission,RebirthNpcTradeService.HostClosed closed)
    {
        if(permission==null||!permission.ClosedCurrent(closed)||!holds.TryGetValue(permission.Original.Operation,out var hold)||
            !ReferenceEquals(hold.Player,player)||!ReferenceEquals(hold.LocalGrant,permission)||hold.Grant!=null||hold.Entered||hold.NeedsSave||
            hold.Progress!=hold.Changes.Count||!MetadataCurrent(hold)||!Prefix(hold,hold.Progress)||
            !RebirthNpcTradeOwnerCheckpoint.TryReadRetainedStage(player,hold.Original,out int step,out int phase)||phase!=3||step!=hold.Progress*2||
            !RebirthNpcTradeOwnerOriginalStore.TryRetainReceipt(player,hold.Original,"closed",RebirthNpcTradeOwnerReceipt.Write(hold.Original.Operation,hold.Original.Digest,closed.TerminalDigest,true))||
            !permission.ClosedCurrent(closed)||!ClearHeldStamp(hold))return false;
        holds.Remove(hold.Original.Operation);return true;
    }
    internal static bool TryAcknowledge(EntityPlayerLocal player,RebirthNpcInteractionNetworkClient.NativeTradeResult result)
    {
        if(result==null||result.Closed||!result.Current()||!holds.TryGetValue(result.Original.Operation,out var hold)||
            !ReferenceEquals(hold.Player,player)||hold.Original.Digest!=result.Original.Digest||hold.Entered||hold.NeedsSave||
            hold.Progress!=hold.Changes.Count||!MetadataCurrent(hold)||!Prefix(hold,hold.Progress)||
            !RebirthNpcTradeOwnerOriginalStore.TryRetainReceipt(player,hold.Original,"ack",Receipt(result))||
            !SavePrefix(hold,hold.Progress*2,3f,true))return false;
        GameManager.Instance.TriggerSendOfLocalPlayerDataFile(0f);return result.Current();
    }
    internal static bool TryRelease(EntityPlayerLocal player,RebirthNpcInteractionNetworkClient.NativeTradeResult result)
    {
        if(result==null||!result.Closed||!result.Current()||!holds.TryGetValue(result.Original.Operation,out var hold)||
            !ReferenceEquals(hold.Player,player)||hold.Original.Digest!=result.Original.Digest||hold.Entered||hold.NeedsSave||
            hold.Progress!=hold.Changes.Count||!MetadataCurrent(hold)||!Prefix(hold,hold.Progress)||
            player.Buffs.GetCustomVar(RebirthNpcTradeOwnerCheckpoint.Marker(hold.Original.Operation))!=3f||
            !RebirthNpcTradeOwnerOriginalStore.TryRetainReceipt(player,hold.Original,"closed",Receipt(result)))return false;
        if(!ClearHeldStamp(hold))return false;holds.Remove(hold.Original.Operation);return true;
    }
    internal static bool TryReleaseUnused(EntityPlayerLocal player,RebirthNpcInteractionNetworkClient.NativeTradeUnused unused)
    {
        // No inventory setter, compensation or terminal payment. Only a hold
        // which never received a grant or entered any original setter qualifies.
        if(unused==null||!unused.Current()||!holds.TryGetValue(unused.Original.Operation,out var hold)||
            !ReferenceEquals(hold.Player,player)||hold.Original.Digest!=unused.Original.Digest||hold.Grant!=null||hold.LocalGrant!=null||
            hold.Progress!=0||hold.Entered||hold.NeedsSave||!Current(hold)||!Prefix(hold,0)||
            player.Buffs.GetCustomVar(Step(hold.Original.Operation))!=0f||
            player.Buffs.GetCustomVar(RebirthNpcTradeOwnerCheckpoint.Marker(hold.Original.Operation))!=1f||
            !RebirthNpcTradeOwnerCheckpoint.TrySaveBefore(player,hold.Original)||
            !RebirthNpcTradeOwnerOriginalStore.TryRetainReceipt(player,hold.Original,"unused",unused.Receipt)||
            !unused.Current()||!Current(hold)||!Prefix(hold,0))return false;
        if(!ClearHeldStamp(hold))return false;holds.Remove(hold.Original.Operation);return true;
    }
    internal static bool IsHeld(EntityPlayerLocal player)=>player!=null&&ReferenceEquals(player.world,GameManager.Instance?.World)&&
        ReferenceEquals(player.world?.GetPrimaryPlayer(),player)&&!player.IsDead()&&
        (player.Buffs.GetCustomVar(RebirthNpcTradeOwnerCheckpoint.Held)==1f&&player.Buffs.GetCustomVar(RebirthNpcTradeOwnerCheckpoint.HeldLife)==player.Died||
        holds.Values.Any(hold=>ReferenceEquals(hold.Player,player)&&ReferenceEquals(hold.World,player.world)&&hold.Original.Deaths==player.Died));
    internal static bool TryHoldLoaded(RebirthNpcTradeOwnerLoad.Source source,RebirthNpcTradeOriginal original,out int phase)
    {
        phase=0;
        try
        {
            var player=source?.Player;
            if(source==null||original==null||source.Operation!=original.Operation||!source.Current(player)||!EmptyCursor(player)||
                holds.Values.Any(other=>other.Original.Operation!=original.Operation&&ReferenceEquals(other.Player,player)&&ReferenceEquals(other.World,player.world))||
                !RebirthRemoteResourceOwnerAction.CanReattachNativeTrade(player,source)||!source.PersistCurrent(original)||
                !RebirthNpcTradeOwnerCheckpoint.TryReadRetainedStage(player,original,out int step,out phase))return false;
            return BindLoadedHold(player,original,step,ref phase);
        }
        catch{return false;}
    }
    internal static bool TryHoldLoaded(RebirthNpcTradeLocalOwnerLoad.Source source,RebirthNpcTradeOriginal original,out int phase)
    {
        phase=0;
        try
        {
            var player=source?.Player;
            if(source==null||!source.Matches(original)||!EmptyCursor(player)||
                holds.Values.Any(other=>other.Original.Operation!=original.Operation&&ReferenceEquals(other.Player,player)&&ReferenceEquals(other.World,player.world))||
                !RebirthRemoteResourceOwnerAction.CanReattachNativeLocalTrade(player,source)||!source.PersistCurrent(original)||
                !RebirthNpcTradeOwnerCheckpoint.TryReadRetainedStage(player,original,out int step,out phase))return false;
            return BindLoadedHold(player,original,step,ref phase);
        }
        catch{return false;}
    }
    private static bool BindLoadedHold(EntityPlayerLocal player,RebirthNpcTradeOriginal original,int step,ref int phase)
    {
        try
        {
            if(holds.TryGetValue(original.Operation,out var prior))
            {
                if(prior.Original.Digest!=original.Digest)return false;
                if(ReferenceEquals(prior.Player,player)&&ReferenceEquals(prior.World,player.world))
                {
                    // An unknown entered phase-1 setter remains held. A real
                    // native join with independently saved full terminal After
                    // can supersede the obsolete transient view of that SAME
                    // operation, without invoking or inferring a setter.
                    if(phase==1)return !prior.Entered&&TryResumeHeld(player,original,out phase);
                    if(step!=RebirthNpcTradePrefix.Changes(original)*2)return false;
                }
                // Only the actual native join of the SAME immutable original
                // can supersede a no-longer-current actor's transient view.
                else if(ReferenceEquals(prior.World,GameManager.Instance?.World)&&ReferenceEquals(prior.Player,prior.World.GetPrimaryPlayer()))return false;
            }
            else if(holds.Count>=64)return false;
            var changes=new List<Change>();
            foreach(bool bag in new[]{true,false})
            {
                var before=original.CopySlots(bag,false);var after=original.CopySlots(bag,true);
                for(int slot=0;slot<before.Length;slot++)if(!Same(before[slot],after[slot]))changes.Add(new Change{Bag=bag,Slot=slot,Value=after[slot].Clone()});
            }
            var hold=new Hold{Player=player,World=player.world,Bag=player.bag.ItemGrid,Belt=player.inventory.ItemGrid,Original=original,Changes=changes,Progress=step/2};
            if(!MetadataCurrent(hold)||!Prefix(hold,hold.Progress))return false;
            holds[original.Operation]=hold;return true;
        }
        catch{return false;}
    }
    private static bool ClearHeldStamp(Hold hold)
    {
        if(!MetadataCurrent(hold)||hold.Entered||hold.NeedsSave||!Prefix(hold,hold.Progress)||
            !RebirthNpcTradeOwnerCheckpoint.TryReadRetainedStage(hold.Player,hold.Original,out int step,out int phase)||step!=hold.Progress*2||
            holds.Values.Any(other=>!ReferenceEquals(other,hold)&&ReferenceEquals(other.Player,hold.Player)&&ReferenceEquals(other.World,hold.World)))return false;
        var player=hold.Player;
        player.Buffs.SetCustomVar(RebirthNpcTradeOwnerCheckpoint.HoldMarker(hold.Original.Operation),0f,true);
        player.Buffs.SetCustomVar(RebirthNpcTradeOwnerCheckpoint.Held,0f,true);GameManager.Instance.SaveLocalPlayerData();
        if(!MetadataCurrent(hold)||!RebirthStablePlayerIdentity.TryFromLocalPlatform(out var owner)||!RebirthGearNativePlayerFile.TryRead(owner,out var saved)||
            saved.id!=player.entityId||saved.deaths!=hold.Original.Deaths||saved.bDead)return false;
        foreach(string key in new[]{RebirthNpcTradeOwnerCheckpoint.HoldMarker(hold.Original.Operation),RebirthNpcTradeOwnerCheckpoint.Held})
            if(!RebirthRemoteResourcePlacedStationSavedReceipt.TryRead(saved,key,out float value,out bool present)||present&&value!=0f)return false;
        var expected=Expected(hold,hold.Progress);
        if(!Equal(RebirthGearPlayerDataInventory.ReadSlots(saved,true),expected[0])||!Equal(RebirthGearPlayerDataInventory.ReadSlots(saved,false),expected[1])||
            !RebirthNpcTradeOwnerCheckpoint.TryReadRetainedStage(player,hold.Original,out int afterStep,out int afterPhase)||afterStep!=step||afterPhase!=phase||
            !MetadataCurrent(hold)||!Prefix(hold,hold.Progress))return false;
        GameManager.Instance.TriggerSendOfLocalPlayerDataFile(0f);PublishRelease(hold);return true;
    }
    internal static bool TryReleaseHistorical(RebirthNpcTradeOwnerLoad.Source source,RebirthNpcTradeOriginal original)
    {
        if(!RebirthNpcTradeOwnerOriginalStore.TryReadFinalDisposition(source?.Player,original,out bool unused)||
            !TryHoldLoaded(source,original,out int phase)||!holds.TryGetValue(original.Operation,out var hold)||
            (unused?phase!=1||hold.Progress!=0||hold.Grant!=null||hold.LocalGrant!=null:phase!=3||hold.Progress!=hold.Changes.Count)||
            !RebirthNpcTradeOwnerOriginalStore.TryReadFinalDisposition(hold.Player,original,out bool stillUnused)||stillUnused!=unused||
            !ClearHeldStamp(hold))return false;
        holds.Remove(original.Operation);return true;
    }
    internal static bool TryReleaseHistorical(RebirthNpcTradeLocalOwnerLoad.Source source,RebirthNpcTradeOriginal original)
    {
        if(source==null||!source.Matches(original)||!RebirthNpcTradeOwnerOriginalStore.TryReadFinalDisposition(source.Player,original,out bool unused)||
            !TryHoldLoaded(source,original,out int phase)||!holds.TryGetValue(original.Operation,out var hold)||
            (unused?phase!=1||hold.Progress!=0||hold.Grant!=null||hold.LocalGrant!=null:phase!=3||hold.Progress!=hold.Changes.Count)||
            !RebirthNpcTradeOwnerOriginalStore.TryReadFinalDisposition(hold.Player,original,out bool stillUnused)||stillUnused!=unused||!ClearHeldStamp(hold))return false;
        holds.Remove(original.Operation);return true;
    }
    internal static bool TryReleaseLocalHostHistory(RebirthNpcTradeLocalOwnerLoad.Source source,RebirthNpcTradeOriginal original,RebirthNpcTradeClosedHistory history)
    {
        if(source==null||history==null||history.Operation!=original.Operation||history.OriginalDigest!=original.Digest||!source.Matches(original)||
            !TryHoldLoaded(source,original,out int phase)||phase!=3||!holds.TryGetValue(original.Operation,out var hold)||hold.Entered||hold.NeedsSave||
            hold.Progress!=hold.Changes.Count||!MetadataCurrent(hold)||!Prefix(hold,hold.Progress)||
            !RebirthNpcTradeOwnerOriginalStore.TryReadAcknowledgement(hold.Player,original,out var terminal)||terminal!=history.TerminalDigest||
            !RebirthNpcTradeOwnerOriginalStore.TryRetainReceipt(hold.Player,original,"closed",RebirthNpcTradeOwnerReceipt.Write(original.Operation,original.Digest,terminal,true))||
            !ClearHeldStamp(hold))return false;
        holds.Remove(original.Operation);return true;
    }
    internal static bool TryReleaseLocalUnused(RebirthNpcTradeLocalOwnerLoad.Source source,RebirthNpcTradeService.HostUnused unused)
    {
        if(source==null||unused==null||!source.Matches(unused.Original)||!unused.ReadRetained()||!TryHoldLoaded(source,unused.Original,out int phase)||phase!=1||
            !holds.TryGetValue(unused.Original.Operation,out var hold)||hold.Progress!=0||hold.Grant!=null||hold.LocalGrant!=null||hold.Entered||hold.NeedsSave||
            !MetadataCurrent(hold)||!Prefix(hold,0)||!RebirthNpcTradeOwnerCheckpoint.TryReadRetainedStage(hold.Player,hold.Original,out int step,out int savedPhase)||step!=0||savedPhase!=1||
            !RebirthNpcTradeOwnerOriginalStore.TryRetainReceipt(hold.Player,hold.Original,"unused",RebirthNpcTradeOwnerReceipt.WriteUnused(hold.Original.Operation,hold.Original.Digest,unused.Digest))||
            !unused.ReadRetained()||!ClearHeldStamp(hold))return false;
        holds.Remove(unused.Original.Operation);return true;
    }
    internal static bool TryResumeHeld(EntityPlayerLocal player,RebirthNpcTradeOriginal original,out int phase)
    {
        phase=0;
        try
        {
            if(original==null||!holds.TryGetValue(original.Operation,out var hold)||!ReferenceEquals(hold.Player,player)||
                hold.Original.Digest!=original.Digest||hold.Entered||!MetadataCurrent(hold)||!Prefix(hold,hold.Progress))return false;
            if(!RebirthNpcTradeOwnerCheckpoint.TryReadRetainedStage(player,original,out int step,out int savedPhase))
            {
                // Only this still-live hold's positively returned setters may
                // finish their metadata save after connection replacement.
                // Cold/entered unknown work cannot use this warm latch.
                float phaseValue=player.Buffs.GetCustomVar(RebirthNpcTradeOwnerCheckpoint.Marker(original.Operation));
                if(phaseValue!=1f&&phaseValue!=2f&&phaseValue!=3f)return false;
                int livePhase=(int)phaseValue;
                if(hold.Grant==null&&hold.LocalGrant==null||!(hold.NeedsSave&&livePhase==1||hold.Progress==hold.Changes.Count&&(livePhase==2||livePhase==3))||
                    !SavePrefix(hold,hold.Progress*2,livePhase,true)||
                    !RebirthNpcTradeOwnerCheckpoint.TryReadRetainedStage(player,original,out step,out savedPhase))return false;
            }
            if(step!=hold.Progress*2)return false;
            hold.NeedsSave=false;phase=savedPhase;return true;
        }
        catch{return false;}
    }
    internal static bool Consume(ItemStackGrid grid,int slot,ItemStack value)
    {
        var once=permit;if(once==null||once.Used||!ReferenceEquals(once.Grid,grid)||once.Slot!=slot||!ReferenceEquals(once.Value,value)||
            once.Hold.Grant==null&&once.Hold.LocalGrant==null||!Current(once.Hold)||!Prefix(once.Hold,once.Hold.Progress))return false;
        once.Used=true;return true;
    }
    internal static bool TryHoldBefore(EntityPlayerLocal player,RebirthNpcTradeOriginal original)
    {
        try
        {
            if(original==null||!RebirthNpcTradeOwnerOriginalStore.IsRetained(player,original))return false;
            if(holds.TryGetValue(original.Operation,out var retained))return ReferenceEquals(retained.Player,player)&&
                retained.Original.Digest==original.Digest&&retained.Progress==0&&!retained.Entered&&Current(retained)&&
                RebirthNpcTradeOwnerCheckpoint.TrySaveBefore(player,original);
            if(!RebirthRemoteResourceOwnerAction.CanPrepareNativeOriginal(player)||!EmptyCursor(player)||holds.Count>=64||
                !original.MatchesSlots(player.bag.ItemGrid.items,player.inventory.ItemGrid.items,false)||
                player.Buffs.GetCustomVar(Step(original.Operation))!=0f)return false;
            var changes=new List<Change>();
            foreach(bool bag in new[]{true,false})
            {
                var before=original.CopySlots(bag,false);var after=original.CopySlots(bag,true);
                for(int i=0;i<before.Length;i++)if(!Same(before[i],after[i]))changes.Add(new Change{Bag=bag,Slot=i,Value=after[i].Clone()});
            }
            var hold=new Hold{Player=player,World=player.world,Bag=player.bag.ItemGrid,Belt=player.inventory.ItemGrid,Original=original,Changes=changes};
            holds.Add(original.Operation,hold);
            return Current(hold)&&RebirthNpcTradeOwnerCheckpoint.TrySaveBefore(player,original);
        }
        catch{return false;}
    }
    internal static bool TryApply(EntityPlayerLocal player,RebirthNpcInteractionNetworkClient.NativeTradeGrant grant)
    {
        try
        {
            if(grant==null||!grant.Current())return false;var original=grant.Original;
            if(!holds.TryGetValue(original.Operation,out var hold))
            {
                if(!TryHoldBefore(player,original)||!holds.TryGetValue(original.Operation,out hold))return false;
            }
            if(!ReferenceEquals(hold.Player,player)||hold.LocalGrant!=null||hold.Original.Digest!=original.Digest||hold.Grant!=null&&hold.Grant.GrantDigest!=grant.GrantDigest)return false;
            hold.Grant=grant;
            return ApplyHeld(hold);
        }
        catch{return false;}
        finally{permit=null;}
    }
    internal static bool TryApplyLocal(EntityPlayerLocal player,RebirthNpcTradeService.OwnerAdmission.LocalPermission grant)
    {
        try
        {
            if(grant==null||!grant.Current())return false;var original=grant.Original;
            if(!holds.TryGetValue(original.Operation,out var hold)&&(!TryHoldBefore(player,original)||!holds.TryGetValue(original.Operation,out hold)))return false;
            if(!ReferenceEquals(hold.Player,player)||hold.Grant!=null||hold.Original.Digest!=original.Digest||hold.LocalGrant!=null&&hold.LocalGrant.Grant.Image!=grant.Grant.Image)return false;
            hold.LocalGrant=grant;return ApplyHeld(hold);
        }
        catch{return false;}
        finally{permit=null;}
    }
    private static bool ApplyHeld(Hold hold)
    {
        try
        {
            if(!Current(hold)||hold.Entered||!Prefix(hold,hold.Progress))return false;
            if(hold.NeedsSave)
            {
                if(!SavePrefix(hold,hold.Progress*2,1f))return false;hold.NeedsSave=false;
            }
            while(hold.Progress<hold.Changes.Count)
            {
                if(!Current(hold)||!Prefix(hold,hold.Progress)||!SavePrefix(hold,hold.Progress*2+1,1f))return false;
                var change=hold.Changes[hold.Progress];var grid=change.Bag?hold.Bag:hold.Belt;
                var once=new Permit{Hold=hold,Grid=grid,Slot=change.Slot,Value=change.Value.Clone()};
                hold.Entered=true;permit=once;
                try{grid.SetItem(change.Slot,once.Value);}finally{permit=null;}
                if(!once.Used||!Current(hold)||!Prefix(hold,hold.Progress+1))return false;
                // Only a returned, consumed original setter advances the warm
                // continuation. Unknown entered work remains held.
                hold.Entered=false;hold.Progress++;hold.NeedsSave=true;
                if(!SavePrefix(hold,hold.Progress*2,1f))return false;hold.NeedsSave=false;
            }
            if(!Prefix(hold,hold.Changes.Count)||!SavePrefix(hold,hold.Progress*2,2f))return false;
            GameManager.Instance.TriggerSendOfLocalPlayerDataFile(0f);
            return Current(hold)&&Prefix(hold,hold.Changes.Count);
        }
        catch{return false;}
        finally{permit=null;}
    }
    private static bool Current(Hold hold)
    {
        if(hold==null)return false;
        bool granted=hold.Grant!=null||hold.LocalGrant!=null;
        return !(hold.Grant!=null&&hold.LocalGrant!=null)&&
            (hold.Grant==null||hold.Grant.Original.Digest==hold.Original.Digest&&hold.Grant.Current())&&
            (hold.LocalGrant==null||hold.LocalGrant.Original.Digest==hold.Original.Digest&&hold.LocalGrant.Current())&&MetadataCurrent(hold,granted);
    }
    private static bool MetadataCurrent(Hold hold,bool descriptionChecked=false)
    {
        if(hold==null||!descriptionChecked&&!RebirthNpcTradeOwnerOriginalStore.IsRetained(hold.Player,hold.Original)||!ReferenceEquals(hold.Player.world,hold.World)||
            !ReferenceEquals(hold.Bag,hold.Player.bag?.ItemGrid)||!ReferenceEquals(hold.Belt,hold.Player.inventory?.ItemGrid)||!EmptyCursor(hold.Player)||
            RebirthToolbeltCapacity.GetOwnedSlotCount(hold.Player,hold.Belt.Length)!=hold.Original.OwnedBeltSlots)return false;
        foreach(bool bag in new[]{true,false})
        {
            var locks=hold.Original.CopyLocks(bag);var grid=bag?hold.Bag:hold.Belt;
            if(locks.Length!=grid.Length)return false;
            for(int i=0;i<locks.Length;i++)if(locks[i]!=(RebirthVehicleNativePolicy.IsLocked(hold.Player,bag,i)||!bag&&i>=hold.Original.OwnedBeltSlots))return false;
        }
        return true;
    }
    private static bool EmptyCursor(EntityPlayerLocal player)=>player?.PlayerUI?.xui?.DragAndDropWindow?.CurrentStack?.IsEmpty()==true;
    private static bool Same(ItemStack left,ItemStack right)=>RebirthStationGridIngredients.IsSameStackSnapshot(left,right);
    private static ItemStack[][] Expected(Hold hold,int progress)
    {
        var cells=new[]{hold.Original.CopySlots(true,false),hold.Original.CopySlots(false,false)};
        for(int i=0;i<progress;i++){var change=hold.Changes[i];cells[change.Bag?0:1][change.Slot]=change.Value.Clone();}
        return cells;
    }
    private static bool Equal(ItemStack[] actual,ItemStack[] expected)=>actual?.Length==expected.Length&&actual.Select((cell,index)=>Same(cell,expected[index])).All(value=>value);
    private static bool Prefix(Hold hold,int progress)
    {var expected=Expected(hold,progress);return Equal(hold.Bag.items,expected[0])&&Equal(hold.Belt.items,expected[1]);}
    private static bool SavePrefix(Hold hold,int step,float phase,bool metadataOnly=false)
    {
        bool PurposeCurrent()=>metadataOnly?MetadataCurrent(hold):Current(hold);
        if(!PurposeCurrent()||!Prefix(hold,hold.Progress))return false;
        var player=hold.Player;string marker=RebirthNpcTradeOwnerCheckpoint.Marker(hold.Original.Operation),stepMarker=Step(hold.Original.Operation);
        player.Buffs.SetCustomVar(marker,phase,true);player.Buffs.SetCustomVar(stepMarker,step,true);
        GameManager.Instance.SaveLocalPlayerData();
        if(!PurposeCurrent()||!RebirthStablePlayerIdentity.TryFromLocalPlatform(out var owner)||owner.CanonicalId!=hold.Original.Account||
            !RebirthGearNativePlayerFile.TryRead(owner,out var saved)||saved.id!=player.entityId||saved.deaths!=hold.Original.Deaths||saved.bDead||
            !RebirthRemoteResourcePlacedStationSavedReceipt.TryRead(saved,marker,out float savedPhase,out bool present)||!present||savedPhase!=phase||
            !RebirthRemoteResourcePlacedStationSavedReceipt.TryRead(saved,stepMarker,out float savedStep,out bool stepPresent)||
            (step==0?stepPresent&&savedStep!=0:!stepPresent||savedStep!=step))return false;
        var expected=Expected(hold,hold.Progress);
        return Equal(RebirthGearPlayerDataInventory.ReadSlots(saved,true),expected[0])&&Equal(RebirthGearPlayerDataInventory.ReadSlots(saved,false),expected[1])&&
            PurposeCurrent()&&Prefix(hold,hold.Progress);
    }
}
