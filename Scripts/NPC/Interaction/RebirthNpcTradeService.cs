using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;

#nullable disable

public enum RebirthNpcTradeDirection : byte
{
    BuyFromNpc = 0,
    SellToNpc = 1
}

public sealed class RebirthNpcTradeOffer
{
    public string ProfileId { get; internal set; }
    public string ItemKey { get; internal set; }
    public string CurrencyItemKey { get; internal set; }
    public int BuyUnitPrice { get; internal set; }
    public int SellUnitPrice { get; internal set; }
    public int MaximumQuantity { get; internal set; }
}

public sealed class RebirthNpcTradeQuote
{
    public ulong RecoveryGeneration { get; internal set; }
    public Guid QuoteId { get; internal set; }
    public string ActorId { get; internal set; }
    public RebirthNpcStableId NpcId { get; internal set; }
    public RebirthNpcTradeDirection Direction { get; internal set; }
    public string EndpointId { get; internal set; }
    public string ItemKey { get; internal set; }
    public string CurrencyItemKey { get; internal set; }
    public int Quantity { get; internal set; }
    public int UnitPrice { get; internal set; }
    public int TotalPrice { get; internal set; }
    public uint ExpectedNpcInventoryRevision { get; internal set; }
    public long ExpiresClockTicks { get; internal set; }
}

/// <summary>
/// Authority-side offer registry. Prices are never accepted from interaction payloads.
/// Content integrations may register or replace offers during bootstrap.
/// </summary>
public static class RebirthNpcTradeOfferRegistry
{
    private static readonly object Sync = new object();
    private static readonly Dictionary<string, RebirthNpcTradeOffer> Offers =
        new Dictionary<string, RebirthNpcTradeOffer>(StringComparer.OrdinalIgnoreCase);

    public static void Register(RebirthNpcTradeOffer offer, bool replace = false)
    {
        if (offer == null) throw new ArgumentNullException(nameof(offer));
        offer.ProfileId = (offer.ProfileId ?? string.Empty).Trim();
        offer.ItemKey = (offer.ItemKey ?? string.Empty).Trim();
        offer.CurrencyItemKey = (offer.CurrencyItemKey ?? string.Empty).Trim();
        if (offer.ProfileId.Length == 0 || offer.ItemKey.Length == 0 || offer.CurrencyItemKey.Length == 0)
            throw new ArgumentException("Trade offers require profile, item and currency keys.");
        if (offer.BuyUnitPrice < 0 || offer.SellUnitPrice < 0 || offer.MaximumQuantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(offer));
        string key = Key(offer.ProfileId, offer.ItemKey);
        lock (Sync)
        {
            if (!replace && Offers.ContainsKey(key))
                throw new InvalidOperationException("Duplicate NPC trade offer: " + key);
            Offers[key] = offer;
        }
    }

    public static bool TryResolve(string profileId, string itemKey, out RebirthNpcTradeOffer offer)
    {
        offer=null;
        if(string.IsNullOrWhiteSpace(profileId)||string.IsNullOrWhiteSpace(itemKey))return false;
        lock (Sync) return Offers.TryGetValue(Key(profileId, itemKey), out offer)
            || Offers.TryGetValue(Key("*", itemKey), out offer);
    }

    public static RebirthNpcTradeOffer[] GetForProfile(string profileId)
    {
        if(string.IsNullOrWhiteSpace(profileId))return new RebirthNpcTradeOffer[0];
        profileId=profileId.Trim();
        var selected=new Dictionary<string,RebirthNpcTradeOffer>(StringComparer.OrdinalIgnoreCase);
        lock(Sync)
        {
            foreach(RebirthNpcTradeOffer o in Offers.Values)
                if(o.ProfileId=="*")selected[o.ItemKey]=o;
            foreach(RebirthNpcTradeOffer o in Offers.Values)
                if(string.Equals(o.ProfileId,profileId,StringComparison.OrdinalIgnoreCase))selected[o.ItemKey]=o;
        }
        List<RebirthNpcTradeOffer> result=new List<RebirthNpcTradeOffer>(selected.Values);
        result.Sort(delegate(RebirthNpcTradeOffer a,RebirthNpcTradeOffer b){return string.Compare(a.ItemKey,b.ItemKey,StringComparison.OrdinalIgnoreCase);});return result.ToArray();
    }
    public static int Count { get { lock (Sync) return Offers.Count; } }
    public static void Reset() { lock (Sync) Offers.Clear(); }

