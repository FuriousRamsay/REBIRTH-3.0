using System;
using System.IO;
using System.Linq;

// Observes native equipment bytes only. Custody owns item movement and receipts.
internal static class RebirthVehicleCursorGearWitness
{
    private const int MaximumBytes=4*1024*1024;
    internal static bool TryCapture(XUiController target,ItemStack afterTarget,out IRebirthVehicleCursorDestinationWitness witness)
    {
        witness=null;
        var slot=target as XUiC_EquipmentStack;
        var owner=target?.xui?.playerUI?.entityPlayer;
        if(slot==null||afterTarget?.itemValue==null||afterTarget.count!=1||afterTarget.IsEmpty()||
            !Scope(owner,out var identity,out var creation))return false;
        int index=(int)slot.EquipSlot;
        if(index<0||index>=13||slot.ItemStack?.itemValue==null||
            !string.Equals(RebirthNativeItemCodec.Encode(slot.ItemStack.itemValue),
                RebirthNativeItemCodec.Encode(owner.equipment.GetSlotItem(index)),StringComparison.Ordinal))return false;
        try
        {
            byte[] before=Live(owner);
            byte[] after=ReplaceSlot(before,(int)slot.EquipSlot,afterTarget);
            if(after==null||before.SequenceEqual(after))return false;
            witness=new Witness(owner,identity.StorageKey,creation,GameIO.GetSaveGameDir(),before,after);
            return true;
        }
        catch{return false;}
    }
    internal static bool TryRead(string payload,out IRebirthVehicleCursorDestinationWitness witness)
    {
        witness=null;
        try
        {
            if(string.IsNullOrEmpty(payload)||payload.Length>MaximumBytes*3)return false;
            var owner=GameManager.Instance?.World?.GetPrimaryPlayer();
            if(!Scope(owner,out var identity,out var creation))return false;
            using(var stream=new MemoryStream(Convert.FromBase64String(payload),false))
            using(var reader=new BinaryReader(stream))
            {
                if(reader.ReadByte()!=1)return false;
                string key=reader.ReadString(),scope=reader.ReadString(),directory=reader.ReadString(),worldId=reader.ReadString();
                if(key!=identity.StorageKey||scope!=creation||directory!=GameIO.GetSaveGameDir()||
                    worldId!=owner.world.worldState?.Guid||!Guid.TryParse(worldId,out _))return false;
                byte[] before=ReadBytes(reader),after=ReadBytes(reader);
                if(stream.Position!=stream.Length||before.SequenceEqual(after))return false;
                witness=new Witness(owner,key,scope,directory,before,after);return true;
            }
        }
        catch{return false;}
    }
    private static byte[] ReadBytes(BinaryReader reader)
    {int n=reader.ReadInt32();if(n<=0||n>MaximumBytes)throw new InvalidDataException();var b=reader.ReadBytes(n);if(b.Length!=n)throw new EndOfStreamException();return b;}
    private static bool Scope(EntityPlayerLocal owner,out RebirthStablePlayerIdentity identity,out string creation)
    {
        identity=null;creation=null;var world=GameManager.Instance?.World;
        if(!ThreadManager.IsMainThread()||owner==null||world==null||!ReferenceEquals(owner.world,world)||
            !ReferenceEquals(world.GetPrimaryPlayer(),owner)||!ReferenceEquals(world.GetEntity(owner.entityId),owner)||
            owner.IsDead()||!Guid.TryParse(world.worldState?.Guid,out _)||
            !RebirthStablePlayerIdentity.TryFromLocalPlatform(out identity))return false;
        if(!RebirthSurvivorMode.IsEnabledForCurrentWorld()){creation="";return true;}
        if(RebirthCharacterCreationHoldService.IsHeld(owner))return false;
        string value;
        if(world.IsRemote())value=RebirthSurvivorClientState.GetProjectedCreationId(owner);
        else
        {
            RebirthWorldCharacterRecord record;
            if(!RebirthWorldCharacterService.TryGet(owner,out record)||record==null||!record.IsComplete||
                !RebirthWorldCharacterRepository.IsCurrentCachedRecord(record))return false;
            value=record.Origin?.CreationId;
        }
        return RebirthSurvivorRequestScope.TryNormalize(value,out creation);
    }
    private static byte[] Live(EntityPlayerLocal owner)
    {
        using(var stream=new MemoryStream())
        using(var writer=MemoryPools.poolBinaryWriter.AllocSync(true))
        {writer.SetBaseStream(stream);owner.equipment.Write(writer,StreamModeWrite.Persistency);writer.Flush();if(stream.Length>MaximumBytes)throw new InvalidDataException();return stream.ToArray();}
    }
    private static byte[] ReplaceSlot(byte[] original,int slot,ItemStack replacement)
    {
        using(var stream=new MemoryStream(original,false))
        using(var reader=MemoryPools.poolBinaryReader.AllocSync(true))
        {
            reader.SetBaseStream(stream);
            if(reader.ReadByte()!=6)return null;
            int version=reader.ReadUInt16();var size=StreamUtils.ReadVector2i(reader);int count=reader.ReadInt16();
            if(version<1||version>2||count!=13||size.x*size.y!=count||slot<0||slot>=count)return null;
            for(int i=0;i<count;i++)
            {
                int start=checked((int)stream.Position);int amount=reader.ReadUInt16();
                if(amount>0)ItemValue.ReadOrNull(reader);
                int end=checked((int)stream.Position);
                if(i!=slot)continue;
                byte[] item=Convert.FromBase64String(RebirthNativeItemCodec.Encode(replacement.itemValue));
                using(var result=new MemoryStream())
                using(var writer=new BinaryWriter(result))
                {writer.Write(original,0,start);writer.Write((ushort)replacement.count);writer.Write(item);writer.Write(original,end,original.Length-end);if(result.Length>MaximumBytes)return null;return result.ToArray();}
            }
            return null;
        }
    }
    private sealed class Witness:IRebirthVehicleCursorDestinationWitness
    {
        private readonly EntityPlayerLocal owner;private readonly World world;private readonly object equipment;
        private readonly string key,creation,directory,worldId;private readonly byte[] before,after;
        internal Witness(EntityPlayerLocal player,string ownerKey,string scope,string saveDirectory,byte[] prior,byte[] next)
        {owner=player;world=player.world;worldId=world.worldState.Guid;equipment=player.equipment;key=ownerKey;creation=scope;directory=saveDirectory;before=(byte[])prior.Clone();after=(byte[])next.Clone();}
        public string Encode()
        {using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream)){writer.Write((byte)1);writer.Write(key);writer.Write(creation);writer.Write(directory);writer.Write(worldId);writer.Write(before.Length);writer.Write(before);writer.Write(after.Length);writer.Write(after);return Convert.ToBase64String(stream.ToArray());}}
        public bool MatchesLive(EntityPlayerLocal player)
        {try{return ReferenceEquals(owner,player)&&ReferenceEquals(world,player.world)&&ReferenceEquals(equipment,player.equipment)&&
            Scope(player,out var identity,out var scope)&&identity.StorageKey==key&&scope==creation&&world.worldState?.Guid==worldId&&GameIO.GetSaveGameDir()==directory&&Live(player).SequenceEqual(after);}catch{return false;}}
        public bool RequestAndVerifySaved(EntityPlayerLocal player,Guid operation)
        {
            if(operation==Guid.Empty||!MatchesLive(player)||!RebirthStablePlayerIdentity.TryFromLocalPlatform(out var identity))return false;
            if(!RebirthGearNativePlayerFile.TryRead(identity,out var saved)||
                !RebirthVehicleClientCustody.HasSavedHandoff(player,operation,saved))return false;
            return saved.equipmentData!=null&&saved.equipmentData.ToArray().SequenceEqual(after)&&MatchesLive(player);
        }
    }
}
