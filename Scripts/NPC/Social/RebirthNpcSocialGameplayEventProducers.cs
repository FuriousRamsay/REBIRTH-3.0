using System;
using System.Globalization;
using System.Threading;
using HarmonyLib;

#nullable disable

/// <summary>
/// Authoritative adapters from concrete gameplay outcomes into the bounded social/faction gateway.
/// Producers publish only after the underlying gameplay mutation has succeeded.
/// </summary>
public static class RebirthNpcSocialGameplayEventProducers
{
    private static long assault, murder, combatSupport, healing, assistance, promiseKept, promiseBroken;
    private static long theft, propertyDamage, rejected, resolverFaults;

    public static bool PublishHealing(RebirthNpcStableId patientId, string actorId, float importance)
    {
        bool ok = Publish(Guid.NewGuid(), patientId, actorId, ResolveFaction(patientId),
            RebirthNpcSocialEventKind.Healing, importance, 1f);
        if (ok) Interlocked.Increment(ref healing); return ok;
    }

    public static bool PublishAssistance(RebirthNpcStableId npcId, string actorId, float importance)
    {
        bool ok = Publish(Guid.NewGuid(), npcId, actorId, ResolveFaction(npcId),
            RebirthNpcSocialEventKind.Assistance, importance, 1f);
        if (ok) Interlocked.Increment(ref assistance); return ok;
    }

    public static bool PublishPromise(RebirthNpcStableId npcId, string actorId, bool kept, float importance)
    {
        RebirthNpcSocialEventKind kind = kept ? RebirthNpcSocialEventKind.PromiseKept : RebirthNpcSocialEventKind.PromiseBroken;
        bool ok = Publish(Guid.NewGuid(), npcId, actorId, ResolveFaction(npcId), kind, importance, 1f);
        if (ok)
        {
            if (kept) Interlocked.Increment(ref promiseKept); else Interlocked.Increment(ref promiseBroken);
        }
        return ok;
    }

    /// <summary>Authority adapter for NPC-owned inventory implementations.</summary>
    public static bool PublishTheft(RebirthNpcStableId victimId, string actorId, float importance, float confidence)
    {
        bool ok = Publish(Guid.NewGuid(), victimId, actorId, ResolveFaction(victimId),
            RebirthNpcSocialEventKind.Theft, importance, confidence);
        if (ok) Interlocked.Increment(ref theft); return ok;
    }

    /// <summary>Authority adapter for NPC/faction-owned block and settlement property implementations.</summary>
    public static bool PublishPropertyDamage(RebirthNpcStableId victimId, string actorId, float importance, float confidence)
    {
        bool ok = Publish(Guid.NewGuid(), victimId, actorId, ResolveFaction(victimId),
            RebirthNpcSocialEventKind.PropertyDamage, importance, confidence);
        if (ok) Interlocked.Increment(ref propertyDamage); return ok;
    }

    public static void OnDamageResolved(EntityAlive target, DamageResponse response, DamageSource source)
    {
        if (!IsAuthoritative() || target == null || response.Strength <= 0) return;
        try
        {
            World world = GameManager.Instance != null ? GameManager.Instance.World : null;
            EntityAlive attacker = source != null && world != null
                ? world.GetEntity(source.getEntityId()) as EntityAlive
                : null;
            if (attacker == null || attacker == target) return;
            string actorId = ResolveActorId(attacker);
            if (string.IsNullOrEmpty(actorId)) return;

            EntityRebirthNPC npcTarget = target as EntityRebirthNPC;
            if (npcTarget != null && npcTarget.RebirthRuntimeState != null)
            {
                RebirthNpcSocialEventKind kind = response.Fatal ? RebirthNpcSocialEventKind.Murder : RebirthNpcSocialEventKind.Assault;
                float importance = response.Fatal ? 1f : Math.Max(0.1f, Math.Min(1f,
                    response.Strength / (float)Math.Max(1, npcTarget.GetMaxHealth())));
                if (Publish(Guid.NewGuid(), npcTarget.RebirthRuntimeState.StableId, actorId,
                    ResolveFaction(npcTarget), kind, importance, 1f))
                {
                    if (response.Fatal) Interlocked.Increment(ref murder); else Interlocked.Increment(ref assault);
                }
                return;
            }

            // A player attacking an entity that is actively targeting a REBIRTH NPC is combat support.
            if (attacker is EntityPlayer)
            {
                EntityRebirthNPC defended = ResolveAttackTarget(target) as EntityRebirthNPC;
                if (defended != null && defended.RebirthRuntimeState != null)
                {
                    float importance = response.Fatal ? 0.8f : 0.35f;
                    if (Publish(Guid.NewGuid(), defended.RebirthRuntimeState.StableId, actorId,
                        ResolveFaction(defended), RebirthNpcSocialEventKind.CombatSupport, importance, 1f))
                        Interlocked.Increment(ref combatSupport);
                }
            }
        }
        catch (Exception ex)
        {
            Interlocked.Increment(ref resolverFaults);
            Log.Warning("[REBIRTH NPC Social Producers] damage event resolution failed: " + ex.Message);
        }
    }