    private static string Key(string profileId, string itemKey)
    {
        return (profileId ?? string.Empty).Trim() + "|" + (itemKey ?? string.Empty).Trim();
    }
}

/// <summary>
/// Retained native-owner trade coordinator. Local Commit and remote native
/// commands preserve their immutable original through owner/NPC checkpoints.
/// </summary>
public static partial class RebirthNpcTradeService
{
    private sealed class QuoteRecord
    {
        public RebirthNpcTradeQuote Quote;
        public bool Consumed;
        internal HostPrepared OwnerPreparation;
        internal HostUnused OwnerUnused;
        internal HostQuote Issued;
        // Runtime outcome only: 0 unknown, 1 both legs, 2 no-effect/reverted
        // first leg, 3 positive compensation. Never a durable payment proof.
        public byte Resolution;
    }

    private const int MaxQuotes = 512;
    private static readonly object Sync = new object();
    private static Dictionary<Guid, QuoteRecord> Quotes = new Dictionary<Guid, QuoteRecord>();
    private static RebirthNpcFairRoundRobin<Guid> QuoteOrder = new RebirthNpcFairRoundRobin<Guid>();
    private static bool registered;
    private static long quoted, committed, rejected, expired, compensated, compensationFailures;

    public static void EnsureRegistered()
    {
        lock (Sync)
        {
            if (registered) return;
            RegisterDefaults();
            RebirthNpcInteractionCommandRouter.Register(new TradeHandler());
            registered = true;
        }
    }

    private static void RegisterDefaults()
    {
        if(RebirthNpcTradeOfferRegistry.Count>0)return;
        RebirthNpcTradeOfferRegistry.Register(new RebirthNpcTradeOffer{ProfileId="*",ItemKey="foodCanChili",CurrencyItemKey="casinoCoin",BuyUnitPrice=24,SellUnitPrice=8,MaximumQuantity=20});
        RebirthNpcTradeOfferRegistry.Register(new RebirthNpcTradeOffer{ProfileId="*",ItemKey="medicalFirstAidBandage",CurrencyItemKey="casinoCoin",BuyUnitPrice=45,SellUnitPrice=15,MaximumQuantity=10});
        RebirthNpcTradeOfferRegistry.Register(new RebirthNpcTradeOffer{ProfileId="*",ItemKey="resourceScrapIron",CurrencyItemKey="casinoCoin",BuyUnitPrice=2,SellUnitPrice=1,MaximumQuantity=250});
    }

    public static string GetReport()
    {
        lock (Sync) if(TradeScope(false))PruneExpiredLocked();
        return "[REBIRTH NPC Trade] registered=" + registered +
            " offers=" + RebirthNpcTradeOfferRegistry.Count +
            " activeQuotes=" + CountActiveQuotes() +
            " quoted=" + Interlocked.Read(ref quoted) +
            " committed=" + Interlocked.Read(ref committed) +
            " rejected=" + Interlocked.Read(ref rejected) +
            " expired=" + Interlocked.Read(ref expired) +
            " compensated=" + Interlocked.Read(ref compensated) +
            " compensationFailures=" + Interlocked.Read(ref compensationFailures);
    }

    private sealed class TradeHandler : IRebirthNpcInteractionCommandHandler
    {
        public RebirthNpcInteractionCommandKind Kind => RebirthNpcInteractionCommandKind.Trade;
        public int Priority => 100;

