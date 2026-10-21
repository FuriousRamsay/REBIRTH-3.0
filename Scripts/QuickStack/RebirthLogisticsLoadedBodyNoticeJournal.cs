using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

// Metadata readiness only. Prepared1 records an already published full native
// body witness; Ack2 records the authenticated owner's exact nav/ref readback.
// Neither state participates in item/source/debit/payment settlement.
internal static class RebirthLogisticsLoadedBodyNoticeJournal
{
    private const int MaximumBytes=1048576;
    internal static XElement Body(RebirthLogisticsLoadedBodyNotice.Notice n)=>new XElement("body",
        new XAttribute("operation",n.Operation.ToString("N")),new XAttribute("incarnation",n.Incarnation.ToString("N")),new XAttribute("nonce",n.Nonce.ToString("N")),
        new XAttribute("world",n.World),new XAttribute("owner",n.Owner),new XAttribute("creation",n.Creation),new XAttribute("command",n.Command),new XAttribute("binding",n.Binding),new XAttribute("proof",n.Proof),
        new XAttribute("mode",n.Suppressed?1:0),new XAttribute("policy",n.PolicyWitness),new XAttribute("deaths",n.Deaths),new XAttribute("life",n.Life),new XAttribute("entity",n.Entity),new XAttribute("class",n.Class),new XAttribute("x",n.X),new XAttribute("z",n.Z),new XAttribute("old",n.Old),new XAttribute("next",n.Next),
        new XElement("before",Convert.ToBase64String(n.Before)),new XElement("after",Convert.ToBase64String(n.After)));
    internal static string Digest(RebirthLogisticsLoadedBodyNotice.Notice n)=>RebirthLogisticsOriginalTransport.Digest(Body(n).ToString(SaveOptions.DisableFormatting));
    private static XElement Document(RebirthLogisticsLoadedBodyNotice.Notice n,int state)=>new XElement("notice",new XAttribute("version",1),new XAttribute("state",state),new XAttribute("digest",n.Digest),Body(n));
    private static string Root=>Path.Combine(GameIO.GetSaveGameDir(),"RebirthLogisticsLoadedBodyNotices");
    private static string PathFor(string owner,Guid operation,Guid incarnation,Guid nonce,int life,string digest=null)
    {
        if(digest!=null&&(digest.Length!=64||!digest.All(c=>c>='0'&&c<='9'||c>='a'&&c<='f')))throw new InvalidDataException();
        if(owner?.Length!=64||!owner.All(c=>c>='0'&&c<='9'||c>='a'&&c<='f')||operation==Guid.Empty||incarnation==Guid.Empty||nonce==Guid.Empty||life<1)throw new InvalidDataException();
        string directory=Path.Combine(Root,owner);
        foreach(string path in new[]{Root,directory})if(Directory.Exists(path)&&(new DirectoryInfo(path).Attributes&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException();
        return Path.Combine(directory,operation.ToString("N")+"_"+incarnation.ToString("N")+"_"+nonce.ToString("N")+"_"+life+(digest==null?string.Empty:"_"+digest)+".xml");
    }
    private static string PathFor(RebirthLogisticsLoadedBodyNotice.Notice n)=>PathFor(n.Owner,n.Operation,n.Incarnation,n.Nonce,n.Life,n.Digest);
    private static XElement ReadXml(string path)
    {
        var file=new FileInfo(path);if(!file.Exists||file.Length<1||file.Length>MaximumBytes||(file.Attributes&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException();
        using(var reader=XmlReader.Create(path,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=MaximumBytes}))return XElement.Load(reader);
    }
    private static RebirthLogisticsLoadedBodyNotice.Notice Parse(RebirthLogisticsTransferJournal.Record original,XElement root,out int state)
    {
        state=(int)root.Attribute("state");if(root.Name!="notice"||root.Attributes().Count()!=3||(int)root.Attribute("version")!=1||(state!=1&&state!=2)||root.Elements().Count()!=1)throw new InvalidDataException();
        var b=root.Element("body");if(b==null||b.Attributes().Count()!=19||b.Elements().Count()!=2)throw new InvalidDataException();
        var n=new RebirthLogisticsLoadedBodyNotice.Notice();
        try
        {
            n.Operation=Guid.ParseExact((string)b.Attribute("operation"),"N");n.Incarnation=Guid.ParseExact((string)b.Attribute("incarnation"),"N");n.Nonce=Guid.ParseExact((string)b.Attribute("nonce"),"N");
            n.World=(string)b.Attribute("world");n.Owner=(string)b.Attribute("owner");n.Creation=(string)b.Attribute("creation");n.Command=(string)b.Attribute("command");n.Binding=(string)b.Attribute("binding");n.Proof=(string)b.Attribute("proof");
            int mode=(int)b.Attribute("mode");if(mode<0||mode>1)throw new InvalidDataException();n.Suppressed=mode==1;n.PolicyWitness=(string)b.Attribute("policy");
            n.Deaths=(int)b.Attribute("deaths");n.Life=(int)b.Attribute("life");n.Entity=(int)b.Attribute("entity");n.Class=(int)b.Attribute("class");n.X=(int)b.Attribute("x");n.Z=(int)b.Attribute("z");n.Old=(int)b.Attribute("old");n.Next=(int)b.Attribute("next");
            string before=b.Element("before")?.Value,after=b.Element("after")?.Value;
            if(!RebirthLogisticsLoadedBodyNotice.ValidHeader(n)||n.Operation!=original.Operation||n.Owner!=original.Owner||n.World!=original.World||n.Creation!=original.Creation||n.Deaths!=original.Deaths||
                n.Binding!=RebirthLogisticsTransferJournal.ImmutablePlanDigest(original)||n.Command!=RebirthLogisticsNativeBodyIntent.Command.From(original).Binding||before?.Length>349528||after?.Length>349528)throw new InvalidDataException();
            n.Before=Convert.FromBase64String(before);n.After=Convert.FromBase64String(after);n.Digest=(string)root.Attribute("digest");
            if(n.Before.Length<1||n.After.Length<1||n.Before.Length>RebirthLogisticsLoadedBodyNotice.MaximumFrame||n.After.Length>RebirthLogisticsLoadedBodyNotice.MaximumFrame||n.Digest!=Digest(n)||!XNode.DeepEquals(root,Document(n,state))||
                !RebirthLogisticsLoadedBodyNotice.Project(n.Before,n.Old,n.Next).SequenceEqual(n.After))throw new InvalidDataException();
            n.Expires=DateTime.UtcNow.AddSeconds(45);n.Acked=state==2;return n;
        }
        catch{n.Dispose();throw;}
    }
    internal static bool TryLoad(RebirthLogisticsTransferJournal.Record original,Guid incarnation,Guid nonce,int life,out RebirthLogisticsLoadedBodyNotice.Notice notice,out int state,string exactDigest=null)
    {
        notice=null;state=0;RebirthLogisticsSnapshotReservation account=null;IDisposable allocation=null;
        try
        {
            string path=PathFor(original.Owner,original.Operation,incarnation,nonce,life,exactDigest),archive=Path.Combine(Root,"archive",original.Owner,Path.GetFileName(path));
            if(File.Exists(path)&&File.Exists(archive))return false;
            if(!File.Exists(path)&&File.Exists(archive))
            {
                if(original.Phase!=4||File.Exists(path+".tmp")||File.Exists(path+".bak"))return false;
                foreach(string directory in new[]{Path.Combine(Root,"archive"),Path.GetDirectoryName(archive)})if(Directory.Exists(directory)&&(new DirectoryInfo(directory).Attributes&FileAttributes.ReparsePoint)!=0)return false;
                path=archive;
            }
            if(File.Exists(path+".bak"))return false;
            long length=File.Exists(path)?new FileInfo(path).Length:0,temp=File.Exists(path+".tmp")?new FileInfo(path+".tmp").Length:0;
            if(length==0&&temp==0||length>MaximumBytes||temp>MaximumBytes)return false;
            var world=GameManager.Instance?.World;account=RebirthLogisticsSnapshotReservation.TryOpenRetainedRead(original,()=>ReferenceEquals(GameManager.Instance?.World,world));
            if(account==null||!account.TryReserveAllocation(checked(1048576+20*(length+temp)),out allocation))return false;
            XElement root=length>0?ReadXml(path):null;
            if(temp>0)
            {
                var pending=ReadXml(path+".tmp");using(var decoded=Parse(original,pending,out int next))
                {
                    bool valid=root==null?next==1:next==2&&(int)root.Attribute("state")==1&&XNode.DeepEquals(root,new XElement("notice",new XAttribute("version",1),new XAttribute("state",1),new XAttribute("digest",decoded.Digest),Body(decoded)));
                    if(!valid)return false;
                }
                if(root==null)File.Move(path+".tmp",path);else File.Replace(path+".tmp",path,null);root=ReadXml(path);
            }
            notice=Parse(original,root,out state);
            if(notice.Incarnation!=incarnation||notice.Nonce!=nonce||notice.Life!=life||exactDigest!=null&&notice.Digest!=exactDigest){notice.Dispose();notice=null;return false;}
            notice.Account=account;notice.Workspace=allocation;account=null;allocation=null;return true;
        }
        catch{return false;}
        finally{allocation?.Dispose();account?.Dispose();}
    }
    private static bool Capacity(int incoming)
    {
        if(!Directory.Exists(Root))return true;int count=0;long bytes=incoming;
        foreach(string directory in Directory.EnumerateDirectories(Root))
        {
            if((new DirectoryInfo(directory).Attributes&FileAttributes.ReparsePoint)!=0)return false;
            if(Path.GetFileName(directory)=="archive")continue;
            if(Directory.EnumerateDirectories(directory).Any())return false;
            foreach(string path in Directory.EnumerateFiles(directory))
            {var file=new FileInfo(path);if((file.Attributes&FileAttributes.ReparsePoint)!=0||file.Length<1||file.Length>MaximumBytes||++count>=4096||checked(bytes+=file.Length)>64L*1024*1024)return false;}
        }
        return !Directory.EnumerateFiles(Root).Any();
    }
    internal static bool Prepare(RebirthLogisticsTransferJournal.Record original,RebirthLogisticsLoadedBodyNotice.Notice n,Func<bool> current)
    {
        if(current?.Invoke()!=true||n.Digest!=Digest(n))return false;
        string path=PathFor(n),archive=Path.Combine(Root,"archive",n.Owner,Path.GetFileName(path));if(File.Exists(path)||File.Exists(path+".tmp")||File.Exists(archive))
        {if(!TryLoad(original,n.Incarnation,n.Nonce,n.Life,out var saved,out _,n.Digest))return false;using(saved)return saved.Digest==n.Digest;}
        if(File.Exists(path+".bak"))return false;byte[] bytes=Encoding.UTF8.GetBytes(Document(n,1).ToString(SaveOptions.DisableFormatting));
        if(bytes.Length>MaximumBytes||!Capacity(bytes.Length)||current()!=true)return false;Directory.CreateDirectory(Path.GetDirectoryName(path));
        using(var stream=new FileStream(path+".tmp",FileMode.CreateNew,FileAccess.Write,FileShare.None)){stream.Write(bytes,0,bytes.Length);stream.Flush(true);}File.Move(path+".tmp",path);
        return current()&&TryLoadAndDispose(original,n,1);
    }
    private static bool TryLoadAndDispose(RebirthLogisticsTransferJournal.Record original,RebirthLogisticsLoadedBodyNotice.Notice expected,int state)
    {if(!TryLoad(original,expected.Incarnation,expected.Nonce,expected.Life,out var actual,out int current,expected.Digest))return false;using(actual)return actual.Digest==expected.Digest&&current==state;}
    internal static bool Acknowledge(RebirthLogisticsTransferJournal.Record original,RebirthLogisticsLoadedBodyNotice.Notice expected,Func<bool> current)
    {
        if(current?.Invoke()!=true||!TryLoad(original,expected.Incarnation,expected.Nonce,expected.Life,out var n,out int state,expected.Digest))return false;
        using(n)
        {
            if(n.Digest!=expected.Digest||current()!=true)return false;if(state==2)return true;
            string path=PathFor(n);if(File.Exists(path+".tmp")||File.Exists(path+".bak"))return false;byte[] bytes=Encoding.UTF8.GetBytes(Document(n,2).ToString(SaveOptions.DisableFormatting));
            using(var stream=new FileStream(path+".tmp",FileMode.CreateNew,FileAccess.Write,FileShare.None)){stream.Write(bytes,0,bytes.Length);stream.Flush(true);}if(current()!=true)return false;File.Replace(path+".tmp",path,null);
            return TryLoadAndDispose(original,n,2);
        }
    }
    internal static bool FindMatchingWitness(RebirthLogisticsTransferJournal.Record original,Guid incarnation,Guid nonce,int life,int oldOwner,int nextOwner,bool suppressed,string policy,EntityBackpack body,out RebirthLogisticsLoadedBodyNotice.Notice notice)
    {
        notice=null;foreach(var n in Entries(original))
        {
            if(n.Incarnation==incarnation&&n.Nonce==nonce&&n.Life==life&&n.Old==oldOwner&&n.Next==nextOwner&&n.Suppressed==suppressed&&n.PolicyWitness==policy&&RebirthLogisticsLoadedBodyNotice.ExactLive(body,n.After)){notice=n;return true;}
            n.Dispose();
        }
        return false;
    }
    internal static bool HasAcknowledgedBody(RebirthLogisticsTransferJournal.Record original,Guid incarnation,EntityBackpack body)
    {
        if(body==null)return false;
        foreach(var entry in Entries(original))
        {
            if(entry.Incarnation!=incarnation){entry.Dispose();continue;}
            using(entry)
                if(entry.Acked&&entry.Next==body.RefPlayerId&&RebirthLogisticsLoadedBodyNotice.ServerCurrent(entry,out _)&&RebirthLogisticsLoadedBodyNotice.ExactLive(body,entry.After))return true;
        }
        return false;
    }
    private static IEnumerable<RebirthLogisticsLoadedBodyNotice.Notice> Entries(RebirthLogisticsTransferJournal.Record original)
    {
        string directory=Path.Combine(Root,original.Owner),archive=Path.Combine(Root,"archive",original.Owner);int count=0;var seen=new HashSet<string>(StringComparer.Ordinal);
        foreach(string parent in original.Phase==4?new[]{directory,archive}:new[]{directory})
        {
        if(!Directory.Exists(parent))continue;if((new DirectoryInfo(parent).Attributes&FileAttributes.ReparsePoint)!=0)yield break;
        foreach(string path in Directory.EnumerateFiles(parent,original.Operation.ToString("N")+"_*.xml"))
        {
            if(!seen.Add(Path.GetFileName(path)))continue;if(++count>4096)yield break;var parts=Path.GetFileNameWithoutExtension(path).Split('_');
            if((parts.Length!=4&&parts.Length!=5)||!Guid.TryParseExact(parts[1],"N",out var inc)||!Guid.TryParseExact(parts[2],"N",out var nonce)||!int.TryParse(parts[3],out int life))yield break;
            if(!TryLoad(original,inc,nonce,life,out var notice,out _,parts.Length==5?parts[4]:null))yield break;yield return notice;
        }
        }
    }
    internal static void ArchiveAcknowledgedIncarnation(RebirthLogisticsTransferJournal.Record original,Guid incarnation,EntityBackpack body)
    {
        if(original.Phase!=4||!HasAcknowledgedBody(original,incarnation,body))return;
        // Old state1 stays state1: current positive native/body ACK supersedes
        // its metadata transport need, never invents a historical ACK/payment.
        foreach(var n in Entries(original))using(n)
        {
            if(n.Incarnation!=incarnation)continue;string path=PathFor(n);if(!File.Exists(path))continue;
            string directory=Path.Combine(Root,"archive",n.Owner),archive=Path.Combine(directory,Path.GetFileName(path));
            foreach(string folder in new[]{Path.Combine(Root,"archive"),directory})if(Directory.Exists(folder)&&(new DirectoryInfo(folder).Attributes&FileAttributes.ReparsePoint)!=0)return;
            Directory.CreateDirectory(directory);if(File.Exists(archive))return;File.Move(path,archive);
        }
    }
    internal static void ArchiveRemovedIncarnation(RebirthLogisticsTransferJournal.Record original,Guid incarnation)
    {
        if(original.Phase!=4||!RebirthLogisticsParcelReferenceJournal.WasPositivelyRemoved(original,incarnation))return;
        // Actual native removal makes only this metadata transport obsolete.
        // State1 is preserved as1; no ACK, payment, refund or spawn is inferred.
        foreach(var n in Entries(original))using(n)
        {
            if(n.Incarnation!=incarnation)continue;string path=PathFor(n);if(!File.Exists(path))continue;
            string directory=Path.Combine(Root,"archive",n.Owner),archive=Path.Combine(directory,Path.GetFileName(path));
            foreach(string folder in new[]{Path.Combine(Root,"archive"),directory})if(Directory.Exists(folder)&&(new DirectoryInfo(folder).Attributes&FileAttributes.ReparsePoint)!=0)return;
            Directory.CreateDirectory(directory);if(File.Exists(archive))return;File.Move(path,archive);
        }
    }
    // Parse only an exact supported candidate name before any journal/body load.
    // Sidecars still pass through TryLoad's strict predecessor/next validation.
    internal static bool TryColdIdentity(string name,out Guid operation,out Guid incarnation,out Guid nonce,out int life,out string digest)
    {
        operation=incarnation=nonce=Guid.Empty;life=0;digest=null;
        if(name==null||name.Length>220)return false;
        string stem=name.EndsWith(".xml.tmp",StringComparison.Ordinal)?name.Substring(0,name.Length-8):name.EndsWith(".xml",StringComparison.Ordinal)?name.Substring(0,name.Length-4):null;
        if(stem==null)return false;var parts=stem.Split('_');
        if((parts.Length!=4&&parts.Length!=5)||!Guid.TryParseExact(parts[0],"N",out operation)||operation==Guid.Empty||!Guid.TryParseExact(parts[1],"N",out incarnation)||incarnation==Guid.Empty||!Guid.TryParseExact(parts[2],"N",out nonce)||nonce==Guid.Empty||!int.TryParse(parts[3],System.Globalization.NumberStyles.None,System.Globalization.CultureInfo.InvariantCulture,out life)||life<1||parts[3]!=life.ToString(System.Globalization.CultureInfo.InvariantCulture))return false;
        if(parts[0]!=operation.ToString("N")||parts[1]!=incarnation.ToString("N")||parts[2]!=nonce.ToString("N"))return false;
        if(parts.Length==5){digest=parts[4];if(digest.Length!=64||!digest.All(c=>c>='0'&&c<='9'||c>='a'&&c<='f'))return false;}
        return true;
    }
    internal static IEnumerable<RebirthLogisticsTransferJournal.Record> Originals()
    {
        if(!Directory.Exists(Root))yield break;
        if((new DirectoryInfo(Root).Attributes&FileAttributes.ReparsePoint)!=0)yield break;
        int count=0,directories=0;var seen=new HashSet<string>(StringComparer.Ordinal);
        foreach(string directory in Directory.EnumerateDirectories(Root))
        {
            if(++directories>4096||(new DirectoryInfo(directory).Attributes&FileAttributes.ReparsePoint)!=0)yield break;
            string owner=Path.GetFileName(directory);if(owner=="archive")continue;
            if(owner.Length!=64||!owner.All(c=>c>='0'&&c<='9'||c>='a'&&c<='f'))yield break;
            foreach(string path in Directory.EnumerateFiles(directory,"*.xml*"))
            {
                if(++count>4096||!TryColdIdentity(Path.GetFileName(path),out var op,out _,out _,out _,out _))yield break;
                if(seen.Add(owner+":"+op.ToString("N"))&&RebirthLogisticsTransferJournal.TryRead(owner,op,out var original))yield return original;
            }
        }
    }
    internal static IEnumerable<RebirthLogisticsLoadedBodyNotice.Notice> Pending(RebirthLogisticsTransferJournal.Record original)
    {
        string directory=Path.Combine(Root,original.Owner);if(!Directory.Exists(directory))yield break;
        if((new DirectoryInfo(Root).Attributes&FileAttributes.ReparsePoint)!=0||(new DirectoryInfo(directory).Attributes&FileAttributes.ReparsePoint)!=0)yield break;
        int count=0;var seen=new HashSet<string>(StringComparer.Ordinal);
        foreach(string path in Directory.EnumerateFiles(directory,original.Operation.ToString("N")+"_*.xml*"))
        {
            if(++count>4096||!TryColdIdentity(Path.GetFileName(path),out var op,out var inc,out var nonce,out int life,out string digest)||op!=original.Operation)yield break;
            string identity=inc.ToString("N")+":"+nonce.ToString("N")+":"+life+":"+digest;
            if(!seen.Add(identity))continue; // TryLoad checks BOTH primary and tmp.
            if(!TryLoad(original,inc,nonce,life,out var notice,out int state,digest))yield break;
            if(state==1)yield return notice;else notice.Dispose();
        }
    }
}
