using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml;

internal static partial class RebirthNpcRecoveryProjectionNetwork
{
    internal static RebirthNpcSaveQuarantineDomain PrepareProjectionMailboxQuarantine(World originalWorld,string originalDirectory)
    {
        if(!ThreadManager.IsMainThread()||originalWorld==null||(context!=null&&(!ReferenceEquals(context,originalWorld)||!string.Equals(directory,originalDirectory,StringComparison.OrdinalIgnoreCase)))||(context==null&&(Deliveries.Count!=0||Pending.Count!=0||Names.Count!=0)))throw new InvalidOperationException("Recovery mailbox original scope unresolved");
        var deliveries=Deliveries;var pending=Pending;var names=Names;var world=context;string save=directory;uint epoch=clientEpoch,connection=clientConnection,cursor=sendCursor;ulong nextSequence=sequence;int clients=clientCursor,nameIndex=nameCursor;
        var nextDeliveries=new Dictionary<string,Delivery>(StringComparer.Ordinal);var nextPending=new Dictionary<int,Probe>();var nextNames=new Dictionary<int,NameImage>();var doc=QuarantineMailboxImage();
        return new RebirthNpcSaveQuarantineDomain("recovery-projection-mailbox",doc,
            ()=>ThreadManager.IsMainThread()&&ReferenceEquals(Deliveries,deliveries)&&ReferenceEquals(Pending,pending)&&ReferenceEquals(Names,names)&&ReferenceEquals(context,world)&&directory==save&&clientEpoch==epoch&&clientConnection==connection&&sendCursor==cursor&&sequence==nextSequence&&clientCursor==clients&&nameCursor==nameIndex&&QuarantineMailboxImage().OuterXml==doc.OuterXml,
            ()=>{Deliveries=nextDeliveries;Pending=nextPending;Names=nextNames;context=null;directory=null;clientEpoch=0;clientConnection=0;sendCursor=0;sequence=0;clientCursor=0;nameCursor=0;},
            ()=>{Deliveries=deliveries;Pending=pending;Names=names;context=world;directory=save;clientEpoch=epoch;clientConnection=connection;sendCursor=cursor;sequence=nextSequence;clientCursor=clients;nameCursor=nameIndex;});
    }
    private static XmlDocument QuarantineMailboxImage()
    {
        var doc=new XmlDocument{XmlResolver=null};var root=doc.CreateElement("recoveryMailboxQuarantine");doc.AppendChild(root);root.SetAttribute("directory",directory??string.Empty);root.SetAttribute("epoch",clientEpoch.ToString(CultureInfo.InvariantCulture));root.SetAttribute("connection",clientConnection.ToString(CultureInfo.InvariantCulture));root.SetAttribute("sequence",sequence.ToString(CultureInfo.InvariantCulture));
        var keys=new List<string>(Deliveries.Keys);keys.Sort(StringComparer.Ordinal);var rows=doc.CreateElement("deliveries");root.AppendChild(rows);foreach(string key in keys){var delivery=Deliveries[key];if(delivery==null||delivery.Peer==null)throw new InvalidOperationException("Original recovery recipient missing");var row=doc.CreateElement("delivery");rows.AppendChild(row);row.SetAttribute("key",key);row.SetAttribute("peer",delivery.Peer.PlatformId?.ToString()??string.Empty);row.SetAttribute("entity",delivery.Peer.entityId.ToString(CultureInfo.InvariantCulture));row.SetAttribute("ack",delivery.Acknowledged?"1":"0");row.SetAttribute("retry",delivery.Retry.ToString("R",CultureInfo.InvariantCulture));row.AppendChild(QuarantineProbe(doc,delivery.Probe));}
        var ids=new List<int>(Pending.Keys);ids.Sort();rows=doc.CreateElement("pending");root.AppendChild(rows);foreach(int id in ids){var row=doc.CreateElement("actor");row.SetAttribute("id",id.ToString(CultureInfo.InvariantCulture));row.AppendChild(QuarantineProbe(doc,Pending[id]));rows.AppendChild(row);}
        ids=new List<int>(Names.Keys);ids.Sort();rows=doc.CreateElement("names");root.AppendChild(rows);foreach(int id in ids){var image=Names[id];if(image==null)throw new InvalidOperationException("Name image missing");var row=doc.CreateElement("name");rows.AppendChild(row);row.SetAttribute("key",id.ToString(CultureInfo.InvariantCulture));row.SetAttribute("entity",image.Entity.ToString(CultureInfo.InvariantCulture));row.SetAttribute("stable",image.Stable.ToString());row.SetAttribute("generation",image.Generation.ToString(CultureInfo.InvariantCulture));row.SetAttribute("sequence",image.Sequence.ToString(CultureInfo.InvariantCulture));row.SetAttribute("value",image.Name??string.Empty);}return doc;
    }
    private static XmlElement QuarantineProbe(XmlDocument doc,Probe probe)
    {
        if(probe==null)throw new InvalidOperationException("Original recovery probe missing");var row=doc.CreateElement("probe");row.SetAttribute("operation",probe.Operation.ToString("N"));row.SetAttribute("challenge",probe.Challenge.ToString("N"));row.SetAttribute("generation",probe.Generation.ToString(CultureInfo.InvariantCulture));row.SetAttribute("sequence",probe.Sequence.ToString(CultureInfo.InvariantCulture));row.SetAttribute("nameSequence",probe.NameSequence.ToString(CultureInfo.InvariantCulture));row.SetAttribute("entity",probe.Entity.ToString(CultureInfo.InvariantCulture));row.SetAttribute("stable",probe.Stable.ToString());row.SetAttribute("epoch",probe.Epoch.ToString(CultureInfo.InvariantCulture));row.SetAttribute("connection",probe.Connection.ToString(CultureInfo.InvariantCulture));row.SetAttribute("revision",probe.Revision.ToString(CultureInfo.InvariantCulture));row.SetAttribute("digest",probe.Digest??string.Empty);row.SetAttribute("name",probe.Name??string.Empty);return row;
    }
}
