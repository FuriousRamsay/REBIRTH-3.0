using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

public static partial class RebirthRecipeOutputIntegration
{
    private static bool ReadPersonalOutputSidecar(string path,out byte[] bytes,out XElement root)
    {
        bytes=null;root=null;
        try
        {
            RebirthWorkstationCompletionPublication.RequireNativeSaveFile(path);
            if(File.Exists(path+".tmp")||File.Exists(path+".bak"))return false;
            using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))
            {
                if(stream.Length<1||stream.Length>UiOriginalMaximum)return false;
                bytes=new byte[checked((int)stream.Length)];int offset=0;
                while(offset<bytes.Length){int read=stream.Read(bytes,offset,bytes.Length-offset);if(read<1)return false;offset+=read;}
                if(stream.ReadByte()!=-1)return false;
            }
            using(var stream=new MemoryStream(bytes,false))
            using(var reader=XmlReader.Create(stream,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=UiOriginalMaximum}))
                root=XElement.Load(reader,LoadOptions.None);
            return root!=null;
        }
        catch{bytes=null;root=null;return false;}
    }
    private static bool ExactPersonalOutputFields(XElement root,string name,string attributes,string elements)
    {
        if(root==null||root.Name!=name||root.Nodes().Any(value=>value is not XElement&&value is not XText))return false;
        var expectedAttributes=attributes.Split(',');var expectedElements=elements.Length==0?Array.Empty<string>():elements.Split(',');
        return root.Attributes().Count()==expectedAttributes.Length&&expectedAttributes.All(value=>root.Attribute(value)!=null)&&
            root.Elements().Count()==expectedElements.Length&&expectedElements.All(value=>root.Elements(value).Count()==1)&&
            root.Elements().All(value=>!value.HasAttributes&&!value.HasElements)&&
            root.Nodes().OfType<XText>().All(value=>string.IsNullOrWhiteSpace(value.Value));
    }
    private static bool ExactPersonalOutputOriginal(XElement root)
    {
        string fields="version,operation,world,owner,creation,deaths,entity,beforeCount,startingOwner,cached,nativeVersion,sink,personalQueueIndex";
        if(root?.Attribute("callbackFence")!=null)
        {if((string)root.Attribute("callbackFence")!="1")return false;fields+=",callbackFence";}
        return ExactPersonalOutputFields(root,"recipeOutputOriginal",fields,"recipe,primaryBeforeEffects,outputs,bagBefore,beltBefore,equipmentBefore,personalQueueBefore");
    }
    private static bool TryRetirePersonalOutputColdTerminal(UiOutputLoadedOwner source,Guid operation)
    {
        // This path retires only a positively saved complete original. It never
        // hydrates a card, decrements a queue, inserts an item or repeats effects.
        if(source==null||!source.Current()||!source.Operations.Contains(operation)||source.Actor.Buffs.GetCustomVar(PersonalOutputReceipt(operation))==0f||
            source.Queue!=RebirthRemoteResourceClientOriginal.QueueImage(source.Actor.PlayerUI?.xui?.GetCraftingData()))return false;
        var cards=source.Actor.PlayerUI.xui.FindWindowGroupByName("crafting")?.GetChildByType<XUiC_CraftingQueue>()?.GetRecipesToCraft();
        if(cards==null||cards.Any(card=>card!=null&&pendingUiOutputs.TryGetValue(card,out var pending)&&pending.Operation==operation))return false;
        string path=Path.Combine(source.Save,"rebirth","recipe-outputs","ui",operation.ToString("N")+".xml");
        using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
        {
            if(decoder==null||!ReadPersonalOutputSidecar(path,out var originalBytes,out var original)||
                !ExactPersonalOutputOriginal(original)||
                !ReadPersonalOutputSidecar(path+".queue-owner-saved",out _,out var terminal)||
                !ExactPersonalOutputFields(terminal,"recipeOutputPersonalQueueSaved","operation,original,owner,creation,deaths,beforeCount,afterCount,outputPrefix,playerFile,nativeDigest,nativeLength,queueDigest,personalPrefix",""))return false;
            string id=operation.ToString("N");
            if((string)original.Attribute("version")!="1"||(string)original.Attribute("operation")!=id||(string)terminal.Attribute("operation")!=id||
                (string)original.Attribute("sink")!="Inventory"||(string)original.Attribute("world")!=source.World.Guid||
                (string)original.Attribute("owner")!=source.Owner||(string)terminal.Attribute("owner")!=source.Owner||
                (string)original.Attribute("creation")!=source.Creation||(string)terminal.Attribute("creation")!=source.Creation||
                (string)terminal.Attribute("original")!=UiOriginalHash(originalBytes)||
                (string)terminal.Attribute("playerFile")!=source.Path||(string)terminal.Attribute("nativeDigest")!=UiOriginalHash(source.Native)||
                (string)terminal.Attribute("queueDigest")!=UiOriginalHash(Encoding.UTF8.GetBytes(source.Queue))||
                !int.TryParse((string)terminal.Attribute("nativeLength"),out int length)||length!=source.Native.Length||
                !int.TryParse((string)original.Attribute("deaths"),out int deaths)||deaths!=source.Deaths||
                (string)terminal.Attribute("deaths")!=(string)original.Attribute("deaths")||
                !int.TryParse((string)original.Attribute("personalQueueIndex"),out int slot)||slot<0||slot>=255||
                !int.TryParse((string)original.Attribute("beforeCount"),out int before)||before<1||
                (string)terminal.Attribute("beforeCount")!=(string)original.Attribute("beforeCount")||
                !int.TryParse((string)terminal.Attribute("afterCount"),out int after)||after!=before-1||
                !int.TryParse((string)terminal.Attribute("outputPrefix"),out int prefix)||prefix<1||prefix>65||
                (string)terminal.Attribute("personalPrefix")!=(string)terminal.Attribute("outputPrefix")||
                !RebirthRemoteResourcePlacedStationSavedReceipt.TryRead(source.Buffs,PersonalOutputReceipt(operation),out float savedPrefix,out bool present)||!present||savedPrefix!=prefix||
                source.Actor.Buffs.GetCustomVar(PersonalOutputReceipt(operation))!=prefix||
                !source.Current()||source.Queue!=RebirthRemoteResourceClientOriginal.QueueImage(source.Actor.PlayerUI?.xui?.GetCraftingData()))return false;
            // Load/Read/ToPlayer already proved these complete native bytes
            // against the real saved file. Later native marker-cleanup saves
            // must not invalidate sibling originals from that same load. The
            // current full inventory and restored queue still match that load.
            source.Actor.Buffs.SetCustomVar(PersonalOutputReceipt(operation),0f,true);
            source.Actor.Buffs.RemoveCustomVar(PersonalOutputReceipt(operation));
            GameManager.Instance.SaveLocalPlayerData();
            return true;
        }
    }
    private static void PumpPersonalOutputColdTerminals()
    {
        foreach(var source in uiOutputLoadedOwners.ToArray())
        {
            if(!source.Hydrated||source.Closed||source.Operations==null||source.Operations.Length==0)continue;
            // One bounded original per owner per existing one-second pump.
            Guid operation=source.Operations[source.TerminalCursor++%source.Operations.Length];
            if(source.TerminalCursor==int.MaxValue)source.TerminalCursor=0;
            try
            {
                TryCompletePersonalOutputLoadedQueue(source,operation);
                if(!TryRetirePersonalOutputColdTerminal(source,operation))TryAdoptPersonalOutputCold(source,operation);
            }
            catch{/* Unknown originals retain their marker and original. */}
        }
    }
}
