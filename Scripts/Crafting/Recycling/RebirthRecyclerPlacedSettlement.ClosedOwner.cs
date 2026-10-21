using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

internal static partial class RebirthRecyclerPlacedSettlement
{
    // Explicit recipient-only arm. It never installs a client TileEntity or
    // invents a window. The actual authority Runtime owns the station leg.
    internal sealed class ClosedOwnerGrant
    {
        internal readonly Guid Operation,Ticket,Group;internal readonly World World;internal readonly EntityPlayerLocal Actor;internal readonly INetConnection Connection;
        internal XElement NativeOriginal{get;private set;}internal readonly string NativeDigest,Owner,Account,Creation,WorldKey;internal readonly int Deaths;
        internal RecyclerClient Client;private ClosedStationStage stage;
        private ClosedOwnerGrant(Guid operation,Guid ticket,Guid group,World world,EntityPlayerLocal actor,INetConnection connection,XElement native,RebirthStablePlayerIdentity identity)
        {Operation=operation;Ticket=ticket;Group=group;World=world;Actor=actor;Connection=connection;NativeOriginal=native;
            NativeDigest=RebirthStationPublicationRecord.Digest(Encoding.UTF8.GetBytes(native.ToString(SaveOptions.DisableFormatting)));Owner=identity.StorageKey;Account=identity.CanonicalId;
            Creation=RebirthCamperCharacterAccess.CurrentCreation(actor);Deaths=actor.Died;WorldKey=RebirthWorkstationCompletionOutbox.CurrentGuid(world);}
        internal bool CurrentOwner(Original original)=>ReferenceEquals(original.ClosedOwner,this)&&ReferenceEquals(original.Actor,Actor)&&OriginCurrent()&&
            (Client==null?ReferenceEquals(receivingClosedGrant,this):ReferenceEquals(Client.Source,original)&&recyclerClients.Contains(Client));
        private bool OriginCurrent()=>ReferenceEquals(GameManager.Instance?.World,World)&&World.IsRemote()&&ReferenceEquals(World.GetPrimaryPlayer(),Actor)&&
            ReferenceEquals(World.GetEntity(Actor.entityId),Actor)&&!Actor.IsDead()&&Actor.Died==Deaths&&RebirthCamperCharacterAccess.CurrentCreation(Actor)==Creation&&
            RebirthStablePlayerIdentity.TryFromLocalPlatform(out var identity)&&identity.StorageKey==Owner&&identity.CanonicalId==Account&&
            RebirthWorkstationCompletionOutbox.CurrentGuid(World)==WorldKey&&!Connection.IsDisconnected()&&
            ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance?.connectionToServer?[0],Connection);
        internal bool IsClient(RecyclerClient client)=>ReferenceEquals(Client,client)&&ReferenceEquals(client.Source?.ClosedOwner,this)&&OriginCurrent();
        internal void Forget(Original original)
        {if(Client!=null&&!ReferenceEquals(Client.Source,original))return;Client=null;NativeOriginal=null;stage=null;}
        internal bool HasStationStage(Original original,bool after)=>CurrentOwner(original)&&stage?.Current(this,after)==true;
        // Called only after the sole authenticated actual reply has passed its
        // full original/nonce/owner/life checks and entered receiving scope.
        internal void ObserveActualStage(RecyclerClient client)
        {
            if(!ReferenceEquals(receivingRecyclerReply,client)||!IsClient(client)||client.LastReply?.Name!="recyclerRemoteStatus"||client.Stage<1)return;
            stage=new ClosedStationStage(this,client.Nonce,client.Stage,new XElement(client.LastReply));
        }
        private sealed class ClosedStationStage
        {
            private readonly ClosedOwnerGrant source;private readonly Guid nonce;private readonly int phase;private readonly XElement receipt;
            internal ClosedStationStage(ClosedOwnerGrant source,Guid nonce,int phase,XElement receipt){this.source=source;this.nonce=nonce;this.phase=phase;this.receipt=receipt;}
            internal bool Current(ClosedOwnerGrant expected,bool after)=>ReferenceEquals(source,expected)&&source.IsClient(source.Client)&&nonce!=Guid.Empty&&nonce==source.Client.Nonce&&
                (after?phase>=3:phase>=1&&phase<=2)&&(string)receipt.Attribute("operation")==source.Operation.ToString("N")&&
                (string)receipt.Attribute("original")==RebirthStationPublicationRecord.Digest(source.Client.Source.Image);
        }
        internal static ClosedOwnerGrant FromActualGrant(World world,INetConnection connection,XElement envelope)
        {
            if(!ReferenceEquals(receivingClosedGrantEnvelope,envelope)||world?.IsRemote()!=true||!RebirthStablePlayerIdentity.TryFromLocalPlatform(out var identity)||
                world.GetPrimaryPlayer() is not EntityPlayerLocal actor||actor.IsDead()||!Guid.TryParseExact((string)envelope.Attribute("operation"),"N",out var operation)||
                !Guid.TryParseExact((string)envelope.Attribute("ticket"),"N",out var ticket)||!Guid.TryParseExact((string)envelope.Attribute("group"),"N",out var group)||
                operation==Guid.Empty||ticket==Guid.Empty||group==Guid.Empty)return null;
            var native=envelope.Element("recyclerClosedAuthorityOriginal");
            if(envelope.Name!="recyclerClosedAuthorityGrant"||(int?)envelope.Attribute("version")!=1||envelope.Elements().Count()!=1||native==null||
                (int?)native.Attribute("version")!=1||(string)native.Attribute("operation")!=operation.ToString("N")||(string)native.Attribute("group")!=group.ToString("N")||
                (string)native.Attribute("owner")!=identity.StorageKey||(string)native.Attribute("account")!=identity.CanonicalId||
                (string)native.Attribute("creation")!=RebirthCamperCharacterAccess.CurrentCreation(actor)||(int?)native.Attribute("deaths")!=actor.Died||
                (string)native.Attribute("world")!=RebirthWorkstationCompletionOutbox.CurrentGuid(world))return null;
            return new ClosedOwnerGrant(operation,ticket,group,world,actor,connection,new XElement(native),identity);
        }
    }
    [ThreadStatic]private static XElement receivingClosedGrantEnvelope;
    [ThreadStatic]private static ClosedOwnerGrant receivingClosedGrant;
    internal sealed partial class Original
    {
        internal ClosedOwnerGrant ClosedOwner;
        internal bool MatchesStationStage(bool after)=>ClosedOwner!=null?ClosedOwner.HasStationStage(this,after):
            Station!=null&&RebirthProductiveEarnedNativeBody.MatchesCurrent(Station,after?AfterNative:BeforeNative,after?AfterOffset:BeforeOffset);
        internal static Original FromActualClosedOwner(ClosedOwnerGrant grant)
        {
            var actor=grant?.Actor;
            if(grant==null||!ReferenceEquals(receivingClosedGrant,grant)||HoldsOwner(actor)||RecyclerOwnerConflict(actor)||actor.PlayerUI?.xui?.DragAndDropWindow?.IsEmpty()!=true||
                RebirthCookingOutputDelivery.IsHeld(actor)||RebirthDogLifecycleService.IsHireInventoryHeld(actor)||RebirthBackpackLibraryReservation.IsHeld(actor)||
                RebirthGearOwnerReservation.IsHeld(actor)||RemoteResourceClientTransactionCoordinator.HasPendingInventoryOperation(actor)||
                !RebirthStablePlayerIdentity.TryFromLocalPlatform(out var identity)||!RebirthProductiveEarnedObservationBudget.TryRetainDetached(64L*1024*1024,out var memory))return null;
            Original issued=null;
            try
            {
                using var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder();if(decoder==null)return null;
                var node=grant.NativeOriginal;byte[] before=ColdBytes(node.Element("beforeNative"),RebirthProductiveEarnedNativeBody.MaximumBodyBytes),after=ColdBytes(node.Element("afterNative"),RebirthProductiveEarnedNativeBody.MaximumBodyBytes);
                int beforeOffset=(int)node.Attribute("beforeOffset"),afterOffset=(int)node.Attribute("afterOffset");
                if(!RebirthProductiveEarnedNativeBody.TryRead(before,beforeOffset,out _,out _)||!RebirthProductiveEarnedNativeBody.TryRead(after,afterOffset,out var native,out _))return null;
                var returns=RebirthCamperInventorySnapshot.Restore(ColdBytes(node.Element("returns"),4*1024*1024));if(returns==null||returns.Length>64)return null;
                var bag=ItemStack.Clone(actor.bag.ItemGrid.items);var belt=ItemStack.Clone(actor.inventory.ItemGrid.items);
                var bagLocks=RebirthCamperStationNativeActorPayment.Locks(actor.bag.LockedSlots);var beltLocks=RebirthCamperStationNativeActorPayment.Locks(actor.inventory.ItemGrid.SlotLocks);
                int beltCount=RebirthToolbeltCapacity.GetOwnedSlotCount(actor,belt.Length);
                if(!RebirthRecyclerOwnerDelivery.Plan(bag,belt,bagLocks,beltLocks,beltCount,returns,out var afterBag,out var afterBelt))return null;
                var original=new XElement("placedRecyclerOriginal",new XAttribute("version",1),new XAttribute("operation",grant.Operation.ToString("N")),new XAttribute("purpose","complete"),
                    new XAttribute("world",grant.WorldKey),new XAttribute("owner",grant.Owner),new XAttribute("account",grant.Account),new XAttribute("creation",grant.Creation),new XAttribute("deaths",grant.Deaths),
                    new XAttribute("bagLocks",new string(bagLocks.Select(value=>value?'1':'0').ToArray())),new XAttribute("beltLocks",new string(beltLocks.Select(value=>value?'1':'0').ToArray())),new XAttribute("beltCount",beltCount),
                    new XAttribute("x",(int)node.Attribute("x")),new XAttribute("y",(int)node.Attribute("y")),new XAttribute("z",(int)node.Attribute("z")),new XAttribute("block",(string)node.Attribute("block")),
                    new XAttribute("beforeOffset",beforeOffset),new XAttribute("afterOffset",afterOffset),new XAttribute("cycle",1),new XAttribute("workSeconds",(float)node.Attribute("workSeconds")),
                    Bytes("beforeNative",before),Bytes("afterNative",after),Grid("beforeBag",bag),Grid("beforeBelt",belt),Grid("afterBag",afterBag),Grid("afterBelt",afterBelt),Grid("returns",returns),
                    new XElement("closedAuthority",new XAttribute("ticket",grant.Ticket.ToString("N")),new XAttribute("group",grant.Group.ToString("N")),new XAttribute("nativeOriginal",grant.NativeDigest)));
                byte[] image=Encoding.UTF8.GetBytes(original.ToString(SaveOptions.DisableFormatting));
                if(image.Length>8*1024*1024||!RebirthProductiveEarnedObservationBudget.TryResizeDetached(memory,checked(16L*(image.Length+before.Length+after.Length)+65536)))return null;
                var source=issued=new Original(grant.Operation,"complete",actor,null,identity,bag,belt,afterBag,afterBelt,returns,before,after,beforeOffset,afterOffset,
                    native.Input,native.Queue,native.Output,native.Tools,image,memory){ClosedOwner=grant,Cycle=true};memory=null;
                if(!source.Current()||RecyclerOwnerConflict(actor)||!source.MatchesOwnerCheckpoint(actor.bag.ItemGrid.items,actor.inventory.ItemGrid.items,0))return null;
                issued=null;return source;
            }
            finally{issued?.Dispose();memory?.Dispose();}
        }
    }
}
