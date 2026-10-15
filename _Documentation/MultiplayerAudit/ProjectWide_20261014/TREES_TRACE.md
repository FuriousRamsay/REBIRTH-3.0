# Auto-replant multiplayer trace

Read Scripts/Trees/RebirthAutoReplantTrees.cs in full. Inspected bootstrap installer call and sandbox ApplyRuntimeState call; full sandbox synchronization remains a separate pending audit. No gameplay executed.

The explicit source/sapling mapping binds only after block definitions load, preserving native falling behavior. Binding tracks runtime object and last-written fields before restoring/rebinding, so changed native/third-party baseline values are not blindly replaced with an older captured value. Harvest suppression depends on the same sandbox option on each peer; actual synchronized option delivery must therefore be qualified.

Completion/unload replant refuses remote entities. It checks the live position and only places into air; repeated completion sees the sapling and returns without another write. There is no independent custom network mutation packet. Native SetBlockRPC owns publication.

Verified exact installed native EntityFallingTree methods by direct decompilation, assembly SHA256 FCEEC27300FFD3A1F97B097E43B60F3B07597F441E7ECBC6E1B59EEBB234C705, extract in E:/_Haven/shared/REBIRTHTemp/MultiplayerAuditNative20261014. DestroyTree clears only the original tree type, supporting the replant prefix. MarkToUnload clears the coordinate unconditionally for nonremote entities, supporting the replant postfix placement order. However, the latter native clear occurs BEFORE the mod's live-position guard: that guard cannot protect a replacement block already removed by native unload. Assess concrete falling-tree replacement reachability before deciding whether the mod should intervene; do not call this a newly introduced overwrite bug without that evidence.

Installer sets installed=true before patching, and ModApi sets initialized=true before callback registration. A thrown patch/registration therefore cannot be retried through the same flag. This is another source instance of the existing MP-013 installation-completion issue, to include in the shared correction, not a new numbered issue.

Later grouped tests: option on/off synchronization, remote harvest drop behavior, normal fall, chunk unload during fall, occupied-coordinate handling and reconnect. Inspect both authoritative block and observer projection. Current status NOT RUN.
