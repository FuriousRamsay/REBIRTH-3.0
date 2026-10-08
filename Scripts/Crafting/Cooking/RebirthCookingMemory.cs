using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml.Linq;

/// <summary>Per-character, per-save preparation; UTC expiry prevents relogging from renewing it.</summary>
public sealed class RebirthCookingMemory
{
    public sealed class Ticket
    {
        public string Recipe;
        public string StationPosition="";
        public int Portions, OutputCount;
        public float XpMultiplier;
        public Ticket Clone() => (Ticket)MemberwiseClone();
    }
    private List<string> activeStationTokens;
    private int indexedTicketCount = -1;
    private IList<string> activeStationView;
    public long JobRevision { get; private set; }
    public void JobsChanged() { activeStationTokens = null; unchecked { JobRevision++; } }
    public bool IsKnownBatch(string token) { return token != null && (Tickets.ContainsKey(token) || Awards.ContainsKey(token)); }
    // Only the authoritative maintenance owner calls this index; HUD reads use published XML.
    public IList<string> ActiveStationTokens()
    {
        if (activeStationTokens == null || indexedTicketCount != Tickets.Count)
        {
            activeStationTokens = new List<string>();
            foreach (var pair in Tickets) if (pair.Value != null && !string.IsNullOrEmpty(pair.Value.StationPosition)) activeStationTokens.Add(pair.Key);
            indexedTicketCount = Tickets.Count;
            activeStationView = activeStationTokens.AsReadOnly();
        }
        return activeStationView;
    }
    public bool RetireStation(string token, bool cancelled)
    {
        Ticket ticket;
        if (token == null || !Tickets.TryGetValue(token, out ticket) || ticket == null) return false;
        int awarded; Awards.TryGetValue(token, out awarded);
        if (cancelled || awarded >= ticket.Portions)
        {
            // Keep a compact permanent tombstone in the existing award schema. int.MaxValue
            // also fails closed in the old Acknowledge comparison if a duplicate is registered.
            Awards[token] = int.MaxValue; Tickets.Remove(token);
        }
        else ticket.StationPosition = string.Empty; // Unsettled native completion receipt retained.
        JobsChanged(); return true;
    }
    public bool CompactTerminalHistory()
    {
        var terminal = new List<string>();
        foreach (var pair in Tickets)
        {
            int awarded;
            if (pair.Value != null && string.IsNullOrEmpty(pair.Value.StationPosition)
                && Awards.TryGetValue(pair.Key, out awarded) && awarded >= pair.Value.Portions) terminal.Add(pair.Key);
        }
        foreach (string token in terminal) RetireStation(token, false);
        return terminal.Count != 0;
    }
    public readonly HashSet<string> PendingUnlocks = new HashSet<string>(StringComparer.Ordinal);
    public readonly Dictionary<string, Ticket> Tickets = new Dictionary<string, Ticket>(StringComparer.Ordinal);
    public sealed class Ready
    {
        public string Book = "", Magazine = "";
        public long Expires;
        public Ready Clone() => (Ready)MemberwiseClone();
        public float Remaining => (float)Math.Max(0, Math.Min(300, (Expires - DateTime.UtcNow.Ticks) / (double)TimeSpan.TicksPerSecond));
    }
    public readonly Dictionary<string, Ready> Recipes = new Dictionary<string, Ready>(StringComparer.Ordinal);
    // Highest completed portion per queued batch; prevents retry/alternate-hook double awards.
    public readonly Dictionary<string, int> Awards = new Dictionary<string, int>(StringComparer.Ordinal);
    public bool Acknowledge(string token,string recipe,int outputs,int completed,out Ticket ticket,out int newPortions)
    {
        newPortions=0;ticket=null;
        if(token==null||!Tickets.TryGetValue(token,out ticket)||ticket.Recipe!=recipe||ticket.OutputCount!=outputs||completed<1||completed>ticket.Portions)return false;
        Awards.TryGetValue(token,out int previous);
        if(completed<=previous)return false;
        newPortions=completed-previous;Awards[token]=completed;
        if (completed == ticket.Portions && string.IsNullOrEmpty(ticket.StationPosition))
        { Awards[token] = int.MaxValue; Tickets.Remove(token); JobsChanged(); }
        return true;
    }
    public RebirthCookingMemory Clone()
    {
        var copy = new RebirthCookingMemory();
        foreach(string name in PendingUnlocks)copy.PendingUnlocks.Add(name);
        foreach (var p in Recipes) copy.Recipes[p.Key] = p.Value.Clone();
        foreach (var p in Awards) copy.Awards[p.Key] = p.Value;
        foreach (var p in Tickets) copy.Tickets[p.Key] = p.Value.Clone();
        return copy;
    }
    public XElement Write()
    {
        var node = new XElement("cooking");
        foreach(string name in PendingUnlocks)node.Add(new XElement("unlock-notice",new XAttribute("recipe",name)));
        foreach (var p in Recipes)
            if (p.Value.Remaining > 0) node.Add(new XElement("prepared", new XAttribute("recipe", p.Key), new XAttribute("book", p.Value.Book ?? ""), new XAttribute("magazine", p.Value.Magazine ?? ""), new XAttribute("expires", p.Value.Expires)));
        foreach (var p in Awards) node.Add(new XElement("award", new XAttribute("batch", p.Key), new XAttribute("count", p.Value)));
        foreach (var p in Tickets) node.Add(new XElement("batch", new XAttribute("id",p.Key), new XAttribute("recipe",p.Value.Recipe),new XAttribute("portions",p.Value.Portions),new XAttribute("outputs",p.Value.OutputCount),new XAttribute("xp",p.Value.XpMultiplier),new XAttribute("station",p.Value.StationPosition??"")));
        return node;
    }
    public static RebirthCookingMemory Read(XElement node)
    {
        var result = new RebirthCookingMemory();
        if (node == null) return result;
        foreach(var e in node.Elements("unlock-notice"))
        {string name=(string)e.Attribute("recipe");if(!string.IsNullOrEmpty(name)&&name.Length<=256)result.PendingUnlocks.Add(name);}
        foreach (var e in node.Elements("prepared"))
        {
            string recipe = (string)e.Attribute("recipe");
            if (string.IsNullOrEmpty(recipe) || !long.TryParse((string)e.Attribute("expires"), NumberStyles.Integer, CultureInfo.InvariantCulture, out long expiry)) continue;
            var ready = new Ready { Book = (string)e.Attribute("book") ?? "", Magazine = (string)e.Attribute("magazine") ?? "", Expires = expiry };
            if (ready.Remaining > 0) result.Recipes[recipe] = ready;
        }
        foreach (var e in node.Elements("award"))
            if (!string.IsNullOrEmpty((string)e.Attribute("batch")) && int.TryParse((string)e.Attribute("count"), out int count) && count > 0) result.Awards[(string)e.Attribute("batch")] = count;
        foreach(var e in node.Elements("batch"))
            if(Guid.TryParse((string)e.Attribute("id"),out _) && int.TryParse((string)e.Attribute("portions"),out int portions) && portions>0 && portions<=9999 && int.TryParse((string)e.Attribute("outputs"),out int outputs) && outputs>0 && float.TryParse((string)e.Attribute("xp"),NumberStyles.Float,CultureInfo.InvariantCulture,out float xp))
                result.Tickets[(string)e.Attribute("id")]=new Ticket{Recipe=(string)e.Attribute("recipe"),StationPosition=(string)e.Attribute("station")??"",Portions=portions,OutputCount=outputs,XpMultiplier=Math.Max(1,Math.Min(1.2f,xp))};
        return result;
    }
}
