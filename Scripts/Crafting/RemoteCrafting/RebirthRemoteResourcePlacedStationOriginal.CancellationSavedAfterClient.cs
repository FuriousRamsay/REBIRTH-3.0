using System;
using System.Linq;
using System.Text;
using System.Xml.Linq;

internal static partial class RebirthRemoteResourcePlacedStationOriginal
{
    private static bool CancellationSavedAfterCandidate(CancellationClient original)
    {
        if(original?.Memory?.Current!=true||original.Stage!=2||original.OwnerSavedAfterVersion?.IsCurrent!=true||original.Permission==null||
            !original.PermissionSaved||!RebirthRemoteResourceClientOriginal.Current(original.Actor,original.Original)||
            original.Actor.Buffs.GetCustomVar(CancellationReceipt(original.Operation))!=2f)return false;
        var permission=CancellationJournal(original.Operation,"Permission").Read(original.Operation);byte[] bytes=Encoding.UTF8.GetBytes(original.Permission);
        return permission?.State==RebirthCamperTransferJournal.Phase.Uncertain&&permission.Vehicle==original.Source&&permission.OwnerCreation==original.Original.Creation&&permission.Revision==0&&
            permission.Before.SequenceEqual(bytes)&&permission.After.SequenceEqual(bytes);
    }
    internal sealed class CancellationSavedAfterProjection
    {
        private readonly CancellationClient original;private readonly string permission,evidence;
        private CancellationSavedAfterProjection(CancellationClient value,string evidence){original=value;permission=value.Permission;this.evidence=evidence;}
        internal bool Current=>original.OwnerCurrent()&&original.OwnerSavedAfterVersion?.IsCurrent==true&&original.Permission==permission&&original.TerminalEvidence==evidence&&original.HostTerminal;
        internal static CancellationSavedAfterProjection FromActualResult(CancellationClient original,string text,CancellationNativeEvidence saved)
        {
            var native=cancellationNativeResult;
            if(native?.Current!=true||!ReferenceEquals(native.Actor,original.Actor)||native.Operation!=original.Original.Operation||native.Reconstruction!=text||
                !CancellationSavedAfterCandidate(original)||native.FullPermission!=original.Permission||native.FullEvidence==null||text==null||saved==null)return null;
            long workspace=checked(12L*Encoding.UTF8.GetByteCount(text)+65536);
            if(workspace>original.ReentryWorkspace)
            {if(!original.Memory.Reserve(checked(original.Memory.ReservedBytes+workspace-original.ReentryWorkspace)))return null;original.ReentryWorkspace=workspace;}
            var eventBody=RebirthCookingRemoteAdmission.ReadProtocolXml(text,MaximumResultBytes);
            bool queueRecovery=(int?)eventBody.Attribute("version")==3&&(string)eventBody.Attribute("cause")=="queue-reentry-saved-after";
            bool firstAfter=(int?)eventBody.Attribute("version")==4&&(string)eventBody.Attribute("cause")=="before-load-first-attempt-saved-after";
            if(eventBody.Name!="placedCancellationReconstruction"||eventBody.Attributes().Count()!=(queueRecovery||firstAfter?13:12)||eventBody.Nodes().Any()||!queueRecovery&&!firstAfter&&(int?)eventBody.Attribute("version")!=2||
                !Guid.TryParseExact((string)eventBody.Attribute("recovery"),"N",out var recovery)||recovery==Guid.Empty||(long?)eventBody.Attribute("generation") is not long generation||generation<1||
                (string)eventBody.Attribute("cancel")!=original.Operation.ToString("N")||(string)eventBody.Attribute("source")!=original.Source.ToString("N")||
                (string)eventBody.Attribute("binding")!=original.Binding||(string)eventBody.Attribute("birth")!=original.Birth||(string)eventBody.Attribute("world")!=original.Original.World||
                !ValidateInput(original.Original,out var position,out _,out _,out _,out _,out _,out _,out _,out _,out _,out _)||
                (int?)eventBody.Attribute("chunkX")!=(position.x>>4)||(int?)eventBody.Attribute("chunkZ")!=(position.z>>4)||
                (long?)eventBody.Attribute("tileOffset")!=saved.Offset||(int?)eventBody.Attribute("tileLength")!=saved.Tile.Length)return null;
            // Full Before/After, saved native2 and original refund were already
            // independently checked by this actual result's consumer. This
            // projection adopts no target and grants no inventory/TE setter.
            if(firstAfter)ObserveCancellationScenarioEvent(original.Operation,"actualBeforeTwoAfterProjection",text);
            return new CancellationSavedAfterProjection(original,native.FullEvidence);
        }
    }
}