        public RebirthNpcInteractionCommandResponse Handle(RebirthNpcInteractionContext context,
            RebirthNpcInteractionCommand command)
        {
            if(!TradeScope())return Reject("Original trade scope is retained");
            Dictionary<string, string> values = Parse(command.Payload);
            string operation;
            if (!values.TryGetValue("operation", out operation))
                return Reject("Trade payload requires operation=quote or operation=commit.");
            if (string.Equals(operation, "catalog", StringComparison.OrdinalIgnoreCase))
                return Catalog(context);
            if (string.Equals(operation, "quote", StringComparison.OrdinalIgnoreCase))
                return Quote(context, values);
            if (string.Equals(operation, "commit", StringComparison.OrdinalIgnoreCase))
                return Commit(context, values);
            return Reject("Unknown trade operation.");
        }
    }


    private static RebirthNpcInteractionCommandResponse Catalog(RebirthNpcInteractionContext context)
    {
        int entityId; RebirthNpcRuntimeState runtime;
        if(!RebirthNpcRuntimeRegistry.TryGetEntityId(context.NpcId,out entityId)||!RebirthNpcRuntimeRegistry.TryGet(entityId,out runtime))return Reject("NPC runtime is unavailable.");
        RebirthNpcTradeOffer[] offers=RebirthNpcTradeOfferRegistry.GetForProfile(runtime.ProfileId);StringBuilder b=new StringBuilder("offers=");
        int count=Math.Min(12,offers.Length);for(int i=0;i<count;i++){if(i>0)b.Append(',');RebirthNpcTradeOffer o=offers[i];b.Append(Escape(o.ItemKey)).Append(':').Append(o.BuyUnitPrice).Append(':').Append(o.SellUnitPrice).Append(':').Append(o.MaximumQuantity).Append(':').Append(Escape(o.CurrencyItemKey));}
        string endpoint;RebirthNpcPlayerInventoryEndpointService.TryGet(context.ActorId,out endpoint);b.Append(";endpoint=").Append(Escape(endpoint??string.Empty));return Accept("Authoritative trade catalogue created.",b.ToString());
    }

