using System;
using System.IO;
using System.Linq;

// Domain-only terminal archival. The complete original history remains readable
// at the same operation ID. A partial archive never replaces a valid primary;
// disagreement or a corrupt complete archive is unknown, never fallback.
internal sealed class RebirthRemoteResourceClientOriginalJournal
{
    private readonly string root;
    internal RebirthRemoteResourceClientOriginalJournal(string directory)
    {root=Path.GetFullPath(directory);Directory.CreateDirectory(root);Check(root);}
    private static void Check(string directory)
    {if(Directory.Exists(directory)&&(File.GetAttributes(directory)&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("Original journal path changed.");}
    private string ArchiveDirectory(Guid operation)
    {string completed=Path.Combine(root,"completed");Check(completed);string path=Path.Combine(completed,operation.ToString("N"));Check(path);return path;}
    private static bool Same(RebirthCamperTransferJournal.Record a,RebirthCamperTransferJournal.Record b)=>a!=null&&b!=null&&a.Operation==b.Operation&&a.Vehicle==b.Vehicle&&a.OwnerCreation==b.OwnerCreation&&a.Revision==b.Revision&&a.State==b.State&&a.Before.SequenceEqual(b.Before)&&a.After.SequenceEqual(b.After);
    private static void Subset(string from,string to,Guid operation)
    {
        foreach(string file in Directory.EnumerateFiles(from,operation.ToString("N")+".*.rcj"))
        {
            string destination=Path.Combine(to,Path.GetFileName(file));
            if(!File.Exists(destination)||!File.ReadAllBytes(file).SequenceEqual(File.ReadAllBytes(destination)))throw new InvalidDataException("Conflicting original journal copies.");
        }
    }
    internal RebirthCamperTransferJournal.Record Read(Guid operation)
        =>ObserveExisting(root,operation);
    internal static RebirthCamperTransferJournal.Record ObserveExisting(string directory,Guid operation)
    {
        if(operation==Guid.Empty)throw new InvalidDataException();string root=Path.GetFullPath(directory);Check(root);
        if(!Directory.Exists(root))return null;
        string completed=Path.Combine(root,"completed");Check(completed);
        string archive=Path.Combine(completed,operation.ToString("N"));Check(archive);
        if(!Directory.Exists(archive))return RebirthCamperTransferJournal.ReadExisting(root,operation);
        bool terminal=File.Exists(Path.Combine(archive,operation.ToString("N")+".2.rcj"))||File.Exists(Path.Combine(archive,operation.ToString("N")+".3.rcj"));
        if(terminal)
        {
            var saved=RebirthCamperTransferJournal.ReadExisting(archive,operation);
            if(saved==null||saved.State!=RebirthCamperTransferJournal.Phase.Applied&&saved.State!=RebirthCamperTransferJournal.Phase.Rejected)throw new InvalidDataException();
            Subset(root,archive,operation);return saved;
        }
        var primary=RebirthCamperTransferJournal.ReadExisting(root,operation);
        if(primary==null||primary.State!=RebirthCamperTransferJournal.Phase.Applied&&primary.State!=RebirthCamperTransferJournal.Phase.Rejected)throw new InvalidDataException();
        Subset(archive,root,operation);return primary;
    }
    internal RebirthCamperTransferJournal.Record Prepare(Guid operation,Guid vehicle,string creation,long revision,byte[] before,byte[] after)
    {
        var retained=Read(operation);
        if(retained==null)return new RebirthCamperTransferJournal(root).Prepare(operation,vehicle,creation,revision,before,after);
        if(retained.Vehicle!=vehicle||retained.OwnerCreation!=creation||retained.Revision!=revision||!retained.Before.SequenceEqual(before)||!retained.After.SequenceEqual(after))throw new InvalidDataException("Original identity reused.");
        return retained;
    }
    internal RebirthCamperTransferJournal.Record Advance(Guid operation,RebirthCamperTransferJournal.Phase expected,RebirthCamperTransferJournal.Phase next)
    {
        var current=Read(operation);if(current==null||current.State!=expected)throw new InvalidDataException("Original phase changed.");
        var advanced=new RebirthCamperTransferJournal(root).Advance(operation,expected,next);
        if(next==RebirthCamperTransferJournal.Phase.Applied||next==RebirthCamperTransferJournal.Phase.Rejected)Archive(operation);
        return advanced;
    }
    internal void Archive(Guid operation)
    {
        var original=Read(operation);if(original==null||original.State!=RebirthCamperTransferJournal.Phase.Applied&&original.State!=RebirthCamperTransferJournal.Phase.Rejected)return;
        string archive=ArchiveDirectory(operation);Directory.CreateDirectory(archive);Check(archive);
        var files=Directory.GetFiles(root,operation.ToString("N")+".*.rcj");
        foreach(string file in files)
        {
            byte[] bytes=File.ReadAllBytes(file);string destination=Path.Combine(archive,Path.GetFileName(file));
            if(File.Exists(destination)){if(!bytes.SequenceEqual(File.ReadAllBytes(destination)))throw new InvalidDataException();continue;}
            // Preserve failed temporary evidence; only a completely flushed
            // immutable phase file can become a terminal archive member.
            string temporary=destination+"."+Guid.NewGuid().ToString("N")+".tmp";
            using(var stream=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None)){stream.Write(bytes,0,bytes.Length);stream.Flush(true);}
            File.Move(temporary,destination);
        }
        if(!Same(original,new RebirthCamperTransferJournal(archive).Read(operation)))throw new InvalidDataException("Terminal archive incomplete.");
        Subset(root,archive,operation);
        // These are verified duplicate phase files. All authority/history stays
        // in the complete archive before any primary duplicate is removed.
        foreach(string file in files)
        {
            string destination=Path.Combine(archive,Path.GetFileName(file));
            if(!File.ReadAllBytes(file).SequenceEqual(File.ReadAllBytes(destination)))throw new InvalidDataException();
            File.Delete(file);
        }
    }
}
