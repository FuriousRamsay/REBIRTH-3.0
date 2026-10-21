using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml;

public static partial class RebirthNpcCombatEmergencyService
{
    // Original tactical order objects stay owned by this capsule; the detached
    // evidence contains every field, including fields absent from the save XML.
    internal static RebirthNpcSaveQuarantineDomain PrepareCombatQuarantine()
    {
        lock(Sync)
        {
            var states=States;var orders=Orders;var finalized=FinalizedDeathTransactions;var epoch=recoveryEpoch;
            var nextStates=new Dictionary<RebirthNpcStableId,RebirthNpcCombatStateRecord>();
            var nextOrders=new Dictionary<Guid,RebirthNpcTacticalOrder>();var nextFinalized=new HashSet<string>(StringComparer.Ordinal);var nextEpoch=new object();
            var doc=QuarantineCombatImage();
            return new RebirthNpcSaveQuarantineDomain("combat-service",doc,
                ()=>{lock(Sync)return ReferenceEquals(States,states)&&ReferenceEquals(Orders,orders)&&ReferenceEquals(FinalizedDeathTransactions,finalized)&&ReferenceEquals(recoveryEpoch,epoch)&&QuarantineCombatImage().OuterXml==doc.OuterXml;},
                ()=>{lock(Sync){States=nextStates;Orders=nextOrders;FinalizedDeathTransactions=nextFinalized;recoveryEpoch=nextEpoch;}},
                ()=>{lock(Sync){States=states;Orders=orders;FinalizedDeathTransactions=finalized;recoveryEpoch=epoch;}});
        }
    }
    private static XmlDocument QuarantineCombatImage()
    {
        var persisted=RebirthNpcCombatPersistenceStore.QuarantineImage();
        var doc=new XmlDocument{XmlResolver=null};var root=doc.CreateElement("combatQuarantine");doc.AppendChild(root);root.AppendChild(doc.ImportNode(persisted.DocumentElement,true));
        var sorted=new List<Guid>(Orders.Keys);sorted.Sort();var rows=doc.CreateElement("orders");root.AppendChild(rows);
        foreach(var id in sorted)
        {
            var order=Orders[id];
            if(order==null||order.OrderId!=id||id==Guid.Empty||order.NpcId.IsEmpty||!Enum.IsDefined(typeof(RebirthNpcTacticalOrderKind),order.Kind)||!Finite(order.TargetPosition.x)||!Finite(order.TargetPosition.y)||!Finite(order.TargetPosition.z))throw new InvalidOperationException("Unresolved combat order cannot detach");
            var row=doc.CreateElement("order");rows.AppendChild(row);row.SetAttribute("id",id.ToString("N"));row.SetAttribute("npc",order.NpcId.ToString());row.SetAttribute("kind",((byte)order.Kind).ToString(CultureInfo.InvariantCulture));row.SetAttribute("target",order.TargetNpcId.ToString());
            row.SetAttribute("x",order.TargetPosition.x.ToString("R",CultureInfo.InvariantCulture));row.SetAttribute("y",order.TargetPosition.y.ToString("R",CultureInfo.InvariantCulture));row.SetAttribute("z",order.TargetPosition.z.ToString("R",CultureInfo.InvariantCulture));row.SetAttribute("issuer",order.IssuerKey??string.Empty);row.SetAttribute("created",order.CreatedUtcTicks.ToString(CultureInfo.InvariantCulture));row.SetAttribute("expires",order.ExpiresUtcTicks.ToString(CultureInfo.InvariantCulture));row.SetAttribute("expectedRevision",order.ExpectedRevision.ToString(CultureInfo.InvariantCulture));
        }
        return doc;
    }
    private static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
}
public static partial class RebirthNpcCombatPersistenceStore
{
    internal static XmlDocument QuarantineImage()
    {
        RebirthNpcCombatEmergencyService.CaptureRetirement(out var states,out var transactions,out _,out _);
        var doc=SnapshotDocument(states,transactions);if(!ValidateDocument(doc))throw new InvalidOperationException("Combat quarantine image invalid");return doc;
    }
    internal static bool TryPrepareQuarantine(RebirthNpcSaveQuarantineScope scope,List<RebirthNpcSaveQuarantineDomain> domains)
    {
        lock(Sync)
        {
            RebirthNpcCombatEmergencyService.CaptureRetirement(out var states,out var transactions,out _,out var orders);
            if(!scope.MatchesBinding(boundWorld,boundPath,loaded||dirty||states.Length!=0||transactions.Length!=0||orders))return false;
            var world=boundWorld;string path=boundPath;var binding=originalBinding;bool priorLoaded=loaded,priorDirty=dirty;
            var doc=new XmlDocument{XmlResolver=null};var root=doc.CreateElement("combatBinding");doc.AppendChild(root);root.SetAttribute("loaded",loaded?"1":"0");root.SetAttribute("dirty",dirty?"1":"0");root.SetAttribute("path",path??string.Empty);
            var service=RebirthNpcCombatEmergencyService.PrepareCombatQuarantine();
            var store=new RebirthNpcSaveQuarantineDomain("combat-binding",doc,
                ()=>{lock(Sync)return loaded==priorLoaded&&dirty==priorDirty&&ReferenceEquals(boundWorld,world)&&boundPath==path&&ReferenceEquals(originalBinding,binding);},
                ()=>{lock(Sync){loaded=false;dirty=false;boundWorld=null;boundPath=null;originalBinding=null;}},
                ()=>{lock(Sync){loaded=priorLoaded;dirty=priorDirty;boundWorld=world;boundPath=path;originalBinding=binding;}});
            domains.Add(service);domains.Add(store);return true;
        }
    }
}
