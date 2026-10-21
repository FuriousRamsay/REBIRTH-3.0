using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

internal static partial class RebirthRemoteResourceFetchedStationOriginal
{
    private static RebirthRemoteResourceClientOriginalJournal ClientUsed()=>new RebirthRemoteResourceClientOriginalJournal(Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH","FetchedStationClientUsed"));
    private static RebirthRemoteResourceClientOriginalJournal HostUsed()=>new RebirthRemoteResourceClientOriginalJournal(Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH","FetchedStationHostUsed"));
    private static bool KnownTerminalFiles(string domain,Guid operation,bool allowAbsent=false)
    {
        string directory=Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH",domain);bool found=false;
        foreach(string path in new[]{directory,Path.Combine(directory,"completed",operation.ToString("N"))})
        {
            if(!Directory.Exists(path))continue;
            if((File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0)return false;
            var files=Directory.EnumerateFileSystemEntries(path,path==directory?operation.ToString("N")+"*":"*").Take(4).ToArray();if(files.Length>3)return false;
            foreach(var file in files)
            {
                if((File.GetAttributes(file)&(FileAttributes.Directory|FileAttributes.ReparsePoint))!=0||
                    !new[]{".0.rcj",".1.rcj",".2.rcj"}.Any(suffix=>Path.GetFileName(file)==operation.ToString("N")+suffix))return false;
                found=true;
            }
        }
        string completed=Path.Combine(directory,"completed"),failed=Path.Combine(directory,"failedCandidates");
        if(Directory.Exists(completed)&&(File.GetAttributes(completed)&FileAttributes.ReparsePoint)!=0||
            Directory.Exists(failed)&&((File.GetAttributes(failed)&FileAttributes.ReparsePoint)!=0||Directory.EnumerateFileSystemEntries(failed,operation.ToString("N")+"*").Any()))return false;
        return found||allowAbsent;
    }
    private static bool SaveHostUsed(EntityPlayerLocal player,RebirthRemoteResourceClientOriginal.Intent original,Guid source,string binding,string proof,Func<bool> authenticated)
    {
        try
        {
            if(authenticated?.Invoke()!=true||!RebirthRemoteResourceClientOriginal.Current(player,original)||!IsCandidate(original)||!KnownTerminalFiles("FetchedStationHostUsed",original.Operation,true)||proof?.Length!=64||
                proof.Any(c=>!(c>='0'&&c<='9'||c>='a'&&c<='f'))||!RebirthRemoteResourceClientOriginal.TryReadPaidActionSource(original,out _,out var paid,out var bound,out _)||paid!=source||bound!=binding)return false;
            var body=new XElement("fetchedStationHostUsed",new XAttribute("version",1),new XAttribute("original",original.Digest),new XAttribute("source",source.ToString("N")),new XAttribute("binding",binding),new XAttribute("proof",proof));
            byte[] bytes=Encoding.UTF8.GetBytes(body.ToString(SaveOptions.DisableFormatting));var journal=HostUsed();var record=journal.Prepare(original.Operation,original.Operation,original.Creation,0,bytes,bytes);
            if(!record.Before.SequenceEqual(bytes)||!record.After.SequenceEqual(bytes)||authenticated()!=true)return false;
            if(record.State==RebirthCamperTransferJournal.Phase.Prepared)record=journal.Advance(original.Operation,record.State,RebirthCamperTransferJournal.Phase.Uncertain);
            if(record.State==RebirthCamperTransferJournal.Phase.Uncertain)record=journal.Advance(original.Operation,record.State,RebirthCamperTransferJournal.Phase.Applied);
            return record.State==RebirthCamperTransferJournal.Phase.Applied&&authenticated();
        }
        catch{return false;}
    }
    internal static bool HasRetirementMetadataBinding(EntityPlayerLocal player,Guid job,RebirthCamperStationAddress address,string creation,byte[] payload)
    {
        try
        {
            if(!RebirthRemoteResourceClientOriginal.TryRead(job,out var original)||!RebirthRemoteResourceClientOriginal.Current(player,original)||!IsCandidate(original)||original.Creation!=creation||
                !RebirthRemoteResourceClientOriginal.TryReadPaidActionSource(original,out string world,out var source,out string binding,out _)||
                !KnownTerminalFiles("FetchedStationHostUsed",job)||!KnownTerminalFiles("FetchedStationNativePayloads",job))return false;
            var host=HostUsed().Read(job);if(host?.State!=RebirthCamperTransferJournal.Phase.Applied||host.Vehicle!=job||host.OwnerCreation!=creation||host.Revision!=0||host.Before.Length>4096||!host.Before.SequenceEqual(host.After))return false;
            var used=RebirthCookingRemoteAdmission.ReadProtocolXml(new UTF8Encoding(false,true).GetString(host.Before),4096);
            string proof=(string)used.Attribute("proof");
            if(used.Name!="fetchedStationHostUsed"||used.Attributes().Count()!=5||used.Nodes().Any()||(int?)used.Attribute("version")!=1||
                (string)used.Attribute("original")!=original.Digest||(string)used.Attribute("source")!=source.ToString("N")||(string)used.Attribute("binding")!=binding||
                proof?.Length!=64||proof.Any(c=>!(c>='0'&&c<='9'||c>='a'&&c<='f')))return false;
            var retained=ClientPayloads().Read(job);if(retained?.State!=RebirthCamperTransferJournal.Phase.Applied||retained.Vehicle!=address.Vehicle||retained.OwnerCreation!=creation||retained.Revision!=0||retained.Before.Length>12*1024*1024||!retained.Before.SequenceEqual(retained.After))return false;
            var body=RebirthCookingRemoteAdmission.ReadProtocolXml(new UTF8Encoding(false,true).GetString(retained.Before),12*1024*1024);
            byte[] canonical=CanonicalClientPayload(player,job,address,creation,payload);
            return body.Name=="fetchedStationNativePayload"&&body.Attributes().Count()==8&&body.Elements().Count()==2&&
                body.Nodes().All(node=>node is XElement)&&body.Elements().All(node=>!node.HasAttributes&&node.Nodes().All(child=>child is XText))&&body.Element("definition")!=null&&body.Element("definition").Value.Length<=262144&&
                (int?)body.Attribute("version")==1&&(string)body.Attribute("original")==original.Digest&&(string)body.Attribute("job")==job.ToString("N")&&
                (string)body.Attribute("address")==address.ToString()&&(string)body.Attribute("source")==source.ToString("N")&&(string)body.Attribute("sourceWorld")==world&&
                (string)body.Attribute("binding")==binding&&(string)body.Attribute("payload")==RebirthRemoteResourceDurableJournal.Digest(Convert.ToBase64String(canonical))&&
                body.Element("native")?.Value==Convert.ToBase64String(canonical)&&RebirthNativeItemCodec.TryDecode(body.Element("definition")?.Value,out _);
        }
        catch{return false;}
    }
    internal static bool HasClientUsedTerminal(EntityPlayerLocal player,Guid job,RebirthCamperStationAddress address,string creation,byte[] actualPayload,
        out Guid source,out string binding,out string proof)
    {
        source=Guid.Empty;binding=proof=null;
        try
        {
            if(!RebirthRemoteResourceClientOriginal.TryRead(job,out var original)||!RebirthRemoteResourceClientOriginal.Current(player,original)||!IsCandidate(original)||original.Creation!=creation||!KnownTerminalFiles("FetchedStationClientUsed",job))return false;
            var record=ClientUsed().Read(job);if(record?.State!=RebirthCamperTransferJournal.Phase.Applied||record.Vehicle!=job||record.OwnerCreation!=creation||record.Revision!=0||record.Before.Length>8192||!record.Before.SequenceEqual(record.After))return false;
            var body=RebirthCookingRemoteAdmission.ReadProtocolXml(new UTF8Encoding(false,true).GetString(record.Before),8192);
            if(body.Name!="fetchedStationUsed"||body.Attributes().Count()!=9||body.Nodes().Any()||(int?)body.Attribute("version")!=1||
                (string)body.Attribute("job")!=job.ToString("N")||(string)body.Attribute("original")!=original.Digest||(string)body.Attribute("address")!=address.ToString()||
                (string)body.Attribute("payload")!=RebirthRemoteResourceDurableJournal.Digest(Convert.ToBase64String(CanonicalClientPayload(player,job,address,creation,actualPayload)))||
                !RebirthRemoteResourceClientOriginal.TryReadPaidActionSource(original,out string world,out var paidSource,out string paidBinding,out _)||
                (string)body.Attribute("sourceWorld")!=world||(string)body.Attribute("source")!=paidSource.ToString("N")||(string)body.Attribute("binding")!=paidBinding)return false;
            string value=(string)body.Attribute("proof");if(value?.Length!=64||value.Any(c=>!(c>='0'&&c<='9'||c>='a'&&c<='f')))return false;
            source=paidSource;binding=paidBinding;proof=value;return true;
        }
        catch{return false;}
    }
    private static bool CompleteClientUsed(EntityPlayerLocal player,RebirthRemoteResourceClientOriginal.Intent original,Guid source,string sourceWorld,string binding,string proof,Func<bool> authenticated)
    {
        try
        {
            if(!SaveHostUsed(player,original,source,binding,proof,authenticated)||
                !ValidateInput(original,false,out var address,out _,out _,out _,out _,out _,out _)||!RebirthCamperStationNativeActorPayment.TryGetSavedFetchedRetirement(player,original.Operation,out var payload))return false;
            string payloadHash=RebirthRemoteResourceDurableJournal.Digest(Convert.ToBase64String(CanonicalClientPayload(player,original.Operation,address,original.Creation,payload)));
            var body=new XElement("fetchedStationUsed",new XAttribute("version",1),new XAttribute("job",original.Operation.ToString("N")),new XAttribute("original",original.Digest),
                new XAttribute("address",address.ToString()),new XAttribute("source",source.ToString("N")),new XAttribute("sourceWorld",sourceWorld),new XAttribute("binding",binding),new XAttribute("proof",proof),new XAttribute("payload",payloadHash));
            byte[] bytes=Encoding.UTF8.GetBytes(body.ToString(SaveOptions.DisableFormatting));var journal=ClientUsed();var record=journal.Prepare(original.Operation,original.Operation,original.Creation,0,bytes,bytes);
            if(!record.Before.SequenceEqual(bytes)||!record.After.SequenceEqual(bytes)||authenticated()!=true)return false;
            if(record.State==RebirthCamperTransferJournal.Phase.Prepared)record=journal.Advance(original.Operation,record.State,RebirthCamperTransferJournal.Phase.Uncertain);
            if(record.State==RebirthCamperTransferJournal.Phase.Uncertain)record=journal.Advance(original.Operation,record.State,RebirthCamperTransferJournal.Phase.Applied);
            if(record.State!=RebirthCamperTransferJournal.Phase.Applied||!HasClientUsedTerminal(player,original.Operation,address,original.Creation,payload,out var actual,out var actualBinding,out var actualProof)||
                actual!=source||actualBinding!=binding||actualProof!=proof)return false;
            return RebirthRemoteResourceClientOriginal.CompleteFetchedStationTerminal(player,original,source,binding,proof,payload,address)&&
                RebirthRemoteResourceOwnerAction.ReleaseFetchedStationTerminal(player,original,source,binding,proof,payload,address);
        }
        catch{return false;}
    }
}
