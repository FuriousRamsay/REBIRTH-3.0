import {createHash} from 'node:crypto';
import {readFile,writeFile,mkdtemp,unlink,rmdir} from 'node:fs/promises';
import {execFile} from 'node:child_process';
import {promisify} from 'node:util';
import {tmpdir} from 'node:os';
import {join,resolve,dirname} from 'node:path';
import {fileURLToPath} from 'node:url';
const run=promisify(execFile);
const here=dirname(fileURLToPath(import.meta.url));
const root=resolve(here,'../..');
const source=await readFile(join(here,'TransferRetryReview/RebirthGameBridgePoiStorage.cs.proposed.txt'),'utf8');
function member(text,signature){const start=text.indexOf(signature);if(start<0)throw new Error(signature);let end=text.indexOf('{',start),depth=1;while(depth&&++end<text.length){if(text[end]==='{')depth++;if(text[end]==='}')depth--;}if(depth)throw new Error('Unclosed actual member');return text.slice(start,end+1);}
const input=await readFile(join(here,'TransferRetryReview/RebirthGameBridgeInput.cs.proposed.txt'),'utf8'),needs=await readFile(join(root,'Scripts/GameBridge/RebirthGameBridgeNeeds.cs'),'utf8'),ui=await readFile(join(here,'TransferRetryReview/RebirthGameBridgeUi.cs.proposed.txt'),'utf8');
const inputMembers=['    private sealed class Held','    private static void UpdateBindingsPrefix(','    private static bool CheckCursorActive(','    private static void GetScreenPositionPostfix(','    private static void HandleMovementPostfix(','    private static void ShiftPostfix(','    private static void ControlPostfix(','    private static void AltPostfix(','    public static void SetCursor(','    public static void ReleaseCursor(','    public static void HoldFrames(','    public static void HoldSeconds(','    public static void Release(','    public static void ReleaseAll(','    public static bool IsHeld('].map(s=>member(input,s)).join('\n') + '\n' + input.slice(input.indexOf('    private sealed class FlagWrite'),input.indexOf('    /// <summary>UI pacing')) + '\n' + member(input,'    internal sealed class OwnedWearReceipt') + '\n' + member(input,'    public sealed class OwnedInputScope');
const needsMembers=['    public static IEnumerator Equip(','    public static IEnumerator Prepare(EntityPlayerLocal p, Func<ItemClass, bool> want, string supplyItem, string label, Action<string> log, Func<ItemStack, bool> matchStack','    public static IEnumerator Equip(EntityPlayerLocal p, int slot, string expectName, Action<bool> done,','    private static IEnumerator EquipOwned(','    public static IEnumerator Prepare(EntityPlayerLocal p, Func<ItemClass, bool> want, string supplyItem, string label,','    private static IEnumerator DragOwnedInventory(','    private static IEnumerator PrepareOwned('].map(s=>member(needs,s)).join('\n');
const uiMembers=['    private static IEnumerator Pause(','    public static IEnumerator GlideTo(','    public static IEnumerator DragItem(','    public static IEnumerator ClickAt(','    public static IEnumerator RunGuarded(','    public static IEnumerator GlideTo(Vector2 to,','    public static IEnumerator ClickAt(Vector2 point, RebirthGameBridgeInput.OwnedInputScope','    private static IEnumerator ClickOwned(','    public static IEnumerator DragItem(Vector2 from, Vector2 to,','    private static IEnumerator DragOwned(','    internal static ItemStack OwnedCursorStack(','    internal static bool TryOwnedInventorySlot(','    public static IEnumerator CloseMenus(RebirthGameBridgeInput.OwnedInputScope','    private static IEnumerator CloseOwned(','    public static bool TypeInput(string window, string id, string value, RebirthGameBridgeInput.OwnedInputScope','    public static bool ScrollContextBackpack(float delta, RebirthGameBridgeInput.OwnedInputScope'].map(s=>member(ui,s)).join('\n');
const player=await readFile(join(root,'Scripts/GameBridge/RebirthGameBridgePlayer.cs'),'utf8'),path=await readFile(join(root,'Scripts/GameBridge/RebirthGameBridgePath.cs'),'utf8');
const playerMembers=player.slice(player.indexOf('    private static IEnumerator TurnOwned('),player.indexOf('    // ------------------------------------------------------------------ movement'));
const pathMembers=path.slice(path.indexOf('    private static IEnumerator FollowPathOwned('),path.lastIndexOf('}'));
const fixture=await readFile(join(here,'test_poi_full_deposit_proposed_fixture.cs'),'utf8');
const transferPhaseSource=(await readFile(join(here,'TransferRetryReview/RebirthGameBridgePoiTransferPhase.cs.proposed.txt'),'utf8')).replace('using System;','');

