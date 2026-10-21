using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

// Canonical host acceptance of the owner's original native command. This is
// deliberately not an actual host block-effect or native publication receipt.
internal static class RebirthRemoteResourceBlockCommand
{
    internal sealed class Accepted
    {
        internal readonly string Changes,OwnerProof,Digest,Birth,Context;internal readonly bool ClearTexture,ObservedClearTexture;internal readonly int NativeEntity;
        private Accepted(string changes,string ownerProof,string digest,string birth,bool observedClear,bool hostClear,int nativeEntity,string context)
        {Changes=changes;OwnerProof=ownerProof;Digest=digest;Birth=birth;ObservedClearTexture=observedClear;ClearTexture=hostClear;NativeEntity=nativeEntity;Context=context;}
        internal static Accepted FromValidated(string changes,string ownerProof,string digest,string birth,bool observedClear,bool hostClear,int nativeEntity,string context)=>new Accepted(changes,ownerProof,digest,birth,observedClear,hostClear,nativeEntity,context);
    }
    private static RebirthRemoteResourceClientOriginalJournal Journal(string name)=>new RebirthRemoteResourceClientOriginalJournal(Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH",name));
    private static bool Publish(string name,RebirthRemoteResourceClientOriginal.Intent original,byte[] bytes)
    {
        if(bytes.Length>128*1024)return false;var journal=Journal(name);
        var record=journal.Prepare(original.Operation,original.Operation,original.Creation,0,bytes,bytes);
        if(!record.Before.SequenceEqual(bytes)||!record.After.SequenceEqual(bytes))return false;
        if(record.State==RebirthCamperTransferJournal.Phase.Prepared)record=journal.Advance(original.Operation,record.State,RebirthCamperTransferJournal.Phase.Uncertain);
        if(record.State==RebirthCamperTransferJournal.Phase.Uncertain)record=journal.Advance(original.Operation,record.State,RebirthCamperTransferJournal.Phase.Applied);
        return record.State==RebirthCamperTransferJournal.Phase.Applied;
    }
    private static XElement Body(RebirthRemoteResourceClientOriginal.Intent original,RebirthRemoteResourceDurableJournal.Record root,string birth,string ownerProof,string changes,bool texture,bool hostClear,int nativeEntity,string context)
        =>new XElement("blockAcceptedCommand",new XAttribute("version",3),new XAttribute("original",original.Digest),new XAttribute("source",root.Operation.ToString("N")),
            new XAttribute("binding",root.Binding),new XAttribute("owner",root.Owner),new XAttribute("world",root.World),new XAttribute("birth",birth),new XAttribute("ownerProof",ownerProof),new XAttribute("clearTexture",texture),new XAttribute("hostClearTexture",hostClear),new XAttribute("nativeEntity",nativeEntity),new XElement("changes",changes),new XElement("transitionContext",context));
    internal static bool Admit(EntityPlayer actor,RebirthRemoteResourceClientOriginal.Intent original,string exactChanges,bool clearTexture)
    {
        try
        {
            if(!RebirthRemoteResourceBlockSettlement.Read(actor,original,out _,out var root)||root.Phase!=2||((int?)root.Copy().Attribute("ownerRejecting")??0)!=0||!RebirthRemoteResourceBlockSettlement.MatchesRootAction(original,root)||
                !RebirthRemoteResourceBlockAdmission.TryReadTarget(original,out var birth,out var density,out var painted)||!RebirthOriginalNativeBlockChange.TryDecode(exactChanges,out var changes)||changes.Count!=1||
                !RebirthRemoteResourceBlockSettlement.TryObserveOwnerPaid(actor,original,out string proof))return false;
            var intent=original.Copy().Element("action");var position=new Vector3i((int)intent.Attribute("x"),(int)intent.Attribute("y"),(int)intent.Attribute("z"));
            if(changes[0].blockValueRef.Type!=BlockValueRefType.Block||changes[0].blockValueRef.BlockPosition!=position)return false;
            if(!RebirthRemoteResourceBlockAdmission.TryReadTransitionContext(original,out var context)||
                !RebirthRemoteResourceBlockTransition.AllowsFrozen(original,exactChanges,density,painted,clearTexture,context,out _,out var hostClear))return false;
            string acceptedText=Body(original,root,birth,proof,exactChanges,clearTexture,hostClear,actor.entityId,context).ToString(SaveOptions.DisableFormatting);
            return RebirthRemoteResourceBlockProtocol.TrackPreparedAcceptance(original,root,RebirthRemoteResourceDurableJournal.Digest(acceptedText))&&
                Publish("RemoteBlockAcceptedCommands",original,Encoding.UTF8.GetBytes(acceptedText));
        }
        catch{return false;}
    }
    internal static bool TryRead(RebirthRemoteResourceClientOriginal.Intent original,RebirthRemoteResourceDurableJournal.Record root,out Accepted accepted)
    {
        accepted=null;
        try
        {
            if(!RebirthRemoteResourceBlockSettlement.MatchesRootAction(original,root)||!RebirthRemoteResourceBlockSettlement.CanonicalAction(original,root,out var action)||
                !RebirthRemoteResourceBlockAdmission.TryReadTarget(original,out var birth,out var density,out var painted))return false;
            var record=Journal("RemoteBlockAcceptedCommands").Read(original.Operation);
            if(record?.State!=RebirthCamperTransferJournal.Phase.Applied||record.Vehicle!=original.Operation||record.OwnerCreation!=original.Creation||record.Revision!=0||
                record.Before.Length>128*1024||!record.Before.SequenceEqual(record.After))return false;
            string text=new UTF8Encoding(false,true).GetString(record.Before);var body=RebirthCookingRemoteAdmission.ReadProtocolXml(text,128*1024);
            if(body.Name!="blockAcceptedCommand"||body.Attributes().Count()!=11||body.Elements().Count()!=2||body.Element("changes")==null||body.Element("transitionContext")==null||
                !RebirthOriginalNativeBlockChange.TryDecode(body.Element("changes").Value,out var changes)||changes.Count!=1)return false;
            string proof=RebirthRemoteResourceDurableJournal.Digest("native-block-owner2|"+original.Digest+"|"+root.Operation.ToString("N")+"|"+root.Binding+"|"+action.Element("afterActor").Value);
            bool texture=(bool)body.Attribute("clearTexture"),hostClear=(bool)body.Attribute("hostClearTexture");int nativeEntity=(int)body.Attribute("nativeEntity");if(nativeEntity<0)return false;
            if(!RebirthRemoteResourceBlockAdmission.TryReadTransitionContext(original,out var context)||context!=body.Element("transitionContext").Value||
                !RebirthRemoteResourceBlockTransition.AllowsFrozen(original,body.Element("changes").Value,density,painted,texture,context,out _,out var canonicalClear)||canonicalClear!=hostClear||
                text!=Body(original,root,birth,proof,body.Element("changes").Value,texture,hostClear,nativeEntity,context).ToString(SaveOptions.DisableFormatting))return false;
            var intent=original.Copy().Element("action");var position=new Vector3i((int)intent.Attribute("x"),(int)intent.Attribute("y"),(int)intent.Attribute("z"));
            if(changes[0].blockValueRef.Type!=BlockValueRefType.Block||changes[0].blockValueRef.BlockPosition!=position)return false;
            accepted=Accepted.FromValidated(body.Element("changes").Value,proof,RebirthRemoteResourceDurableJournal.Digest(text),birth,texture,hostClear,nativeEntity,context);return true;
        }
        catch{return false;}
    }
    internal static bool TryReadAcceptedWorld(World world,RebirthRemoteResourceClientOriginal.Intent original,out RebirthRemoteResourceDurableJournal.Record root,out Accepted accepted)
    {
        root=null;accepted=null;
        try
        {
            // Parent-approved ended-life/disconnected continuation: only an
            // ALREADY durably accepted original-life native2 command. No fresh
            // owner/source permission, inventory setter or range re-admission.
            if(!ThreadManager.IsMainThread()||world?.IsRemote()!=false||!ReferenceEquals(GameManager.Instance?.World,world)||original==null||world.worldState.Guid!=original.World||
                !RebirthRemoteResourceBlockAdmission.TryReadOriginal(original.Operation,out var retained)||retained.Digest!=original.Digest||
                !RemoteResourceWorldIdentity.TryGet(world,out string sourceWorld)||!RebirthRemoteResourceBlockAdmission.TryReadNativeUser(original,out _))return false;
            string owner=RebirthStablePlayerIdentity.ComputeStorageKey(original.Owner);
            Guid source=RebirthRemoteResourceDurableJournal.Operation(sourceWorld,owner,original.Creation,original.Epoch,original.Request,original.Kind);
            if(!RebirthRemoteResourceDurableJournal.TryRead(owner,source,out root,out bool unknown)||unknown||root==null||root.Owner!=owner||root.World!=sourceWorld||root.Creation!=original.Creation||
                root.Phase!=2&&root.Phase!=4||(int)root.Copy().Attribute("deaths")!=original.Deaths||(int)root.Copy().Attribute("kind")!=(int)original.Kind)return false;
            return TryRead(original,root,out accepted);
        }
        catch{root=null;accepted=null;return false;}
    }
    private static bool KnownAbsent(string name,Guid operation)
    {
        string directory=Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH",name);if(!Directory.Exists(directory))return true;
        if((File.GetAttributes(directory)&FileAttributes.ReparsePoint)!=0)return false;
        return !Directory.EnumerateFiles(directory,operation.ToString("N")+".*",SearchOption.TopDirectoryOnly).Any()&&
            !Directory.Exists(Path.Combine(directory,"completed",operation.ToString("N")));
    }
    internal static bool DeclineBeforeAttempt(EntityPlayer actor,RebirthRemoteResourceClientOriginal.Intent original,out string proof)
    {
        proof=null;
        try
        {
            if(!RebirthRemoteResourceBlockSettlement.Read(actor,original,out _,out var root)||root.Phase!=2||!TryRead(original,root,out var accepted)||
                !KnownAbsent("RemoteBlockHostAttempts",original.Operation))return false;
            // An accepted, positively paid command is owed when the host
            // positively declines BEFORE it ever attempts ChangeBlocks.
            var body=new XElement("blockHostDeclined",new XAttribute("version",1),new XAttribute("original",original.Digest),new XAttribute("command",accepted.Digest),
                new XAttribute("source",root.Operation.ToString("N")),new XAttribute("binding",root.Binding),new XAttribute("ownerProof",accepted.OwnerProof));
            string text=body.ToString(SaveOptions.DisableFormatting);
            if(!Publish("RemoteBlockHostDeclined",original,Encoding.UTF8.GetBytes(text)))return false;
            proof=RebirthRemoteResourceDurableJournal.Digest(text);return true;
        }
        catch{proof=null;return false;}
    }
    private static XElement DeclinedBody(RebirthRemoteResourceClientOriginal.Intent original,RebirthRemoteResourceDurableJournal.Record root,Accepted accepted)
        =>new XElement("blockHostDeclined",new XAttribute("version",1),new XAttribute("original",original.Digest),new XAttribute("command",accepted.Digest),
            new XAttribute("source",root.Operation.ToString("N")),new XAttribute("binding",root.Binding),new XAttribute("ownerProof",accepted.OwnerProof));
    internal static bool DeclineAcceptedWorld(World world,RebirthRemoteResourceClientOriginal.Intent original,out string proof)
    {
        proof=null;
        try
        {
            if(!TryReadAcceptedWorld(world,original,out var root,out var accepted)||root.Phase!=2||!KnownAbsent("RemoteBlockHostAttempts",original.Operation))return false;
            string text=DeclinedBody(original,root,accepted).ToString(SaveOptions.DisableFormatting);
            if(!Publish("RemoteBlockHostDeclined",original,Encoding.UTF8.GetBytes(text)))return false;
            proof=RebirthRemoteResourceDurableJournal.Digest(text);return true;
        }
        catch{proof=null;return false;}
    }
    internal static bool TryReadDeclined(World world,RebirthRemoteResourceClientOriginal.Intent original,out string proof)
    {
        proof=null;
        try
        {
            if(!TryReadAcceptedWorld(world,original,out var root,out var accepted)||!KnownAbsent("RemoteBlockHostAttempts",original.Operation))return false;
            return TryReadDeclined(original,root,out proof);
        }
        catch{proof=null;return false;}
    }
    internal static bool TryReadDeclined(RebirthRemoteResourceClientOriginal.Intent original,RebirthRemoteResourceDurableJournal.Record root,out string proof)
    {
        proof=null;
        try
        {
            if(!TryRead(original,root,out var accepted)||!KnownAbsent("RemoteBlockHostAttempts",original.Operation))return false;
            var record=Journal("RemoteBlockHostDeclined").Read(original.Operation);
            if(record?.State!=RebirthCamperTransferJournal.Phase.Applied||record.Vehicle!=original.Operation||record.OwnerCreation!=original.Creation||record.Revision!=0||record.Before.Length>4096||
                !record.Before.SequenceEqual(record.After))return false;
            string text=DeclinedBody(original,root,accepted).ToString(SaveOptions.DisableFormatting);
            if(!Encoding.UTF8.GetBytes(text).SequenceEqual(record.Before))return false;
            proof=RebirthRemoteResourceDurableJournal.Digest(text);return true;
        }
        catch{proof=null;return false;}
    }
    internal static bool HasDeclinedOrUnknown(RebirthRemoteResourceClientOriginal.Intent original)
    {
        try{return !KnownAbsent("RemoteBlockHostDeclined",original.Operation);}
        catch{return true;}
    }
    internal static bool CanIssueOwnerPermission(RebirthRemoteResourceClientOriginal.Intent original)
    {
        try{return KnownAbsent("RemoteBlockHostDeclined",original.Operation)&&KnownAbsent("RemoteBlockHostAttempts",original.Operation)&&KnownAbsent("RemoteBlockAcceptedCommands",original.Operation)&&KnownAbsent("RemoteBlockAcceptedPending",original.Operation);}
        catch{return false;}
    }
}
