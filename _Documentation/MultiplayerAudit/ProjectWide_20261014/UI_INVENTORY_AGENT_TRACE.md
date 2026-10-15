# Inventory, creative and common window audit evidence

Audit-only tranche, 2026-10-15. No production changes, builds, tests, game control or Git commands. Scope excludes station/crafting workspace implementation assigned to another agent. This document does not certify all UI sources. Project operating directives and prior project-wide traces were reviewed; existing MP013 installer partial-failure and prior NPC trade live-grid/publication findings remain their existing findings, not new duplicates.

## Coverage and method

Actual controller method bodies, caller installation snippets, native decompilation and scoped authored XML were reviewed. FULL below means complete executable source and accompanying text read in bounded chunks; partial dependencies and XML are explicitly distinct. Hashes identify bytes, not proof of review. The large CreativeWorkspace diagnostics body remains partial; production lines1–436 were completely read, diagnostic437–655 and approximately758–965 were read, remainder not certified. No native or XML whole-file claim is made from searching. No layout/visual correctness is claimed without game observation.

## Concrete finding UI-INV-A: trash control still addresses native bars

Config/XUi_InGame/windows.xml1135–1144 authors native Toolbelt inventory and inventory2 pools of10 each. First btnClearInventory0 is visible only with creativewindowopen; second btnClearInventory1 is unconditionally visible=false. XUiC_RebirthToolbeltLayout.Update lays all owned real slots in one row, enables second inventory pool, and positions both trash controls after the final usable slot; it never changes secondClear visibility or handlers. RebirthToolbeltCapacity supplies20 backing public slots and up to18 usable slots, with a four-slot base. Its shortcut/focus patches deliberately avoid changing native ToolbeltConfig.

Native XUiC_ToolbeltWindow.Init binds only BarsPerPage buttons to BtnClearInventory_OnPress. The callback computes start=CurrentPage*SlotsPerPage+barIndex*SlotsPerBar and calls the primary local player's EmptyToolbelt(start,start+SlotsPerBar). Native ToolbeltConfig defaults SlotsPerBar10, BarsPerPage1, PageCount1; no REBIRTH source writes to these config fields were found. Native EntityPlayerLocal.EmptyToolbelt simply calls inventory.SetItem(i,Empty) over that range. Thus on the default native config the sole visible trash control clears0..9, rather than all of REBIRTH's displayed0..17. Slots10..17 remain even while the button is located after them. At usable capacity4 it also addresses retained backing4..9, whose empty writes are expressly allowed by RebirthToolbeltSetItemAuthorityPatch. This is a concrete source/UI contract mismatch, local-owner destructive action, not a network packet authorization defect. If another mod changes native bars/pages, the exact range changes; this audit did not load the merged runtime XML or third-party mods. Native click callback has no additional creative authorization check, but visibility is the actual normal user route; no remote exploit is claimed.

## Selected item ownership and action behavior

Toolbelt custom slot derives native ItemStack, keeps base mutation behavior, then routes selection to quest rewards/editor/character/context/crafting as available. It validates same xui for main owners before selecting. CharacterBackpack reads the actual bag through RebirthCraftingInventoryBridge; slot-change events write a clone to exactly the changed physical Bag.SetSlot index, never resize by writing a presentation array. Filter affects visibility, not physical storage. Sort persists locks then calls native PlayerInventory.SortStacks.

SelectionSource switches to the cursor's ItemStackControl while cursor is nonempty. RefreshSelection snapshots source identity, stack and liquid fingerprint; a different source invalidates actionType. It regenerates native action lists for real selected sources, removes all ordinary ItemActionEntryDrop entries, and adds RebirthCursorDropEntry only for cursor ownership. Consequently selected real-slot Drop is intentionally absent: the user must pick up the stack to obtain the cursor Drop action. This is actual behavior, not an inferred transaction failure. Survivor gear uses the existing equip transaction entry; creative catalogue sources have Use/Equip/Wear/custom gear/literature use stripped. Whether that matches the user's desired selected-item UX requires product judgment; no new inventory duplication finding established.

RebirthCursorDropEntry refreshes enabled state against primary local player/current world/current entity, exact cursor ItemController identity and live library/remote/camper holds; activation refreshes again, compares cursor snapshot, then calls native DropCurrentItem. SelectedItemSalePrice and ItemSaleEstimate are display-only native sale quotes and do not debit/credit currency. ItemActionList hides empty presentation entries without replacing transaction handlers. InfoWindowLookup replaces native lookup with a per-xui weak cache keyed root identities; a missing transpiler match leaves native lookup behavior (performance loss, not item authority loss).

