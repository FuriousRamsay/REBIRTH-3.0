using System;

// PREPARED, NOT RUN. Supply the actual received native package BEFORE native
// ProcessPackage saves it. This fixture never constructs a sender/file/receipt.
internal static class mpA_logistics_stale_terminal_upload_fixture
{
    internal static void ClassifyActualReceived(NetPackagePlayerData actual,World actualWorld,bool expectedRefusal)
    {
        if(actual?.Sender==null||actual.playerDataFile==null||actualWorld?.IsRemote()!=false)throw new Exception("Actual authenticated received native package required.");
        if(RebirthLogisticsOwnerReceipt.ShouldRefuseStaleTerminalUpload(actual,actualWorld)!=expectedRefusal)throw new Exception("Native stale-terminal classification differs from actual checkpoint expectation.");
    }
    // Required harness checkpoints (real events/files, never authored here):
    // 1 cold same-life saved0 upload before terminal reply + host canonical4 and
    //   positive native3/4 exact final After/belt/nonce => refuse before save.
    // 2 same original valid native3/4 final upload => pass.
    // 3 unrelated operation, new-life/death, changed creation/entity => pass.
    // 4 missing/corrupt/ambiguous header/journal/native final proof => pass;
    //   classifier cannot manufacture final history to deny native saving.
    // 5 incoming0/1/2 already full final After/belt => pass (no stale conflict).
    // 6 mismatched accepted nonce => pass, never claim original authority.
}
