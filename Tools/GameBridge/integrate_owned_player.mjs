import {readFile,writeFile} from 'node:fs/promises';
const path='Scripts/GameBridge/RebirthGameBridgePlayer.cs';let source=await readFile(path,'utf8');
function method(sig){const a=source.indexOf(sig);if(a<0)throw Error(sig);let b=source.indexOf('{',a),n=1;while(n&&++b<source.length){if(source[b]==='{')n++;if(source[b]==='}')n--;}return source.slice(a,b+1);}
function core(name,signature){let m=method('    public static IEnumerator '+name+'(').replace(/    public static IEnumerator [^{]+\{/,signature+'\n    {\n        if (!scope.Admitted) yield break;');return m;}
let turn=core('TurnToRoutine','    private static IEnumerator TurnOwned(EntityPlayerLocal p, Func<Vector3?> point, float yaw, float pitch, float maxSeconds, RebirthGameBridgeInput.OwnedInputScope scope)');turn=turn.replace('while (Time.realtimeSinceStartup < end && p != null && !p.IsDead())','while (scope.Admitted && Time.realtimeSinceStartup < end && p != null && !p.IsDead())');
let door=core('DoorRoutine','    private static IEnumerator DoorOwned(EntityPlayerLocal p, Vector3i pos, Action<string> done, RebirthGameBridgeInput.OwnedInputScope scope)').replace('ActivateRoutine(dummy, p, () => c, pos, 0f)','ActivateRoutine(dummy, p, () => c, pos, 0f, scope)').replaceAll('RebirthGameBridgeUi.CloseMenus()','RebirthGameBridgeUi.CloseMenus(scope)').replace('BreakDoorRoutine(p, pos, r => broke = r)','BreakDoorRoutine(p, pos, r => broke = r, scope)');
let breakDoor=core('BreakDoorRoutine','    private static IEnumerator BreakDoorOwned(EntityPlayerLocal p, Vector3i pos, Action<string> done, RebirthGameBridgeInput.OwnedInputScope scope)').replace('BreakBlockRoutine(p, pos, tool, r => result = r)','BreakBlockRoutine(p, pos, tool, r => result = r, scope)');
let breaking=core('BreakBlockRoutine','    private static IEnumerator BreakBlockOwned(EntityPlayerLocal p, Vector3i pos, string tool, Action<string> done, RebirthGameBridgeInput.OwnedInputScope scope)');
breaking=breaking.replace('        int previous = p.inventory.holdingItemIdx;',String.raw`        int previous = p.inventory.holdingItemIdx;
        ItemStack original = previous >= 0 && previous < RebirthGameBridgeNeeds.ToolbeltSize(p) ? p.inventory.GetItem(previous) : null;
        string originalKey = RebirthGameBridgePoiStorage.OwnedItemKey(original);
        if (original != null && !original.IsEmpty() && originalKey == null) { scope.Refuse("original tool fingerprint unavailable"); done("original tool custody unresolved"); yield break; }
        string originalName = originalKey != null ? original.itemValue.ItemClass.GetItemName() : null;
        Func<ItemStack, bool> originalMatch = stack => originalKey != null && RebirthGameBridgePoiStorage.OwnedItemKey(stack) == originalKey;`);
breaking=breaking.replace('RebirthGameBridgeNeeds.Prepare(p, ic => ic.GetItemName() == tool, tool, tool, msg => { })','RebirthGameBridgeNeeds.Prepare(p, ic => ic.GetItemName() == tool, null, tool, msg => { }, scope)').replace('RebirthGameBridgeNeeds.Equip(p, slot, name, ok => equipped = ok)','RebirthGameBridgeNeeds.Equip(p, slot, name, ok => equipped = ok, scope)').replace('while (Time.realtimeSinceStartup < until &&','while (scope.Admitted && Time.realtimeSinceStartup < until &&').replace('RebirthGameBridgeInput.HoldFrames(RebirthGameBridgeInput.LocalActions().Primary, 3);','if (!scope.TryHoldFrames(RebirthGameBridgeInput.LocalActions().Primary, 3)) yield break;');
breaking=breaking.replace('        if (previous >= 0 && previous != slot) yield return RebirthGameBridgeNeeds.Equip(p, previous, null, ok => { });',String.raw`        if (originalKey != null && scope.Admitted)
        {
            string foundName;
            int restore = RebirthGameBridgeNeeds.ToolbeltSlot(p, ic => ic.GetItemName() == originalName, out foundName, originalMatch);
            if (restore < 0)
            {
                yield return RebirthGameBridgeNeeds.Prepare(p, ic => ic.GetItemName() == originalName, null, "the original held tool", msg => { }, scope, originalMatch);
                if (!scope.Admitted) yield break;
                restore = RebirthGameBridgeNeeds.ToolbeltSlot(p, ic => ic.GetItemName() == originalName, out foundName, originalMatch);
            }
            bool restored = false;
            if (restore >= 0) yield return RebirthGameBridgeNeeds.Equip(p, restore, originalName, ok => restored = ok && originalMatch(p.inventory.GetItem(restore)), scope);
            if (!restored) { scope.Refuse("original held tool restoration pending"); done("original held tool restoration pending"); yield break; }
        }`);
let activate=core('ActivateRoutine','    private static IEnumerator ActivateOwned(BridgeRequest req, EntityPlayerLocal p, Func<Vector3?> point, Vector3i? block, float holdSeconds, RebirthGameBridgeInput.OwnedInputScope scope)');
activate=activate.replace('ApproachRoutine(p, new Vector3(block.Value.x + 0.5f, block.Value.y + 0.5f, block.Value.z + 0.5f))','ApproachOwned(p, new Vector3(block.Value.x + 0.5f, block.Value.y + 0.5f, block.Value.z + 0.5f), 2f, 60f, scope)').replace('TurnToRoutine(p, point, p.rotation.y, p.rotation.x, 4f)','TurnToRoutine(p, point, p.rotation.y, p.rotation.x, 4f, scope)').replace('TurnToRoutine(p, () => alt, 0f, 0f, 2f)','TurnToRoutine(p, () => alt, 0f, 0f, 2f, scope)').replace('if (holdSeconds > 0f) RebirthGameBridgeInput.HoldSeconds(a.Activate, holdSeconds);','if (holdSeconds > 0f) { if (!scope.TryHoldSeconds(a.Activate, holdSeconds)) yield break; }').replace('else RebirthGameBridgeInput.HoldFrames(a.Activate, 3);','else { if (!scope.TryHoldFrames(a.Activate, 3)) yield break; }');
let walk=method('    private static IEnumerator WalkRoutine(').replace(/    private static IEnumerator WalkRoutine\([^\n]+\)/,'    private static IEnumerator WalkRoutineOwned(BridgeRequest req, EntityPlayerLocal p, Vector3 target, float radius, bool run, float maxSeconds, int generation, Func<string> interruption, bool allowDoors, RebirthGameBridgeInput.OwnedInputScope scope)');walk=walk.replace('        PlayerActionsLocal a =','        if (!scope.Admitted) yield break;\n        PlayerActionsLocal a =').replace('            if (generation != movementGeneration)','            if (!scope.Admitted) { result = scope.Failure ?? "interrupted"; break; }\n            if (generation != movementGeneration)').replaceAll('RebirthGameBridgeInput.HoldFrames(','scope.TryHoldFrames(').replaceAll('RebirthGameBridgeInput.HoldSeconds(','scope.TryHoldSeconds(').replaceAll('RebirthGameBridgeInput.Release(','scope.Release(').replace('DoorRoutine(p, doorPos, msg => doorResult = msg)','DoorRoutine(p, doorPos, msg => doorResult = msg, scope)');
const approach=String.raw`    private static IEnumerator ApproachOwned(EntityPlayerLocal p, Vector3 center, float reach, float maxWalkSeconds, RebirthGameBridgeInput.OwnedInputScope scope)
    {
        if (!scope.Admitted) yield break;
        Vector3 flat = center - p.position; flat.y = 0f;
        if (flat.magnitude <= reach + .4f) yield break;
        var request = new BridgeRequest { Method = "POST", Path = "/walkto" };
        yield return WalkRoutineOwned(request, p, center, reach, flat.magnitude > 12f, maxWalkSeconds, ++movementGeneration, null, true, scope);
    }
    private static IEnumerator ApproachActivateOwned(EntityPlayerLocal p, Vector3i block, float reach, float maxSeconds, RebirthGameBridgeInput.OwnedInputScope scope)
    {
        var request = new BridgeRequest { Method = "POST", Path = "/activate" };
        Vector3 center = new Vector3(block.x + .5f, block.y + .5f, block.z + .5f);
        yield return ApproachOwned(p, center, reach, maxSeconds, scope);
        yield return ActivateOwned(request, p, () => center, block, 0f, scope);
    }
    public static IEnumerator TurnToRoutine(EntityPlayerLocal p, Func<Vector3?> point, float yaw, float pitch, float maxSeconds, RebirthGameBridgeInput.OwnedInputScope parent)
    {
        using (var scope = new RebirthGameBridgeInput.OwnedInputScope(p, () => parent != null && parent.Admitted, parent))
        {
            try { yield return RebirthGameBridgeUi.RunGuarded(TurnOwned(p, point, yaw, pitch, maxSeconds, scope), scope); }
            finally { if (scope.Failure != null) parent?.Refuse(scope.Failure); }
        }
    }
    public static IEnumerator ActivateRoutine(BridgeRequest req, EntityPlayerLocal p, Func<Vector3?> point, Vector3i? block, float holdSeconds, RebirthGameBridgeInput.OwnedInputScope parent)
    {
        using (var scope = new RebirthGameBridgeInput.OwnedInputScope(p, () => parent != null && parent.Admitted, parent))
        {
            try { yield return RebirthGameBridgeUi.RunGuarded(ActivateOwned(req, p, point, block, holdSeconds, scope), scope); }
            finally { if (scope.CursorCustodyPending) parent?.MarkCursorCustodyPending(); else if (scope.Failure != null) parent?.Refuse(scope.Failure); }
        }
    }
    public static IEnumerator ApproachAndActivate(EntityPlayerLocal p, Vector3i block, Action<string> log, float reach, float maxWalkSeconds, RebirthGameBridgeInput.OwnedInputScope parent)
    {
        using (var scope = new RebirthGameBridgeInput.OwnedInputScope(p, () => parent != null && parent.Admitted, parent))
        {
            try { yield return RebirthGameBridgeUi.RunGuarded(ApproachActivateOwned(p, block, reach, maxWalkSeconds, scope), scope); }
            finally { if (scope.CursorCustodyPending) parent?.MarkCursorCustodyPending(); else if (scope.Failure != null) parent?.Refuse(scope.Failure); }
        }
    }
    public static IEnumerator WalkToGuarded(EntityPlayerLocal p, Vector3 target, float radius, float maxSeconds, Func<string> interruption, Action<string> done, RebirthGameBridgeInput.OwnedInputScope parent)
    {
        bool completed = false;
        using (var scope = new RebirthGameBridgeInput.OwnedInputScope(p, () => parent != null && parent.Admitted && (interruption == null || interruption() == null), parent))
        {
            try
            {
                var request = new BridgeRequest { Method = "POST", Path = "/walkto" };
                LastWalkResult = null;
                yield return RebirthGameBridgeUi.RunGuarded(WalkRoutineOwned(request, p, target, radius, false, maxSeconds, ++movementGeneration, interruption, false, scope), scope);
                completed = true; done(scope.Failure ?? LastWalkResult ?? "cancelled");
            }
            finally { if (scope.Failure != null) parent?.Refuse(scope.Failure); if (!completed) done("cancelled"); }
        }
    }
`;
function callbackWrapper(name,extra,argumentsText){return `    public static IEnumerator ${name}(EntityPlayerLocal p, Vector3i pos, ${extra}Action<string> done, RebirthGameBridgeInput.OwnedInputScope parent)\n    {\n        bool completed = false;\n        using (var scope = new RebirthGameBridgeInput.OwnedInputScope(p, () => parent != null && parent.Admitted, parent))\n        {\n            try { yield return RebirthGameBridgeUi.RunGuarded(${name.replace('Routine','Owned')}(p, pos, ${argumentsText}message => { completed = true; done(message); }, scope), scope); }\n            finally { if (scope.CursorCustodyPending) parent?.MarkCursorCustodyPending(); else if (scope.Failure != null) parent?.Refuse(scope.Failure); if (!completed) done(scope.Failure ?? "interrupted"); }\n        }\n    }\n`;}
const members=[turn,door,breakDoor,breaking,activate,walk,approach,callbackWrapper('DoorRoutine','',''),callbackWrapper('BreakDoorRoutine','',''),callbackWrapper('BreakBlockRoutine','string tool, ','tool, ')].join('\n');
const marker='    // ------------------------------------------------------------------ movement';if(!source.includes(marker))throw Error('Player insertion anchor');source=source.replace(marker,members+'\n'+marker);await writeFile(path,source);
