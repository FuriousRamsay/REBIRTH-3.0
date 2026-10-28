using System;

// PREPARED, NOT RUN. The harness supplies the canonical terminal4 captured
// from the actual authenticated result package for THIS original. Never author
// a replacement journal/permission, edit native markers, or write player files.
internal sealed class mpA_logistics_terminal4_no_replay_fixture
{
    private readonly EntityPlayerLocal player;private readonly RebirthLogisticsTransferJournal.Record terminal;
    internal mpA_logistics_terminal4_no_replay_fixture(EntityPlayerLocal actual,RebirthLogisticsTransferJournal.Record actualAuthenticatedTerminal)
    {
        player=actual;terminal=actualAuthenticatedTerminal;
        if(actual?.world?.IsRemote()!=true||terminal?.Phase!=4||terminal.Outcome==0||!RebirthStablePlayerIdentity.TryResolveServerEntity(actual,out var owner)||owner.StorageKey!=terminal.Owner||RebirthLogisticsTransferJournal.WorldKey(actual.world)!=terminal.World||RebirthCamperCharacterAccess.CurrentCreation(actual)!=terminal.Creation||!RebirthLogisticsOwnerReceipt.TryOperation(actual.Buffs,out var operation,out _)||operation!=terminal.Operation)throw new Exception("Actual correlated terminal4/native original is required.");
    }
    private bool Current()=>ReferenceEquals(GameManager.Instance?.World,player.world)&&RebirthLogisticsTransferJournal.WorldKey(player.world)==terminal.World&&RebirthLogisticsOwnerReceipt.TryOperation(player.Buffs,out var operation,out _)&&operation==terminal.Operation;
    internal void DeliverAtActualNativePhase(int requiredPhase,bool expectMetadataClosure)
    {
        if(!RebirthLogisticsOwnerReceipt.TryOperation(player.Buffs,out _,out int phase)||phase!=requiredPhase)throw new Exception("Harness must produce the actual requested native stage; fixture never changes markers.");
        var bag=ItemStack.Clone(player.bag.ItemGrid.items);var belt=ItemStack.Clone(player.inventory.ItemGrid.items);
        bool closed=RebirthLogisticsOwnerReceipt.Advance(player,terminal,4,Current);
        if(closed!=expectMetadataClosure||!RebirthLogisticsDeathPartition.Same(bag,player.bag.ItemGrid.items)||!RebirthLogisticsDeathPartition.Same(belt,player.inventory.ItemGrid.items))throw new Exception("Terminal4 replayed inventory or violated positive metadata closure eligibility.");
        if(!closed&&(!RebirthLogisticsOwnerReceipt.TryOperation(player.Buffs,out _,out int after)||after!=phase))throw new Exception("Unknown terminal proof changed original native stage.");
    }
    internal void RequireEarlyStagesRetained(int actualPhase)
    {
        if(player.Died!=terminal.Deaths||actualPhase<0||actualPhase>2)throw new Exception("Requires actual same-life stage0/1/2.");DeliverAtActualNativePhase(actualPhase,false);
    }
    internal void RequireMissingNativeFileRetained()
    {
        if(!RebirthStablePlayerIdentity.TryFromLocalPlatform(out var owner)||RebirthGearNativePlayerFile.TryRead(owner,out _))throw new Exception("Harness must first establish an actually unavailable exact own final file.");
        RebirthLogisticsOwnerReceipt.TryOperation(player.Buffs,out _,out int phase);DeliverAtActualNativePhase(phase,false);
    }
    internal void RequireStaleNativePhaseRetained()
    {
        if(!RebirthStablePlayerIdentity.TryFromLocalPlatform(out var owner)||!RebirthLogisticsOwnerReceipt.TrySavedOperation(owner,out var operation,out int savedPhase,out _,out _)||!RebirthLogisticsOwnerReceipt.TryOperation(player.Buffs,out var live,out int phase)||operation!=live||phase==savedPhase)throw new Exception("Requires actual stale own saved phase, not a fixture-created receipt.");DeliverAtActualNativePhase(phase,false);
    }
    internal void RequireMissingAcceptedNonceRetained()
    {
        if(RebirthLogisticsHeaderAdmission.TryAcceptedNonce(player,terminal,out _)&&RebirthLogisticsOwnerReceipt.TryLocalHeader(player,out _))throw new Exception("Requires actual missing/unsupported accepted nonce evidence.");
        RebirthLogisticsOwnerReceipt.TryOperation(player.Buffs,out _,out int phase);DeliverAtActualNativePhase(phase,false);
    }
    internal void RequireLiveAfterMismatchRetained(bool beltMismatch)
    {
        ItemStack[] expected;if(terminal.Outcome==3)expected=terminal.OriginalOwnerBag;else if(!RebirthLogisticsDeathPartition.TryExpected(terminal,3,out expected))throw new Exception("Original projection is unavailable.");
        bool mismatch=beltMismatch?!RebirthLogisticsDeathPartition.Same(terminal.OriginalOwnerBelt,player.inventory.ItemGrid.items):!RebirthLogisticsDeathPartition.Same(expected,player.bag.ItemGrid.items);
        if(!mismatch||player.Died!=terminal.Deaths)throw new Exception("Actual live native full postimage mismatch required.");RebirthLogisticsOwnerReceipt.TryOperation(player.Buffs,out _,out int phase);DeliverAtActualNativePhase(phase,false);
    }
    internal void RequireOldLifeProofGate(bool expectProof)
    {
        if(player.Died<=terminal.Deaths||RebirthLogisticsOriginalTransport.HasPhysicalTerminalEvidence(player,terminal)!=expectProof)throw new Exception("Requires actual old-life named evidence delivery/missing evidence checkpoint.");
        RebirthLogisticsOwnerReceipt.TryOperation(player.Buffs,out _,out int phase);DeliverAtActualNativePhase(phase,expectProof);
    }
    internal void RequireDirectReleaseCannotBypassEarlyStage(int actualPhase)
    {
        if(player.Died!=terminal.Deaths||actualPhase<0||actualPhase>2||!RebirthLogisticsOwnerReceipt.TryOperation(player.Buffs,out _,out int phase)||phase!=actualPhase)throw new Exception("Requires actual same-life early native phase.");
        var bag=ItemStack.Clone(player.bag.ItemGrid.items);var belt=ItemStack.Clone(player.inventory.ItemGrid.items);float held=player.Buffs.GetCustomVar("rbLogisticsOwnerHeld");
        if(RebirthLogisticsOwnerReceipt.ReleaseCompletedOriginal(player,terminal,Current)||!RebirthLogisticsDeathPartition.Same(bag,player.bag.ItemGrid.items)||!RebirthLogisticsDeathPartition.Same(belt,player.inventory.ItemGrid.items)||!RebirthLogisticsOwnerReceipt.TryOperation(player.Buffs,out _,out int after)||after!=phase||player.Buffs.GetCustomVar("rbLogisticsOwnerHeld")!=held)throw new Exception("Direct terminal closure bypassed same-life positive final proof.");
    }
    internal void RequireUnauthenticatedTerminalRetained()
    {
        if(!RebirthLogisticsOwnerReceipt.TryOperation(player.Buffs,out _,out int phase))throw new Exception("Actual native original required.");
        var bag=ItemStack.Clone(player.bag.ItemGrid.items);var belt=ItemStack.Clone(player.inventory.ItemGrid.items);float held=player.Buffs.GetCustomVar("rbLogisticsOwnerHeld");
        if(RebirthLogisticsOwnerReceipt.ReleaseCompletedOriginal(player,terminal,()=>false)||!RebirthLogisticsDeathPartition.Same(bag,player.bag.ItemGrid.items)||!RebirthLogisticsDeathPartition.Same(belt,player.inventory.ItemGrid.items)||!RebirthLogisticsOwnerReceipt.TryOperation(player.Buffs,out _,out int after)||after!=phase||player.Buffs.GetCustomVar("rbLogisticsOwnerHeld")!=held)throw new Exception("Uncorroborated terminal changed original custody.");
    }
    internal void RequireMismatchedServerPhaseRetained(int mismatchedServerPhase)
    {
        if(mismatchedServerPhase==4||!RebirthLogisticsOwnerReceipt.TryOperation(player.Buffs,out _,out int phase))throw new Exception("Actual native original and nonterminal wire phase required.");
        var bag=ItemStack.Clone(player.bag.ItemGrid.items);var belt=ItemStack.Clone(player.inventory.ItemGrid.items);float held=player.Buffs.GetCustomVar("rbLogisticsOwnerHeld");
        if(RebirthLogisticsOwnerReceipt.Advance(player,terminal,mismatchedServerPhase,Current)||!RebirthLogisticsDeathPartition.Same(bag,player.bag.ItemGrid.items)||!RebirthLogisticsDeathPartition.Same(belt,player.inventory.ItemGrid.items)||!RebirthLogisticsOwnerReceipt.TryOperation(player.Buffs,out _,out int after)||after!=phase||player.Buffs.GetCustomVar("rbLogisticsOwnerHeld")!=held)throw new Exception("Mismatched terminal/wire phase changed native custody.");
    }
    // Matrix: actual same-life0/1/2 => retained; native3/4 + exact saved full
    // After/belt/phase/hold/nonce => metadata-only close; native5 only actual
    // Outcome3/full saved Before; stale phase/nonce/bag/belt/file => retained.
    // Old-life data never uses current Before; only actual named terminal
    // publication assertion plus correlated canonical4 can close metadata.
}
