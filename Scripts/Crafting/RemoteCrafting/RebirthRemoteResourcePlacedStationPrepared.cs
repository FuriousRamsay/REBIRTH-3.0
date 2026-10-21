using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

// Small immutable reference publication. Its Applied phase means only that
// the full original artifact was durably published and reparsed. It is NEVER
// an owner permission, source effect, native queue publication or Ready flag.
internal static class RebirthRemoteResourcePlacedStationPrepared
{
    private const int MaximumReferenceBytes=4096;
    // Actual rcj encoder: magic +2 GUIDs +7bit UTF8 creation length
    // +native-mode creation<=512 chars at worst4 bytes +revision/phase
    // +two Int32 image lengths +two reference bodies +SHA256 trailer.
    private const int MaximumPhaseFileBytes=4+32+5+4*512+8+1+8+2*MaximumReferenceBytes+32;
    private static string Root=>Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH","NativePlacedPrepared");
    private static void Ancestors(string path)
    {for(var dir=new DirectoryInfo(Path.GetFullPath(path));dir!=null;dir=dir.Parent)if(dir.Exists&&(dir.Attributes&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException();}
    private static bool Files(Guid operation)
    {
        try
        {
            string root=Root,prefix=operation.ToString("N");Ancestors(root);int entries=0;
            foreach(string folder in new[]{root,Path.Combine(root,"completed"),Path.Combine(root,"completed",prefix),Path.Combine(root,"failedCandidates")})
            {
                Ancestors(folder);if(!Directory.Exists(folder))continue;
                foreach(string entry in Directory.EnumerateFileSystemEntries(folder,prefix+"*"))
                {
                    if(++entries>32)return false;
                    if(folder==Path.Combine(root,"completed")&&entry==Path.Combine(folder,prefix)&&Directory.Exists(entry))continue;
                    if(folder==Path.Combine(root,"failedCandidates")||Directory.Exists(entry)||(File.GetAttributes(entry)&FileAttributes.ReparsePoint)!=0||
                        !new[]{prefix+".0.rcj",prefix+".1.rcj",prefix+".2.rcj"}.Contains(Path.GetFileName(entry))||new FileInfo(entry).Length>MaximumPhaseFileBytes)return false;
                }
            }
            return true;
        }
        catch{return false;}
    }
    private static byte[] Reference(RebirthRemoteResourceDurableJournal.Record source,RebirthRemoteResourcePlacedStationArtifactReader.Image artifact,string digest)
    {
        var body=new XElement("placedPrepared",new XAttribute("version",1),new XAttribute("source",source.Operation.ToString("N")),
            new XAttribute("job",artifact.Original.Operation.ToString("N")),new XAttribute("binding",source.Binding),new XAttribute("owner",source.Owner),
            new XAttribute("creation",source.Creation),new XAttribute("world",source.World),new XAttribute("original",artifact.Original.Digest),
            new XAttribute("birth",artifact.Birth),new XAttribute("artifact",digest),new XAttribute("sourceRevision",artifact.SourceRevision));
        byte[] bytes=Encoding.UTF8.GetBytes(body.ToString(SaveOptions.DisableFormatting));
        if(bytes.Length<1||bytes.Length>MaximumReferenceBytes)throw new InvalidDataException();return bytes;
    }
    private static bool Current(RebirthRemoteResourceDurableJournal.Record source)=>ThreadManager.IsMainThread()&&source!=null&&
        source.Creation!=null&&source.Creation.Length<=512&&RebirthOriginalActionScope.Validate(source.Owner,source.Creation)&&
        GameManager.Instance?.World?.IsRemote()==false&&RemoteResourceWorldIdentity.TryGet(GameManager.Instance.World,out string world)&&world==source.World;
    internal static bool TryRead(RebirthRemoteResourceDurableJournal.Record source,out RebirthRemoteResourcePlacedStationArtifactReader.Image artifact,out string digest,
        RebirthRemoteResourcePlacedStationAllocation.Admission allocation=null,RebirthRemoteResourcePlacedStationPreparation.ColdRead cold=null)
    {
        artifact=null;digest=null;
        try
        {
            if(!Current(source)||!Files(source.Operation))return false;
            var record=new RebirthRemoteResourceClientOriginalJournal(Root).Read(source.Operation);
            if(record?.State!=RebirthCamperTransferJournal.Phase.Applied||record.Revision!=0||record.OwnerCreation!=source.Creation||
                record.Before.Length>MaximumReferenceBytes||!record.Before.SequenceEqual(record.After)||
                !RebirthRemoteResourcePlacedStationArtifactStore.TryRead(source,out var bytes,out var original,allocation,cold))return false;
            string hash=RebirthRemoteResourcePlacedStationArtifactStore.Digest(bytes);
            if(record.Vehicle!=original.Original.Operation||!record.Before.SequenceEqual(Reference(source,original,hash)))return false;
            artifact=original;digest=hash;return true;
        }
        catch{artifact=null;digest=null;return false;}
    }
    internal static bool TryPublish(RebirthRemoteResourceDurableJournal.Record source,string expectedDigest,
        RebirthRemoteResourcePlacedStationAllocation.Admission allocation=null)
    {
        try
        {
            if(!Current(source)||source.Phase!=1||source.HasOwnerAction||!source.SourcesUnattempted||
                !RebirthRemoteResourceDurableJournal.NeverPreparedOwnerAction(source)||!Files(source.Operation)||
                !RebirthRemoteResourcePlacedStationArtifactStore.TryRead(source,out var bytes,out var artifact,allocation))return false;
            string hash=RebirthRemoteResourcePlacedStationArtifactStore.Digest(bytes);if(hash!=expectedDigest)return false;
            byte[] reference=Reference(source,artifact,hash);Guid job=artifact.Original.Operation;
            artifact=null;bytes=null; // no full artifact graph retained during final independent readback
            var journal=new RebirthRemoteResourceClientOriginalJournal(Root);
            var record=journal.Prepare(source.Operation,job,source.Creation,0,reference,reference);
            if(record.State==RebirthCamperTransferJournal.Phase.Prepared)record=journal.Advance(source.Operation,record.State,RebirthCamperTransferJournal.Phase.Uncertain);
            if(record.State==RebirthCamperTransferJournal.Phase.Uncertain)record=journal.Advance(source.Operation,record.State,RebirthCamperTransferJournal.Phase.Applied);
            return record.State==RebirthCamperTransferJournal.Phase.Applied&&TryRead(source,out _,out string readback,allocation)&&readback==hash;
        }
        catch{return false;}
    }
}
