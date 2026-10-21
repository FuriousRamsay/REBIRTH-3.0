using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

internal static partial class RebirthPlacedOriginalDeathPartition
{
    // An individual delivery remains immutable across all retries. A later life
    // cannot replace its Before/After or replay its additive slot fragment.
    private sealed class OwedChild
    {
        internal Guid Parent,Operation;internal string World,Owner,Creation,Partition,Head,Before,After,Proof=string.Empty;internal int Deaths,Sequence,Row,DeliveredBefore,Quantity,Slot,Phase;internal bool Belt,Applied;
    }
    private static string ChildBody(OwedChild value){var xml=XElement.Parse(BareChildBody(value));xml.SetAttributeValue("checksum",Hash(BareChildBody(value)));return xml.ToString(SaveOptions.DisableFormatting);}
    private static string BareChildBody(OwedChild c)=>new XElement("placedOwedChild",new XAttribute("version",1),new XAttribute("parent",c.Parent.ToString("N")),new XAttribute("operation",c.Operation.ToString("N")),new XAttribute("world",c.World),new XAttribute("owner",c.Owner),new XAttribute("creation",c.Creation),new XAttribute("partition",c.Partition),new XAttribute("head",c.Head),new XAttribute("deliveredBefore",c.DeliveredBefore),new XAttribute("deaths",c.Deaths),new XAttribute("sequence",c.Sequence),new XAttribute("row",c.Row),new XAttribute("quantity",c.Quantity),new XAttribute("slot",c.Slot),new XAttribute("belt",c.Belt),new XAttribute("phase",c.Phase),new XAttribute("applied",c.Applied),new XAttribute("proof",c.Proof),new XElement("before",c.Before),new XElement("after",c.After)).ToString(SaveOptions.DisableFormatting);
    private static OwedChild DecodeChild(string image)
    {
        if(image==null||image.Length>Maximum)throw new InvalidDataException();XElement x;using(var input=new StringReader(image))using(var reader=XmlReader.Create(input,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=Maximum}))x=XElement.Load(reader);if(x.Name!="placedOwedChild"||(int?)x.Attribute("version")!=1)throw new InvalidDataException();
        var c=new OwedChild{Parent=Guid.ParseExact((string)x.Attribute("parent"),"N"),Operation=Guid.ParseExact((string)x.Attribute("operation"),"N"),World=(string)x.Attribute("world"),Owner=(string)x.Attribute("owner"),Creation=(string)x.Attribute("creation"),Partition=(string)x.Attribute("partition"),Head=(string)x.Attribute("head"),DeliveredBefore=(int)x.Attribute("deliveredBefore"),Deaths=(int)x.Attribute("deaths"),Sequence=(int)x.Attribute("sequence"),Row=(int)x.Attribute("row"),Quantity=(int)x.Attribute("quantity"),Slot=(int)x.Attribute("slot"),Belt=(bool)x.Attribute("belt"),Phase=(int)x.Attribute("phase"),Applied=(bool)x.Attribute("applied"),Proof=(string)x.Attribute("proof"),Before=(string)x.Element("before"),After=(string)x.Element("after")};
        if(c.Parent==Guid.Empty||c.Operation==Guid.Empty||c.Parent==c.Operation||!Guid.TryParse(c.World,out _)||!RebirthOriginalActionScope.Validate(c.Owner,c.Creation)||!FullHash(c.Partition)||!FullHash(c.Head)||c.DeliveredBefore<0||c.DeliveredBefore>ushort.MaxValue-c.Quantity||c.Deaths<1||c.Sequence<1||c.Row<0||c.Row>=525||c.Quantity<1||c.Quantity>ushort.MaxValue||c.Phase<0||c.Phase>4||c.Applied&&c.Phase<2||c.Phase==3&&!c.Applied||c.Phase==4&&c.Applied||c.Proof==null||(c.Phase>=3?!FullHash(c.Proof):c.Proof.Length!=0)||ChildBody(c)!=image)throw new InvalidDataException();
        var before=RebirthPlacedOriginal.ReadActor(c.Before);var after=RebirthPlacedOriginal.ReadActor(c.After);var target=c.Belt?after.Belt:after.Bag;if(c.Slot<0||c.Slot>=target.Length||target[c.Slot].IsEmpty()||target[c.Slot].count!=c.Quantity)throw new InvalidDataException();
        if(!TryPlanOwedFragment(before,target[c.Slot],out var plan,out bool belt,out int slot,out int quantity)||belt!=c.Belt||slot!=c.Slot||quantity!=c.Quantity||!plan.Matches(after))throw new InvalidDataException();return c;
    }
    private static bool SameChild(OwedChild a,OwedChild b)=>a.Parent==b.Parent&&a.Operation==b.Operation&&a.World==b.World&&a.Owner==b.Owner&&a.Creation==b.Creation&&a.Partition==b.Partition&&a.Head==b.Head&&a.DeliveredBefore==b.DeliveredBefore&&a.Deaths==b.Deaths&&a.Sequence==b.Sequence&&a.Row==b.Row&&a.Quantity==b.Quantity&&a.Slot==b.Slot&&a.Belt==b.Belt&&a.Before==b.Before&&a.After==b.After;
    private static string ChildPath(OwedChild c,bool server)=>Path.Combine(GameIO.GetSaveGameDir(),"rebirth","placed-original",server?"owed-children-server":"owed-children-owner",c.Parent.ToString("N"),c.Sequence.ToString("D10",System.Globalization.CultureInfo.InvariantCulture)+".xml");
    private static bool ReadChild(string path,out OwedChild c)
    {c=null;try{if(!File.Exists(path)||new FileInfo(path).Length>Maximum)return false;c=DecodeChild(File.ReadAllText(path,new UTF8Encoding(false,true)));string name=c.Sequence.ToString("D10",System.Globalization.CultureInfo.InvariantCulture)+".xml";return Path.GetFileName(Path.GetDirectoryName(path))==c.Parent.ToString("N")&&(Path.GetFileName(path)==name||Path.GetFileName(path).StartsWith(name+".",StringComparison.Ordinal));}catch{return false;}}
    private static bool SaveChild(OwedChild c,bool server,string expected=null)
    {
        try
        {
            string path=ChildPath(c,server),image=ChildBody(c);DecodeChild(image);if(File.Exists(path)||File.Exists(path+".bak")){if(!ReadChild(path,out var old)||!SameChild(old,c)||expected==null&&ChildBody(old)!=image||expected!=null&&ChildBody(old)!=expected)return false;}else if(expected!=null)return false;Directory.CreateDirectory(Path.GetDirectoryName(path));
            var pending=Directory.EnumerateFiles(Path.GetDirectoryName(path),Path.GetFileName(path)+".*.tmp").ToArray();if(pending.Length!=0){if(pending.Length!=1||new FileInfo(pending[0]).Length>Maximum||ChildBody(DecodeChild(File.ReadAllText(pending[0],new UTF8Encoding(false,true))))!=image)return false;return RebirthDurableFileCommit.TryPublish(pending[0],path,out _)&&ReadChild(path,out var recovered)&&ChildBody(recovered)==image;}
            string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";var bytes=new UTF8Encoding(false,true).GetBytes(image);using(var stream=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){stream.Write(bytes,0,bytes.Length);stream.Flush(true);}if(!ReadChild(temp,out var prepared)||ChildBody(prepared)!=image)return false;return RebirthDurableFileCommit.TryPublish(temp,path,out _)&&ReadChild(path,out var saved)&&ChildBody(saved)==image;
        }catch{return false;}
    }
    private static bool RecoverChildFile(OwedChild key,bool server)
    {
        try
        {
            string path=ChildPath(key,server),folder=Path.GetDirectoryName(path);if(!Directory.Exists(folder))return true;var pending=Directory.EnumerateFiles(folder,Path.GetFileName(path)+".*.tmp").ToArray();if(pending.Length==0)return true;if(pending.Length!=1||!ReadChild(pending[0],out var prepared))return false;
            var world=GameManager.Instance?.World;if(world==null||prepared.World!=world.worldState.Guid||prepared.Parent!=key.Parent||prepared.Sequence!=key.Sequence)return false;
            if(File.Exists(path)||File.Exists(path+".bak")){if(!ReadChild(path,out var old)||!SameChild(old,prepared)||prepared.Phase<old.Phase||old.Applied&&!prepared.Applied||old.Phase>=3&&ChildBody(old)!=ChildBody(prepared))return false;}
            if(server){if(world.IsRemote()||prepared.Phase!=1&&prepared.Phase!=3&&prepared.Phase!=4||!OwedSource(prepared,out _,out var head,out _)||head.Sequence!=prepared.Sequence-1&&head.Sequence!=prepared.Sequence)return false;}
            else if(!(world.GetPrimaryPlayer() is EntityPlayerLocal player)||!OwedIdentity(player,prepared))return false;
            // Canonical original-bound prepared terminal carries the positive
            // native proof produced before publication failed. Never replace it
            // with a newly computed hash from a later owner-file snapshot.
            return RebirthDurableFileCommit.TryPublish(pending[0],path,out _)&&ReadChild(path,out var saved)&&ChildBody(saved)==ChildBody(prepared);
        }catch{return false;}
    }
    internal static int OwedDeliveredDelta(int phase,int quantity)
    {if(quantity<1||quantity>ushort.MaxValue||phase!=3&&phase!=4)throw new InvalidDataException();return phase==3?quantity:0;}
    private sealed class OwedHead
    {internal Guid Parent;internal string Partition;internal int Sequence;internal int[] Delivered;}
    private static string HeadBody(OwedHead value){var xml=XElement.Parse(BareHeadBody(value));xml.SetAttributeValue("checksum",Hash(BareHeadBody(value)));return xml.ToString(SaveOptions.DisableFormatting);}
    private static string BareHeadBody(OwedHead h)=>new XElement("placedOwedHead",new XAttribute("version",1),new XAttribute("parent",h.Parent.ToString("N")),new XAttribute("partition",h.Partition),new XAttribute("sequence",h.Sequence),h.Delivered.Select(value=>new XElement("delivered",value))).ToString(SaveOptions.DisableFormatting);
    private static OwedHead DecodeHead(string image)
    {
        if(image==null||image.Length>65536)throw new InvalidDataException();XElement x;using(var input=new StringReader(image))using(var reader=XmlReader.Create(input,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=65536}))x=XElement.Load(reader);if(x.Name!="placedOwedHead"||(int?)x.Attribute("version")!=1)throw new InvalidDataException();
        var h=new OwedHead{Parent=Guid.ParseExact((string)x.Attribute("parent"),"N"),Partition=(string)x.Attribute("partition"),Sequence=(int)x.Attribute("sequence"),Delivered=x.Elements("delivered").Select(row=>(int)row).ToArray()};if(h.Parent==Guid.Empty||!FullHash(h.Partition)||h.Sequence<0||h.Delivered.Length<1||h.Delivered.Length>525||h.Delivered.Any(value=>value<0||value>ushort.MaxValue)||HeadBody(h)!=image)throw new InvalidDataException();return h;
    }
    private static string HeadPath(Guid parent)=>Path.Combine(GameIO.GetSaveGameDir(),"rebirth","placed-original","owed-children-server",parent.ToString("N"),"head.xml");
    private static bool ReadHead(Guid parent,out OwedHead h)
    {h=null;try{string path=HeadPath(parent);if(!File.Exists(path)||new FileInfo(path).Length>65536)return false;h=DecodeHead(File.ReadAllText(path,new UTF8Encoding(false,true)));return h.Parent==parent;}catch{return false;}}
    private static bool SaveHead(OwedHead h,string expected=null)
    {
        try
        {
            string path=HeadPath(h.Parent),image=HeadBody(h);DecodeHead(image);if(File.Exists(path)||File.Exists(path+".bak")){if(!ReadHead(h.Parent,out var old)||old.Partition!=h.Partition||expected==null&&HeadBody(old)!=image||expected!=null&&HeadBody(old)!=expected)return false;}else if(expected!=null)return false;Directory.CreateDirectory(Path.GetDirectoryName(path));
            var pending=Directory.EnumerateFiles(Path.GetDirectoryName(path),"head.xml.*.tmp").ToArray();if(pending.Length!=0){if(pending.Length!=1||new FileInfo(pending[0]).Length>65536||HeadBody(DecodeHead(File.ReadAllText(pending[0],new UTF8Encoding(false,true))))!=image)return false;return RebirthDurableFileCommit.TryPublish(pending[0],path,out _)&&ReadHead(h.Parent,out var recovered)&&HeadBody(recovered)==image;}
            string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";var bytes=new UTF8Encoding(false,true).GetBytes(image);using(var stream=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){stream.Write(bytes,0,bytes.Length);stream.Flush(true);}if(HeadBody(DecodeHead(File.ReadAllText(temp,new UTF8Encoding(false,true))))!=image)return false;return RebirthDurableFileCommit.TryPublish(temp,path,out _)&&ReadHead(h.Parent,out var saved)&&HeadBody(saved)==image;
        }catch{return false;}
    }
    private static bool OwnOwedPartition(OwedPartition p,PhysicalTrace t)
    {
        var original=t.Observation.Original;if(p.Operation!=original.Operation||p.Trace!=t.Digest||p.World!=original.World||p.Owner!=original.Owner||p.Creation!=original.Creation||original.Deaths==int.MaxValue||p.Deaths!=original.Deaths+1)return false;
        var rows=t.Inputs.Where(input=>t.Overflow||input.Failed).Select(input=>input.Item).ToArray();return Rows(rows).SequenceEqual(Convert.FromBase64String(p.Owed))&&(t.Creation.Length==0||RebirthPlacedOriginalBodyPublication.TryPublished(original.Operation,t.Creation,out _));
    }
    private static bool OwedHeadFor(OwedPartition p,out OwedHead h)
    {
        h=null;try
        {
            var rows=ReadRows(Convert.FromBase64String(p.Owed));string partition=Hash(p.Image);string path=HeadPath(p.Operation);
            if(!File.Exists(path)&&!File.Exists(path+".bak")){string folder=Path.GetDirectoryName(path);if(Directory.Exists(folder)&&Directory.EnumerateFiles(folder).Any(file=>!Path.GetFileName(file).StartsWith("head.xml.",StringComparison.Ordinal)))return false;var initial=new OwedHead{Parent=p.Operation,Partition=partition,Delivered=new int[rows.Length]};if(!SaveHead(initial))return false;}
            if(!ReadHead(p.Operation,out h)||h.Partition!=partition||h.Delivered.Length!=rows.Length)return false;for(int i=0;i<rows.Length;i++)if(h.Delivered[i]>rows[i].count)return false;if(h.Sequence>0){var key=new OwedChild{Parent=h.Parent,Sequence=h.Sequence};if(!ReadChild(ChildPath(key,true),out var terminal)||terminal.Phase<3||terminal.Parent!=h.Parent||terminal.Sequence!=h.Sequence||terminal.Partition!=h.Partition||terminal.Row>=h.Delivered.Length||h.Delivered[terminal.Row]!=terminal.DeliveredBefore+OwedDeliveredDelta(terminal.Phase,terminal.Quantity))return false;var prior=DecodeHead(HeadBody(h));prior.Sequence--;prior.Delivered[terminal.Row]-=OwedDeliveredDelta(terminal.Phase,terminal.Quantity);if(Hash(HeadBody(prior))!=terminal.Head)return false;}return true;
        }catch{h=null;return false;}
    }

    private static bool OwedIdentity(EntityPlayer actor,OwedChild c)=>ThreadManager.IsMainThread()&&actor!=null&&actor.world!=null&&ReferenceEquals(GameManager.Instance?.World,actor.world)&&ReferenceEquals(actor.world.GetEntity(actor.entityId),actor)&&actor.world.worldState.Guid==c.World&&RebirthCamperCharacterAccess.CurrentCreation(actor)==c.Creation&&RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var identity)&&identity.StorageKey==c.Owner;
    private static bool OwedCurrent(EntityPlayer actor,OwedChild c)=>OwedIdentity(actor,c)&&!actor.IsDead()&&actor.Died==c.Deaths;
    private static string ChildBinding(OwedChild c){var clone=DecodeChild(ChildBody(c));clone.Phase=0;clone.Applied=false;clone.Proof=string.Empty;return Hash(ChildBody(clone));}
    private const string OwedMarkerPrefix="rbPlacedOwedOriginal";
    private static void StampOwed(EntityPlayerLocal actor,OwedChild c,int phase)
    {if(!OwedCurrent(actor,c)||phase<1||phase>2)throw new InvalidDataException();string binding=ChildBinding(c);for(int i=0;i<16;i++)actor.Buffs.SetCustomVar(OwedMarkerPrefix+i,Convert.ToUInt16(binding.Substring(i*4,4),16),true);actor.Buffs.SetCustomVar(OwedMarkerPrefix+"Phase",phase,true);}
    private static bool SavedOwedMarker(PlayerDataFile file,OwedChild c,int phase)=>SavedOwedMarkerDigest(file,ChildBinding(c),phase);
    private static bool SavedOwedMarkerDigest(PlayerDataFile file,string binding,int phase)
    {
        try
        {
            var bytes=file.buffData?.ToArray();if(bytes==null||bytes.Length>1048576)return false;var values=new System.Collections.Generic.Dictionary<string,float>(StringComparer.Ordinal);
            using(var stream=new MemoryStream(bytes,false))using(var reader=MemoryPools.poolBinaryReader.AllocSync(true)){reader.SetBaseStream(stream);int version=reader.ReadByte();if(version!=EntityBuffs.Version||version<2)return false;int count=reader.ReadUInt16();for(int i=0;i<count;i++)new BuffValue().Read(reader,version);count=reader.ReadUInt16();for(int i=0;i<count;i++){string key=reader.ReadString();float value=reader.ReadSingle();if(key.StartsWith(OwedMarkerPrefix,StringComparison.Ordinal)){if(values.ContainsKey(key)||float.IsNaN(value)||float.IsInfinity(value))return false;values.Add(key,value);}}if(stream.Position!=stream.Length)return false;}
            if(!values.TryGetValue(OwedMarkerPrefix+"Phase",out float actual)||actual!=phase)return false;for(int i=0;i<16;i++)if(!values.TryGetValue(OwedMarkerPrefix+i,out float word)||word!=Convert.ToUInt16(binding.Substring(i*4,4),16))return false;return true;
        }catch{return false;}
    }
    private static bool SavedOwed(EntityPlayer actor,OwedChild c,int phase,out PlayerDataFile file)
    {file=null;return OwedCurrent(actor,c)&&RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var identity)&&RebirthCamperEnergySavedOwner.TryRead(identity,out var actual,out file)&&file.id==actor.entityId&&file.deaths==c.Deaths&&SavedOwedMarker(file,c,phase)&&actual.Matches(RebirthPlacedOriginal.ReadActor(phase==1?c.Before:c.After));}
    private static bool SavedOwedPublished(EntityPlayer actor,OwedChild c,out PlayerDataFile file)
    {
        file=null;return OwedIdentity(actor,c)&&actor.Died>=c.Deaths&&RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var identity)&&RebirthCamperEnergySavedOwner.TryRead(identity,out var actual,out file)&&file.id==actor.entityId&&file.deaths==c.Deaths&&SavedOwedMarker(file,c,2)&&actual.Matches(RebirthPlacedOriginal.ReadActor(c.After));
    }
    private static bool OwedSource(OwedChild c,out OwedPartition p,out OwedHead h,out ItemStack[] rows)
    {
        p=null;h=null;rows=null;try
        {
            if(!ReadOwed(c.Parent,out p)||Hash(p.Image)!=c.Partition||p.World!=c.World||p.Owner!=c.Owner||p.Creation!=c.Creation||c.Deaths<p.Deaths)return false;
            if(!ValidOwedPartitionSource(p)||!OwedHeadFor(p,out h))return false;rows=ReadRows(Convert.FromBase64String(p.Owed));return c.Row<rows.Length;
        }catch{return false;}
    }
    private static bool PrepareOwedChild(EntityPlayer actor,OwedChild request,out OwedChild accepted)
    {
        accepted=null;try
        {
            if(actor?.world?.IsRemote()!=false||request.Phase!=0)return false;if(OwedRejectionExists(request)){if(!OwedIdentity(actor,request)||actor.Died<request.Deaths)return false;return PublishOwedRejection(request,out accepted);}if(!OwedCurrent(actor,request)||!OwedSource(request,out var p,out var head,out var rows)||request.Sequence!=head.Sequence+1||request.Head!=Hash(HeadBody(head))||request.DeliveredBefore!=head.Delivered[request.Row]||!SavedOwed(actor,request,1,out var nativeBefore)||!CanRepresentOwedRecovery(request,SavedEquipment(nativeBefore)))return false;
            int first=-1;for(int i=0;i<rows.Length;i++)if(head.Delivered[i]<rows[i].count){first=i;break;}if(first!=request.Row)return false;
            var owed=rows[first].Clone();owed.count-=head.Delivered[first];if(!TryPlanOwedFragment(RebirthPlacedOriginal.ReadActor(request.Before),owed,out var expected,out bool belt,out int slot,out int quantity)||belt!=request.Belt||slot!=request.Slot||quantity!=request.Quantity||!expected.Matches(RebirthPlacedOriginal.ReadActor(request.After)))return false;
            string path=ChildPath(request,true);if(File.Exists(path)||File.Exists(path+".bak")){if(!ReadChild(path,out accepted)||!SameChild(accepted,request))return false;if(accepted.Phase<1)return false;TrackOwedNativeChild(actor,accepted);return true;}
            accepted=DecodeChild(ChildBody(request));accepted.Phase=1;if(!SaveChild(accepted,true))return false;TrackOwedNativeChild(actor,accepted);return true;
        }catch{accepted=null;return false;}
    }
    private static bool AdvanceOwedHead(OwedChild terminal)
    {
        if(terminal.Phase<3||!FullHash(terminal.Proof)||!OwedSource(terminal,out _,out var head,out var rows))return false;
        if(head.Sequence==terminal.Sequence){var before=DecodeHead(HeadBody(head));if(before.Delivered[terminal.Row]!=terminal.DeliveredBefore+OwedDeliveredDelta(terminal.Phase,terminal.Quantity))return false;before.Delivered[terminal.Row]-=OwedDeliveredDelta(terminal.Phase,terminal.Quantity);before.Sequence--;return Hash(HeadBody(before))==terminal.Head;}
        if(head.Sequence!=terminal.Sequence-1||Hash(HeadBody(head))!=terminal.Head||head.Delivered[terminal.Row]!=terminal.DeliveredBefore||terminal.Quantity>rows[terminal.Row].count-head.Delivered[terminal.Row])return false;
        string previous=HeadBody(head);head.Delivered[terminal.Row]+=OwedDeliveredDelta(terminal.Phase,terminal.Quantity);head.Sequence=terminal.Sequence;return SaveHead(head,previous);
    }
    private static bool OwedTerminalCovered(OwedChild terminal)
    {return terminal.Phase>=3&&FullHash(terminal.Proof)&&OwedSource(terminal,out _,out var head,out _)&&head.Sequence>=terminal.Sequence&&head.Delivered[terminal.Row]>=terminal.DeliveredBefore+OwedDeliveredDelta(terminal.Phase,terminal.Quantity);}
    private static bool CompleteOwedChild(EntityPlayer actor,OwedChild request,out OwedChild terminal)
    {
        terminal=null;try
        {
            if(actor.world?.IsRemote()!=false||!OwedIdentity(actor,request)||!RecoverChildFile(request,true)||!ReadChild(ChildPath(request,true),out var saved)||!SameChild(saved,request)||saved.Phase<1)return false;
            if(saved.Phase>=3){if(!AdvanceOwedHead(saved)&&!OwedTerminalCovered(saved))return false;terminal=saved;return true;}
            if(!SavedOwedPublished(actor,saved,out var file)){if(TryCompleteOwedUnused(actor,saved,out terminal)||TryCompleteEndedOwed(actor,saved,out terminal)||TryCompleteOwedDelete(actor,saved,out terminal)||TryCompleteOwedPhysical(actor,saved,out terminal))return true;return false;}using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream,Encoding.UTF8,true)){writer.Write(ChildBinding(saved));writer.Write(file.id);writer.Write(file.deaths);foreach(var bytes in new[]{file.bagData.ToArray(),file.inventoryData.ToArray(),file.buffData.ToArray()}){writer.Write(bytes.Length);writer.Write(bytes);}writer.Flush();using(var hash=System.Security.Cryptography.SHA256.Create())saved.Proof=BitConverter.ToString(hash.ComputeHash(stream.ToArray())).Replace("-","").ToLowerInvariant();}
            string previous=ChildBody(DecodeChild(File.ReadAllText(ChildPath(saved,true),new UTF8Encoding(false,true))));saved.Phase=3;saved.Applied=true;if(!SaveChild(saved,true,previous)||!AdvanceOwedHead(saved))return false;terminal=saved;return true;
        }catch{terminal=null;return false;}
    }

    internal static bool TryOwedTerminal(RebirthPlacedOriginal.Record original,string policy,bool paid,out string proof,out string capsule)
    {
        proof=null;capsule=null;try
        {
            var world=GameManager.Instance?.World;if(!ThreadManager.IsMainThread()||world?.IsRemote()!=false||world.worldState.Guid!=original.World||!ReadOwed(original.Operation,out var partition)||!ReadPhysical(Path.Combine(GameIO.GetSaveGameDir(),"rebirth","placed-original","physical-server",original.Operation.ToString("N")+".xml"),out var trace)||trace.Observation.Policy!=policy||trace.Observation.Paid!=paid||!RebirthPlacedOriginal.SamePartitionOriginal(original,trace.Observation.Original)||!OwnOwedPartition(partition,trace)||!OwedHeadFor(partition,out var head))return false;
            var rows=ReadRows(Convert.FromBase64String(partition.Owed));for(int i=0;i<rows.Length;i++)if(head.Delivered[i]!=rows[i].count)return false;string bodyProof=string.Empty;if(trace.Creation.Length!=0&&(!RebirthPlacedOriginalBodyPublication.TryPublished(original.Operation,trace.Creation,out bodyProof)||!RecordBodyInvocation(trace)))return false;
            proof=Hash("positive-owed-terminal|"+partition.Image+"|"+HeadBody(head)+"|"+bodyProof);capsule=trace.Observation.Original.Changes;return true;
        }catch{return false;}
    }

}
