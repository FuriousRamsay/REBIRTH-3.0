using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml;

// All adapters below are process-local projection/callback custody only. They
// read no host backing path, acquire no payment/source lease, and invoke no ACK.
internal static partial class RebirthNpcPendingRuntimeStates
{
    internal static RebirthNpcSaveQuarantineDomain PrepareClientRuntimeQuarantine(World originalWorld,uint originalEpoch,uint originalConnection)
    {
        if(!ThreadManager.IsMainThread()||originalWorld?.IsRemote()!=true||(contextWorld!=null&&(!ReferenceEquals(contextWorld,originalWorld)||contextEpoch!=originalEpoch||contextConnection!=originalConnection))||(contextWorld==null&&(Pending.Count!=0||ByEntity.Count!=0)))throw new InvalidOperationException("Pending runtime projection scope unresolved");
        var pending=Pending;var index=ByEntity;var world=contextWorld;uint epoch=contextEpoch,connection=contextConnection;var nextPending=new LinkedList<Snapshot>();var nextIndex=new Dictionary<int,LinkedListNode<Snapshot>>();var doc=QuarantineClientRuntimeImage();
        return new RebirthNpcSaveQuarantineDomain("client-pending-runtime",doc,
            ()=>ThreadManager.IsMainThread()&&ReferenceEquals(Pending,pending)&&ReferenceEquals(ByEntity,index)&&ReferenceEquals(contextWorld,world)&&contextEpoch==epoch&&contextConnection==connection&&QuarantineClientRuntimeImage().OuterXml==doc.OuterXml,
            ()=>{Pending=nextPending;ByEntity=nextIndex;contextWorld=null;contextEpoch=contextConnection=0;},
            ()=>{Pending=pending;ByEntity=index;contextWorld=world;contextEpoch=epoch;contextConnection=connection;});
    }
    private static XmlDocument QuarantineClientRuntimeImage()
    {
        var doc=new XmlDocument{XmlResolver=null};var root=doc.CreateElement("pendingRuntimeQuarantine");doc.AppendChild(root);root.SetAttribute("epoch",contextEpoch.ToString(CultureInfo.InvariantCulture));root.SetAttribute("connection",contextConnection.ToString(CultureInfo.InvariantCulture));if(Pending.Count!=ByEntity.Count)throw new InvalidOperationException("Pending runtime index mismatch");
        foreach(var state in Pending)
        {
            if(state==null||!ByEntity.TryGetValue(state.EntityId,out var node)||!ReferenceEquals(node.Value,state))throw new InvalidOperationException("Pending runtime original missing");var row=doc.CreateElement("actor");root.AppendChild(row);row.SetAttribute("entity",state.EntityId.ToString(CultureInfo.InvariantCulture));row.SetAttribute("stable",state.StableId.ToString());row.SetAttribute("epoch",state.Epoch.ToString(CultureInfo.InvariantCulture));row.SetAttribute("generation",state.Generation.ToString(CultureInfo.InvariantCulture));row.SetAttribute("revision",state.Revision.ToString(CultureInfo.InvariantCulture));row.SetAttribute("presence",((byte)state.Presence).ToString(CultureInfo.InvariantCulture));row.SetAttribute("ownership",((byte)state.Ownership).ToString(CultureInfo.InvariantCulture));row.SetAttribute("owner",state.Owner??string.Empty);row.SetAttribute("order",((byte)state.Order).ToString(CultureInfo.InvariantCulture));row.SetAttribute("travel",((byte)state.Travel).ToString(CultureInfo.InvariantCulture));row.SetAttribute("hasGuard",state.HasGuardPosition?"1":"0");row.SetAttribute("x",state.GuardPosition.x.ToString("R",CultureInfo.InvariantCulture));row.SetAttribute("y",state.GuardPosition.y.ToString("R",CultureInfo.InvariantCulture));row.SetAttribute("z",state.GuardPosition.z.ToString("R",CultureInfo.InvariantCulture));row.SetAttribute("expires",state.Expires.ToBinary().ToString(CultureInfo.InvariantCulture));
        }
        return doc;
    }
}
internal static partial class RebirthNpcPendingEquipmentSnapshots
{
    internal static RebirthNpcSaveQuarantineDomain PrepareClientEquipmentQuarantine(World originalWorld,uint originalEpoch,uint originalConnection)
    {
        if(!ThreadManager.IsMainThread()||originalWorld?.IsRemote()!=true||(context!=null&&(!ReferenceEquals(context,originalWorld)||epoch!=originalEpoch||connection!=originalConnection))||(context==null&&pending.Count!=0))throw new InvalidOperationException("Pending equipment projection scope unresolved");
        var records=pending;var world=context;uint oldEpoch=epoch,oldConnection=connection;var next=new Dictionary<int,Snapshot>();var doc=QuarantineClientEquipmentImage();
        return new RebirthNpcSaveQuarantineDomain("client-pending-equipment",doc,
            ()=>ThreadManager.IsMainThread()&&ReferenceEquals(pending,records)&&ReferenceEquals(context,world)&&epoch==oldEpoch&&connection==oldConnection&&QuarantineClientEquipmentImage().OuterXml==doc.OuterXml,
            ()=>{pending=next;context=null;epoch=connection=0;},
            ()=>{pending=records;context=world;epoch=oldEpoch;connection=oldConnection;});
    }
    private static XmlDocument QuarantineClientEquipmentImage()
    {
        var doc=new XmlDocument{XmlResolver=null};var root=doc.CreateElement("pendingEquipmentQuarantine");doc.AppendChild(root);root.SetAttribute("epoch",epoch.ToString(CultureInfo.InvariantCulture));root.SetAttribute("connection",connection.ToString(CultureInfo.InvariantCulture));var ids=new List<int>(pending.Keys);ids.Sort();foreach(int id in ids)
        {
            var state=pending[id];if(state==null||state.Entity!=id||state.Slots==null)throw new InvalidOperationException("Pending equipment original invalid");var row=doc.CreateElement("actor");root.AppendChild(row);row.SetAttribute("entity",id.ToString(CultureInfo.InvariantCulture));row.SetAttribute("stable",state.Id.ToString());row.SetAttribute("epoch",state.Epoch.ToString(CultureInfo.InvariantCulture));row.SetAttribute("generation",state.Generation.ToString(CultureInfo.InvariantCulture));row.SetAttribute("revision",state.Revision.ToString(CultureInfo.InvariantCulture));row.SetAttribute("expires",state.Expires.ToBinary().ToString(CultureInfo.InvariantCulture));row.SetAttribute("retry",state.Next.ToString("R",CultureInfo.InvariantCulture));var slots=new List<RebirthNpcEquipmentSlot>(state.Slots.Keys);slots.Sort();foreach(var slot in slots){var item=doc.CreateElement("slot");row.AppendChild(item);item.SetAttribute("id",((byte)slot).ToString(CultureInfo.InvariantCulture));item.SetAttribute("value",state.Slots[slot]??string.Empty);}
        }
        return doc;
    }
}
internal static partial class RebirthNpcPendingAppearanceSnapshots
{
    internal static RebirthNpcSaveQuarantineDomain PrepareClientAppearanceQuarantine(World originalWorld,uint originalEpoch,uint originalConnection)
    {
        if(!ThreadManager.IsMainThread()||originalWorld?.IsRemote()!=true||(context!=null&&(!ReferenceEquals(context,originalWorld)||epoch!=originalEpoch||connection!=originalConnection))||(context==null&&Pending.Count!=0))throw new InvalidOperationException("Pending appearance projection scope unresolved");
        var records=Pending;var world=context;uint oldEpoch=epoch,oldConnection=connection;int oldCursor=cursor;var next=new Dictionary<int,Snapshot>();var doc=QuarantineClientAppearanceImage();
        return new RebirthNpcSaveQuarantineDomain("client-pending-appearance",doc,
            ()=>ThreadManager.IsMainThread()&&ReferenceEquals(Pending,records)&&ReferenceEquals(context,world)&&epoch==oldEpoch&&connection==oldConnection&&cursor==oldCursor&&QuarantineClientAppearanceImage().OuterXml==doc.OuterXml,
            ()=>{Pending=next;context=null;epoch=connection=0;cursor=0;},
            ()=>{Pending=records;context=world;epoch=oldEpoch;connection=oldConnection;cursor=oldCursor;});
    }
    private static XmlDocument QuarantineClientAppearanceImage()
    {
        var doc=new XmlDocument{XmlResolver=null};var root=doc.CreateElement("pendingAppearanceQuarantine");doc.AppendChild(root);root.SetAttribute("epoch",epoch.ToString(CultureInfo.InvariantCulture));root.SetAttribute("connection",connection.ToString(CultureInfo.InvariantCulture));root.SetAttribute("cursor",cursor.ToString(CultureInfo.InvariantCulture));var ids=new List<int>(Pending.Keys);ids.Sort();foreach(int id in ids)
        {
            var state=Pending[id];if(state==null||state.Entity!=id)throw new InvalidOperationException("Pending appearance original invalid");var row=doc.CreateElement("actor");root.AppendChild(row);row.SetAttribute("entity",id.ToString(CultureInfo.InvariantCulture));row.SetAttribute("stable",state.Stable.ToString());row.SetAttribute("epoch",state.Epoch.ToString(CultureInfo.InvariantCulture));row.SetAttribute("generation",state.Generation.ToString(CultureInfo.InvariantCulture));row.SetAttribute("revision",state.Revision.ToString(CultureInfo.InvariantCulture));row.SetAttribute("semanticRevision",state.SemanticRevision.ToString(CultureInfo.InvariantCulture));row.SetAttribute("expires",state.Expires.ToBinary().ToString(CultureInfo.InvariantCulture));row.SetAttribute("retry",state.Retry.ToBinary().ToString(CultureInfo.InvariantCulture));
            using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream)){RebirthNpcAppearanceBinaryCodec.Write(writer,state.Appearance);writer.Flush();row.SetAttribute("appearance",Convert.ToBase64String(stream.ToArray()));}
            if(state.Equipment==null){row.SetAttribute("equipment","absent");continue;}var equipment=doc.CreateElement("equipment");row.AppendChild(equipment);for(int slot=0;slot<state.Equipment.GetSlotCount();slot++){var stack=state.Equipment.ItemGrid[slot];var item=doc.CreateElement("slot");equipment.AppendChild(item);item.SetAttribute("id",slot.ToString(CultureInfo.InvariantCulture));item.SetAttribute("count",(stack?.count??0).ToString(CultureInfo.InvariantCulture));item.SetAttribute("stackNull",stack==null?"1":"0");item.SetAttribute("value",stack?.itemValue==null?string.Empty:RebirthNativeItemCodec.Encode(stack.itemValue));}var cosmetics=state.Equipment.GetCosmeticIDs();for(int slot=0;slot<cosmetics.Length;slot++){var item=doc.CreateElement("cosmetic");equipment.AppendChild(item);item.SetAttribute("slot",slot.ToString(CultureInfo.InvariantCulture));item.SetAttribute("id",cosmetics[slot].ToString(CultureInfo.InvariantCulture));}foreach(int cosmetic in state.Equipment.m_unlockedCosmetics){var item=doc.CreateElement("unlocked");equipment.AppendChild(item);item.SetAttribute("id",cosmetic.ToString(CultureInfo.InvariantCulture));}
        }
        return doc;
    }
}
public static partial class RebirthNpcInteractionNetworkClient
{
    internal static RebirthNpcSaveQuarantineDomain PrepareInteractionCallbackQuarantine(World originalWorld,uint originalConnection)
    {
        lock(Sync)
        {
            if(!ThreadManager.IsMainThread()||originalWorld==null)throw new InvalidOperationException("Callback retention requires original native world");foreach(var request in Pending.Values)if(request==null||!ReferenceEquals(request.World,originalWorld)||request.ConnectionEpoch!=originalConnection)throw new InvalidOperationException("Interaction callback belongs to another connection/world");
            var records=Pending;var next=new Dictionary<ulong,PendingRequest>();var doc=QuarantineInteractionCallbackImage();
            string callbackImage=doc.OuterXml;
            var trade=new[]{RebirthNpcTradeClientFlow.PrepareQuarantine(originalWorld),RebirthNpcTradeOwnerExchange.PrepareQuarantine(originalWorld),RebirthNpcTradeOwnerLoad.PrepareQuarantine(originalWorld)};
            foreach(var domain in trade)doc.DocumentElement.AppendChild(domain.CopyEvidence(doc));
            return new RebirthNpcSaveQuarantineDomain("interaction-callbacks",doc,
                ()=>{lock(Sync)return ReferenceEquals(Pending,records)&&QuarantineInteractionCallbackImage().OuterXml==callbackImage&&Array.TrueForAll(trade,value=>value.IsCurrent);},
                ()=>{lock(Sync){foreach(var domain in trade)domain.Detach();Pending=next;}},
                ()=>{lock(Sync){var errors=new List<Exception>();for(int i=trade.Length-1;i>=0;i--)try{trade[i].Rollback();}catch(Exception error){errors.Add(error);}Pending=records;if(errors.Count!=0)throw new AggregateException(errors);}});
        }
    }
    private static XmlDocument QuarantineInteractionCallbackImage()
    {
        var doc=new XmlDocument{XmlResolver=null};var root=doc.CreateElement("interactionCallbackQuarantine");doc.AppendChild(root);
        var ids=new List<ulong>(Pending.Keys);ids.Sort();
        foreach(var id in ids)
        {
            var request=Pending[id];if(request==null)throw new InvalidOperationException("Interaction callback missing");
            var row=doc.CreateElement("request");root.AppendChild(row);
            row.SetAttribute("id",id.ToString(CultureInfo.InvariantCulture));row.SetAttribute("connection",request.ConnectionEpoch.ToString(CultureInfo.InvariantCulture));
            row.SetAttribute("generation",request.Generation.ToString(CultureInfo.InvariantCulture));row.SetAttribute("deadline",request.DeadlineUtcTicks.ToString(CultureInfo.InvariantCulture));
            row.SetAttribute("callbackRetained",request.Callback!=null?"1":"0");
            if(request.NativeTradeOriginal!=null)
            {
                var original=request.NativeTradeOriginal;
                if(request.NativeTradeOperation!="prepareNative"&&request.NativeTradeOperation!="settleNative"&&request.NativeTradeOperation!="ackNative"||
                    request.Generation!=original.CopyQuote().RecoveryGeneration||request.NativeTradeOperation=="ackNative"&&request.NativeTradeTerminal?.Length!=64)
                    throw new InvalidOperationException("Native trade callback purpose incomplete");
                row.SetAttribute("nativeTradeOperation",request.NativeTradeOperation);row.SetAttribute("nativeTradeDigest",original.Digest);
                row.SetAttribute("nativeTradeTerminal",request.NativeTradeTerminal??string.Empty);
                var body=doc.CreateElement("nativeTradeOriginal");row.AppendChild(body);body.InnerText=original.Image;
            }
            else if(request.NativeTradeOperation!=null||request.NativeTradeTerminal!=null)
                throw new InvalidOperationException("Native trade callback original missing");
        }
        return doc;
    }
}
