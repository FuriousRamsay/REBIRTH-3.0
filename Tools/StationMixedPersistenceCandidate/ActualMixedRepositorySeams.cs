using System;using System.Linq;using System.IO;using System.Collections.Generic;using System.Xml.Linq;
internal sealed class MixedProgressionCandidate {
    internal readonly Dictionary<string,MixedCompletionRecord> StationMixedCompletionRecords = new Dictionary<string,MixedCompletionRecord>(StringComparer.Ordinal);
internal MixedProgressionCandidate Clone(){var copy=new MixedProgressionCandidate();
        foreach(var pair in StationMixedCompletionRecords) copy.StationMixedCompletionRecords.Add(pair.Key,pair.Value.Clone());
return copy;}}
internal static class ActualMixedRepositorySeams {
internal static XElement SerializeSeam(XElement node,MixedProgressionCandidate state){
        if(state.StationMixedCompletionRecords.Count!=0)
        {
            if(!MixedCompletionRecord.TryAppendData(node,state.StationMixedCompletionRecords.Values,out var mixedNode))throw new InvalidDataException("Invalid mixed completion DATA section");
            node=mixedNode;
        }
return node;}
internal static bool DeserializeSeam(XElement node,out MixedProgressionCandidate state,out string error){state=new MixedProgressionCandidate();error="";
        if(!MixedCompletionRecord.TryReadAllData(node,out var mixedCompletionRecords)){error="mixed completion DATA invalid";return false;}
        foreach(var pair in mixedCompletionRecords)state.StationMixedCompletionRecords.Add(pair.Key,pair.Value);
return true;}}