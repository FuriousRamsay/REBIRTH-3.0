using System;
using System.Collections.Generic;
using System.Xml;

public static partial class RebirthNpcSocialService
{
    internal static RebirthNpcSaveQuarantineDomain PrepareSocialQuarantine()
    {
        lock(Sync)
        {
            var relationships=Relationships;var memories=Memories;var events=AppliedEvents;var order=AppliedEventOrder;var epoch=recoveryEpoch;long decay=nextDecay;
            var nextRelationships=new Dictionary<string,RebirthNpcRelationship>(relationships.Comparer);
            var nextMemories=new Dictionary<RebirthNpcStableId,List<RebirthNpcSocialMemory>>();var nextEvents=new HashSet<Guid>();var nextOrder=new Queue<Guid>();var nextEpoch=new object();
            var doc=RebirthNpcSocialPersistenceStore.QuarantineImage();
            return new RebirthNpcSaveQuarantineDomain("social-service",doc,
                ()=>{lock(Sync)return ReferenceEquals(Relationships,relationships)&&ReferenceEquals(Memories,memories)&&ReferenceEquals(AppliedEvents,events)&&ReferenceEquals(AppliedEventOrder,order)&&ReferenceEquals(recoveryEpoch,epoch)&&nextDecay==decay&&RebirthNpcSocialPersistenceStore.QuarantineImage().OuterXml==doc.OuterXml;},
                ()=>{lock(Sync){Relationships=nextRelationships;Memories=nextMemories;AppliedEvents=nextEvents;AppliedEventOrder=nextOrder;recoveryEpoch=nextEpoch;nextDecay=0;}},
                ()=>{lock(Sync){Relationships=relationships;Memories=memories;AppliedEvents=events;AppliedEventOrder=order;recoveryEpoch=epoch;nextDecay=decay;}});
        }
    }
}
public static partial class RebirthNpcFactionGameplayService
{
    internal static RebirthNpcSaveQuarantineDomain PrepareFactionQuarantine()
    {
        lock(Sync)
        {
            var standings=Standings;var order=Order;var nodes=OrderNodes;var epoch=recoveryEpoch;
            var nextStandings=new Dictionary<string,RebirthNpcFactionStanding>(standings.Comparer);var nextOrder=new LinkedList<string>();var nextNodes=new Dictionary<string,LinkedListNode<string>>(nodes.Comparer);var nextEpoch=new object();
            var doc=new XmlDocument();var root=(XmlElement)doc.ImportNode(RebirthNpcSocialPersistenceStore.QuarantineImage().DocumentElement.SelectSingleNode("factions"),true);doc.AppendChild(root);
            return new RebirthNpcSaveQuarantineDomain("faction-service",doc,
                ()=>{lock(Sync)return ReferenceEquals(Standings,standings)&&ReferenceEquals(Order,order)&&ReferenceEquals(OrderNodes,nodes)&&ReferenceEquals(recoveryEpoch,epoch);},
                ()=>{lock(Sync){Standings=nextStandings;Order=nextOrder;OrderNodes=nextNodes;recoveryEpoch=nextEpoch;}},
                ()=>{lock(Sync){Standings=standings;Order=order;OrderNodes=nodes;recoveryEpoch=epoch;}});
        }
    }
}
public static partial class RebirthNpcSocialPersistenceStore
{
    internal static XmlDocument QuarantineImage()
    {var doc=SnapshotDocument();if(!Validate(doc))throw new InvalidOperationException("Retained social/faction state cannot be captured losslessly");return doc;}
    internal static bool TryPrepareQuarantine(RebirthNpcSaveQuarantineScope scope,List<RebirthNpcSaveQuarantineDomain> domains)
    {
        lock(Sync)
        {
            var image=QuarantineImage();
            if(flushing||!scope.MatchesBinding(boundWorld as World,boundPath,loaded||image.SelectNodes("/*/*/*").Count!=0))return false;
            bool priorLoaded=loaded;var world=boundWorld;string path=boundPath,saved=savedState,source=lastSource,error=lastError;var binding=originalBinding;var observed=observedEpoch;
            var trades=tradeCompletions;var emptyTrades=new Dictionary<Guid,string>();
            var doc=new XmlDocument();var root=doc.CreateElement("socialBinding");doc.AppendChild(root);root.SetAttribute("loaded",loaded?"1":"0");root.SetAttribute("path",path??string.Empty);root.SetAttribute("savedState",saved??string.Empty);root.SetAttribute("source",source??string.Empty);root.SetAttribute("error",error??string.Empty);
            var social=RebirthNpcSocialService.PrepareSocialQuarantine();var faction=RebirthNpcFactionGameplayService.PrepareFactionQuarantine();
            var store=new RebirthNpcSaveQuarantineDomain("social-binding",doc,
                ()=>{lock(Sync)return ReferenceEquals(tradeCompletions,trades)&&loaded==priorLoaded&&!flushing&&ReferenceEquals(boundWorld,world)&&boundPath==path&&savedState==saved&&ReferenceEquals(originalBinding,binding)&&ReferenceEquals(observedEpoch,observed);},
                ()=>{lock(Sync){tradeCompletions=emptyTrades;loaded=false;boundWorld=null;boundPath=null;savedState=null;lastSource="quarantined-prior-domain";lastError=string.Empty;originalBinding=null;observedEpoch=null;}},
                ()=>{lock(Sync){tradeCompletions=trades;loaded=priorLoaded;boundWorld=world;boundPath=path;savedState=saved;lastSource=source;lastError=error;originalBinding=binding;observedEpoch=observed;}});
            domains.Add(social);domains.Add(faction);domains.Add(store);return true;
        }
    }
}
