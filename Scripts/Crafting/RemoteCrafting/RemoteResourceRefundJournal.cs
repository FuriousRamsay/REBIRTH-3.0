using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;

// Save-before-publication ledger. Persistence and native owner evidence are supplied by
// authenticated authority adapters; callbacks returning true alone are not native proof.
internal sealed class RemoteResourceRefundJournal
{
    internal const int MaxEntries=128;
    internal const int MaxEncodedText=4194304;
    private sealed class Entry {internal RemoteResourceRefundRecord Record;internal bool Settled;}
    private List<Entry> entries=new List<Entry>();
    private readonly string world,owner,creation;
    private bool busy;
    private RemoteResourceRefundJournal(string world,string owner,string creation)
    {this.world=world;this.owner=owner;this.creation=creation;}
    private static string Key(RemoteResourceRefundRecord r)
        =>r.SessionEpoch.ToString(CultureInfo.InvariantCulture)+"/"+r.RequestId.ToString(CultureInfo.InvariantCulture);
    internal static bool TryCreate(string world,string owner,string creation,out RemoteResourceRefundJournal journal)
    {
        journal=null;
        if(!RemoteResourceRefundRecord.ValidStorageKey(world)||!RemoteResourceRefundRecord.ValidStorageKey(owner)||
            !RebirthSurvivorRequestScope.TryNormalize(creation,out var normalized))return false;
        journal=new RemoteResourceRefundJournal(world,owner,normalized);return true;
    }
    private XElement Write(List<Entry> source)
        =>new XElement("remoteResourceRefunds",new XAttribute("version",1),new XAttribute("world",world),
            new XAttribute("owner",owner),new XAttribute("creation",creation),
            source.Select(e=>new XElement("entry",new XAttribute("settled",e.Settled),e.Record.ToXml())));
    internal XElement ToXml()=>Write(entries);
    internal RemoteResourceRefundRecord[] Pending()=>entries.Where(e=>!e.Settled).Select(e=>e.Record).ToArray();
    private static bool Budget(List<Entry> source)
    {
        if(source.Count>MaxEntries)return false;
        long size=0;
        foreach(var e in source)foreach(var item in e.Record.ToXml().Elements())
        {size+=((string)item.Attribute("data")).Length;if(size>MaxEncodedText)return false;}
        return true;
    }
    internal static bool TryRead(XElement node,string world,string owner,string creation,out RemoteResourceRefundJournal journal)
    {
        journal=null;
        try
        {
            if(!TryCreate(world,owner,creation,out var candidate)||node==null||node.Name!="remoteResourceRefunds"||
                node.Attributes().Count()!=4||(string)node.Attribute("version")!="1"||
                (string)node.Attribute("world")!=candidate.world||(string)node.Attribute("owner")!=candidate.owner||
                (string)node.Attribute("creation")!=candidate.creation||node.Elements().Count()>MaxEntries||
                node.Nodes().Any(n=>!(n is XElement)&&(!(n is XText)||!string.IsNullOrWhiteSpace(((XText)n).Value))))return false;
            var keys=new HashSet<string>(StringComparer.Ordinal);
            foreach(var entry in node.Elements())
            {
                if(entry.Name!="entry"||entry.Attributes().Count()!=1||
                    !bool.TryParse((string)entry.Attribute("settled"),out bool settled)||entry.Elements().Count()!=1||
                    entry.Nodes().Any(n=>!(n is XElement)&&(!(n is XText)||!string.IsNullOrWhiteSpace(((XText)n).Value)))||
                    !RemoteResourceRefundRecord.TryRead(entry.Elements().Single(),world,owner,candidate.creation,out var record)||
                    !keys.Add(Key(record)))return false;
                candidate.entries.Add(new Entry{Record=record,Settled=settled});
            }
            if(!Budget(candidate.entries))return false;
            journal=candidate;return true;
        }
        catch{return false;}
    }
    private bool Publish(List<Entry> candidate,Func<XElement,bool> save,Func<bool> isCurrent)
    {
        if(busy||save==null||isCurrent==null||!Budget(candidate))return false;
        busy=true;
        try
        {
            if(!isCurrent()||!save(Write(candidate))||!isCurrent())return false;
            entries=candidate;return true;
        }
        catch{return false;}
        finally{busy=false;}
    }
    internal bool TryRegister(RemoteResourceRefundRecord record,Func<XElement,bool> save,Func<bool> isCurrent)
    {
        if(busy||record==null||save==null||isCurrent==null)return false;
        try
        {
            if(!isCurrent()||!RemoteResourceRefundRecord.TryRead(record.ToXml(),world,owner,creation,out var valid))return false;
            var existing=entries.FirstOrDefault(e=>Key(e.Record)==Key(valid));
            // Settled identities are retained too: a duplicate can never re-credit items.
            if(existing!=null)return XNode.DeepEquals(existing.Record.ToXml(),valid.ToXml())&&isCurrent();
            var candidate=new List<Entry>(entries){new Entry{Record=valid}};
            return Publish(candidate,save,isCurrent);
        }
        catch{return false;}
    }
    internal bool TrySettle(RemoteResourceRefundRecord record,Func<RemoteResourceRefundRecord,bool> verifySavedOwner,
        Func<XElement,bool> save,Func<bool> isCurrent)
    {
        if(busy||record==null||save==null||isCurrent==null||verifySavedOwner==null)return false;
        busy=true;
        List<Entry> candidate;
        try
        {
            if(!isCurrent())return false;
            var existing=entries.FirstOrDefault(e=>Key(e.Record)==Key(record));
            if(existing==null||!XNode.DeepEquals(existing.Record.ToXml(),record.ToXml())||
                !verifySavedOwner(existing.Record)||!isCurrent())return false;
            if(existing.Settled)return true;
            candidate=entries.Select(e=>new Entry{Record=e.Record,Settled=e.Settled||ReferenceEquals(e,existing)}).ToList();
        }
        catch{return false;}
        finally{busy=false;}
        return Publish(candidate,save,isCurrent);
    }
}