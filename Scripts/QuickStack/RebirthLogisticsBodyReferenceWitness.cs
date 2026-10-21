using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;

// Detached evidence codec ONLY. No setters, identity lookup, persistence or
// invocation permission. Caller must separately prove Prepared0, native mapping,
// retained original/fatal and durable monotone invocation before using a witness.
internal static class RebirthLogisticsBodyReferenceWitness
{
    internal sealed class Witness
    {
        internal string Original, Projected, Nonce, Binding;
        internal int HistoricalOwner, NativeOwner;
    }
    // Readonly routing prerequisite, never a death/payment/invocation proof.
    // Both native indexes and the same actual persistent row must agree.
    internal static bool TryCurrentNativeOwner(EntityPlayer actor, ClientInfo sender, string owner, out int entity)
    {
        entity=-1;
        try
        {
            var world=GameManager.Instance?.World;
            if(!ThreadManager.IsMainThread()||world?.IsRemote()!=false||actor==null||sender?.InternalId==null||
                !ReferenceEquals(actor.world,world)||!ReferenceEquals(world.GetEntity(sender.entityId),actor)||actor.entityId!=sender.entityId||
                !ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance?.Clients.ForEntityId(sender.entityId),sender)||
                !RebirthStablePlayerIdentity.TryFromClientInfo(sender,out var identity)||identity.StorageKey!=owner)return false;
            var list=GameManager.Instance.GetPersistentPlayerList();
            var row=list?.GetPlayerData(sender.InternalId);
            if(row?.PrimaryId==null||row.EntityId!=sender.entityId||row.PrimaryId.CombinedString!=sender.InternalId.CombinedString||
                !ReferenceEquals(list.GetPlayerDataFromEntityID(sender.entityId),row)||
                !list.PlayerToEntityMap.TryGetValue(row.PrimaryId,out int mapped)||mapped!=sender.entityId||
                !RebirthStablePlayerIdentity.TryFromPersistentPlayer(row,out var persistent)||persistent.StorageKey!=owner)return false;
            // Native SpawnEntityServer scans Players.Values by EntityId, so a
            // duplicate stale mapping could associate the body with another row.
            if(list.Players.Values.Count(value=>value.EntityId==sender.entityId)!=1)return false;
            entity=sender.entityId;return true;
        }
        catch{return false;}
    }
    private const int MaximumNativeBytes = 262144;
    private static bool CanonicalNonce(string text)
        => Guid.TryParseExact(text, "N", out var id) && id != Guid.Empty && id.ToString("N") == text;
    private static void PreflightNative(byte[] bytes)
    {
        // Installed ECD v38 Backpack construction has no EntityAlive stats or
        // trader payload. Reject other shapes BEFORE native variable allocations.
        using(var stream=new MemoryStream(bytes,false))
        using(var reader=new BinaryReader(stream))
        {
            if(reader.ReadByte()!=38||!EntityClass.list.TryGetValue(reader.ReadInt32(),out var definition)||definition.classname!=typeof(EntityBackpack)||reader.ReadInt32()!=-1)throw new InvalidDataException();
            Skip(stream,28);Bool(reader);
            if(reader.ReadInt32()!=4)throw new InvalidDataException();Skip(stream,8);
            if(Bool(reader))throw new InvalidDataException();Skip(stream,2);
            if(!Bool(reader))throw new InvalidDataException();int bag=reader.ReadInt32();if(bag<1||bag>131072)throw new InvalidDataException();Skip(stream,bag);
            Skip(stream,15);int body=reader.ReadUInt16();if(body<4)throw new InvalidDataException();Skip(stream,body);
            if(Bool(reader))throw new InvalidDataException();
            // Installed native StreamModeWrite/Read.Persistency are both0;
            // network-only sleeper/name fields are absent in this canonical lane.
            Skip(stream,28);if(stream.Position!=stream.Length)throw new InvalidDataException();
        }
    }
    private static bool Bool(BinaryReader reader)
    {byte value=reader.ReadByte();if(value>1)throw new InvalidDataException();return value==1;}
    private static void Skip(Stream stream,int count)
    {if(count<0||count>stream.Length-stream.Position)throw new InvalidDataException();stream.Position+=count;}
    private static EntityCreationData Decode(string text)
    {
        if (text == null || text.Length < 1 || text.Length > 349528) throw new InvalidDataException();
        byte[] bytes = Convert.FromBase64String(text);
        if (bytes.Length > MaximumNativeBytes || Convert.ToBase64String(bytes) != text) throw new InvalidDataException();
        PreflightNative(bytes);
        var value = new EntityCreationData();
        try
        {
            using (var stream = new MemoryStream(bytes, false))
            using (var reader = MemoryPools.poolBinaryReader.AllocSync(true))
            {
                reader.SetBaseStream(stream); value.read(reader, StreamModeRead.Persistency);
                if (stream.Position != stream.Length || !RebirthLogisticsNativeInvocationJournal.Encode(value).SequenceEqual(bytes)) throw new InvalidDataException();
            }
            return value;
        }
        catch
        {
            // Native read may allocate detached streams before rejecting a later
            // field. They never escape on failure and must not retain the budget.
            value.entityData?.Dispose(); value.bagData?.Dispose();
            throw;
        }
    }
    private static int RefOffset(EntityCreationData value, int expected)
    {
        if (value.id != -1 || value.entityData == null || value.bagData == null || value.entityData.Length > ushort.MaxValue ||
            value.bagData.Length < 1 || value.bagData.Length > 131072 ||
            !EntityClass.list.TryGetValue(value.entityClass, out var definition) || definition.classname != typeof(EntityBackpack)) throw new InvalidDataException();
        byte[] body = value.entityData.ToArray(), bag = value.bagData.ToArray();
        int offset = body.Length - bag.Length - 4;
        if (offset < 0 || BitConverter.ToInt32(body, offset) != expected) throw new InvalidDataException();
        for (int i = 0; i < bag.Length; i++) if (body[offset + 4 + i] != bag[i]) throw new InvalidDataException();
        foreach (float v in new[] { value.pos.x,value.pos.y,value.pos.z,value.rot.x,value.rot.y,value.rot.z })
            if (float.IsNaN(v) || float.IsInfinity(v)) throw new InvalidDataException();
        return offset;
    }
    internal static Witness Create(string originalCanonical, int historicalOwner, int nativeOwner, string nonce, string binding)
    {
        if (historicalOwner < 0 || nativeOwner < 0 || historicalOwner == nativeOwner || !CanonicalNonce(nonce) ||
            binding == null || binding.Length != 64 || binding.Any(c => !(c >= '0' && c <= '9' || c >= 'a' && c <= 'f'))) throw new InvalidDataException();
        var detached = Decode(originalCanonical);
        try
        {
        int offset = RefOffset(detached, historicalOwner);
        // Never use EntityCreationData's shallow copy constructor: its streams alias.
        byte[] body = detached.entityData.ToArray();
        byte[] replacement = BitConverter.GetBytes(nativeOwner);
        Buffer.BlockCopy(replacement, 0, body, offset, 4);
        detached.entityData.Dispose(); detached.entityData = new MemoryStream(body);
        RefOffset(detached, nativeOwner);
        return new Witness { Original=originalCanonical, Projected=Convert.ToBase64String(RebirthLogisticsNativeInvocationJournal.Encode(detached)),
            HistoricalOwner=historicalOwner, NativeOwner=nativeOwner, Nonce=nonce, Binding=binding };
        }
        finally { detached.entityData?.Dispose(); detached.bagData?.Dispose(); }
    }
    internal static bool MatchesOriginalFatal(Witness witness,RebirthLogisticsNativeDeathReceipt.Record fatal)
    {
        EntityCreationData native=null;
        try
        {
            if(witness==null||fatal==null||fatal.OwnerEntity!=witness.HistoricalOwner||fatal.Binding!=witness.Binding||
                (fatal.Physical?.Stage??0)<4||fatal.Physical.Requests!=1||fatal.Physical.BodyByteRefused)return false;
            native=Decode(witness.Original);RefOffset(native,witness.HistoricalOwner);
            return fatal.Physical.BodyClass==native.entityClass&&fatal.Physical.BodyPosition.Equals(native.pos)&&
                fatal.Physical.BodyBagHash==RebirthLogisticsPhysicalLossWitness.BlobHash(native.bagData)&&
                fatal.Physical.BodyEntityHash==RebirthLogisticsPhysicalLossWitness.BlobHash(native.entityData);
        }
        catch{return false;}
        finally{native?.entityData?.Dispose();native?.bagData?.Dispose();}
    }
    internal static EntityCreationData DecodeProjected(Witness witness)
    {
        var expected=Create(witness.Original,witness.HistoricalOwner,witness.NativeOwner,witness.Nonce,witness.Binding);
        if(expected.Projected!=witness.Projected)throw new InvalidDataException();
        return Decode(witness.Projected);
    }
    internal static string Fingerprint(Witness witness)
        =>RebirthLogisticsOriginalTransport.Digest(Write(witness).ToString(SaveOptions.DisableFormatting));
    internal static bool MatchesProjectedBody(Witness witness,RebirthLogisticsNativeDeathReceipt.Record fatal,int nativeOwner,string bagHash,string entityHash)
    {
        EntityCreationData native=null;
        try
        {
            if(nativeOwner!=witness.NativeOwner||!MatchesOriginalFatal(witness,fatal))return false;
            native=Decode(witness.Projected);RefOffset(native,nativeOwner);
            return bagHash==RebirthLogisticsPhysicalLossWitness.BlobHash(native.bagData)&&entityHash==RebirthLogisticsPhysicalLossWitness.BlobHash(native.entityData);
        }
        catch{return false;}
        finally{native?.entityData?.Dispose();native?.bagData?.Dispose();}
    }
    internal static XElement Write(Witness value)
    {
        if (value == null) throw new InvalidDataException();
        var expected = Create(value.Original,value.HistoricalOwner,value.NativeOwner,value.Nonce,value.Binding);
        if (expected.Projected != value.Projected) throw new InvalidDataException();
        return new XElement("bodyReference",new XAttribute("version",1),new XAttribute("historicalOwner",value.HistoricalOwner),
            new XAttribute("nativeOwner",value.NativeOwner),new XAttribute("nonce",value.Nonce),new XAttribute("binding",value.Binding),
            new XElement("original",value.Original),new XElement("projected",value.Projected));
    }
    internal static Witness Read(XElement root)
    {
        string[] names={"version","historicalOwner","nativeOwner","nonce","binding"};
        if (root == null || root.Name != "bodyReference" || root.Attributes().Count()!=5 || root.Attributes().Any(a=>a.Name.Namespace!=XNamespace.None||!names.Contains(a.Name.LocalName)) ||
            (string)root.Attribute("version")!="1" || root.Elements().Count()!=2 || root.Element("original")==null || root.Element("projected")==null ||
            root.Elements().Any(e=>e.HasAttributes||e.HasElements) || root.Nodes().Any(n=>!(n is XElement))) throw new InvalidDataException();
        var value=Create(root.Element("original").Value,(int)root.Attribute("historicalOwner"),(int)root.Attribute("nativeOwner"),(string)root.Attribute("nonce"),(string)root.Attribute("binding"));
        if (root.Element("projected").Value!=value.Projected || !XNode.DeepEquals(root,Write(value))) throw new InvalidDataException();
        return value;
    }
}