const reviewManifest=JSON.parse(await readFile(join(here,'TransferRetryReview/manifest.json'),'utf8'));
for(const entry of reviewManifest){const actual=await readFile(join(root,'Scripts/GameBridge/'+entry.File));const proposal=await readFile(join(here,'TransferRetryReview/'+entry.File+'.proposed.txt'));if(createHash('sha256').update(actual).digest('hex').toUpperCase()!==entry.CurrentSHA256||createHash('sha256').update(proposal).digest('hex').toUpperCase()!==entry.ProposedSHA256)throw Error('review source hash changed '+entry.File);}
const worldSource=await readFile(join(root,'Scripts/GameBridge/RebirthGameBridgeWorld.cs'),'utf8');
const branches=[...worldSource.matchAll(/                if \(!stored\)\r?\n                \{[\s\S]*?                    break;\r?\n                \}/g)].map(m=>m[0]);if(branches.length!==2)throw Error('actual two caller branches');
const caller=branches.map((body,index)=>'static string Branch'+index+'(bool retry){int storageThreatRetries=0;string storageFailure=null;var storage=new FakeStorage{RetryAfterThreat=retry,Failure="fatal"};bool stored=false;Action<string> log=s=>{};for(int pass=0;pass<10;pass++){'+body+'}return storageFailure+"-after-budget:"+storageThreatRetries;}').join('\n')+'\npublic static string Run(bool second,bool retry){return second?Branch1(retry):Branch0(retry);}\nclass FakeStorage{public bool RetryAfterThreat;public string Failure;}';
const depositMembers=[['DEPOSIT','    public IEnumerator Deposit('],['THREAT','    private bool ThreatInterrupt('],['CONTEXT','    private string TripContextFailure('],['CURSOR','    private static bool TransferCursorEmpty('],['SNAPSHOT','    private static string TransferSnapshotKey('],['CUSTODY','    public string CustodyFailure('],['PLACED','    private TEFeatureStorage PlacedLoot('],['TARGET','    private static bool IsDepositTarget(']].map(([tag,signature])=>[tag,member(source,signature)]);
let expanded=fixture;for(const [tag,body]of depositMembers)expanded=expanded.replace('// PRODUCTION_'+tag,body);
expanded=expanded.replace('// PRODUCTION_COUNTS',['    private static bool TryCount(','    private static Dictionary<string, long> CombinedCounts(','    private static void AddCounts(','    private static bool SameCounts(','    private bool OwnedCustodyCurrent('].map(s=>member(source,s)).join('\n')).replace('// PRODUCTION_BRANCHES',caller);

const temp=await mkdtemp(join(tmpdir(),'rebirth-owner-test-'));
try {
 const cs=join(temp,'check.cs'), exe=join(temp,'check.exe');
 await writeFile(cs,expanded.replace('class Checks {',transferPhaseSource+'\nclass Checks {').replace('// PRODUCTION_INPUT',inputMembers).replace('// PRODUCTION_NEEDS',needsMembers).replace('// PRODUCTION_UI',uiMembers).replace('// PRODUCTION_WEAR',member(source,'    internal static bool TryOwnedWearReceipt(')).replace('// PRODUCTION_CRAFT',member(source,'    private static IEnumerator CraftCrateOwned(')));
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



