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
const a=source.indexOf('    private bool ThreatInterrupt('),b=source.indexOf('    public IEnumerator Deposit(',a);if(a<0||b<a)throw new Error('Production method missing');
const fixture=await readFile(join(here,'test_poi_storage_scope_retry_fixture.cs'),'utf8');
const world=await readFile(join(root,'Scripts/GameBridge/RebirthGameBridgeWorld.cs'),'utf8');
const start=world.indexOf('    public static IEnumerator LootPoiRoutine('),end=world.indexOf('    private static IEnumerator Wait(',start);
const routine=world.slice(start,end);
const branches=[...routine.matchAll(/                if \(!stored\)\r?\n                \{[\s\S]*?                    break;\r?\n                \}/g)].map(m=>m[0]);
if(branches.length!==2)throw new Error('exactly two actual retry branches required');
const methods=branches.map((body,index)=>`static string Branch${index}(bool retry){int storageThreatRetries=0;string storageFailure=null;var storage=new FakeStorage{RetryAfterThreat=retry,Failure="fatal"};bool stored=false;Action<string> log=s=>{};for(int pass=0;pass<10;pass++){${body}}return storageFailure+"-after-budget:"+storageThreatRetries;}`).join('\n')+'\npublic static string Run(bool second,bool retry){return second?Branch1(retry):Branch0(retry);}';
if(!routine.includes('int storageThreatRetries = 0;'))throw new Error('retry counter absent');
const temp=await mkdtemp(join(tmpdir(),'rebirth-owner-test-'));
try {
 const cs=join(temp,'check.cs'), exe=join(temp,'check.exe');
 await writeFile(cs,fixture.replace('// PRODUCTION_CLASS',source.slice(a,b)).replace('// PRODUCTION_BRANCHES',methods));
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


