using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

internal static partial class RebirthNpcStructureRepairOriginal
{
    // Immutable original files avoid cumulative rich images in each aggregate.
    // No age/count pruning may erase an unsettled operation or its native claim.
    private static RebirthRemoteResourceClientOriginalJournal RepairArchive()=>new RebirthRemoteResourceClientOriginalJournal(
        Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH","NpcStructureRepairArchiveV1"));
    private static bool ValidArchived(RebirthNpcStableId id,Guid operation,XElement row)
    {
        try
        {
            return row?.Name==Element&&(int?)row.Attribute("version")==1&&(string)row.Attribute("npc")==id.ToString()&&
                (string)row.Attribute("operation")==operation.ToString("N")&&(string)row.Attribute("phase")=="native-completed"&&
                RebirthNpcSingleRepairWork.IsDefinition((string)row.Attribute("definition"))&&
                (string)row.Attribute("world")==GameManager.Instance?.World?.worldState?.Guid&&
                ((string)row.Attribute("nativeBinding"))?.Length==64&&
                (string)row.Attribute("nativeDebitProof")==DebitProof(row)&&PublishedTool(row)&&
                ((string)row.Attribute("nativeBlockProof"))?.Length==64&&((long?)row.Attribute("nativeCompletedTicks")??0)>0&&
                RebirthOriginalActionScope.Validate((string)row.Attribute("nativeOwner"),(string)row.Attribute("nativeCreation"));
        }
        catch{return false;}
    }
    private static bool ReadArchived(RebirthNpcStableId id,Guid operation,out XElement row)
    {
        row=null;
        try
        {
            if(!ThreadManager.IsMainThread()||GameManager.Instance?.World?.IsRemote()!=false)return false;
            var entry=RepairArchive().Read(operation);
            if(entry==null||entry.State!=RebirthCamperTransferJournal.Phase.Applied||entry.Vehicle!=operation||entry.Revision!=0||
                entry.Before==null||entry.Before.Length>6*1024*1024||!entry.Before.SequenceEqual(entry.After))return false;
            string raw=new UTF8Encoding(false,true).GetString(entry.Before);
            var archived=RebirthCookingRemoteAdmission.ReadProtocolXml(raw,6*1024*1024);
            if(!ValidArchived(id,operation,archived)||entry.OwnerCreation!=(string)archived.Attribute("nativeCreation")||
                archived.ToString(SaveOptions.DisableFormatting)!=raw)return false;
            row=archived;return true;
        }
        catch{return false;}
    }
    private static bool TryArchiveCompleted(RebirthNpcStableId id,Guid nextOperation)
    {
        try
        {
            if(!RebirthNpcAggregatePersistenceStore.TryReadSavedPerson(id,out var person))return false;
            var old=Read(person);
            if(old==null||!Guid.TryParseExact((string)old.Attribute("operation"),"N",out var previous)||previous==nextOperation||
                !ValidArchived(id,previous,old)||
                !ulong.TryParse((string)old.Attribute("assignment"),out ulong oldAssignment)||
                !RebirthNpcWorkAssignmentService.TryGet(oldAssignment,out var assignment)||assignment.NpcId!=id||
                !RebirthNpcWorkOutcomeService.HasCommittedLiterature(oldAssignment,id))return false;
            byte[] bytes=Encoding.UTF8.GetBytes(old.ToString(SaveOptions.DisableFormatting));if(bytes.Length>6*1024*1024)return false;
            var journal=RepairArchive();var entry=journal.Prepare(previous,previous,(string)old.Attribute("nativeCreation"),0,bytes,bytes);
            if(entry.Vehicle!=previous||entry.OwnerCreation!=(string)old.Attribute("nativeCreation")||entry.Revision!=0||
                !entry.Before.SequenceEqual(bytes)||!entry.After.SequenceEqual(bytes))return false;
            if(entry.State==RebirthCamperTransferJournal.Phase.Prepared)entry=journal.Advance(previous,entry.State,RebirthCamperTransferJournal.Phase.Uncertain);
            if(entry.State==RebirthCamperTransferJournal.Phase.Uncertain)entry=journal.Advance(previous,entry.State,RebirthCamperTransferJournal.Phase.Applied);
            if(!ReadArchived(id,previous,out var proof)||!XNode.DeepEquals(old,proof))return false;
            // Saved archive is the prerequisite, never a deletion follow-up.
            // A crash before aggregate removal leaves both identical copies.
            string raw=old.ToString(SaveOptions.DisableFormatting);
            if(!RebirthNpcAggregatePersistenceStore.Mutate(id,record=>
            {
                if(!XNode.DeepEquals(Read(record),old))throw new InvalidDataException("Completed repair original changed before archival.");
                record.UnknownOptionalElements=record.UnknownOptionalElements.Where(x=>x!=raw).ToArray();
            }))return false;
            RebirthNpcAggregatePersistenceStore.Save();
            return RebirthNpcAggregatePersistenceStore.TryReadSavedPerson(id,out var saved)&&Read(saved)==null&&
                ReadArchived(id,previous,out proof)&&XNode.DeepEquals(proof,old);
        }
        catch{return false;}
    }
    private static bool ArchivedNativeTerminal(World world,Guid operation,string digest,Vector3i position,string birth)
    {
        try
        {
            if(!ThreadManager.IsMainThread()||world?.IsRemote()!=false||!ReferenceEquals(GameManager.Instance?.World,world))return false;
            var candidate=RepairArchive().Read(operation);if(candidate?.Before==null||candidate.Before.Length>6*1024*1024)return false;
            var image=RebirthCookingRemoteAdmission.ReadProtocolXml(new UTF8Encoding(false,true).GetString(candidate.Before),6*1024*1024);
            if(!RebirthNpcStableId.TryParse((string)image.Attribute("npc"),out var id)||!ReadArchived(id,operation,out var archived))return false;
            return XNode.DeepEquals(image,archived)&&(string)archived.Attribute("nativeBinding")==digest&&
                NativePosition(archived).Equals(position)&&(string)archived.Attribute("nativeBirth")==birth;
        }
        catch{return false;}
    }
}
