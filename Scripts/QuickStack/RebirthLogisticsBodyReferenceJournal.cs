using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

// Frozen projected Prepared metadata. No native invocation producer is enabled
// by this store. Old fatal/native images are immutable; rejection is explicit.
internal static class RebirthLogisticsBodyReferenceJournal
{
    private const int MaximumBytes=786432, MaximumGenerations=16;
    private static string PathFor(RebirthLogisticsTransferJournal.Record original)
        =>Path.Combine(GameIO.GetSaveGameDir(),"RebirthLogisticsBodyReferences",original.Owner,original.Operation.ToString("N")+".xml");
    private static string ArchiveFor(RebirthLogisticsTransferJournal.Record original)
        =>Path.Combine(GameIO.GetSaveGameDir(),"RebirthLogisticsBodyReferences","archive",original.Owner,original.Operation.ToString("N")+".xml");
    internal static bool HasCandidate(RebirthLogisticsTransferJournal.Record original)
    {
        if(original==null)return true;
        string path=PathFor(original);return File.Exists(path)||File.Exists(path+".tmp")||File.Exists(path+".bak")||File.Exists(ArchiveFor(original));
    }
    private static long ReservationBytes(RebirthLogisticsTransferJournal.Record original)
    {
        long bytes=0;
        foreach(string path in new[]{PathFor(original),Path.Combine(GameIO.GetSaveGameDir(),"RebirthLogisticsNativeInvocations",original.Owner,original.Operation.ToString("N")+".xml")})
        {
            if(!File.Exists(path))continue;
            var info=new FileInfo(path);if(info.Length<1||info.Length>MaximumBytes||(info.Attributes&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException();
            bytes=checked(bytes+info.Length);
        }
        // Original strings, detached codec read/roundtrip, projected streams,
        // strict XML rewrite and candidate readback coexist inside this charge.
        return checked(1048576L+32L*bytes);
    }
    private static XElement Read(RebirthLogisticsTransferJournal.Record original)
    {
        string path=PathFor(original),archive=ArchiveFor(original);
        if(File.Exists(archive))
        {
            if(original.Phase!=4||File.Exists(path)||File.Exists(path+".tmp")||File.Exists(path+".bak"))throw new InvalidDataException();
            path=archive;
        }
        if(File.Exists(path+".tmp")||File.Exists(path+".bak"))throw new InvalidDataException();
        return ReadAt(original,path);
    }
    private static XElement ReadAt(RebirthLogisticsTransferJournal.Record original,string path)
    {
        var info=new FileInfo(path);if(info.Length<1||info.Length>MaximumBytes||(info.Attributes&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException();
        XElement root;
        using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))
        using(var reader=XmlReader.Create(stream,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=MaximumBytes}))root=XElement.Load(reader);
        string[] attrs={"version","operation","owner","world","creation","binding","deaths"};
        if(root.Name!="logisticsBodyReferences"||root.Attributes().Count()!=attrs.Length||root.Attributes().Any(a=>a.Name.Namespace!=XNamespace.None||!attrs.Contains(a.Name.LocalName))||
            (string)root.Attribute("version")!="1"||(string)root.Attribute("operation")!=original.Operation.ToString("N")||
            (string)root.Attribute("owner")!=original.Owner||(string)root.Attribute("world")!=original.World||(string)root.Attribute("creation")!=original.Creation||
            (string)root.Attribute("binding")!=RebirthLogisticsTransferJournal.ImmutablePlanDigest(original)||(string)root.Attribute("deaths")!=original.Deaths.ToString(System.Globalization.CultureInfo.InvariantCulture)||
            root.Elements().Count()<1||root.Elements().Count()>MaximumGenerations||root.Nodes().Any(n=>!(n is XElement)))throw new InvalidDataException();
        int index=0;string old=null,nonce=null;
        foreach(var row in root.Elements())
        {
            if(row.Name!="projection"||row.Attributes().Count()!=2||row.Attribute("index")==null||row.Attribute("state")==null||
                (string)row.Attribute("index")!=(index++).ToString(System.Globalization.CultureInfo.InvariantCulture)||row.Elements().Count()!=1||row.Element("bodyReference")==null||row.Nodes().Any(n=>!(n is XElement)))throw new InvalidDataException();
            string state=(string)row.Attribute("state");
            if(state!="prepared"&&state!="rejected"&&state!="attempted")throw new InvalidDataException();
            if(row!=root.Elements().Last()&&state!="rejected")throw new InvalidDataException();
            var witness=RebirthLogisticsBodyReferenceWitness.Read(row.Element("bodyReference"));
            if(witness.Binding!=RebirthLogisticsTransferJournal.ImmutablePlanDigest(original)||old!=null&&(old!=witness.Original||nonce!=witness.Nonce))throw new InvalidDataException();
            old=witness.Original;nonce=witness.Nonce;
        }
        return root;
    }
    private static bool Publish(RebirthLogisticsTransferJournal.Record original,XElement root,bool replace)
    {
        string path=PathFor(original);byte[] bytes=new UTF8Encoding(false).GetBytes(root.ToString(SaveOptions.DisableFormatting));
        if(bytes.Length>MaximumBytes||File.Exists(path+".tmp")||File.Exists(path+".bak"))return false;
        string directory=Path.Combine(GameIO.GetSaveGameDir(),"RebirthLogisticsBodyReferences");long total=bytes.Length;int count=0;
        if(Directory.Exists(directory))
        {
            if((File.GetAttributes(directory)&FileAttributes.ReparsePoint)!=0)return false;
            foreach(string folder in Directory.EnumerateDirectories(directory))
            {
                if((File.GetAttributes(folder)&FileAttributes.ReparsePoint)!=0)return false;
                if(Path.GetFileName(folder)=="archive")continue;
                if(Path.GetFileName(folder).Length!=64||Directory.EnumerateDirectories(folder).Any())return false;
                foreach(string file in Directory.EnumerateFiles(folder))
                {
                    var info=new FileInfo(file);if(info.Length<1||info.Length>MaximumBytes||(info.Attributes&FileAttributes.ReparsePoint)!=0)return false;
                    bool archived=false;
                    if(Path.GetExtension(file)==".xml"&&Guid.TryParseExact(Path.GetFileNameWithoutExtension(file),"N",out var operation)&&
                        RebirthLogisticsTransferJournal.TryRead(Path.GetFileName(folder),operation,out var terminal)&&terminal.Phase==4&&
                        !File.Exists(file+".tmp")&&!File.Exists(file+".bak"))
                    {
                        // Full strict original-bound reader before moving; no
                        // volatile terminal flag and no evidence deletion.
                        Read(terminal);string destination=ArchiveFor(terminal);
                        if(File.Exists(destination))return false;
                        Directory.CreateDirectory(Path.GetDirectoryName(destination));File.Move(file,destination);archived=true;
                    }
                    if(!archived){if(++count>128)return false;total=checked(total+info.Length);if(total>32L*1024*1024)return false;}
                }
            }
        }
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        using(var stream=new FileStream(path+".tmp",FileMode.CreateNew,FileAccess.Write,FileShare.None)){stream.Write(bytes,0,bytes.Length);stream.Flush(true);}
        if(replace)File.Replace(path+".tmp",path,null);else File.Move(path+".tmp",path);
        return XNode.DeepEquals(Read(original),root);
    }
    internal static bool TryRecoverMetadata(RebirthLogisticsTransferJournal.Record original)
    {
        try
        {
            if(!ThreadManager.IsMainThread()||GameManager.Instance?.World?.IsRemote()!=false||original==null||original.Phase>=4||
                original.World!=RebirthLogisticsTransferJournal.WorldKey(GameManager.Instance.World)||
                !RebirthLogisticsTransferJournal.TryRead(original.Owner,original.Operation,out var retained)||RebirthLogisticsTransferJournal.Encode(retained)!=RebirthLogisticsTransferJournal.Encode(original))return false;
            string path=PathFor(original);if(File.Exists(path+".bak")||File.Exists(ArchiveFor(original)))return false;
            if(!File.Exists(path+".tmp"))return true;
            using(var account=RebirthLogisticsSnapshotReservation.TryOpenRetainedRead(original,()=>GameManager.Instance?.World?.IsRemote()==false))
            {
                long tempBytes=new FileInfo(path+".tmp").Length;
                if(tempBytes<1||tempBytes>MaximumBytes||account==null||!account.TryReserveAllocation(checked(ReservationBytes(original)+32L*tempBytes),out var allocation))return false;
                using(allocation)
                {
                    if(!RebirthLogisticsNativeInvocationJournal.TryReadStored(original,out var nonce,out var native,out int state)||state!=0)return false;
                    var next=ReadAt(original,path+".tmp");var last=next.Elements().Last();var witness=RebirthLogisticsBodyReferenceWitness.Read(last.Element("bodyReference"));
                    if(witness.Nonce!=nonce||witness.Original!=native)return false;
                    if(!File.Exists(path))
                    {
                        if(next.Elements().Count()!=1||(string)last.Attribute("state")!="prepared")return false;
                        File.Move(path+".tmp",path);return XNode.DeepEquals(Read(original),next);
                    }
                    var before=ReadAt(original,path);bool valid=false;
                    if(next.Elements().Count()==before.Elements().Count()&&(string)before.Elements().Last().Attribute("state")=="prepared"&&
                        ((string)last.Attribute("state")=="rejected"||(string)last.Attribute("state")=="attempted"))
                    {var normalized=new XElement(next);normalized.Elements().Last().SetAttributeValue("state","prepared");valid=XNode.DeepEquals(normalized,before);}
                    else if(next.Elements().Count()==before.Elements().Count()+1&&(string)before.Elements().Last().Attribute("state")=="rejected"&&(string)last.Attribute("state")=="prepared")
                    {var normalized=new XElement(next);normalized.Elements().Last().Remove();valid=XNode.DeepEquals(normalized,before);}
                    if(!valid)return false;
                    File.Replace(path+".tmp",path,null);return XNode.DeepEquals(Read(original),next);
                }
            }
        }
        catch{return false;}
    }
    internal static bool TryFreezeCurrent(RebirthLogisticsTransferJournal.Record original,EntityPlayer actor,ClientInfo sender)
    {
        try
        {
            if(original==null)return false;
            using(var account=RebirthLogisticsSnapshotReservation.TryOpenRetainedRead(original,()=>ReferenceEquals(GameManager.Instance?.World,actor?.world)))
            {
            if(account==null||!account.TryReserveAllocation(ReservationBytes(original),out var allocation))return false;
            using(allocation)
            {
            if(original.Phase>=4||RebirthLogisticsAcceptedBodyJournal.HasAnyCandidate(original)||!RebirthLogisticsBodyReferenceWitness.TryCurrentNativeOwner(actor,sender,original.Owner,out int nativeOwner)||
                original.World!=RebirthLogisticsTransferJournal.WorldKey(actor.world)||original.Creation!=RebirthCamperCharacterAccess.CurrentCreation(actor)||
                actor.Died<original.Deaths||actor.Died>original.Deaths+1||!RebirthLogisticsTransferJournal.TryRead(original.Owner,original.Operation,out var retained)||
                RebirthLogisticsTransferJournal.Encode(retained)!=RebirthLogisticsTransferJournal.Encode(original)||
                !RebirthLogisticsNativeDeathReceipt.TryRead(original,out var fatal)||fatal.OwnerEntity==nativeOwner||
                !RebirthLogisticsNativeInvocationJournal.TryReadStored(original,out var nonce,out var native,out int state)||state!=0)return false;
            var witness=RebirthLogisticsBodyReferenceWitness.Create(native,fatal.OwnerEntity,nativeOwner,nonce,RebirthLogisticsTransferJournal.ImmutablePlanDigest(original));
            XElement root;
            bool replace=HasCandidate(original);
            if(replace)
            {
                root=Read(original);var last=root.Elements().Last();var previous=RebirthLogisticsBodyReferenceWitness.Read(last.Element("bodyReference"));
                if((string)last.Attribute("state")=="prepared")return XNode.DeepEquals(last.Element("bodyReference"),RebirthLogisticsBodyReferenceWitness.Write(witness));
                if((string)last.Attribute("state")!="rejected"||root.Elements().Count()>=MaximumGenerations||previous.Original!=witness.Original||previous.Nonce!=witness.Nonce)return false;
            }
            else root=new XElement("logisticsBodyReferences",new XAttribute("version",1),new XAttribute("operation",original.Operation.ToString("N")),new XAttribute("owner",original.Owner),
                new XAttribute("world",original.World),new XAttribute("creation",original.Creation),new XAttribute("binding",witness.Binding),new XAttribute("deaths",original.Deaths));
            root.Add(new XElement("projection",new XAttribute("index",root.Elements().Count()),new XAttribute("state","prepared"),RebirthLogisticsBodyReferenceWitness.Write(witness)));
            return Publish(original,root,replace);
            }
            }
        }
        catch{return false;}
    }
    internal sealed class Frozen:IDisposable
    {
        internal RebirthLogisticsBodyReferenceWitness.Witness Witness;
        internal string State;
        private RebirthLogisticsTransferJournal.Record original;
        private XElement root;
        private RebirthLogisticsSnapshotReservation account;
        private IDisposable allocation;
        internal static Frozen Open(RebirthLogisticsTransferJournal.Record original)
        {
            var result=new Frozen{original=original};
            try
            {
                result.account=RebirthLogisticsSnapshotReservation.TryOpenRetainedRead(original,()=>GameManager.Instance?.World?.IsRemote()==false);
                if(result.account==null||!result.account.TryReserveAllocation(ReservationBytes(original),out result.allocation)){result.Dispose();return null;}
                result.root=Read(original);var last=result.root.Elements().Last();
                result.Witness=RebirthLogisticsBodyReferenceWitness.Read(last.Element("bodyReference"));result.State=(string)last.Attribute("state");
                if(!RebirthLogisticsNativeInvocationJournal.TryReadOriginalReferenceEvidence(original,out var nonce,out var native,out int state)||
                    state!=0||nonce!=result.Witness.Nonce||native!=result.Witness.Original){result.Dispose();return null;}
                return result;
            }
            catch{result.Dispose();return null;}
        }
        internal bool IsCurrent(EntityPlayer actor,ClientInfo sender)
        {
            try
            {
                return account!=null&&Witness!=null&&original.Phase<4&&
                    RebirthLogisticsBodyReferenceWitness.TryCurrentNativeOwner(actor,sender,original.Owner,out int entity)&&entity==Witness.NativeOwner&&
                    original.World==RebirthLogisticsTransferJournal.WorldKey(actor.world)&&original.Creation==RebirthCamperCharacterAccess.CurrentCreation(actor)&&actor.Died>=original.Deaths&&actor.Died<=original.Deaths+1&&
                    RebirthLogisticsTransferJournal.TryRead(original.Owner,original.Operation,out var retained)&&RebirthLogisticsTransferJournal.Encode(retained)==RebirthLogisticsTransferJournal.Encode(original)&&
                    RebirthLogisticsNativeDeathReceipt.TryRead(original,out var fatal)&&fatal.OwnerEntity==Witness.HistoricalOwner&&
                    RebirthLogisticsNativeInvocationJournal.TryReadStored(original,out var nonce,out var native,out int state)&&state==0&&nonce==Witness.Nonce&&native==Witness.Original&&XNode.DeepEquals(Read(original),root);
            }
            catch{return false;}
        }
        internal bool TryMarkAttempted(EntityPlayer actor,ClientInfo sender)
        {
            try
            {
                if(account==null||State!="prepared"||original.Phase>=4||!RebirthLogisticsBodyReferenceWitness.TryCurrentNativeOwner(actor,sender,original.Owner,out int entity)||entity!=Witness.NativeOwner||
                    original.World!=RebirthLogisticsTransferJournal.WorldKey(actor.world)||original.Creation!=RebirthCamperCharacterAccess.CurrentCreation(actor)||actor.Died<original.Deaths||actor.Died>original.Deaths+1||
                    !RebirthLogisticsNativeDeathReceipt.TryRead(original,out var fatal)||fatal.OwnerEntity!=Witness.HistoricalOwner||
                    !RebirthLogisticsTransferJournal.TryRead(original.Owner,original.Operation,out var retained)||RebirthLogisticsTransferJournal.Encode(retained)!=RebirthLogisticsTransferJournal.Encode(original)||
                    !RebirthLogisticsNativeInvocationJournal.TryReadStored(original,out var nonce,out var native,out int state)||state!=0||nonce!=Witness.Nonce||native!=Witness.Original||!XNode.DeepEquals(Read(original),root))return false;
                var next=new XElement(root);next.Elements().Last().SetAttributeValue("state","attempted");
                if(!Publish(original,next,true))return false;
                root=next;State="attempted";return true;
            }
            catch{return false;}
        }
        public void Dispose(){Witness=null;root=null;allocation?.Dispose();allocation=null;account?.Dispose();account=null;}
    }
    internal static Frozen TryOpenFrozen(RebirthLogisticsTransferJournal.Record original)
        =>original==null?null:Frozen.Open(original);
    internal static bool BlocksNewAttempt(RebirthLogisticsTransferJournal.Record original)
    {
        if(!HasCandidate(original))return false;
        using(var frozen=TryOpenFrozen(original))return frozen==null||frozen.State!="prepared";
    }
    internal static bool WasAttemptedOriginal(RebirthLogisticsTransferJournal.Record original,string exactOriginal,string nonce)
    {
        if(!HasCandidate(original))return false;
        using(var frozen=TryOpenFrozen(original))return frozen!=null&&frozen.State=="attempted"&&frozen.Witness.Original==exactOriginal&&frozen.Witness.Nonce==nonce;
    }
    internal static bool TryMatchAccepted(RebirthLogisticsTransferJournal.Record original,int nativeOwner,string bagHash,string entityHash,
        out int historicalOwner,out string reference,out string historicalEntityHash)
    {
        historicalOwner=-1;reference=historicalEntityHash=null;
        try
        {
            using(var frozen=TryOpenFrozen(original))
            {
                if(frozen==null||frozen.State!="attempted"||!RebirthLogisticsNativeDeathReceipt.TryRead(original,out var fatal)||
                    !RebirthLogisticsBodyReferenceWitness.MatchesProjectedBody(frozen.Witness,fatal,nativeOwner,bagHash,entityHash))return false;
                historicalOwner=frozen.Witness.HistoricalOwner;reference=RebirthLogisticsBodyReferenceWitness.Fingerprint(frozen.Witness);historicalEntityHash=fatal.Physical.BodyEntityHash;return true;
            }
        }
        catch{return false;}
    }
    // Caller must first positively identify the exact frozen intermediate row.
    // Original v2 must STILL be exact never-invoked Prepared0. No absence proof.
    internal static bool TryRejectPrepared(RebirthLogisticsTransferJournal.Record original,EntityPlayer actor,ClientInfo sender,string exactProjected,string exactNonce)
    {
        try
        {
            if(original==null)return false;
            using(var account=RebirthLogisticsSnapshotReservation.TryOpenRetainedRead(original,()=>GameManager.Instance?.World?.IsRemote()==false))
            {
            if(account==null||!account.TryReserveAllocation(ReservationBytes(original),out var allocation))return false;
            using(allocation)
            {
            if(!ThreadManager.IsMainThread()||GameManager.Instance?.World?.IsRemote()!=false||original.Phase>=4||
                !RebirthLogisticsBodyReferenceWitness.TryCurrentNativeOwner(actor,sender,original.Owner,out _)||
                original.Creation!=RebirthCamperCharacterAccess.CurrentCreation(actor)||actor.Died<original.Deaths||actor.Died>original.Deaths+1||
                original.World!=RebirthLogisticsTransferJournal.WorldKey(GameManager.Instance.World)||
                !RebirthLogisticsTransferJournal.TryRead(original.Owner,original.Operation,out var retained)||RebirthLogisticsTransferJournal.Encode(retained)!=RebirthLogisticsTransferJournal.Encode(original)||
                !RebirthLogisticsNativeInvocationJournal.TryReadStored(original,out var nonce,out var native,out int state)||state!=0||nonce!=exactNonce)return false;
            var root=Read(original);var last=root.Elements().Last();var witness=RebirthLogisticsBodyReferenceWitness.Read(last.Element("bodyReference"));
            if(witness.Projected!=exactProjected||witness.Nonce!=nonce||witness.Original!=native)return false;
            if((string)last.Attribute("state")=="rejected")return true;
            if((string)last.Attribute("state")!="prepared")return false;
            last.SetAttributeValue("state","rejected");return Publish(original,root,true);
            }
            }
        }
        catch{return false;}
    }
}