    public static void PublishEconomyOutcome(string source, string actorId, RebirthNpcStableId npcId, string detail, long magnitude, bool committed)
    {
        if (!committed)
            return;

        string normalized = (source ?? string.Empty).Trim().ToLowerInvariant();
        if (normalized.Length == 0)
            return;

        RebirthNpcSocialGameplayGateway.PublishExternalOutcome(
            normalized, actorId, npcId, detail, magnitude);
    }

    public static string GetReport()
    {
        return "[REBIRTH NPC Social Producers] assault=" + Interlocked.Read(ref assault) +
            " murder=" + Interlocked.Read(ref murder) + " combatSupport=" + Interlocked.Read(ref combatSupport) +
            " healing=" + Interlocked.Read(ref healing) + " assistance=" + Interlocked.Read(ref assistance) +
            " promiseKept=" + Interlocked.Read(ref promiseKept) + " promiseBroken=" + Interlocked.Read(ref promiseBroken) +
            " theft=" + Interlocked.Read(ref theft) + " propertyDamage=" + Interlocked.Read(ref propertyDamage) +
            " rejected=" + Interlocked.Read(ref rejected) + " resolverFaults=" + Interlocked.Read(ref resolverFaults);
    }

    private static bool Publish(Guid eventId, RebirthNpcStableId npcId, string actorId, string faction,
        RebirthNpcSocialEventKind kind, float importance, float confidence)
    {
        if (!IsAuthoritative() || npcId.IsEmpty || string.IsNullOrWhiteSpace(actorId))
        { Interlocked.Increment(ref rejected); return false; }
        return RebirthNpcSocialGameplayGateway.Publish(eventId, npcId, actorId.Trim(), faction,
            kind, Math.Max(0f, Math.Min(1f, importance)), Math.Max(0f, Math.Min(1f, confidence)));
    }

    private static bool IsAuthoritative()
    {
        ConnectionManager c = SingletonMonoBehaviour<ConnectionManager>.Instance;
        return c == null || c.IsServer;
    }

    private static string ResolveFaction(RebirthNpcStableId id)
    {
        int entityId; if (!RebirthNpcRuntimeRegistry.TryGetEntityId(id, out entityId)) return "npc";
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        return ResolveFaction(world != null ? world.GetEntity(entityId) as EntityRebirthNPC : null);
    }

    private static string ResolveFaction(EntityRebirthNPC npc)
    {
        if (npc == null || npc.RebirthRuntimeState == null) return "npc";
        RebirthNpcProfile profile;
        if (!RebirthNpcProfileRegistry.TryResolve(npc.RebirthRuntimeState.ProfileId, out profile)) return "npc";
        switch (profile.Category)
        {
            case RebirthNpcCategory.Bandit: return "bandit";
            case RebirthNpcCategory.Survivor: return "survivor";
            case RebirthNpcCategory.DogCompanion:
            case RebirthNpcCategory.PantherCompanion: return "companion";
            case RebirthNpcCategory.SpecialHumanoid: return "special";
            default: return "npc";
        }
    }

    private static string ResolveActorId(EntityAlive actor)
    {
        if (actor == null) return string.Empty;
        EntityPlayer player = actor as EntityPlayer;
        if (player != null && GameManager.Instance != null)
        {
            PersistentPlayerData persistent = GameManager.Instance.GetPersistentPlayerList()
                .GetPlayerDataFromEntityID(player.entityId);
            if (persistent != null && persistent.PrimaryId != null)
                return persistent.PrimaryId.ToString();
        }
        return "entity:" + actor.entityId.ToString(CultureInfo.InvariantCulture);
    }

    private static EntityAlive ResolveAttackTarget(EntityAlive entity)
    {
        return entity != null ? entity.GetAttackTarget() : null;
    }
}

[HarmonyPatch(typeof(EntityAlive), "damageEntityLocal")]
public static class RebirthNpcSocialGameplayEventPatches
{
    public static void Postfix(EntityAlive __instance, DamageSource _damageSource, ref DamageResponse __result)
    { RebirthNpcSocialGameplayEventProducers.OnDamageResolved(__instance, __result, _damageSource); }
}

public static class RebirthNpcSocialGameplayEventPatchInstaller
{
    public const string HarmonyId = "rebirth.3.1.npc.social.gameplay";
    private static readonly Harmony Harmony = new Harmony(HarmonyId);
    private static bool installed;
    public static bool Installed { get { return installed; } }

    public static void Install()
    {
        if (installed) return;
        RebirthHarmonyBootstrap.PatchClassOnce(Harmony, typeof(RebirthNpcSocialGameplayEventPatches));
        installed = true;
    }

}
