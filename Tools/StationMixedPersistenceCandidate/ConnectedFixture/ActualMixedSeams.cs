using System;using System.IO;using System.Linq;using System.Xml.Linq;
static partial class RebirthWorldCharacterRepository {
static bool ReadMixedActual(XElement node,string ownerKey,RebirthWorldProgressionState state,out string error){error="";
        if(!MixedCompletionRecord.TryReadAllData(node,out var mixedCompletionRecords)){error="mixed completion DATA invalid";return false;}
        foreach(var pair in mixedCompletionRecords)
        {
            var data=pair.Value.Write();
            if((string)data.Attribute("owner")!=ownerKey||state.StationCompletionPublications.ContainsKey(pair.Key)||state.StationCompletionExpectationProjections.ContainsKey(pair.Key)||
                !state.StationPreparations.TryGetValue(pair.Key,out var mixedAdmission)||
                !state.StationTerminalIntents.TryGetValue(pair.Key,out var mixedIntent)||
                !state.StationPublications.TryGetValue(pair.Key,out var mixedQueued)||
                !XNode.DeepEquals(data.Element("admission").Elements().Single(),mixedAdmission.Write())||
                !XNode.DeepEquals(data.Element("intent").Elements().Single(),mixedIntent.Write())||
                !XNode.DeepEquals(data.Element("queued").Elements().Single(),mixedQueued.Write())||
                !MixedNativePublicationCandidate.TryRead(data.Element("completed").Elements().Single(),mixedAdmission,mixedIntent,mixedQueued,out _))
            {error="mixed completion original binding invalid";return false;}
        }
        foreach(var pair in mixedCompletionRecords)state.StationMixedCompletionRecords.Add(pair.Key,pair.Value);

return true;}
static XElement AppendMixedActual(XElement node,RebirthWorldProgressionState state){
        if(state.StationMixedCompletionRecords.Count!=0)
        {
            if(!MixedCompletionRecord.TryAppendData(node,state.StationMixedCompletionRecords.Values,out var mixedNode))throw new InvalidDataException("Invalid mixed completion DATA section");
            node=mixedNode;
        }

return node;}}