    private static RebirthNpcInteractionCommandResponse Quote(RebirthNpcInteractionContext context,
        Dictionary<string, string> values)
    {
        if(context==null||!RebirthNpcTradeReach.CanBegin(context.ActorId,context.NpcId))return Reject("NPC is outside interaction reach.");
        string endpointId, itemKey, directionText, quantityText;
        int quantity;
        RebirthNpcTradeDirection direction;
        if (!TryGet(values, "endpoint", out endpointId)) RebirthNpcPlayerInventoryEndpointService.TryGet(context.ActorId,out endpointId);
        if (string.IsNullOrWhiteSpace(endpointId) || !TryGet(values, "item", out itemKey) ||
            !TryGet(values, "direction", out directionText) || !TryGet(values, "quantity", out quantityText) ||
            !int.TryParse(quantityText, NumberStyles.Integer, CultureInfo.InvariantCulture, out quantity) || quantity <= 0 ||
            !Enum.TryParse(directionText, true, out direction) || !Enum.IsDefined(typeof(RebirthNpcTradeDirection), direction))
            return Reject("Trade quote requires endpoint, item, direction and positive quantity.");
        string actorEndpoint;
        if (!RebirthNpcPlayerInventoryEndpointService.TryGet(context.ActorId, out actorEndpoint) ||
            !string.Equals(endpointId, actorEndpoint, StringComparison.OrdinalIgnoreCase))
            return Reject("Trade endpoint does not belong to the authenticated actor.");
        if (!RebirthNpcExternalInventoryEndpointRegistry.TryResolve(endpointId, out IRebirthNpcExternalInventoryEndpoint endpoint))
            return Reject("Actor inventory endpoint is not registered.");

        RebirthNpcRuntimeState runtime;
        int entityId;
        if (!RebirthNpcRuntimeRegistry.TryGetEntityId(context.NpcId, out entityId) ||
            !RebirthNpcRuntimeRegistry.TryGet(entityId, out runtime))
            return Reject("NPC runtime is unavailable.");
        RebirthNpcTradeOffer offer;
        if (!RebirthNpcTradeOfferRegistry.TryResolve(runtime.ProfileId, itemKey, out offer))
            return Reject("No authority-side trade offer exists for this NPC and item.");
        if (quantity > offer.MaximumQuantity) return Reject("Requested quantity exceeds the offer limit.");
        int unitPrice = direction == RebirthNpcTradeDirection.BuyFromNpc ? offer.BuyUnitPrice : offer.SellUnitPrice;
        if (unitPrice <= 0) return Reject("This trade direction is not offered.");
        long totalLong = (long)quantity * unitPrice;
        if (totalLong <= 0 || totalLong > int.MaxValue) return Reject("Trade total is outside the supported range.");

        RebirthNpcInventorySnapshot npc = RebirthNpcInventoryTransactionService.GetSnapshot(context.NpcId);
        int npcItem = GetAvailableQuantity(npc, itemKey);
        int npcCurrency = GetAvailableQuantity(npc, offer.CurrencyItemKey);
        // A quote describes authority prices and available NPC stock. The
        // server's live player bag/belt replica is not owner payment evidence.
        // Actual owner funds/capacity belong to the frozen native exchange.
        if (direction == RebirthNpcTradeDirection.BuyFromNpc && npcItem < quantity)
            return Reject("NPC stock is insufficient.");
        if (direction == RebirthNpcTradeDirection.SellToNpc && npcCurrency < totalLong)
            return Reject("NPC currency is insufficient.");

        RebirthNpcTradeQuote quote = new RebirthNpcTradeQuote
        {
            QuoteId = Guid.NewGuid(), ActorId = context.ActorId, NpcId = context.NpcId,
            RecoveryGeneration=RebirthNpcRecoveryGeneration.RequireCurrent(),
            Direction = direction, EndpointId = endpointId, ItemKey = itemKey,
            CurrencyItemKey = offer.CurrencyItemKey, Quantity = quantity, UnitPrice = unitPrice,
            TotalPrice = (int)totalLong, ExpectedNpcInventoryRevision = npc.Revision,
            ExpiresClockTicks = DateTime.UtcNow.AddSeconds(20).Ticks
        };
        lock (Sync)
        {
            PruneExpiredLocked();
            if(Quotes.Count>=MaxQuotes)return Reject("Unresolved original quote capacity retained; no quote was evicted");
            if(!HostQuote.TryIssue(quote,out var issued))return Reject("Native trade quotation could not be durably retained.");
            Quotes[quote.QuoteId] = new QuoteRecord { Quote = quote, Issued=issued };
            QuoteOrder.Add(quote.QuoteId);
        }
        Interlocked.Increment(ref quoted);
        return Accept("Authoritative trade quote created.", Serialize(quote));
    }

    private static RebirthNpcInteractionCommandResponse Commit(RebirthNpcInteractionContext context,
        Dictionary<string, string> values)
        =>CommitLocalOriginal(context,values);

    private static Guid Derive(Guid source, byte discriminator)
    {
        byte[] bytes = source.ToByteArray();
        bytes[0] ^= discriminator; bytes[15] ^= (byte)(discriminator * 31);
        return new Guid(bytes);
    }

    private static int GetAvailableQuantity(RebirthNpcInventorySnapshot snapshot, string item)
    {
        int quantity, reserved;
        snapshot.Quantities.TryGetValue(item, out quantity);
        snapshot.Reservations.TryGetValue(item, out reserved);
        return (int)Math.Max(0L,(long)quantity-reserved-(snapshot.NativeStacks?.GetQuantity(item)??0));
    }

