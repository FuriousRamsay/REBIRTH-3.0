using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using UnityEngine;

// Hiring uses the actual owner's native inventory. The server never debits a
// remote EntityPlayer replica. Its retained before/after plan is independently
// checked against native saved player data before ownership can be published.
internal static class RebirthSurvivorHirePayment
{
    internal const string PaidPrefix = "rbSurvivorHire_";
    private const int MaximumCells = 32;
    // An unpaid prepared original holds a Leadership slot only this long (real time). The client refuses
    // to start a debit on a payload older than ClientWindow, so a released hold can never be paid late.
    private static readonly long HoldWindowTicks=TimeSpan.FromMinutes(10).Ticks;
    // Every prepare renews Created, so a payload is normally seconds old when paid; the 8 minute margin to
    // the hold window absorbs client/server clock skew. The server Commit stays the authority.
    private static readonly long ClientWindowTicks=TimeSpan.FromMinutes(2).Ticks;
    private static long cacheTicks;private static string cacheKey;private static HashSet<RebirthNpcStableId> cacheValue;
    private sealed class Cell
    {
        internal bool Bag; internal int Slot, Before, After; internal string Item;
        internal XElement Write() => new XElement("cell", new XAttribute("bag",Bag?1:0),
            new XAttribute("slot",Slot),new XAttribute("before",Before),new XAttribute("after",After),new XAttribute("item",Item));
    }
    private sealed class Original
    {
        internal Guid Operation; internal string World, Owner, Actor, Npc, Creation, Currency;
        internal int Deaths, Cost, OwnedBelt; internal long Created; internal int Refund; internal bool Settled; internal List<Cell> Cells=new List<Cell>();
        internal string Marker => PaidPrefix+Operation.ToString("N");
        internal XElement Write() => new XElement("survivorHire",new XAttribute("version",1),
            new XAttribute("operation",Operation.ToString("N")),new XAttribute("world",World),new XAttribute("owner",Owner),
            new XAttribute("actor",Actor),new XAttribute("npc",Npc),new XAttribute("creation",Creation),
            new XAttribute("deaths",Deaths),new XAttribute("cost",Cost),new XAttribute("currency",Currency),
            new XAttribute("belt",OwnedBelt),new XAttribute("settled",Settled?1:0),new XAttribute("created",Created),new XAttribute("refund",Refund),Cells.Select(c=>c.Write()));
        internal static Original Read(XElement x)
        {
            if(x==null||x.Name!="survivorHire"||(int)x.Attribute("version")!=1)throw new InvalidDataException("Hire original format");
            var o=new Original {Operation=Guid.ParseExact((string)x.Attribute("operation"),"N"),World=(string)x.Attribute("world"),
                Owner=(string)x.Attribute("owner"),Actor=(string)x.Attribute("actor"),Npc=(string)x.Attribute("npc"),Creation=(string)x.Attribute("creation"),
                Deaths=(int)x.Attribute("deaths"),Cost=(int)x.Attribute("cost"),Currency=(string)x.Attribute("currency"),
                OwnedBelt=(int)x.Attribute("belt"),Settled=(int)x.Attribute("settled")==1,
                Created=x.Attribute("created")==null?0L:(long)x.Attribute("created"),Refund=x.Attribute("refund")==null?0:(int)x.Attribute("refund")}; // Older originals have no order: they sort first.
            foreach(var c in x.Elements("cell"))o.Cells.Add(new Cell {Bag=(int)c.Attribute("bag")==1,Slot=(int)c.Attribute("slot"),
                Before=(int)c.Attribute("before"),After=(int)c.Attribute("after"),Item=(string)c.Attribute("item")});
            if(o.Operation==Guid.Empty||o.Refund<0||o.Refund>2||string.IsNullOrEmpty(o.World)||string.IsNullOrEmpty(o.Owner)||string.IsNullOrEmpty(o.Actor)||
                string.IsNullOrEmpty(o.Npc)||o.Cost<0||o.Cost>10000000||o.OwnedBelt<0||o.OwnedBelt>20||
                o.Cells.Count>MaximumCells||o.Cells.Any(c=>c.Slot<0||c.Slot>168||!c.Bag&&c.Slot>=o.OwnedBelt||c.Before<=0||c.After<0||c.After>=c.Before||
                    string.IsNullOrEmpty(c.Item)||c.Item.Length>4096)||o.Cells.GroupBy(c=>(c.Bag?"b":"t")+c.Slot).Any(g=>g.Count()!=1)||
                o.Cells.Sum(c=>(long)c.Before-c.After)!=o.Cost)throw new InvalidDataException("Hire original bounds");
            return o;
        }
    }

    internal static bool Eligible(EntityRebirthNPC npc) => npc!=null &&
        (npc.RebirthProfileId=="survivor.ambient"||npc.RebirthProfileId=="survivor.persistent");

