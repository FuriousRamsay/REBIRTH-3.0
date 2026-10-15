# Fetch quest multiplayer trace

Status: source trace in progress; no gameplay executed. Native reference extracts inspected under `E:/_Haven/shared/REBIRTHTemp/FetchNativeInspect`. Reconfirm their assembly provenance against the build used for validation before treating them as final native-version qualification.

## Installed path

`RebirthTraderJobsInstaller.Install` explicitly installs position routing, server storage identity, expected-item binding, count/container callbacks, all four stack guards and identity-based removal. These are active targeted patches rather than uninstalled attributes. The installer sets `installed=true` before patching; the same early-completion retry defect described by MP-013 also applies here and can leave a partial set of identity patches after an installation exception.

`XUiC_RebirthQuestTurnInWorkspace` delegates turn-in lifecycle to the native group. Its custom code changes inspection bindings, layout and trader action presentation; it does not implement a separate reward transaction.

The inspected native `Quest.CloseQuest` sets the final quest state, removes objective hooks, calls each objective's `HandleCompleted`, and subsequently gives completion rewards. `ObjectiveBaseFetchContainer.HandleCompleted` calls `RemoveFetchItems`. Native removal returns void and does not supply a success acknowledgement to quest closure. REBIRTH preserves one-package removal but selects by item type, seed and quest code, preventing the old type-only removal of another quest's package.

## Identity and routing

`RebirthFetchQuestIdentity.Matches` requires matching type and seed and accepts either native Meta or REBIRTH metadata matching the quest code. Position routing first selects an exact stored container position, otherwise the nearest eligible quest whose POI rectangle contains the container. An unresolved position is refused rather than broadcast to every active quest.

Qualification edge: the helper does not prove uniqueness despite the caller's warning text. Two active eligible quests at the same POI/container can select the first/nearest candidate. Establish whether quest admission and POI reservation prohibit that state, including shared quests, before calling this a reachable defect. If reachable, preserve original quest identity in routing rather than inventing an arbitrary tie-breaker.

The server storage hook checks native server role and fetch-satchel block type and derives the quest from the locking player's journal. It adds an identity-bound package only when the storage lacks it. The local container callback additionally checks the player's existing package count. Trace native journal availability on dedicated servers and whether re-opening before journal replication can produce another package; absence of an equivalent check is not by itself a demonstrated duplication.

## Native setter interaction requiring qualification

`RebirthFetchRemovalIdentity` calls Bag.SetSlot or Inventory.SetItem and announces removal without verifying the resulting slot. The native Bag.SetSlot delegates to ItemStackGrid.SetItem. Separately, the installed camper energy owner mutation guard can refuse both setters while original transfer custody is held. This creates a conditional cross-feature failure path: if turn-in is admitted during that hold, quest closure can proceed without removing its package. Determine the UI/transaction admission path and verify actual reachability before implementing a change. Do not disable the energy hold or drop its original transfer custody as a workaround.

## Batched scenarios after all coding

- Two distinct fetch jobs with the same package item type: backpack, belt and partial/full transfer paths retain identity; turning in one removes exactly its own package.
- Host and remote owner, plus shared party completion: prove the owning quest receives the container position and reward exactly once.
- Reopen satchel, leave/rejoin, reload, and simultaneous eligible quests at one POI if admission permits it.
- Outstanding energy transfer hold followed by attempted turn-in: either admission defers safely or package debit and reward remain consistent. Record original operation identity and before/after item counts.
- Inject failure during trader patch installation and retry: every required patch is installed once; no partial identity feature is reported as complete.

Additional energy lifecycle evidence: `RebirthCamperEnergySourceCoordinator.Tick` retires its in-memory pending entry when the original UI/session predicate stops matching, while deliberately retaining durable journal and owner holds. `RebirthCamperEnergyOwnerPayload` requires the current vehicle owner scope for Prepare/Apply, but allows Retire without the open vehicle UI. Thus closing a window does not automatically release inventory custody; qualification must cover a retained hold after window closure, not just simultaneous windows. Resume requires the original frame's owner, creation and actor entity ID. Check native entity-ID persistence across the reconnect topologies before asserting that recovery succeeds after reassignment.

The local source coordinator requests `SaveLocalPlayerData` once per stage; subsequent ticks verify the saved image rather than issuing another save. Include a failed native save followed by ordinary autosave/reopen in the batch to establish recovery latency. This observation is not proof that a normal successful save stalls.

All scenarios are NOT RUN. Native extract review and code inspection are not multiplayer gameplay evidence.
