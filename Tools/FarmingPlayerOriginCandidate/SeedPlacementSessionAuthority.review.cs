using System;
using System.Collections.Generic;
using System.Security.Cryptography;

// TOOLS REVIEW ONLY. No debit, placement, recovery or bonus authority.
internal static class SeedPlacementSessionAuthority
{
    private const int MaximumSessions=256;
    private static readonly object Gate=new object();
    private static readonly List<SessionEntry> Sessions=new List<SessionEntry>();
    private sealed class SessionEntry
    {
        internal World World; internal ClientInfo Sender; internal EntityPlayer Actor;
        internal ConnectionManager Connection; internal GameManager Manager;
        internal ulong Epoch,LastNonce; internal bool Revoked;
    }
    internal sealed class Reservation
    {
        private readonly SessionEntry entry;
        internal readonly ulong Epoch,Nonce;
        internal readonly EntityPlayer Actor;
        internal Reservation(object privateEntry,ulong nonce)
        {entry=privateEntry as SessionEntry; if(entry==null)throw new ArgumentException("Private session binding required.");Epoch=entry.Epoch;Nonce=nonce;Actor=entry.Actor;}
        internal bool BoundCurrent() => Current(entry)&&Epoch==entry.Epoch&&Nonce!=0&&Nonce<=entry.LastNonce&&ReferenceEquals(Actor,entry.Actor);
        internal bool BoundTo(World world,EntityPlayer actor) => BoundCurrent()&&ReferenceEquals(entry.World,world)&&ReferenceEquals(entry.Actor,actor);
        internal bool BoundToSender(ClientInfo sender,World world,EntityPlayer actor) => BoundTo(world,actor)&&ReferenceEquals(entry.Sender,sender);
    }
    private static bool TryContext(ClientInfo sender,World world,out EntityPlayer actor,
        out ConnectionManager connection,out GameManager manager)
    {
        actor=null;connection=SingletonMonoBehaviour<ConnectionManager>.Instance;manager=GameManager.Instance;
        if(sender==null||world==null||connection==null||!connection.IsServer||manager==null||
            !ReferenceEquals(manager.World,world)||world.IsRemote()||connection.Clients==null||
            !ReferenceEquals(connection.Clients.ForClientNumber(sender.ClientNumber),sender)||
            !sender.loginDone||!sender.bAttachedToEntity||sender.disconnecting||sender.entityId<=0)return false;
        actor=world.GetEntity(sender.entityId) as EntityPlayer;
        if(actor==null||actor.IsDead()||!ReferenceEquals(actor.world,world))return false;
        // Recheck reference ownership after native getters. No client-supplied bool grants authority.
        return ReferenceEquals(GameManager.Instance,manager)&&ReferenceEquals(manager.World,world)&&
            ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance,connection)&&connection.IsServer&&
            ReferenceEquals(connection.Clients.ForClientNumber(sender.ClientNumber),sender)&&
            sender.loginDone&&sender.bAttachedToEntity&&!sender.disconnecting&&sender.entityId==actor.entityId&&
            ReferenceEquals(world.GetEntity(sender.entityId),actor)&&ReferenceEquals(actor.world,world)&&!world.IsRemote();
    }
    private static bool Current(SessionEntry entry)
    {
        if(entry==null||entry.Revoked||!Sessions.Contains(entry))return false;
        bool current=TryContext(entry.Sender,entry.World,out var actor,out var connection,out var manager)&&
            ReferenceEquals(actor,entry.Actor)&&ReferenceEquals(connection,entry.Connection)&&ReferenceEquals(manager,entry.Manager);
        // Once replacement/disconnection is observed, restoring old references cannot
        // resurrect this epoch. Retain its nonce memory until explicit revoke/clear.
        if(!current)entry.Revoked=true;
        return current;
    }
    internal static bool TryIssue(ClientInfo sender,World world,out ulong epoch)
    {
        epoch=0;
        lock(Gate)
        {
            if(!TryContext(sender,world,out var actor,out var connection,out var manager))return false;
            foreach(var prior in Sessions)
                if(ReferenceEquals(prior.Sender,sender))
                {
                    if(!Current(prior))return false; // explicit revoke required; never reset replay memory
                    epoch=prior.Epoch;return true;
                }
            if(Sessions.Count>=MaximumSessions)return false;
            ulong value=0;var bytes=new byte[8];
            using(var random=RandomNumberGenerator.Create())
            {
                for(int attempt=0;attempt<16&&value==0;attempt++)
                {
                    random.GetBytes(bytes);value=BitConverter.ToUInt64(bytes,0);
                    foreach(var active in Sessions)if(active.Epoch==value){value=0;break;}
                }
            }
            if(value==0||!TryContext(sender,world,out var currentActor,out var currentConnection,out var currentManager)||
                !ReferenceEquals(actor,currentActor)||!ReferenceEquals(connection,currentConnection)||!ReferenceEquals(manager,currentManager))return false;
            Sessions.Add(new SessionEntry{World=world,Sender=sender,Actor=actor,Connection=connection,Manager=manager,Epoch=value});
            epoch=value;return true;
        }
    }
    internal static bool TryReserve(ClientInfo sender,World world,ulong epoch,ulong nonce,out Reservation reservation)
    {
        reservation=null;if(epoch==0||nonce==0)return false;
        lock(Gate)
        {
            foreach(var entry in Sessions)
                if(ReferenceEquals(entry.Sender,sender)&&ReferenceEquals(entry.World,world)&&entry.Epoch==epoch)
                {
                    if(!Current(entry)||nonce<=entry.LastNonce)return false;
                    // Ordered-channel contract: gaps allowed, lower/out-of-order rejected.
                    // Reserve BEFORE execution; failed/unknown execution never releases nonce.
                    entry.LastNonce=nonce;reservation=new Reservation(entry,nonce);return true;
                }
            return false;
        }
    }
    internal static bool IsCurrent(Reservation reservation)
    {
        if(reservation==null)return false;
        lock(Gate)return reservation.BoundCurrent();
    }
    internal static bool IsBoundTo(Reservation reservation,World world,EntityPlayer actor)
    { if(reservation==null)return false;lock(Gate)return reservation.BoundTo(world,actor); }
    internal static bool IsBoundToSender(Reservation reservation,ClientInfo sender,World world,EntityPlayer actor)
    { if(reservation==null)return false;lock(Gate)return reservation.BoundToSender(sender,world,actor); }
    internal static void Revoke(ClientInfo sender)
    {
        lock(Gate)for(int i=Sessions.Count-1;i>=0;i--)
            if(ReferenceEquals(Sessions[i].Sender,sender)){Sessions[i].Revoked=true;Sessions.RemoveAt(i);}
    }
    internal static void Clear(World world)
    {
        lock(Gate)for(int i=Sessions.Count-1;i>=0;i--)
            if(ReferenceEquals(Sessions[i].World,world)){Sessions[i].Revoked=true;Sessions.RemoveAt(i);}
    }
}




