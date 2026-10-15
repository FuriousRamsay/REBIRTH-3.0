# Container rename trace

Read in full: RebirthContainerRenameService, TEFeatureRebirthContainerName, XUiC_RebirthRenameContainer, NetPackageRebirthRenameContainer and NetPackageRebirthContainerNameSync. Registry persistence was previously reviewed under MP-011. No runtime validation performed.

The radial/modal resolves parent blocks, checks access and captures target position. Confirm validates current world and network channel readiness, uses direct authority on a host or sends a request remotely, and closes the modal after submission. Submission is not an acknowledged successful rename: the server may reject changed distance/access. The UI makes no durable transfer and requires no inventory receipt.

The server packet validates main thread, current world, exact Sender/connection membership, entity and persistent identity, and living player. Service rechecks identity, six-metre distance, supported storage and owner/allowlist or remote-resource authorization. Owned storage uses its native tile feature; ordinary POI storage uses the external registry to avoid changing native composite schemas.

The tile feature normalizes names, marks itself modified, copies during upgrade/downgrade and clears on reset/add. Its persistence writes version 1 but reads the version without checking its value; future-format compatibility remains an open persistence concern. Native tile synchronization/dirty propagation must be checked before claiming remote rename persistence fully validated.

External name sync is ToClient, bounded to 128 entries with bounded strings, and current-world/remote-role guarded. Rename request and native feature read a string before normalization truncates it; exact native framing limits are needed to assess allocation exposure. Name normalization removes line breaks/tabs and limits length to 100, but leaves markup characters; display behavior is a UI concern to inspect, not evidence of executable-code injection.

Concurrent authorized renames are effectively last-applied-wins with current authorization rechecked; there is no expected name revision or placement identity in the modal request. Exercise target replacement while the modal is open, permission/distance changes, host/remote names, reset and late join in the later batch. The modal's deliberate cursor centering is separate from station switching, which the user confirmed fixed; do not reopen that resolved report based on this code.

## Storage schema and display dependencies

Read RebirthContainerSizeService, RebirthSecureContainerItem, RebirthBlockPickupSecurityInjector and RebirthLootTitleProjection completely. Sizing runs on server/player-owned storage only, prioritizes the explicit REBIRTH property over legacy names, bounds dimensions to 1..20, and refuses a shrink that would discard an occupied trailing slot. The earlier storage OnAdded hook establishes its placement callsite. Native grid replication remains part of runtime qualification.

Security injection excludes quest storage and only extends generated or explicitly player-storage definitions; existing lockability alone is deliberately insufficient. This preserves ordinary POI composite schemas. Secure recovered-item metadata provides a lock icon, with slot-lock icon taking priority; it is not itself server ownership authorization. Stack separation is enforced by the previously inspected ItemStack hook. Generated variant selection and native item metadata round trips still need full chain qualification.

Loot-title projection is per window/target object and preserves later-arriving native titles. It does not mutate server names. This prevents incorrectly treating a local display cache as shared authority. No new multiplayer defect was established in these four helpers; lifecycle/init-time manager availability in the sizing guard remains a caller-context check rather than an asserted runtime failure.
