using System;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Linq;

#nullable disable

// Farming original output custody only. No caller is admitted until source and
// owner saved-publication witnesses are both wired into the protocol.
internal static class AdvancedFarmingHarvestReceipt
{
    internal enum Phase { Reserved=0,SourcePublished=1,OwnerPublished=2,Retired=3 }
    internal sealed class Record
    {
        internal Guid Operation,Source;internal string World,Owner,Creation,PlanImage,Outputs;
        internal int X,Y,Z,Deaths;internal bool Replant;internal ushort Block;
        internal byte Meta,Meta2,Rotation;internal Phase State;
        internal string NativeSourceDigest=string.Empty;internal long NativeSourceGeneration;
        internal uint Raw;
        internal string Terminal=string.Empty;
        internal bool EventsRequired;
        internal float DropX,DropY,DropZ;
        internal string DropOutputDigest=string.Empty,DropNativeDigest=string.Empty;internal long DropNativeGeneration;
    }
    private const int MaximumImage=12*1024*1024;
    internal static string OutputImage(ItemStack[] outputs)
    {
        if(outputs==null||outputs.Length>128)throw new InvalidDataException("Harvest outputs exceed bound.");
        var bytes=RebirthCamperInventorySnapshot.Capture(outputs);
        if(bytes.Length>4*1024*1024)throw new InvalidDataException("Harvest output payload exceeds bound.");
        return Convert.ToBase64String(bytes);
    }
    internal static ItemStack[] ReadOutputs(string image)
    {
        if(image==null||image.Length>6*1024*1024)throw new InvalidDataException();
        var bytes=Convert.FromBase64String(image);if(bytes.Length>4*1024*1024)throw new InvalidDataException();
        var outputs=RebirthCamperInventorySnapshot.Restore(bytes);
        if(outputs==null||outputs.Length>128||OutputImage(outputs)!=image)throw new InvalidDataException();
        foreach(var item in outputs)if(item==null||item.IsEmpty()||item.count<=0||item.count>1000000)throw new InvalidDataException();
        return outputs;
    }
    private static bool AssignedOutputs(AdvancedFarmingHarvestOwnerDelta.Plan plan,ItemStack[] outputs,bool complete)
    {
        var remaining=new ItemStack[outputs.Length];for(int i=0;i<outputs.Length;i++)remaining[i]=outputs[i].Clone();
        for(int area=0;area<2;area++)
        {
            var before=area==0?plan.BeforeBag:plan.BeforeBelt;var after=area==0?plan.AfterBag:plan.AfterBelt;
            for(int slot=0;slot<after.Length;slot++)
            {
                if(after[slot].IsEmpty())continue;
                int count=after[slot].count-(before[slot].IsEmpty()?0:before[slot].count);if(count==0)continue;if(count<0)return false;
                var identity=after[slot].Clone();identity.count=1;
                for(int i=0;i<remaining.Length&&count>0;i++)
                {
                    if(remaining[i].count<=0)continue;var candidate=remaining[i].Clone();candidate.count=1;
                    if(!RebirthStationGridIngredients.IsSameStackSnapshot(identity,candidate))continue;
                    int taken=Math.Min(count,remaining[i].count);count-=taken;remaining[i].count-=taken;
                }
                if(count!=0)return false;
            }
        }
        if(complete)foreach(var item in remaining)if(item.count!=0)return false;
        return true;
    }
    internal static ItemStack[] Remaining(Record record)
    {
        var plan=AdvancedFarmingHarvestOwnerDelta.Decode(record.PlanImage);var remaining=ReadOutputs(record.Outputs);var result=new System.Collections.Generic.List<ItemStack>();
        for(int area=0;area<2;area++)
        {
            var before=area==0?plan.BeforeBag:plan.BeforeBelt;var after=area==0?plan.AfterBag:plan.AfterBelt;
            for(int slot=0;slot<after.Length;slot++)
            {
                if(after[slot].IsEmpty())continue;int count=after[slot].count-(before[slot].IsEmpty()?0:before[slot].count);
                var identity=after[slot].Clone();identity.count=1;
                for(int i=0;i<remaining.Length&&count>0;i++)
                {var candidate=remaining[i].Clone();candidate.count=1;if(!RebirthStationGridIngredients.IsSameStackSnapshot(identity,candidate))continue;int taken=Math.Min(count,remaining[i].count);count-=taken;remaining[i].count-=taken;}
                if(count!=0)throw new InvalidDataException();
            }
        }
        foreach(var item in remaining)if(item.count>0)result.Add(item);return result.ToArray();
    }
    internal static string OutputDigest(string image)
    {using(var hash=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(image))).Replace("-","").ToLowerInvariant();}
    internal static string Encode(Record record)
    {
        var root=new XElement("farmingHarvestReceipt",new XAttribute("version",1),new XAttribute("operation",record.Operation.ToString("N")),new XAttribute("source",record.Source.ToString("N")),
            new XAttribute("world",record.World),new XAttribute("owner",record.Owner),new XAttribute("creation",record.Creation),new XAttribute("deaths",record.Deaths),
            new XAttribute("x",record.X),new XAttribute("y",record.Y),new XAttribute("z",record.Z),new XAttribute("raw",record.Raw),new XAttribute("block",record.Block),new XAttribute("meta",record.Meta),new XAttribute("meta2",record.Meta2),new XAttribute("rotation",record.Rotation),
            new XAttribute("replant",record.Replant),record.EventsRequired?new XAttribute("eventsRequired",true):null,new XAttribute("phase",(int)record.State),new XAttribute("nativeSource",record.NativeSourceDigest),new XAttribute("nativeGeneration",record.NativeSourceGeneration),new XAttribute("dropX",record.DropX),new XAttribute("dropY",record.DropY),new XAttribute("dropZ",record.DropZ),new XAttribute("dropOutput",record.DropOutputDigest),new XAttribute("dropNative",record.DropNativeDigest),new XAttribute("dropGeneration",record.DropNativeGeneration),new XElement("ownerPlan",record.PlanImage),new XElement("outputs",record.Outputs),string.IsNullOrEmpty(record.Terminal)?null:new XElement("terminal",record.Terminal));
        string image=root.ToString(SaveOptions.DisableFormatting);if(image.Length>MaximumImage)throw new InvalidDataException();return image;
    }
    internal static Record Decode(string image)
    {
        if(image==null||image.Length>MaximumImage)throw new InvalidDataException();
        XElement root;using(var text=new StringReader(image))using(var reader=XmlReader.Create(text,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=MaximumImage}))root=XElement.Load(reader);
        if(root.Name!="farmingHarvestReceipt"||(int?)root.Attribute("version")!=1)throw new InvalidDataException();
        var r=new Record{Operation=Guid.ParseExact((string)root.Attribute("operation"),"N"),Source=Guid.ParseExact((string)root.Attribute("source"),"N"),World=(string)root.Attribute("world"),Owner=(string)root.Attribute("owner"),Creation=(string)root.Attribute("creation"),Deaths=(int)root.Attribute("deaths"),
            X=(int)root.Attribute("x"),Y=(int)root.Attribute("y"),Z=(int)root.Attribute("z"),Raw=(uint)root.Attribute("raw"),Block=checked((ushort)(int)root.Attribute("block")),Meta=checked((byte)(int)root.Attribute("meta")),Meta2=checked((byte)(int)root.Attribute("meta2")),Rotation=checked((byte)(int)root.Attribute("rotation")),Replant=(bool)root.Attribute("replant"),State=(Phase)(int)root.Attribute("phase"),NativeSourceDigest=(string)root.Attribute("nativeSource"),NativeSourceGeneration=(long)root.Attribute("nativeGeneration"),PlanImage=(string)root.Element("ownerPlan"),Outputs=(string)root.Element("outputs")};
        if(r.Operation==Guid.Empty||r.Source==Guid.Empty||!Guid.TryParse(r.World,out _)||string.IsNullOrEmpty(r.Owner)||r.Owner.Length>512||(!RebirthSurvivorRequestScope.TryNormalize(r.Creation,out _)&&r.Creation!="native-mode:"+r.Owner)||r.Deaths<0||r.Y<0||r.Y>4095||r.Block==0||!Enum.IsDefined(typeof(Phase),r.State))throw new InvalidDataException();
        r.EventsRequired=(bool?)root.Attribute("eventsRequired")??false;
        r.Terminal=(string)root.Element("terminal")??string.Empty;AdvancedFarmingHarvestTerminalPlot.Decode(r.Terminal);
        var plan=AdvancedFarmingHarvestOwnerDelta.Decode(r.PlanImage);
        r.DropX=(float)root.Attribute("dropX");r.DropY=(float)root.Attribute("dropY");r.DropZ=(float)root.Attribute("dropZ");r.DropOutputDigest=(string)root.Attribute("dropOutput");r.DropNativeDigest=(string)root.Attribute("dropNative");r.DropNativeGeneration=(long)root.Attribute("dropGeneration");
        if(float.IsNaN(r.DropX)||float.IsInfinity(r.DropX)||float.IsNaN(r.DropY)||float.IsInfinity(r.DropY)||float.IsNaN(r.DropZ)||float.IsInfinity(r.DropZ)||Math.Abs(r.DropX-r.X)>8f||Math.Abs(r.DropY-r.Y)>8f||Math.Abs(r.DropZ-r.Z)>8f)throw new InvalidDataException();
        if(r.State==Phase.Reserved?(r.NativeSourceGeneration!=0||r.NativeSourceDigest!=string.Empty):(r.NativeSourceGeneration<=0||!Digest(r.NativeSourceDigest)))throw new InvalidDataException();
        if(plan.Operation!=r.Operation||plan.World!=r.World||plan.Owner!=r.Owner||plan.Creation!=r.Creation||plan.Deaths!=r.Deaths)throw new InvalidDataException();
        if(r.DropNativeGeneration<0)throw new InvalidDataException();bool drop=r.DropNativeGeneration>0;
        if(drop?(r.State!=Phase.Retired||!Digest(r.DropNativeDigest)||r.DropOutputDigest!=OutputDigest(OutputImage(Remaining(r)))):(r.DropNativeDigest!=string.Empty||r.DropOutputDigest!=string.Empty))throw new InvalidDataException();
        if(!AssignedOutputs(plan,ReadOutputs(r.Outputs),r.State==Phase.Retired&&!drop)||Encode(r)!=image)throw new InvalidDataException();return r;
    }
    private static bool Digest(string value)
    {if(value==null||value.Length!=64)return false;foreach(char c in value)if(!(c>='0'&&c<='9')&&!(c>='a'&&c<='f'))return false;return true;}
    private static bool Scope(string world,out string directory)
    {
        directory=null;var active=GameManager.Instance?.World;
        if(!ThreadManager.IsMainThread()||active==null||active.IsRemote()||active.worldState?.Guid!=world||!Guid.TryParse(world,out _))return false;
        string save=GameIO.GetSaveGameDir();if(string.IsNullOrEmpty(save))return false;
        directory=Path.GetFullPath(Path.Combine(save,"rebirth","farming-harvest","v1"));return true;
    }
    internal static bool TryRead(string world,Guid operation,out Record record)
    {
        record=null;if(operation==Guid.Empty||!Scope(world,out var directory))return false;
        string path=Path.Combine(directory,operation.ToString("N")+".xml");
        try{string archive=Path.Combine(directory,"archive",operation.ToString("N")+".xml.done");bool archived=!File.Exists(path);if(archived)path=archive;else if(File.Exists(archive))return false;var file=new FileInfo(path);if(!file.Exists||file.Length>MaximumImage*4L)return false;var candidate=Decode(File.ReadAllText(path,new UTF8Encoding(false,true)));if(candidate.World!=world||candidate.Operation!=operation||archived&&candidate.State!=Phase.Retired)return false;record=candidate;return true;}
        catch(Exception error){Log.Warning("[REBIRTH Farming] original harvest receipt quarantined: "+operation.ToString("N")+" "+error.GetType().Name);return false;}
    }
    internal static bool ExistsOriginal(string world,Guid operation)
    {if(!Scope(world,out var directory))return true;string path=Path.Combine(directory,operation.ToString("N")+".xml");return File.Exists(path)||File.Exists(path+".bak")||File.Exists(Path.Combine(directory,"archive",operation.ToString("N")+".xml.done"));}
    internal static bool ArchiveRetired(Record record)
    {
        try
        {
            if(record==null||record.State!=Phase.Retired||!Scope(record.World,out var directory)||!TryRead(record.World,record.Operation,out var current)||Encode(current)!=Encode(record))return false;
            string path=Path.Combine(directory,record.Operation.ToString("N")+".xml"),archive=Path.Combine(directory,"archive",record.Operation.ToString("N")+".xml.done");
            if(!File.Exists(path))return File.Exists(archive);
            if(File.Exists(archive))return false;Directory.CreateDirectory(Path.GetDirectoryName(archive));
            if(File.ReadAllText(path,new UTF8Encoding(false,true))!=Encode(record))return false;File.Move(path,archive);
            return TryRead(record.World,record.Operation,out var saved)&&Encode(saved)==Encode(record);
        }
        catch{return false;}
    }
    private static bool Publish(string path,string image)
    {
        string temporary=path+"."+Guid.NewGuid().ToString("N")+".tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));var bytes=new UTF8Encoding(false,true).GetBytes(image);
            using(var stream=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None)){stream.Write(bytes,0,bytes.Length);stream.Flush(true);}
            if(Encode(Decode(File.ReadAllText(temporary,new UTF8Encoding(false,true))))!=image)return false;
            return RebirthDurableFileCommit.TryPublish(temporary,path,out _);
        }
        catch(Exception error){Log.Warning("[REBIRTH Farming] original harvest receipt retained: "+error.GetType().Name);return false;}
    }
    internal static bool TryReserve(Record record)
    {
        try
        {
            if(record==null||record.State!=Phase.Reserved||!Scope(record.World,out var directory))return false;
            string image=Encode(record);Decode(image);string path=Path.Combine(directory,record.Operation.ToString("N")+".xml");
            if(ExistsOriginal(record.World,record.Operation))return TryRead(record.World,record.Operation,out var prior)&&Encode(prior)==image;
            return Publish(path,image);
        }
        catch(Exception error){Log.Warning("[REBIRTH Farming] harvest reservation refused: "+error.GetType().Name);return false;}
    }
    internal static bool TryAdvance(Record original,Phase next,out Record advanced)
    {
        advanced=null;
        try
        {
            if(original==null||next==Phase.SourcePublished||(int)next!=(int)original.State+1||!Scope(original.World,out var directory)||!TryRead(original.World,original.Operation,out var current)||Encode(current)!=Encode(original))return false;
            current.State=next;string image=Encode(current);Decode(image);
            if(!Publish(Path.Combine(directory,current.Operation.ToString("N")+".xml"),image))return false;
            advanced=current;return true;
        }
        catch(Exception error){Log.Warning("[REBIRTH Farming] original harvest transition retained: "+error.GetType().Name);return false;}
    }
    internal static bool TryPublishSource(Record original,AdvancedFarmingHarvestPublication.Proof proof,out Record advanced)
    {
        advanced=null;
        try
        {
            if(original==null||original.State!=Phase.Reserved||proof==null||proof.Operation!=original.Operation||proof.Source!=original.Source||proof.World!=original.World||proof.X!=original.X||proof.Y!=original.Y||proof.Z!=original.Z||proof.Generation<=0||!Digest(proof.Digest)||!Scope(original.World,out var directory)||!TryRead(original.World,original.Operation,out var current)||Encode(current)!=Encode(original))return false;
            current.NativeSourceDigest=proof.Digest;current.NativeSourceGeneration=proof.Generation;current.State=Phase.SourcePublished;
            string image=Encode(current);Decode(image);if(!Publish(Path.Combine(directory,current.Operation.ToString("N")+".xml"),image))return false;
            advanced=current;return true;
        }
        catch(Exception error){Log.Warning("[REBIRTH Farming] original native source proof retained: "+error.GetType().Name);return false;}
    }
    internal static bool TryPublishDrops(Record original,AdvancedFarmingHarvestDrops.Proof proof,out Record advanced)
    {
        advanced=null;
        try
        {
            if(original==null||original.State!=Phase.OwnerPublished||proof==null||proof.Operation!=original.Operation||proof.Source!=original.Source||proof.World!=original.World||proof.Generation<=0||!Digest(proof.NativeDigest)||proof.OutputDigest!=OutputDigest(OutputImage(Remaining(original)))||!Scope(original.World,out var directory)||!TryRead(original.World,original.Operation,out var current)||Encode(current)!=Encode(original))return false;
            current.DropOutputDigest=proof.OutputDigest;current.DropNativeDigest=proof.NativeDigest;current.DropNativeGeneration=proof.Generation;current.State=Phase.Retired;
            string image=Encode(current);Decode(image);if(!Publish(Path.Combine(directory,current.Operation.ToString("N")+".xml"),image))return false;advanced=current;return true;
        }
        catch(Exception error){Log.Warning("[REBIRTH Farming] original native drop proof retained: "+error.GetType().Name);return false;}
    }
}
