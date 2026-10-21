using System;
using System.Collections.Generic;
using System.Linq;

// Executes the existing immutable plan/recovery decisions against actual native
// owner and world persistence. The authenticated request factory owns admission;
// loaded descriptions and the pure decision enum cannot instantiate this scope.
internal static partial class RebirthVehicleTransferCoordinator
{
    internal sealed class Transfer
    {
        internal readonly EntityPlayer Player;
        internal readonly RebirthVehicleTransferPlan Plan;
        private readonly World world;
        private readonly GameManager game;
        private readonly ConnectionManager manager;
        private readonly ClientInfo peer;
        private readonly object channel;
        private readonly int deaths;
        private readonly string directory;
        private readonly RebirthVehicleTransferPlanStore store;
        private RebirthVehicleAssemblySave reservationSave,assemblySave;
        private RebirthVehicleConversionChunkSave conversionSave;
        private bool running,finished;
        private bool ownerAcknowledged;
        private bool historical;
        internal bool Rejected {get;private set;}
        internal bool OwnerRejected,NeverEntered;
        internal bool Finished=>finished;
        private Transfer(EntityPlayer player,ClientInfo sender,RebirthVehicleTransferPlan plan)
        {
            Player=player;Plan=plan;world=player.world;game=GameManager.Instance;manager=SingletonMonoBehaviour<ConnectionManager>.Instance;
            peer=sender;channel=sender?.netConnection!=null&&sender.netConnection.Length>0?sender.netConnection[0]:null;
            deaths=player.Died;directory=GameIO.GetSaveGameDir();store=new RebirthVehicleTransferPlanStore(directory,GameIO.GetSaveGameDir,MetadataCurrent);
        }
        internal static bool TryCreate(EntityPlayer player,ClientInfo sender,RebirthVehicleTransferPlan plan,out Transfer transfer)
        {
            transfer=null;var next=new Transfer(player,sender,plan);
            next.historical=next.store.HasCompletion(plan);
            if(!next.Current()||!next.historical&&!TryReadNativeReservation(player,plan.TransactionId,plan.ActorId,plan.CreationId,plan.Fingerprint,plan.Custody,false,next.Current,out _,out _))return false;
            next.store.Prepare(plan);
            if(!next.Current()||next.store.Read(plan.TransactionId)?.ToXml().ToString()!=plan.ToXml().ToString())return false;
            next.historical=next.store.HasCompletion(plan);
            transfer=next;return true;
        }
        internal void RevokeEvidence()
        {reservationSave?.Dispose();assemblySave?.Dispose();conversionSave?.Dispose();reservationSave=assemblySave=null;conversionSave=null;}
        internal bool CanPublishResult()=>finished&&Current()&&store.HasCompletion(Plan)&&
            (Rejected||Plan.Training==null||store.HasTrainingCompletion(Plan));
        internal bool OwnerAcknowledged=>ownerAcknowledged;
        internal bool Acknowledge(ClientInfo sender,string hash)
        {
            if(!Current()||!ReferenceEquals(peer,sender)||hash!=RebirthVehicleTransferCustody.Hash(Plan.ToXml().ToString(System.Xml.Linq.SaveOptions.DisableFormatting))||
                !store.HasCompletion(Plan)||!HasSavedOwnerSeen(this))return false;
            ownerAcknowledged=true;return Current();
        }
        internal bool Current()=>MetadataCurrent()&&(Plan.NativeDeaths==Player.Died||historical&&store.HasCompletion(Plan));
        private bool MetadataCurrent()
        {
            try
            {
                if(!IsAdmitting||!ThreadManager.IsMainThread()||!ReferenceEquals(game,GameManager.Instance)||!ReferenceEquals(world,game.World)||!ReferenceEquals(Player.world,world)||
                    world.IsRemote()||!ReferenceEquals(world.GetEntity(Player.entityId),Player)||Player.IsDead()||!Player.IsSpawned()||Player.Died!=deaths||
                    directory!=GameIO.GetSaveGameDir()||!ReferenceEquals(manager,SingletonMonoBehaviour<ConnectionManager>.Instance)||!manager.IsServer||
                    !RebirthStablePlayerIdentity.TryResolveServerEntity(Player,out var identity)||identity.CanonicalId!=Plan.ActorId||Plan.NativeDeaths<0||Plan.NativeDeaths>Player.Died)return false;
                if(RebirthSurvivorMode.IsEnabledForCurrentWorld())
                {if(!RebirthConsumptionOwnerPublication.TryCaptureCreation(Player,out var creation)||!RebirthSurvivorRequestScope.Matches(creation,Plan.CreationId))return false;}
                else if(Plan.CreationId!=string.Empty||Plan.NativeDeaths<0)return false;
                if(Player is EntityPlayerLocal)return peer==null&&ReferenceEquals(world.GetPrimaryPlayer(),Player);
                return peer!=null&&peer.loginDone&&!peer.disconnecting&&ReferenceEquals(manager.Clients?.ForEntityId(Player.entityId),peer)&&
                    channel!=null&&peer.netConnection!=null&&peer.netConnection.Length>0&&ReferenceEquals(channel,peer.netConnection[0])&&!peer.netConnection[0].IsDisconnected();
            }
            catch{return false;}
        }
        private bool ReadReceipts(out Dictionary<Guid,RebirthVehicleOwnerTransferResult> receipts)
        {
            receipts=null;
            if(!Current()||!RebirthStablePlayerIdentity.TryResolveServerEntity(Player,out var identity)||!RebirthGearNativePlayerFile.TryRead(identity,out var native)||
                native.deaths!=deaths||native.buffData==null||native.buffData.Length<1||native.buffData.Length>1024*1024)return false;
            return RebirthVehicleReceiptReader.TryRead(native.buffData.ToArray(),Plan.Items.Select(item=>item.ReceiptId),out receipts)&&Current();
        }
        private bool JointSaved()
            =>Current()&&assemblySave!=null&&assemblySave.Saved()&&(!Plan.Target.ConvertsToEntity||conversionSave!=null&&conversionSave.Saved())&&ReadReceipts(out var receipts)&&
                Plan.Items.All(item=>receipts[item.ReceiptId]==RebirthVehicleOwnerTransferResult.Applied)&&Current();
        internal bool Advance()
        {
            if(running||!Current())return false;if(finished)return true;running=true;
            try
            {
                if(historical||store.HasCompletion(Plan)){historical=true;return AdvanceHistorical();}
                if(!EnsureOwnerReady(this))
                {
                    if(!OwnerRejected||!RebirthVehicleTransferResolver.TryObserve(world,Plan,Current,out var untouched,out _,out _)||
                        RebirthVehicleAssemblySerializer.ToBase64(untouched)!=Plan.BeforePayload)return false;
                    if(assemblySave==null)assemblySave=RebirthVehicleAssemblySave.Begin(world,Plan,Current,Plan.BeforePayload);
                    bool NoEffectSaved()=>Current()&&assemblySave!=null&&assemblySave.Saved()&&ReadyStage(Player,Plan)==RebirthVehicleOwnerTransferResult.Rejected&&
                        ReadReceipts(out var native)&&native.Values.All(value=>value==RebirthVehicleOwnerTransferResult.Pending);
                    if(!NoEffectSaved()||!store.TryComplete(Plan,NoEffectSaved,true))return false;
                    historical=true;
                    Rejected=NeverEntered=true;
                    if(!CompleteOwner(this))return false;finished=true;assemblySave.Dispose();assemblySave=null;return true;
                }
                if(!ReadReceipts(out var receipts))return false;
                if(!RebirthVehicleTransferResolver.TryObserve(world,Plan,Current,out var observed,out var position,out var id))
                {
                    bool PaidOriginal()=>Current()&&ReadReceipts(out var saved)&&Plan.Items.Any(item=>item.Debit)&&
                        Plan.Items.Where(item=>item.Debit).All(item=>saved[item.ReceiptId]==RebirthVehicleOwnerTransferResult.Applied);
                    if(Plan.Target.ConvertsToEntity&&PaidOriginal())RebirthVehicleAssemblyService.TryFinishPaidConversionRemoval(world,Plan,PaidOriginal);
                    return false;
                }
                var entity=id>=0?world.GetEntity(id) as EntityVehicle:null;
                if(Plan.Target.ConvertsToEntity&&entity==null&&RebirthVehicleAssemblySerializer.ToBase64(observed)==Plan.AfterPayload)
                {
                    if(!Plan.Items.Where(item=>item.Debit).All(item=>receipts[item.ReceiptId]==RebirthVehicleOwnerTransferResult.Applied))return false;
                    RebirthVehicleAssemblyService.TryConvertPaid(world,Player,Plan,Current);return false;
                }
                bool saved=assemblySave!=null&&assemblySave.Saved()&&(!Plan.Target.ConvertsToEntity||conversionSave!=null&&conversionSave.Saved());
                var step=RebirthVehicleTransferRecovery.Next(Plan,observed,receipts,saved,out var receipt,
                    entity?.vehicle?.GetFuelLevel(),entity?.vehicle?.GetMaxFuelLevel(),entity?.Health,entity?.GetMaxHealth());
                if(step==RebirthVehicleTransferStep.Reconcile&&receipts.Values.Any(value=>value==RebirthVehicleOwnerTransferResult.Indeterminate))
                {
                    var reserved=Plan.ReadBefore();RebirthVehicleTransferReservation.TryReserve(reserved,Plan.TransactionId,reserved.Revision);
                    if(RebirthVehicleAssemblySerializer.ToBase64(observed)==RebirthVehicleAssemblySerializer.ToBase64(reserved))
                    {
                        if(reservationSave==null)reservationSave=RebirthVehicleAssemblySave.Begin(world,Plan,Current,RebirthVehicleAssemblySerializer.ToBase64(reserved));
                        if(reservationSave==null||!reservationSave.Saved())return false;
                        var debits=Plan.Items.Where(item=>item.Debit).ToArray();
                        if(debits.Length>0&&debits.Any(item=>receipts[item.ReceiptId]==RebirthVehicleOwnerTransferResult.Indeterminate))return OfferOwner(this,debits);
                    }
                    if(RebirthVehicleAssemblySerializer.ToBase64(observed)==Plan.AfterPayload)
                    {
                        if(assemblySave==null)assemblySave=RebirthVehicleAssemblySave.Begin(world,Plan,Current);
                        if(assemblySave==null||!assemblySave.Saved())return false;
                        var credit=Plan.Items.FirstOrDefault(item=>!item.Debit&&receipts[item.ReceiptId]==RebirthVehicleOwnerTransferResult.Indeterminate);
                        if(credit!=null)return OfferOwner(this,new[]{credit});
                    }
                    return false;
                }
                if(step==RebirthVehicleTransferStep.CancelRejected)
                {
                    var before=Plan.ReadBefore();string payload=RebirthVehicleAssemblySerializer.ToBase64(before);
                    if(RebirthVehicleAssemblySerializer.ToBase64(observed)!=payload)
                    {if(!Publish(before,position,entity,false))return false;reservationSave?.Dispose();reservationSave=null;}
                    if(reservationSave==null)reservationSave=RebirthVehicleAssemblySave.Begin(world,Plan,Current,payload);
                    bool RejectedSaved()=>Current()&&reservationSave!=null&&reservationSave.Saved()&&ReadReceipts(out var actual)&&
                        Plan.Items.Where(item=>item.Debit).All(item=>actual[item.ReceiptId]==RebirthVehicleOwnerTransferResult.Rejected)&&
                        Plan.Items.Where(item=>!item.Debit).All(item=>actual[item.ReceiptId]==RebirthVehicleOwnerTransferResult.Pending);
                    if(!RejectedSaved()||!store.TryComplete(Plan,RejectedSaved,true))return false;
                    historical=true;
                    Rejected=true;
                    if(Player is EntityPlayerLocal rejectedLocal&&!RebirthVehicleOwnerCustody.Release(rejectedLocal,Plan,()=>store.HasCompletion(Plan)&&RejectedSaved()))return false;
                    if(!CompleteOwner(this))return false;finished=true;reservationSave.Dispose();reservationSave=null;return true;
                }
                if(step==RebirthVehicleTransferStep.ReserveAssembly)
                {
                    var reserved=Plan.ReadBefore();if(!RebirthVehicleTransferReservation.TryReserve(reserved,Plan.TransactionId,reserved.Revision)||!Publish(reserved,position,entity,false))return false;
                    reservationSave=RebirthVehicleAssemblySave.Begin(world,Plan,Current,RebirthVehicleAssemblySerializer.ToBase64(reserved));return false;
                }
                if(step==RebirthVehicleTransferStep.ApplyDebit||step==RebirthVehicleTransferStep.ApplyDebitBatch)
                {
                    if(reservationSave==null)reservationSave=RebirthVehicleAssemblySave.Begin(world,Plan,Current,RebirthVehicleAssemblySerializer.ToBase64(observed));
                    if(reservationSave==null||!reservationSave.Saved())return false;
                    var operations=Plan.Items.Where(item=>item.Debit).ToArray();
                    return OfferOwner(this,step==RebirthVehicleTransferStep.ApplyDebitBatch?operations:operations.Where(item=>item.ReceiptId==receipt).ToArray());
                }
                if(step==RebirthVehicleTransferStep.ApplyAssembly)
                {
                    if(!Plan.Items.Where(item=>item.Debit).All(item=>receipts[item.ReceiptId]==RebirthVehicleOwnerTransferResult.Applied)||!Current())return false;
                    if(!Publish(Plan.ReadAfter(),position,entity,true))return false;
                    if(Plan.Target.ConvertsToEntity){RebirthVehicleAssemblyService.TryConvertPaid(world,Player,Plan,Current);return false;}
                    reservationSave?.Dispose();reservationSave=null;assemblySave=RebirthVehicleAssemblySave.Begin(world,Plan,Current);return false;
                }
                if(step==RebirthVehicleTransferStep.WaitForAssemblySave)
                {
                    if(assemblySave==null)assemblySave=RebirthVehicleAssemblySave.Begin(world,Plan,Current);
                    if(Plan.Target.ConvertsToEntity&&conversionSave==null)
                    {var source=new Vector3i(Plan.Target.X,Plan.Target.Y,Plan.Target.Z);conversionSave=RebirthVehicleConversionChunkSave.Begin(world,source,world.GetChunkSync(source.x>>4,source.z>>4) as Chunk,Current);}
                    return false;
                }
                if(step==RebirthVehicleTransferStep.ApplyCredit)return OfferOwner(this,Plan.Items.Where(item=>item.ReceiptId==receipt).ToArray());
                if(step!=RebirthVehicleTransferStep.Complete||!JointSaved()||!store.TryComplete(Plan,JointSaved))return false;
                historical=true;
                if(Player is EntityPlayerLocal local&&!RebirthVehicleOwnerCustody.Release(local,Plan,()=>store.HasCompletion(Plan)&&JointSaved()))return false;
                if(!CompleteOwner(this))return false;
                finished=true;reservationSave?.Dispose();reservationSave=null;assemblySave?.Dispose();assemblySave=null;conversionSave?.Dispose();conversionSave=null;return true;
            }
            catch{return false;}
            finally{running=false;}
        }
        private bool AdvanceHistorical()
        {
            Rejected=store.HasRejectedCompletion(Plan);
            // The financial completion was published only after both actual
            // native saved legs. Subsequent legitimate inventory/world changes
            // cannot reopen that original or demand another item/world delta.
            bool Closed()
            {
                if(!Current()||!store.HasCompletion(Plan))return false;
                if(Plan.NativeDeaths<deaths)return true; // exact saved financial terminal only; no item/world reentry
                if(!ReadReceipts(out var receipts)||
                    !TryReadNativeReservation(Player,Plan.TransactionId,Plan.ActorId,Plan.CreationId,Plan.Fingerprint,Plan.Custody,false,Current,out _,out _))return false;
                if(!Rejected)return ReadyStage(Player,Plan)==RebirthVehicleOwnerTransferResult.Applied&&Plan.Items.All(item=>receipts[item.ReceiptId]==RebirthVehicleOwnerTransferResult.Applied);
                NeverEntered=ReadyStage(Player,Plan)==RebirthVehicleOwnerTransferResult.Rejected;
                return Plan.Items.All(item=>receipts[item.ReceiptId]==(NeverEntered||!item.Debit?RebirthVehicleOwnerTransferResult.Pending:RebirthVehicleOwnerTransferResult.Rejected));
            }
            if(!Closed())return false;
            if(Player is EntityPlayerLocal resumed&&Plan.NativeDeaths<deaths&&!RebirthVehicleClientCustody.CanRetireCompletedTransfer(resumed,Plan,Rejected))return false;
            if(Player is EntityPlayerLocal local&&!RebirthVehicleOwnerCustody.RetireHistorical(local,Plan,Closed))return false;
            if(!CompleteOwner(this))return false;finished=true;RevokeEvidence();return true;
        }
        private bool Publish(RebirthVehicleAssembly assembly,Vector3i position,EntityVehicle entity,bool result)
        {
            if(!Current())return false;
            if(entity!=null)
            {
                if(result&&Plan.Fuel!=null){if(entity.vehicle.GetFuelLevel()!=Plan.Fuel.AfterLevel)entity.vehicle.SetFuelLevel(Plan.Fuel.AfterLevel);if(!Current()||entity.vehicle.GetFuelLevel()!=Plan.Fuel.AfterLevel)return false;}
                if(result&&Plan.Health!=null){if(entity.Health!=Plan.Health.After)entity.Health=Plan.Health.After;if(!Current()||entity.Health!=Plan.Health.After)return false;}
                RebirthVehicleAssemblyIndex.RegisterEntity(entity,assembly);
                if(!Current())return false;entity.SendSyncData(4);
            }
            else
            {
                var tile=world.GetTileEntity(position) as TileEntityRepairableVehicleRebirth;if(tile==null)return false;
                tile.Assembly=assembly;tile.SetModified();if(!Current()||!ReferenceEquals(world.GetTileEntity(position),tile))return false;
                RebirthVehicleAssemblyIndex.RegisterBlock(position,tile);
            }
            return Current()&&RebirthVehicleTransferResolver.TryObserve(world,Plan,Current,out var actual,out _,out _)&&
                RebirthVehicleAssemblySerializer.ToBase64(actual)==RebirthVehicleAssemblySerializer.ToBase64(assembly);
        }
    }
    // Implemented by the actual request/owner courier companion, not callbacks
    // supplied by tests or by arbitrary parsed plan consumers.
    private static partial bool OfferOwner(Transfer transfer,RebirthVehicleTransferItem[] operations);
    private static partial bool CompleteOwner(Transfer transfer);
    private static partial bool EnsureOwnerReady(Transfer transfer);
}
