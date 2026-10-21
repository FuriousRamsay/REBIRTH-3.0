#if REBIRTH_DEBUG
using System;using System.Linq;using Newtonsoft.Json.Linq;
internal static partial class RebirthProductiveEarnedUiQueueOwner
{
    internal static bool InitialObserverInstalled=>installed;
    internal static JArray InitialFixtureStatus()
    {
        var rows=new JArray();
        foreach(var source in originals.Where(value=>!value.Closed&&ReferenceEquals(value.World,GameManager.Instance?.World)).Take(64))
        {
            var row=new JObject{["operation"]=source.Operation.ToString("N"),["birth"]=source.Birth.ToString("N"),["owner"]=source.OriginalOwnerKey,["creation"]=source.Creation,
                ["deaths"]=source.Deaths,["rowBinding"]=InitialHash(System.Text.Encoding.UTF8.GetBytes(source.NativeRecipeBinding)),["originalDigest"]=InitialHash(source.Image)};
            try{row["saved"]=initialPublications.Any(value=>ReferenceEquals(value.Source,source)&&value.Saved)||source.InitialLoaded is InitialNativeOriginal;
                row["loaded"]=source.InitialLoaded is InitialNativeOriginal loaded&&loaded.OriginalRowCurrent();row["ownerWitnessCurrent"]=source.SourcePublished();
                row["nativeHeld"]=HoldsInitialPublication(source.Station);}catch{row["unknown"]=true;}rows.Add(row);
        }
        lock(initialLoadGate)foreach(var source in initialNativeOriginals.Where(value=>value.Installed&&ReferenceEquals(value.World,GameManager.Instance?.World)).Take(64))
        {
            if(rows.OfType<JObject>().Any(row=>(string)row["operation"]==source.Reference.Source.ToString("N")))continue;
            var row=new JObject{["operation"]=source.Reference.Source.ToString("N"),["birth"]=source.Reference.Birth,["owner"]=source.Owner,["creation"]=source.Creation,["deaths"]=source.Deaths,
                ["rowBinding"]=source.RowBindingDigest,["originalDigest"]=source.OriginalDigest,["ownerWitnessCurrent"]=false};
            try{row["saved"]=InitialReceiptCurrent(source);row["loaded"]=source.OriginalRowCurrent();row["nativeHeld"]=HoldsInitialPublication(source.Tile);}catch{row["unknown"]=true;}rows.Add(row);
        }
        return rows;
    }
}
#endif
