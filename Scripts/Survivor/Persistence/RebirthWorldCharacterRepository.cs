using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;

#nullable disable

/// <summary>
/// Server-authoritative one-file-per-player Survivor world repository.
/// The immutable origin and mutable progression/condition/support sections are serialized
/// separately inside the same player record. Metabolism remains owned by its existing repository.
/// </summary>
public static partial class RebirthWorldCharacterRepository
{
    private static readonly object Sync = new object();
    private static readonly Dictionary<string, RebirthWorldCharacterRecord> Cache =
        new Dictionary<string, RebirthWorldCharacterRecord>(StringComparer.Ordinal);
    private static readonly List<RebirthWorldCharacterPersistenceIssue> Issues =
        new List<RebirthWorldCharacterPersistenceIssue>();
    private static readonly Dictionary<string, long> RetryAfterUtcTicks = new Dictionary<string, long>(StringComparer.Ordinal);
    private static readonly Dictionary<string, object> KeyWriteLocks = new Dictionary<string, object>(RepositoryPathComparer);
    private static bool serverAuthority;

    public static string RootDirectory
    {
        get
        {
            string root = GameIO.GetSaveGameDir();
            return string.IsNullOrEmpty(root) ? string.Empty : Path.Combine(root, "RebirthData", "Survivor", "Players");
        }
    }

    public static bool IsServerAuthority { get { return serverAuthority; } }

    public static void Reset(bool asServer)
    {
        lock (Sync)
        {
            Cache.Clear();
            Issues.Clear();
            RetryAfterUtcTicks.Clear();
            RepositoryEpoch = new object(); // Stable gates survive reset; epoch revokes old selections.
            serverAuthority = asServer;
        }
    }

    public static RebirthWorldCharacterPersistenceIssue[] GetIssuesSnapshot()
    {
        lock (Sync) return Issues.ToArray();
    }