## Creative catalogue is a native source, not a second inventory

xui.xml209–235 conditionally replaces creative window children with rebirthCreativeRoot only for character_progression('Rebirth'), disables native open_backpack_on_open. windows.xml root controller is RebirthCreativeWorkspace, catalogue controller RebirthCreativeCatalogueWindow; parsed authored XML contains182 RebirthCreativeStatsStack cells, matching13 columns x14 pooled rows expected by CatalogueGrid (12 rows visible). Authored source cells disallow dropping. This checks controller registration/cardinality, not merged XML or render quality.

CatalogueWindow retains native Creative2Window filtering, coalescing refresh by.12s. CatalogueGrid reads native Creative2 data, disconnects physical SlotChangedEvent handling, and binds clone source stacks to the pool. Native XUiC_Creative2Stack.SwapItem clones its current source onto the cursor; setItemStack only changes the catalogue source view and HandleDropOne is empty. Thus the no-op backend callback preserves creative-source semantics rather than losing a real bag write. CatalogueIndex and PoolIndex are deliberately separate; native SlotNumber remains the pool index. Rebinding clears selection of a recycled source. Input/cursor/locked cells inhibit catalogue navigation; pending changes wait until safe. Native per-frame page binding is bypassed explicitly (page0/IsDirtyfalse) because this grid performs source virtualization.

Creative production source checks active instance/open state and resets selection/hover/action sets on close; close postfix also resets action sets after native creative closure. Slot culling disables visual descendants and hit colliders, not physical roots/SlotNumber/controller parents. Character backpack real inventory dirtiness takes native Update immediately, unlike catalogue source dirty coalescing. Wake forces native preparation, view flush and collider restoration. The presentation grouping catch restores children when possible and falls back native if construction fails. No item conservation defect found in these reviewed paths. Conditional REBIRTH_UI_DIAGNOSTICS code is not enabled by the inspected csproj normal Debug/Release definitions; its unread remainder is excluded.

## Lock mode input and persistence boundary

SlotLockInput covers the explicitly listed custom real slots/context slots. It caches overlay rectSlotLock and ancestor userlockmode binding per slot/Parent; reads live binding on every check. While lock mode is active, native Update has EventOnPress and isOver masked, with a finalizer restoring both even on exceptions. Five native zero-argument mutation methods are separately prefixed at First priority before library/camper patches. Missing target throws at installation rather than silently omitting protection. Lock overlay receives the click; native pickup/swap/split/quick-transfer does not also consume it. Existing NativeControls early installed-flag failure can prevent reaching this installer (MP013 overlap). SlotLockInput itself sets installed only after both class patches; a second class failure can leave first installed before retry, no rollback shown.

CharacterBackpack persists lock state on toggling, sorting, close and while effective lockmode; delegates durable/native mask writes to CraftingInventoryBridge, which is a cross-tranche dependency rather than certified here. Context slot lock changes update the real native source UserLockedSlot and native UpdateLockedSlotStates, with explicit flush on close/suspend. These are sorting/transfer protection locks, not exclusive server ownership leases. UI authority remains local to the player's xui; no new authenticated network endpoint is implemented in these files. Dedicated server has no player-facing UI; host and remote owner both execute the UI branch, while native container lock/transport remains responsible for shared storage authority.

## Common container workspace retains native session ownership

ContextNavigationService creates a presentation lease only for supported native loot/storage source and capacities (container<=198, backpack<=176). It hides native container/backpack views while leaving native group/session/network lock ownership alive. Begin validates views/pools before replacing presentation; failure restores native. Installer optional context patch results are ANDed into IntegrationReady, preventing Begin when only a subset installed. Earlier patch hooks may remain installed after a later failure, but Begin remains gated; installed prevents another optional retry this session. This is fallback availability, not established partial-mutation corruption.

Workspace mirrors physical source indices and calls actual native source controllers for selected actions. Grid filtering compacts views, not inventory indices. Commands call native Sort/MoveFillAndSmart/MoveAll/ToggleLockMode controls. Writes use real bag.SetSlot or original loot/storage slot-changed event with a cloned stack. Suspended navigation blocks writes/quick transfers; cursor is resolved via native PlaceItemBackInInventory before suspending. Return reopens required groups and restores modal/action routing. Original loot/bag roots retain native interaction callbacks. End/close flush locks and restore saved presentation for logically open groups. Capacity mismatch falls back by closing custom interaction.

