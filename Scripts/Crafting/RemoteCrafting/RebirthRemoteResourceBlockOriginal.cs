using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

// Owner original records only. Neither a prepared body nor a captured native
// command proves host commit; producer/host transport must compose those stages.
internal static class RebirthRemoteResourceBlockOriginal
{
    private static RebirthRemoteResourceClientOriginalJournal Actions()=>new RebirthRemoteResourceClientOriginalJournal(Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH","RemoteBlockOwnerOriginals"));
    private static RebirthRemoteResourceClientOriginalJournal Commands()=>new RebirthRemoteResourceClientOriginalJournal(Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH","RemoteBlockOwnerCommands"));
    private static RebirthRemoteResourceClientOriginalJournal Terminals()=>new RebirthRemoteResourceClientOriginalJournal(Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH","RemoteBlockOwnerTerminals"));
    internal static bool RejectedOrUnknown(RebirthRemoteResourceClientOriginal.Intent original)
    {
        try
        {
            foreach(string name in new[]{"RemoteBlockOwnerRejections","RemoteBlockPreparedRejections"})
            {
                string directory=Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH",name);if(!Directory.Exists(directory))continue;
                if((File.GetAttributes(directory)&FileAttributes.ReparsePoint)!=0)return true;
                string prefix=original.Operation.ToString("N");
                if(Directory.EnumerateFileSystemEntries(directory,prefix+"*").Any()||Directory.Exists(Path.Combine(directory,"completed",prefix))||
                    Directory.Exists(Path.Combine(directory,"failedCandidates"))&&Directory.EnumerateFileSystemEntries(Path.Combine(directory,"failedCandidates"),prefix+"*").Any())return true;
            }
            return false;
        }
        catch{return true;}
    }
    internal static bool SaveRejected(EntityPlayerLocal player,RebirthRemoteResourceClientOriginal.Intent original,Func<bool> authenticatedMonotoneRejecting)
    {
        try
        {
            if(!RebirthRemoteResourceClientOriginal.Current(player,original)||authenticatedMonotoneRejecting?.Invoke()!=true)return false;
            byte[] bytes=Encoding.UTF8.GetBytes(original.Copy().ToString(SaveOptions.DisableFormatting));
            var journal=new RebirthRemoteResourceClientOriginalJournal(Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH","RemoteBlockOwnerRejections"));
            var record=journal.Prepare(original.Operation,original.Operation,original.Creation,0,bytes,bytes);
            if(!record.Before.SequenceEqual(bytes)||!record.After.SequenceEqual(bytes))return false;
            if(record.State==RebirthCamperTransferJournal.Phase.Prepared)record=journal.Advance(original.Operation,record.State,RebirthCamperTransferJournal.Phase.Uncertain);
            if(record.State==RebirthCamperTransferJournal.Phase.Uncertain)record=journal.Advance(original.Operation,record.State,RebirthCamperTransferJournal.Phase.Applied);
            return record.State==RebirthCamperTransferJournal.Phase.Applied;
        }
        catch{return false;}
    }
    internal static XElement PreparedRejectionBody(RebirthRemoteResourceClientOriginal.Intent original,XElement action)=>new XElement("blockPreparedRejection",new XAttribute("version",1),
        new XAttribute("original",original.Digest),new XElement("action",action.ToString(SaveOptions.DisableFormatting)));
    internal static bool SavePreparedRejection(EntityPlayerLocal player,RebirthRemoteResourceClientOriginal.Intent original)
    {
        try
        {
            if(!RebirthRemoteResourceClientOriginal.Current(player,original)||!NeverEntered(original)||!TryRead(original,out var action,out _))return false;
            var body=PreparedRejectionBody(original,action);byte[] bytes=Encoding.UTF8.GetBytes(body.ToString(SaveOptions.DisableFormatting));if(bytes.Length>24*1024*1024)return false;
            var journal=new RebirthRemoteResourceClientOriginalJournal(Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH","RemoteBlockPreparedRejections"));
            var record=journal.Prepare(original.Operation,original.Operation,original.Creation,0,bytes,bytes);if(!record.Before.SequenceEqual(bytes)||!record.After.SequenceEqual(bytes))return false;
            if(record.State==RebirthCamperTransferJournal.Phase.Prepared)record=journal.Advance(original.Operation,record.State,RebirthCamperTransferJournal.Phase.Uncertain);
            if(record.State==RebirthCamperTransferJournal.Phase.Uncertain)record=journal.Advance(original.Operation,record.State,RebirthCamperTransferJournal.Phase.Applied);
            return record.State==RebirthCamperTransferJournal.Phase.Applied&&SaveRejected(player,original,()=>true);
        }
        catch{return false;}
    }
    internal static bool TryReadPreparedRejection(RebirthRemoteResourceClientOriginal.Intent original,out XElement body)
    {
        body=null;
        try
        {
            if(!NeverEntered(original)||!TryRead(original,out var action,out _))return false;
            var journal=new RebirthRemoteResourceClientOriginalJournal(Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH","RemoteBlockPreparedRejections"));var record=journal.Read(original.Operation);
            if(record?.State!=RebirthCamperTransferJournal.Phase.Applied||record.Vehicle!=original.Operation||record.OwnerCreation!=original.Creation||record.Revision!=0||record.Before.Length>24*1024*1024||!record.Before.SequenceEqual(record.After))return false;
            var expected=PreparedRejectionBody(original,action);if(!record.Before.SequenceEqual(Encoding.UTF8.GetBytes(expected.ToString(SaveOptions.DisableFormatting))))return false;
            body=expected;return true;
        }
        catch{return false;}
    }
    internal static bool TerminalScope(EntityPlayerLocal player,RebirthRemoteResourceClientOriginal.Intent original)=>ThreadManager.IsMainThread()&&player?.world?.IsRemote()==true&&
        ReferenceEquals(GameManager.Instance?.World,player.world)&&ReferenceEquals(player.world.GetPrimaryPlayer(),player)&&ReferenceEquals(player.world.GetEntity(player.entityId),player)&&
        original!=null&&player.world.worldState.Guid==original.World&&RebirthCamperCharacterAccess.CurrentCreation(player)==original.Creation&&
        RebirthStablePlayerIdentity.TryResolveServerEntity(player,out var identity)&&identity.CanonicalId==original.Owner;
    private static bool Hex(string value)=>value?.Length==64&&value.All(c=>c>='0'&&c<='9'||c>='a'&&c<='f');
    internal static bool Canonical(RebirthRemoteResourceClientOriginal.Intent original,string world,Guid source,string binding,string targetBirth,ItemStack[] paid,out XElement body)
    {
        body=null;
        try
        {
            if(!Hex(world)||!Hex(binding)||source==Guid.Empty||!Guid.TryParseExact(targetBirth,"N",out var birth)||birth==Guid.Empty||birth.ToString("N")!=targetBirth||
                !RebirthRemoteResourceBlockPlan.Expected(original,paid,out var before,out var after,out var local))return false;
            var value=new XElement("blockOwnerOriginal",new XAttribute("originalDigest",original.Digest),new XAttribute("sourceWorld",world),new XAttribute("source",source.ToString("N")),
                new XAttribute("binding",binding),new XAttribute("targetBirth",targetBirth),new XElement("beforeActor",RebirthRemoteResourceClientOriginal.ActorImage(before)),
                new XElement("afterActor",RebirthRemoteResourceClientOriginal.ActorImage(after)),new XElement("localRemoved",Convert.ToBase64String(RebirthCamperInventorySnapshot.Capture(local))),
                new XElement("remotePaid",Convert.ToBase64String(RebirthCamperInventorySnapshot.Capture(paid))));
            if(Encoding.UTF8.GetByteCount(value.ToString(SaveOptions.DisableFormatting))>24*1024*1024)return false;
            body=value;return true;
        }
        catch{return false;}
    }
    internal static bool Prepare(EntityPlayerLocal player,RebirthRemoteResourceClientOriginal.Intent original,string targetBirth,Func<bool> authenticatedSource,out XElement action)
    {
        action=null;
        try
        {
            if(!RebirthRemoteResourceClientOriginal.Current(player,original)||RebirthRemoteResourcePreAdmissionRefusal.ClientRequested(original)||authenticatedSource?.Invoke()!=true||
                !RebirthRemoteResourceClientOriginal.TryReadPaidActionSource(original,out var world,out var source,out var binding,out var paid)||
                !Canonical(original,world,source,binding,targetBirth,paid,out var body)||
                !RebirthRemoteResourceClientOriginal.ReadActor(body.Element("beforeActor").Value).Matches(RebirthCamperEnergyActorImage.Capture(player)))return false;
            byte[] bytes=Encoding.UTF8.GetBytes(body.ToString(SaveOptions.DisableFormatting));var record=Actions().Prepare(original.Operation,original.Operation,original.Creation,0,bytes,bytes);
            if(!record.Before.SequenceEqual(bytes)||!record.After.SequenceEqual(bytes)||authenticatedSource()!=true)return false;
            action=body;return true;
        }
        catch{return false;}
    }
    internal static bool TryRead(RebirthRemoteResourceClientOriginal.Intent original,out XElement action,out RebirthCamperTransferJournal.Phase phase)
    {
        action=null;phase=default;
        try
        {
            if(!RebirthRemoteResourceBlockPlan.Validate(original)||!RebirthRemoteResourceClientOriginal.TryReadPaidActionSource(original,out var world,out var source,out var binding,out var paid))return false;
            var record=Actions().Read(original.Operation);if(record==null||record.Vehicle!=original.Operation||record.OwnerCreation!=original.Creation||record.Before.Length>24*1024*1024||!record.Before.SequenceEqual(record.After))return false;
            string text=new UTF8Encoding(false,true).GetString(record.Before);var parsed=RebirthCookingRemoteAdmission.ReadProtocolXml(text,24*1024*1024);
            if(!Canonical(original,world,source,binding,(string)parsed.Attribute("targetBirth"),paid,out var expected)||expected.ToString(SaveOptions.DisableFormatting)!=text)return false;
            action=expected;phase=record.State;return true;
        }
        catch{return false;}
    }
    internal static bool BeginExecution(EntityPlayerLocal player,RebirthRemoteResourceClientOriginal.Intent original,Func<bool> actualSavedHostPermission)
    {
        try
        {
            if(!RebirthRemoteResourceClientOriginal.Current(player,original)||RejectedOrUnknown(original)||RebirthRemoteResourcePreAdmissionRefusal.ClientRequested(original)||actualSavedHostPermission?.Invoke()!=true||!TryRead(original,out var body,out var state)||
                state!=RebirthCamperTransferJournal.Phase.Prepared||!RebirthRemoteResourceClientOriginal.ReadActor(body.Element("beforeActor").Value).Matches(RebirthCamperEnergyActorImage.Capture(player)))return false;
            return Actions().Advance(original.Operation,state,RebirthCamperTransferJournal.Phase.Uncertain).State==RebirthCamperTransferJournal.Phase.Uncertain;
        }
        catch{return false;}
    }
    internal static bool NeverEntered(RebirthRemoteResourceClientOriginal.Intent original)
    {
        try
        {
            if(!TryRead(original,out _,out var state)||state!=RebirthCamperTransferJournal.Phase.Prepared)return false;
            foreach(string name in new[]{"RemoteBlockOwnerOriginals","RemoteBlockOwnerCommands","RemoteBlockOwnerTerminals"})
            {
                string path=Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH",name);if(!Directory.Exists(path))continue;
                if((File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0)return false;
                string prefix=original.Operation.ToString("N");
                foreach(string entry in Directory.EnumerateFileSystemEntries(path,prefix+"*"))
                    if(name!="RemoteBlockOwnerOriginals"||Path.GetFileName(entry)!=prefix+".0.rcj")return false;
                foreach(string child in new[]{"completed","failedCandidates"})
                {
                    string nested=Path.Combine(path,child);if(!Directory.Exists(nested))continue;
                    if((File.GetAttributes(nested)&FileAttributes.ReparsePoint)!=0||Directory.EnumerateFileSystemEntries(nested,prefix+"*").Any())return false;
                }
            }
            return true;
        }
        catch{return false;}
    }
    internal static bool CanRejectUnentered(RebirthRemoteResourceClientOriginal.Intent original)
    {
        try
        {
            if(Actions().Read(original.Operation)!=null)return NeverEntered(original);
            foreach(string name in new[]{"RemoteBlockOwnerOriginals","RemoteBlockOwnerCommands","RemoteBlockOwnerTerminals"})
            {
                string directory=Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH",name);if(!Directory.Exists(directory))continue;
                if((File.GetAttributes(directory)&FileAttributes.ReparsePoint)!=0)return false;
                string prefix=original.Operation.ToString("N");if(Directory.EnumerateFileSystemEntries(directory,prefix+"*").Any())return false;
                foreach(string child in new[]{"completed","failedCandidates"})
                {
                    string nested=Path.Combine(directory,child);if(!Directory.Exists(nested))continue;
                    if((File.GetAttributes(nested)&FileAttributes.ReparsePoint)!=0||Directory.EnumerateFileSystemEntries(nested,prefix+"*").Any())return false;
                }
            }
            return true;
        }
        catch{return false;}
    }
    internal static bool CanUploadInitial(RebirthRemoteResourceClientOriginal.Intent original)
    {
        try
        {
            if(Actions().Read(original.Operation)!=null||!CanRejectUnentered(original)||RejectedOrUnknown(original))return false;
            string directory=Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH","RemoteResourceClientSources");if(!Directory.Exists(directory))return true;
            if((File.GetAttributes(directory)&FileAttributes.ReparsePoint)!=0)return false;
            string prefix=original.Operation.ToString("N");if(Directory.EnumerateFileSystemEntries(directory,prefix+"*").Any())return false;
            foreach(string child in new[]{"completed","failedCandidates"})
            {
                string nested=Path.Combine(directory,child);if(!Directory.Exists(nested))continue;
                if((File.GetAttributes(nested)&FileAttributes.ReparsePoint)!=0||Directory.EnumerateFileSystemEntries(nested,prefix+"*").Any())return false;
            }
            return true;
        }
        catch{return false;}
    }
    internal static bool SaveCapturedCommand(EntityPlayerLocal player,RebirthRemoteResourceClientOriginal.Intent original,RebirthRemoteResourceBlockNativeCapture.Scope actualScope)
    {
        try
        {
            if(!RebirthRemoteResourceClientOriginal.Current(player,original)||actualScope==null||!ReferenceEquals(actualScope.Player,player)||actualScope.Original.Digest!=original.Digest||
                !actualScope.TryGet(out var changes,out bool texture)||!TryRead(original,out var action,out var state)||state!=RebirthCamperTransferJournal.Phase.Uncertain||
                !RebirthRemoteResourceClientOriginal.ReadActor(action.Element("afterActor").Value).Matches(RebirthCamperEnergyActorImage.Capture(player)))return false;
            RebirthRemoteResourceBlockOptimisticView.FinalizeCapture(actualScope); // Missing/invalid view evidence never grants a forced correction.
            var command=new XElement("capturedBlockOriginal",new XAttribute("version",1),new XAttribute("originalDigest",original.Digest),
                new XAttribute("actionDigest",RebirthRemoteResourceDurableJournal.Digest(action.ToString(SaveOptions.DisableFormatting))),new XAttribute("clearTexture",texture),new XElement("changes",changes));
            byte[] bytes=Encoding.UTF8.GetBytes(command.ToString(SaveOptions.DisableFormatting));var journal=Commands();var record=journal.Prepare(original.Operation,original.Operation,original.Creation,0,bytes,bytes);
            if(!record.Before.SequenceEqual(bytes)||!record.After.SequenceEqual(bytes))return false;
            if(record.State==RebirthCamperTransferJournal.Phase.Prepared)record=journal.Advance(original.Operation,record.State,RebirthCamperTransferJournal.Phase.Uncertain);
            if(record.State==RebirthCamperTransferJournal.Phase.Uncertain)record=journal.Advance(original.Operation,record.State,RebirthCamperTransferJournal.Phase.Applied);
            return record.State==RebirthCamperTransferJournal.Phase.Applied;
        }
        catch{return false;}
    }
    internal static bool TryReadCapturedCommand(RebirthRemoteResourceClientOriginal.Intent original,out string changes,out bool clearTexture)
    {
        changes=null;clearTexture=false;
        try
        {
            if(!TryRead(original,out var action,out var phase)||(phase!=RebirthCamperTransferJournal.Phase.Uncertain&&phase!=RebirthCamperTransferJournal.Phase.Applied))return false;
            var record=Commands().Read(original.Operation);
            if(record?.State!=RebirthCamperTransferJournal.Phase.Applied||record.Vehicle!=original.Operation||record.OwnerCreation!=original.Creation||
                record.Revision!=0||record.Before.Length>128*1024||!record.Before.SequenceEqual(record.After))return false;
            string text=new UTF8Encoding(false,true).GetString(record.Before);var body=RebirthCookingRemoteAdmission.ReadProtocolXml(text,128*1024);
            if(body.Name!="capturedBlockOriginal"||body.Attributes().Count()!=4||body.Elements().Count()!=1||body.Nodes().Any(node=>node is not XElement)||
                (int?)body.Attribute("version")!=1||(string)body.Attribute("originalDigest")!=original.Digest||
                (string)body.Attribute("actionDigest")!=RebirthRemoteResourceDurableJournal.Digest(action.ToString(SaveOptions.DisableFormatting))||
                body.Element("changes")==null||body.Element("changes").HasAttributes||body.Element("changes").Nodes().Any(node=>node is not XText)||
                !RebirthOriginalNativeBlockChange.TryDecode(body.Element("changes").Value,out var parsed))return false;
            bool texture=(bool)body.Attribute("clearTexture");
            var expected=new XElement("capturedBlockOriginal",new XAttribute("version",1),new XAttribute("originalDigest",original.Digest),
                new XAttribute("actionDigest",RebirthRemoteResourceDurableJournal.Digest(action.ToString(SaveOptions.DisableFormatting))),new XAttribute("clearTexture",texture),new XElement("changes",body.Element("changes").Value));
            var target=original.Copy().Element("action");var position=new Vector3i((int)target.Attribute("x"),(int)target.Attribute("y"),(int)target.Attribute("z"));
            if(expected.ToString(SaveOptions.DisableFormatting)!=text||parsed.Count!=1||parsed[0].blockValueRef.Type!=BlockValueRefType.Block||parsed[0].blockValueRef.BlockPosition!=position)return false;
            changes=body.Element("changes").Value;clearTexture=texture;return true;
        }
        catch{changes=null;clearTexture=false;return false;}
    }
    internal static bool SaveUsedTerminal(EntityPlayerLocal player,RebirthRemoteResourceClientOriginal.Intent original,Guid source,string binding,
        string commandDigest,string savedEventProof,string usedProof,Func<bool> authenticatedSavedTerminal)
    {
        try
        {
            if(!TerminalScope(player,original)||authenticatedSavedTerminal?.Invoke()!=true||!Hex(commandDigest)||!Hex(savedEventProof)||!Hex(usedProof)||
                !TryRead(original,out var action,out var state)||state==RebirthCamperTransferJournal.Phase.Prepared||
                (string)action.Attribute("source")!=source.ToString("N")||(string)action.Attribute("binding")!=binding||!TryReadCapturedCommand(original,out var changes,out var clear))return false;
            string ownerProof=RebirthRemoteResourceDurableJournal.Digest("native-block-owner2|"+original.Digest+"|"+source.ToString("N")+"|"+binding+"|"+action.Element("afterActor").Value);
            if(RebirthRemoteResourceBlockSettlement.UsedProof(original,source,binding,commandDigest,ownerProof,savedEventProof)!=usedProof)return false;
            var body=new XElement("blockUsedTerminal",new XAttribute("version",1),new XAttribute("original",original.Digest),new XAttribute("source",source.ToString("N")),
                new XAttribute("binding",binding),new XAttribute("command",commandDigest),new XAttribute("savedEvent",savedEventProof),new XAttribute("usedProof",usedProof),
                new XAttribute("clearTexture",clear),new XElement("changes",changes),new XElement("actionDigest",RebirthRemoteResourceDurableJournal.Digest(action.ToString(SaveOptions.DisableFormatting))));
            byte[] bytes=Encoding.UTF8.GetBytes(body.ToString(SaveOptions.DisableFormatting));if(bytes.Length>128*1024)return false;
            var journal=Terminals();var record=journal.Prepare(original.Operation,original.Operation,original.Creation,0,bytes,bytes);
            if(!record.Before.SequenceEqual(bytes)||!record.After.SequenceEqual(bytes)||authenticatedSavedTerminal()!=true)return false;
            if(record.State==RebirthCamperTransferJournal.Phase.Prepared)record=journal.Advance(original.Operation,record.State,RebirthCamperTransferJournal.Phase.Uncertain);
            if(record.State==RebirthCamperTransferJournal.Phase.Uncertain)record=journal.Advance(original.Operation,record.State,RebirthCamperTransferJournal.Phase.Applied);
            if(record.State!=RebirthCamperTransferJournal.Phase.Applied||!TerminalScope(player,original)||authenticatedSavedTerminal()!=true)return false;
            if(state==RebirthCamperTransferJournal.Phase.Uncertain)state=Actions().Advance(original.Operation,state,RebirthCamperTransferJournal.Phase.Applied).State;
            return state==RebirthCamperTransferJournal.Phase.Applied;
        }
        catch{return false;}
    }
    internal static bool HasUsedTerminal(RebirthRemoteResourceClientOriginal.Intent original,Guid source,string binding,string usedProof)
    {
        try
        {
            if(!TryRead(original,out var action,out var state)||state!=RebirthCamperTransferJournal.Phase.Applied||!TryReadCapturedCommand(original,out var changes,out var clear))return false;
            var record=Terminals().Read(original.Operation);if(record?.State!=RebirthCamperTransferJournal.Phase.Applied||record.Vehicle!=original.Operation||record.OwnerCreation!=original.Creation||record.Revision!=0||
                record.Before.Length>128*1024||!record.Before.SequenceEqual(record.After))return false;
            var body=RebirthCookingRemoteAdmission.ReadProtocolXml(new UTF8Encoding(false,true).GetString(record.Before),128*1024);
            string command=(string)body.Attribute("command"),saved=(string)body.Attribute("savedEvent");if(!Hex(command)||!Hex(saved)||!Hex(usedProof))return false;
            string ownerProof=RebirthRemoteResourceDurableJournal.Digest("native-block-owner2|"+original.Digest+"|"+source.ToString("N")+"|"+binding+"|"+action.Element("afterActor").Value);
            if((string)action.Attribute("source")!=source.ToString("N")||(string)action.Attribute("binding")!=binding||
                RebirthRemoteResourceBlockSettlement.UsedProof(original,source,binding,command,ownerProof,saved)!=usedProof)return false;
            var expected=new XElement("blockUsedTerminal",new XAttribute("version",1),new XAttribute("original",original.Digest),new XAttribute("source",source.ToString("N")),
                new XAttribute("binding",binding),new XAttribute("command",command),new XAttribute("savedEvent",saved),new XAttribute("usedProof",usedProof),new XAttribute("clearTexture",clear),
                new XElement("changes",changes),new XElement("actionDigest",RebirthRemoteResourceDurableJournal.Digest(action.ToString(SaveOptions.DisableFormatting))));
            return expected.ToString(SaveOptions.DisableFormatting)==new UTF8Encoding(false,true).GetString(record.Before);
        }
        catch{return false;}
    }
}