    // Includes offline final files and cached pending intents. Observation only: a future
    // mutation dispatcher must also own a world-wide station reservation across commit.
    public static bool HasExclusiveStationPreparation(RebirthWorldCharacterRecord owner,RebirthStationGridAdmission admission)
    {
        if(!serverAuthority||owner==null||!owner.IsComplete||admission==null)return false;
        lock(Sync){foreach(var pair in Cache){var record=pair.Value;if(record?.Progression==null)continue;
            foreach(var other in record.Progression.StationPreparations.Values)
                if(!RebirthStationOfflineClaims.Compatible(owner.StablePlayerKey,admission,pair.Key,other))return false;}}
        return RebirthStationOfflineClaims.CheckDirectory(RootDirectory,owner.StablePlayerKey,admission);
    }
    internal static bool HasRetainedRemoteResourceRefunds()
    {
        lock(Sync)return Cache.Values.Any(record=>record?.Support?.RemoteResourceRefundJournalImage!=null);
    }
    // Current final character file only. This witnesses a saved journal image,
    // never native owner credit or authenticated world/transaction entitlement.
    internal static bool HasSavedRemoteResourceRefunds(RebirthStablePlayerIdentity identity,string creation,XElement expected)
    {
        if(!serverAuthority||identity==null||expected==null)return false;
        string path=GetPath(identity.StorageKey);if(string.IsNullOrEmpty(path))return false;
        lock(GetWriteLock(identity.StorageKey))
        {
            try
            {
                var validated=RemoteResourceRefundSupportPersistence.Write(expected,identity.StorageKey,creation);
                if(validated==null||!TryLoadValidatedRecord(path,identity,out var saved,out var migrated,out _,out _)||migrated||
                    saved?.Support?.RemoteResourceRefundJournalImage==null||
                    !RebirthSurvivorRequestScope.Matches(creation,saved.Origin?.CreationId)||path!=GetPath(identity.StorageKey))return false;
                return XNode.DeepEquals(validated,saved.Support.RemoteResourceRefundJournalImage);
            }
            catch{return false;}
        }
    }
    // Exact final-file data witness only. Completion, eligibility and current live scope are separate.
    internal static bool HasSavedRecipeDiscovery(RebirthStablePlayerIdentity identity,
        RebirthStationRecipeDiscoveryRecord expected)
    {
        if(!serverAuthority||identity==null||expected==null)return false;
        var image=expected.Write();
        if(!RebirthStationRecipeDiscoveryRecord.TryReadStored(image,out var validated)||
            (string)image.Attribute("owner")!=identity.StorageKey)return false;
        string creation=(string)image.Attribute("creation");
        string path=GetPath(identity.StorageKey);if(string.IsNullOrEmpty(path))return false;
        lock(GetWriteLock(identity.StorageKey))
        {
            try
            {
                if(!serverAuthority||!TryLoadValidatedRecord(path,identity,out var saved,out var migrated,out _,out _)||migrated||
                    saved?.Progression==null||!RebirthSurvivorRequestScope.Matches(creation,saved.Origin?.CreationId)||
                    !saved.Progression.RecipeDiscoveries.TryGetValue(validated.CanonicalRecipe,out var stored)||stored==null||
                    !serverAuthority||path!=GetPath(identity.StorageKey))return false;
                return XNode.DeepEquals(stored.Write(),image);
            }
            catch{return false;}
        }
    }
    // Original final-file intent class, never ambiguous absence/cache/backup authority.
    internal static bool TryGetSavedStationPreparationIntent(RebirthStablePlayerIdentity identity,
        RebirthStationGridAdmission expected,out RebirthStationSavedPreparationIntent intent)
    {
        intent=null;if(!serverAuthority||identity==null||expected==null)return false;
        string path=GetPath(identity.StorageKey);if(string.IsNullOrEmpty(path))return false;
        lock(GetWriteLock(identity.StorageKey))
        {
            try
            {
                if(!serverAuthority||!TryLoadValidatedRecord(path,identity,out var saved,out var migrated,out _,out _)||migrated||
                    saved?.Progression==null||!RebirthSurvivorRequestScope.Matches(expected.CreationId,saved.Origin?.CreationId))return false;
                bool hasPrepared=saved.Progression.StationPreparations.TryGetValue(expected.JobId,out var prepared);
                bool hasBinding=saved.Progression.StationDiscoveryAdmissions.TryGetValue(expected.JobId,out var binding);
                RebirthStationSavedPreparationIntent result;
                if(!hasPrepared)
                {
                    if(hasBinding||expected.IsPublicationAttempted)return false;
                    result=new RebirthStationSavedPreparationIntent(RebirthStationSavedIntentKind.NewOriginal,null);
                }
                else
                {
                    if(prepared==null)return false;
                    bool exact=XNode.DeepEquals(prepared.Write(),expected.Write());
                    if(!exact&&expected.IsPublicationAttempted&&!prepared.IsPublicationAttempted&&prepared.TryMarkPublicationAttempted(out var next))
                        exact=XNode.DeepEquals(next.Write(),expected.Write());
                    if(!exact)return false;
                    if(hasBinding)
                    {
                        if(!RebirthStationDiscoveryPreparationPair.Matches(binding,prepared)||
                            !RebirthStationDiscoveryPreparationPair.Matches(binding,expected)||
                            (string)binding.Write().Element("stationDiscoveryWitness")?.Attribute("owner")!=identity.StorageKey)return false;
                        result=new RebirthStationSavedPreparationIntent(RebirthStationSavedIntentKind.Discovery,binding);
                    }
                    else result=new RebirthStationSavedPreparationIntent(RebirthStationSavedIntentKind.Ordinary,null);
                }
                if(path!=GetPath(identity.StorageKey)||!serverAuthority)return false;
                intent=result;return true;
            }
            catch{return false;}
        }
    }
    // Exact saved original frozen binding + known preparation phase; data witness only.
    internal static bool HasSavedStationDiscoveryAdmission(RebirthStablePlayerIdentity identity,
        RebirthStationGridAdmission admission,RebirthStationDiscoveryAdmissionBinding binding)
    {
        if(!serverAuthority||identity==null||!RebirthStationDiscoveryPreparationPair.Matches(binding,admission))return false;
        var image=binding.Write();var witness=image.Element("stationDiscoveryWitness");
        if((string)witness?.Attribute("owner")!=identity.StorageKey)return false;
        string path=GetPath(identity.StorageKey);if(string.IsNullOrEmpty(path))return false;
        lock(GetWriteLock(identity.StorageKey))
        {
            try
            {
                if(!serverAuthority||!TryLoadValidatedRecord(path,identity,out var saved,out var migrated,out _,out _)||migrated||
                    saved?.Progression==null||!RebirthSurvivorRequestScope.Matches(admission.CreationId,saved.Origin?.CreationId)||
                    !saved.Progression.StationPreparations.TryGetValue(admission.JobId,out var stored)||stored==null||
                    !saved.Progression.StationDiscoveryAdmissions.TryGetValue(admission.JobId,out var frozen)||frozen==null||
                    !XNode.DeepEquals(stored.Write(),admission.Write())||!XNode.DeepEquals(frozen.Write(),image)||
                    !RebirthStationDiscoveryPreparationPair.Matches(frozen,stored)||!serverAuthority||path!=GetPath(identity.StorageKey))return false;
                return true;
            }
            catch{return false;}
        }
    }
    // Exact current final-file witness only; cache/backup/migration cannot authorize payment.
    public static bool HasSavedStationPreparation(RebirthStablePlayerIdentity identity,RebirthStationGridAdmission admission)
    {
        if(!serverAuthority||identity==null||admission==null)return false;
        string path=GetPath(identity.StorageKey);
        if(string.IsNullOrEmpty(path))return false;
        lock(GetWriteLock(identity.StorageKey))
        {
            try
            {
                if(!TryLoadValidatedRecord(path,identity,out var saved,out var migrated,out _,out _)||migrated||
                    saved?.Progression==null||saved.Progression.StationPublications.ContainsKey(admission.JobId)||
                    !RebirthSurvivorRequestScope.Matches(admission.CreationId,saved.Origin?.CreationId)||
                    !saved.Progression.StationPreparations.TryGetValue(admission.JobId,out var stored)||stored==null)return false;
                return XNode.DeepEquals(stored.Write(),admission.Write());
            }
            catch{return false;}
        }
    }
    // Exact saved admission plus publication receipt; native region revalidation remains separate.
    internal static bool HasSavedStationPublication(RebirthStablePlayerIdentity identity,
        RebirthStationGridAdmission admission,RebirthStationPublicationRecord publication)
    {
        if(!serverAuthority||identity==null||admission==null||publication==null||publication.JobId!=admission.JobId)return false;
        string path=GetPath(identity.StorageKey);if(string.IsNullOrEmpty(path))return false;
        lock(GetWriteLock(identity.StorageKey))
        {
            try
            {
                if(!TryLoadValidatedRecord(path,identity,out var saved,out var migrated,out _,out _)||migrated||
                    saved?.Progression==null||!RebirthSurvivorRequestScope.Matches(admission.CreationId,saved.Origin?.CreationId)||
                    !saved.Progression.StationPreparations.TryGetValue(admission.JobId,out var stored)||stored==null||
                    !saved.Progression.StationPublications.TryGetValue(admission.JobId,out var receipt)||receipt==null)return false;
                return XNode.DeepEquals(stored.Write(),admission.Write())&&XNode.DeepEquals(receipt.Write(),publication.Write());
            }
            catch{return false;}
        }
    }
    // Exact saved completion record and original bindings only; native region/event revalidation is separate.
    internal static bool HasSavedStationCompletionPublication(RebirthStablePlayerIdentity identity,
        RebirthStationGridAdmission admission,RebirthStationTerminalIntent intent,
        RebirthStationPublicationRecord queued,RebirthStationCompletionPublication completed)
    {
        if(!serverAuthority||identity==null||admission==null||intent==null||queued==null||completed==null||
            !RebirthStationCompletionPublication.TryRead(completed.Write(),admission,intent,queued,out _))return false;
        string path=GetPath(identity.StorageKey);if(string.IsNullOrEmpty(path))return false;
        lock(GetWriteLock(identity.StorageKey))
        {
            try
            {
                if(!serverAuthority||!TryLoadValidatedRecord(path,identity,out var saved,out var migrated,out _,out _)||migrated||
                    saved?.Progression==null||!RebirthSurvivorRequestScope.Matches(admission.CreationId,saved.Origin?.CreationId)||
                    !saved.Progression.StationPreparations.TryGetValue(admission.JobId,out var a)||a==null||
                    !saved.Progression.StationTerminalIntents.TryGetValue(admission.JobId,out var i)||i==null||
                    !saved.Progression.StationPublications.TryGetValue(admission.JobId,out var q)||q==null||
                    !saved.Progression.StationCompletionPublications.TryGetValue(admission.JobId,out var c)||c==null||
                    !serverAuthority||path!=GetPath(identity.StorageKey))return false;
                return XNode.DeepEquals(a.Write(),admission.Write())&&XNode.DeepEquals(i.Write(),intent.Write())&&
                    XNode.DeepEquals(q.Write(),queued.Write())&&XNode.DeepEquals(c.Write(),completed.Write());
            }
            catch{return false;}
        }
    }
    internal static bool HasSavedStationCompletionExpectationProjection(RebirthStablePlayerIdentity identity,
        RebirthStationGridAdmission admission,RebirthStationTerminalIntent intent,
        RebirthStationPublicationRecord queued,RebirthStationCompletionPublication completed,RebirthStationCompletionExpectationProjection projection)
    {
        if(!serverAuthority||identity==null||admission==null||intent==null||queued==null||completed==null||projection==null||
            !RebirthStationCompletionPublication.TryRead(completed.Write(),admission,intent,queued,out _)||
            !RebirthStationCompletionExpectationProjection.TryRead(projection.Write(),admission,intent,queued,completed,XUiM_Recipes.GetRecipes(),out _))return false;
        string path=GetPath(identity.StorageKey);if(string.IsNullOrEmpty(path))return false;
        lock(GetWriteLock(identity.StorageKey))
        {
            try
            {
                if(!serverAuthority||!TryLoadValidatedRecord(path,identity,out var saved,out var migrated,out _,out _)||migrated||
                    saved?.Progression==null||!RebirthSurvivorRequestScope.Matches(admission.CreationId,saved.Origin?.CreationId)||
                    !saved.Progression.StationPreparations.TryGetValue(admission.JobId,out var a)||a==null||
                    !saved.Progression.StationTerminalIntents.TryGetValue(admission.JobId,out var i)||i==null||
                    !saved.Progression.StationPublications.TryGetValue(admission.JobId,out var q)||q==null||
                    !saved.Progression.StationCompletionPublications.TryGetValue(admission.JobId,out var c)||c==null||
                    !saved.Progression.StationCompletionExpectationProjections.TryGetValue(admission.JobId,out var p)||p==null||
                    !serverAuthority||path!=GetPath(identity.StorageKey))return false;
                return XNode.DeepEquals(a.Write(),admission.Write())&&XNode.DeepEquals(i.Write(),intent.Write())&&
                    XNode.DeepEquals(q.Write(),queued.Write())&&XNode.DeepEquals(c.Write(),completed.Write())&&XNode.DeepEquals(p.Write(),projection.Write())&&
                    serverAuthority&&path==GetPath(identity.StorageKey);
            }
            catch{return false;}
        }
    }
    // Current final-file intent witness only; does not authorize a native terminal effect.
    internal static bool HasSavedStationTerminalIntent(RebirthStablePlayerIdentity identity,
        RebirthStationGridAdmission admission,RebirthStationTerminalIntent intent)
    {
        if(!serverAuthority||identity==null||admission==null||intent==null||intent.JobId!=admission.JobId||
            !RebirthStationTerminalIntent.TryRead(intent.Write(),admission,out _))return false;
        string path=GetPath(identity.StorageKey);if(string.IsNullOrEmpty(path))return false;
        lock(GetWriteLock(identity.StorageKey))
        {
            try
            {
                if(!TryLoadValidatedRecord(path,identity,out var saved,out var migrated,out _,out _)||migrated||
                    saved?.Progression==null||!RebirthSurvivorRequestScope.Matches(admission.CreationId,saved.Origin?.CreationId)||
                    !saved.Progression.StationPreparations.TryGetValue(admission.JobId,out var stored)||stored==null||
                    !saved.Progression.StationTerminalIntents.TryGetValue(admission.JobId,out var receipt)||receipt==null)return false;
                return XNode.DeepEquals(stored.Write(),admission.Write())&&XNode.DeepEquals(receipt.Write(),intent.Write());
            }
            catch{return false;}
        }
    }
    // Same final file must contain exact admission, cancellation choice and refund obligation.
    internal static bool HasSavedStationCancellationRefund(RebirthStablePlayerIdentity identity,
        RebirthStationGridAdmission admission,RebirthStationTerminalIntent intent,RebirthStationCancellationRefund refund)
    {
        if(!serverAuthority||identity==null||admission==null||intent==null||intent.JobId!=admission.JobId||intent.IsCompletion||refund==null||
            !RebirthStationTerminalIntent.TryRead(intent.Write(),admission,out _)||
            !RebirthStationCancellationRefund.TryReadStored(refund.Write(),admission,intent,out _))return false;
        string path=GetPath(identity.StorageKey);if(string.IsNullOrEmpty(path))return false;
        lock(GetWriteLock(identity.StorageKey))
        {
            try
            {
                if(!TryLoadValidatedRecord(path,identity,out var saved,out var migrated,out _,out _)||migrated||
                    saved?.Progression==null||!RebirthSurvivorRequestScope.Matches(admission.CreationId,saved.Origin?.CreationId)||
                    !saved.Progression.StationPreparations.TryGetValue(admission.JobId,out var stored)||stored==null||
                    !saved.Progression.StationTerminalIntents.TryGetValue(admission.JobId,out var receipt)||receipt==null||
                    !saved.Progression.StationCancellationRefunds.TryGetValue(admission.JobId,out var storedRefund)||storedRefund==null)return false;
                return XNode.DeepEquals(stored.Write(),admission.Write())&&XNode.DeepEquals(receipt.Write(),intent.Write())&&XNode.DeepEquals(storedRefund.Write(),refund.Write());
            }
            catch{return false;}
        }
    }
    // Exact attempted-phase witness only; never grants replay after an uncertain native effect.
    internal static bool HasSavedStationCancellationAttempt(RebirthStablePlayerIdentity identity,
        RebirthStationGridAdmission admission,RebirthStationTerminalIntent intent,RebirthStationCancellationRefund refund,RebirthStationCancellationAttempt attempt)
    {
        if(!serverAuthority||identity==null||admission==null||intent==null||intent.JobId!=admission.JobId||intent.IsCompletion||refund==null||attempt==null||
            !RebirthStationTerminalIntent.TryRead(intent.Write(),admission,out _)||
            !RebirthStationCancellationRefund.TryReadStored(refund.Write(),admission,intent,out _)||
            !RebirthStationCancellationAttempt.TryRead(attempt.Write(),admission,intent,refund,out _))return false;
        string path=GetPath(identity.StorageKey);if(string.IsNullOrEmpty(path))return false;
        lock(GetWriteLock(identity.StorageKey))
        {
            try
            {
                if(!TryLoadValidatedRecord(path,identity,out var saved,out var migrated,out _,out _)||migrated||
                    saved?.Progression==null||!RebirthSurvivorRequestScope.Matches(admission.CreationId,saved.Origin?.CreationId)||
                    !saved.Progression.StationPreparations.TryGetValue(admission.JobId,out var stored)||stored==null||
                    !saved.Progression.StationTerminalIntents.TryGetValue(admission.JobId,out var receipt)||receipt==null||
                    !saved.Progression.StationCancellationRefunds.TryGetValue(admission.JobId,out var storedRefund)||storedRefund==null||
                    !saved.Progression.StationCancellationAttempts.TryGetValue(admission.JobId,out var storedAttempt)||storedAttempt==null)return false;
                return XNode.DeepEquals(stored.Write(),admission.Write())&&XNode.DeepEquals(receipt.Write(),intent.Write())&&XNode.DeepEquals(storedRefund.Write(),refund.Write())&&XNode.DeepEquals(storedAttempt.Write(),attempt.Write());
            }
            catch{return false;}
        }
    }
    // Exact final character checkpoint only; native region revalidation remains separate.
    internal static bool HasSavedStationCancellationPublication(RebirthStablePlayerIdentity identity,
        RebirthStationGridAdmission admission,RebirthStationTerminalIntent intent,RebirthStationCancellationRefund refund,RebirthStationCancellationAttempt attempt,RebirthStationCancellationPublication publication)
    {
        if(!serverAuthority||identity==null||admission==null||intent==null||intent.JobId!=admission.JobId||intent.IsCompletion||refund==null||attempt==null||
            !RebirthStationTerminalIntent.TryRead(intent.Write(),admission,out _)||
            !RebirthStationCancellationRefund.TryReadStored(refund.Write(),admission,intent,out _)||
            !RebirthStationCancellationAttempt.TryRead(attempt.Write(),admission,intent,refund,out _)||publication==null||
            !RebirthStationCancellationPublication.TryRead(publication.Write(),admission,intent,refund,attempt,out _))return false;
        string path=GetPath(identity.StorageKey);if(string.IsNullOrEmpty(path))return false;
        lock(GetWriteLock(identity.StorageKey))
        {
            try
            {
                if(!TryLoadValidatedRecord(path,identity,out var saved,out var migrated,out _,out _)||migrated||
                    saved?.Progression==null||!RebirthSurvivorRequestScope.Matches(admission.CreationId,saved.Origin?.CreationId)||
                    !saved.Progression.StationPreparations.TryGetValue(admission.JobId,out var stored)||stored==null||
                    !saved.Progression.StationTerminalIntents.TryGetValue(admission.JobId,out var receipt)||receipt==null||
                    !saved.Progression.StationCancellationRefunds.TryGetValue(admission.JobId,out var storedRefund)||storedRefund==null||
                    !saved.Progression.StationCancellationAttempts.TryGetValue(admission.JobId,out var storedAttempt)||storedAttempt==null||
                    !saved.Progression.StationCancellationPublications.TryGetValue(admission.JobId,out var storedPublication)||storedPublication==null)return false;
                return XNode.DeepEquals(stored.Write(),admission.Write())&&XNode.DeepEquals(receipt.Write(),intent.Write())&&XNode.DeepEquals(storedRefund.Write(),refund.Write())&&XNode.DeepEquals(storedAttempt.Write(),attempt.Write())&&XNode.DeepEquals(storedPublication.Write(),publication.Write());
            }
            catch{return false;}
        }
    }
    // Exact owner-bound delivery checkpoint; not native inventory application or receipt.
    internal static bool HasSavedStationRefundDelivery(RebirthStablePlayerIdentity identity,
        RebirthStationGridAdmission admission,RebirthStationTerminalIntent intent,RebirthStationCancellationRefund refund,RebirthStationCancellationAttempt attempt,RebirthStationCancellationPublication publication,RebirthStationRefundDelivery delivery)
    {
        if(!serverAuthority||identity==null||admission==null||intent==null||intent.JobId!=admission.JobId||intent.IsCompletion||refund==null||attempt==null||
            !RebirthStationTerminalIntent.TryRead(intent.Write(),admission,out _)||
            !RebirthStationCancellationRefund.TryReadStored(refund.Write(),admission,intent,out _)||
            !RebirthStationCancellationAttempt.TryRead(attempt.Write(),admission,intent,refund,out _)||publication==null||
            !RebirthStationCancellationPublication.TryRead(publication.Write(),admission,intent,refund,attempt,out _)||delivery==null||
            !RebirthStationRefundDelivery.TryRead(delivery.Write(),identity.StorageKey,admission,intent,refund,attempt,publication,out _))return false;
        string path=GetPath(identity.StorageKey);if(string.IsNullOrEmpty(path))return false;
        lock(GetWriteLock(identity.StorageKey))
        {
            try
            {
                if(!TryLoadValidatedRecord(path,identity,out var saved,out var migrated,out _,out _)||migrated||
                    saved?.Progression==null||!RebirthSurvivorRequestScope.Matches(admission.CreationId,saved.Origin?.CreationId)||
                    !saved.Progression.StationPreparations.TryGetValue(admission.JobId,out var stored)||stored==null||
                    !saved.Progression.StationTerminalIntents.TryGetValue(admission.JobId,out var receipt)||receipt==null||
                    !saved.Progression.StationCancellationRefunds.TryGetValue(admission.JobId,out var storedRefund)||storedRefund==null||
                    !saved.Progression.StationCancellationAttempts.TryGetValue(admission.JobId,out var storedAttempt)||storedAttempt==null||
                    !saved.Progression.StationCancellationPublications.TryGetValue(admission.JobId,out var storedPublication)||storedPublication==null||
                    !saved.Progression.StationRefundDeliveries.TryGetValue(admission.JobId,out var storedDelivery)||storedDelivery==null)return false;
                return XNode.DeepEquals(stored.Write(),admission.Write())&&XNode.DeepEquals(receipt.Write(),intent.Write())&&XNode.DeepEquals(storedRefund.Write(),refund.Write())&&XNode.DeepEquals(storedAttempt.Write(),attempt.Write())&&XNode.DeepEquals(storedPublication.Write(),publication.Write())&&XNode.DeepEquals(storedDelivery.Write(),delivery.Write());
            }
            catch{return false;}
        }
    }
    // Current final-file evidence; never infer durable completion from cache or backup.
    internal static bool HasSavedStationRefundArchive(RebirthStablePlayerIdentity identity,RebirthStationRefundArchive archive)
    {
        if(!serverAuthority||identity==null||archive==null||
            !RebirthStationRefundArchive.TryRead(archive.Write(),identity.StorageKey,out var valid))return false;
        string path=GetPath(identity.StorageKey);if(string.IsNullOrEmpty(path))return false;
        lock(GetWriteLock(identity.StorageKey))
        {
            try
            {
                if(!TryLoadValidatedRecord(path,identity,out var saved,out var migrated,out _,out _)||migrated||
                    saved?.Progression==null||!RebirthSurvivorRequestScope.Matches(valid.CreationId,saved.Origin?.CreationId)||
                    !saved.Progression.StationRefundArchives.TryGetValue(valid.JobId,out var retained)||retained==null||
                    !XNode.DeepEquals(retained.Write(),valid.Write()))return false;
                var state=saved.Progression;var job=valid.JobId;
                return !state.StationPreparations.ContainsKey(job)&&!state.StationPublications.ContainsKey(job)&&
                    !state.StationTerminalIntents.ContainsKey(job)&&!state.StationCancellationRefunds.ContainsKey(job)&&
                    !state.StationCancellationAttempts.ContainsKey(job)&&!state.StationCancellationPublications.ContainsKey(job)&&
                    !state.StationRefundDeliveries.ContainsKey(job);
            }
            catch{return false;}
        }
    }
    // Current validated final file only. Neither cache nor backup can authorize
    // sending an inventory-changing offer or advancing its custody stage.
    internal static bool HasSavedGearTransfer(RebirthStablePlayerIdentity identity,
        RebirthGearTransferState expected, RebirthGearTransferPhase phase)
    {
        if (!serverAuthority || identity == null || expected == null) return false;
        string path = GetPath(identity.StorageKey);
        if (string.IsNullOrEmpty(path)) return false;
        lock (GetWriteLock(identity.StorageKey))
        {
            try
            {
                if (!TryLoadValidatedRecord(path, identity, out var saved, out var migrated, out _, out _) ||
                    migrated || saved == null || !saved.IsComplete) return false;
                return RebirthGearTransferSavedWitness.Matches(saved.Support,
                    saved.Origin?.CreationId, expected, phase);
            }
            catch { return false; }
        }
    }
    // Exact final canonical record proof. Save return/cache presence alone is insufficient.
    // Allows only the known refusal field to differ before its first/uncertain save.
    internal static bool HasSavedUnpreparedGearBase(RebirthStablePlayerIdentity identity,RebirthWorldCharacterRecord expected,string marker)
    {
        if(!serverAuthority||identity==null||expected?.Support==null||expected.Origin==null||!expected.IsComplete)return false;
        string path=GetPath(identity.StorageKey);if(string.IsNullOrEmpty(path))return false;
        lock(GetWriteLock(identity.StorageKey))
        {
            try
            {
                if(!TryGetCurrentCached(identity,out var current)||!ReferenceEquals(current,expected)||
                    !TryLoadValidatedRecord(path,identity,out var saved,out var migrated,out _,out _)||migrated||
                    saved?.Support==null||!saved.IsComplete||saved.Origin?.CreationId!=expected.Origin.CreationId)return false;
                foreach(var support in new[]{saved.Support,expected.Support})
                    if(support.PendingGearTransfer!=null||support.PendingLibraryTransfer!=null||support.PendingMusicTransfer!=null||
                        support.PendingGearPreparationRefusal!=null&&!support.PendingGearPreparationRefusal.MatchesOriginal(marker))return false;
                var before=saved.Support.Clone();var after=expected.Support.Clone();
                before.PendingGearPreparationRefusal=null;after.PendingGearPreparationRefusal=null;
                return XNode.DeepEquals(SerializeSupport(before,identity.StorageKey,expected.Origin.CreationId),
                    SerializeSupport(after,identity.StorageKey,expected.Origin.CreationId));
            }
            catch{return false;}
        }
    }
    internal static bool HasSavedGearPreparationRefusal(RebirthStablePlayerIdentity identity,RebirthGearPreparationRefusal expected)
    {
        if(!serverAuthority||identity==null||expected==null)return false;
        string path=GetPath(identity.StorageKey);if(string.IsNullOrEmpty(path))return false;
        lock(GetWriteLock(identity.StorageKey))
        {
            try
            {
                if(!TryLoadValidatedRecord(path,identity,out var saved,out var migrated,out _,out _)||migrated||
                    saved==null||!saved.IsComplete||saved.Support?.PendingGearPreparationRefusal==null||
                    !RebirthSurvivorRequestScope.Matches(expected.CreationId,saved.Origin?.CreationId)||
                    saved.Support.GearRevision!=expected.ObservedRevision||
                    !XNode.DeepEquals(saved.Support.PendingGearPreparationRefusal.Write(),expected.Write())||
                    !TryGetCurrentCached(identity,out var current)||current==null||!current.IsComplete||
                    !RebirthSurvivorRequestScope.Matches(expected.CreationId,current.Origin?.CreationId)||
                    !ReferenceEquals(current.Support?.PendingGearPreparationRefusal,expected)||
                    current.Support.GearRevision!=expected.ObservedRevision)return false;
                return XNode.DeepEquals(SerializeSupport(saved.Support,identity.StorageKey,expected.CreationId),
                    SerializeSupport(current.Support,identity.StorageKey,expected.CreationId));
            }
            catch{return false;}
        }
    }
    // Retry base permits ONLY the known pending-to-retired refusal metadata transition.
    internal static bool HasSavedGearRefusalRetirementBase(RebirthStablePlayerIdentity identity,RebirthWorldCharacterRecord expected,RebirthGearPreparationRefusal refusal)
    {
        if(!serverAuthority||identity==null||expected==null||refusal==null)return false;
        string path=GetPath(identity.StorageKey);if(string.IsNullOrEmpty(path))return false;
        lock(GetWriteLock(identity.StorageKey))
        {
            try
            {
                if(!TryGetCurrentCached(identity,out var current)||!ReferenceEquals(current,expected)||!current.IsComplete||
                    !TryLoadValidatedRecord(path,identity,out var saved,out var migrated,out _,out _)||migrated||saved==null||!saved.IsComplete||
                    !RebirthSurvivorRequestScope.Matches(refusal.CreationId,saved.Origin?.CreationId)||
                    !RebirthSurvivorRequestScope.Matches(refusal.CreationId,current.Origin?.CreationId))return false;
                foreach(var support in new[]{saved.Support,current.Support})
                {
                    if(support==null||support.GearRevision!=refusal.ObservedRevision||support.PendingGearTransfer!=null||
                        support.PendingLibraryTransfer!=null||support.PendingMusicTransfer!=null)return false;
                    var original=support.PendingGearPreparationRefusal??support.LastGearPreparationRefusal;
                    if(original==null||!XNode.DeepEquals(original.Write(),refusal.Write()))return false;
                }
                var before=saved.Support.Clone();var after=current.Support.Clone();
                before.PendingGearPreparationRefusal=null;before.LastGearPreparationRefusal=null;
                after.PendingGearPreparationRefusal=null;after.LastGearPreparationRefusal=null;
                return XNode.DeepEquals(SerializeSupport(before,identity.StorageKey,refusal.CreationId),
                    SerializeSupport(after,identity.StorageKey,refusal.CreationId));
            }
            catch{return false;}
        }
    }
    internal static bool HasSavedGearPreparationRefusalRetirement(RebirthStablePlayerIdentity identity,RebirthGearPreparationRefusal expected)
    {
        if(!serverAuthority||identity==null||expected==null)return false;
        string path=GetPath(identity.StorageKey);if(string.IsNullOrEmpty(path))return false;
        lock(GetWriteLock(identity.StorageKey))
        {
            try
            {
                if(!TryLoadValidatedRecord(path,identity,out var saved,out var migrated,out _,out _)||migrated||
                    saved==null||!saved.IsComplete||saved.Support?.LastGearPreparationRefusal==null||
                    !RebirthSurvivorRequestScope.Matches(expected.CreationId,saved.Origin?.CreationId)||
                    saved.Support.GearRevision<expected.ObservedRevision||
                    !XNode.DeepEquals(saved.Support.LastGearPreparationRefusal.Write(),expected.Write())||
                    !TryGetCurrentCached(identity,out var current)||current==null||!current.IsComplete||
                    !RebirthSurvivorRequestScope.Matches(expected.CreationId,current.Origin?.CreationId)||
                    !ReferenceEquals(current.Support?.LastGearPreparationRefusal,expected)||
                    current.Support.GearRevision<expected.ObservedRevision||
                    current.Support.PendingGearPreparationRefusal?.TransactionId==expected.TransactionId)return false;
                return XNode.DeepEquals(SerializeSupport(saved.Support,identity.StorageKey,expected.CreationId),
                    SerializeSupport(current.Support,identity.StorageKey,expected.CreationId));
            }
            catch{return false;}
        }
    }
    internal static bool HasSavedGearSettlement(RebirthStablePlayerIdentity identity,RebirthGearSettlement expected)
    {
        if(!serverAuthority||identity==null||expected==null)return false;
        string path=GetPath(identity.StorageKey);if(string.IsNullOrEmpty(path))return false;
        lock(GetWriteLock(identity.StorageKey))
        {
            try
            {
                if(!TryLoadValidatedRecord(path,identity,out var saved,out var migrated,out _,out _)||migrated||
                    saved==null||!saved.IsComplete||saved.Support?.LastGearSettlement==null||
                    !RebirthSurvivorRequestScope.Matches(expected.CreationId,saved.Origin?.CreationId))return false;
                return saved.Support.PendingGearTransfer==null&&saved.Support.GearRevision==expected.GearRevision&&
                    XNode.DeepEquals(saved.Support.LastGearSettlement.Write(),expected.Write())&&
                    TryGetCurrentCached(identity,out var current)&&
                    (current.Support?.LastGearSettlementOriginal==null?
                     saved.Support.LastGearSettlementOriginal==null:
                     saved.Support.LastGearSettlementOriginal!=null&&
                     XNode.DeepEquals(saved.Support.LastGearSettlementOriginal.ToXml(),current.Support.LastGearSettlementOriginal.ToXml()));
            }
            catch{return false;}
        }
    }
    internal static bool HasSavedSoloOriginalTasks(RebirthStablePlayerIdentity identity,RebirthTheorySoloState expected)
    {
        if(!serverAuthority||identity==null||expected?.OriginalTasks==null)return false;
        string path=GetPath(identity.StorageKey);if(string.IsNullOrEmpty(path))return false;
        lock(GetWriteLock(identity.StorageKey))
        {
            try{return TryLoadValidatedRecord(path,identity,out var saved,out var migrated,out _,out _)&&!migrated&&saved?.Origin?.CreationId==expected.CreationId&&saved.Progression?.SoloTheory!=null&&XNode.DeepEquals(RebirthTheorySoloPersistence.Write(saved.Progression.SoloTheory),RebirthTheorySoloPersistence.Write(expected));}
            catch{return false;}
        }
    }
    internal static bool HasSavedSoloTeachingOriginal(RebirthStablePlayerIdentity identity,RebirthTheorySoloState expected)
    {
        if(!serverAuthority||identity==null||expected?.TeachingOriginal==null||expected.TeachingOriginal.Through<1)return false;
        string path=GetPath(identity.StorageKey);if(string.IsNullOrEmpty(path))return false;
        lock(GetWriteLock(identity.StorageKey))
        {
            try{return TryLoadValidatedRecord(path,identity,out var saved,out var migrated,out _,out _)&&!migrated&&saved?.Origin?.CreationId==expected.CreationId&&saved.Progression?.SoloTheory!=null&&XNode.DeepEquals(RebirthTheorySoloPersistence.Write(saved.Progression.SoloTheory),RebirthTheorySoloPersistence.Write(expected));}
            catch{return false;}
        }
    }
    internal static bool HasSavedSoloLockpickOriginal(RebirthStablePlayerIdentity identity,RebirthTheorySoloState expected)
    {
        if(!serverAuthority||identity==null||expected?.LockpickOriginal==null||expected.LockpickOriginal.Issued<1)return false;
        string path=GetPath(identity.StorageKey);if(string.IsNullOrEmpty(path))return false;
        lock(GetWriteLock(identity.StorageKey))
        {
            try{return TryLoadValidatedRecord(path,identity,out var saved,out var migrated,out _,out _)&&!migrated&&saved?.Origin?.CreationId==expected.CreationId&&saved.Progression?.SoloTheory!=null&&XNode.DeepEquals(RebirthTheorySoloPersistence.Write(saved.Progression.SoloTheory),RebirthTheorySoloPersistence.Write(expected));}
            catch{return false;}
        }
    }
    internal static bool HasSavedSoloCombatOriginal(RebirthStablePlayerIdentity identity,RebirthTheorySoloState expected)
    {
        if(!serverAuthority||identity==null||expected?.CombatOriginal==null||expected.CombatOriginal.Issued<1)return false;
        string path=GetPath(identity.StorageKey);if(string.IsNullOrEmpty(path))return false;
        lock(GetWriteLock(identity.StorageKey))
        {
            try{return TryLoadValidatedRecord(path,identity,out var saved,out var migrated,out _,out _)&&!migrated&&saved?.Origin?.CreationId==expected.CreationId&&saved.Progression?.SoloTheory!=null&&XNode.DeepEquals(RebirthTheorySoloPersistence.Write(saved.Progression.SoloTheory),RebirthTheorySoloPersistence.Write(expected));}
            catch{return false;}
        }
    }
    internal static bool HasSavedSoloCancellationHold(RebirthStablePlayerIdentity identity,RebirthTheorySoloState expected)
    {
        if(!serverAuthority||identity==null||expected==null||string.IsNullOrEmpty(expected.CancelPendingId)||expected.Session!=null)return false;
        string path=GetPath(identity.StorageKey);if(string.IsNullOrEmpty(path))return false;
        lock(GetWriteLock(identity.StorageKey))
        {
            try{return TryLoadValidatedRecord(path,identity,out var saved,out var migrated,out _,out _)&&!migrated&&saved?.Origin?.CreationId==expected.CreationId&&saved.Progression?.PendingTheoryStudy==null&&XNode.DeepEquals(RebirthTheorySoloPersistence.Write(saved.Progression.SoloTheory),RebirthTheorySoloPersistence.Write(expected));}
            catch{return false;}
        }
    }
    internal static bool HasSavedSoloCancellation(RebirthStablePlayerIdentity identity,string creation,string session)
    {
        if(!serverAuthority||identity==null)return false;string path=GetPath(identity.StorageKey);if(string.IsNullOrEmpty(path))return false;
        lock(GetWriteLock(identity.StorageKey))
        {
            try{return TryLoadValidatedRecord(path,identity,out var saved,out var migrated,out _,out _)&&!migrated&&saved?.Origin?.CreationId==creation&&saved.Progression?.SoloTheory?.LastCancelledId==session&&saved.Progression.SoloTheory.Session==null&&string.IsNullOrEmpty(saved.Progression.SoloTheory.CancelPendingId)&&saved.Progression.PendingTheoryStudy==null;}
            catch{return false;}
        }
    }
    internal static bool HasSavedSoloSession(RebirthStablePlayerIdentity identity,RebirthTheorySoloState expected)
    {
        if(!serverAuthority||identity==null||expected?.Session==null)return false;
        string path=GetPath(identity.StorageKey);if(string.IsNullOrEmpty(path))return false;
        lock(GetWriteLock(identity.StorageKey))
        {
            try{return TryLoadValidatedRecord(path,identity,out var saved,out var migrated,out _,out _)&&!migrated&&saved?.Progression?.SoloTheory!=null&&saved.Origin.CreationId==expected.CreationId&&XNode.DeepEquals(RebirthTheorySoloPersistence.Write(saved.Progression.SoloTheory),RebirthTheorySoloPersistence.Write(expected));}
            catch{return false;}
        }
    }
    internal static bool HasSavedTheoryStudy(RebirthStablePlayerIdentity identity,RebirthTheoryStudyOutcome outcome,bool applied,bool settled=false)
    {
        if(!serverAuthority||identity==null||outcome==null)return false;
        string path=GetPath(identity.StorageKey);if(string.IsNullOrEmpty(path))return false;
        lock(GetWriteLock(identity.StorageKey))
        {
            try
            {
                if(!TryLoadValidatedRecord(path,identity,out var saved,out var migrated,out _,out _)||migrated||
                    saved?.Progression==null||!RebirthSurvivorRequestScope.Matches(outcome.CreationId,saved.Origin?.CreationId)||
                    (settled?saved.Progression.PendingTheoryStudy!=null:saved.Progression.PendingTheoryStudy==null||!XNode.DeepEquals(saved.Progression.PendingTheoryStudy.Write(),outcome.Write())))return false;
                if(!RebirthTheorySoloService.ValidateOutcome(saved.Progression.SoloTheory,outcome,applied,settled))return false;
                if(!applied)return !settled;
                return saved.Progression.SkillAwardReceipts.Contains(outcome.Receipt)&&
                    saved.Progression.SkillKnowledge.TryGetValue(outcome.SkillId,out var theory)&&theory!=null&&theory.Value>=outcome.Target&&
                    saved.Progression.TeachingHistory.TryGetValue(outcome.HistoryKey,out var history)&&history!=null&&
                    history.CompletionCount>=outcome.HistoryCount&&history.LastCompletedUtcTicks>=outcome.CompletedTicks;
            }
            catch{return false;}
        }
    }
    public static bool Exists(RebirthStablePlayerIdentity identity)
    {
        RebirthWorldCharacterRecord ignored;
        return TryGet(identity, out ignored);
    }

