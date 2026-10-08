import {readFile,writeFile} from 'node:fs/promises';
const path='Scripts/GameBridge/RebirthGameBridgePoiStorage.cs';let s=await readFile(path,'utf8');
function member(signature){const a=s.indexOf(signature);let b=s.indexOf('{',a),n=1;while(n&&++b<s.length){if(s[b]==='{')n++;if(s[b]==='}')n--;}return s.slice(a,b+1);}
const craft=member('    private static IEnumerator CraftCrate(');
let owned=craft.replace('CraftCrate(EntityPlayerLocal p, Action<string> log, Func<bool> canAct, Func<bool> canCleanup = null)','CraftCrateOwned(EntityPlayerLocal p, Action<string> log, Func<bool> canAct, Func<bool> canCleanup, RebirthGameBridgeInput.OwnedInputScope scope)');
owned=owned.replaceAll('RebirthGameBridgeUi.CloseMenus()','RebirthGameBridgeUi.CloseMenus(scope)').replace('RebirthGameBridgeUi.ClickAt(pt, () =>','RebirthGameBridgeUi.ClickAt(pt, scope, () =>').replaceAll('RebirthGameBridgeInput.HoldFrames(action, 3);','if (!scope.TryHoldFrames(action, 3)) yield break;');
s=s.replace('    public static bool CanAccept(',owned+'\n    public static bool CanAccept(');
const start=s.indexOf('    public IEnumerator Deposit(');let dep=s.slice(start);
const marker='        yield return RebirthGameBridgeUi.CloseMenus();';const a=dep.indexOf(marker);if(a<0)throw Error('root scope anchor');let tail=dep.slice(a);
tail=tail.replaceAll('sameContext()','guardedContext()').replaceAll('RebirthGameBridgeUi.CloseMenus()','RebirthGameBridgeUi.CloseMenus(input)').replace('yield return CraftCrate(p, log,','using (var crafting = new RebirthGameBridgeInput.OwnedInputScope(p, () => input.Admitted, input))\n                    yield return RebirthGameBridgeUi.RunGuarded(CraftCrateOwned(p, log,').replace('}, sameContext);','}, guardedContext, crafting), crafting);');
tail=tail.replace('null, "a storage crate", log);','null, "a storage crate", log, input);').replace('RebirthGameBridgeNeeds.Equip(p, slot, CrateName, ok => equipped = ok);','RebirthGameBridgeNeeds.Equip(p, slot, CrateName, ok => equipped = ok, input);').replace('null, "the previously held item", log, restoreMatch);','null, "the previously held item", log, input, restoreMatch);').replace('ok => restored = ok && restoreMatch(p.inventory.GetItem(restoredSlot)));','ok => restored = ok && restoreMatch(p.inventory.GetItem(restoredSlot)), input);').replace('RebirthGameBridgeNeeds.Equip(p, previous, null, ok => { });','RebirthGameBridgeNeeds.Equip(p, previous, null, ok => { }, input);');
tail=tail.replace('RebirthGameBridgeInput.HoldFrames(action, 2);','if (!input.TryHoldFrames(action, 2)) { Failure = input.Failure; break; }');
tail=tail.replace(/                    try\r?\n                    \{\r?\n                        RebirthGameBridgeInput.Shift = true;/,'                    using (var transfer = new RebirthGameBridgeInput.OwnedInputScope(p, () => input.Admitted, input))\n                    {\n                        if (!transfer.TrySetShift(true)) { Failure = transfer.Failure; break; }');
tail=tail.replace('RebirthGameBridgeUi.ClickAt(pt,()=>','RebirthGameBridgeUi.ClickAt(pt,transfer,()=>').replace(/                    finally \{ RebirthGameBridgeInput.Shift = false; \}\r?\n/,'');
tail=tail.replace('finally { RebirthGameBridgeInput.Shift = false; RebirthGameBridgeInput.ReleaseCursor(); }','finally { /* Input cleanup belongs to the exact owned scope below. */ }');
const end=tail.lastIndexOf('        done(Failure == null);');if(end<0)throw Error('scope end');tail=tail.slice(0,end)+tail.slice(end).replace('        done(Failure == null);','        done(Failure == null);\n        }');
const scope=String.raw`        using (var input = new RebirthGameBridgeInput.OwnedInputScope(p, () => sameContext()
            && !ThreatInterrupt(p, 10f, "threat interrupted owned storage input") && CustodyFailure(p) == null))
        {
        Func<bool> guardedContext = () =>
        {
            if (!sameContext()) return false;
            if (input.Admitted) return true;
            if (input.CursorCustodyPending) { RetryAfterThreat = false; Failure = "native cursor item custody remains unresolved"; }
            else if (Failure == null) Failure = input.Failure ?? "owned storage input interrupted";
            return false;
        };
        if (RebirthGameBridgeUi.HeldItemName() != null) input.MarkCursorCustodyPending();
        if (!guardedContext()) { done(false); yield break; }
`;
dep=dep.slice(0,a)+scope+tail;s=s.slice(0,start)+dep;await writeFile(path,s);