    internal static RebirthNpcInteractionCommandResponse Handle(RebirthNpcInteractionContext context, RebirthNpcInteractionCommand command)
    {
        try
        {
            var actor=RebirthNpcInteractionMutationHandlers.FindActorPlayer(context.ActorId);
            var world=GameManager.Instance?.World;
            // Resolving a held payment must work even when the survivor despawned or is no longer eligible.
            var early=Parse(command.Payload);early.TryGetValue("operation",out var earlyOp);
            if((earlyOp=="resolve"||earlyOp=="refunded")&&ThreadManager.IsMainThread()&&world!=null&&!world.IsRemote()&&actor!=null&&!actor.IsDead())
                return Resolve(context,actor,world,early,earlyOp);
            if(!ThreadManager.IsMainThread()||world==null||world.IsRemote()||actor==null||actor.IsDead()||
                !RebirthNpcRuntimeRegistry.TryGetEntityId(context.NpcId,out int entity)||world.GetEntity(entity) is not EntityRebirthNPC npc||!Eligible(npc))
                return No(Localization.Get("xuiRbNpcHireUnavailable"));
            var values=Parse(command.Payload);values.TryGetValue("operation",out var operation);
            if(operation=="commit")return Commit(context,actor,npc,values);
            // A saved payment original owns the quote. Rechecking today's level,
            // discounts or capacity before returning it can strand a debit after
            // reload; Commit checks current capacity unless this same hire is
            // already durably owned by the payer.
            bool pending=TryLoad(context.NpcId,out var retained)&&!retained.Settled;
            if(pending)
            {
                if(!Matches(retained,actor,context))return No(Localization.Get("xuiRbNpcHireOtherPayment"));
                if(retained.Refund!=0)return NoHeld(Localization.Get("xuiRbNpcHireRefundPending"),retained);
                if(RebirthCompanionLeadership.IsEstablishedGroupMember(context.NpcId))
                    return No(Localization.Get("xuiRebirthNpcEstablishedGroup"));
                // Capacity can vanish after a quote (Leadership loss, another hire). Until a debit has
                // started the payment is still free to refuse; once it has, the original is returned so
                // Commit can finish or the player can retry without a second debit.
                if(operation=="prepare"&&!RebirthNpcHireService.HasSavedHire(context.NpcId,context.ActorId)&&!DebitStarted(actor,retained))
                {
                    // Every prepare of an unpaid original renews it to be the newest, so it is checked as a
                    // NEW original (yielding to all others) BEFORE Created moves. Otherwise the check could
                    // pass on the old order and the renewal would then rank it behind a rival at Commit.
                    // The fresh payload is then seconds old when the client pays it.
                    if(!RebirthCompanionLeadership.CanHire(actor,context.NpcId,Reservations(actor,context.NpcId,null),out var staleReason))
                        return No(staleReason);
                    retained.Created=DateTime.UtcNow.Ticks;Save(context.NpcId,retained);
                }
            }
            else
            {
                // A direct or stale command must not quote, let alone debit, a hire that
                // ownership will refuse: another owner means no payment is ever taken.
                if(OwnedByAnother(context.NpcId,context.ActorId))return No(Localization.Get("xuiRbNpcHireOtherOwner"));
                if(!RebirthCompanionLeadership.CanHire(actor,context.NpcId,Reservations(actor,context.NpcId,null),out var capacityReason))return No(capacityReason);
            }
            if(!RebirthNpcTradeReach.CanBegin(actor,context.NpcId))return No(Localization.Get("xuiRbNpcHireMoveCloser"));
            int cost;string currency,detail;
            if(pending){cost=retained.Cost;currency=retained.Currency;}
            else if(!Price(npc,actor,out cost,out currency,out detail))return No(detail);
            if(operation!="prepare")return Yes(string.Format(Localization.Get("xuiRbNpcHireConfirm"),cost,Localization.Get(currency)),
                "price="+cost.ToString(CultureInfo.InvariantCulture)+";currency="+currency+
                (pending&&DebitStarted(actor,retained)?";held="+retained.Operation.ToString("N"):string.Empty));
            if(!values.TryGetValue("price",out var quoted)||quoted!=cost.ToString(CultureInfo.InvariantCulture))
                return No(Localization.Get("xuiRbNpcHirePriceChanged"));
            Original original;
            if(TryLoad(context.NpcId,out original))
            {
                if(!original.Settled)
                {
                    if(!Matches(original,actor,context))return No(Localization.Get("xuiRbNpcHireOtherPayment"));
                    return Prepared(original);
                }
            }
            if(!RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var owner)||!RebirthGearNativePlayerFile.TryRead(owner,out var saved)||
                saved.bDead||saved.id!=actor.entityId||saved.deaths!=actor.Died)return No(Localization.Get("xuiRbNpcHireWaitSave"));
            original=new Original {Operation=Guid.NewGuid(),Created=DateTime.UtcNow.Ticks,World=world.worldState.Guid,Owner=owner.CanonicalId,Actor=context.ActorId,
                Npc=context.NpcId.ToString(),Creation=RebirthCamperCharacterAccess.CurrentCreation(actor),Deaths=actor.Died,Cost=cost,Currency=currency,
                OwnedBelt=RebirthToolbeltCapacity.GetOwnedSlotCount(actor,RebirthGearPlayerDataInventory.ReadSlots(saved,false).Length)};
            var bag=RebirthGearPlayerDataInventory.ReadSlots(saved,true);var belt=RebirthGearPlayerDataInventory.ReadSlots(saved,false);
            var item=ItemClass.GetItem(currency,false);int left=cost;
            AddCells(original,bag,true,bag.Length,item.type,ref left);AddCells(original,belt,false,original.OwnedBelt,item.type,ref left);
            if(left!=0||original.Cells.Count>MaximumCells)return No(string.Format(Localization.Get("xuiRbNpcHireInsufficient"),Localization.Get(currency)));
            Save(context.NpcId,original);return Prepared(original);
        }
        catch(Exception ex){Log.Warning("[REBIRTH Survivor Hire] "+ex.GetType().Name+": "+ex.Message);return No(Localization.Get("xuiRbNpcHireRetained"));}
    }
    private static RebirthNpcInteractionCommandResponse Commit(RebirthNpcInteractionContext context,EntityPlayer actor,EntityRebirthNPC npc,Dictionary<string,string> values)
    {
        if(!TryLoad(context.NpcId,out var original))return No(Localization.Get("xuiRbNpcHireOriginalUnavailable"));
        if(!values.TryGetValue("id",out var id)||id!=original.Operation.ToString("N")||!Matches(original,actor,context))return No(Localization.Get("xuiRbNpcHirePaymentMismatch"));
        if(original.Settled)return No(Localization.Get("xuiRbNpcHireAlreadyPaid"));
        if(original.Refund!=0)return NoHeld(Localization.Get("xuiRbNpcHireRefundPending"),original);
        if(RebirthCompanionLeadership.IsEstablishedGroupMember(context.NpcId))
            return No(Localization.Get("xuiRebirthNpcEstablishedGroup"));
        if(!RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var owner)||!RebirthGearNativePlayerFile.TryRead(owner,out var saved)||
            saved.bDead||saved.deaths!=original.Deaths||
            !RebirthRemoteResourcePlacedStationSavedReceipt.TryRead(saved,original.Marker,out float paid,out bool present)||!present||paid!=2f||
            !Frame(original,RebirthGearPlayerDataInventory.ReadSlots(saved,true),RebirthGearPlayerDataInventory.ReadSlots(saved,false),true))
            return No(Localization.Get("xuiRbNpcHireWaitPayment"));
        // Receipt and frame are verified above and this exact hire is durably saved: settle without asking
        // the interaction session to authorize a second HirePaid (which can fail while ownership is saved).
        if(RebirthNpcHireService.HasSavedHire(context.NpcId,context.ActorId))
        {
            original.Settled=true;Save(context.NpcId,original);
            return Yes(Localization.Get("xuiRebirthNpcHired"),"settled=1");
        }
        // A paid retry may bypass the quote-time check only when this exact
        // survivor is already durably owned by the same actor. Otherwise the
        // current Leadership capacity and protected-group rules still apply.
        if(!RebirthNpcHireService.HasSavedHire(context.NpcId,context.ActorId)&&!OwnedBySame(context.NpcId,context.ActorId)&&
           !RebirthCompanionLeadership.CanHire(actor,context.NpcId,Reservations(actor,context.NpcId,original),out var capacityReason))
            return NoHeld(string.Format(Localization.Get("xuiRbNpcHireCapacityHeld"),capacityReason),original);
        RebirthNpcHireRecord hire;
        var result=RebirthNpcHireService.HirePaid(context.SessionKey,context.RuntimeRevision,out hire);
        if(result!=RebirthNpcInteractionResult.Allowed&&result!=RebirthNpcInteractionResult.AlreadyHired)
            return NoHeld(Localization.Get("xuiRbNpcHireSurvivorHeld"),original);
        if(!RebirthNpcHireService.HasSavedHire(context.NpcId,context.ActorId))
            return NoHeld(Localization.Get("xuiRbNpcHireWaitOwnership"),original);
        original.Settled=true;Save(context.NpcId,original);
        return Yes(Localization.Get("xuiRebirthNpcHired"),"settled=1");
    }
    // ---- Held payment resolution -------------------------------------------------------------
    // A debit that began (saved receipt phase >= 1) is never silently expired. It ends in exactly one of:
    // the hire completing (Commit), or an operation-bound refund: server marks Refund=1 (saved), the
    // payer's client restores the exact debited cells (marker 3 then 4, idempotent per cell), and the
    // server settles only after reading marker 4 back from the saved player file (Refund=2, Settled).
    // Wire ops (Hire command payload): "operation=resolve;id=<op>" -> settled=1 | refund=<base64> ;
    // then client TryRefundLocal ; then "operation=refunded;id=<op>" -> refunded=1.
    internal sealed class HeldPayment
    {
        public RebirthNpcStableId Npc; public string Operation, Currency; public int Cost; public bool RefundAuthorized;
    }
    // Host/server read API (a remote client learns of a held payment from the quote payload "held=<op>"
    // or from NoHeld responses, which carry "held=<op>"). Never expires; only debit-started originals.
    internal static List<HeldPayment> GetHeldPayments(EntityPlayer actor)
    {
        var result=new List<HeldPayment>();
        try
        {
            if(actor==null||actor.world==null||actor.world.IsRemote())return result;
            string world=actor.world.worldState.Guid,actorKey=ActorKey(actor);var file=new SavedFile();
            foreach(var view in RebirthNpcAggregatePersistenceStore.SnapshotViews())
            {
                if(view?.Identity==null||view.UnknownOptionalElements==null)continue;
                foreach(string text in view.UnknownOptionalElements)
                {
                    if(text==null||text.Length>100000||!text.StartsWith("<survivorHire",StringComparison.Ordinal))continue;
                    Original o;
                    try
                    {
                        using(var input=new StringReader(text))
                        using(var reader=System.Xml.XmlReader.Create(input,new System.Xml.XmlReaderSettings {DtdProcessing=System.Xml.DtdProcessing.Prohibit,XmlResolver=null}))
                            o=Original.Read(XElement.Load(reader));
                    }
                    catch(Exception){continue;}
                    if(o.Settled||o.World!=world||o.Deaths!=actor.Died||!string.Equals(o.Actor,actorKey,StringComparison.OrdinalIgnoreCase)||
                       !DebitStarted(actor,o,file))continue;
                    result.Add(new HeldPayment {Npc=view.Identity.StableNpcId,Operation=o.Operation.ToString("N"),Currency=o.Currency,Cost=o.Cost,RefundAuthorized=o.Refund==1});
                }
            }
        }
        catch(Exception ex){Log.Warning("[REBIRTH Survivor Hire] held payments: "+ex.GetType().Name);}
        return result;
    }
    private static RebirthNpcInteractionCommandResponse Resolve(RebirthNpcInteractionContext context,EntityPlayer actor,World world,Dictionary<string,string> values,string operation,bool scoped=false)
    {
        if(!TryLoad(context.NpcId,out var o))return No(Localization.Get("xuiRbNpcHireOriginalUnavailable"));
        if(!values.TryGetValue("id",out var id)||id!=o.Operation.ToString("N")||!Matches(o,actor,context))return No(Localization.Get("xuiRbNpcHirePaymentMismatch"));
        if(o.Settled)return operation=="refunded"&&o.Refund==2?Yes(Localization.Get("xuiRbNpcHireRefunded"),"refunded=1"):No(Localization.Get("xuiRbNpcHireOriginalUnavailable"));
        if(operation=="refunded")
        {
            if(o.Refund!=1)return No(Localization.Get("xuiRbNpcHireOriginalUnavailable"));
            // Only the saved player file proves the refund finished (marker 4 is written after the cells).
            if(!RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var owner)||!RebirthGearNativePlayerFile.TryRead(owner,out var saved)||
               saved.bDead||!RebirthRemoteResourcePlacedStationSavedReceipt.TryRead(saved,o.Marker,out float phase,out bool present)||!present||phase!=4f)
                return NoHeld(Localization.Get("xuiRbNpcHireWaitPayment"),o);
            o.Refund=2;o.Settled=true;Save(context.NpcId,o);
            return Yes(Localization.Get("xuiRbNpcHireRefunded"),"refunded=1");
        }
        if(o.Refund==1)return RefundOffer(o);
        EntityRebirthNPC npc=RebirthNpcRuntimeRegistry.TryGetEntityId(context.NpcId,out int entity)&&world.GetEntity(entity) is EntityRebirthNPC found&&Eligible(found)?found:null;
        // A ownership already saved or reserved for this payer is completed, never refunded.
        // Actor-scoped (no NPC session): HirePaid cannot be authorized, so only an already saved hire is
        // settled here (Commit settles it directly); completing a hire must go through the NPC session.
        bool hireSaved=RebirthNpcHireService.HasSavedHire(context.NpcId,context.ActorId);
        if(hireSaved||OwnedBySame(context.NpcId,context.ActorId))
            return scoped&&!hireSaved?NoHeld(Localization.Get("xuiRbNpcHireOpenToComplete"),o):Commit(context,actor,npc,values);
        if(npc!=null&&!OwnedByAnother(context.NpcId,context.ActorId)&&!RebirthCompanionLeadership.IsEstablishedGroupMember(context.NpcId)&&
           RebirthCompanionLeadership.CanHire(actor,context.NpcId,Reservations(actor,context.NpcId,o),out _))
            return scoped?NoHeld(Localization.Get("xuiRbNpcHireOpenToComplete"),o):Commit(context,actor,npc,values);
        // A refund is authorized only on proof the debit marker was saved; an unreadable file waits.
        if(!RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var payer)||!RebirthGearNativePlayerFile.TryRead(payer,out var payerFile)||
           !RebirthRemoteResourcePlacedStationSavedReceipt.TryRead(payerFile,o.Marker,out float debitPhase,out bool debitPresent))
            return NoHeld(Localization.Get("xuiRbNpcHireWaitPayment"),o);
        if(!debitPresent||debitPhase<1f)return No(Localization.Get("xuiRbNpcHireNothingHeld"));
        o.Refund=1;Save(context.NpcId,o);
        return RefundOffer(o);
    }
    // ---- Actor-scoped route (no NPC session) ----------------------------------------------------
    // Reached from RebirthNpcInteractionCommandRouter.Dispatch for Kind=Hire with operation list|resolve|refunded
    // and SessionKey of any non-empty value (e.g. "held-payment"); authenticated actor id comes from the packet layer.
    internal const string HeldSessionKey="held-payment";
    internal static bool IsActorScoped(RebirthNpcInteractionCommand command)
    {
        if(command==null||command.SessionKey!=HeldSessionKey)return false; // NPC-session commands keep their existing path
        Parse(command.Payload).TryGetValue("operation",out var op);
        return op=="list"||op=="resolve"||op=="refunded";
    }
    internal static RebirthNpcInteractionCommandResponse HandleActorScoped(RebirthNpcInteractionCommand command,string actorId)
    {
        try
        {
            var world=GameManager.Instance?.World;
            var actor=string.IsNullOrEmpty(actorId)?null:RebirthNpcInteractionMutationHandlers.FindActorPlayer(actorId);
            if(!ThreadManager.IsMainThread()||world==null||world.IsRemote()||actor==null||actor.IsDead())return No(Localization.Get("xuiRbNpcHireUnavailable"));
            var values=Parse(command.Payload);values.TryGetValue("operation",out var op);
            var held=GetHeldPayments(actor);
            if(op=="list")
                return Yes(string.Empty,"held="+string.Join(",",held.Take(8).Select(h=>h.Operation+"|"+h.Npc+"|"+h.Cost.ToString(CultureInfo.InvariantCulture)+"|"+h.Currency)));
            if(!values.TryGetValue("id",out var id))return No(Localization.Get("xuiRbNpcHireOriginalUnavailable"));
            RebirthNpcStableId npc=default;bool found=false;
            foreach(var h in held)if(h.Operation==id){npc=h.Npc;found=true;break;}
            // A refunded original is settled and no longer listed: let Resolve answer the repeat idempotently.
            if(!found&&op=="refunded")found=FindSettledRefund(actor,id,out npc);
            if(!found)return No(Localization.Get("xuiRbNpcHireOriginalUnavailable"));
            var context=new RebirthNpcInteractionContext(actorId,npc,RebirthNpcInteractionPermissions.Hire,0u,command.SessionKey,0L);
            return Resolve(context,actor,world,values,op,true);
        }
        catch(Exception ex){Log.Warning("[REBIRTH Survivor Hire] actor-scoped: "+ex.GetType().Name+": "+ex.Message);return No(Localization.Get("xuiRbNpcHireRetained"));}
    }
    private static bool FindSettledRefund(EntityPlayer actor,string operation,out RebirthNpcStableId npc)
    {
        npc=default;string actorKey=ActorKey(actor);
        foreach(var view in RebirthNpcAggregatePersistenceStore.SnapshotViews())
        {
            if(view?.Identity==null||view.UnknownOptionalElements==null)continue;
            foreach(string text in view.UnknownOptionalElements)
            {
                if(text==null||text.Length>100000||!text.StartsWith("<survivorHire",StringComparison.Ordinal))continue;
                try
                {
                    using(var input=new StringReader(text))
                    using(var reader=System.Xml.XmlReader.Create(input,new System.Xml.XmlReaderSettings {DtdProcessing=System.Xml.DtdProcessing.Prohibit,XmlResolver=null}))
                    {
                        var o=Original.Read(XElement.Load(reader));
                        if(o.Settled&&o.Refund==2&&o.Operation.ToString("N")==operation&&string.Equals(o.Actor,actorKey,StringComparison.OrdinalIgnoreCase))
                        {npc=view.Identity.StableNpcId;return true;}
                    }
                }
                catch(Exception){}
            }
        }
        return false;
    }
    private static RebirthNpcInteractionCommandResponse RefundOffer(Original o)=>Yes(Localization.Get("xuiRbNpcHireRefundReady"),
        "refund="+Convert.ToBase64String(Encoding.UTF8.GetBytes(o.Write().ToString(SaveOptions.DisableFormatting))));
    private static RebirthNpcInteractionCommandResponse NoHeld(string detail,Original o)=>new RebirthNpcInteractionCommandResponse
        {Status=RebirthNpcInteractionCommandStatus.Rejected,Detail=detail,ResponsePayload="held="+o.Operation.ToString("N")};
    private static bool OwnedBySame(RebirthNpcStableId id,string actorId)=>
        RebirthNpcRuntimeRegistry.TryGetEntityId(id,out int entity)&&RebirthNpcRuntimeRegistry.TryGet(entity,out var state)&&state!=null&&
        state.OwnershipKind==RebirthNpcOwnershipKind.Player&&string.Equals(state.OwnerId,actorId,StringComparison.OrdinalIgnoreCase);
    // Local refund on the payer's own client after the server marked Refund=1. Per cell: a slot still in the
    // debited state is restored exactly; a slot already restored is left; a slot the player changed gets the
    // owed stack added to the backpack. Marker 3 (started) then 4 (done) are saved, so reruns never add twice
    // except for the unavoidable window between a fallback add and the marker-4 save in the same call.
    internal static bool TryRefundLocal(EntityPlayerLocal player,RebirthNpcInteractionProjection projection,string payload,out string operation,out string error)
    {
        operation=null;error=Localization.Get("xuiRbNpcHireOriginalUnavailable");
        try
        {
            var values=Parse(payload);if(!values.TryGetValue("refund",out var encoded)||encoded.Length>60000)return false;
            var o=Original.Read(XElement.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(encoded))));
            if(o.Refund!=1)return false;
            // projection may be null on the actor-scoped route (no NPC session); the payer identity checks below still apply.
            if(!ThreadManager.IsMainThread()||player==null||player.IsDead()||player.Died!=o.Deaths||projection!=null&&(projection.NpcId.ToString()!=o.Npc||projection.ActorId!=o.Actor)||
                !ReferenceEquals(GameManager.Instance?.World,player.world)||!ReferenceEquals(player.world.GetPrimaryPlayer(),player)||
                player.world.worldState.Guid!=o.World||RebirthCamperCharacterAccess.CurrentCreation(player)!=o.Creation||
                !RebirthStablePlayerIdentity.TryFromLocalPlatform(out var owner)||owner.CanonicalId!=o.Owner)return false;
            operation=o.Operation.ToString("N");
            float phase=player.Buffs.GetCustomVar(o.Marker);
            if(phase!=4f)
            {
                if(phase!=0f&&phase!=1f&&phase!=2f&&phase!=3f)return false;
                if(phase!=0f)
                {
                    // Pre-flight: every owed fallback stack must fit before any cell changes, so a full bag
                    // refuses cleanly and a retry cannot add an earlier stack twice.
                    int owedStacks=0;var bagItems=player.bag.ItemGrid.items;int empty=0;
                    foreach(var b in bagItems)if(b==null||b.IsEmpty())empty++;
                    for(int i=0;i<o.Cells.Count;i++)
                    {
                        var c=o.Cells[i];if(player.Buffs.GetCustomVar(o.Marker+"_"+i)==1f)continue;
                        var grid=c.Bag?player.bag.ItemGrid:player.inventory.ItemGrid;
                        if(c.Slot>=grid.items.Length)return false;
                        var slot=grid.items[c.Slot];
                        if(CellMatches(c,slot,false)||CellMatches(c,slot,true))continue;
                        if(!RebirthNativeItemCodec.TryDecode(c.Item,out var owedValue))return false;
                        owedStacks++;
                        if(!player.bag.CanTakeItem(new ItemStack(owedValue,c.Before-c.After))||empty<owedStacks){error=Localization.Get("xuiRbNpcHireRefundRoom");return false;}
                    }
                    player.Buffs.SetCustomVar(o.Marker,3f,true);GameManager.Instance.SaveLocalPlayerData();
                    for(int i=0;i<o.Cells.Count;i++)
                    {
                        var c=o.Cells[i];string flag=o.Marker+"_"+i;if(player.Buffs.GetCustomVar(flag)==1f)continue;
                        var grid=c.Bag?player.bag.ItemGrid:player.inventory.ItemGrid;
                        var slot=grid.items[c.Slot];
                        if(CellMatches(c,slot,false)){player.Buffs.SetCustomVar(flag,1f,true);continue;}
                        if(!RebirthNativeItemCodec.TryDecode(c.Item,out var value))return false;
                        if(CellMatches(c,slot,true))
                        {
                            var next=c.After==0?new ItemStack(value,c.Before):slot.Clone();next.count=c.Before;
                            grid.SetItem(c.Slot,next);
                            if(!CellMatches(c,grid.items[c.Slot],false))return false;
                        }
                        else if(!player.bag.AddItem(new ItemStack(value,c.Before-c.After))){error=Localization.Get("xuiRbNpcHireRefundRoom");return false;}
                        // Flag and save right after the change so a retry or reload never repeats this cell.
                        player.Buffs.SetCustomVar(flag,1f,true);GameManager.Instance.SaveLocalPlayerData();
                    }
                }
                player.Buffs.SetCustomVar(o.Marker,4f,true);
            }
            GameManager.Instance.SaveLocalPlayerData();error=null;return true;
        }
        catch(Exception ex){Log.Warning("[REBIRTH Survivor Hire] local refund: "+ex.GetType().Name);error=Localization.Get("xuiRbNpcHireRetained");return false;}
    }
    private static bool OwnedByAnother(RebirthNpcStableId id,string actorId)
    {
        if(RebirthNpcRuntimeRegistry.TryGetEntityId(id,out int entity)&&RebirthNpcRuntimeRegistry.TryGet(entity,out var state)&&state!=null&&
            state.OwnershipKind==RebirthNpcOwnershipKind.Player&&!string.IsNullOrEmpty(state.OwnerId)&&
            !string.Equals(state.OwnerId,actorId,StringComparison.OrdinalIgnoreCase))return true;
        return RebirthNpcAggregatePersistenceStore.TryGetView(id,out var view)&&view.Lifecycle?.TombstoneState!=true&&
            string.Equals(view.Ownership?.OwnershipState,RebirthNpcOwnershipKind.Player.ToString(),StringComparison.OrdinalIgnoreCase)&&
            !string.IsNullOrEmpty(view.Ownership.OwnerPlatformIdOrPersistentPlayerId)&&
            !string.Equals(view.Ownership.OwnerPlatformIdOrPersistentPlayerId,actorId,StringComparison.OrdinalIgnoreCase);
    }
    // Slots held by the actor's other unsettled originals. A new original (self==null) yields to all of
    // them, so a second quote is refused before any debit exists. An existing one (self) yields only to
    // EARLIER originals (Created, then operation id), so with a legacy overlap exactly the earliest
    // paid original can commit and a later one never blocks an earlier one.
    internal static HashSet<RebirthNpcStableId> Reservations(EntityPlayer actor,RebirthNpcStableId exclude,object self)
    {
        var held=new HashSet<RebirthNpcStableId>();
        var mine=self as Original;var fileCache=new SavedFile(); // one player file read per call, however many aged originals
        string world=actor.world.worldState.Guid,actorKey=ActorKey(actor);
        foreach(var view in RebirthNpcAggregatePersistenceStore.SnapshotViews())
        {
            if(view?.Identity==null||view.Identity.StableNpcId.Equals(exclude)||view.UnknownOptionalElements==null)continue;
            foreach(string text in view.UnknownOptionalElements)
            {
                if(text==null||text.Length>100000||!text.StartsWith("<survivorHire",StringComparison.Ordinal))continue;
                Original o;
                try
                {
                    using(var input=new StringReader(text))
                    using(var reader=System.Xml.XmlReader.Create(input,new System.Xml.XmlReaderSettings {DtdProcessing=System.Xml.DtdProcessing.Prohibit,XmlResolver=null}))
                        o=Original.Read(XElement.Load(reader));
                }
                catch(Exception){continue;}
                if(o.Settled||o.World!=world||o.Deaths!=actor.Died||!string.Equals(o.Actor,actorKey,StringComparison.OrdinalIgnoreCase))continue;
                // Release a hold that is aged (or predates the timestamp) and provably unpaid; never one
                // whose debit started or whose player file cannot prove otherwise.
                if((o.Created==0||DateTime.UtcNow.Ticks-o.Created>HoldWindowTicks)&&!DebitStarted(actor,o,fileCache))continue;
                if(mine!=null&&!(o.Created<mine.Created||o.Created==mine.Created&&
                    string.CompareOrdinal(o.Operation.ToString("N"),mine.Operation.ToString("N"))<0))continue;
                held.Add(view.Identity.StableNpcId);
            }
        }
        return held;
    }
    // True when the saved player file shows this payment began (phase 1 or 2). Unreadable counts as started
    // so a possibly debited payment is never refused.
    // UI read path (polled): result reused for 3 s per actor/target. A fresh own original ranks as itself,
    // matching what Commit would decide; an old one is shown as a new quote, matching the renewing prepare.
    internal static HashSet<RebirthNpcStableId> CachedReservations(EntityPlayer actor,RebirthNpcStableId target)
    {
        string key=ActorKey(actor)+"|"+target;long now=DateTime.UtcNow.Ticks;
        if(cacheValue!=null&&cacheKey==key&&now-cacheTicks>=0&&now-cacheTicks<TimeSpan.FromSeconds(3).Ticks)return cacheValue;
        object self=null;
        if(TryLoad(target,out var own)&&!own.Settled&&now-own.Created<=ClientWindowTicks)self=own;
        cacheValue=Reservations(actor,target,self);cacheKey=key;cacheTicks=now;return cacheValue;
    }
    private sealed class SavedFile {internal bool Loaded; internal PlayerDataFile File;}
    private static bool DebitStarted(EntityPlayer actor,Original o,SavedFile cache=null)
    {
        PlayerDataFile saved;
        if(cache!=null&&cache.Loaded)saved=cache.File;
        else
        {
            saved=RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var owner)&&RebirthGearNativePlayerFile.TryRead(owner,out var read)?read:null;
            if(cache!=null){cache.Loaded=true;cache.File=saved;}
        }
        if(saved==null)return true;
        return !RebirthRemoteResourcePlacedStationSavedReceipt.TryRead(saved,o.Marker,out float phase,out bool present)||present&&phase>=1f;
    }
    private static string ActorKey(EntityPlayer actor)=>GameManager.Instance?.GetPersistentPlayerList()?.GetPlayerDataFromEntityID(actor.entityId)?.PrimaryId?.ToString()??"";
    private static bool Matches(Original o,EntityPlayer actor,RebirthNpcInteractionContext c)=>o.World==actor.world.worldState.Guid&&
        o.Actor==c.ActorId&&o.Npc==c.NpcId.ToString()&&o.Deaths==actor.Died&&o.Creation==RebirthCamperCharacterAccess.CurrentCreation(actor)&&
        RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var owner)&&owner.CanonicalId==o.Owner;
    private static void AddCells(Original o,ItemStack[] slots,bool bag,int count,int type,ref int left)
    {
        for(int i=0;i<count&&left>0;i++)
        {
            var s=slots[i];if(s==null||s.IsEmpty()||s.itemValue.type!=type||!RebirthNpcPlayerInventoryEndpoint.IsFungible(s.itemValue))continue;
            int take=Math.Min(s.count,left);left-=take;o.Cells.Add(new Cell {Bag=bag,Slot=i,Before=s.count,After=s.count-take,Item=RebirthNativeItemCodec.Encode(s.itemValue)});
        }
    }
    private static bool Frame(Original o,ItemStack[] bag,ItemStack[] belt,bool after)
    {
        foreach(var c in o.Cells)
        {
            var slots=c.Bag?bag:belt;if(slots==null||c.Slot>=slots.Length||!CellMatches(c,slots[c.Slot],after))return false;
        }
        return true;
    }
    private static bool CellMatches(Cell c,ItemStack s,bool after)
    {
        int count=after?c.After:c.Before;
        return s!=null&&(count==0?s.IsEmpty():s.count==count&&!s.IsEmpty()&&RebirthNativeItemCodec.Encode(s.itemValue)==c.Item);
    }
    // Called only by the actual local interaction owner after a server preparation.
    // Each cell is an immutable before/after pair; resuming a partially saved debit
    // never subtracts from an already-after cell a second time.
    internal static bool TryPayLocal(EntityPlayerLocal player,RebirthNpcInteractionProjection projection,string payload,string confirmedPrice,out string operation,out string error)
    {
        operation=null;error=Localization.Get("xuiRbNpcHireOriginalUnavailable");
        try
        {
            var values=Parse(payload);if(!values.TryGetValue("prepared",out var encoded)||encoded.Length>60000)return false;
            var o=Original.Read(XElement.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(encoded))));
            if(confirmedPrice!=o.Cost.ToString(CultureInfo.InvariantCulture)){error=Localization.Get("xuiRbNpcHireConfirmPrice");return false;}
            if(!ThreadManager.IsMainThread()||player==null||player.IsDead()||player.Died!=o.Deaths||projection==null||projection.NpcId.ToString()!=o.Npc||projection.ActorId!=o.Actor||
                !ReferenceEquals(GameManager.Instance?.World,player.world)||!ReferenceEquals(player.world.GetPrimaryPlayer(),player)||
                player.world.worldState.Guid!=o.World||RebirthCamperCharacterAccess.CurrentCreation(player)!=o.Creation||
                !RebirthStablePlayerIdentity.TryFromLocalPlatform(out var owner)||owner.CanonicalId!=o.Owner||
                RebirthToolbeltCapacity.GetOwnedSlotCount(player,player.inventory.ItemGrid.items.Length)!=o.OwnedBelt)return false;
            operation=o.Operation.ToString("N");
            var bag=player.bag.ItemGrid;var belt=player.inventory.ItemGrid;float phase=player.Buffs.GetCustomVar(o.Marker);
            if(phase==2f){error=null;GameManager.Instance.SaveLocalPlayerData();return true;}
            if(phase!=0f&&phase!=1f)return false;
            if(phase==0f&&o.Created>0&&DateTime.UtcNow.Ticks-o.Created>ClientWindowTicks){error=Localization.Get("xuiRbNpcHireOriginalUnavailable");return false;}
            if(phase==0f&&!Frame(o,bag.items,belt.items,false)){error=Localization.Get("xuiRbNpcHireInventoryChanged");return false;}
            foreach(var c in o.Cells)
            {
                var grid=c.Bag?bag:belt;
                if(c.Slot>=grid.items.Length||!CellMatches(c,grid.items[c.Slot],false)&&!(phase==1f&&CellMatches(c,grid.items[c.Slot],true)))return false;
            }
            player.Buffs.SetCustomVar(o.Marker,1f,true);GameManager.Instance.SaveLocalPlayerData();
            foreach(var c in o.Cells)
            {
                var grid=c.Bag?bag:belt;if(CellMatches(c,grid.items[c.Slot],true))continue;
                if(!CellMatches(c,grid.items[c.Slot],false))return false;
                var next=c.After==0?ItemStack.Empty.Clone():grid.items[c.Slot].Clone();next.count=c.After;
                grid.SetItem(c.Slot,next);
                if(!CellMatches(c,grid.items[c.Slot],true))return false;
            }
            if(!Frame(o,bag.items,belt.items,true))return false;
            player.Buffs.SetCustomVar(o.Marker,2f,true);GameManager.Instance.SaveLocalPlayerData();error=null;return true;
        }
        catch(Exception ex){Log.Warning("[REBIRTH Survivor Hire] local payment continuation: "+ex.GetType().Name);error=Localization.Get("xuiRbNpcHireRetained");return false;}
    }
    private static bool Price(EntityRebirthNPC npc,EntityPlayer player,out int cost,out string currency,out string error)
    {
        var p=npc.EntityClass.Properties;currency=p.GetString("HireCurrency");if(string.IsNullOrEmpty(currency))currency="casinoCoin";
        cost=0;error=Localization.Get("xuiRbNpcHireTermsInvalid");
        if(!int.TryParse(p.GetString("HireCost"),out int baseCost))baseCost=500;
        if(!int.TryParse(p.GetString("PlayerLevelRestriction"),out int minLevel))minLevel=1;
        if(baseCost<0||baseCost>10000000||minLevel<1)return false;
        // The hiring-restrictions override lifts the player-level requirement; price and currency still apply.
        bool levelWaived=RebirthSandboxOptionManager.Current!=null&&RebirthSandboxOptionManager.Current.DisableHiringRestrictions;
        if(!levelWaived&&player.Progression.Level<minLevel){error=string.Format(Localization.Get("xuiRbNpcHireMinimumLevel"),minLevel);return false;}
        float raw=npc.Buffs.GetCustomVar("$FR_NPC_Level");if(float.IsNaN(raw)||float.IsInfinity(raw)||raw<0||raw>10000)return false;
        float discount=1f;
        if(Progression.ProgressionClasses!=null&&Progression.ProgressionClasses.ContainsKey("perkBetterBarter"))
            discount-=Math.Min(5,Math.Max(0,player.Progression.GetProgressionValue("perkBetterBarter").Level))*.02f;
        if(Progression.ProgressionClasses!=null&&Progression.ProgressionClasses.ContainsKey("FuriousRamsayAchievementTradeAura"))
        {
            int aura=player.Progression.GetProgressionValue("FuriousRamsayAchievementTradeAura").Level;
            discount-=aura<=0?0f:aura==1?.2f:aura==2?.3f:.4f;
        }
        long gross=baseCost+500L*(int)raw;if(gross>10000000)return false;
        cost=(int)(gross*Math.Max(.5f,discount));var item=ItemClass.GetItem(currency,false);
        if(item==null||item.IsEmpty()||!RebirthNpcPlayerInventoryEndpoint.IsFungible(item))return false;
        error=null;return true;
    }
    private static bool TryLoad(RebirthNpcStableId id,out Original original)
    {
        original=null;
        if(!RebirthNpcAggregatePersistenceStore.TryGet(id,out var person))return false;
        foreach(var text in person.UnknownOptionalElements??new string[0])
        {
            if(text==null||text.Length>100000)throw new InvalidDataException("Hire extension bounds");
            using(var input=new StringReader(text))
            using(var reader=System.Xml.XmlReader.Create(input,new System.Xml.XmlReaderSettings {DtdProcessing=System.Xml.DtdProcessing.Prohibit,XmlResolver=null}))
            {
                var element=XElement.Load(reader);if(element.Name!="survivorHire")continue;
                if(original!=null)throw new InvalidDataException("Duplicate hire original");original=Original.Read(element);
            }
        }
        return original!=null;
    }
    private static void Save(RebirthNpcStableId id,Original o)
    {
        string image=o.Write().ToString(SaveOptions.DisableFormatting);
        // The optional extension participates in the existing aggregate checkpoint,
        // backup and recovery generation; it is not a second economy sidecar.
        RebirthNpcAggregatePersistenceStore.CaptureRuntimeEnvelopes();
        if(!RebirthNpcAggregatePersistenceStore.Mutate(id,person=>
        {
            var extensions=(person.UnknownOptionalElements??new string[0]).Where(text=>XElement.Parse(text).Name!="survivorHire").ToList();
            extensions.Add(image);person.UnknownOptionalElements=extensions.ToArray();
        }))throw new IOException("Hire original publication unavailable");
        RebirthNpcAggregatePersistenceStore.Save();
        if(!TryLoad(id,out var saved)||saved.Write().ToString(SaveOptions.DisableFormatting)!=image||
            !RebirthNpcAggregatePersistenceStore.TryReadSavedPerson(id,out var published)||
            !(published.UnknownOptionalElements??new string[0]).Contains(image))throw new IOException("Hire original primary readback");
    }
    private static RebirthNpcInteractionCommandResponse Prepared(Original o)=>Yes("Your confirmed hiring payment is ready.",
        "prepared="+Convert.ToBase64String(Encoding.UTF8.GetBytes(o.Write().ToString(SaveOptions.DisableFormatting))));
    internal static Dictionary<string,string> Parse(string text)
    {
        var result=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        foreach(var s in (text??string.Empty).Split(';')){int i=s.IndexOf('=');if(i>0)result[s.Substring(0,i)]=s.Substring(i+1);}return result;
    }
    private static RebirthNpcInteractionCommandResponse Yes(string detail,string payload)=>new RebirthNpcInteractionCommandResponse {Status=RebirthNpcInteractionCommandStatus.Accepted,Detail=detail,ResponsePayload=payload};
    private static RebirthNpcInteractionCommandResponse No(string detail)=>new RebirthNpcInteractionCommandResponse {Status=RebirthNpcInteractionCommandStatus.Rejected,Detail=detail,ResponsePayload=string.Empty};
}
