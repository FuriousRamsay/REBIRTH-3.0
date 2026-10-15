using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;

// Retention/scheduling remain unintegrated. ACK transport is guarded default-off.
// Retains replies, never re-enters consumption. Memory-only: not a durable receipt.
internal static class RebirthConsumptionPublicationRetry
{
    // Default off. Parent review of durability/receipt lifetime is required before enabling.
    internal static bool AcknowledgementProtocolEnabled;
    // Parent-owned lifecycle may install this only after durability review. Null preserves memory-only behavior.
    internal static Func<World,EntityPlayer,Guid,string,bool> DurableAcknowledgement;
    private sealed class Retained
    {
        internal EntityPlayer Player;
        internal string Creation;
        internal byte[] Wire;
        internal bool Acknowledged;
        internal int Attempts;
    }
    private const int MaximumRecords = 256;
    private static World retainedWorld;
    private static readonly Dictionary<Guid,Retained> records=new Dictionary<Guid,Retained>();

    internal static bool Retain(World world,EntityPlayer player,NetPackageRebirthMetabolismConsumeResult packet)
    {
        int owner;Guid id;string creation;
        if(!Authority(world,player,out creation)||packet==null||!packet.TryGetPublicationIdentity(out owner,out id)
            ||owner!=player.entityId)return false;
        // Reset is an explicit integration decision. Never silently drop unresolved records.
        if(retainedWorld!=null&&!ReferenceEquals(retainedWorld,world))return false;
        byte[] wire=Write(packet);
        Retained existing;
        if(records.TryGetValue(id,out existing))
            return Current(world,player,existing)&&existing.Wire.SequenceEqual(wire);
        if(records.Count>=MaximumRecords)return false;
        retainedWorld=world;
        records.Add(id,new Retained{Player=player,Creation=creation,Wire=(byte[])wire.Clone()});
        return true;
    }

    internal static bool TryGetRetry(World world,EntityPlayer player,Guid id,out NetPackageRebirthMetabolismConsumeResult packet)
    {
        packet=null;Retained record;
        if(!ThreadManager.IsMainThread()||!records.TryGetValue(id,out record)||!Current(world,player,record)||record.Acknowledged)return false;
        packet=Read(record.Wire);
        record.Attempts++;
        return true;
    }

    // Caller must first pass native NetPackage.ValidEntityIdForSender for owner.
    // This confirms publication delivery only, never authorizes benefits/refunds.
    internal static bool AcknowledgeAuthenticated(World world,EntityPlayer player,Guid id,string creation)
    {
        if(!ThreadManager.IsMainThread())return false;
        if(DurableAcknowledgement!=null)return DurableAcknowledgement(world,player,id,creation);
        Retained record;
        if(!ThreadManager.IsMainThread()||!records.TryGetValue(id,out record)||!Current(world,player,record)
            ||!string.Equals(record.Creation,creation,StringComparison.Ordinal))return false;
        record.Acknowledged=true; // duplicate valid ACK is idempotently true
        return true;
    }

    // Owner-side caller sends ACK only after this succeeds. An existing applied GUID
    // acknowledges without a setter, even if the owner subsequently moved the source.
    internal static bool ApplyForAcknowledgement(World world,NetPackageRebirthMetabolismConsumeResult packet,string creation)
    {
        int owner;Guid id;
        var player=world?.GetPrimaryPlayer();
        if(!ThreadManager.IsMainThread()||world==null||!world.IsRemote()||player==null||packet==null
            ||!packet.TryGetPublicationIdentity(out owner,out id)||player.entityId!=owner
            ||!RebirthSurvivorRequestScope.Matches(creation,RebirthSurvivorClientState.GetProjectedCreationId(player)))return false;
        return packet.ApplyPublicationForAcknowledgement(world);
    }

    internal static JObject Observe()
    {
        if(!ThreadManager.IsMainThread())throw new InvalidOperationException("Main-thread observation required.");
        var rows=new JArray();
        foreach(var pair in records)
            rows.Add(new JObject{["publication"]=pair.Key.ToString("N"),["owner"]=pair.Value.Player.entityId,
                ["creationId"]=pair.Value.Creation,["acknowledged"]=pair.Value.Acknowledged,
                ["retryMaterializations"]=pair.Value.Attempts,["wireBytes"]=pair.Value.Wire.Length});
        return new JObject{["schema"]=1,["activeIntegration"]=false,["acknowledgementProtocolEnabled"]=AcknowledgementProtocolEnabled,["durable"]=false,["capacity"]=MaximumRecords,["records"]=rows};
    }

    // Refuses to discard unacknowledged replies. Teardown/recovery policy is unresolved.
    internal static bool ClearAcknowledgedWorld(World world)
    {
        if(!ThreadManager.IsMainThread()||!ReferenceEquals(world,retainedWorld)||records.Values.Any(r=>!r.Acknowledged))return false;
        records.Clear();retainedWorld=null;return true;
    }
    private static bool Authority(World world,EntityPlayer player,out string creation)
    {
        creation=null;RebirthWorldCharacterRecord character;
        if(!ThreadManager.IsMainThread()||world==null||world.IsRemote()||player==null||!ReferenceEquals(player.world,world)
            ||!ReferenceEquals(world.GetEntity(player.entityId),player)||!RebirthWorldCharacterService.TryGet(player,out character)
            ||character==null||!character.IsComplete)return false;
        creation=character.Origin?.CreationId;
        return !string.IsNullOrEmpty(creation);
    }
    private static bool Current(World world,EntityPlayer player,Retained record)
    {
        string creation;
        return ReferenceEquals(retainedWorld,world)&&ReferenceEquals(record.Player,player)&&Authority(world,player,out creation)
            &&string.Equals(record.Creation,creation,StringComparison.Ordinal);
    }
    internal static byte[] Write(NetPackageRebirthMetabolismConsumeResult packet)
    {
        using(var stream=new MemoryStream())
        using(var writer=MemoryPools.poolBinaryWriter.AllocSync(true))
        {
            Encoding previous=writer.Encoding;
            try{writer.Encoding=new UTF8Encoding(false,false);writer.SetBaseStream(stream);packet.write(writer);writer.Flush();return stream.ToArray();}
            finally{writer.Encoding=previous;}
        }
    }
    internal static NetPackageRebirthMetabolismConsumeResult Read(byte[] wire)
    {
        using(var stream=new MemoryStream(wire,false))
        using(var reader=MemoryPools.poolBinaryReader.AllocSync(true))
        {
            Encoding previous=reader.Encoding;
            try
            {
                reader.Encoding=new UTF8Encoding(false,false);reader.SetBaseStream(stream);
                ((BinaryReader)reader).ReadUInt16();
                var packet=new NetPackageRebirthMetabolismConsumeResult();packet.read(reader);
                if(stream.Position!=stream.Length)throw new InvalidDataException("Trailing retained consumption reply.");
                return packet;
            }
            finally{reader.Encoding=previous;}
        }
    }
}
