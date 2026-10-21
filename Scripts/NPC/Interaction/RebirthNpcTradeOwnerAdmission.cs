using System;
using System.IO;
using System.Linq;
using Platform;

public static partial class RebirthNpcTradeService
{
    // A short-lived, actual authenticated preparation observation. It grants
    // neither NPC mutation nor owner After. The host must durably retain and
    // reserve this original before sending any native exchange permission.
    internal sealed partial class OwnerAdmission
    {
        internal readonly RebirthNpcTradeOriginal Original;
        internal readonly RebirthNpcTradeNpcPlan NpcPlan;
        internal readonly string SavedNpcBefore;
        private readonly World world;
        private readonly EntityPlayer actor;
        private readonly ClientInfo peer;
        private readonly INetConnection transport;
        private readonly ConnectionManager manager;
        private readonly PlayerDataFile accepted;
        private readonly string session,actorKey,directory;
        private readonly uint connection,epoch;
        private readonly int nativeId;
        private readonly bool reattached;
        private readonly bool local;
        private readonly QuoteRecord quote;
        private OwnerAdmission(RebirthNpcTradeOriginal original,RebirthNpcTradeNpcPlan plan,string saved,World world,EntityPlayer actor,
            ClientInfo peer,INetConnection transport,ConnectionManager manager,PlayerDataFile accepted,string session,string actorKey,uint connection,uint epoch,QuoteRecord quote,bool reattached=false,bool local=false)
        {
            Original=original;NpcPlan=plan;SavedNpcBefore=saved;this.world=world;this.actor=actor;this.peer=peer;this.transport=transport;
            this.manager=manager;this.accepted=accepted;this.session=session;this.actorKey=actorKey;this.connection=connection;this.epoch=epoch;this.quote=quote;
            nativeId=actor.entityId;this.reattached=reattached;this.local=local;
            string selected=GameIO.GetSaveGameDir();
            if(string.IsNullOrWhiteSpace(selected)||!Path.IsPathRooted(selected))throw new InvalidDataException("Native trade save directory unavailable");
            directory=Path.GetFullPath(selected);
        }
        internal static bool TryCapture(NetPackage packet,PlatformUserIdentifierAbs user,string session,uint connection,uint epoch,
            RebirthNpcTradeOriginal original,out OwnerAdmission admission)
        {
            admission=null;
            try
            {
                var world=GameManager.Instance?.World;var peer=packet?.Sender;var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;
                if(!ThreadManager.IsMainThread()||world?.IsRemote()!=false||manager?.IsServer!=true||peer==null||user==null||original==null||
                    !packet.ValidEntityIdForSender(original.NativeId)||!packet.ValidUserIdForSender(user)||
                    !ReferenceEquals(manager.Clients.ForEntityId(original.NativeId),peer)||peer.entityId!=original.NativeId||peer.disconnecting||!peer.loginDone||!peer.bAttachedToEntity||
                    peer.netConnection==null||peer.netConnection.Length<1||peer.netConnection[0]==null||peer.netConnection[0].IsDisconnected()||
                    !RebirthNpcNetworkEpoch.ValidateConnectionEpoch(original.NativeId,connection)||RebirthNpcNetworkEpoch.GetServerEpoch()!=epoch||
                    world.GetEntity(original.NativeId) is not EntityPlayer actor||actor is EntityPlayerLocal||actor.IsDead()||!actor.IsSpawned()||actor.Died!=original.Deaths||
                    !ReferenceEquals(actor.world,world)||world.worldState?.Guid!=original.World||RebirthCamperCharacterAccess.CurrentCreation(actor)!=original.Creation||
                    !RebirthStablePlayerIdentity.TryFromClientInfo(peer,out var owner)||owner.CanonicalId!=original.Account)return false;
                string actorKey=user.ToString();
                if(RebirthNpcInteractionSessionService.Authorize(session,actorKey,RebirthNpcInteractionAction.Trade,out var context)!=RebirthNpcInteractionResult.Allowed||
                    context.NpcId!=original.CopyQuote().NpcId||!TradeScope()||!RebirthNpcTradeReach.CanBegin(actor,context.NpcId))return false;
                QuoteRecord retained;
                lock(Sync)
                {
                    if(!Quotes.TryGetValue(original.Operation,out retained)||retained.Consumed||retained.Resolution!=0||
                        !original.MatchesQuote(retained.Quote)||retained.Quote.ActorId!=actorKey||retained.Quote.NpcId!=context.NpcId||
                        retained.Quote.ExpiresClockTicks<=DateTime.UtcNow.Ticks||retained.Quote.RecoveryGeneration!=RebirthNpcRecoveryGeneration.RequireCurrent())return false;
                }
                var accepted=peer.latestPlayerData;
                if(!Before(accepted,original,actor)||!RebirthGearNativePlayerFile.TryRead(owner,out var saved)||!Before(saved,original,actor)||
                    !RebirthNpcTradeNpcPlan.TryCreate(original,RebirthNpcInventoryTransactionService.GetSnapshot(context.NpcId),out var plan))return false;
                string npcBefore=RebirthNpcInventoryPersistenceStore.CaptureTradeSaved(original,plan,false);if(npcBefore==null)return false;
                var next=new OwnerAdmission(original,plan,npcBefore,world,actor,peer,peer.netConnection[0],manager,accepted,session,actorKey,connection,epoch,retained);
                if(!next.Current())return false;admission=next;return true;
            }
            catch{return false;}
        }
        internal static bool TryCaptureFromSaved(NetPackage packet,PlatformUserIdentifierAbs user,string session,uint connection,uint epoch,
            int playerId,Guid operation,string digest,out OwnerAdmission admission)
        {
            admission=null;
            return TryDescribeSavedBefore(packet,user,connection,epoch,playerId,operation,digest,out var original)&&
                TryCapture(packet,user,session,connection,epoch,original,out admission);
        }
        // Description and independently saved Before only. Session/quote
        // validity and NPC stock admission are deliberately separate: an
        // expired retained quotation can prove an unused refusal, but can
        // never issue a grant through this method.
        internal static bool TryDescribeSavedBefore(NetPackage packet,PlatformUserIdentifierAbs user,uint connection,uint epoch,
            int playerId,Guid operation,string digest,out RebirthNpcTradeOriginal original)
        {
            original=null;
            try
            {
                var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;var peer=packet?.Sender;
                if(packet==null||user==null||!packet.ValidEntityIdForSender(playerId)||!packet.ValidUserIdForSender(user)||
                    !TradeScope()||GameManager.Instance.World.GetEntity(playerId) is not EntityPlayer actor||actor is EntityPlayerLocal||
                    actor.IsDead()||!actor.IsSpawned()||!ReferenceEquals(actor.world,GameManager.Instance.World)||
                    manager?.IsServer!=true||peer==null||!ReferenceEquals(manager.Clients.ForEntityId(playerId),peer)||peer.entityId!=playerId||
                    peer.disconnecting||!peer.loginDone||!peer.bAttachedToEntity||peer.netConnection==null||peer.netConnection.Length<1||
                    peer.netConnection[0]==null||peer.netConnection[0].IsDisconnected()||
                    !RebirthNpcNetworkEpoch.ValidateConnectionEpoch(playerId,connection)||RebirthNpcNetworkEpoch.GetServerEpoch()!=epoch||
                    !RebirthStablePlayerIdentity.TryFromClientInfo(peer,out var owner))return false;
                RebirthNpcTradeQuote quote;
                lock(Sync)
                {
                    if(!Quotes.TryGetValue(operation,out var record)||record.Consumed&&record.OwnerUnused==null||record.Quote.ActorId!=user.ToString())return false;
                    quote=record.Quote;
                }
                var data=packet.Sender.latestPlayerData;
                if(data==null||data.id!=playerId||data.bDead||data.deaths!=actor.Died)return false;
                var bag=RebirthGearPlayerDataInventory.ReadSlots(data,true);var belt=RebirthGearPlayerDataInventory.ReadSlots(data,false);
                if(bag==null||bag.Length>169||belt==null||belt.Length<1||belt.Length>20)return false;
                var bagLocks=NativeLocks(data.bagData.ToArray(),true,bag.Length);
                var beltLocks=NativeLocks(data.inventoryData.ToArray(),false,belt.Length);
                int owned=RebirthToolbeltCapacity.GetOwnedSlotCount(actor,belt.Length);
                if(bagLocks==null||beltLocks==null||!RebirthNpcTradeOwnerPlan.TryCreate(quote,bag,belt,owned,
                    (backpack,slot)=>backpack?bagLocks[slot]:beltLocks[slot],out var plan))return false;
                // Derive only from the native accepted Before, never the live
                // server replica. The owner must independently retain exactly
                // this same immutable description before requesting admission.
                var described=RebirthNpcTradeOriginal.Freeze(quote,plan,owner.CanonicalId,RebirthCamperCharacterAccess.CurrentCreation(actor),
                    actor.world.worldState.Guid,actor.Died,playerId);
                if(described.Digest!=digest||!Before(data,described,actor)||!RebirthGearNativePlayerFile.TryRead(owner,out var saved)||
                    !Before(saved,described,actor)||!ReferenceEquals(peer.latestPlayerData,data))return false;
                original=described;return true;
            }
            catch{return false;}
        }
        private static bool Before(PlayerDataFile data,RebirthNpcTradeOriginal original,EntityPlayer actor)=>data!=null&&!data.bDead&&data.id==actor.entityId&&data.deaths==original.Deaths&&
            RebirthRemoteResourcePlacedStationSavedReceipt.TryRead(data,RebirthNpcTradeOwnerCheckpoint.Marker(original.Operation),out float phase,out bool present)&&present&&phase==1f&&
            ReadStep(data,original,out int step)&&step==0&&
            original.MatchesSlots(RebirthGearPlayerDataInventory.ReadSlots(data,true),RebirthGearPlayerDataInventory.ReadSlots(data,false),false)&&SavedPolicy(data,original,actor);
        private static bool ReadStep(PlayerDataFile data,RebirthNpcTradeOriginal original,out int step)
        {
            step=0;
            if(!RebirthRemoteResourcePlacedStationSavedReceipt.TryRead(data,RebirthNpcTradeOwnerExchange.Step(original.Operation),out float value,out bool present))return false;
            if(!present)return true;
            if(float.IsNaN(value)||float.IsInfinity(value)||value<0||value>378||value!=Math.Floor(value))return false;
            step=(int)value;return true;
        }
        private static bool RetainedPrefix(PlayerDataFile data,RebirthNpcTradeOriginal original,EntityPlayer actor)=>
            data!=null&&!data.bDead&&data.id==actor.entityId&&data.deaths==original.Deaths&&
            RebirthRemoteResourcePlacedStationSavedReceipt.TryRead(data,RebirthNpcTradeOwnerCheckpoint.Marker(original.Operation),out float phase,out bool present)&&present&&phase==1f&&
            ReadStep(data,original,out int step)&&RebirthNpcTradePrefix.Matches(original,RebirthGearPlayerDataInventory.ReadSlots(data,true),RebirthGearPlayerDataInventory.ReadSlots(data,false),step)&&SavedPolicy(data,original,actor);
        private static bool SameRetainedStage(PlayerDataFile a,PlayerDataFile b,RebirthNpcTradeOriginal original)=>
            RebirthRemoteResourcePlacedStationSavedReceipt.TryRead(a,RebirthNpcTradeOwnerCheckpoint.Marker(original.Operation),out float pa,out bool ha)&&ha&&
            RebirthRemoteResourcePlacedStationSavedReceipt.TryRead(b,RebirthNpcTradeOwnerCheckpoint.Marker(original.Operation),out float pb,out bool hb)&&hb&&pa==pb&&
            ReadStep(a,original,out int sa)&&ReadStep(b,original,out int sb)&&sa==sb;
        internal static bool TryCaptureRetained(NetPackage packet,PlatformUserIdentifierAbs user,int player,uint connection,
            HostPrepared.RetainedSource source,out OwnerAdmission admission)
        {
            admission=null;
            try
            {
                if(source==null||!source.IsRetained())return false;
                var original=source.Original;var plan=source.NpcPlan;string savedNpcBefore=source.SavedNpcBefore;
                var world=GameManager.Instance?.World;var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;var peer=packet?.Sender;
                if(!TradeScope()||world?.IsRemote()!=false||manager?.IsServer!=true||peer==null||user==null||original==null||plan==null||
                    !packet.ValidEntityIdForSender(player)||!packet.ValidUserIdForSender(user)||!ReferenceEquals(manager.Clients.ForEntityId(player),peer)||
                    peer.entityId!=player||peer.disconnecting||!peer.loginDone||!peer.bAttachedToEntity||peer.netConnection==null||peer.netConnection.Length<1||
                    peer.netConnection[0]==null||peer.netConnection[0].IsDisconnected()||!RebirthNpcNetworkEpoch.ValidateConnectionEpoch(player,connection)||
                    world.GetEntity(player) is not EntityPlayer actor||actor is EntityPlayerLocal||!ReferenceEquals(actor.world,world)||actor.IsDead()||!actor.IsSpawned()||
                    actor.Died!=original.Deaths||original.World!=world.worldState?.Guid||RebirthCamperCharacterAccess.CurrentCreation(actor)!=original.Creation||
                    original.CopyQuote().ActorId!=user.ToString()||!RebirthStablePlayerIdentity.TryFromClientInfo(peer,out var owner)||owner.CanonicalId!=original.Account||
                    !HostQuote.TryReadEntered(original,out var issued))return false;
                var head=peer.latestPlayerData;
                bool Known(PlayerDataFile data)=>RetainedPrefix(data,original,actor)||After(data,original,actor,2f)||After(data,original,actor,3f);
                if(!Known(head)||!RebirthGearNativePlayerFile.TryRead(owner,out var saved)||!Known(saved)||!SameRetainedStage(head,saved,original))return false;
                QuoteRecord retained;
                lock(Sync)
                {
                    if(Quotes.TryGetValue(original.Operation,out retained))
                    {if(!retained.Consumed||retained.Resolution!=0||retained.OwnerUnused!=null||!original.MatchesQuote(retained.Quote))return false;}
                    else retained=new QuoteRecord{Quote=original.CopyQuote(),Issued=issued,Consumed=true};
                }
                var next=new OwnerAdmission(original,plan,savedNpcBefore,world,actor,peer,peer.netConnection[0],manager,head,null,user.ToString(),
                    connection,RebirthNpcNetworkEpoch.GetServerEpoch(),retained,true);
                if(!next.MetadataCurrent(packet,user)||!ReferenceEquals(head,peer.latestPlayerData))return false;
                admission=next;return true;
            }
            catch{return false;}
        }
        internal bool BindRetained(HostPrepared prepared)
        {
            if(!reattached||prepared==null||!ReferenceEquals(prepared.Original,Original))return false;
            lock(Sync)
            {
                if(!TradeScope()||Quotes.TryGetValue(Original.Operation,out var current)&&!ReferenceEquals(current,quote)||
                    !Quotes.ContainsKey(Original.Operation)&&Quotes.Count>=MaxQuotes||
                    Quotes.Values.Count(value=>value.OwnerPreparation!=null&&value.OwnerPreparation.ClosedRecord==null&&value.Quote.QuoteId!=Original.Operation)>=4||
                    Quotes.Values.Any(value=>value.Quote.QuoteId!=Original.Operation&&value.OwnerPreparation!=null&&value.OwnerPreparation.ClosedRecord==null&&value.Quote.NpcId==Original.CopyQuote().NpcId))return false;
                quote.OwnerPreparation=prepared;quote.Consumed=true;
                if(!Quotes.ContainsKey(Original.Operation)){Quotes.Add(Original.Operation,quote);QuoteOrder.Add(Original.Operation);}
                return true;
            }
        }
        internal bool CanIssueMissingGrant=>!reattached||Before(accepted,Original,actor);
        private static bool SavedPolicy(PlayerDataFile data,RebirthNpcTradeOriginal original,EntityPlayer actor)
        {
            try
            {
                var bag=original.CopyLocks(true);var belt=original.CopyLocks(false);
                if(bag==null||bag.Length>169||belt==null||belt.Length<1||belt.Length>20||
                    RebirthToolbeltCapacity.GetOwnedSlotCount(actor,belt.Length)!=original.OwnedBeltSlots)return false;
                return ReadPolicy(data.bagData.ToArray(),bag,true,original.OwnedBeltSlots)&&
                    ReadPolicy(data.inventoryData.ToArray(),belt,false,original.OwnedBeltSlots);
            }
            catch{return false;}
        }
        // Native file policy decoder only, shared with the owning client
        // checkpoint reader. This issues no host or owner capability.
        internal static bool MatchesNativePolicy(PlayerDataFile data,RebirthNpcTradeOriginal original,EntityPlayer actor)=>SavedPolicy(data,original,actor);
        private static bool ReadPolicy(byte[] bytes,bool[] expected,bool bag,int owned)
        {
            var locks=NativeLocks(bytes,bag,expected.Length);if(locks==null)return false;
            for(int i=0;i<expected.Length;i++)if(expected[i]!=(locks[i]||!bag&&i>=owned))return false;
            return true;
        }
        private static bool[] NativeLocks(byte[] bytes,bool bag,int width)
        {
            if(bytes==null||bytes.Length<1||bytes.Length>4*1024*1024)return null;
            using(var stream=new MemoryStream(bytes,false))using(var reader=MemoryPools.poolBinaryReader.AllocSync(true))
            {
                reader.SetBaseStream(stream);if(reader.ReadByte()!=(bag?2:1))return null;
                var grid=ItemStackGrid.Read(reader,StreamModeRead.Persistency);
                if(!bag)reader.ReadByte(); // Native selected toolbelt slot footer.
                if(stream.Position!=stream.Length||grid?.Length!=width)return null;
                var locks=RebirthCamperEnergyItems.Locks(grid.SlotLocks);
                // Native Inventory constructs ToolBelt with _hasLocks:false.
                // A null native lock map means every cell is unlocked, not an
                // absent ownership proof. Owned width is checked separately.
                return locks==null?new bool[width]:locks.Length==width?locks:null;
            }
        }
        internal bool Current()=>CurrentCore(null);
        internal bool CurrentReserved(HostPrepared prepared)=>prepared!=null&&CurrentCore(prepared);
        internal bool ObserveAcknowledgement(HostPrepared prepared,NetPackage packet,PlatformUserIdentifierAbs user)
        {
            try{return prepared!=null&&packet!=null&&ReferenceEquals(packet.Sender,peer)&&user!=null&&user.ToString()==actorKey&&
                packet.ValidEntityIdForSender(nativeId)&&packet.ValidUserIdForSender(user)&&CurrentCore(prepared,true,peer.latestPlayerData,3f);}
            catch{return false;}
        }
        internal bool MetadataCurrent(NetPackage packet,PlatformUserIdentifierAbs user)
        {
            try{return ThreadManager.IsMainThread()&&ReferenceEquals(world,GameManager.Instance?.World)&&world.IsRemote()==false&&
                Path.GetFullPath(GameIO.GetSaveGameDir())==directory&&packet!=null&&ReferenceEquals(packet.Sender,peer)&&user!=null&&user.ToString()==actorKey&&
                packet.ValidEntityIdForSender(nativeId)&&packet.ValidUserIdForSender(user)&&ReferenceEquals(manager.Clients.ForEntityId(nativeId),peer)&&
                !peer.disconnecting&&peer.loginDone&&peer.bAttachedToEntity&&RebirthStablePlayerIdentity.TryFromClientInfo(peer,out var owner)&&owner.CanonicalId==Original.Account&&
                ReferenceEquals(actor,world.GetEntity(nativeId))&&world.worldState.Guid==Original.World&&RebirthCamperCharacterAccess.CurrentCreation(actor)==Original.Creation&&
                RebirthNpcNetworkEpoch.ValidateConnectionEpoch(nativeId,connection)&&RebirthNpcNetworkEpoch.GetServerEpoch()==epoch&&
                peer.netConnection!=null&&peer.netConnection.Length>0&&ReferenceEquals(peer.netConnection[0],transport)&&!transport.IsDisconnected();}
            catch{return false;}
        }
        private bool CurrentCore(HostPrepared prepared,bool completed=false,PlayerDataFile afterHead=null,float expectedPhase=2f)
        {
            if(local)return CurrentLocal(prepared,completed,afterHead,expectedPhase);
            try
            {
                if(!ThreadManager.IsMainThread()||!ReferenceEquals(world,GameManager.Instance?.World)||!ReferenceEquals(actor.world,world)||!ReferenceEquals(actor,world.GetEntity(nativeId))||actor.entityId!=nativeId||
                    Path.GetFullPath(GameIO.GetSaveGameDir())!=directory||
                    actor.IsDead()||!actor.IsSpawned()||actor.Died!=Original.Deaths||world.worldState?.Guid!=Original.World||RebirthCamperCharacterAccess.CurrentCreation(actor)!=Original.Creation||
                    !ReferenceEquals(manager,SingletonMonoBehaviour<ConnectionManager>.Instance)||manager.IsServer!=true||!ReferenceEquals(manager.Clients.ForEntityId(nativeId),peer)||
                    peer.entityId!=nativeId||peer.disconnecting||!peer.loginDone||!peer.bAttachedToEntity||peer.netConnection==null||peer.netConnection.Length<1||
                    !ReferenceEquals(peer.netConnection[0],transport)||transport.IsDisconnected()||
                    (completed?afterHead==null||!reattached&&ReferenceEquals(afterHead,accepted)||!ReferenceEquals(peer.latestPlayerData,afterHead)||!After(afterHead,Original,actor,expectedPhase):
                        !ReferenceEquals(peer.latestPlayerData,accepted)||(reattached?!RetainedPrefix(accepted,Original,actor):!Before(accepted,Original,actor)))||
                    !RebirthStablePlayerIdentity.TryFromClientInfo(peer,out var owner)||owner.CanonicalId!=Original.Account||
                    !RebirthNpcNetworkEpoch.ValidateConnectionEpoch(nativeId,connection)||RebirthNpcNetworkEpoch.GetServerEpoch()!=epoch)return false;
                // The interactive session admits the original Before. Closing
                // its window cannot revoke already permitted owner work.
                if(prepared==null&&!completed&&!reattached&&(RebirthNpcInteractionSessionService.Authorize(session,actorKey,RebirthNpcInteractionAction.Trade,out var context)!=RebirthNpcInteractionResult.Allowed||
                    context.NpcId!=Original.CopyQuote().NpcId))return false;
                lock(Sync)
                    if(!Quotes.TryGetValue(Original.Operation,out var actual)||!ReferenceEquals(actual,quote)||
                        (prepared==null?actual.Consumed:!actual.Consumed||!ReferenceEquals(actual.OwnerPreparation,prepared))||actual.Resolution!=0||
                        prepared==null&&!completed&&!reattached&&actual.Quote.ExpiresClockTicks<=DateTime.UtcNow.Ticks||!Original.MatchesQuote(actual.Quote))return false;
                return RebirthGearNativePlayerFile.TryRead(owner,out var saved)&&(completed?After(saved,Original,actor,expectedPhase):
                    reattached?RetainedPrefix(saved,Original,actor)&&SameRetainedStage(accepted,saved,Original):Before(saved,Original,actor))&&
                    (completed||RebirthNpcInventoryPersistenceStore.CaptureTradeSaved(Original,NpcPlan,false)!=null);
            }
            catch{return false;}
        }
        private static bool After(PlayerDataFile data,RebirthNpcTradeOriginal original,EntityPlayer actor,float expectedPhase=2f)=>data!=null&&!data.bDead&&data.id==actor.entityId&&data.deaths==original.Deaths&&
            RebirthRemoteResourcePlacedStationSavedReceipt.TryRead(data,RebirthNpcTradeOwnerCheckpoint.Marker(original.Operation),out float phase,out bool present)&&present&&phase==expectedPhase&&
            ReadStep(data,original,out int step)&&step==RebirthNpcTradePrefix.Changes(original)*2&&
            original.MatchesSlots(RebirthGearPlayerDataInventory.ReadSlots(data,true),RebirthGearPlayerDataInventory.ReadSlots(data,false),true)&&SavedPolicy(data,original,actor);

