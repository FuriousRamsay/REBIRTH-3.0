using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;

// Read-only SAME-original effect evidence, not a command replay/retirement API.
// The caller must separately retain the authenticated complete request binding.
internal static class RebirthCamperRemoteStationCommandStatus
{
    internal enum State : byte { Unknown=0, Pending=1, Applied=2, Rejected=3 }
    internal static bool HasNativeOriginal(RebirthCamperRemoteStationCommand command)
        =>command!=null&&(command.IsFuel||command.Kind==RebirthCamperRemoteStationCommandKind.Craft||command.Kind==RebirthCamperRemoteStationCommandKind.CraftPlayer||
            command.Kind==RebirthCamperRemoteStationCommandKind.ScrapQueue||
            command.Kind==RebirthCamperRemoteStationCommandKind.ScrapCancel||
            command.Kind==RebirthCamperRemoteStationCommandKind.Cancel);
    internal sealed class Observation
    {
        internal readonly Guid RequestOperation,EffectOperation,Job;
        internal readonly string Owner,Creation,Address,RequestBinding,EffectProof;
        internal readonly RebirthCamperRemoteStationCommandKind Kind;
        internal readonly int OriginalDeaths;
        internal readonly State Status;
        internal Observation(RebirthCamperRemoteStationCommand c,string owner,string creation,RebirthCamperStationAddress address,Guid effect,string proof,int deaths,State status)
        {RequestOperation=c.Operation;EffectOperation=effect;Job=c.Job;Kind=c.Kind;Owner=owner;Creation=creation;Address=address.ToString();RequestBinding=Binding(c,owner,creation,address);EffectProof=proof;OriginalDeaths=deaths;Status=status;}
        // A positive paid-effect witness does NOT bind a new arbitrary request ID
        // to a historical job and does not prove a declined/no-effect outcome.
        internal bool AuthorizesCommandRetirement=>Status==State.Applied||Status==State.Rejected; // Constructed only after exact durable request identity + positive saved effect or actual pre-effect refusal.
    }
    internal static string Binding(RebirthCamperRemoteStationCommand c,string owner,string creation,RebirthCamperStationAddress address)
    {
        if(c==null||!address.IsValid||owner==null||owner.Length!=64||creation==null||creation.Length>512)throw new ArgumentException();
        var node=new XElement("stationCommand",new XAttribute("version",1),new XAttribute("owner",owner),new XAttribute("creation",creation),new XAttribute("address",address.ToString()),new XAttribute("operation",c.Operation.ToString("N")),new XAttribute("kind",(byte)c.Kind),new XAttribute("job",c.Job.ToString("N")),new XAttribute("recipe",c.RecipeName),new XAttribute("batches",c.Batches),new XAttribute("tier",c.Tier));
        using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(node.ToString(SaveOptions.DisableFormatting)))).Replace("-","").ToLowerInvariant();
    }
    private static Guid NativeCancel(Guid job)
    {using(var sha=SHA256.Create()){var bytes=sha.ComputeHash(Encoding.UTF8.GetBytes(job.ToString("N")+":native-actor-cancel"));var id=new byte[16];Array.Copy(bytes,id,16);return new Guid(id);}}
    internal static bool TryReadEffect(EntityPlayer actor,RebirthCamperStationAddress address,string creation,object lease,RebirthCamperRemoteStationCommand command,out Observation observation)
    {
        var status=ReadStatus(actor,address,creation,lease,command,out observation);
        if(status==State.Applied)return true;observation=null;return false;
    }
    internal static State ReadStatus(EntityPlayer actor,RebirthCamperStationAddress address,string creation,object lease,RebirthCamperRemoteStationCommand command,out Observation observation)
        =>ReadStatusCore(actor,address,creation,lease,command,true,out observation);
    // Diagnostic observation uses the identical native qualification, without
    // publishing never-invoked metadata or archiving an identity as a side effect.
    internal static State ObserveStatus(EntityPlayer actor,RebirthCamperStationAddress address,string creation,object lease,RebirthCamperRemoteStationCommand command,out Observation observation)
        =>ReadStatusCore(actor,address,creation,lease,command,false,out observation);
    private static State ReadStatusCore(EntityPlayer actor,RebirthCamperStationAddress address,string creation,object lease,RebirthCamperRemoteStationCommand command,bool publishMetadata,out Observation observation)
    {
        observation=null;
        try
        {
            if(!ThreadManager.IsMainThread()||actor==null||actor.world?.IsRemote()!=false||!ReferenceEquals(GameManager.Instance?.World,actor.world)||!ReferenceEquals(actor.world.GetEntity(actor.entityId),actor)||actor.IsDead()||command==null||creation!=RebirthCamperCharacterAccess.CurrentCreation(actor)||
                !RebirthCamperRemoteStationTransport.IsCurrent(actor,address,creation,lease)||!RebirthCamperStationNativeActorPayment.RemoteIdentity(actor,out var owner,out _))return State.Unknown;
            if(!RebirthCamperRemoteStationCommandStatusStore.TryReadExact(actor,address,creation,lease,command,out var requestBinding))return State.Unknown;
            if(publishMetadata)RebirthCamperRemoteStationCommandStatusStore.TrySaveNeverInvoked(actor,address,creation,lease,command);
            if(RebirthCamperRemoteStationCommandStatusStore.TryReadBeforeEffectRefusal(actor,address,creation,lease,command,out var refusedDeaths,out var refusalCandidate))
            {
                observation=new Observation(command,owner.StorageKey,creation,address,Guid.Empty,
                    "saved-before-effect-refusal:"+requestBinding,refusedDeaths,State.Rejected);
                if(publishMetadata)RebirthCamperRemoteStationCommandStatusStore.TryArchiveBeforeEffectIdentity(actor,address,creation,lease,command);
                return State.Rejected;
            }
            if(refusalCandidate)return State.Unknown; // corrupt retained refusal cannot be bypassed
            if(!RebirthCamperRemoteStationCommandStatusStore.HasInvocation(actor,address,creation,lease,command))return State.Unknown;
            if(RebirthSurvivorMode.IsEnabledForCurrentWorld()&&(command.Kind==RebirthCamperRemoteStationCommandKind.Craft||command.Kind==RebirthCamperRemoteStationCommandKind.CraftPlayer||command.Kind==RebirthCamperRemoteStationCommandKind.Cancel))
            {
                if(!RebirthCamperRemoteStationCommandStatusStore.TryReadAdmittedDeaths(actor,address,creation,lease,command,out int admittedDeaths))return State.Unknown;
                var status=RebirthCamperRemoteStationSurvivorStatus.Read(actor,address,creation,lease,command,out var savedProof);
                if(status==State.Unknown)return State.Unknown;
                observation=new Observation(command,owner.StorageKey,creation,address,command.Job,savedProof,admittedDeaths,status);
                if(observation.RequestBinding!=requestBinding){observation=null;return State.Unknown;}
                if(publishMetadata&&status==State.Applied)RebirthCamperRemoteStationCommandStatusStore.TryArchiveAppliedIdentity(actor,address,creation,lease,command,observation);
                return status;
            }
            if(command.IsFuel||command.Kind==RebirthCamperRemoteStationCommandKind.Craft&&!RebirthSurvivorMode.IsEnabledForCurrentWorld()||
                command.Kind==RebirthCamperRemoteStationCommandKind.Cancel&&RebirthCamperStationFuelOriginal.HasOriginal(address,command.Operation))
            {
                var fuelStatus=RebirthCamperStationFuelOriginal.ReadStatus(actor,address,creation,command,out var fuelProof,out var fuelDeaths);
                if(fuelStatus==State.Unknown)return State.Unknown;
                observation=new Observation(command,owner.StorageKey,creation,address,command.Operation,fuelProof,fuelDeaths,fuelStatus);
                if(observation.RequestBinding!=requestBinding){observation=null;return State.Unknown;}
                if(publishMetadata&&fuelStatus==State.Applied)RebirthCamperRemoteStationCommandStatusStore.TryArchiveAppliedIdentity(actor,address,creation,lease,command,observation);
                return fuelStatus;
            }
            Guid effect;
            switch(command.Kind)
            {
                case RebirthCamperRemoteStationCommandKind.CraftPlayer:
                case RebirthCamperRemoteStationCommandKind.ScrapQueue:
                    if(command.Operation!=command.Job)return State.Unknown;effect=command.Job;break;
                case RebirthCamperRemoteStationCommandKind.ScrapCancel:effect=RebirthRecyclerRuntime.CancelOperation(command.Job);break;
                case RebirthCamperRemoteStationCommandKind.Cancel:effect=NativeCancel(command.Job);break;
                default:return State.Unknown; // Native station-input Craft has no retained request-effect receipt here.
            }
            RebirthCamperStationState before,after;string proof;int deaths;State state;
            if(RebirthRemoteResourceStationPaymentWitness.TryReadSavedPayment(actor.world,effect,address.ToString(),owner.StorageKey,creation,out var saved)&&saved.OriginalDeaths>=0&&saved.OwnerKey==owner.StorageKey&&saved.Address==address.ToString()&&saved.Creation==creation)
            {before=saved.RestoreBefore();after=saved.RestoreAfter();proof=saved.Proof;deaths=saved.OriginalDeaths;state=State.Applied;}
            else
            {
                if(!RebirthCamperStationNativeActorPayment.TryReadCommandStatusOriginal(actor,address,creation,lease,effect,out var retained))return State.Unknown;
                before=retained.Before;after=retained.After;proof=retained.Proof;deaths=retained.OriginalDeaths;
                state=retained.Phase==RebirthCamperTransferJournal.Phase.Rejected?State.Rejected:retained.Phase==RebirthCamperTransferJournal.Phase.Prepared||retained.Phase==RebirthCamperTransferJournal.Phase.Uncertain?State.Pending:State.Unknown;
                if(state==State.Unknown)return State.Unknown;
            }
            if(before.Queue==null||after.Queue==null||before.Queue.Length!=after.Queue.Length)return State.Unknown;
            bool isRecycler=command.Kind==RebirthCamperRemoteStationCommandKind.ScrapQueue||command.Kind==RebirthCamperRemoteStationCommandKind.ScrapCancel;
            Func<RecipeQueueItem,bool> belongs=row=>isRecycler?RebirthRecyclerRuntime.TryGroup(row?.Recipe,out var group)&&group==command.Job:RebirthCamperStationNativeCrafting.TryJob(row?.Recipe,out var job)&&job==command.Job;
            var oldRows=before.Queue.Where(belongs).ToArray();var newRows=after.Queue.Where(belongs).ToArray();
            bool cancel=command.Kind==RebirthCamperRemoteStationCommandKind.Cancel||command.Kind==RebirthCamperRemoteStationCommandKind.ScrapCancel;
            if(cancel)
            {if(oldRows.Length==0||newRows.Length!=0)return State.Unknown;}
            else if(oldRows.Length!=0||newRows.Length==0)return State.Unknown;
            if(command.Kind==RebirthCamperRemoteStationCommandKind.CraftPlayer)
            {
                if(newRows.Length!=1||newRows[0].Multiplier!=command.Batches||newRows[0].Quality!=command.Tier||newRows[0].Recipe.GetName()!=command.RecipeName)return State.Unknown;
            }
            if(isRecycler)
            {
                foreach(var row in cancel?oldRows:newRows)
                {
                    if(!RebirthRecyclerRuntime.ValidRow(cancel?before:after,row))return State.Unknown;
                    var token=row.Recipe.ingredients[0].itemValue;
                    if(!token.TryGetMetadata(RebirthRecyclerRuntime.Prefix+"owner",out string who)||who!=owner.CanonicalId||!token.TryGetMetadata(RebirthRecyclerRuntime.Prefix+"creation",out string scope)||scope!=creation)return State.Unknown;
                    if(!cancel&&(!token.TryGetMetadata(RebirthRecyclerRuntime.Prefix+"units",out int units)||units!=command.Batches||token.ItemClass.GetItemName()!=command.RecipeName))return State.Unknown;
                }
                if(!cancel)
                {
                    if(before.Input==null||after.Input==null||before.Input.Length!=after.Input.Length||command.Tier<0||command.Tier>=before.Input.Length)return State.Unknown;
                    for(int slot=0;slot<before.Input.Length;slot++)
                    {
                        var prior=before.Input[slot];var next=after.Input[slot];if(prior==null||next==null)return State.Unknown;
                        if(slot!=command.Tier){if(!RebirthStationGridIngredients.IsSameStackSnapshot(prior,next))return State.Unknown;continue;}
                        if(prior.IsEmpty()||prior.itemValue.ItemClass.GetItemName()!=command.RecipeName||prior.count<command.Batches)return State.Unknown;
                        var expected=prior.Clone();expected.count-=command.Batches;if(expected.count==0)expected=ItemStack.Empty.Clone();
                        if(!RebirthStationGridIngredients.IsSameStackSnapshot(expected,next))return State.Unknown;
                    }
                }
            }
                        observation=new Observation(command,owner.StorageKey,creation,address,effect,proof,deaths,state);
            if(observation.RequestBinding==requestBinding)
            {
                if(publishMetadata&&state==State.Applied)RebirthCamperRemoteStationCommandStatusStore.TryArchiveAppliedIdentity(actor,address,creation,lease,command,observation);
                else if(publishMetadata&&state==State.Rejected)RebirthCamperRemoteStationCommandStatusStore.TryArchiveRejectedIdentity(actor,address,creation,lease,command,observation);
                return state;
            }
            observation=null;return State.Unknown;
        }
        catch{observation=null;return State.Unknown;} // Missing/unsupported evidence stays unknown, never rejected/unused.
    }
}





