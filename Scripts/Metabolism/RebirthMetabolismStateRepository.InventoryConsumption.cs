using System;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

public static partial class RebirthMetabolismStateRepository
{
    internal static bool ApplyInventoryServing(RebirthConsumptionDurableDelivery.InventoryOriginal original)
    {
        if(!RebirthConsumptionDurableDelivery.AllowsInventoryCanonical(original))return false;
        lock(Sync)
        {
            var state=original.Metabolism;if(!NpcServingBeforeCurrent(state))return false;
            ValidateStateForPersistence(original.Serving.State);state.After=original.Serving.State;state.After.Touch();States[state.Id]=state.After;state.Applied=true;return true;
        }
    }
    internal static bool SaveInventoryServing(RebirthConsumptionDurableDelivery.InventoryOriginal original,out string full)
    {
        full=null;var token=original?.Metabolism;
        if(token?.Applied!=true||!original.Current()||!TryFlush("player-source-consumption"))return false;
        lock(Sync)
        {
            if(!ScopeCurrentLocked()||token.Generation!=loadGeneration||!States.TryGetValue(token.Id,out var state)||!ReferenceEquals(state,token.After))return false;
            ValidateStateForPersistence(state);full=SerializePlayer(token.Id,state).ToString(SaveOptions.DisableFormatting);
            var file=new FileInfo(scopePath);if(!file.Exists||file.Length<1||file.Length>8*1024*1024||(file.Attributes&FileAttributes.ReparsePoint)!=0)return false;
            using var reader=XmlReader.Create(scopePath,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=8*1024*1024});
            var document=XDocument.Load(reader);var row=document.Root.Elements("player").SingleOrDefault(value=>(string)value.Attribute("id")==token.Id);
            return row!=null&&XNode.DeepEquals(row,SerializePlayer(token.Id,state));
        }
    }
}
