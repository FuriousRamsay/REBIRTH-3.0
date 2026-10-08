using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using UnityEngine;

// Read-only post-save observation. A receipt grants neither admission nor completion.
internal static class RebirthTheorySoloNativeUploadReceipt
{
    internal sealed class Receipt
    {
        internal World World;internal object State;internal ConnectionManager Manager;internal ClientInfo Sender;
        internal EntityPlayer Player;internal string Creation,Owner,InventoryHash,QueueHash;internal float Seen;
        internal Guid Id=Guid.NewGuid();internal Guid ClaimedRequest;
    }
    private static World activeWorld;
    private static readonly Dictionary<ClientInfo,Receipt> Latest=new Dictionary<ClientInfo,Receipt>();
    private static readonly Dictionary<ClientInfo,Receipt> Armed=new Dictionary<ClientInfo,Receipt>();
    internal static void Reset(){Latest.Clear();Armed.Clear();activeWorld=null;}
    internal static bool Arm(ClientInfo sender,EntityPlayer player,string creation,Guid request)
    {
        if(sender==null||player==null||request==Guid.Empty||!RebirthStablePlayerIdentity.TryResolveServerEntity(player,out var owner)||owner==null)return false;
        var world=GameManager.Instance?.World;
        if(!ReferenceEquals(activeWorld,world)){Reset();activeWorld=world;}
        if(Armed.TryGetValue(sender,out var previous)&&Current(previous)&&previous.ClaimedRequest!=request)return false;
        if(Armed.Count>=128&&!Armed.ContainsKey(sender))
        {
            var expired=new List<ClientInfo>();foreach(var pair in Armed)if(!Current(pair.Value))expired.Add(pair.Key);
            foreach(var key in expired)Armed.Remove(key);if(Armed.Count>=128)return false;
        }
        var intent=new Receipt{World=world,State=world?.worldState,Manager=SingletonMonoBehaviour<ConnectionManager>.Instance,Sender=sender,Player=player,Creation=creation,Owner=owner.CanonicalId,Seen=Time.realtimeSinceStartup,ClaimedRequest=request};
        if(!Current(intent))return false;Armed[sender]=intent;return true;
    }
    internal static void OnSaved(ref ModEvents.SSavePlayerDataData data)
    {
        try
        {
            var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;var world=GameManager.Instance?.World;
            if(!ReferenceEquals(activeWorld,world)){Reset();activeWorld=world;}
            var sender=data.ClientInfo;var native=data.PlayerDataFile;
            if(sender==null||!Armed.TryGetValue(sender,out var intent)||!Current(intent))return;
            if(!ThreadManager.IsMainThread()||world==null||world.IsRemote()||manager==null||manager.Clients==null||!manager.IsServer||sender==null||native==null||sender.disconnecting||!sender.loginDone||!sender.bAttachedToEntity||sender.entityId!=native.id||!ReferenceEquals(manager.Clients.ForClientNumber(sender.ClientNumber),sender))return;
            var player=world.GetEntity(sender.entityId) as EntityPlayer;
            if(player==null||!ReferenceEquals(player.world,world)||!RebirthSurvivorMode.IsEnabledForCurrentWorld()||!RebirthStablePlayerIdentity.TryResolveServerEntity(player,out var owner)||owner==null||sender.InternalId?.CombinedString!=owner.CanonicalId||!RebirthWorldCharacterRepository.TryGetCurrentCached(owner,out var record)||record.Origin==null)return;
            string creation=record.Origin.CreationId;
            if(!RebirthSurvivorRequestScope.TryNormalize(creation,out var normalized)||normalized!=creation||!TryImages(native,out var inventory,out var queue))return;
            var receipt=new Receipt{World=world,State=world.worldState,Manager=manager,Sender=sender,Player=player,Creation=creation,Owner=owner.CanonicalId,InventoryHash=inventory,QueueHash=queue,Seen=Time.realtimeSinceStartup,ClaimedRequest=intent.ClaimedRequest};
            if(receipt.Creation!=intent.Creation||!ReferenceEquals(receipt.Player,intent.Player)||!Current(receipt)||!RebirthGearNativePlayerFile.TryRead(owner,out var saved)||!TryImages(saved,out var finalInventory,out var finalQueue)||inventory!=finalInventory||queue!=finalQueue||!Current(receipt))return;
            if(Latest.Count>=128&&!Latest.ContainsKey(sender))
            {
                var expired=new List<ClientInfo>();foreach(var pair in Latest)if(!Current(pair.Value))expired.Add(pair.Key);
                foreach(var key in expired)Latest.Remove(key);if(Latest.Count>=128)return;
            }
            Latest[sender]=receipt;
            RebirthTheorySoloOriginalAdmission.Observe(receipt,native);
        }
        catch { } // Unknown/missing native save produces no usable receipt.
    }
    internal static bool TryClaim(ClientInfo sender,EntityPlayer player,string creation,string inventoryHash,Guid request,out Receipt receipt)
    {
        receipt=null;if(request==Guid.Empty||sender==null||!Latest.TryGetValue(sender,out var original)||!Current(original)||!ReferenceEquals(original.Player,player)||original.Creation!=creation||original.InventoryHash!=inventoryHash||original.ClaimedRequest!=Guid.Empty&&original.ClaimedRequest!=request)return false;
        original.ClaimedRequest=request;receipt=original;return true;
    }
    internal static bool Current(Receipt r)
    {
        float now=Time.realtimeSinceStartup;
        if(r==null||r.State==null||r.Player==null||float.IsNaN(r.Seen)||float.IsInfinity(r.Seen)||float.IsNaN(now)||float.IsInfinity(now)||!ThreadManager.IsMainThread()||Time.realtimeSinceStartup<r.Seen||Time.realtimeSinceStartup-r.Seen>15f||!ReferenceEquals(GameManager.Instance?.World,r.World)||r.World==null||r.World.IsRemote()||!ReferenceEquals(r.World.worldState,r.State)||!ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance,r.Manager)||r.Manager==null||r.Manager.Clients==null||!r.Manager.IsServer||r.Sender==null||r.Sender.disconnecting||!r.Sender.loginDone||!r.Sender.bAttachedToEntity||!ReferenceEquals(r.Manager.Clients.ForClientNumber(r.Sender.ClientNumber),r.Sender)||r.Sender.entityId!=r.Player.entityId||!ReferenceEquals(r.World.GetEntity(r.Player.entityId),r.Player)||!ReferenceEquals(r.Player.world,r.World)||r.Sender.InternalId?.CombinedString!=r.Owner||!RebirthSurvivorMode.IsEnabledForCurrentWorld()||!RebirthStablePlayerIdentity.TryResolveServerEntity(r.Player,out var identity)||identity==null||identity.CanonicalId!=r.Owner||!RebirthWorldCharacterRepository.TryGetCurrentCached(identity,out var record)||record.Origin?.CreationId!=r.Creation)return false;
        return RebirthWorldCharacterRepository.IsCurrentCachedRecord(record);
    }
    internal static bool MatchesNative(Receipt receipt,PlayerDataFile file)
        =>Current(receipt)&&TryImages(file,out var inventory,out var queue)&&inventory==receipt.InventoryHash&&queue==receipt.QueueHash;
    private static bool TryImages(PlayerDataFile file,out string inventory,out string queue)
    {
        inventory=null;queue=null;
        if(file==null||!RebirthTheorySoloNativePaymentPlan.TryHash(RebirthPlayerDataInventory.ReadSlots(file,true),RebirthPlayerDataInventory.ReadSlots(file,false),out inventory)||file.craftingData?.RecipeQueueItems?.Length>128)return false;
        using(var stream=new MemoryStream())using(var writer=MemoryPools.poolBinaryWriter.AllocSync(true))
        {
            writer.SetBaseStream(stream);writer.Write(file.craftingData!=null);file.craftingData?.Write(writer);writer.Flush();
            if(stream.Length>1024*1024)return false;
            using(var hash=SHA256.Create())queue=BitConverter.ToString(hash.ComputeHash(stream.ToArray())).Replace("-","").ToLowerInvariant();
        }
        return true;
    }
}