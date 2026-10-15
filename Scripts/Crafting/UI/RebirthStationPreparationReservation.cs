using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

// Live authority-thread reservation across generic station preparation/commit.
// A claim is not payment, queue acceptance, or a durable publication receipt.
public static class RebirthStationPreparationReservation
{
    private sealed class Claims
    {
        internal readonly Dictionary<string,Claim> Entries=new Dictionary<string,Claim>(StringComparer.Ordinal);
    }
    private sealed class Claim
    {
        internal string Owner,Job;
        internal bool NativeAttemptReady;
        internal int AttemptThread;
        internal RebirthStationGridAdmission Admission;
    }
    private static readonly object Sync=new object();
    private static readonly ConditionalWeakTable<World,Claims> Worlds=new ConditionalWeakTable<World,Claims>();
    private const int MaximumClaims=1024;
    private static string StationKey(RebirthStationGridAdmission admission)
    {
        if(admission.TryGetMobileAddress(out var address))return address.ToString();
        if(admission.IsMobile)throw new InvalidOperationException("Invalid mobile station address.");
        var image=admission.Write();
        return "terrain:"+((int)image.Attribute("x")).ToString(System.Globalization.CultureInfo.InvariantCulture)+":"+
            ((int)image.Attribute("y")).ToString(System.Globalization.CultureInfo.InvariantCulture)+":"+
            ((int)image.Attribute("z")).ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
    public static bool TryAcquire(World world,RebirthWorldCharacterRecord owner,RebirthStationGridAdmission admission)
    {
        if(world==null||world.IsRemote()||!ReferenceEquals(world,GameManager.Instance?.World)||
            !RebirthWorldCharacterRepository.IsServerAuthority||owner?.Progression==null||!owner.IsComplete||
            admission==null||owner.Progression.StationRefundArchives.ContainsKey(admission.JobId)||
            !RebirthSurvivorRequestScope.Matches(admission.CreationId,owner.Origin?.CreationId))return false;
        var image=admission.Write();var position=StationKey(admission);
        lock(Sync)
        {
            var claims=Worlds.GetOrCreateValue(world);
            Claim existing;
            if(claims.Entries.TryGetValue(position,out existing))
                return existing.Owner==owner.StablePlayerKey&&existing.Job==admission.JobId&&
                    System.Xml.Linq.XNode.DeepEquals(existing.Admission.Write(),image)&&
                    RebirthWorldCharacterRepository.HasExclusiveStationPreparation(owner,admission);
            if(claims.Entries.Count>=MaximumClaims||!RebirthWorldCharacterRepository.HasExclusiveStationPreparation(owner,admission))return false;
            claims.Entries.Add(position,new Claim{Owner=owner.StablePlayerKey,Job=admission.JobId,Admission=admission.Clone()});
            return true;
        }
    }
    // A failed/uncertain save retains BOTH intent and live reservation for recovery.
    public static bool TryRegister(World world,RebirthWorldCharacterRecord owner,RebirthStationGridAdmission admission,Func<bool> save)
    {
        if(save==null||owner?.Progression==null||admission==null||
            !RebirthStationPreparationPersistence.MatchesOwner(owner.Progression.StationPreparations,owner.Origin?.CreationId)||
            owner.Progression.StationPreparations.Count>=RebirthStationPreparationPersistence.MaximumRecords&&
                !owner.Progression.StationPreparations.ContainsKey(admission.JobId)||
            !TryAcquire(world,owner,admission))return false;
        return RebirthStationPreparationPersistence.TryRegister(owner.Progression.StationPreparations,
            owner.Origin.CreationId,admission,save);
    }
    // Saved phase transition; failure retains the attempted intent and station claim.
    internal static bool TryMarkPublicationAttempt(World world,RebirthWorldCharacterRecord owner,
        RebirthStationGridAdmission prepared,Func<RebirthStationGridAdmission,bool> save,
        out RebirthStationGridAdmission attempted)
    {
        attempted=null;
        if(save==null||prepared==null||prepared.IsPublicationAttempted||!TryAcquire(world,owner,prepared))return false;
        var image=prepared.Write();var position=StationKey(prepared);
        lock(Sync)
        {
            if(!Worlds.TryGetValue(world,out var claims)||!claims.Entries.TryGetValue(position,out var claim)||
                claim.Owner!=owner.StablePlayerKey||claim.Job!=prepared.JobId||
                !System.Xml.Linq.XNode.DeepEquals(claim.Admission.Write(),image)||
                owner.Progression.StationPublications.ContainsKey(prepared.JobId)||owner.Progression.CamperStationPublications.ContainsKey(prepared.JobId)||
                !owner.Progression.StationPreparations.TryGetValue(prepared.JobId,out var stored)||stored==null||
                !System.Xml.Linq.XNode.DeepEquals(stored.Write(),image)||!prepared.TryMarkPublicationAttempted(out var next))return false;
            owner.Progression.StationPreparations[prepared.JobId]=next.Clone();claim.Admission=next.Clone();attempted=next;
            RebirthWorldCharacterService.MarkDirty(owner,"station-publication-attempt");
            try{if(!save(next))return false;claim.NativeAttemptReady=true;claim.AttemptThread=System.Threading.Thread.CurrentThread.ManagedThreadId;return true;}catch{return false;}
        }
    }
    // One-use in-process permission from the ORIGINAL successful attempt save only.
    // Consume before any native effect; failure/exception afterward never restores permission.
    internal static bool TryConsumeNativeAttempt(World world,RebirthWorldCharacterRecord owner,
        RebirthStationGridAdmission attempted)
    {
        if(attempted==null||!attempted.IsPublicationAttempted||
            !RebirthStationObservationDispatcher.IsCurrentAuthorityThread(world)||!TryAcquire(world,owner,attempted))return false;
        var image=attempted.Write();var position=StationKey(attempted);
        lock(Sync)
        {
            if(!Worlds.TryGetValue(world,out var claims)||!claims.Entries.TryGetValue(position,out var claim)||
                !claim.NativeAttemptReady||claim.AttemptThread!=System.Threading.Thread.CurrentThread.ManagedThreadId||
                claim.Owner!=owner.StablePlayerKey||claim.Job!=attempted.JobId||
                !System.Xml.Linq.XNode.DeepEquals(claim.Admission.Write(),image)||
                owner.Progression.StationPublications.ContainsKey(attempted.JobId)||owner.Progression.CamperStationPublications.ContainsKey(attempted.JobId)||
                !owner.Progression.StationPreparations.TryGetValue(attempted.JobId,out var stored)||stored==null||
                !System.Xml.Linq.XNode.DeepEquals(stored.Write(),image))return false;
            claim.NativeAttemptReady=false;return true;
        }
    }
    // Owner payment confirmation updates the SAME station claim and durable
    // admission. It never restores the consumed native-attempt permission.
    internal static bool TryConfirmPlayerPayment(EntityPlayer player,RebirthWorldCharacterRecord owner,
        RebirthStationGridAdmission attempted,Func<RebirthStationGridAdmission,bool> save,
        out RebirthStationGridAdmission confirmed)
    {
        confirmed=null;
        if(player?.world==null||save==null||attempted==null||!attempted.IsPlayerPayment||attempted.IsPlayerPaymentConfirmed||
            !RebirthStationObservationDispatcher.IsCurrentAuthorityThread(player.world)||
            !RebirthCamperStationPlayerPayment.HasSaved(player,attempted)||!TryAcquire(player.world,owner,attempted))return false;
        var position=StationKey(attempted);var original=attempted.Write();
        lock(Sync)
        {
            if(!Worlds.TryGetValue(player.world,out var claims)||!claims.Entries.TryGetValue(position,out var claim)||
                claim.NativeAttemptReady||claim.Owner!=owner.StablePlayerKey||claim.Job!=attempted.JobId||
                !System.Xml.Linq.XNode.DeepEquals(claim.Admission.Write(),original)||
                !owner.Progression.StationPreparations.TryGetValue(attempted.JobId,out var retained)||retained==null||
                !System.Xml.Linq.XNode.DeepEquals(retained.Write(),original)||!attempted.TryConfirmPlayerPayment(out var next))return false;
            owner.Progression.StationPreparations[attempted.JobId]=next.Clone();claim.Admission=next.Clone();confirmed=next;
            RebirthWorldCharacterService.MarkDirty(owner,"camper-actor-payment-confirmed");
            try{return save(next)&&RebirthCamperStationPlayerPayment.HasSaved(player,next);}catch{return false;}
        }
    }
    // Dispatcher may release only after its terminal settlement save succeeds.
    // Never use timeout, lost UI lock, or a missing live queue as terminal evidence.
    public static bool TryReleaseSettled(World world,RebirthWorldCharacterRecord owner,RebirthStationGridAdmission admission,Func<bool> saveTerminal)
    {
        if(world==null||world.IsRemote()||!ReferenceEquals(world,GameManager.Instance?.World)||
            !RebirthWorldCharacterRepository.IsServerAuthority||owner?.Progression==null||!owner.IsComplete||admission==null||saveTerminal==null||
            !RebirthSurvivorRequestScope.Matches(admission.CreationId,owner.Origin?.CreationId))return false;
        var image=admission.Write();var position=StationKey(admission);
        lock(Sync)
        {
            Claims claims;Claim claim;
            if(!Worlds.TryGetValue(world,out claims)||!claims.Entries.TryGetValue(position,out claim))
            {
                // Positive archived admission and final-file proof, never missing claim alone.
                if(owner.Progression.StationPreparations.ContainsKey(admission.JobId)||
                    !owner.Progression.StationRefundArchives.TryGetValue(admission.JobId,out var archive)||archive==null||
                    !System.Xml.Linq.XNode.DeepEquals(archive.Write().Element("stationAdmission"),image))return false;
                try{return saveTerminal();}catch{return false;}
            }
            if(claim.Owner!=owner.StablePlayerKey||claim.Job!=admission.JobId||
                !System.Xml.Linq.XNode.DeepEquals(claim.Admission.Write(),image)||
                owner.Progression.StationPreparations.ContainsKey(admission.JobId))return false;
            try{if(!saveTerminal())return false;}catch{return false;}
            claims.Entries.Remove(position);return true;
        }
    }
}
