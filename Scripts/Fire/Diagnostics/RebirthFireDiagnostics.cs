#if DEBUG
using System;

#nullable disable

public static class RebirthFireDiagnostics
{
    public struct Snapshot
    {
        public long ExplosionPatchCalls;
        public long ExplosionSkippedDisabled;
        public long ExplosionSkippedAuthority;
        public long ExplosionSkippedInvalidDamageOrParticle;
        public long ExplosionSkippedSpreadPolicy;
        public long ExplosionNoChangedBlocks;
        public long ExplosionChangedBlockPositions;
        public long ExplosionScheduledPositions;

        public long AddFireMinEventCalls;
        public long AddFireMinEventSkippedDisabled;
        public long AddFireMinEventSkippedInvalidContext;
        public long AddFireMinEventSkippedRange;
        public long AddFireMinEventServerSchedules;
        public long AddFireMinEventClientRequests;
        public long AddFireMinEventPositions;

        public long NetworkSendCalls;
        public long NetworkSendPositions;
        public long NetworkAuthoritativeCalls;
        public long NetworkAuthoritativeIgnitePositions;
        public long NetworkRequestPackages;
        public long NetworkRejectedSender;
        public long NetworkRejectedDisabled;
        public long NetworkRejectedEnum;
        public long NetworkRejectedMissingSource;
        public long NetworkRejectedDistance;
        public long NetworkValidatedPositions;

        public long ServiceIgniteAttempts;
        public long ServiceIgniteAccepted;
        public long ServiceAlreadyBurning;
        public long ServiceRejectedDisabled;
        public long ServiceRejectedNoWorld;
        public long ServiceRejectedNotServer;

        public long ContactWalkCalls;
        public long ContactClientRequests;
        public long ContactHostDirect;
        public long ContactLocalRateLimited;
        public long ContactServerPlayerWalkSuppressed;
        public long ContactRequestsReceived;
        public long ContactRejectedSender;
        public long ContactRejectedDisabled;
        public long ContactRejectedNoFire;
        public long ContactRejectedDistance;
        public long ContactRejectedInvisible;
        public long ContactRejectedImmune;
        public long ContactRejectedInvalidBuff;
        public long ContactBuffApplied;

        public long VisualSpawnAttempts;
        public long VisualLoadAttempts;
        public long VisualUnavailable;
        public long VisualNullPrefab;
        public long VisualSpawnUntracked;
        public long VisualExceptions;
        public long VisualSpawnSuccess;
    }

    public static long ExplosionPatchCalls;
    public static long ExplosionSkippedDisabled;
    public static long ExplosionSkippedAuthority;
    public static long ExplosionSkippedInvalidDamageOrParticle;
    public static long ExplosionSkippedSpreadPolicy;
    public static long ExplosionNoChangedBlocks;
    public static long ExplosionChangedBlockPositions;
    public static long ExplosionScheduledPositions;

    public static long AddFireMinEventCalls;
    public static long AddFireMinEventSkippedDisabled;
    public static long AddFireMinEventSkippedInvalidContext;
    public static long AddFireMinEventSkippedRange;
    public static long AddFireMinEventServerSchedules;
    public static long AddFireMinEventClientRequests;
    public static long AddFireMinEventPositions;

    public static long NetworkSendCalls;
    public static long NetworkSendPositions;
    public static long NetworkAuthoritativeCalls;
    public static long NetworkAuthoritativeIgnitePositions;
    public static long NetworkRequestPackages;
    public static long NetworkRejectedSender;
    public static long NetworkRejectedDisabled;
    public static long NetworkRejectedEnum;
    public static long NetworkRejectedMissingSource;
    public static long NetworkRejectedDistance;
    public static long NetworkValidatedPositions;

    public static long ServiceIgniteAttempts;
    public static long ServiceIgniteAccepted;
    public static long ServiceAlreadyBurning;
    public static long ServiceRejectedDisabled;
    public static long ServiceRejectedNoWorld;
    public static long ServiceRejectedNotServer;

    public static long ContactWalkCalls;
    public static long ContactClientRequests;
    public static long ContactHostDirect;
    public static long ContactLocalRateLimited;
    public static long ContactServerPlayerWalkSuppressed;
    public static long ContactRequestsReceived;
    public static long ContactRejectedSender;
    public static long ContactRejectedDisabled;
    public static long ContactRejectedNoFire;
    public static long ContactRejectedDistance;
    public static long ContactRejectedInvisible;
    public static long ContactRejectedImmune;
    public static long ContactRejectedInvalidBuff;
    public static long ContactBuffApplied;