    // No loading or disk I/O: verifies that a session binding still names the authoritative cached object.
    internal static bool TryGetCurrentCached(RebirthStablePlayerIdentity identity,out RebirthWorldCharacterRecord record)
    {
        record=null;if(!serverAuthority||identity==null||string.IsNullOrEmpty(identity.StorageKey))return false;
        lock(Sync)return Cache.TryGetValue(identity.StorageKey,out record)&&record!=null&&record.IsComplete&&record.Progression!=null;
    }
    internal static bool IsCurrentCachedRecord(RebirthWorldCharacterRecord record)
    {
        if(!serverAuthority||record==null||!record.IsComplete||record.Progression==null||string.IsNullOrEmpty(record.StablePlayerKey))return false;
        lock(Sync)return Cache.TryGetValue(record.StablePlayerKey,out var current)&&ReferenceEquals(current,record);
    }
    public static bool TryGet(RebirthStablePlayerIdentity identity, out RebirthWorldCharacterRecord record)
    {
        record = null;
        if (!serverAuthority || identity == null || string.IsNullOrEmpty(identity.StorageKey)) return false;
        lock (Sync)
        {
            if (Cache.TryGetValue(identity.StorageKey, out var cached) && cached != null)
            { record = cached; return cached.IsComplete; }
        }
        var gate = (RepositoryWriteGate)GetWriteLock(identity.StorageKey);
        lock (gate)
        {
            if (!TryEnterRepositoryOperation(gate)) return false;
            object epoch, generation;
            lock (Sync) { epoch = RepositoryEpoch; generation = gate.Generation; }
            string path = gate.Path;
            try { return TryGetUnderGate(identity, gate, epoch, generation, path, out record); }
            finally { gate.Active = false; }
        }
    }

    private static bool TryGetUnderGate(RebirthStablePlayerIdentity identity, RepositoryWriteGate gate,
        object epoch, object generation, string capturedPath, out RebirthWorldCharacterRecord record)
    {
        record = null;
        if (!serverAuthority || identity == null || string.IsNullOrEmpty(identity.StorageKey))
            return false;

        lock (Sync)
        {
            RebirthWorldCharacterRecord cached;
            if (Cache.TryGetValue(identity.StorageKey, out cached) && cached != null)
            {
                record = cached;
                return cached.IsComplete;
            }
            long retryAfter;
            if (RetryAfterUtcTicks.TryGetValue(identity.StorageKey, out retryAfter) && DateTime.UtcNow.Ticks < retryAfter) return false;
        }

        string path = capturedPath;
        if (string.IsNullOrEmpty(path))
            return false;
        if (!File.Exists(path) && !File.Exists(path + ".bak"))
            return false;

        RebirthWorldCharacterRecord loaded = null;
        bool migrated = false;
        string finalError = "missing";
        bool loadedFinal = false;
        bool finalHasPendingCustody = false;
        if (File.Exists(path))
            loadedFinal = TryLoadValidatedRecord(path, identity, out loaded, out migrated, out finalError, out finalHasPendingCustody);
        bool usedBackup = false;
        string recoveryMessage = string.Empty;
        if (!loadedFinal)
        {
            // A valid older backup cannot establish whether the owner already
            // applied this transfer. Never overwrite observable pending custody.
            if (finalHasPendingCustody)
            {
                AddIssue(identity.StorageKey, path, "final failed (" + finalError
                    + "); pending item custody requires reconciliation; automatic backup replacement refused", false);
                lock (Sync) RetryAfterUtcTicks[identity.StorageKey] = DateTime.UtcNow.AddSeconds(5).Ticks;
                return false;
            }
            string backupPath = path + ".bak";
            RebirthWorldCharacterRecord backupRecord = null;
            bool backupMigrated = false;
            string backupError = "missing";
            bool loadedBackup = false;
            bool backupHasPendingCustody;
            if (File.Exists(backupPath))
                loadedBackup = TryLoadValidatedRecord(backupPath, identity, out backupRecord, out backupMigrated, out backupError, out backupHasPendingCustody);
            if (loadedBackup)
            {
                loaded = backupRecord;
                migrated = backupMigrated;
                usedBackup = true;
                recoveryMessage = "final failed (" + (finalError ?? "missing") + "); recovered from validated backup";
            }
            else
            {
                AddIssue(identity.StorageKey, path, "final failed (" + (finalError ?? "missing") + "); backup failed (" + (backupError ?? "missing") + ")", false);
                lock (Sync) RetryAfterUtcTicks[identity.StorageKey] = DateTime.UtcNow.AddSeconds(5).Ticks;
                return false;
            }
        }

        bool repairPersisted = true;
        if (migrated || usedBackup)
        {
            string rewriteError;
            if (!IsRepositoryContextCurrent(epoch, gate, generation, identity.StorageKey, capturedPath)) return false;
            var repairDocument = Serialize(loaded);
            if (!IsRepositoryContextCurrent(epoch, gate, generation, identity.StorageKey, capturedPath)) return false;
            lock (Sync) { gate.Generation = generation = new object(); }
            repairPersisted = RebirthAtomicXmlFile.TryWrite(path, repairDocument, out rewriteError);
            if (!repairPersisted) AddIssue(identity.StorageKey, path, "loaded but could not persist repaired world record; retained dirty for retry: " + rewriteError, usedBackup);
        }
        if (repairPersisted) loaded.MarkPersisted();
        lock (Sync)
        {
            RetryAfterUtcTicks.Remove(identity.StorageKey);
            if (!IsRepositoryContextCurrentLocked(epoch, gate, generation, identity.StorageKey, capturedPath)) return false;
            Cache[identity.StorageKey] = loaded;
        }
        if (usedBackup)
            AddIssue(identity.StorageKey, path, recoveryMessage, true);
        record = loaded;
        return true;
    }