Maintain checks world existence and native interaction distance but does not bind the session to an exact world object/generation. Normal native close hooks clear Session; abnormal world teardown without native close could retain a stale UI/session reference. No reachable native teardown bypass was demonstrated, so this is a qualified lifecycle dependency, not a confirmed cross-world mutation defect.

BulkTransferRefresh uses thread-static nesting only around native StashItems, postpones presentation refresh callbacks, then drains in a finalizer. It does not suppress native mutation or native slot publication. First refresh exception is retained/rethrown after cleanup, with state finally cleared. No claimed suppression/replication defect from batching.

## Capacity recovery and multiplayer limitation

Constructor capacity integration occurs before native Hand binds its action arrays; runtime mismatch logs rather than resizing live ItemGrid alone. Nonempty writes/adds into unusable slots are rejected; empty writes remain allowed for recovery. Selected-slot/Hand/shortcut/focus bounds use usable count. Fresh_Init94–96 installs info lookup and capacity through named RunStepOnce steps; class installation uses PatchClassOnce. NativeControls separately installs lock input. Metabolism spawn292/306 and Survivor creation203 call SanitizePlayerStartup, which removes startup artifacts and then RecoverLockedSlots. This is actual lifecycle reachability; no periodic recovery on every gear capacity decrease was found in the searched callers.

RecoverLockedSlots clones each locked source, finds an empty bag slot and calls void Bag.SetSlot, then clears source via void Inventory.SetItem; alternatively ItemDropServer then source clear. It does not verify destination/source postimages. A setter refusal or partial drop callback can therefore break conservation under an overlapping rejecting transaction/hold; this audit did not prove that such a hold survives into these startup callers. Treat as unproven failure-path concern, not a reproduced duplication/loss bug. The remote player's server live inventory versus uploaded owner save-domain mismatch and delivery publication omission are already established in NPC_TRADE_AGENT_TRACE; this UI tranche does not independently establish which topology executes each startup recovery call or assert that native owner replication is suppressed. Prior native SetItem/Bag callbacks and owner upload evidence should be used for that follow-up, not replaced with generic authority assumptions.

## Explicit limits

Native ToolbeltWindow callback/Init, ToolbeltConfig complete type, Creative2Stack complete type, and EmptyToolbelt method were directly decompiled/read from exact installed assembly. Other native methods here rely on already documented native traces or clearly identified dependencies. No game run, click test, save/reload, merged-XUi load or packet capture occurred. No station workspace/crafting source ownership claimed. CharacterBackpack complete source reviewed across bounded reads; CreativeWorkspace production only complete, diagnostics partial. Shared reporting/coverage files unchanged.

