import {readFile,writeFile,mkdtemp,unlink,rmdir} from 'node:fs/promises';
import {execFile} from 'node:child_process';
import {promisify} from 'node:util';
import {tmpdir} from 'node:os';
import {join,resolve,dirname} from 'node:path';
import {fileURLToPath} from 'node:url';
const run=promisify(execFile);
const here=dirname(fileURLToPath(import.meta.url));
const root=resolve(here,'../..');
const source=await readFile(join(root,'Scripts/Survivor/Persistence/RebirthStationSnapshotEvidence.cs'),'utf8');
const budget=await readFile(join(root,'Scripts/QuickStack/RebirthLogisticsSnapshotReservation.cs'),'utf8');
const interfaceStart=budget.indexOf('public interface IRebirthStationSnapshotByteBudget');
const interfaceEnd=budget.indexOf('\n}',interfaceStart)+2;
if(interfaceStart<0||interfaceEnd<interfaceStart)throw Error('Actual snapshot budget interface missing');
// This fixture does not request entity evidence. Entity serializer hooks are native tests.
const nonEntity=`public static partial class RebirthStationSnapshotEvidence {
 static void DisposeEntityEvidence(Request r){} static void InvalidateEntityEvidence(object s){}
 static bool ValidateEntityEvidence(Frame f,System.IO.Stream s,byte[] b){return true;}
 static void CommitEntityEvidence(Frame f){} }`;
const cooking=await readFile(join(root,'Scripts/Crafting/Cooking/RebirthCookingOutputPlanPublication.cs'),'utf8');
function body(text,key){const start=text.indexOf(key);if(start<0||text.indexOf(key,start+key.length)>=0)throw Error('Missing/ambiguous '+key);let p=text.indexOf('{',start),depth=0;for(let i=p;i<text.length;i++){if(text[i]==='{')depth++;if(text[i]==='}'&&--depth===0)return text.slice(start,i+1);}throw Error('Incomplete '+key);}
const hook='watch.Placed.OnPublished=(generation,digest)=>watch.Published=true;';
if(cooking.split(hook).length!==2||cooking.indexOf(hook)>cooking.indexOf('regions.SaveChunkSnapshot(chunk,true)'))throw Error('Cooking hook must precede its native save');
const legacyCookingNegative=process.argv.includes('--legacy-cooking-negative');
const cookingFixture=(await readFile(join(here,'test_cooking_publication_receipt_fixture.cs'),'utf8'))
 .replace('// ACTUAL_COOKING_WATCH',body(cooking,'private sealed class Watch:IDisposable'))
 .replace('// ACTUAL_COOKING_PUBLISHED',body(cooking,'private static bool Published(Watch watch)'))
 .replace('// ACTUAL_COOKING_HOOK',legacyCookingNegative?'':hook);
const fixture=(await readFile(join(here,'test_station_snapshot_evidence_fixture.cs'),'utf8')).replace('// CODEC',source+'\n'+budget.slice(interfaceStart,interfaceEnd)+'\n'+nonEntity)+'\n'+cookingFixture;
const temp=await mkdtemp(join(tmpdir(),'rebirth-owner-test-'));
try {
 const cs=join(temp,'check.cs'), exe=join(temp,'check.exe');
 await writeFile(cs,fixture);
 await run('C:/Program Files/dotnet/dotnet.exe',[
 'C:/Program Files/dotnet/sdk/9.0.301/Roslyn/bincore/csc.dll','/nologo','/target:exe','/out:'+exe,
 '/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/mscorlib.dll',
 '/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/System.dll',
 '/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/System.Core.dll',cs],{windowsHide:true,timeout:10000});
 try {
  const result=await run(exe,[],{windowsHide:true,timeout:10000});
  if(legacyCookingNegative)throw Error('Legacy polling unexpectedly passed the recycling control');
  console.log(result.stdout.trim());
 } catch(e) {
  if(!legacyCookingNegative||!String(e.stderr).includes('cooking recycled/scope/closed/missing-readback/wrong-generation 0'))throw e;
  console.log('PASS expected negative: removing the cooking readback callback loses the exact published generation after pool return');
 }
} finally {
 for(const name of ['check.cs','check.exe']) await unlink(join(temp,name)).catch(e=>{if(e.code!=='ENOENT')throw e;});
 await rmdir(temp);
}