    private static bool HasPendingItemCustody(XDocument document)
    {
        if (document == null || document.Root == null) return false;
        foreach (XElement element in document.Root.Descendants())
            if (element.Name.LocalName == "pendingTransfer" || element.Name.LocalName == "pendingGearTransfer" || element.Name.LocalName == "stationAdmission" || element.Name.LocalName == "stationPublication" || element.Name.LocalName == "remoteResourceRefunds")
                return true;
        return false;
    }

    private static bool TryLoadValidatedRecord(string candidatePath, RebirthStablePlayerIdentity identity,
        out RebirthWorldCharacterRecord record, out bool migrated, out string error, out bool hasPendingCustody)
    {
        record = null;
        migrated = false;
        error = string.Empty;
        hasPendingCustody = false;
        XDocument doc;
        if (!RebirthAtomicXmlFile.TryLoad(candidatePath, out doc, out error))
            return false;
        hasPendingCustody = HasPendingItemCustody(doc);
        string migrationError;
        if (!RebirthWorldCharacterMigrationRegistry.TryMigrateToCurrent(doc, out migrated, out migrationError))
        {
            error = migrationError;
            return false;
        }
        string parseError;
        if (!TryDeserialize(doc, out record, out parseError))
        {
            error = parseError;
            record = null;
            return false;
        }
        if (!string.Equals(record.StablePlayerKey, identity.StorageKey, StringComparison.Ordinal) ||
            !string.Equals(record.StablePlayerId, identity.CanonicalId, StringComparison.Ordinal))
        {
            error = "stable player identity inside record does not match server-derived identity";
            record = null;
            return false;
        }
        if (!record.IsComplete)
        {
            error = "record does not contain a complete committed origin";
            record = null;
            return false;
        }
        return true;
    }

    /// <summary>
    /// Non-mutating preflight used by creation before it resets/persists fresh metabolism state.
    /// It treats any final, backup, or cached character record as occupied, including corrupt files,
    /// so a failed/corrupt existing origin can never cause the player's metabolism to be reset.
    /// </summary>
    public static bool CanCommitNew(RebirthStablePlayerIdentity identity, out string error)
    {
        error = string.Empty;
        if (!serverAuthority) { error = "world-character repository is not server-authoritative"; return false; }
        if (identity == null || string.IsNullOrEmpty(identity.StorageKey)) { error = "stable player identity is missing"; return false; }

        lock (Sync)
        {
            if (Cache.ContainsKey(identity.StorageKey))
            {
                error = "world character already exists in repository cache";
                return false;
            }
        }

        string path = GetPath(identity.StorageKey);
        if (string.IsNullOrEmpty(path))
        {
            error = "world-character storage root is unavailable";
            return false;
        }

        if (File.Exists(path) || File.Exists(path + ".bak"))
        {
            error = "world-character storage already exists; creation will not overwrite it";
            return false;
        }
        return true;
    }

    /// <summary>
    /// Repository primitive used by the authoritative creation transaction in Chunk 04/05.
    /// It refuses to overwrite any existing final or backup record, including unreadable ones.
    /// </summary>
    public static bool TryCommitNew(RebirthStablePlayerIdentity identity, RebirthWorldCharacterRecord candidate,
        out RebirthWorldCharacterRecord committed, out string error)
    {
        committed = null; error = string.Empty;
        if (identity == null) { error = "stable player identity is missing"; return false; }
        if (!ValidateCandidate(identity, candidate, out error)) return false;
        var gate = (RepositoryWriteGate)GetWriteLock(identity.StorageKey);
        lock (gate)
        {
            if (!TryEnterRepositoryOperation(gate)) { error = "repository write reentry refused"; return false; }
            try
            {
                if (!CanCommitNew(identity, out error)) return false;
                object epoch, generation;
                lock (Sync) { epoch = RepositoryEpoch; generation = gate.Generation; }
                string path = gate.Path;
                var snapshot = candidate.Clone();
                var document = Serialize(snapshot);
                if (!IsRepositoryContextCurrent(epoch, gate, generation, identity.StorageKey, path)) return false;
                lock (Sync) { gate.Generation = generation = new object(); }
                if (!RebirthAtomicXmlFile.TryWrite(path, document, out error)) return false;
                lock (Sync)
                {
                    if (!IsRepositoryContextCurrentLocked(epoch, gate, generation, identity.StorageKey, path)) return false;
                    snapshot.MarkPersisted();
                    RetryAfterUtcTicks.Remove(identity.StorageKey);
                    Cache[identity.StorageKey] = snapshot;
                }
                committed = snapshot; return true;
            }
            finally { gate.Active = false; }
        }
    }
    public static bool SaveIfDirty(RebirthStablePlayerIdentity identity, string reason)
    {
        if (!serverAuthority || identity == null) return false;
        RepositoryWriteSelection selected;
        lock (Sync)
        {
            if (!Cache.TryGetValue(identity.StorageKey, out var record) || record == null || !record.Dirty) return false;
            selected = CaptureWriteSelectionLocked(identity.StorageKey, record);
        }
        return SaveSnapshot(selected, reason);
    }

    public static int SaveAllDirty(string reason)
    {
        if (!serverAuthority) return 0;
        var dirty = new List<RepositoryWriteSelection>();
        lock (Sync)
        {
            foreach (var pair in Cache)
                if (pair.Value != null && pair.Value.Dirty)
                    dirty.Add(CaptureWriteSelectionLocked(pair.Key, pair.Value));
        }
        int saved = 0;
        foreach (var selected in dirty) if (SaveSnapshot(selected, reason)) saved++;
        return saved;
    }

    public static void MarkDirty(RebirthWorldCharacterRecord record, string reason)
    {
        if (record != null) record.Touch(reason);
    }

    private static bool SaveSnapshot(RepositoryWriteSelection selected, string reason)
    {
        if (selected == null) return false;
        lock (selected.Gate)
        {
            if (!TryEnterRepositoryOperation(selected.Gate)) return false;
            try
            {
                selected.Document = Serialize(selected.Snapshot);
                if (!IsSelectionCurrent(selected, true)) return false;
                if (!TryWriteSelected(selected, out var error))
                {
                    AddIssue(selected.Key, selected.Path, "save failed (" + (reason ?? "unspecified") + "): " + error, false);
                    return false;
                }
                if (!IsSelectionCurrent(selected, true)) return false;
                lock (Sync)
                {
                    if (!IsSelectionBindingCurrentLocked(selected)) return false;
                    selected.Original.MarkPersisted();
                }
                return true;
            }
            finally { selected.Gate.Active = false; }
        }
    }

    private static object GetWriteLock(string key)
    {
        lock (Sync) return GetRepositoryGateLocked(key);
    }
    private static bool ValidateCandidate(RebirthStablePlayerIdentity identity, RebirthWorldCharacterRecord record, out string error)
    {
        error = string.Empty;
        if (record == null) { error = "world-character candidate is null"; return false; }
        if (record.SchemaVersion != RebirthWorldCharacterRecord.CurrentSchemaVersion) { error = "world-character schema is not current"; return false; }
        if (!record.IsComplete) { error = "world-character origin is incomplete"; return false; }
        if (!string.Equals(record.StablePlayerId, identity.CanonicalId, StringComparison.Ordinal)) { error = "candidate canonical identity mismatch"; return false; }
        if (!string.Equals(record.StablePlayerKey, identity.StorageKey, StringComparison.Ordinal)) { error = "candidate storage key mismatch"; return false; }
        if (record.Origin == null || string.IsNullOrEmpty(record.Origin.DefinitionHash) || string.IsNullOrEmpty(record.Origin.DefinitionVersion)) { error = "origin definition identity is missing"; return false; }
        if (string.IsNullOrEmpty(record.Origin.CreationId)) { error = "origin creationId is missing"; return false; }
        return true;
    }

    private static string GetPath(string key)
    {
        string root = RootDirectory;
        return string.IsNullOrEmpty(root) ? string.Empty : Path.Combine(root, key + ".xml");
    }

    private static XDocument Serialize(RebirthWorldCharacterRecord record)
    {
        XElement root = new XElement("rebirthWorldCharacter",
            new XAttribute("schemaVersion", record.SchemaVersion),
            new XAttribute("stablePlayerId", record.StablePlayerId),
            new XAttribute("stablePlayerKey", record.StablePlayerKey),
            new XAttribute("revision", record.Revision),
            new XAttribute("createdAtUtc", record.CreatedAtUtc.ToString("O", CultureInfo.InvariantCulture)),
            new XAttribute("modifiedAtUtc", record.ModifiedAtUtc.ToString("O", CultureInfo.InvariantCulture)),
            new XAttribute("migrationSourceSchema", record.MigrationSourceSchema),
            new XAttribute("migrationTargetSchema", record.MigrationTargetSchema),
            new XAttribute("migrationPolicyId", record.MigrationPolicyId ?? string.Empty),
            new XAttribute("migrationApplied", record.MigrationApplied),
            new XAttribute("migrationOriginAudit", record.MigrationOriginAudit ?? string.Empty),
            new XAttribute("migrationProgressionAudit", record.MigrationProgressionAudit ?? string.Empty));
        root.Add(SerializeOrigin(record.Origin));
        if(!RebirthStationPreparationPersistence.MatchesOwner(record.Progression.StationPreparations,record.Origin.CreationId)) throw new InvalidDataException("Station preparation owner mismatch");
        if(record.Progression.StationRefundArchives.Values.Any(a=>a==null||!RebirthSurvivorRequestScope.Matches(a.CreationId,record.Origin.CreationId)))throw new InvalidDataException("Station refund archive character mismatch");
        if(!RebirthTheoryStudyPersistence.MatchesOwner(record.Progression.PendingTheoryStudy,record.Origin.CreationId)) throw new InvalidDataException("Theory study owner mismatch");
        if(!RebirthTheorySoloPersistence.MatchesOwner(record.Progression.SoloTheory,record.Origin.CreationId)) throw new InvalidDataException("Solo Theory owner mismatch");
        if(!RebirthStationRecipeDiscoveryPersistence.MatchesCreation(record.Progression.RecipeDiscoveries.Values,record.Origin.CreationId))throw new InvalidDataException("Recipe discovery character mismatch");
        var progressionNode=SerializeProgression(record.Progression,record.StablePlayerKey);
        progressionNode.Add(RebirthStationDiscoveryAdmissionPersistence.Write(record.Progression.StationDiscoveryAdmissions,record.StablePlayerKey,record.Origin.CreationId));
        root.Add(progressionNode);
        root.Add(SerializeCondition(record.Condition));
        root.Add(SerializeSupport(record.Support,record.StablePlayerKey,record.Origin.CreationId));
        root.Add(new XElement("externalState",
            new XElement("metabolism", new XAttribute("owner", "RebirthMetabolismStateRepository"), new XAttribute("duplicatedHere", false))));
        return new XDocument(new XDeclaration("1.0", "utf-8", null), root);
    }

    private static XElement SerializeOrigin(RebirthWorldOriginSnapshot origin)
    {
        XElement node = new XElement("origin",
            new XAttribute("creationId", origin.CreationId),
            new XAttribute("definitionHash", origin.DefinitionHash),
            new XAttribute("definitionVersion", origin.DefinitionVersion),
            new XAttribute("sourceProfileId", origin.SourceProfileId),
            new XAttribute("sourceProfileName", origin.SourceProfileName),
            new XAttribute("backgroundId", origin.BackgroundId),
            new XAttribute("dietId", origin.DietId),
            new XAttribute("remainingCreationPoints", origin.RemainingCreationPoints),
            new XAttribute("healthPotential", F(origin.HealthPotential)),
            new XAttribute("unencumberedSlotDelta", origin.UnencumberedSlotDelta),
            new XAttribute("committedAtUtc", origin.CommittedAtUtc.ToString("O", CultureInfo.InvariantCulture)));

        XElement traits = new XElement("traits");
        for (int i = 0; i < origin.TraitIds.Count; i++) traits.Add(new XElement("trait", new XAttribute("id", origin.TraitIds[i])));
        node.Add(traits);

        XElement attrs = new XElement("attributes");
        for (int i = 0; i < origin.Attributes.Count; i++)
        {
            RebirthWorldOriginAttribute a = origin.Attributes[i];
            attrs.Add(new XElement("attribute", new XAttribute("id", a.AttributeId), new XAttribute("current", F(a.Current)), new XAttribute("potential", F(a.Potential))));
        }
        node.Add(attrs);

        XElement skills = new XElement("skills");
        List<string> skillIds = new List<string>(origin.StartingSkills.Keys); skillIds.Sort(StringComparer.Ordinal);
        for (int i = 0; i < skillIds.Count; i++) skills.Add(new XElement("skill", new XAttribute("id", skillIds[i]), new XAttribute("value", F(origin.StartingSkills[skillIds[i]]))));
        node.Add(skills);

        XElement skillKnowledge = new XElement("skillKnowledge");
        List<string> skillKnowledgeIds = new List<string>(origin.StartingSkillKnowledge.Keys); skillKnowledgeIds.Sort(StringComparer.Ordinal);
        for (int i = 0; i < skillKnowledgeIds.Count; i++)
            skillKnowledge.Add(new XElement("skill", new XAttribute("id", skillKnowledgeIds[i]), new XAttribute("value", F(origin.StartingSkillKnowledge[skillKnowledgeIds[i]]))));
        node.Add(skillKnowledge);

        XElement knowledge = new XElement("knowledge");
        for (int i = 0; i < origin.StartingKnowledgeIds.Count; i++) knowledge.Add(new XElement("grant", new XAttribute("id", origin.StartingKnowledgeIds[i])));
        node.Add(knowledge);

        XElement choices = new XElement("creationChoices");
        List<string> choiceKeys = new List<string>(origin.CreationChoices.Keys); choiceKeys.Sort(StringComparer.Ordinal);
        for (int i = 0; i < choiceKeys.Count; i++) choices.Add(new XElement("choice", new XAttribute("key", choiceKeys[i]), new XAttribute("value", origin.CreationChoices[choiceKeys[i]] ?? string.Empty)));
        node.Add(choices);
        return node;
    }