    private static string Serialize(RebirthNpcTradeQuote q)
    {
        return "quote=" + q.QuoteId.ToString("N") + ";direction=" + q.Direction +
            ";npc=" + q.NpcId.ToString() + ";actor=" + Escape(q.ActorId) +
            ";recoveryGeneration=" + q.RecoveryGeneration.ToString(CultureInfo.InvariantCulture) +
            ";endpoint=" + Escape(q.EndpointId) + ";item=" + Escape(q.ItemKey) +
            ";currency=" + Escape(q.CurrencyItemKey) + ";quantity=" + q.Quantity.ToString(CultureInfo.InvariantCulture) +
            ";unitPrice=" + q.UnitPrice.ToString(CultureInfo.InvariantCulture) + ";total=" + q.TotalPrice.ToString(CultureInfo.InvariantCulture) +
            ";inventoryRevision=" + q.ExpectedNpcInventoryRevision.ToString(CultureInfo.InvariantCulture) +
            ";expiresUtcTicks=" + q.ExpiresClockTicks.ToString(CultureInfo.InvariantCulture);
    }

    private static Dictionary<string, string> Parse(string payload)
    {
        Dictionary<string, string> result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(payload)) return result;
        string[] parts = payload.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length > 24) return result;
        for (int i = 0; i < parts.Length; i++)
        {
            int at = parts[i].IndexOf('=');
            if (at <= 0) continue;
            string key = parts[i].Substring(0, at).Trim();
            string value = Decode(parts[i].Substring(at + 1).Trim());
            if (key.Length > 0 && key.Length <= 48 && value.Length <= 256) result[key] = value;
        }
        return result;
    }

    private static bool TryGet(Dictionary<string, string> values, string key, out string value)
    { return values.TryGetValue(key, out value) && !string.IsNullOrWhiteSpace(value); }

    private static RebirthNpcInteractionCommandResponse Accept(string detail, string payload)
    {
        return new RebirthNpcInteractionCommandResponse { Status = RebirthNpcInteractionCommandStatus.Accepted,
            Detail = detail, ResponsePayload = payload ?? string.Empty };
    }

    private static RebirthNpcInteractionCommandResponse Reject(string detail)
    {
        Interlocked.Increment(ref rejected);
        return new RebirthNpcInteractionCommandResponse { Status = RebirthNpcInteractionCommandStatus.Rejected,
            Detail = detail ?? string.Empty, ResponsePayload = string.Empty };
    }

    private static string Escape(string value)
    { return (value ?? string.Empty).Replace("%", "%25").Replace(";", "%3B").Replace("=", "%3D").Replace(",", "%2C").Replace(":", "%3A"); }
    private static string Decode(string value)
    { return (value ?? string.Empty).Replace("%3A", ":").Replace("%2C", ",").Replace("%3D", "=").Replace("%3B", ";").Replace("%25", "%"); }

    private static void PruneExpiredLocked()
    {
        if (Quotes.Count == 0) return;
        long now = DateTime.UtcNow.Ticks;
        List<Guid> remove = null;
        foreach (KeyValuePair<Guid, QuoteRecord> pair in Quotes)
        {
            if (CanPruneQuote(pair.Value,now))
            { if (remove == null) remove = new List<Guid>(); remove.Add(pair.Key); }
        }
        if (remove == null) return;
        for (int i = 0; i < remove.Count; i++)
        {
            Quotes.Remove(remove[i]);
            QuoteOrder.Remove(remove[i]);
        }
        Interlocked.Add(ref expired, remove.Count);
    }

    public static void ResetForWorldChange()
    {
        lock(Sync){if(!CanRetireEmptyTrade)throw new InvalidOperationException("Original trade quotes require typed retirement");tradeWorld=null;}
    }

    private static int CountActiveQuotes()
    { lock (Sync) { if(TradeScope(false))PruneExpiredLocked(); return Quotes.Count; } }
}
