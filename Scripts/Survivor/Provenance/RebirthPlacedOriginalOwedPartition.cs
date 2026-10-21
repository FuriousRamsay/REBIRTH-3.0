using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Security.Cryptography;

internal static partial class RebirthPlacedOriginalDeathPartition
{
    // Positive first partition checkpoint, NOT output or payment settlement.
    // This lets a later current-life additive child use an already authenticated
    // final primary image without requiring the owner to remain frozen forever.
    private sealed class OwedPartition
    {
        internal Guid Operation;internal string World,Owner,Creation,Trace,Body,Native,Owed,Image;internal int Deaths;
    }
    private static string OwedPath(Guid operation)=>Path.Combine(GameIO.GetSaveGameDir(),"rebirth","placed-original","owed-partitions",operation.ToString("N")+".xml");
    private static string OwedBody(OwedPartition value){var xml=XElement.Parse(BareOwedBody(value));xml.SetAttributeValue("checksum",Hash(BareOwedBody(value)));return xml.ToString(SaveOptions.DisableFormatting);}
    private static string BareOwedBody(OwedPartition p)=>new XElement("placedOwedPartition",new XAttribute("version",1),new XAttribute("operation",p.Operation.ToString("N")),new XAttribute("world",p.World),new XAttribute("owner",p.Owner),new XAttribute("creation",p.Creation),new XAttribute("deaths",p.Deaths),new XAttribute("trace",p.Trace),new XAttribute("body",p.Body),new XAttribute("native",p.Native),new XElement("owed",p.Owed)).ToString(SaveOptions.DisableFormatting);
    private static bool FullHash(string value)=>value!=null&&value.Length==64&&value.All(c=>c>='0'&&c<='9'||c>='a'&&c<='f');
    private static OwedPartition DecodeOwed(string image)
    {
        if(image==null||image.Length>Maximum)throw new InvalidDataException();XElement x;using(var input=new StringReader(image))using(var reader=XmlReader.Create(input,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=Maximum}))x=XElement.Load(reader);
        if(x.Name!="placedOwedPartition"||(int?)x.Attribute("version")!=1)throw new InvalidDataException();var p=new OwedPartition{Operation=Guid.ParseExact((string)x.Attribute("operation"),"N"),World=(string)x.Attribute("world"),Owner=(string)x.Attribute("owner"),Creation=(string)x.Attribute("creation"),Deaths=(int)x.Attribute("deaths"),Trace=(string)x.Attribute("trace"),Body=(string)x.Attribute("body"),Native=(string)x.Attribute("native"),Owed=(string)x.Element("owed"),Image=image};
        var rows=ReadRows(Convert.FromBase64String(p.Owed));if(p.Operation==Guid.Empty||!Guid.TryParse(p.World,out _)||!RebirthOriginalActionScope.Validate(p.Owner,p.Creation)||p.Deaths<1||!FullHash(p.Trace)||!FullHash(p.Native)||p.Body.Length!=0&&!FullHash(p.Body)||rows.Length==0||rows.Any(row=>row.IsEmpty())||OwedBody(p)!=image)throw new InvalidDataException();return p;
    }
    private static bool ReadOwed(Guid operation,out OwedPartition p)
    {p=null;try{string path=OwedPath(operation);if(!File.Exists(path)||new FileInfo(path).Length>Maximum)return false;p=DecodeOwed(File.ReadAllText(path,new UTF8Encoding(false,true)));return p.Operation==operation;}catch{return false;}}
    private static bool SaveOwed(OwedPartition p)
    {
        try
        {
            string path=OwedPath(p.Operation);if(File.Exists(path)||File.Exists(path+".bak"))return ReadOwed(p.Operation,out var existing)&&existing.Image==p.Image;Directory.CreateDirectory(Path.GetDirectoryName(path));
            var pending=Directory.EnumerateFiles(Path.GetDirectoryName(path),p.Operation.ToString("N")+".*.tmp").ToArray();if(pending.Length!=0){if(pending.Length!=1||new FileInfo(pending[0]).Length>Maximum||DecodeOwed(File.ReadAllText(pending[0],new UTF8Encoding(false,true))).Image!=p.Image)return false;return RebirthDurableFileCommit.TryPublish(pending[0],path,out _)&&ReadOwed(p.Operation,out var recovered)&&recovered.Image==p.Image;}
            string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";var bytes=new UTF8Encoding(false,true).GetBytes(p.Image);using(var stream=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){stream.Write(bytes,0,bytes.Length);stream.Flush(true);}if(DecodeOwed(File.ReadAllText(temp,new UTF8Encoding(false,true))).Image!=p.Image)return false;return RebirthDurableFileCommit.TryPublish(temp,path,out _)&&ReadOwed(p.Operation,out var saved)&&saved.Image==p.Image;
        }catch{return false;}
    }
    private static bool RetainOwedPartition(PhysicalTrace trace,PlayerDataFile file,string bodyProof)
    {
        try
        {
            // Caller has checked SAME final primary actor/equipment/marker and
            // exact original death. Ordinary request/trace bytes alone cannot
            // reach this writer. Successful body rows need separate positive publication before any child admission.
            var world=GameManager.Instance?.World;if(!ThreadManager.IsMainThread()||world?.IsRemote()!=false||world.worldState.Guid!=trace.Observation.Original.World)return false;
            var rows=trace.Inputs.Where(input=>trace.Overflow||input.Failed).Select(input=>input.Item.Clone()).ToArray();if(rows.Length==0)return false;
            string native;using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream,Encoding.UTF8,true)){writer.Write(file.id);writer.Write(file.deaths);foreach(var bytes in new[]{file.bagData.ToArray(),file.inventoryData.ToArray(),file.equipmentData.ToArray(),file.buffData.ToArray()}){writer.Write(bytes.Length);writer.Write(bytes);}writer.Flush();using(var hash=SHA256.Create())native=BitConverter.ToString(hash.ComputeHash(stream.ToArray())).Replace("-","").ToLowerInvariant();}
            var original=trace.Observation.Original;var p=new OwedPartition{Operation=original.Operation,World=original.World,Owner=original.Owner,Creation=original.Creation,Deaths=file.deaths,Trace=trace.Digest,Body=string.Empty,Native=native,Owed=Convert.ToBase64String(Rows(rows))};p=DecodeOwed(OwedBody(p));return SaveOwed(p);
        }catch{return false;}
    }
    // One bounded child fragment changes only one previously empty native slot.
    // Locked/full inventory leaves the exact source row owed, without a drop.
    private static bool TryPlanOwedFragment(RebirthCamperEnergyActorImage before,ItemStack owed,out RebirthCamperEnergyActorImage after,out bool belt,out int slot,out int quantity)
    {
        after=null;belt=false;slot=-1;quantity=0;
        try
        {
            if(before?.Bag==null||before.Belt==null||before.Cursor==null||!before.Cursor.IsEmpty()||before.BagLocks==null||before.BagLocks.Length!=before.Bag.Length||owed==null||owed.IsEmpty()||owed.count<1||owed.count>ushort.MaxValue||owed.itemValue?.ItemClass==null)return false;
            int maximum=Math.Min(ushort.MaxValue,Math.Max(1,owed.itemValue.ItemClass.Stacknumber.Value));quantity=Math.Min(maximum,owed.count);
            for(int i=0;i<before.Bag.Length;i++)if(!before.BagLocks[i]&&before.Bag[i]!=null&&before.Bag[i].IsEmpty()){slot=i;break;}
            if(slot<0){belt=true;for(int i=0;i<before.Belt.Length;i++)if(before.Belt[i]!=null&&before.Belt[i].IsEmpty()){slot=i;break;}}
            if(slot<0){quantity=0;return false;}after=before.Copy();var fragment=owed.Clone();fragment.count=quantity;if(belt)after.Belt[slot]=fragment;else after.Bag[slot]=fragment;
            return Conserved(before.Bag.Concat(before.Belt).Concat(new[]{fragment}),after.Bag.Concat(after.Belt));
        }catch{after=null;slot=-1;quantity=0;return false;}
    }

}