    private static XElement SerializeProgression(RebirthWorldProgressionState state,string ownerKey)
    {
        state = state ?? new RebirthWorldProgressionState();
        XElement node = new XElement("progression", new XAttribute("healthPotential", F(state.HealthPotential)));
        XElement attrs = new XElement("attributes");
        List<string> attrIds = new List<string>(state.Attributes.Keys); attrIds.Sort(StringComparer.Ordinal);
        for (int i = 0; i < attrIds.Count; i++)
        {
            RebirthAttributeRuntimeState a = state.Attributes[attrIds[i]]; if (a == null) continue;
            attrs.Add(new XElement("attribute", new XAttribute("id", a.AttributeId), new XAttribute("current", F(a.Current)), new XAttribute("potential", F(a.Potential))));
        }
        node.Add(attrs);
        RebirthConstitutionTrainingRuntimeState constitutionTraining = state.ConstitutionTraining ?? new RebirthConstitutionTrainingRuntimeState();
        XElement constitutionTrainingNode = new XElement("constitutionTraining",
            new XAttribute("directWindowStartedActiveSeconds", D(Math.Max(0d, constitutionTraining.DirectWindowStartedActiveSeconds))),
            new XAttribute("healingFractionInWindow", F(Math.Max(0f, constitutionTraining.HealingFractionInWindow))),
            new XAttribute("rawDirectAwardInWindow", F(Math.Max(0f, constitutionTraining.RawDirectAwardInWindow))));
        XElement exposureCooldowns = new XElement("exposureCooldowns");
        List<string> exposureFamilies = new List<string>(constitutionTraining.ExposureFamilyReadyAtActiveSeconds.Keys); exposureFamilies.Sort(StringComparer.Ordinal);
        for (int i=0;i<exposureFamilies.Count;i++)
        {
            string family=exposureFamilies[i]; double readyAt=constitutionTraining.ExposureFamilyReadyAtActiveSeconds[family];
            if(string.IsNullOrWhiteSpace(family)||double.IsNaN(readyAt)||double.IsInfinity(readyAt)||readyAt<0d) continue;
            exposureCooldowns.Add(new XElement("family",new XAttribute("id",family),new XAttribute("readyAtActiveSeconds",D(readyAt))));
        }
        constitutionTrainingNode.Add(exposureCooldowns);
        node.Add(constitutionTrainingNode);

        RebirthSkillAntiRepeatRuntimeState antiRepeat = state.SkillAntiRepeat ?? new RebirthSkillAntiRepeatRuntimeState();
        XElement skillAwardCooldowns = new XElement("skillAwardCooldowns");
        List<string> cooldownKeys = new List<string>(antiRepeat.AwardReadyAtActiveSeconds.Keys); cooldownKeys.Sort(StringComparer.Ordinal);
        for (int i=0;i<cooldownKeys.Count;i++)
        {
            string key=cooldownKeys[i]; double readyAt=antiRepeat.AwardReadyAtActiveSeconds[key];
            if(string.IsNullOrWhiteSpace(key)||double.IsNaN(readyAt)||double.IsInfinity(readyAt)||readyAt<0d) continue;
            skillAwardCooldowns.Add(new XElement("cooldown",new XAttribute("key",key),new XAttribute("readyAtActiveSeconds",D(readyAt))));
        }
        node.Add(skillAwardCooldowns);

        RebirthCommerceTrainingRuntimeState commerce = state.CommerceTraining ?? new RebirthCommerceTrainingRuntimeState();
        XElement commerceTraining = new XElement("commerceTraining",
            new XAttribute("lastGlobalAwardActiveSeconds",D(Math.Max(-1d,commerce.LastGlobalAwardActiveSeconds))));
        List<string> commerceItemIds = new List<string>(commerce.Items.Keys); commerceItemIds.Sort(StringComparer.Ordinal);
        for (int i=0;i<commerceItemIds.Count;i++)
        {
            string itemId=commerceItemIds[i]; RebirthCommerceItemTrainingRuntimeState h=commerce.Items[itemId];
            if(h==null||string.IsNullOrWhiteSpace(itemId)) continue;
            commerceTraining.Add(new XElement("item",
                new XAttribute("id",itemId),
                new XAttribute("lastBuyActiveSeconds",D(Math.Max(-1d,h.LastBuyActiveSeconds))),
                new XAttribute("lastSellActiveSeconds",D(Math.Max(-1d,h.LastSellActiveSeconds))),
                new XAttribute("lastAwardActiveSeconds",D(Math.Max(-1d,h.LastAwardActiveSeconds))),
                new XAttribute("repeatChain",Math.Max(0,Math.Min(8,h.RepeatChain)))));
        }
        node.Add(commerceTraining);

        XElement skills = new XElement("skills");
        List<string> skillIds = new List<string>(state.Skills.Keys); skillIds.Sort(StringComparer.Ordinal);
        for (int i = 0; i < skillIds.Count; i++)
        {
            RebirthSkillRuntimeState s = state.Skills[skillIds[i]]; if (s == null) continue;
            skills.Add(new XElement("skill", new XAttribute("id", s.SkillId), new XAttribute("value", F(s.Value)), new XAttribute("progress", F(s.Progress))));
        }
        node.Add(skills);
        XElement skillKnowledge = new XElement("skillKnowledge");
        List<string> skillKnowledgeIds = new List<string>(state.SkillKnowledge.Keys); skillKnowledgeIds.Sort(StringComparer.Ordinal);
        for (int i = 0; i < skillKnowledgeIds.Count; i++)
        {
            RebirthSkillKnowledgeRuntimeState k = state.SkillKnowledge[skillKnowledgeIds[i]]; if (k == null) continue;
            skillKnowledge.Add(new XElement("skill", new XAttribute("id", k.SkillId), new XAttribute("value", F(k.Value))));
        }
        node.Add(skillKnowledge);
        node.Add(RebirthImprovisationProgressPersistence.Write(state.ImprovisationProgress));
        XElement knowledge = new XElement("knowledge");
        List<string> knowledgeIds = new List<string>(state.KnowledgeIds); knowledgeIds.Sort(StringComparer.Ordinal);
        for (int i = 0; i < knowledgeIds.Count; i++) knowledge.Add(new XElement("owned", new XAttribute("id", knowledgeIds[i])));
        node.Add(knowledge);
        node.Add(RebirthStationRecipeDiscoveryPersistence.Write(state.RecipeDiscoveries,ownerKey));
        XElement studyProgress = new XElement("studyProgress");
        List<string> studyIds = new List<string>(state.LiteratureStudyProgress.Keys); studyIds.Sort(StringComparer.Ordinal);
        for (int i = 0; i < studyIds.Count; i++)
        {
            float value=state.LiteratureStudyProgress[studyIds[i]];
            if(value<=0f || value>=1f) continue;
            studyProgress.Add(new XElement("item",new XAttribute("id",studyIds[i]),new XAttribute("progress",F(value))));
        }
        node.Add(studyProgress);
        node.Add(state.Cooking.Write());
        node.Add(RebirthTheoryStudyPersistence.Write(state.PendingTheoryStudy));
        node.Add(RebirthTheorySoloPersistence.Write(state.SoloTheory));
        node.Add(RebirthStationPreparationPersistence.Write(state.StationPreparations));
        node.Add(RebirthStationPublicationRecord.WriteAll(state.StationPublications,state.StationPreparations));
        node.Add(RebirthStationTerminalIntent.WriteAll(state.StationTerminalIntents,state.StationPreparations));
        node.Add(RebirthStationCompletionPublication.WriteAll(state.StationCompletionPublications,state.StationPreparations,state.StationTerminalIntents,state.StationPublications));
        if(state.StationCompletionExpectationProjections.Count!=0)node.Add(RebirthStationCompletionExpectationProjection.WriteAllStored(state.StationCompletionExpectationProjections,state.StationPreparations,state.StationTerminalIntents,state.StationPublications,state.StationCompletionPublications));
        node.Add(RebirthStationCancellationRefund.WriteAll(state.StationCancellationRefunds,state.StationPreparations,state.StationTerminalIntents));
        node.Add(RebirthStationCancellationAttempt.WriteAll(state.StationCancellationAttempts,state.StationPreparations,state.StationTerminalIntents,state.StationCancellationRefunds));
        node.Add(RebirthStationCancellationPublication.WriteAll(state.StationCancellationPublications,state.StationPreparations,state.StationTerminalIntents,state.StationCancellationRefunds,state.StationCancellationAttempts));
        node.Add(RebirthStationRefundDelivery.WriteAll(state.StationRefundDeliveries,ownerKey,state.StationPreparations,state.StationTerminalIntents,state.StationCancellationRefunds,state.StationCancellationAttempts,state.StationCancellationPublications));
        foreach(var archive in state.StationRefundArchives.Values)
            if(state.StationPreparations.ContainsKey(archive.JobId)||state.StationRefundDeliveries.Values.Any(d=>
                (string)d.Write().Attribute("delivery")== (string)archive.Write().Element("stationRefundDelivery").Attribute("delivery")))
                throw new InvalidDataException("Active/archived station refund identity collision");
        node.Add(RebirthStationRefundArchive.WriteAll(state.StationRefundArchives,ownerKey));
        XElement disciplines = new XElement("disciplines");
        List<string> disciplineIds = new List<string>(state.AcquiredDisciplineIds); disciplineIds.Sort(StringComparer.Ordinal);
        for (int i = 0; i < disciplineIds.Count; i++) disciplines.Add(new XElement("acquired", new XAttribute("id", disciplineIds[i])));
        node.Add(disciplines);
        XElement accomplishments = new XElement("accomplishments");
        List<string> accomplishmentIds = new List<string>(state.AccomplishmentIds); accomplishmentIds.Sort(StringComparer.Ordinal);
        for (int i = 0; i < accomplishmentIds.Count; i++) accomplishments.Add(new XElement("completed", new XAttribute("id", accomplishmentIds[i])));
        node.Add(accomplishments);
        XElement trials = new XElement("trials", new XAttribute("witchDoctorAttunements", Math.Max(0,state.WitchDoctorInitiationAttunements)), new XAttribute("berserkerMeleeHits", Math.Max(0,state.BerserkerInitiationMeleeHits)));
        List<string> trialIds = new List<string>(state.CompletedTrialIds); trialIds.Sort(StringComparer.Ordinal);
        for (int i = 0; i < trialIds.Count; i++) trials.Add(new XElement("completed", new XAttribute("id", trialIds[i])));
        node.Add(trials);
        XElement wildTaming = new XElement("wildTaming");
        List<string> sessionIds = new List<string>(state.WildTamingSessions.Keys); sessionIds.Sort(StringComparer.Ordinal);
        for (int i = 0; i < sessionIds.Count; i++)
        {
            RebirthWildTamingRuntimeState t = state.WildTamingSessions[sessionIds[i]]; if(t==null)continue;
            wildTaming.Add(new XElement("session", new XAttribute("id", t.SessionId ?? sessionIds[i]), new XAttribute("entityId", t.TargetEntityId),
                new XAttribute("sourceClass", t.SourceEntityClass ?? string.Empty), new XAttribute("category", t.SpeciesCategory ?? string.Empty),
                new XAttribute("stage", t.Stage ?? "calm"), new XAttribute("interactions", Math.Max(0,t.SuccessfulInteractions)),
                new XAttribute("x", F(t.LastKnownPosition.x)), new XAttribute("y", F(t.LastKnownPosition.y)), new XAttribute("z", F(t.LastKnownPosition.z)),
                new XAttribute("startedUtcTicks", Math.Max(0L,t.StartedUtcTicks)), new XAttribute("lastInteractionUtcTicks", Math.Max(0L,t.LastInteractionUtcTicks)),
                new XAttribute("initiation", t.InitiationTrial ? "1" : "0")));
        }
        node.Add(wildTaming);
        XElement teaching = new XElement("teaching");
        XElement lessons = new XElement("lessons");
        List<string> lessonIds = new List<string>(state.TeachingLessons.Keys); lessonIds.Sort(StringComparer.Ordinal);
        for (int i=0;i<lessonIds.Count;i++)
        {
            RebirthTeachingLessonRuntimeState l=state.TeachingLessons[lessonIds[i]]; if(l==null||l.RemainingActiveSeconds<=0f)continue;
            lessons.Add(new XElement("lesson",new XAttribute("skillId",l.SkillId??lessonIds[i]),new XAttribute("remainingActiveSeconds",F(l.RemainingActiveSeconds)),new XAttribute("gainMultiplier",F(Math.Max(1f,l.GainMultiplier))),new XAttribute("instructorStorageKey",l.InstructorStorageKey??string.Empty),new XAttribute("outcomeId",l.OutcomeId??string.Empty)));
        }
        teaching.Add(lessons);
        XElement history = new XElement("history");
        List<string> historyIds = new List<string>(state.TeachingHistory.Keys); historyIds.Sort(StringComparer.Ordinal);
        for (int i=0;i<historyIds.Count;i++)
        {
            RebirthTeachingHistoryRuntimeState h=state.TeachingHistory[historyIds[i]]; if(h==null||h.LastCompletedUtcTicks<=0L)continue;
            history.Add(new XElement("completed",new XAttribute("key",h.Key??historyIds[i]),new XAttribute("studentStorageKey",h.StudentStorageKey??string.Empty),new XAttribute("skillId",h.SkillId??string.Empty),new XAttribute("lastCompletedUtcTicks",Math.Max(0L,h.LastCompletedUtcTicks)),new XAttribute("count",Math.Max(1,h.CompletionCount)),new XAttribute("studentTheoryValue",F(Mathf.Clamp(h.StudentTheoryValue,0f,100f)))));
        }
        teaching.Add(history);
        node.Add(teaching);
        XElement awardReceipts = new XElement("skillAwardReceipts");
        List<string> receiptIds = new List<string>(state.SkillAwardReceipts); receiptIds.Sort(StringComparer.Ordinal);
        for (int i=0;i<receiptIds.Count;i++) if(!string.IsNullOrEmpty(receiptIds[i])) awardReceipts.Add(new XElement("receipt",new XAttribute("id",receiptIds[i])));
        node.Add(awardReceipts);
        return node;
    }

    private static XElement SerializeCondition(RebirthWorldConditionState state)
    {
        state = state ?? new RebirthWorldConditionState();
        XElement node = new XElement("condition",
            new XAttribute("moodCurrent", F(state.MoodCurrent)),
            new XAttribute("moodTarget", F(state.MoodTarget)),
            new XAttribute("dietSatisfaction", F(state.DietSatisfaction)),
            new XAttribute("healthCapacity", F(state.HealthCapacity)),
            new XAttribute("severeDehydrationActiveSeconds", F(state.SevereDehydrationActiveSeconds)),
            new XAttribute("severeMalnutritionActiveSeconds", F(state.SevereMalnutritionActiveSeconds)),
            new XAttribute("activePlaySeconds", D(state.ActivePlaySeconds)),
            new XAttribute("lastActiveWorldTime", state.LastActiveWorldTime));
        XElement meals = new XElement("recentMeals");
        node.Add(state.Stress.Write());
        for (int i = 0; i < state.RecentMeals.Count; i++)
        {
            RebirthRecentMealState meal = state.RecentMeals[i]; if (meal == null) continue;
            meals.Add(new XElement("meal", new XAttribute("sourceItemId", meal.SourceItemId ?? string.Empty), new XAttribute("varietyFamilyId", meal.VarietyFamilyId ?? string.Empty),
                new XAttribute("moodQuality", F(meal.MoodQuality)), new XAttribute("compatibleWithDiet", meal.CompatibleWithDiet), new XAttribute("ageActiveSeconds", F(meal.AgeActiveSeconds))));
        }
        node.Add(meals);
        return node;
    }

