import {readFile,writeFile,mkdtemp,unlink,rmdir} from 'node:fs/promises';
import {execFile} from 'node:child_process';
import {promisify} from 'node:util';
import {tmpdir} from 'node:os';
import {join,resolve,dirname} from 'node:path';
import {fileURLToPath} from 'node:url';
const run=promisify(execFile);
const here=dirname(fileURLToPath(import.meta.url));
const root=resolve(here,'../..');
const source=await readFile(join(root,'Scripts/GameBridge/RebirthGameBridgePoiStorage.cs'),'utf8');
function member(text,signature){const start=text.indexOf(signature);if(start<0)throw new Error(signature);let end=text.indexOf('{',start),depth=1;while(depth&&++end<text.length){if(text[end]==='{')depth++;if(text[end]==='}')depth--;}if(depth)throw new Error('Unclosed actual member');return text.slice(start,end+1);}
const input=await readFile(join(root,'Scripts/GameBridge/RebirthGameBridgeInput.cs'),'utf8'),needs=await readFile(join(root,'Scripts/GameBridge/RebirthGameBridgeNeeds.cs'),'utf8'),ui=await readFile(join(root,'Scripts/GameBridge/RebirthGameBridgeUi.cs'),'utf8');
const inputMembers=['    private sealed class Held','    private static void UpdateBindingsPrefix(','    private static bool CheckCursorActive(','    private static void GetScreenPositionPostfix(','    private static void HandleMovementPostfix(','    private static void ShiftPostfix(','    private static void ControlPostfix(','    private static void AltPostfix(','    public static void SetCursor(','    public static void ReleaseCursor(','    public static void HoldFrames(','    public static void HoldSeconds(','    public static void Release(','    public static void ReleaseAll(','    public static bool IsHeld('].map(s=>member(input,s)).join('\n') + '\n' + input.slice(input.indexOf('    private sealed class FlagWrite'),input.indexOf('    /// <summary>UI pacing')) + '\n' + member(input,'    internal sealed class OwnedWearReceipt') + '\n' + member(input,'    public sealed class OwnedInputScope');
const needsMembers=['    public static IEnumerator Equip(','    public static IEnumerator Prepare(EntityPlayerLocal p, Func<ItemClass, bool> want, string supplyItem, string label, Action<string> log, Func<ItemStack, bool> matchStack','    public static IEnumerator Equip(EntityPlayerLocal p, int slot, string expectName, Action<bool> done,','    private static IEnumerator EquipOwned(','    public static IEnumerator Prepare(EntityPlayerLocal p, Func<ItemClass, bool> want, string supplyItem, string label,','    private static IEnumerator DragOwnedInventory(','    private static IEnumerator PrepareOwned('].map(s=>member(needs,s)).join('\n');
const uiMembers=['    private static IEnumerator Pause(','    public static IEnumerator GlideTo(','    public static IEnumerator DragItem(','    public static IEnumerator ClickAt(','    public static IEnumerator RunGuarded(','    public static IEnumerator GlideTo(Vector2 to,','    public static IEnumerator ClickAt(Vector2 point, RebirthGameBridgeInput.OwnedInputScope','    private static IEnumerator ClickOwned(','    public static IEnumerator DragItem(Vector2 from, Vector2 to,','    private static IEnumerator DragOwned(','    internal static ItemStack OwnedCursorStack(','    internal static bool TryOwnedInventorySlot(','    public static IEnumerator CloseMenus(RebirthGameBridgeInput.OwnedInputScope','    private static IEnumerator CloseOwned(','    public static bool TypeInput(string window, string id, string value, RebirthGameBridgeInput.OwnedInputScope','    public static bool ScrollContextBackpack(float delta, RebirthGameBridgeInput.OwnedInputScope'].map(s=>member(ui,s)).join('\n');
const player=await readFile(join(root,'Scripts/GameBridge/RebirthGameBridgePlayer.cs'),'utf8'),path=await readFile(join(root,'Scripts/GameBridge/RebirthGameBridgePath.cs'),'utf8');
const playerMembers=player.slice(player.indexOf('    private static IEnumerator TurnOwned('),player.indexOf('    // ------------------------------------------------------------------ movement'));
const pathMembers=path.slice(path.indexOf('    private static IEnumerator FollowPathOwned('),path.lastIndexOf('}'));
const fixture=await readFile(join(here,'test_poi_full_crafting_owned_fixture.cs'),'utf8');
const transferPhaseSource=(await readFile(join(root,'Scripts/GameBridge/RebirthGameBridgePoiTransferPhase.cs'),'utf8')).replace('using System;','');
const temp=await mkdtemp(join(tmpdir(),'rebirth-owner-test-'));
try {
 const cs=join(temp,'check.cs'), exe=join(temp,'check.exe');
 await writeFile(cs,fixture.replace('class Checks {',transferPhaseSource+'\nclass Checks {').replace('// PRODUCTION_INPUT',inputMembers).replace('// PRODUCTION_NEEDS',needsMembers).replace('// PRODUCTION_UI',uiMembers).replace('// PRODUCTION_PLAYER',playerMembers).replace('// PRODUCTION_PATH',pathMembers).replace('// PRODUCTION_WEAR',member(source,'    internal static bool TryOwnedWearReceipt(')).replace('// PRODUCTION_CRAFT','const string CrateName="cntStorageGeneric";\n'+member(source,'    private static IEnumerator CraftCrateOwned(')+'\npublic static IEnumerator Craft(EntityPlayerLocal p,Action<string> log,Func<bool> canAct,RebirthGameBridgeInput.OwnedInputScope scope){return CraftCrateOwned(p,log,canAct,()=>true,scope);}'));
 await run('C:/Program Files/dotnet/dotnet.exe',[
 'C:/Program Files/dotnet/sdk/9.0.301/Roslyn/bincore/csc.dll','/nologo','/target:exe','/out:'+exe,
 '/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/mscorlib.dll',
 '/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/System.dll',
 '/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/System.Core.dll',cs],{windowsHide:true,timeout:10000});
 const result=await run(exe,[],{windowsHide:true,timeout:10000});
 console.log(result.stdout.trim());
} finally {
 for(const name of ['check.cs','check.exe']) await unlink(join(temp,name)).catch(e=>{if(e.code!=='ENOENT')throw e;});
 await rmdir(temp);
}



