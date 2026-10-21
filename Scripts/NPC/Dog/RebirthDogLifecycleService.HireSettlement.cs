using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using UnityEngine;

public static partial class RebirthDogLifecycleService
{
    private sealed class HireDebitSource
    {
        internal bool Belt;internal int Slot;internal ItemStack[] Grid;
        internal ItemStack Before,After,OriginalReference,AppliedReference;
    }
    private sealed class HireSettlement
    {
        internal World World;internal EntityPlayer Player;internal EntityRebirthDogCompanion Dog;
        internal RebirthNpcRuntimeState State;internal object Inventory,Bag;internal string Owner,Account,Creation,Save,Directory,Original;internal int Deaths;
        internal RebirthNpcOrderState PreviousOrder;internal Vector3 PreviousGuard;internal bool PreviousHasGuard;
        internal readonly List<HireDebitSource> Sources=new List<HireDebitSource>();
        internal bool EnteredDebit,Debited,OwnershipAttempted,OwnershipSaved,OwnershipArchived,ReversalSaved,ReversalArchived,RefundEntered,Refunded,Busy,Terminal;
        internal ulong RecoveryGeneration;
        internal Guid RemoteNonce;
        internal bool ObservationRetired;
        internal bool RemoteBaselineSent,RemoteSettledSent,RemoteClientReady;
    }
    private static readonly Dictionary<RebirthNpcStableId,HireSettlement> HireSettlements=new Dictionary<RebirthNpcStableId,HireSettlement>();
    [ThreadStatic] private static HireSettlement CurrentHireMutation;
    internal static bool HasRetainedHireForShutdown(World world,string save)
    {
        if(!ThreadManager.IsMainThread())return true;
        foreach(var original in HireSettlements.Values)
            if(!original.Terminal&&ReferenceEquals(original.World,world)&&SameHirePath(original.Save,save))return true;
        return false;
    }
    private static bool IsHireCustodyHeld(RebirthNpcStableId id)
    {
        try{return HireSettlements.ContainsKey(id)||HasColdHireHold(id);}catch{return true;}
    }
    internal static bool AllowsHireMutation(EntityRebirthNPC entity)
    {
        var dog=entity as EntityRebirthDogCompanion;if(dog?.RebirthRuntimeState==null)return true;
        try
        {
            if(HireSettlements.TryGetValue(dog.RebirthRuntimeState.StableId,out var original))
                return ReferenceEquals(original.Dog,dog)&&ReferenceEquals(CurrentHireMutation,original)&&original.Busy&&HireCurrent(original);
            return !HasColdHireHold(dog.RebirthRuntimeState.StableId);
        }
        catch{return false;}
    }
    private const string HireRetained="Dog hire settlement is retained for the original owner; no additional meat was charged.";
#if REBIRTH_DEBUG
    internal static Action<string> HireScenarioBoundary;
#endif
    private static void ObserveHireScenario(string stage)
    {
#if REBIRTH_DEBUG
        if(ThreadManager.IsMainThread()&&GamePrefs.GetString(EnumGamePrefs.GameName)=="CodexTest"){HoldHireScenario(stage);HireScenarioBoundary?.Invoke(stage);}
#endif
    }
    private static bool SameHirePath(string a,string b)=>string.Equals(Path.GetFullPath(a),Path.GetFullPath(b),
        Environment.OSVersion.Platform==PlatformID.Win32NT?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal);
    private static bool HireCurrent(HireSettlement value)
    {
        if(value==null||value.ObservationRetired)return false;
        foreach(var source in value.Sources)if(!ReferenceEquals(source.Grid,source.Belt?value.Player.inventory?.ItemGrid.items:value.Player.bag?.ItemGrid.items))return false;
        return ThreadManager.IsMainThread()&&ReferenceEquals(value.World,GameManager.Instance?.World)&&
            ReferenceEquals(value.Player.world,value.World)&&ReferenceEquals(value.Dog.world,value.World)&&ReferenceEquals(value.World.GetEntity(value.Player.entityId),value.Player)&&ReferenceEquals(value.World.GetEntity(value.Dog.entityId),value.Dog)&&
            ReferenceEquals(value.State,value.Dog.RebirthRuntimeState)&&!value.Dog.IsDead()&&!value.Player.IsDead()&&
            ReferenceEquals(value.Player.inventory,value.Inventory)&&ReferenceEquals(value.Player.bag,value.Bag)&&value.Player.Died==value.Deaths&&
            (string.IsNullOrEmpty(value.Creation)||RebirthCamperCharacterAccess.CurrentCreation(value.Player)==value.Creation)&&
            SameHirePath(value.Save,GameIO.GetSaveGameDir())&&value.RecoveryGeneration==RebirthNpcRecoveryGeneration.RequireCurrent()&&
            TryResolveOwnerId(value.Player,out string owner)&&string.Equals(owner,value.Owner,StringComparison.Ordinal)&&
            RebirthStablePlayerIdentity.TryResolveServerEntity(value.Player,out var identity)&&identity.CanonicalId==value.Account;
    }
    private static string HireRoot()=>Path.Combine(GameIO.GetSaveGameDir(),"RebirthData","DogHire");
    private static void RequireHirePath(string path)
    {
        for(var node=new DirectoryInfo(Path.GetFullPath(path));node!=null;node=node.Parent)
            if(node.Exists&&(node.Attributes&FileAttributes.ReparsePoint)!=0)throw new IOException("Dog hire path is a reparse point");
    }
    private static string NativeHireItem(ItemStack stack)
    {
        int length;using(var counter=new RebirthRemoteResourcePlacedStationStreams.Counter(65536))
        {using(var writer=MemoryPools.poolBinaryWriter.AllocSync(true)){writer.SetBaseStream(counter);stack.itemValue.Write(writer);writer.Flush();}length=checked((int)counter.Length);}
        var bytes=new byte[length];using(var stream=new MemoryStream(bytes,0,bytes.Length,true,true))
        {using(var writer=MemoryPools.poolBinaryWriter.AllocSync(true)){writer.SetBaseStream(stream);stack.itemValue.Write(writer);writer.Flush();}if(stream.Position!=length)throw new InvalidDataException("Dog hire item changed during native measure");}
        return Convert.ToBase64String(bytes);
    }
    private static void WriteHireRecord(HireSettlement value,string stage)
    {
        RequireHirePath(value.Directory);
        var record=new XElement("dogHireStage",new XAttribute("version",1),new XAttribute("stage",stage),new XElement("original",value.Original));
        string path=Path.Combine(value.Directory,stage+".xml");
        byte[] bytes=new UTF8Encoding(false,true).GetBytes(record.ToString(SaveOptions.DisableFormatting));
        if(bytes.Length>1048576)throw new InvalidDataException("Dog hire original exceeds bound");
        if(File.Exists(path))
        {
            if((File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0||!File.ReadAllBytes(path).SequenceEqual(bytes))throw new IOException("Dog hire stage conflicts");
            return;
        }
        using(var file=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None)){file.Write(bytes,0,bytes.Length);file.Flush(true);}
        if(!File.ReadAllBytes(path).SequenceEqual(bytes))throw new IOException("Dog hire stage readback differs");
        ObserveHireScenario(stage);
    }
    private static bool HasColdHireHold(RebirthNpcStableId id)
    {
        string root=HireRoot();if(!Directory.Exists(root))return false;RequireHirePath(root);
        var directories=Directory.EnumerateDirectories(root,id+".*").Take(65).ToArray();
        if(directories.Length>64)return true;
        foreach(string directory in directories)
        {
            RequireHirePath(directory);
            string prepared=Path.Combine(directory,"prepared.xml");
            if(!File.Exists(prepared))return true;
            var preparedInfo=new FileInfo(prepared);if(preparedInfo.Length>1048576||(preparedInfo.Attributes&FileAttributes.ReparsePoint)!=0)return true;
            byte[] original=File.ReadAllBytes(prepared);if(original.Length>1048576)return true;
            XElement parsed;
            using(var reader=XmlReader.Create(new MemoryStream(original),new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=1048576}))parsed=XElement.Load(reader);
            if(parsed.Name!="dogHireStage"||parsed.Attributes().Count()!=2||(string)parsed.Attribute("stage")!="prepared"||(int?)parsed.Attribute("version")!=1||parsed.Elements().Count()!=1||parsed.Element("original")==null||parsed.Element("original").HasElements||parsed.Element("original").HasAttributes)return true;
            string full=(string)parsed.Element("original");var originalImage=ReadRemoteHireXml(full);
            if((string)originalImage.Attribute("dog")!=id.ToString()||!Guid.TryParseExact((string)originalImage.Attribute("operation"),"N",out var operation)||operation==Guid.Empty||
                !SameHirePath((string)originalImage.Attribute("save"),GameIO.GetSaveGameDir())||Path.GetFileName(directory)!=id+"."+operation.ToString("N"))return true;
            int terminal=0;
            foreach(string stage in new[]{"committed","refunded","never-debited"})
            {
                string path=Path.Combine(directory,stage+".xml");if(!File.Exists(path))continue;
                var info=new FileInfo(path);if(info.Length>1048576||(info.Attributes&FileAttributes.ReparsePoint)!=0)return true;
                using(var reader=XmlReader.Create(path,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=1048576}))
                {
                    var node=XElement.Load(reader);
                    if(node.Name!="dogHireStage"||node.Attributes().Count()!=2||node.Elements().Count()!=1||node.Element("original")==null||node.Element("original").HasElements||node.Element("original").HasAttributes||(string)node.Attribute("stage")!=stage||(int?)node.Attribute("version")!=1||(string)node.Element("original")!=full)return true;
                    terminal++;
                }
            }
            if(terminal!=1)return true;
        }
        return false;
    }
    private static bool TryResumeHireSettlement(World world,EntityPlayer player,EntityRebirthDogCompanion dog,out bool handled,out string reason)
    {
        handled=false;reason=string.Empty;
        try
        {
            if(!ThreadManager.IsMainThread()){handled=true;reason=HireRetained;return false;}
            RetireReplacedHireWorld();
            if(HireSettlements.TryGetValue(dog.RebirthRuntimeState.StableId,out var original))
            {
                handled=true;
                if(!ReferenceEquals(original.World,world)||!ReferenceEquals(original.Player,player)||!ReferenceEquals(original.Dog,dog)||!HireCurrent(original)||original.Busy)
                {reason=HireRetained;return false;}
                return FinishHireSettlement(original,out reason);
            }
            TryCompleteColdHireOriginal(world,player,dog);
            if(HasColdHireHold(dog.RebirthRuntimeState.StableId)){handled=true;reason=HireRetained;return false;}
            return false;
        }
        catch(Exception ex){handled=true;reason=HireRetained;Log.Warning("[REBIRTH Dog] hire original retained: "+ex.Message);return false;}
    }
    private static bool BeginHireSettlement(World world,EntityPlayer player,EntityRebirthDogCompanion dog,string owner,ItemValue meat,out string reason)
    {
        reason=HireRetained;HireSettlement original=null;
        try
        {
            if(!ThreadManager.IsMainThread()||!ReferenceEquals(world,GameManager.Instance?.World))return false;
            if(player is EntityPlayerLocal local&&CompetingHireCustody(local))return false;
            if(!RebirthStablePlayerIdentity.TryResolveServerEntity(player,out var account))return false;
            original=new HireSettlement{World=world,Player=player,Dog=dog,State=dog.RebirthRuntimeState,Owner=owner,Account=account.CanonicalId,Creation=RebirthCamperCharacterAccess.CurrentCreation(player),Deaths=player.Died,
                Inventory=player.inventory,Bag=player.bag,Save=Path.GetFullPath(GameIO.GetSaveGameDir()),
                PreviousOrder=dog.RebirthRuntimeState.Order,PreviousGuard=dog.RebirthRuntimeState.GuardPosition,
                PreviousHasGuard=dog.RebirthRuntimeState.HasGuardPosition,RecoveryGeneration=RebirthNpcRecoveryGeneration.RequireCurrent()};
            if(!ReserveHireOwner(original))return false;
            int remaining=HireItemCount;
            foreach(bool belt in new[]{true,false})
            {
                ItemStack[] grid=belt?player.inventory?.ItemGrid.items:player.bag?.ItemGrid.items;
                for(int i=0;grid!=null&&i<grid.Length&&remaining>0;i++)
                {
                    var stack=grid[i];if(stack==null||stack.IsEmpty()||stack.itemValue.type!=meat.type)continue;
                    string encoded=NativeHireItem(stack);if(!RebirthNativeItemConformanceReader.TryMeasureCanonicalV9Structure(encoded,out var measured))throw new InvalidDataException("Dog hire source native structure unavailable");
                    int take=Math.Min(remaining,stack.count);var after=stack.Clone();after.count-=take;
                    original.Sources.Add(new HireDebitSource{Belt=belt,Slot=i,Grid=grid,Before=stack.Clone(),After=after.count>0?after:ItemStack.Empty.Clone(),OriginalReference=stack});remaining-=take;
                }
            }
            if(remaining!=0){original.Terminal=true;ReleaseHireOwner(original);reason=Localization.Get("xuiRebirthDogHireNeedsRawMeat");return false;}
            if(!BindRemoteHire(original)){original.Terminal=true;ReleaseHireOwner(original);return false;}
            string operation=Guid.NewGuid().ToString("N");string root=HireRoot();RequireHirePath(root);Directory.CreateDirectory(root);
            original.Directory=Path.Combine(root,original.State.StableId+"."+operation);
            var image=new XElement("dogHireOriginal",new XAttribute("version",1),new XAttribute("operation",operation),new XAttribute("save",original.Save),
                new XAttribute("dog",original.State.StableId.ToString()),new XAttribute("owner",owner),new XAttribute("account",original.Account),new XAttribute("creation",original.Creation??string.Empty),new XAttribute("deaths",player.Died),new XAttribute("price",HireItemCount),
                new XAttribute("order",original.PreviousOrder),new XAttribute("guard",original.PreviousHasGuard),
                new XAttribute("x",original.PreviousGuard.x.ToString("R",System.Globalization.CultureInfo.InvariantCulture)),
                new XAttribute("y",original.PreviousGuard.y.ToString("R",System.Globalization.CultureInfo.InvariantCulture)),
                new XAttribute("z",original.PreviousGuard.z.ToString("R",System.Globalization.CultureInfo.InvariantCulture)));
            foreach(var source in original.Sources)image.Add(new XElement("source",new XAttribute("belt",source.Belt),new XAttribute("slot",source.Slot),
                new XAttribute("beforeCount",source.Before.count),new XAttribute("afterCount",source.After.count),new XElement("beforeItem",NativeHireItem(source.Before)),new XElement("afterItem",NativeHireItem(source.After))));
            original.Original=image.ToString(SaveOptions.DisableFormatting);
            Directory.CreateDirectory(original.Directory);SaveRemoteHireRequest(original);HireSettlements.Add(original.State.StableId,original);WriteHireRecord(original,"prepared");AdvanceHireControl(original,"AwaitingBaseline");
            return FinishHireSettlement(original,out reason);
        }
        catch(Exception ex)
        {
            Log.Warning("[REBIRTH Dog] hire retained without a second charge: "+ex.GetType().Name+": "+ex.Message);
            if(original!=null&&!original.EnteredDebit)
            {
                if(!HireSettlements.ContainsKey(original.State.StableId)){original.Terminal=true;ReleaseHireOwner(original);return false;}
                try{WriteHireRecord(original,"never-debited");original.Terminal=true;HireSettlements.Remove(original.State.StableId);ReleaseHireOwner(original);}catch{}
            }
            return false;
        }
    }
    private static bool DebitHireSettlement(HireSettlement original)
    {
        var player=original.Player;
            ObserveHireScenario("before-debit-permit");AdvanceHireControl(original,"UncertainDebit");WriteHireRecord(original,"debit-intent");
            if(IsRemoteHire(original)){original.EnteredDebit=true;SendRemoteHire(original,0);return false;}
            foreach(var source in original.Sources)
            {
                if(!HireCurrent(original)||!ReferenceEquals(source.Grid[source.Slot],source.OriginalReference)||!RebirthStationGridIngredients.IsSameStackSnapshot(source.Grid[source.Slot],source.Before))return RetireNeverEnteredHire(original);
                ObserveHireScenario("before-native-debit");
                original.EnteredDebit=true;
                if(!ApplyLocalHireSlot(original,source,source.After.Clone()))return false;
                source.AppliedReference=source.Grid[source.Slot];
                if(!RebirthStationGridIngredients.IsSameStackSnapshot(source.AppliedReference,source.After))return false;
            }
            original.Debited=true;WriteHireRecord(original,"debited");AdvanceHireControl(original,"Debited");
        return true;
    }
    private static bool FinishHireSettlement(HireSettlement original,out string reason)
    {
        reason=HireRetained;if(original.Busy||!HireCurrent(original)||IsRemoteHire(original)&&!original.RemoteClientReady&&!ConsumeRemoteHireReady(original))return false;original.Busy=true;var priorMutation=CurrentHireMutation;CurrentHireMutation=original;
        try
        {
            if(!original.Debited){if(original.EnteredDebit)return false;foreach(var source in original.Sources)if(!ReferenceEquals(source.Grid[source.Slot],source.OriginalReference)||!RebirthStationGridIngredients.IsSameStackSnapshot(source.Grid[source.Slot],source.Before))return RetireNeverEnteredHire(original);if(!ReadyHireDebit(original)||!DebitHireSettlement(original))return false;}
            if(!original.OwnershipAttempted)
            {
                if(!EnsureHireOwnerCheckpoint(original,"after"))return false;
                AdvanceHireControl(original,"ReadyOwnership");ObserveHireScenario("before-ownership-permit");AdvanceHireControl(original,"UncertainOwnership");
                WriteHireRecord(original,"ownership-intent");original.OwnershipAttempted=true;
                ObserveHireScenario("before-owner-set");
                var own=ApplyHireOwner(original,RebirthNpcOwnershipKind.Player,original.Owner);
                if(!own.Succeeded)return ReverseHireSettlement(original,out reason);
                ObserveHireScenario("before-order-set");
                var order=ApplyHireOrder(original,RebirthNpcOrderState.Follow,Vector3.zero,false);
                if(!order.Succeeded)return ReverseHireSettlement(original,out reason);
                original.Dog.SetSpawnerSource(EnumSpawnerSource.StaticSpawner);
                SynchronizeLegacyOwnerProjection(original.Dog);
                try{if(original.Dog.IsSleeping)original.Dog.ConditionalTriggerSleeperWakeUp();}catch{}
                float mode=original.Player.Buffs!=null?original.Player.Buffs.GetCustomVar("varNPCModMode"):0f;
                float halt=original.Player.Buffs!=null?original.Player.Buffs.GetCustomVar("varNPCModStopAttacking"):0f;
                RebirthDogStateService.SetCombatMode(original.State.StableId,mode==0f?RebirthCompanionBehaviorMode.FullControl:RebirthCompanionBehaviorMode.Hunting);
                RebirthDogStateService.SetAttackStopped(original.State.StableId,halt==1f);SynchronizeDisplayName(original.Dog);
                WriteHireRecord(original,"ownership-applied");
            }
            if(original.ReversalSaved){if(!ArchiveSavedHireReversal(original))return false;return RefundHireSettlement(original,out reason);}
            if(original.State.OwnershipKind!=RebirthNpcOwnershipKind.Player||original.State.OwnerId!=original.Owner||original.State.Order!=RebirthNpcOrderState.Follow)
                return ReverseHireSettlement(original,out reason);
            if(!original.OwnershipSaved)
            {
                ObserveHireScenario("before-ownership-save");
                if(!RebirthNpcPersistenceCoordinator.SaveAll(out var error)){Log.Warning("[REBIRTH Dog] paid hire publication retained: "+error);return false;}
                if(!HireCurrent(original))return false;
                original.OwnershipSaved=true;
            }
            if(!original.OwnershipArchived)
            {
                if(!ArchiveHireOwnership(original,"ownership"))return false;
                WriteHireRecord(original,"ownership-saved");original.OwnershipArchived=true;AdvanceHireControl(original,"OwnedSaved");
            }
            if(IsRemoteHire(original)&&!original.RemoteSettledSent){original.RemoteSettledSent=true;SendRemoteHire(original,5);}
            if(!EnsureHireOwnerCheckpoint(original,"settled"))return false;
            ObserveHireScenario("before-terminal");WriteHireRecord(original,"committed");if(IsRemoteHire(original))SendRemoteHire(original,2);original.Terminal=true;HireSettlements.Remove(original.State.StableId);ReleaseHireOwner(original);
            // Notification is outside the financial transaction. Its failure cannot refund a saved owner.
            try{ObserveHireScenario("notify");RebirthDogCapacitySyncService.NotifyOwner(original.Player);}catch(Exception ex){Log.Warning("[REBIRTH Dog] committed hire notification deferred: "+ex.Message);}
            reason=Localization.Get("xuiRebirthDogHireSuccess");return true;
        }
        catch(Exception ex){Log.Warning("[REBIRTH Dog] original hire settlement retained: "+ex.Message);return false;}
        finally{CurrentHireMutation=priorMutation;original.Busy=false;}
    }
    private static bool ReverseHireSettlement(HireSettlement original,out string reason)
    {
        reason=HireRetained;if(original.OwnershipSaved||!HireCurrent(original))return false;
        AdvanceHireControl(original,"UncertainReversal");WriteHireRecord(original,"reversal-intent");ObserveHireScenario("before-owner-reversal");
        var order=ApplyHireOrder(original,original.PreviousOrder,original.PreviousGuard,original.PreviousHasGuard);
        var owner=ApplyHireOwner(original,RebirthNpcOwnershipKind.None,string.Empty);
        if(!order.Succeeded||!owner.Succeeded)return false;
        ReleaseLegacyOwnerProjection(original.Dog);
        ObserveHireScenario("before-reversal-save");
        if(!RebirthNpcPersistenceCoordinator.SaveAll(out var error))return false;
        if(!HireCurrent(original)||original.State.OwnershipKind!=RebirthNpcOwnershipKind.None||original.State.Order!=original.PreviousOrder)return false;
        original.ReversalSaved=true;if(!ArchiveSavedHireReversal(original))return false;
        return RefundHireSettlement(original,out reason);
    }
    private static bool ArchiveSavedHireReversal(HireSettlement original)
    {
        if(original.ReversalArchived)return true;
        if(!original.ReversalSaved||!ArchiveHireOwnership(original,"reversal"))return false;
        WriteHireRecord(original,"reversal-saved");original.ReversalArchived=true;AdvanceHireControl(original,"ReadyRefund");return true;
    }
    private static bool RefundHireSettlement(HireSettlement original,out string reason)
    {
        reason=HireRetained;if(!original.ReversalSaved||original.OwnershipSaved||!HireCurrent(original))return false;
        if(original.Refunded)return CompleteHireRefund(original,out reason);
        if(original.RefundEntered)return false;
        if(IsRemoteHire(original))
        {
            ObserveHireScenario("before-refund-permit");AdvanceHireControl(original,"UncertainRefund");WriteHireRecord(original,"refund-intent");original.RefundEntered=true;SendRemoteHire(original,1);return false;
        }
        foreach(var source in original.Sources)
            if(!ReferenceEquals(source.Grid[source.Slot],source.AppliedReference)||!RebirthStationGridIngredients.IsSameStackSnapshot(source.Grid[source.Slot],source.After))return false;
        ObserveHireScenario("before-refund-permit");AdvanceHireControl(original,"UncertainRefund");WriteHireRecord(original,"refund-intent");original.RefundEntered=true;
        foreach(var source in original.Sources)
        {
            if(!ApplyLocalHireSlot(original,source,source.Before.Clone()))return false;
            if(!RebirthStationGridIngredients.IsSameStackSnapshot(source.Grid[source.Slot],source.Before))return false;
        }
        original.Refunded=true;WriteHireRecord(original,"refund-returned");AdvanceHireControl(original,"RefundReturned");return CompleteHireRefund(original,out reason);
    }
    private static bool CompleteHireRefund(HireSettlement original,out string reason)
    {
        reason=HireRetained;if(!EnsureHireOwnerCheckpoint(original,"refund"))return false;
        ObserveHireScenario("before-terminal");WriteHireRecord(original,"refunded");if(IsRemoteHire(original))SendRemoteHire(original,2);original.Terminal=true;HireSettlements.Remove(original.State.StableId);ReleaseHireOwner(original);
        reason="Dog hire was reversed and the exact original meat was returned.";return false;
    }
}
