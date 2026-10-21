using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml;

public static partial class RebirthNpcNativeEquipmentBridge
{
    internal static RebirthNpcSaveQuarantineDomain PrepareNativeProjectionQuarantine(World originalWorld,string originalDirectory)
    {
        lock(Sync)
        {
            if(originalWorld==null||(projectionWorld!=null&&(!ReferenceEquals(projectionWorld,originalWorld)||!RebirthNpcSaveQuarantineScope.SameDirectory(projectionDirectory,originalDirectory)))||(projectionWorld==null&&Applied.Count!=0))throw new InvalidOperationException("Native equipment projection original scope unresolved");
            var applied=Applied;var world=projectionWorld;string directory=projectionDirectory;ulong floor=generationFloor;var next=new Dictionary<RebirthNpcStableId,ProjectionState>();var doc=QuarantineNativeProjectionImage();
            return new RebirthNpcSaveQuarantineDomain("native-equipment-projection",doc,
                ()=>{lock(Sync)return ReferenceEquals(Applied,applied)&&ReferenceEquals(projectionWorld,world)&&projectionDirectory==directory&&generationFloor==floor&&QuarantineNativeProjectionImage().OuterXml==doc.OuterXml;},
                ()=>{lock(Sync){Applied=next;projectionWorld=null;projectionDirectory=null;generationFloor=0;}},
                ()=>{lock(Sync){Applied=applied;projectionWorld=world;projectionDirectory=directory;generationFloor=floor;}});
        }
    }
    private static XmlDocument QuarantineNativeProjectionImage()
    {
        var doc=new XmlDocument{XmlResolver=null};var root=doc.CreateElement("nativeEquipmentProjectionQuarantine");doc.AppendChild(root);root.SetAttribute("directory",projectionDirectory??string.Empty);root.SetAttribute("floor",generationFloor.ToString(CultureInfo.InvariantCulture));
        var ids=new List<RebirthNpcStableId>(Applied.Keys);ids.Sort((a,b)=>string.CompareOrdinal(a.ToString(),b.ToString()));foreach(var id in ids)
        {
            var state=Applied[id];if(state==null||id.IsEmpty)throw new InvalidOperationException("Native equipment projection identity unresolved");var row=doc.CreateElement("npc");root.AppendChild(row);row.SetAttribute("id",id.ToString());row.SetAttribute("generation",state.Generation.ToString(CultureInfo.InvariantCulture));row.SetAttribute("converged",state.Converged?"1":"0");row.SetAttribute("completed",state.Completed?"1":"0");row.SetAttribute("requestedRevision",state.RequestedRevision.ToString(CultureInfo.InvariantCulture));row.SetAttribute("appliedRevision",state.AppliedRevision.ToString(CultureInfo.InvariantCulture));
            CopyQuarantineSlots(doc,row,"requested",state.RequestedSlots);CopyQuarantineSlots(doc,row,"applied",state.AppliedSlots);
        }
        return doc;
    }
    private static void CopyQuarantineSlots(XmlDocument doc,XmlElement parent,string name,Dictionary<RebirthNpcEquipmentSlot,string> slots)
    {var rows=doc.CreateElement(name);parent.AppendChild(rows);var ids=new List<RebirthNpcEquipmentSlot>(slots.Keys);ids.Sort();foreach(var id in ids){var row=doc.CreateElement("slot");rows.AppendChild(row);row.SetAttribute("id",((byte)id).ToString(CultureInfo.InvariantCulture));row.SetAttribute("item",slots[id]??string.Empty);}}
}
