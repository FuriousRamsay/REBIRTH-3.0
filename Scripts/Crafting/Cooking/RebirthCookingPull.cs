using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Platform;
using UnityEngine;
using UnityEngine.Scripting;

#nullable disable

public static class RebirthCookingPull
{
    internal const int MaximumIngredientRequests = 12;

    internal static List<ItemStack> ReadIngredientRequests(PooledBinaryReader reader)
    {
        int count = reader.ReadUInt16();
        if (count > MaximumIngredientRequests)
            throw new InvalidDataException("Cooking ingredient request exceeds its slot limit.");
        var result = new List<ItemStack>(count);
        for (int index = 0; index < count; index++)
        {
            var stack = new ItemStack();
            stack.Read(reader);
            result.Add(ItemClass.GetForId(stack.itemValue.type) == null ? ItemStack.Empty : stack);
        }
        return result;
    }

    // A restarted client must not reuse a request id still held by the server journal.
    private static long sequence = DateTime.UtcNow.Ticks;
    private static readonly Dictionary<long, Action<List<ItemStack>,string>> waiting = new Dictionary<long, Action<List<ItemStack>,string>>();
    public static bool Request(EntityPlayerLocal player,List<ItemStack> needs,Action<List<ItemStack>,string> callback)
    {
        var game = GameManager.Instance;
        var connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (player == null || callback == null || game == null || player.world == null ||
            !player.world.IsRemote() || !ReferenceEquals(game.World, player.world) ||
            connection == null || connection.IsServer || !connection.IsConnected ||
            needs == null || needs.Count == 0 || needs.Count > MaximumIngredientRequests) return false;
        var persistent = game.GetPersistentPlayerList()?.GetPlayerDataFromEntityID(player.entityId);
        if (persistent?.PrimaryId == null) return false;
        var packet = NetPackageManager.GetPackage<NetPackageRebirthCookingPull>();
        var channels = connection.GetConnectionToServer();
        int channel = packet.Channel;
        if (channels == null || channel < 0 || channel >= channels.Length ||
            channels[channel] == null || channels[channel].IsDisconnected()) return false;
        long request = System.Threading.Interlocked.Increment(ref sequence);
        packet.Setup(player.entityId, persistent.PrimaryId, request, needs);
        waiting[request] = callback;
        // Once queueing starts, preserve the callback: an exception does not prove
        // the server failed to receive the withdrawal.
        connection.SendToServer(packet);
        return true;
    }
    public static void Receive(long id,List<ItemStack> items,string reason)
    {
        if(waiting.TryGetValue(id,out var callback)){waiting.Remove(id);callback(items,reason);}
    }
}
[Preserve]
public sealed class NetPackageRebirthCookingPull : NetPackage
{
    private int playerId;private PlatformUserIdentifierAbs user;private long id;private List<ItemStack> needs;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToServer;
    public NetPackageRebirthCookingPull Setup(int player,PlatformUserIdentifierAbs uid,long request,List<ItemStack> items){playerId=player;user=uid;id=request;needs=items;return this;}
    public override void read(PooledBinaryReader r){playerId=r.ReadInt32();user=PlatformUserIdentifierAbs.FromStream(r);id=r.ReadInt64();needs=RebirthCookingPull.ReadIngredientRequests(r);}
    public override void write(PooledBinaryWriter w){base.write(w);((BinaryWriter)w).Write(playerId);user.ToStream(w);((BinaryWriter)w).Write(id);GameUtils.WriteItemStack(w,needs);}
    public override void ProcessPackage(World world,GameManager callbacks)
    {
        if(world==null||world.IsRemote()||id<=0||!ValidEntityIdForSender(playerId)||!ValidUserIdForSender(user))return;
        var player=world.GetEntity(playerId) as EntityPlayer;
        var removed=new List<ItemStack>();string error="Ingredients unavailable.";
        if(player!=null&&needs!=null&&needs.Count>0&&needs.Count<=RebirthCookingPull.MaximumIngredientRequests&&needs.All(s=>s!=null&&!s.IsEmpty()&&s.count>0&&s.count<=10000&&RebirthCookingCatalogue.IsIngredient(s)))
        {
            var outcome=RemoteResourceTransactionOutcomeJournal.Resolve(
                world,player,user,1,(ulong)id,RemoteResourceClientOperation.CookingPull,needs,false);
            // An in-flight duplicate must not complete the client callback before
            // the original request publishes its authoritative receipt.
            if(outcome.State==RemoteResourceTransactionOutcomeState.Unknown)return;
            removed=outcome.Removed;
            error=outcome.State==RemoteResourceTransactionOutcomeState.Success?"":outcome.Reason;
        }
        SingletonMonoBehaviour<ConnectionManager>.Instance.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthCookingPulled>().Setup(id,removed,error),_attachedToEntityId:playerId);
    }
    public int GetLength()=>0;
}
[Preserve]
public sealed class NetPackageRebirthCookingPulled : NetPackage
{
    private long id;private List<ItemStack> items;private string error;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToClient;
    public NetPackageRebirthCookingPulled Setup(long request,List<ItemStack> stacks,string reason){id=request;items=stacks;error=reason??"";return this;}
    public override void read(PooledBinaryReader r){id=r.ReadInt64();error=r.ReadString();items=new List<ItemStack>(GameUtils.ReadItemStack(r));}
    public override void write(PooledBinaryWriter w){base.write(w);((BinaryWriter)w).Write(id);((BinaryWriter)w).Write(error);GameUtils.WriteItemStack(w,items);}
    public override void ProcessPackage(World world,GameManager callbacks){if(world!=null&&world.IsRemote())RebirthCookingPull.Receive(id,items,error);}
    public int GetLength()=>0;
}
