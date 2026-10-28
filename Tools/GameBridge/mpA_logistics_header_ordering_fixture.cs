using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;

// PREPARED, NOT RUN. These checkpoints consume a real user Request, actual
// authenticated ACK/native upload, and actual lifecycle events in each process.
// They never synthesize headers, native markers, source journals or permissions.
internal sealed class mpA_logistics_header_ordering_fixture
{
    private readonly EntityPlayerLocal player;private readonly Guid operation;private readonly ItemStack[] beforeBag,beforeBelt;private readonly string path;
    internal mpA_logistics_header_ordering_fixture(EntityPlayerLocal actual,Guid original)
    {
        player=actual;operation=original;if(actual?.world?.IsRemote()!=true||!RebirthLogisticsOriginalTransport.TryReadCommandScope(actual,original,out var command))throw new Exception("Requires real owner intent before Header ACK.");
        beforeBag=ItemStack.Clone(actual.bag.ItemGrid.items);beforeBelt=ItemStack.Clone(actual.inventory.ItemGrid.items);
        path=Path.Combine(GameIO.GetPlayerDataDir(),"RebirthLogisticsHeaderAdmission",command.World,command.Owner,original.ToString("N"),"header.xml");
    }
    internal void RequireWaitingWithoutNativeHold()
    {
        if(!File.Exists(path)||File.Exists(path+".holdAttempted")||RebirthLogisticsOwnerReceipt.HasPendingOriginal(player)||!RebirthLogisticsDeathPartition.Same(beforeBag,player.bag.ItemGrid.items)||!RebirthLogisticsDeathPartition.Same(beforeBelt,player.inventory.ItemGrid.items))throw new Exception("Lost/delayed Header ACK acquired native custody or changed items.");
    }
    internal void RequireAcceptedBeforeHold()
    {
        if(!File.Exists(path+".accepted")||!File.Exists(path+".holdAttempted")||!RebirthLogisticsOwnerReceipt.TryOperation(player.Buffs,out var actual,out int phase)||actual!=operation||phase!=0)throw new Exception("Native Begin lacks durable original acceptance/attempt markers.");
        var accepted=XElement.Load(path+".accepted");var attempted=XElement.Load(path+".holdAttempted");if((string)accepted.Attribute("nonce")!=(string)attempted.Attribute("nonce")||!XNode.DeepEquals(accepted.Element("command"),attempted.Element("command")))throw new Exception("Acceptance and HoldAttempted do not bind same immutable original.");
    }
    internal void RequestActualPreHoldCancel()
    {
        RequireWaitingWithoutNativeHold();RebirthLogisticsOriginalTransport.CancelCurrent();if(!File.Exists(path+".cancel")||File.Exists(path+".holdAttempted"))throw new Exception("Pre-hold cancel did not durably exclude Begin.");
    }
    internal void RequireCanceledArchive()
    {
        string directory=Path.GetDirectoryName(path);string archive=Path.Combine(Path.GetDirectoryName(directory),"archive",operation.ToString("N"),"header.xml");if(Directory.Exists(directory)||!File.Exists(archive+".done")||!File.Exists(archive+".cancel")||File.Exists(archive+".holdAttempted")||RebirthLogisticsOwnerReceipt.HasPendingOriginal(player))throw new Exception("Positive pre-hold cancel archive changed native custody or lost original metadata.");
    }
    internal static void RequireHostNativeAdmission(EntityPlayer actor,Guid operation)
    {
        if(actor?.world?.IsRemote()!=false||!RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var owner)||!RebirthLogisticsOwnerReceipt.TrySaved(owner,operation,0,out var saved)||!RebirthLogisticsOwnerReceipt.TryReadDetachedHeader(saved,out var nonce)||!RebirthLogisticsTransferJournal.TryRead(owner.StorageKey,operation,out var original))throw new Exception("Requires real host saved0 and original plan.");
        string path=Path.Combine(GameIO.GetSaveGameDir(),"RebirthLogisticsHeaders",owner.StorageKey,operation.ToString("N")+".xml");var h=XElement.Load(path);
        if((string)h.Attribute("version")=="2"&&((int?)h.Attribute("bodyIndex")!=2||!RebirthLogisticsBodyAdmissionIndex.Ready(RebirthLogisticsNativeBodyIntent.Command.From(original),nonce)))throw new Exception("Mandatory index was not positively Ready before native/source admission.");
        if((int)h.Attribute("state")!=1||(string)h.Attribute("nonce")!=nonce||(string)h.Attribute("binding")!=RebirthLogisticsNativeBodyIntent.Command.From(original).Binding)throw new Exception("Source admission was not preceded by exact host Admitting/native nonce binding.");
    }
    internal void RequireColdAttemptDoesNotRearm()
    {
        if(!File.Exists(path+".holdAttempted"))throw new Exception("Requires a real durable Begin attempt before cold recovery.");
        bool had=RebirthLogisticsOwnerReceipt.TryOperation(player.Buffs,out var prior,out int phase);var bag=ItemStack.Clone(player.bag.ItemGrid.items);var belt=ItemStack.Clone(player.inventory.ItemGrid.items);
        RebirthLogisticsHeaderAdmission.Tick(player);
        bool has=RebirthLogisticsOwnerReceipt.TryOperation(player.Buffs,out var next,out int nextPhase);
        if(had!=has||had&&(prior!=next||phase!=nextPhase)||!RebirthLogisticsDeathPartition.Same(bag,player.bag.ItemGrid.items)||!RebirthLogisticsDeathPartition.Same(belt,player.inventory.ItemGrid.items))throw new Exception("Cold attempted header rearmed native Begin or changed owner cells.");
    }
    internal static void ReplayActualHostHeader(ClientInfo sender,Guid operation,int expectedState,bool cancel)
    {
        if(!RebirthStablePlayerIdentity.TryFromClientInfo(sender,out var owner))throw new Exception("Actual authenticated sender required.");
        string path=Path.Combine(GameIO.GetSaveGameDir(),"RebirthLogisticsHeaders",owner.StorageKey,operation.ToString("N")+".xml");var before=XElement.Load(path);
        if((int)before.Attribute("state")!=expectedState)throw new Exception("Requires actual expected host lifecycle checkpoint.");
        var actor=GameManager.Instance.World.GetEntity(sender.entityId) as EntityPlayer;if(actor==null)throw new Exception("Current native actor required.");
        var bag=ItemStack.Clone(actor.bag.ItemGrid.items);var belt=ItemStack.Clone(actor.inventory.ItemGrid.items);
        RebirthLogisticsHeaderAdmission.Receive(sender,before.Element("command").ToString(SaveOptions.DisableFormatting),cancel?RebirthLogisticsHeaderAdmission.RequestMode.Cancel:RebirthLogisticsHeaderAdmission.RequestMode.Prepare);
        var after=XElement.Load(path);
        if(!XNode.DeepEquals(before,after)||!RebirthLogisticsDeathPartition.Same(bag,actor.bag.ItemGrid.items)||!RebirthLogisticsDeathPartition.Same(belt,actor.inventory.ItemGrid.items))throw new Exception("Header replay changed immutable identity/native cells or canceled an Admitting original.");
        // Use expectedState0/cancel=false for ACK retry, state1/cancel=true for
        // late prehold cancel refusal, state2/cancel=false for canceled replay.
    }
    internal void RequireChangedScopeNeverRearms()
    {
        if(File.Exists(path+".holdAttempted"))throw new Exception("Requires genuinely pre-hold intent.");
        var root=XElement.Load(path);var command=root.Element("command");if((int)command.Attribute("deaths")==player.Died&&(string)command.Attribute("creation")==RebirthCamperCharacterAccess.CurrentCreation(player)&&(int)root.Attribute("entity")==player.entityId)throw new Exception("Actual death/creation/entity change is required.");
        var bag=ItemStack.Clone(player.bag.ItemGrid.items);RebirthLogisticsHeaderAdmission.Tick(player);
        if(File.Exists(path+".holdAttempted")||!File.Exists(path+".cancel")||!RebirthLogisticsDeathPartition.Same(bag,player.bag.ItemGrid.items))throw new Exception("Changed incarnation rearmed pre-hold action.");
    }
    internal void RequireReadOnlyTerminalQuery()
    {
        if(!RebirthLogisticsHeaderAdmission.CanQueryCompleted(player,operation))throw new Exception("Requires real same-original native Held0 terminal marker and durable attempt.");
        var bag=ItemStack.Clone(player.bag.ItemGrid.items);var belt=ItemStack.Clone(player.inventory.ItemGrid.items);RebirthLogisticsHeaderAdmission.Tick(player);
        if(!RebirthLogisticsDeathPartition.Same(bag,player.bag.ItemGrid.items)||!RebirthLogisticsDeathPartition.Same(belt,player.inventory.ItemGrid.items)||RebirthLogisticsOwnerReceipt.HasPendingOriginal(player))throw new Exception("Readonly Header terminal recovery rearmed or changed native owner.");
    }
    internal void RequireArchiveAfterActualTerminal()
    {
        string directory=Path.GetDirectoryName(path);string archived=Path.Combine(Path.GetDirectoryName(directory),"archive",operation.ToString("N"),"header.xml.done");
        if(Directory.Exists(directory)||!File.Exists(archived)||(int)XElement.Load(archived).Attribute("terminal")!=4||RebirthLogisticsOwnerReceipt.HasPendingOriginal(player))throw new Exception("Actual authenticated terminal4 did not retire only Header metadata.");
    }
    internal static void RequireFatalWithDeferredPrepare(EntityPlayer actor,Guid operation,string actualPreviouslyObservedHeaderBinding)
    {
        if(actor?.world?.IsRemote()!=false||!RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var owner)||!RebirthLogisticsTransferJournal.TryRead(owner.StorageKey,operation,out var original)||original.Phase!=0||original.Legs.Count!=0||original.Npcs.Count!=0||RebirthLogisticsNativeBodyIntent.Command.From(original).Binding!=actualPreviouslyObservedHeaderBinding||!RebirthLogisticsNativeDeathReceipt.TryRead(original,out _))throw new Exception("Actual fatal after SAME saved0/before Prepare lacks canonical zero-effect/fatal chain.");
        string path=Path.Combine(GameIO.GetSaveGameDir(),"RebirthLogisticsHeaders",owner.StorageKey,operation.ToString("N")+".xml");if((int)XElement.Load(path).Attribute("state")!=3)throw new Exception("Actual never-Admitting header was not ended.");
    }
    // Real harness scheduling requirements: lost/reordered ACK, pressure retry,
    // cold accepted/no-attempt, cold attempted/no-native-result, cancel vs ACK,
    // source upload before/after host dispatch, actual death before first saved0,
    // creation/entity change without hold, malformed/temp candidates, host
    // header loss after ACK and positive original terminal archival.
}
