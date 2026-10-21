using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using HarmonyLib;

internal static partial class RebirthRemoteResourcePlacedStationOriginal
{
    internal sealed class CancellationJoining
    {
        internal NetPackagePlayerId Packet;internal World World;internal PlayerDataFile File;internal INetConnection Connection;internal readonly List<CancellationClient> Prepared=new List<CancellationClient>();
        internal EntityPlayerLocal Actor;internal int Reads;internal bool Completed;
    }
    [ThreadStatic]private static CancellationJoining cancellationJoining;
    internal static bool IsCancellationJoinPacket(NetPackage packet)=>packet is NetPackagePlayerId||ProtocolActive&&packet is NetPackageRebirthRemoteRepairResult;
    private static bool CancellationJoinConnectionCurrent(CancellationJoining joining)=>joining?.Connection!=null&&
        ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance?.connectionToServer?[0],joining.Connection);
    internal sealed class CancellationOwnerHydrated
    {
        internal readonly CancellationClient Original;private readonly World world;private readonly EntityPlayerLocal actor;private readonly ItemStackGrid bag,belt;
        private readonly int entity,deaths;private readonly string owner,creation;private readonly CancellationJoining joining;private bool accepted;
        private CancellationOwnerHydrated(CancellationClient original,CancellationJoining source)
        {Original=original;joining=source;world=source.World;actor=source.Actor;bag=actor.bag.ItemGrid;belt=actor.inventory.ItemGrid;entity=actor.entityId;deaths=original.Original.Deaths;owner=original.Original.Owner;creation=original.Original.Creation;}
        internal static CancellationOwnerHydrated FromActualHydration(CancellationClient original,CancellationJoining source)
        {
            if(!ReferenceEquals(cancellationJoining,source)||!CancellationJoinConnectionCurrent(source)||source.Reads!=1||!ReferenceEquals(source.Actor,original.Actor)||source.File==null||source.File.deaths!=original.Original.Deaths)return null;
            var result=new CancellationOwnerHydrated(original,source);return result.Current?result:null;
        }
        internal void NativeJoinCompleted(CancellationJoining source)
        {if(ReferenceEquals(joining,source)&&ReferenceEquals(cancellationJoining,source)&&CancellationJoinConnectionCurrent(source)&&source.Reads==1&&ReferenceEquals(source.Actor,actor))accepted=true;}
        internal bool AcceptedTerminal=>accepted&&FullTerminal;
        internal bool Current=>ThreadManager.IsMainThread()&&CancellationJoinConnectionCurrent(joining)&&(accepted||ReferenceEquals(cancellationJoining,joining))&&Original.Memory?.Current==true&&
            ReferenceEquals(GameManager.Instance?.World,world)&&ReferenceEquals(actor.world,world)&&ReferenceEquals(Original.Actor,actor)&&actor.entityId==entity&&!actor.IsDead()&&actor.Died==deaths&&
            ReferenceEquals(actor.bag?.ItemGrid,bag)&&ReferenceEquals(actor.inventory?.ItemGrid,belt)&&
            RebirthStablePlayerIdentity.TryFromLocalPlatform(out var actualOwner)&&actualOwner.CanonicalId==owner&&RebirthCamperCharacterAccess.CurrentCreation(actor)==creation&&
            actor.Buffs.GetCustomVar(CancellationReceipt(Original.Operation))==3f;
        internal bool FullTerminal
        {
            get
            {
                if(!Current||!Original.After.Matches(RebirthCamperEnergyActorImage.Capture(actor)))return false;
                var record=CancellationJournal(Original.Operation,"Terminal").Read(Original.Operation);byte[] expected=CancellationClientTerminalBytes(Original);
                return record?.State==RebirthCamperTransferJournal.Phase.Applied&&record.Vehicle==Original.Source&&record.OwnerCreation==creation&&record.Revision==0&&
                    record.Before.SequenceEqual(expected)&&record.After.SequenceEqual(expected);
            }
        }
    }
    private static CancellationClient PrepareRestartClient(CancellationJoining joining,Guid operation,bool savedAfterOnly=false,bool beforeOnly=false)
    {
        RebirthRemoteResourcePlacedStationPreparation.UploadRead memory=null;
        RebirthRemoteResourcePlacedStationAllocation.Scratch scratch=null;
        try
        {
            if(cancellationClients.ContainsKey(operation)||!RebirthStablePlayerIdentity.TryFromLocalPlatform(out var localOwner)||!RebirthRemoteResourcePlacedStationPreparation.TryReserveCancellationRead(operation,RebirthRemoteResourcePlacedStationAllocation.SharedReaderScratchBytes,out memory))return null;
            if(!RebirthRemoteResourcePlacedStationAllocation.TryBeginScratch(memory,RebirthRemoteResourcePlacedStationAllocation.ReadPhase.OwnerEnvelope,out scratch)||
                !ReadRestartHeader(operation,memory,out var request,out string header,out var source,out _,out _)||
                !RebirthRemoteResourceClientOriginal.TryDecodeOriginal(request.Element("fetchClientOriginal").ToString(SaveOptions.DisableFormatting),out var original)||
                original.Owner!=localOwner.CanonicalId||original.World!=joining.World.worldState.Guid||original.Deaths!=joining.File.deaths||
                original.Creation!="native-mode:"+original.Owner||CancellationOperation(original.Operation)!=operation||
                !TryCancellationClientFiles(original.Operation,out long historicalBytes)||!memory.Reserve(checked(memory.ReservedBytes+historicalBytes))||
                !ReadCancellationOriginal(original.Operation,memory,out var savedOriginal)||!XNode.DeepEquals(savedOriginal.Copy(),original.Copy()))return null;
            string client=ReadArchivedCancellation(operation,source,original.Creation,"Client",memory,RebirthCamperTransferJournal.Phase.Uncertain);
            string permission=ReadArchivedCancellation(operation,source,original.Creation,"Permission",memory,RebirthCamperTransferJournal.Phase.Uncertain);
            bool nonterminal=savedAfterOnly||beforeOnly;
            string terminal=nonterminal?null:ReadArchivedCancellation(operation,source,original.Creation,"Terminal",memory,RebirthCamperTransferJournal.Phase.Applied);
            if(client!=header||permission==null||!nonterminal&&terminal==null)return null;
            var body=RebirthCookingRemoteAdmission.ReadProtocolXml(permission,MaximumResultBytes);var terminalBody=nonterminal?null:RebirthCookingRemoteAdmission.ReadProtocolXml(terminal,MaximumResultBytes);
            if(body.Element("request")?.Value!=header||!nonterminal&&(terminalBody.Name!="placedCancellationClientTerminal"||terminalBody.Attributes().Count()!=4||terminalBody.Elements().Count()!=2||
                terminalBody.Element("permission")?.Value!=permission))return null;
            string paidText=request.Element("paidOriginal").Value;var paid=ClientPermissions().Read(original.Operation);
            if(paid==null||!paid.Before.SequenceEqual(paid.After)||new UTF8Encoding(false,true).GetString(paid.Before)!=paidText||
                !HasClientRetired(original,source,(string)request.Attribute("binding"),(string)request.Attribute("proof")))return null;
            var paidBody=RebirthCookingRemoteAdmission.ReadProtocolXml(paidText,MaximumResultBytes);
            if(!RebirthRemoteResourcePlacedStationAllocation.TryAdmitOwnerReplyBeforeHydration(paidBody,out long paidWorkspace)||
                !memory.Reserve(checked(memory.ReservedBytes+paidWorkspace-RebirthRemoteResourcePlacedStationAllocation.SharedReaderScratchBytes))||
                !RebirthRemoteResourcePlacedStationQueue.TryDecode(paidBody.Element("queue").Value,out var paidQueue)||
                !RebirthRemoteResourcePlacedStationReference.TryRead(paidBody.Element("reference")?.Value,out var reference)||reference.Source!=source||reference.Job!=original.Operation||
                reference.Binding!=(string)request.Attribute("binding")||reference.Birth!=(string)request.Attribute("birth"))return null;
            var own=paidQueue.Where(row=>row.Recipe?.ingredients?.Any(item=>item?.itemValue!=null&&item.itemValue.TryGetMetadata(RebirthRemoteResourcePlacedStationPaidRow.JobKey,out string id)&&id==original.Operation.ToString("N"))==true).ToArray();
            var beforeEvidence=RebirthCookingRemoteAdmission.ReadProtocolXml(body.Element("beforeEvidence").Value,MaximumResultBytes);
            if(own.Length!=1||(int?)request.Attribute("slot") is not int slot||(int?)beforeEvidence.Attribute("ownerId") is not int nativeOwner||nativeOwner<1)return null;
            var result=new CancellationClient{Original=original,Operation=operation,Source=source,Binding=reference.Binding,Birth=reference.Birth,Proof=(string)request.Attribute("proof"),
                OriginalWorld=joining.World,Directory=GameIO.GetSaveGameDir(),Header=header,Permission=permission,TerminalEvidence=terminalBody?.Element("evidence").Value,
                Before=RebirthRemoteResourceClientOriginal.ReadActor(request.Element("actorBefore").Value),SavedPaidRow=own[0],FullPaidPermission=paidText,Memory=memory,Slot=slot,Stage=beforeOnly?1:savedAfterOnly?2:3,NativeOwnerId=nativeOwner,
                PermissionSaved=true,HostTerminal=!nonterminal};
            // Full expected images are admitted by ReadRestartHeader. Actual
            // native owner adoption occurs only after this packet's ToPlayer.
            if(!nonterminal&&!Encoding.UTF8.GetBytes(terminal).SequenceEqual(CancellationClientTerminalBytes(result)))return null;
            memory=null;return result;
        }
        catch{return null;}
        finally{scratch?.Dispose();memory?.Dispose();}
    }
    [HarmonyPatch(typeof(NetPackagePlayerId),nameof(NetPackagePlayerId.ProcessPackage),new[]{typeof(World),typeof(GameManager)})]
    private static class CancellationPlayerJoin
    {
        private sealed class Purposes{internal CancellationJoining Cancellation;internal object Hire,Consumption,Recycler,ActionPayment,Trade,RecipeOutput;}
        [HarmonyPriority(Priority.Last)]private static void Prefix(NetPackagePlayerId __instance,World _world,GameManager _callbacks,bool __runOriginal,out object __state)
        {
            var purposes=new Purposes();__state=purposes;
            try{purposes.Trade=RebirthNpcTradeOwnerLoad.EnterJoin(__instance,_world,_callbacks,__runOriginal);}catch{}
            try{purposes.RecipeOutput=RebirthRecipeOutputIntegration.EnterRemoteOutputOwnerJoin(__instance,_world,_callbacks,__runOriginal);}catch{}
            try{purposes.ActionPayment=RemoteResourceTransactions.EnterActionPaymentNativeJoin(__instance,_world,_callbacks,__runOriginal);}catch{}
            try{purposes.Hire=RebirthDogLifecycleService.EnterHireRemoteJoin(__instance,_world,_callbacks,__runOriginal);}catch{}
            try{purposes.Consumption=RebirthNpcMetabolismCustody.EnterClientNativeJoin(__instance,_world,_callbacks,__runOriginal);}catch{}
            try{purposes.Recycler=RebirthRecyclerPlacedSettlement.EnterRecyclerRemoteJoin(__instance,_world,_callbacks,__runOriginal);}catch{}
            try
            {
                if(!ProtocolActive||!__runOriginal||!ThreadManager.IsMainThread()||cancellationJoining!=null||_world?.IsRemote()!=true||!ReferenceEquals(_world,GameManager.Instance?.World)||
                    !ReferenceEquals(_callbacks,GameManager.Instance)||
                    !RebirthProductiveEarnedUiDeferredTransport.TryGetReceivedConnection(__instance,out var received)||
                    !ReferenceEquals(received,SingletonMonoBehaviour<ConnectionManager>.Instance?.connectionToServer?[0])||
                    __instance.playerDataFile?.bLoaded!=true||__instance.playerDataFile.bDead||
                    !RebirthRemoteResourcePlacedStationSavedReceipt.TryCancellationTerminalKeys(__instance.playerDataFile,out var terminalOperations)||
                    !RebirthRemoteResourcePlacedStationSavedReceipt.TryCancellationSavedAfterKeys(__instance.playerDataFile,out var savedAfterOperations)||
                    !RebirthRemoteResourcePlacedStationSavedReceipt.TryCancellationBeforeKeys(__instance.playerDataFile,out var beforeOperations)||terminalOperations.Length+savedAfterOperations.Length+beforeOperations.Length>32)return;
                BindCancellationWorld();var joining=new CancellationJoining{Packet=__instance,File=__instance.playerDataFile,World=_world,Connection=received};
                cancellationJoining=joining;purposes.Cancellation=joining;
                foreach(var operation in terminalOperations){var original=PrepareRestartClient(joining,operation);if(original!=null)joining.Prepared.Add(original);}
                foreach(var operation in savedAfterOperations){var original=PrepareRestartClient(joining,operation,true);if(original!=null)joining.Prepared.Add(original);}
                foreach(var operation in beforeOperations){var original=PrepareRestartClient(joining,operation,false,true);if(original!=null)joining.Prepared.Add(original);}
            }
            catch{/* Native owner join proceeds with no reconstructed permit. */}
        }
        private static void Postfix(NetPackagePlayerId __instance,World _world,bool __runOriginal,object __state)
        {
            var purposes=__state as Purposes;
            try{RebirthNpcTradeOwnerLoad.ObserveJoin(__instance,_world,__runOriginal,purposes?.Trade);}catch{}
            try{RebirthRecipeOutputIntegration.ObserveRemoteOutputOwnerJoin(__instance,_world,__runOriginal,purposes?.RecipeOutput);}catch{}
            try{RemoteResourceTransactions.ObserveActionPaymentNativeJoin(__instance,_world,__runOriginal,purposes?.ActionPayment);}catch{}
            try{RebirthDogLifecycleService.ObserveHireRemoteJoin(__instance,_world,__runOriginal,purposes?.Hire);}catch{}
            try{RebirthNpcMetabolismCustody.ObserveClientNativeJoin(__instance,_world,__runOriginal,purposes?.Consumption);}catch{}
            try{RebirthRecyclerPlacedSettlement.ObserveRecyclerRemoteJoin(__instance,_world,__runOriginal,purposes?.Recycler);}catch{}
            var joining=purposes?.Cancellation;
            if(!__runOriginal||joining==null||!ReferenceEquals(cancellationJoining,joining)||!CancellationJoinConnectionCurrent(joining)||!ReferenceEquals(joining.Packet,__instance)||joining.Reads!=1||joining.Actor==null||
                !ReferenceEquals(_world.GetPrimaryPlayer(),joining.Actor)||!ReferenceEquals(_world.GetEntity(joining.Actor.entityId),joining.Actor))return;
            foreach(var original in joining.Prepared)if(original.RestartOwner!=null)original.RestartOwner.NativeJoinCompleted(joining);
            foreach(var original in joining.Prepared)if(original.RestartSavedAfterOwner!=null)original.RestartSavedAfterOwner.NativeJoinCompleted(joining);
            foreach(var original in joining.Prepared)original.RestartBeforeOwner?.NativeJoinCompleted(joining);
            foreach(var original in joining.Prepared)
                if(original.RestartBeforeOwner?.Accepted==true||original.RestartSavedAfterOwner?.Accepted==true||original.RestartOwner?.AcceptedTerminal==true)
                    ObserveCancellationScenarioHydration(original);
            joining.Completed=true;
        }
        private static Exception Finalizer(Exception __exception,object __state)
        {
            var purposes=__state as Purposes;
            try{RebirthNpcTradeOwnerLoad.EndJoin(purposes?.Trade,__exception);}catch{}
            try{RebirthRecipeOutputIntegration.EndRemoteOutputOwnerJoin(purposes?.RecipeOutput,__exception);}catch{}
            try{RemoteResourceTransactions.EndActionPaymentNativeJoin(purposes?.ActionPayment,__exception);}catch{}
            try{RebirthDogLifecycleService.EndHireRemoteJoin(purposes?.Hire,__exception);}catch{}
            try{RebirthNpcMetabolismCustody.EndClientNativeJoin(purposes?.Consumption,__exception);}catch{}
            try{RebirthRecyclerPlacedSettlement.EndRecyclerRemoteJoin(purposes?.Recycler,__exception);}catch{}
            if(purposes?.Cancellation is CancellationJoining joining)
            {
                foreach(var original in joining.Prepared)
                    if(!joining.Completed||__exception!=null||(original.Stage==1?original.RestartBeforeOwner?.Accepted!=true||!RebirthRemoteResourceOwnerAction.HoldsPlacedCancellation(original):original.Stage==2?original.OwnerSavedAfterVersion==null||original.RestartSavedAfterOwner?.Accepted!=true:original.OwnerTerminalVersion==null)||!cancellationClients.TryGetValue(original.Operation,out var registered)||!ReferenceEquals(registered,original))
                    {RebirthRemoteResourceOwnerAction.DropFailedRestartCancellation(original);DropCancellationClient(original);}
                joining.Prepared.Clear();joining.File=null;joining.Packet=null;joining.Actor=null;
                if(ReferenceEquals(cancellationJoining,joining))cancellationJoining=null;
            }
            return __exception;
        }
    }
    [HarmonyPatch(typeof(PlayerDataFile),nameof(PlayerDataFile.ToPlayer),new[]{typeof(EntityPlayer),typeof(global::StreamModeRead)})]
    private static class CancellationOwnerHydration
    {
        private sealed class Purposes{internal object Cancellation,Recycler,RecipeOutput,Trade;}
        private static void Prefix(PlayerDataFile __instance,EntityPlayer _player,global::StreamModeRead _eStreamMode,bool __runOriginal,out object __state)
        {
            try{RemoteResourceTransactions.EnterActionPaymentNativeHydration(__instance,_player,_eStreamMode,__runOriginal);}catch{}
            try{RebirthRecipeOutputIntegration.EnterRemoteOutputOwnerHydration(__instance,_player,_eStreamMode,__runOriginal);}catch{}
            try{RebirthDogLifecycleService.EnterHireOwnerToPlayer(__instance,_player,_eStreamMode,__runOriginal);}catch{}
            var recycler=RebirthRecyclerPlacedSettlement.EnterRecyclerOwnerHydration(__instance,_player,_eStreamMode,__runOriginal);
            object recipeOutput=null;try{recipeOutput=RebirthRecipeOutputIntegration.EnterRecipeOutputOwnerHydration(__instance,_player,_eStreamMode,__runOriginal);}catch{}
            object trade=null;try{trade=RebirthNpcTradeOwnerLoad.EnterHydration(__instance,_player,_eStreamMode,__runOriginal);}catch{}
            var purposes=recycler==null&&recipeOutput==null&&trade==null?null:new Purposes{Recycler=recycler,RecipeOutput=recipeOutput,Trade=trade};
            __state=purposes;var joining=cancellationJoining;
            if(!__runOriginal||_eStreamMode!=global::StreamModeRead.FromServer||joining==null||!CancellationJoinConnectionCurrent(joining)||!ReferenceEquals(joining.File,__instance)||_player is not EntityPlayerLocal actor||!ReferenceEquals(actor.world,joining.World)||joining.Reads!=0||
                !ReferenceEquals(joining.World.GetPrimaryPlayer(),actor)||actor.entityId!=joining.Packet.id)return;
            joining.Reads++;joining.Actor=actor;if(purposes==null){purposes=new Purposes();__state=purposes;}purposes.Cancellation=joining;
        }
        [HarmonyPriority(Priority.Last)]private static void Postfix(PlayerDataFile __instance,EntityPlayer _player,bool __runOriginal,object __state)
        {
            try{RemoteResourceTransactions.ObserveActionPaymentNativeHydration(__instance,_player,__runOriginal);}catch{}
            try{RebirthRecipeOutputIntegration.ObserveRemoteOutputOwnerHydration(__instance,_player,__runOriginal);}catch{}
            try{RebirthDogLifecycleService.ObserveHireOwnerToPlayer(__instance,_player,__runOriginal);}catch{}
            var purposes=__state as Purposes;
            try{RebirthNpcTradeOwnerLoad.ObserveHydration(__instance,_player,__runOriginal,purposes?.Trade);}catch{}
            finally{try{RebirthNpcTradeOwnerLoad.EndHydration(purposes?.Trade);}catch{}}
            try{RebirthRecipeOutputIntegration.ObserveRecipeOutputOwnerHydration(__instance,_player,__runOriginal,purposes?.RecipeOutput);}catch{}
            finally{try{RebirthRecipeOutputIntegration.EndRecipeOutputOwnerHydration(purposes?.RecipeOutput);}catch{}}
            try{RebirthRecyclerPlacedSettlement.ObserveRecyclerOwnerHydration(__instance,_player,__runOriginal,purposes?.Recycler);}
            finally{RebirthRecyclerPlacedSettlement.EndRecyclerOwnerHydration(purposes?.Recycler);}
            try
            {
                if(!__runOriginal||purposes?.Cancellation is not CancellationJoining joining||!ReferenceEquals(cancellationJoining,joining)||!ReferenceEquals(joining.File,__instance)||!ReferenceEquals(joining.Actor,_player)||joining.Reads!=1)return;
                foreach(var original in joining.Prepared)
                {
                    if(!RebirthRemoteResourcePlacedStationAllocation.TryBeginScratch(original.Memory,RebirthRemoteResourcePlacedStationAllocation.ReadPhase.OwnerEnvelope,out var scratch))continue;
                    using(scratch)
                    {
                    original.Actor=joining.Actor;
                    if(!RebirthRemoteResourcePlacedStationAllocation.TryAdmitCancellationOwnerBeforeCapture(joining.Actor,out long nativeWorkspace)||
                        !original.Memory.Reserve(checked(original.Memory.ReservedBytes+nativeWorkspace)))continue;
                    var permission=RebirthCookingRemoteAdmission.ReadProtocolXml(original.Permission,MaximumResultBytes);
                    if(original.Stage==1){if(ValidateCancellationPermission(original,permission,true))HydrateRestartBefore(original,joining);continue;}
                    if(!ValidateCancellationPermission(original,permission,true)||!original.After.Matches(RebirthCamperEnergyActorImage.Capture(joining.Actor))||
                        joining.Actor.Died!=original.Original.Deaths||joining.Actor.Buffs.GetCustomVar(CancellationReceipt(original.Operation))!=(original.Stage==2?2f:3f))continue;
                    if(original.Stage==2){HydrateRestartSavedAfter(original,joining);continue;}
                    string evidenceText=original.TerminalEvidence;var evidence=RebirthCookingRemoteAdmission.ReadProtocolXml(evidenceText,MaximumResultBytes);
                    byte[] marker=CancellationMarkerImage(original.Operation,original.Original.Operation,original.Source,original.Binding,original.Birth,original.Header);
                    if(!ValidateInput(original.Original,out var position,out _,out _,out _,out _,out _,out _,out _,out _,out _,out _)||
                        !ValidateCancellationSnapshot(evidence,original.Operation,original.Source,original.Binding,original.Birth,original.Header,true,
                            original.NativeAfter,original.After,original.NativeOwnerId,original.Original.Deaths,position,marker,out _))continue;
                    var hydrated=CancellationOwnerHydrated.FromActualHydration(original,joining);original.RestartOwner=hydrated;
                    if(hydrated?.FullTerminal!=true||cancellationClients.ContainsKey(original.Operation))continue;
                    cancellationClients.Add(original.Operation,original);
                    if(!RebirthRemoteResourceOwnerAction.HoldRestartCancellation(hydrated))continue;
                    original.OwnerTerminalVersion=RebirthRemoteResourceOwnerAction.PlacedCancellationTerminalVersion.CaptureRestart(hydrated);
                    }
                }
            }
            catch{/* The native hydration result is unchanged; unknown retains receipts. */}
        }
        private static Exception Finalizer(Exception __exception,PlayerDataFile __instance,object __state)
        {if(__exception!=null){try{RebirthRecipeOutputIntegration.ObserveRemoteOutputOwnerHydration(__instance,null,false);}catch{}try{RebirthNpcTradeOwnerLoad.EndHydration((__state as Purposes)?.Trade,true);}catch{}try{RebirthRecipeOutputIntegration.EndRecipeOutputOwnerHydration((__state as Purposes)?.RecipeOutput);}catch{}try{RebirthDogLifecycleService.EndHireOwnerToPlayer(__instance);}catch{}
            RebirthRecyclerPlacedSettlement.EndRecyclerOwnerHydration((__state as Purposes)?.Recycler);}return __exception;}
    }
}