## Full-read source manifest (SHA256)
- FULL `Scripts\UI\RebirthInventorySlotLockInput.cs` | 7D4C7B762E1B11A7A9B32DF0712C72D85F08765374F8E3A28248D78A4EAB3C23
- FULL `Scripts\UI\XUiC_RebirthBackpackSectionControls.cs` | 0036121A2B60FF50078BD71D9658938C3C7925296B8534E0F37744BC169C2EB0
- FULL `Scripts\UI\XUiC_RebirthBackpackSectionNavigationChrome.cs` | 1D23993AC8BA2CE2494CACCABDE37F11DEE23E3C9A54E083CF3D7CCF9C0D9CE9
- FULL `Scripts\UI\XUiC_RebirthToolbeltItemSlot.cs` | 37BB1910A98AA8C8AF0F8BEDF1975E27DFB652FD8B4546270F769156996ACEC7
- FULL `Scripts\UI\XUiC_RebirthToolbeltLayout.cs` | 69226F3734014EE7F57000E91A36174B39BEAB93A84F0375F347D079ACF01CD6
- FULL `Scripts\UI\RebirthToolbeltCapacity.cs` | 7D200F5431A0AFD7192168E6EBF640D13B9F987FEF9BAA71394D3EA69858E5D7
- FULL `Scripts\UI\RebirthWindowInventoryScope.cs` | 7A0814C8E1CA46E2320A76EDAD647E4009F1666CEB8482F1CF9830888C4BF2FD
- FULL `Scripts\UI\RebirthWindowReturn.cs` | D3B7CBBE33C58B8C62BF1F61741FBBB906F27C412A4CCE0AC8585010AFD18A2E
- FULL `Scripts\UI\RebirthCursorDropEntry.cs` | FB7BF31EF9C075FE9BF7C3C477EBE07EBFA4D3055561815B45AA2678CD93AF12
- FULL `Scripts\UI\RebirthInventoryInfoWindowLookup.cs` | B5C632517813928DEDF0F758EDE86BBC2160C9871A4E97D0419E0B1867D54589
- FULL `Scripts\UI\XUiC_RebirthCreativeAndConversationChrome.cs` | 468F92AA4CBA5CF214CC2A7B38943FC4B75D6E5CCC7046ACEA509129C5453B90
- FULL `Scripts\UI\RebirthCreativeCloseInputPatch.cs` | 9EBE452F7CD3131F1ED17A27C4AC9160662860C5FDA0CF27D2520279F7AE228C
- FULL `Scripts\UI\RebirthContextNavigationService.cs` | B8F5BE79AB8ECBAEEF2373E876628655033EA37A34E7BDF6DC49D25322C42AAB
- FULL `Scripts\UI\XUiC_RebirthContainerWorkspace.cs` | 3CD0DBB6235403AB9E5847CCE27BDFBF6B207EBE3971474F946E8CE02AA2174B
- FULL `Scripts\UI\XUiC_RebirthCreativeCatalogueGrid.cs` | 2FF7BF695CE7C608926C92CD226A07EEB5E6FC55DBDFF8326F012CE909BE83E3
- FULL `Scripts\UI\RebirthBulkTransferRefresh.cs` | 0CF1A99BFF725C4DA9F359017089A84644B741C8AF0ED7816528FE7962B16C7C
- FULL `Scripts\UI\XUiC_RebirthItemActionList.cs` | 1668B88D3671ACC37498D0D1F1A91D3E3918A7F7DCC5F3502FBD07DF4B3B9939
- FULL `Scripts\UI\XUiC_RebirthSelectedItemSalePrice.cs` | EED4C1722FB40B35D8E303FF566DC6C7CD8333D224665DA07161B3500AF6B973
- FULL `Scripts\UI\RebirthItemSaleEstimate.cs` | CC61E1A9D63931CC99DF04F16505289F8B1C84D9128FA3236BF9926CEC0EF208
- FULL `Scripts\UI\RebirthScreenLayout.cs` | 64D9C2B96D4DDB6CEADA3C5EC4D195193E1A99BBD47AF72A853404C8DF854E0C
- FULL `Scripts\Survivor\UI\XUiC_RebirthCharacterBackpack.cs` | A54476D913E7A9CF54DF0346E1D39404CFCED2A6B8C4304C99A09436628B5771

## Partial dependency hashes (not full-read claims)
- PARTIAL `Scripts/UI/XUiC_RebirthCreativeWorkspace.cs` | 16C13C8FC7927CE77F6C36D4E6B77A74B655D46509C8E3E9508404B4A8BCA8B3
- PARTIAL `Scripts/Survivor/UI/RebirthSurvivorUiInstaller.cs` | 46BD2EF348FB9ED0BE1D3B1D127039A2BCFA193C6E88B36AE33731212CFF9170
- PARTIAL `Scripts/Input/RebirthNativeControls.cs` | 19A0530451B1DB5FD09C2F7B7699E905BD59FE655313A6240339701DDA07D34C
- PARTIAL `Scripts/Bootstrap/RebirthFresh_Init.cs` | 002E8D028A646C7063C5C18FFA9D1882988BB906BF9447B5658EC38E8E9CBA95
- PARTIAL `Scripts/Metabolism/RebirthMetabolismInstaller.cs` | 0C6C6A2EC85EED1696F2ECB9BBCDCE2D0DE03E85A384FBC833DED81275C45875
- PARTIAL `Scripts/Survivor/Creation/RebirthSurvivorCreationService.cs` | 4AE8B771496395106668CCDC864D0A2CACF2885DA368B5E8B085D772D9F043EA
- PARTIAL `Config/XUi_InGame/windows.xml` | D7D2B5BF991A2F5318A3F5591A9F883E43B7415194C2209042ED9A59253B0E8C
- PARTIAL `Config/XUi_InGame/xui.xml` | B6058445A6CE5C99E1998FD4A23B3E63F1C096040AEBB6E913AF6EEB39DD7B48
- PARTIAL `RebirthUtils.csproj` | EA05C33A3B17BC04CCDFEE806F01E338377596BE513D183DC93F71BE88BEC141
- Native Assembly-CSharp.dll SHA256: FCEEC27300FFD3A1F97B097E43B60F3B07597F441E7ECBC6E1B59EEBB234C705 (method/type scoped reads).

Registration cross-check: parsed authored btnClearInventory0 ancestor is toolbelt controller=Toolbelt inside windowToolbelt controller=ToolbeltWindow, so the inspected native callback is the actual XML owner. RebirthBulkTransferRefresh.Install(Harmony) is called at SurvivorUiInstaller42 before optional context patch admission.