    private static XElement SerializeSupport(RebirthWorldSupportState state,string ownerKey,string creation)
    {
        state = state ?? new RebirthWorldSupportState();
        XElement node = new XElement("support");
        XElement gear = new XElement("gear");
        List<string> gearSlots = new List<string>(state.EquippedGearBySlot.Keys); gearSlots.Sort(StringComparer.Ordinal);
        for (int i = 0; i < gearSlots.Count; i++)
        {
            string slotId = gearSlots[i];
            string itemId = state.EquippedGearBySlot[slotId] ?? string.Empty;
            if (string.IsNullOrEmpty(slotId) || string.IsNullOrEmpty(itemId)) continue;
            string itemData; state.EquippedGearItemDataBySlot.TryGetValue(slotId,out itemData);
            XElement gearSlot=new XElement("slot", new XAttribute("id", slotId), new XAttribute("itemId", itemId));
            if(!string.IsNullOrEmpty(itemData))gearSlot.Add(new XAttribute("itemData",itemData));
            gear.Add(gearSlot);
        }
        node.Add(gear);
        node.Add(RebirthGearTransferPersistence.Write(state.GearRevision, state.PendingGearTransfer, state.GearTransferPhase));
        if(state.PendingLibraryTransfer!=null&&(state.PendingGearTransfer!=null||state.PendingMusicTransfer!=null))throw new InvalidOperationException("Competing library custody.");
        node.Add(RebirthBackpackLibraryPersistence.Write(state.GearRevision,state.PendingLibraryTransfer,state.LibraryTransferPhase));
        if(state.PendingGearPreparationRefusal!=null)
        {
            if(!state.PendingGearPreparationRefusal.MatchesSupport(creation,state.GearRevision,
                state.PendingGearTransfer!=null||state.PendingLibraryTransfer!=null||state.PendingMusicTransfer!=null,
                state.LastGearSettlement?.TransactionId))throw new InvalidOperationException("Competing or invalid unprepared gear refusal.");
            node.Add(state.PendingGearPreparationRefusal.Write());
        }
        if(state.LastGearPreparationRefusal!=null)
        {
            if(!RebirthGearPreparationRefusalRetirement.MatchesSupport(state.LastGearPreparationRefusal,creation,state.GearRevision,
                state.PendingGearPreparationRefusal,state.LastGearSettlement?.TransactionId))throw new InvalidOperationException("Invalid retired gear refusal.");
            node.Add(RebirthGearPreparationRefusalRetirement.Write(state.LastGearPreparationRefusal));
        }
        if(state.LastGearSettlement!=null)node.Add(state.LastGearSettlement.Write());
        var terminalOriginal=RebirthGearTerminalOriginalPersistence.Write(state.LastGearSettlement,state.LastGearSettlementOriginal);
        if(terminalOriginal!=null)node.Add(terminalOriginal);
        if(state.LastLibrarySettlement!=null)node.Add(state.LastLibrarySettlement.Write());
        node.Add(RemoteResourceRefundSupportPersistence.Write(state.RemoteResourceRefundJournalImage,ownerKey,creation));
        XElement music = new XElement("music", new XAttribute("shuffle", state.MusicShuffle), new XAttribute("revision", state.MusicRevision));
        if (state.PendingMusicTransfer != null) music.Add(state.PendingMusicTransfer.ToXml());
        foreach (var cassette in state.MusicCassettes)
            if (cassette != null && !string.IsNullOrEmpty(cassette.ItemId))
                music.Add(new XElement("cassette", new XAttribute("itemId", cassette.ItemId), new XAttribute("itemData", cassette.ItemData ?? string.Empty)));
        node.Add(music);
        node.Add(RebirthAudiobookLibraryPersistence.Write(state.AudiobookRevision,state.AudiobookCassettes));
        List<string> ids = new List<string>(state.Entries.Keys); ids.Sort(StringComparer.Ordinal);
        for (int i = 0; i < ids.Count; i++)
        {
            RebirthTraitSupportRuntimeState s = state.Entries[ids[i]]; if (s == null) continue;
            node.Add(new XElement("entry", new XAttribute("profileId", s.SupportProfileId ?? ids[i]),
                new XAttribute("graceRemainingActiveSeconds", F(s.GraceRemainingActiveSeconds)),
                new XAttribute("managedRemainingActiveSeconds", F(s.ManagedRemainingActiveSeconds)),
                new XAttribute("positiveRemainingActiveSeconds", F(s.PositiveRemainingActiveSeconds)),
                new XAttribute("cooldownRemainingActiveSeconds", F(s.CooldownRemainingActiveSeconds)),
                new XAttribute("stacks", Math.Max(0, s.Stacks))));
        }
        return node;
    }

    private static bool TryDeserialize(XDocument doc, out RebirthWorldCharacterRecord record, out string error)
    {
        record = null; error = string.Empty;
        try
        {
            XElement root = doc != null ? doc.Root : null;
            if (root == null || root.Name != "rebirthWorldCharacter") { error = "unexpected world-character root"; return false; }
            int schema; long revision; DateTime created; DateTime modified;
            if (!int.TryParse(A(root,"schemaVersion"), NumberStyles.Integer, CultureInfo.InvariantCulture, out schema)) { error = "invalid schemaVersion"; return false; }
            if (!long.TryParse(A(root,"revision"), NumberStyles.Integer, CultureInfo.InvariantCulture, out revision)) { error = "invalid revision"; return false; }
            if (!DateTime.TryParse(A(root,"createdAtUtc"), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out created)) { error = "invalid createdAtUtc"; return false; }
            if (!DateTime.TryParse(A(root,"modifiedAtUtc"), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out modified)) { error = "invalid modifiedAtUtc"; return false; }
            int migrationSource=schema, migrationTarget=schema; bool migrationApplied=false;
            string migrationSourceText=A(root,"migrationSourceSchema"), migrationTargetText=A(root,"migrationTargetSchema"), migrationAppliedText=A(root,"migrationApplied");
            if(migrationSourceText.Length>0&&!int.TryParse(migrationSourceText,NumberStyles.Integer,CultureInfo.InvariantCulture,out migrationSource)){error="invalid migrationSourceSchema";return false;}
            if(migrationTargetText.Length>0&&!int.TryParse(migrationTargetText,NumberStyles.Integer,CultureInfo.InvariantCulture,out migrationTarget)){error="invalid migrationTargetSchema";return false;}
            if(migrationAppliedText.Length>0&&!bool.TryParse(migrationAppliedText,out migrationApplied)){error="invalid migrationApplied";return false;}
            if(migrationSource<1||migrationTarget<1||migrationTarget!=schema){error="invalid migration metadata source="+migrationSource+" target="+migrationTarget+" schema="+schema;return false;}
            RebirthWorldOriginSnapshot origin; if (!TryDeserializeOrigin(root.Element("origin"), out origin, out error)) return false;
            RebirthWorldProgressionState progression; if (!TryDeserializeProgression(root.Element("progression"), A(root,"stablePlayerKey"), out progression, out error)) return false;
            if(!RebirthStationDiscoveryAdmissionPersistence.TryRead(root.Element("progression"),A(root,"stablePlayerKey"),origin.CreationId,out var discoveryAdmissions,out error))return false;
            foreach(var pair in discoveryAdmissions)progression.StationDiscoveryAdmissions.Add(pair.Key,pair.Value);
            if(!RebirthStationRecipeDiscoveryPersistence.MatchesCreation(progression.RecipeDiscoveries.Values,origin.CreationId)){error="Recipe discovery character mismatch";return false;}
            if(!RebirthTheoryStudyPersistence.MatchesOwner(progression.PendingTheoryStudy,origin.CreationId)){error="Theory study owner mismatch";return false;}
            if(!RebirthStationPreparationPersistence.MatchesOwner(progression.StationPreparations,origin.CreationId)){error="Station preparation owner mismatch";return false;}
            if(progression.StationRefundArchives.Values.Any(a=>a==null||!RebirthSurvivorRequestScope.Matches(a.CreationId,origin.CreationId))){error="Station refund archive character mismatch";return false;}
            if(!RebirthTheorySoloPersistence.MatchesOwner(progression.SoloTheory,origin.CreationId)){error="Solo Theory owner mismatch";return false;}
            RebirthWorldConditionState condition; if (!TryDeserializeCondition(root.Element("condition"), out condition, out error)) return false;
            RebirthWorldSupportState support; if (!TryDeserializeSupport(root.Element("support"),A(root,"stablePlayerKey"),origin.CreationId, out support, out error)) return false;
            record = new RebirthWorldCharacterRecord(schema, A(root,"stablePlayerId"), A(root,"stablePlayerKey"), revision, created, modified, origin, progression, condition, support,
                migrationSource,migrationTarget,A(root,"migrationPolicyId"),migrationApplied,A(root,"migrationOriginAudit"),A(root,"migrationProgressionAudit"));
            if (record.SchemaVersion != RebirthWorldCharacterRecord.CurrentSchemaVersion) { error = "world-character schema did not migrate to current"; record = null; return false; }
            return true;
        }
        catch (Exception ex) { error = ex.GetType().Name + ": " + ex.Message; record = null; return false; }
    }