        // Only an actual authenticated owner reply following retained grant
        // publication can produce this observation. Full native accepted AND
        // independently saved After are rechecked at use; no item setter here.
        internal sealed partial class SavedAfter
        {
            internal readonly HostPrepared.HostGrant Grant;
            private readonly OwnerAdmission admission;
            private readonly PlayerDataFile head;
            private SavedAfter(OwnerAdmission admission,HostPrepared.HostGrant grant,PlayerDataFile head)
            {this.admission=admission;Grant=grant;this.head=head;}
            internal static bool TryCapture(OwnerAdmission admission,HostPrepared.HostGrant grant,NetPackage packet,
                PlatformUserIdentifierAbs user,out SavedAfter observation)
            {
                observation=null;
                try
                {
                    if(admission==null||grant==null||!ReferenceEquals(grant.Prepared.Original,admission.Original)||
                        packet==null||!ReferenceEquals(packet.Sender,admission.peer)||user==null||user.ToString()!=admission.actorKey||
                        !packet.ValidEntityIdForSender(admission.nativeId)||!packet.ValidUserIdForSender(user)||!grant.IsRetained())return false;
                    var head=admission.peer.latestPlayerData;
                    var next=new SavedAfter(admission,grant,head);
                    if(!next.Current())return false;observation=next;return true;
                }
                catch{return false;}
            }
            internal bool Current()=>Grant.IsRetained()&&admission.CurrentCore(Grant.Prepared,true,head);
        }
    }
}
