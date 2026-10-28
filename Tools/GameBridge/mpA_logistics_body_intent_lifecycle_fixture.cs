using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;

// PREPARED, NOT RUN. Separate owner/host methods run in their REAL respective
// processes. No client reads a host journal; no fixture authors receipt fields.
internal sealed class mpA_logistics_body_intent_lifecycle_fixture
{
    private readonly RebirthLogisticsTransferJournal.Record original;private readonly World world;private readonly int entity;private readonly int[] before;
    internal mpA_logistics_body_intent_lifecycle_fixture(RebirthLogisticsTransferJournal.Record actual,EntityPlayer actor)
    {
        if(actor?.world?.IsRemote()!=false||!RebirthLogisticsTransferJournal.TryRead(actual.Owner,actual.Operation,out var saved)||
            RebirthLogisticsTransferJournal.Encode(saved)!=RebirthLogisticsTransferJournal.Encode(actual)||!RebirthLogisticsNativeDeathReceipt.TryRead(saved,out var fatal))throw new Exception("Requires real admitted host original and actual fatal.");
        original=saved;world=actor.world;entity=fatal.OwnerEntity;before=Bodies();
    }
    private int[] Bodies()=>world.Entities.list.OfType<EntityBackpack>().Where(value=>value.RefPlayerId==entity).Select(value=>value.entityId).OrderBy(value=>value).ToArray();
    internal string RequirePreparedNoEffect()
    {
        if(!RebirthLogisticsNativeInvocationJournal.TryReadStored(original,out var nonce,out var native,out int state)||state!=0||RebirthLogisticsNativeInvocationJournal.HasAttempt(original)||
            !Guid.TryParseExact(nonce,"N",out var id)||id==Guid.Empty||string.IsNullOrEmpty(native)||!before.SequenceEqual(Bodies()))throw new Exception("Host Prepared did not remain positively never invoked.");
        return nonce;
    }
    internal void RequireAttemptedOutcome(bool expectAccepted)
    {
        if(!RebirthLogisticsNativeInvocationJournal.TryReadStored(original,out _,out _,out int state)||state!=1||!RebirthLogisticsNativeInvocationJournal.HasAttempt(original))throw new Exception("Native invocation was not durably attempted.");
        var added=Bodies().Except(before).ToArray();if(expectAccepted?added.Length!=1:added.Length!=0)throw new Exception("Actual original accepted native body count mismatch.");
        // Attempted-but-absent is explicitly unknown, never permission to call again.
    }
    internal void RepeatActualPreparedQuery(ClientInfo sender,string nonce,string exactBodyHash)
    {
        if(!RebirthLogisticsNativeInvocationJournal.TryReadStored(original,out _,out _,out int state)||state!=1)throw new Exception("Requires actual attempted original before replay test.");
        var prior=Bodies();RebirthLogisticsNativeBodyIntent.Query(sender,original.Operation,RebirthLogisticsNativeBodyIntent.Command.From(original).Binding,exactBodyHash,nonce);
        if(!prior.SequenceEqual(Bodies()))throw new Exception("Attempted original query spawned another body.");
    }
    internal static void RequireOwnerCapsule(EntityPlayerLocal actor,Guid operation,byte[] actualOriginalCanonicalEcd,bool prepared)
    {
        if(actor?.world?.IsRemote()!=true||!RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var owner))throw new Exception("Owner process/native scope required.");
        string path=Path.Combine(GameIO.GetPlayerDataDir(),"RebirthLogisticsNativeBodyIntent",RebirthLogisticsTransferJournal.WorldKey(actor.world),owner.StorageKey,operation.ToString("N")+".xml");
        var file=new FileInfo(path);if(!file.Exists||file.Length<1||file.Length>RebirthLogisticsNativeBodyIntent.MaximumBytes)throw new Exception("Full original owner capsule not durable before send.");
        var root=XElement.Load(path);if(!Convert.FromBase64String(root.Element("native").Value).SequenceEqual(actualOriginalCanonicalEcd))throw new Exception("Original full native body/preferences changed.");
        if(prepared&&!File.Exists(path+".prepared"))throw new Exception("Owner did not durably retain actual host Prepared acknowledgement before permit.");
        // Candidate-conflict, cold Prepared/Attempted, missing host record,
        // pressure retry and reordered ACK cases require real harness events.
    }
}