    private static bool TryDeserializeOrigin(XElement node, out RebirthWorldOriginSnapshot origin, out string error)
    {
        origin = null; error = string.Empty;
        if (node == null) { error = "origin section is missing"; return false; }
        DateTime committed; int points; int slots; float health;
        if (!DateTime.TryParse(A(node,"committedAtUtc"), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out committed)) { error = "origin committedAtUtc invalid"; return false; }
        if (!int.TryParse(A(node,"remainingCreationPoints"), NumberStyles.Integer, CultureInfo.InvariantCulture, out points)) { error = "origin points invalid"; return false; }
        if (!int.TryParse(A(node,"unencumberedSlotDelta"), NumberStyles.Integer, CultureInfo.InvariantCulture, out slots)) { error = "origin slot delta invalid"; return false; }
        if (!TryFloat(node,"healthPotential",out health)) { error = "origin healthPotential invalid"; return false; }
        List<string> traits = new List<string>(); XElement traitsNode=node.Element("traits"); if(traitsNode!=null) foreach(XElement x in traitsNode.Elements("trait")) traits.Add(A(x,"id"));
        List<RebirthWorldOriginAttribute> attrs; if(!TryDeserializeOriginAttributes(node.Element("attributes"),out attrs,out error)) return false;
        Dictionary<string,float> skills;
        if(!TryDeserializeOriginSkills(node.Element("skills"),out skills,out error)) return false;
        Dictionary<string,float> skillKnowledge;
        if(!TryDeserializeOriginSkillKnowledge(node.Element("skillKnowledge"),out skillKnowledge,out error)) return false;
        List<string> knowledge=new List<string>(); XElement knowledgeNode=node.Element("knowledge"); if(knowledgeNode!=null) foreach(XElement x in knowledgeNode.Elements("grant")) knowledge.Add(A(x,"id"));
        Dictionary<string,string> choices=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase); XElement choicesNode=node.Element("creationChoices"); if(choicesNode!=null) foreach(XElement x in choicesNode.Elements("choice")) choices[A(x,"key")]=A(x,"value");
        origin=new RebirthWorldOriginSnapshot(A(node,"creationId"),A(node,"definitionHash"),A(node,"definitionVersion"),A(node,"sourceProfileId"),A(node,"sourceProfileName"),A(node,"backgroundId"),A(node,"dietId"),traits,points,attrs,health,slots,skills,skillKnowledge,knowledge,choices,committed);
        if(string.IsNullOrEmpty(origin.CreationId)||string.IsNullOrEmpty(origin.DefinitionHash)||string.IsNullOrEmpty(origin.BackgroundId)||string.IsNullOrEmpty(origin.DietId)){error="origin required IDs are missing";origin=null;return false;} return true;
    }

    private static bool TryDeserializeProgression(XElement node,string ownerKey, out RebirthWorldProgressionState state, out string error)
    {
        state=new RebirthWorldProgressionState();error=string.Empty;if(node==null){error="progression section is missing";return false;}float hp;if(!TryFloat(node,"healthPotential",out hp)){error="progression healthPotential invalid";return false;}state.HealthPotential=hp;
        if(!TryDeserializeProgressionAttributes(node.Element("attributes"),state,out error)) return false;
        if(!TryDeserializeConstitutionTraining(node.Element("constitutionTraining"),state,out error)) return false;
        if(!TryDeserializeSkillAwardCooldowns(node.Element("skillAwardCooldowns"),state,out error)) return false;
        if(!TryDeserializeCommerceTraining(node.Element("commerceTraining"),state,out error)) return false;
        if(!TryDeserializeProgressionSkills(node.Element("skills"),state,out error)) return false;
        if(!TryDeserializeProgressionSkillKnowledge(node.Element("skillKnowledge"),state,out error)) return false;
        Dictionary<string,float> improvisation;
        if(!RebirthImprovisationProgressPersistence.TryRead(node,out improvisation,out error)) return false;
        foreach(var pair in improvisation) state.ImprovisationProgress[pair.Key]=pair.Value;
        Dictionary<string,RebirthStationGridAdmission> preparations;
        if(!RebirthStationPreparationPersistence.TryRead(node,out preparations,out error)) return false;
        foreach(var pair in preparations) state.StationPreparations.Add(pair.Key,pair.Value);
        if(!RebirthStationPublicationRecord.ReadAll(node,state.StationPreparations,out var publications)){error="Invalid station publications";return false;}
        foreach(var pair in publications) state.StationPublications.Add(pair.Key,pair.Value);
        if(!RebirthStationTerminalIntent.ReadAll(node,state.StationPreparations,out var terminalIntents)){error="Invalid station terminal intents";return false;}
        foreach(var pair in terminalIntents) state.StationTerminalIntents.Add(pair.Key,pair.Value);
        if(!RebirthStationCompletionPublication.ReadAll(node,state.StationPreparations,state.StationTerminalIntents,state.StationPublications,out var completedPublications)){error="Invalid station completion publications";return false;}
        foreach(var pair in completedPublications)state.StationCompletionPublications.Add(pair.Key,pair.Value);
        if(!RebirthStationCompletionExpectationProjection.ReadAllStored(node,state.StationPreparations,state.StationTerminalIntents,state.StationPublications,state.StationCompletionPublications,out var completionProjections)){error="Invalid station completion expectation projections";return false;}
        foreach(var pair in completionProjections)state.StationCompletionExpectationProjections.Add(pair.Key,pair.Value);
        if(!RebirthStationCancellationRefund.ReadAll(node,state.StationPreparations,state.StationTerminalIntents,out var cancellationRefunds)){error="Invalid station cancellation refunds";return false;}
        foreach(var pair in cancellationRefunds) state.StationCancellationRefunds.Add(pair.Key,pair.Value);
        if(!RebirthStationCancellationAttempt.ReadAll(node,state.StationPreparations,state.StationTerminalIntents,state.StationCancellationRefunds,out var cancellationAttempts)){error="Invalid station cancellation attempts";return false;}
        foreach(var pair in cancellationAttempts) state.StationCancellationAttempts.Add(pair.Key,pair.Value);
        if(!RebirthStationCancellationPublication.ReadAll(node,state.StationPreparations,state.StationTerminalIntents,state.StationCancellationRefunds,state.StationCancellationAttempts,out var cancelledPublications)){error="Invalid cancelled station publications";return false;}
        foreach(var pair in cancelledPublications) state.StationCancellationPublications.Add(pair.Key,pair.Value);
        if(!RebirthStationRefundDelivery.ReadAll(node,ownerKey,state.StationPreparations,state.StationTerminalIntents,state.StationCancellationRefunds,state.StationCancellationAttempts,state.StationCancellationPublications,out var refundDeliveries)){error="Invalid station refund deliveries";return false;}
        foreach(var pair in refundDeliveries) state.StationRefundDeliveries.Add(pair.Key,pair.Value);
        if(!RebirthStationRefundArchive.ReadAll(node,ownerKey,out var refundArchives)){error="Invalid station refund archives";return false;}
        foreach(var pair in refundArchives)
        {
            if(state.StationPreparations.ContainsKey(pair.Key)||state.StationRefundDeliveries.Values.Any(d=>
                (string)d.Write().Attribute("delivery")== (string)pair.Value.Write().Element("stationRefundDelivery").Attribute("delivery")))
            {error="Active/archived station refund identity collision";return false;}
            state.StationRefundArchives.Add(pair.Key,pair.Value);
        }
        RebirthTheoryStudyOutcome studyOutcome;
        if(!RebirthTheoryStudyPersistence.TryRead(node,out studyOutcome,out error))return false;
        state.PendingTheoryStudy=studyOutcome;
        RebirthTheorySoloState soloTheory;
        if(!RebirthTheorySoloPersistence.TryRead(node,out soloTheory,out error))return false;
        state.SoloTheory=soloTheory;
        if(!string.IsNullOrEmpty(soloTheory?.CancelPendingId)&&studyOutcome!=null){error="Cancellation hold conflicts with Theory completion intent";return false;}
        if(studyOutcome?.Mode=="solo"&&!RebirthTheorySoloService.ValidateOutcome(soloTheory,studyOutcome,false,false)){error="Solo Theory outcome/evidence mismatch";return false;}
        if(soloTheory?.Session?.Consumed==true&&studyOutcome?.Mode!="solo"){error="Consumed solo Theory session has no pending outcome";return false;}
        state.Cooking=RebirthCookingMemory.Read(node.Element("cooking"));
        Dictionary<string,RebirthStationRecipeDiscoveryRecord> discoveries;
        if(!RebirthStationRecipeDiscoveryPersistence.TryRead(node,ownerKey,out discoveries,out error))return false;
        foreach(var pair in discoveries)state.RecipeDiscoveries.Add(pair.Key,pair.Value);
        XElement knowledge=node.Element("knowledge");if(knowledge!=null)foreach(XElement x in knowledge.Elements("owned"))state.KnowledgeIds.Add(A(x,"id"));
        XElement study=node.Element("studyProgress");
        if(study!=null)foreach(XElement x in study.Elements("item"))
        {
            string id=A(x,"id"); float progress;
            if(string.IsNullOrEmpty(id)||!TryFloat(x,"progress",out progress)||float.IsNaN(progress)||float.IsInfinity(progress)||progress<=0f||progress>=1f){error="progression literature study progress invalid";return false;}
            RebirthLiteratureDefinition literature; if(!RebirthProgressionRuntimeConfig.TryGetLiterature(id,out literature)||literature==null){error="progression literature study progress references unknown item \'"+id+"\'";return false;}
            state.LiteratureStudyProgress[id]=progress;
        }
        XElement disciplines=node.Element("disciplines");if(disciplines!=null)foreach(XElement x in disciplines.Elements("acquired")){string id=A(x,"id");if(!string.IsNullOrWhiteSpace(id))state.AcquiredDisciplineIds.Add(id);}
        XElement accomplishments=node.Element("accomplishments");if(accomplishments!=null)foreach(XElement x in accomplishments.Elements("completed")){string id=A(x,"id");if(!string.IsNullOrWhiteSpace(id))state.AccomplishmentIds.Add(id);}
        XElement trials=node.Element("trials");if(trials!=null){int att=0;int.TryParse(A(trials,"witchDoctorAttunements"),NumberStyles.Integer,CultureInfo.InvariantCulture,out att);state.WitchDoctorInitiationAttunements=Math.Max(0,att);int bh=0;int.TryParse(A(trials,"berserkerMeleeHits"),NumberStyles.Integer,CultureInfo.InvariantCulture,out bh);state.BerserkerInitiationMeleeHits=Math.Max(0,bh);foreach(XElement x in trials.Elements("completed")){string id=A(x,"id");if(!string.IsNullOrWhiteSpace(id))state.CompletedTrialIds.Add(id);}}
        XElement wild=node.Element("wildTaming");if(wild!=null)foreach(XElement x in wild.Elements("session"))
        {
            string id=A(x,"id");int entityId=0,interactions=0;long started=0,last=0;float px=0f,py=0f,pz=0f;
            int.TryParse(A(x,"entityId"),NumberStyles.Integer,CultureInfo.InvariantCulture,out entityId);
            int.TryParse(A(x,"interactions"),NumberStyles.Integer,CultureInfo.InvariantCulture,out interactions);
            long.TryParse(A(x,"startedUtcTicks"),NumberStyles.Integer,CultureInfo.InvariantCulture,out started);
            long.TryParse(A(x,"lastInteractionUtcTicks"),NumberStyles.Integer,CultureInfo.InvariantCulture,out last);
            float.TryParse(A(x,"x"),NumberStyles.Float,CultureInfo.InvariantCulture,out px);float.TryParse(A(x,"y"),NumberStyles.Float,CultureInfo.InvariantCulture,out py);float.TryParse(A(x,"z"),NumberStyles.Float,CultureInfo.InvariantCulture,out pz);
            if(string.IsNullOrWhiteSpace(id))id="entity:"+entityId.ToString(CultureInfo.InvariantCulture);
            state.WildTamingSessions[id]=new RebirthWildTamingRuntimeState{SessionId=id,TargetEntityId=entityId,SourceEntityClass=A(x,"sourceClass"),SpeciesCategory=A(x,"category"),Stage=A(x,"stage"),SuccessfulInteractions=Math.Max(0,interactions),LastKnownPosition=new UnityEngine.Vector3(px,py,pz),StartedUtcTicks=Math.Max(0L,started),LastInteractionUtcTicks=Math.Max(0L,last),InitiationTrial=A(x,"initiation")=="1"};
        }
        XElement teaching=node.Element("teaching"); if(teaching!=null)
        {
            XElement lessons=teaching.Element("lessons"); if(lessons!=null)foreach(XElement x in lessons.Elements("lesson"))
            {
                string skill=A(x,"skillId"),instructor=A(x,"instructorStorageKey"); float remaining,multiplier; if(string.IsNullOrEmpty(skill)||!TryFloat(x,"remainingActiveSeconds",out remaining)||!TryFloat(x,"gainMultiplier",out multiplier)||remaining<=0f||multiplier<1f)continue;
                RebirthSkillDefinition def; if(!RebirthSurvivorDefinitionRegistry.TryGetSkill(skill,out def)||def==null)continue;
                state.TeachingLessons[skill]=new RebirthTeachingLessonRuntimeState{SkillId=skill,RemainingActiveSeconds=remaining,GainMultiplier=multiplier,InstructorStorageKey=instructor,OutcomeId=A(x,"outcomeId")};
            }
            XElement history=teaching.Element("history"); if(history!=null)foreach(XElement x in history.Elements("completed"))
            {
                string key=A(x,"key"),student=A(x,"studentStorageKey"),skill=A(x,"skillId"); long ticks; int count; if(string.IsNullOrEmpty(key)||string.IsNullOrEmpty(student)||string.IsNullOrEmpty(skill)||!long.TryParse(A(x,"lastCompletedUtcTicks"),NumberStyles.Integer,CultureInfo.InvariantCulture,out ticks)||ticks<=0L)continue; if(!int.TryParse(A(x,"count"),NumberStyles.Integer,CultureInfo.InvariantCulture,out count))count=1;
                float studentTheory=0f;TryFloat(x,"studentTheoryValue",out studentTheory);state.TeachingHistory[key]=new RebirthTeachingHistoryRuntimeState{Key=key,StudentStorageKey=student,SkillId=skill,LastCompletedUtcTicks=ticks,CompletionCount=Math.Max(1,count),StudentTheoryValue=Mathf.Clamp(studentTheory,0f,100f)};
            }
        }
        // Load all saved evidence before applying pending-aware retention.
        XElement awardReceipts=node.Element("skillAwardReceipts");
        if(awardReceipts!=null)foreach(XElement x in awardReceipts.Elements("receipt"))
        {
            string id=A(x,"id"); if(!string.IsNullOrEmpty(id)) state.SkillAwardReceipts.Add(id);
        }
        RebirthTeachingOutcomeStore.PruneAwardReceipts(state.SkillAwardReceipts,state.PendingTheoryStudy?.Receipt);
        return true;
    }

    private static bool TryDeserializeSkillAwardCooldowns(XElement node,RebirthWorldProgressionState state,out string error)
    {
        error=string.Empty;
        if(node==null){error="progression Skill award cooldown state is missing";return false;}
        RebirthSkillAntiRepeatRuntimeState parsed=new RebirthSkillAntiRepeatRuntimeState();
        foreach(XElement x in node.Elements("cooldown"))
        {
            string key=A(x,"key"); double readyAt;
            if(string.IsNullOrWhiteSpace(key)){error="progression Skill award cooldown key is blank";return false;}
            if(parsed.AwardReadyAtActiveSeconds.ContainsKey(key)){error="progression Skill award cooldown key '"+key+"' is duplicated";return false;}
            if(!double.TryParse(A(x,"readyAtActiveSeconds"),NumberStyles.Float,CultureInfo.InvariantCulture,out readyAt)||double.IsNaN(readyAt)||double.IsInfinity(readyAt)||readyAt<0d)
            {error="progression Skill award cooldown '"+key+"' is invalid";return false;}
            if(parsed.AwardReadyAtActiveSeconds.Count>=512){error="progression Skill award cooldown state exceeds 512 entries";return false;}
            parsed.AwardReadyAtActiveSeconds[key]=readyAt;
        }
        state.SkillAntiRepeat=parsed;
        return true;
    }

    private static bool TryDeserializeCommerceTraining(XElement node,RebirthWorldProgressionState state,out string error)
    {
        error=string.Empty;
        if(node==null){error="progression commerce training state is missing";return false;}
        double global;
        if(!double.TryParse(A(node,"lastGlobalAwardActiveSeconds"),NumberStyles.Float,CultureInfo.InvariantCulture,out global)||double.IsNaN(global)||double.IsInfinity(global)||global< -1d)
        {error="progression commerce global award time is invalid";return false;}
        RebirthCommerceTrainingRuntimeState parsed=new RebirthCommerceTrainingRuntimeState{LastGlobalAwardActiveSeconds=global};
        foreach(XElement x in node.Elements("item"))
        {
            string id=A(x,"id"); double buy,sell,award; int chain;
            if(string.IsNullOrWhiteSpace(id)){error="progression commerce item id is blank";return false;}
            if(parsed.Items.ContainsKey(id)){error="progression commerce item '"+id+"' is duplicated";return false;}
            if(!double.TryParse(A(x,"lastBuyActiveSeconds"),NumberStyles.Float,CultureInfo.InvariantCulture,out buy)||double.IsNaN(buy)||double.IsInfinity(buy)||buy< -1d ||
               !double.TryParse(A(x,"lastSellActiveSeconds"),NumberStyles.Float,CultureInfo.InvariantCulture,out sell)||double.IsNaN(sell)||double.IsInfinity(sell)||sell< -1d ||
               !double.TryParse(A(x,"lastAwardActiveSeconds"),NumberStyles.Float,CultureInfo.InvariantCulture,out award)||double.IsNaN(award)||double.IsInfinity(award)||award< -1d ||
               !int.TryParse(A(x,"repeatChain"),NumberStyles.Integer,CultureInfo.InvariantCulture,out chain)||chain<0||chain>8)
            {error="progression commerce item '"+id+"' state is invalid";return false;}
            if(parsed.Items.Count>=256){error="progression commerce training state exceeds 256 entries";return false;}
            parsed.Items[id]=new RebirthCommerceItemTrainingRuntimeState{LastBuyActiveSeconds=buy,LastSellActiveSeconds=sell,LastAwardActiveSeconds=award,RepeatChain=chain};
        }
        state.CommerceTraining=parsed;
        return true;
    }

    private static bool TryDeserializeConstitutionTraining(XElement node,RebirthWorldProgressionState state,out string error)
    {
        error=string.Empty;
        if(node==null){error="progression Constitution training state is missing";return false;}
        double windowStart; float healingFraction,rawAward;
        if(!double.TryParse(A(node,"directWindowStartedActiveSeconds"),NumberStyles.Float,CultureInfo.InvariantCulture,out windowStart)||double.IsNaN(windowStart)||double.IsInfinity(windowStart)||windowStart<0d)
        {error="progression Constitution training window start is invalid";return false;}
        if(!TryFloat(node,"healingFractionInWindow",out healingFraction)||float.IsNaN(healingFraction)||float.IsInfinity(healingFraction)||healingFraction<0f)
        {error="progression Constitution training healing fraction is invalid";return false;}
        if(!TryFloat(node,"rawDirectAwardInWindow",out rawAward)||float.IsNaN(rawAward)||float.IsInfinity(rawAward)||rawAward<0f)
        {error="progression Constitution training direct award is invalid";return false;}
        RebirthConstitutionTrainingRuntimeState parsed=new RebirthConstitutionTrainingRuntimeState{DirectWindowStartedActiveSeconds=windowStart,HealingFractionInWindow=healingFraction,RawDirectAwardInWindow=rawAward};
        XElement cooldowns=node.Element("exposureCooldowns");
        if(cooldowns==null){error="progression Constitution exposure cooldown state is missing";return false;}
        foreach(XElement x in cooldowns.Elements("family"))
        {
            string id=A(x,"id"); double readyAt;
            if(string.IsNullOrWhiteSpace(id)){error="progression Constitution exposure family id is blank";return false;}
            if(parsed.ExposureFamilyReadyAtActiveSeconds.ContainsKey(id)){error="progression Constitution exposure family '"+id+"' is duplicated";return false;}
            if(!double.TryParse(A(x,"readyAtActiveSeconds"),NumberStyles.Float,CultureInfo.InvariantCulture,out readyAt)||double.IsNaN(readyAt)||double.IsInfinity(readyAt)||readyAt<0d)
            {error="progression Constitution exposure family '"+id+"' cooldown is invalid";return false;}
            parsed.ExposureFamilyReadyAtActiveSeconds[id]=readyAt;
        }
        state.ConstitutionTraining=parsed;
        return true;
    }

    private static bool TryDeserializeOriginAttributes(XElement node,out List<RebirthWorldOriginAttribute> values,out string error)
    {
        values=new List<RebirthWorldOriginAttribute>(); error=string.Empty;
        Dictionary<string,bool> seen=new Dictionary<string,bool>(StringComparer.OrdinalIgnoreCase);
        if(node==null){error="origin Attributes section is missing";return false;}
        foreach(XElement x in node.Elements("attribute"))
        {
            string id=A(x,"id");float current,potential;
            if(!IsCurrentAttributeId(id)){error="origin contains unknown Attribute '"+id+"'";return false;}
            if(seen.ContainsKey(id)){error="origin contains duplicate Attribute '"+id+"'";return false;}
            if(!TryFloat(x,"current",out current)||!TryFloat(x,"potential",out potential)||float.IsNaN(current)||float.IsInfinity(current)||float.IsNaN(potential)||float.IsInfinity(potential)){error="origin Attribute '"+id+"' invalid";return false;}
            if(current<0f||current>100f||potential<0f||potential>100f){error="origin Attribute '"+id+"' outside 0..100";return false;}
            seen[id]=true;values.Add(new RebirthWorldOriginAttribute(id,current,potential));
        }
        if(!ValidateCurrentAttributeSet(seen,"origin",out error))return false;
        return true;
    }

    private static bool TryDeserializeProgressionAttributes(XElement node,RebirthWorldProgressionState state,out string error)
    {
        error=string.Empty;Dictionary<string,bool> seen=new Dictionary<string,bool>(StringComparer.OrdinalIgnoreCase);
        if(node==null){error="progression Attributes section is missing";return false;}
        foreach(XElement x in node.Elements("attribute"))
        {
            string id=A(x,"id");float current,potential;
            if(!IsCurrentAttributeId(id)){error="progression contains unknown Attribute '"+id+"'";return false;}
            if(seen.ContainsKey(id)){error="progression contains duplicate Attribute '"+id+"'";return false;}
            if(!TryFloat(x,"current",out current)||!TryFloat(x,"potential",out potential)||float.IsNaN(current)||float.IsInfinity(current)||float.IsNaN(potential)||float.IsInfinity(potential)){error="progression Attribute '"+id+"' invalid";return false;}
            if(current<0f||current>100f||potential<0f||potential>100f){error="progression Attribute '"+id+"' outside 0..100";return false;}
            seen[id]=true;state.Attributes[id]=new RebirthAttributeRuntimeState{AttributeId=id,Current=current,Potential=potential};
        }
        return ValidateCurrentAttributeSet(seen,"progression",out error);
    }

    private static bool ValidateCurrentAttributeSet(Dictionary<string,bool> seen,string owner,out string error)
    {
        error=string.Empty;string[] expected={"strength","dexterity","constitution","intelligence","charisma"};
        for(int i=0;i<expected.Length;i++)if(!seen.ContainsKey(expected[i])){error=owner+" is missing Attribute '"+expected[i]+"'";return false;}
        if(seen.Count!=expected.Length){error=owner+" Attribute count is not exactly five";return false;}
        return true;
    }

    private static bool IsCurrentAttributeId(string id)
    {
        return string.Equals(id,"strength",StringComparison.OrdinalIgnoreCase)||string.Equals(id,"dexterity",StringComparison.OrdinalIgnoreCase)||
            string.Equals(id,"constitution",StringComparison.OrdinalIgnoreCase)||string.Equals(id,"intelligence",StringComparison.OrdinalIgnoreCase)||
            string.Equals(id,"charisma",StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryDeserializeOriginSkills(XElement skillsNode, out Dictionary<string,float> skills, out string error)
    {
        skills=new Dictionary<string,float>(StringComparer.OrdinalIgnoreCase); error=string.Empty;
        if(skillsNode==null){error="origin Skills section is missing";return false;}
        foreach(XElement x in skillsNode.Elements("skill"))
        {
            string id=A(x,"id"); float value;
            if(string.IsNullOrEmpty(id)){error="origin Skill id is blank";return false;}
            if(!RebirthSurvivorSkillMigrationPolicy.IsCurrentSkillId(id)){error="origin contains unknown/non-schema5 Skill '"+id+"'";return false;}
            if(skills.ContainsKey(id)){error="origin contains duplicate Skill '"+id+"'";return false;}
            if(!TryFloat(x,"value",out value)||float.IsNaN(value)||float.IsInfinity(value)){error="origin Skill '"+id+"' value invalid";return false;}
            if(value<RebirthSurvivorSkillMigrationPolicy.RuntimeSkillMin||value>RebirthSurvivorSkillMigrationPolicy.RuntimeSkillMax){error="origin Skill '"+id+"' value out of signed range";return false;}
            skills[id]=value;
        }
        return ValidateCompleteSchema5SkillSet(skills.Keys,"origin",out error);
    }

    private static bool TryDeserializeProgressionSkills(XElement skillsNode, RebirthWorldProgressionState state, out string error)
    {
        error=string.Empty; if(skillsNode==null){error="progression Skills section is missing";return false;}
        HashSet<string> seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(XElement x in skillsNode.Elements("skill"))
        {
            string id=A(x,"id"); float value,progress;
            if(string.IsNullOrEmpty(id)){error="progression Skill id is blank";return false;}
            if(!RebirthSurvivorSkillMigrationPolicy.IsCurrentSkillId(id)){error="progression contains unknown/non-schema5 Skill '"+id+"'";return false;}
            if(!seen.Add(id)){error="progression contains duplicate Skill '"+id+"'";return false;}
            if(!TryFloat(x,"value",out value)||float.IsNaN(value)||float.IsInfinity(value)){error="progression Skill '"+id+"' value invalid";return false;}
            if(!TryFloat(x,"progress",out progress)||float.IsNaN(progress)||float.IsInfinity(progress)){error="progression Skill '"+id+"' progress invalid";return false;}
            if(value<RebirthSurvivorSkillMigrationPolicy.RuntimeSkillMin||value>RebirthSurvivorSkillMigrationPolicy.RuntimeSkillMax){error="progression Skill '"+id+"' value out of signed range";return false;}
            if(progress<0f||progress>=1f){error="progression Skill '"+id+"' progress out of [0,1)";return false;}
            state.Skills[id]=new RebirthSkillRuntimeState{SkillId=id,Value=value,Progress=progress};
        }
        return ValidateCompleteSchema5SkillSet(seen,"progression",out error);
    }

    private static bool TryDeserializeOriginSkillKnowledge(XElement node, out Dictionary<string,float> values, out string error)
    {
        values=new Dictionary<string,float>(StringComparer.OrdinalIgnoreCase); error=string.Empty;
        if(node==null){error="origin Skill Knowledge section is missing";return false;}
        foreach(XElement x in node.Elements("skill"))
        {
            string id=A(x,"id"); float value;
            if(string.IsNullOrEmpty(id)){error="origin Skill Knowledge id is blank";return false;}
            RebirthSkillDefinition skill; if(!RebirthSurvivorDefinitionRegistry.TryGetSkill(id,out skill)||skill==null){error="origin Skill Knowledge references unknown Skill '"+id+"'";return false;}
            if(values.ContainsKey(id)){error="origin contains duplicate Skill Knowledge '"+id+"'";return false;}
            if(!TryFloat(x,"value",out value)||float.IsNaN(value)||float.IsInfinity(value)){error="origin Skill Knowledge '"+id+"' value invalid";return false;}
            if(!IsSkillKnowledgeInRange(value)){error="origin Skill Knowledge '"+id+"' value out of range";return false;}
            values[id]=value;
        }
        return ValidateCompleteSkillKnowledgeSet(values.Keys,"origin",out error);
    }

    private static bool TryDeserializeProgressionSkillKnowledge(XElement node, RebirthWorldProgressionState state, out string error)
    {
        error=string.Empty; if(node==null){error="progression Skill Knowledge section is missing";return false;}
        HashSet<string> seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(XElement x in node.Elements("skill"))
        {
            string id=A(x,"id"); float value;
            if(string.IsNullOrEmpty(id)){error="progression Skill Knowledge id is blank";return false;}
            RebirthSkillDefinition skill; if(!RebirthSurvivorDefinitionRegistry.TryGetSkill(id,out skill)||skill==null){error="progression Skill Knowledge references unknown Skill '"+id+"'";return false;}
            if(!seen.Add(id)){error="progression contains duplicate Skill Knowledge '"+id+"'";return false;}
            if(!TryFloat(x,"value",out value)||float.IsNaN(value)||float.IsInfinity(value)){error="progression Skill Knowledge '"+id+"' value invalid";return false;}
            if(!IsSkillKnowledgeInRange(value)){error="progression Skill Knowledge '"+id+"' value out of range";return false;}
            state.SkillKnowledge[id]=new RebirthSkillKnowledgeRuntimeState{SkillId=id,Value=value};
        }
        return ValidateCompleteSkillKnowledgeSet(seen,"progression",out error);
    }

    private static bool ValidateCompleteSkillKnowledgeSet(IEnumerable<string> ids, string owner, out string error)
    {
        error=string.Empty; HashSet<string> seen=new HashSet<string>(ids??new string[0],StringComparer.OrdinalIgnoreCase);
        RebirthSurvivorDefinitionBundle bundle=RebirthSurvivorDefinitionRegistry.Bundle;
        if(bundle==null||bundle.Progression==null){error=owner+" Skill Knowledge definitions unavailable";return false;}
        List<string> missing=new List<string>();
        for(int i=0;i<bundle.Progression.Skills.Count;i++)
        {
            RebirthSkillDefinition skill=bundle.Progression.Skills[i];
            if(skill!=null&&!seen.Contains(skill.Id))missing.Add(skill.Id);
        }
        if(missing.Count>0){error=owner+" Skill Knowledge missing "+missing.Count+" entries: "+string.Join(",",missing.ToArray());return false;}
        if(seen.Count!=bundle.Progression.Skills.Count){error=owner+" Skill Knowledge count="+seen.Count+" expected="+bundle.Progression.Skills.Count;return false;}
        return true;
    }

    private static bool IsSkillKnowledgeInRange(float value)
    {
        RebirthSurvivorDefinitionBundle bundle=RebirthSurvivorDefinitionRegistry.Bundle;
        return bundle!=null&&bundle.Progression!=null&&value>=bundle.Progression.SkillKnowledgeMin&&value<=bundle.Progression.SkillKnowledgeMax;
    }

    private static bool ValidateCompleteSchema5SkillSet(IEnumerable<string> ids, string owner, out string error)
    {
        error=string.Empty; HashSet<string> seen=new HashSet<string>(ids??new string[0],StringComparer.OrdinalIgnoreCase);
        string[] required=RebirthSurvivorSkillMigrationPolicy.GetCurrentSkillIds(); List<string> missing=new List<string>();
        for(int i=0;i<required.Length;i++) if(!seen.Contains(required[i])) missing.Add(required[i]);
        if(missing.Count>0){error=owner+" schema-5 Skills missing "+missing.Count+" entries: "+string.Join(",",missing.ToArray());return false;}
        if(seen.Count!=required.Length){error=owner+" schema-5 Skill count="+seen.Count+" expected="+required.Length;return false;}
        return true;
    }

    private static bool TryDeserializeCondition(XElement node, out RebirthWorldConditionState state, out string error)
    {
        state=new RebirthWorldConditionState();error=string.Empty;if(node==null){error="condition section is missing";return false;}float mc,mt,ds,hc,dehydration,malnutrition;double active;ulong world;
        state.Stress=RebirthStressState.Read(node.Element("stress"));
        if(!TryFloat(node,"moodCurrent",out mc)||!TryFloat(node,"moodTarget",out mt)||!TryFloat(node,"dietSatisfaction",out ds)||!TryFloat(node,"healthCapacity",out hc)){error="condition scalar invalid";return false;}
        if(!float.TryParse(A(node,"severeDehydrationActiveSeconds"),NumberStyles.Float,CultureInfo.InvariantCulture,out dehydration))dehydration=0f;if(!float.TryParse(A(node,"severeMalnutritionActiveSeconds"),NumberStyles.Float,CultureInfo.InvariantCulture,out malnutrition))malnutrition=0f;
        if(!double.TryParse(A(node,"activePlaySeconds"),NumberStyles.Float,CultureInfo.InvariantCulture,out active)){error="condition activePlaySeconds invalid";return false;}if(!ulong.TryParse(A(node,"lastActiveWorldTime"),NumberStyles.Integer,CultureInfo.InvariantCulture,out world))world=0UL;
        state.MoodCurrent=mc;state.MoodTarget=mt;state.DietSatisfaction=ds;state.HealthCapacity=hc;state.SevereDehydrationActiveSeconds=Math.Max(0f,dehydration);state.SevereMalnutritionActiveSeconds=Math.Max(0f,malnutrition);state.ActivePlaySeconds=Math.Max(0d,active);state.LastActiveWorldTime=world;
        XElement meals=node.Element("recentMeals");if(meals!=null)foreach(XElement x in meals.Elements("meal")){if(state.RecentMeals.Count>=64)break;float q,age;if(!TryFloat(x,"moodQuality",out q)||!TryFloat(x,"ageActiveSeconds",out age))continue;bool compat;bool.TryParse(A(x,"compatibleWithDiet"),out compat);state.RecentMeals.Add(new RebirthRecentMealState{SourceItemId=A(x,"sourceItemId"),VarietyFamilyId=A(x,"varietyFamilyId"),MoodQuality=q,CompatibleWithDiet=compat,AgeActiveSeconds=Math.Max(0f,age)});}return true;
    }

    private static bool TryDeserializeSupport(XElement node,string ownerKey,string creation, out RebirthWorldSupportState state, out string error)
    {
        state=new RebirthWorldSupportState();error=string.Empty;
        if(!RemoteResourceRefundSupportPersistence.TryRead(node,ownerKey,creation,out state.RemoteResourceRefundJournalImage))
        {error="Invalid remote resource refund custody; refusing to discard retained items.";return false;}
        if(node==null || node.Elements("gearTransfers").Count()!=1
            || !RebirthGearTransferPersistence.TryRead(node.Element("gearTransfers"),out state.GearRevision,out state.PendingGearTransfer,out state.GearTransferPhase))
        { error="Invalid gear transfer custody section; refusing to discard pending items.";return false; }
        System.Collections.Generic.List<RebirthAudiobookCassetteState> audioEntries;
        if(node.Elements("audiobooks").Count()>1 ||
            !RebirthAudiobookLibraryPersistence.TryRead(node.Element("audiobooks"),out state.AudiobookRevision,out audioEntries))
        { error="Invalid audiobook custody; refusing to discard stored tapes.";return false; }
        state.AudiobookCassettes.AddRange(audioEntries);
        XElement music=node.Element("music");
        if(music!=null)
        {
            if (!RebirthMusicTransferState.TryRead(music.Element("pendingTransfer"), out state.PendingMusicTransfer))
            { error = "Invalid pending music transfer; refusing to discard item custody."; return false; }
            long revision;
            if(long.TryParse(A(music,"revision"),out revision))state.MusicRevision=Math.Max(0,revision);
            bool shuffle;
            if(bool.TryParse(A(music,"shuffle"),out shuffle))state.MusicShuffle=shuffle;
            foreach(var cassette in music.Elements("cassette"))
            {
                string itemId=A(cassette,"itemId");
                if(!string.IsNullOrEmpty(itemId))state.MusicCassettes.Add(new RebirthMusicCassetteState { ItemId=itemId, ItemData=A(cassette,"itemData") });
            }
        }
        if(!RebirthGearSettlement.TryRead(node,state.GearRevision,out state.LastGearSettlement)||
            state.LastGearSettlement!=null&&state.PendingGearTransfer!=null&&
            (state.LastGearSettlement.TransactionId==state.PendingGearTransfer.TransactionId||
             state.LastGearSettlement.CreationId!=state.PendingGearTransfer.CreationId||
             state.LastGearSettlement.GearRevision>state.PendingGearTransfer.ExpectedRevision))
        {error="Invalid terminal gear settlement; refusing to discard custody.";return false;}
        if(!RebirthGearTerminalOriginalPersistence.TryRead(node,state.LastGearSettlement,out state.LastGearSettlementOriginal))
            {error="Invalid retained original gear settlement.";return false;}
        if(!RebirthBackpackLibrarySettlement.TryRead(node,state.GearRevision,out state.LastLibrarySettlement)
            ||!RebirthBackpackLibraryPersistence.TryRead(node,state.GearRevision,out state.PendingLibraryTransfer,out state.LibraryTransferPhase)
            ||state.PendingLibraryTransfer!=null&&(state.PendingGearTransfer!=null||state.PendingMusicTransfer!=null)
            ||state.LastLibrarySettlement!=null&&state.PendingLibraryTransfer!=null&&
                (state.LastLibrarySettlement.TransactionId==state.PendingLibraryTransfer.TransactionId||
                 state.LastLibrarySettlement.CreationId!=state.PendingLibraryTransfer.CreationId||
                 state.LastLibrarySettlement.GearRevision>state.PendingLibraryTransfer.ExpectedGearRevision))
        {error="Invalid or competing library custody; refusing to discard pending items.";return false;}
        XElement gear=node.Element("gear");if(gear!=null)foreach(XElement x in gear.Elements("slot")){string slot=A(x,"id");string item=A(x,"itemId");if(string.IsNullOrEmpty(slot)||string.IsNullOrEmpty(item))continue;state.EquippedGearBySlot[slot]=item;string itemData=A(x,"itemData");if(!string.IsNullOrEmpty(itemData))state.EquippedGearItemDataBySlot[slot]=itemData;}
        if(!RebirthGearPreparationRefusal.TryRead(node,out state.PendingGearPreparationRefusal)||
            state.PendingGearPreparationRefusal!=null&&!state.PendingGearPreparationRefusal.MatchesSupport(creation,state.GearRevision,
                state.PendingGearTransfer!=null||state.PendingLibraryTransfer!=null||state.PendingMusicTransfer!=null,
                state.LastGearSettlement?.TransactionId))
        {error="Invalid or competing unprepared gear refusal; retaining original custody.";return false;}
        if(!RebirthGearPreparationRefusalRetirement.TryRead(node,out state.LastGearPreparationRefusal)||
            state.LastGearPreparationRefusal!=null&&!RebirthGearPreparationRefusalRetirement.MatchesSupport(state.LastGearPreparationRefusal,
                creation,state.GearRevision,state.PendingGearPreparationRefusal,state.LastGearSettlement?.TransactionId))
        {error="Invalid retired gear refusal acknowledgment.";return false;}
        foreach(XElement x in node.Elements("entry")){float g,m,p,c;int stacks;if(!TryFloat(x,"graceRemainingActiveSeconds",out g)||!TryFloat(x,"managedRemainingActiveSeconds",out m)||!TryFloat(x,"positiveRemainingActiveSeconds",out p)||!TryFloat(x,"cooldownRemainingActiveSeconds",out c))continue;if(!int.TryParse(A(x,"stacks"),NumberStyles.Integer,CultureInfo.InvariantCulture,out stacks))stacks=0;string id=A(x,"profileId");if(string.IsNullOrEmpty(id))continue;state.Entries[id]=new RebirthTraitSupportRuntimeState{SupportProfileId=id,GraceRemainingActiveSeconds=Math.Max(0f,g),ManagedRemainingActiveSeconds=Math.Max(0f,m),PositiveRemainingActiveSeconds=Math.Max(0f,p),CooldownRemainingActiveSeconds=Math.Max(0f,c),Stacks=Math.Max(0,stacks)};}return true;
    }

    private static void AddIssue(string key,string path,string reason,bool recovered){lock(Sync)Issues.Add(new RebirthWorldCharacterPersistenceIssue(key,path,reason,recovered));Log.Warning("[REBIRTH Survivor] world persistence issue key="+(key??string.Empty)+" reason="+(reason??string.Empty));}
    private static string A(XElement e,string n){if(e==null)return string.Empty;XAttribute a=e.Attribute(n);return a!=null?a.Value:string.Empty;}
    private static bool TryFloat(XElement e,string n,out float v){return float.TryParse(A(e,n),NumberStyles.Float,CultureInfo.InvariantCulture,out v);}
    private static string F(float v){return v.ToString("R",CultureInfo.InvariantCulture);}
    private static string D(double v){return v.ToString("R",CultureInfo.InvariantCulture);}
}
