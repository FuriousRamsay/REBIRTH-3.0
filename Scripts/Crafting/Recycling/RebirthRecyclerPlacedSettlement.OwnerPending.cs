using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;

internal static partial class RebirthRecyclerPlacedSettlement
{
    // Discovery creates a zero-authority owner hold only. The full primary and
    // actual station read are still mandatory for the cold financial issuer.
    private static Guid[] FindLoadedOwnerPending(LoadedOwner source)
    {
        var pending=new List<Guid>();
        try
        {
            string root=Path.Combine(source.save,"RebirthData","Survivor","WorkstationCompletions","recyclerPlaced");
            if(!Directory.Exists(root))return pending.ToArray();
            var receipts=ReadRecyclerReceipts(source.buffs);if(receipts==null)return new[]{Guid.Empty};
            foreach(var receipt in receipts)
            {
                string path=Path.Combine(root,receipt.Key.ToString("N")+".plan.xml");
                if(RebirthWorkstationCompletionPublication.PublishedLength(path)<1)continue;
                using(var file=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))
                using(var xml=XmlReader.Create(file,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=24L*1024*1024,MaxCharactersFromEntities=0}))
                {
                    xml.MoveToContent();if(xml.Name!="workstationCompletionIntent")continue;string envelope=xml.GetAttribute("operation");
                    while(xml.Read())
                    {
                        if(xml.NodeType!=XmlNodeType.Element||xml.Depth!=2||xml.Name!="placedRecyclerOriginal")continue;
                        if(!Guid.TryParseExact(xml.GetAttribute("operation"),"N",out var operation)||operation==Guid.Empty||envelope!=operation.ToString("N")||
                            xml.GetAttribute("owner")!=source.owner||xml.GetAttribute("account")!=source.account||xml.GetAttribute("creation")!=source.creation||
                            xml.GetAttribute("world")!=RebirthWorkstationCompletionOutbox.CurrentGuid(source.world)||
                            !int.TryParse(xml.GetAttribute("deaths"),out int deaths)||deaths!=source.deaths)break;
                        float phase=receipt.Value;if(operation!=receipt.Key||phase<1||phase>3||phase!=(int)phase)break;
                        // Positive metadata cleanup publication permits ordinary
                        // owner activity; it grants no old operation another slot.
                        string cleanup=Path.Combine(root,operation.ToString("N")+".cleanup-saved.xml");
                        var finished=RebirthWorkstationCompletionPublication.ReadPrimary(cleanup);
                        if(phase==3&&finished?.Name=="workstationCompletionIntent"&&(string)finished.Attribute("operation")==operation.ToString("N")&&
                            finished.Element("recyclerCleanupSaved")!=null)break;
                        pending.Add(operation);break;
                    }
                }
            }
        }
        catch{return new[]{Guid.Empty};}
        return pending.Distinct().ToArray();
    }
    private static List<KeyValuePair<Guid,float>> ReadRecyclerReceipts(byte[] bytes)
    {
        if(bytes==null||bytes.Length>1024*1024)return null;
        using(var stream=new MemoryStream(bytes,false))using(var reader=MemoryPools.poolBinaryReader.AllocSync(true))
        {
            reader.SetBaseStream(stream);int version=reader.ReadByte();if(version!=EntityBuffs.Version||version<2)return null;
            int count=reader.ReadUInt16();for(int i=0;i<count;i++)new BuffValue().Read(reader,version);
            count=reader.ReadUInt16();var result=new List<KeyValuePair<Guid,float>>();var seen=new HashSet<Guid>();const string prefix="rbRecyclerPlaced_";
            for(int i=0;i<count;i++)
            {
                string key=reader.ReadString();float value=reader.ReadSingle();if(!key.StartsWith(prefix,StringComparison.Ordinal))continue;
                if(!Guid.TryParseExact(key.Substring(prefix.Length),"N",out var operation)||operation==Guid.Empty||!seen.Add(operation)||float.IsNaN(value)||float.IsInfinity(value))return null;
                if(value>=1&&value<=3&&value==(int)value)result.Add(new KeyValuePair<Guid,float>(operation,value));
            }
            return stream.Position==stream.Length?result:null;
        }
    }
}