    public static long VisualSpawnAttempts;
    public static long VisualLoadAttempts;
    public static long VisualUnavailable;
    public static long VisualNullPrefab;
    public static long VisualSpawnUntracked;
    public static long VisualExceptions;
    public static long VisualSpawnSuccess;

    public static long VisualStaleBlockRemovals;

    public static Snapshot Capture()
    {
        return new Snapshot
        {
            ExplosionPatchCalls = ExplosionPatchCalls,
            ExplosionSkippedDisabled = ExplosionSkippedDisabled,
            ExplosionSkippedAuthority = ExplosionSkippedAuthority,
            ExplosionSkippedInvalidDamageOrParticle = ExplosionSkippedInvalidDamageOrParticle,
            ExplosionSkippedSpreadPolicy = ExplosionSkippedSpreadPolicy,
            ExplosionNoChangedBlocks = ExplosionNoChangedBlocks,
            ExplosionChangedBlockPositions = ExplosionChangedBlockPositions,
            ExplosionScheduledPositions = ExplosionScheduledPositions,

            AddFireMinEventCalls = AddFireMinEventCalls,
            AddFireMinEventSkippedDisabled = AddFireMinEventSkippedDisabled,
            AddFireMinEventSkippedInvalidContext = AddFireMinEventSkippedInvalidContext,
            AddFireMinEventSkippedRange = AddFireMinEventSkippedRange,
            AddFireMinEventServerSchedules = AddFireMinEventServerSchedules,
            AddFireMinEventClientRequests = AddFireMinEventClientRequests,
            AddFireMinEventPositions = AddFireMinEventPositions,

            NetworkSendCalls = NetworkSendCalls,
            NetworkSendPositions = NetworkSendPositions,
            NetworkAuthoritativeCalls = NetworkAuthoritativeCalls,
            NetworkAuthoritativeIgnitePositions = NetworkAuthoritativeIgnitePositions,
            NetworkRequestPackages = NetworkRequestPackages,
            NetworkRejectedSender = NetworkRejectedSender,
            NetworkRejectedDisabled = NetworkRejectedDisabled,
            NetworkRejectedEnum = NetworkRejectedEnum,
            NetworkRejectedMissingSource = NetworkRejectedMissingSource,
            NetworkRejectedDistance = NetworkRejectedDistance,
            NetworkValidatedPositions = NetworkValidatedPositions,

            ServiceIgniteAttempts = ServiceIgniteAttempts,
            ServiceIgniteAccepted = ServiceIgniteAccepted,
            ServiceAlreadyBurning = ServiceAlreadyBurning,
            ServiceRejectedDisabled = ServiceRejectedDisabled,
            ServiceRejectedNoWorld = ServiceRejectedNoWorld,
            ServiceRejectedNotServer = ServiceRejectedNotServer,

            ContactWalkCalls = ContactWalkCalls,
            ContactClientRequests = ContactClientRequests,
            ContactHostDirect = ContactHostDirect,
            ContactLocalRateLimited = ContactLocalRateLimited,
            ContactServerPlayerWalkSuppressed = ContactServerPlayerWalkSuppressed,
            ContactRequestsReceived = ContactRequestsReceived,
            ContactRejectedSender = ContactRejectedSender,
            ContactRejectedDisabled = ContactRejectedDisabled,
            ContactRejectedNoFire = ContactRejectedNoFire,
            ContactRejectedDistance = ContactRejectedDistance,
            ContactRejectedInvisible = ContactRejectedInvisible,
            ContactRejectedImmune = ContactRejectedImmune,
            ContactRejectedInvalidBuff = ContactRejectedInvalidBuff,
            ContactBuffApplied = ContactBuffApplied,

            VisualSpawnAttempts = VisualSpawnAttempts,
            VisualLoadAttempts = VisualLoadAttempts,
            VisualUnavailable = VisualUnavailable,
            VisualNullPrefab = VisualNullPrefab,
            VisualSpawnUntracked = VisualSpawnUntracked,
            VisualExceptions = VisualExceptions,
            VisualSpawnSuccess = VisualSpawnSuccess
        };
    }
}
#endif
