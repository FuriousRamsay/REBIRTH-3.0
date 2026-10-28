// AUTHORED_NOT_RUN. Invoke only after the user's all-coding test checkpoint.
import {readFile,writeFile,mkdtemp,rm} from 'node:fs/promises';
import {execFile} from 'node:child_process';
import {promisify} from 'node:util';
import {tmpdir} from 'node:os';
import {join,resolve,dirname,basename,sep} from 'node:path';
import {fileURLToPath} from 'node:url';
const run=promisify(execFile),here=dirname(fileURLToPath(import.meta.url)),root=resolve(here,'../..');
let fixture='#define LEGACY_CAPACITY\n'+await readFile(join(here,'test_remote_outcome_receipt_fixture.cs'),'utf8');
const actual=await readFile(join(root,'Scripts/Crafting/RemoteCrafting/RemoteResourceClientTransactions.cs'),'utf8');
const marker='// PRODUCTION_METHOD';
if(fixture.split(marker).length!==2)throw Error('Expected one actual source extraction slot');
const start=actual.indexOf('internal sealed class RemoteResourceTransactionOutcome');
const end=actual.indexOf('[HarmonyPatch(typeof(GameManager), nameof(GameManager.Update))]',start);
if(start<0||end<=start)throw Error('Pull session predicate boundary changed');
const store=(await readFile(join(root,'Scripts/Crafting/RemoteCrafting/RemoteResourceLegacyOutcomeStore.cs'),'utf8')).replace(/^using .*;\r?\n/gm,'');
fixture=fixture.replace(marker,actual.slice(start,end)).replace('// PRODUCTION_STORE',store);
// Keep the full historical boundary and durable production path. Progress from
// the bounded child identifies throughput when a deadline expires; no rows are
// skipped and no synthetic success/publication is substituted.
const seedLoop=' for(ulong request=1;request<=(ulong)maximum;request++)SeedHistorical(world,player,user,request,RemoteResourceClientOperation.Craft,needs,2);';
if(fixture.split(seedLoop).length!==2)throw Error('Historical full-capacity seed boundary changed');
fixture=fixture.replace(seedLoop,' var capacityClock=System.Diagnostics.Stopwatch.StartNew();\n for(ulong request=1;request<=(ulong)maximum;request++){SeedHistorical(world,player,user,request,RemoteResourceClientOperation.Craft,needs,2);if(request==1||request%128==0||request==(ulong)maximum){Console.WriteLine("CAPACITY_PROGRESS originals="+request+"/"+maximum+" elapsed_ms="+capacityClock.ElapsedMilliseconds);Console.Out.Flush();}}');
const temp=await mkdtemp(join(tmpdir(),'rebirth-outcome-capacity-'));
try{
 const cs=join(temp,'fixture.cs'),exe=join(temp,'fixture.exe');await writeFile(cs,fixture);await writeFile(exe+'.config','<?xml version="1.0"?><configuration><startup><supportedRuntime version="v4.0" sku=".NETFramework,Version=v4.8" /></startup><runtime><AppContextSwitchOverrides value="Switch.System.IO.UseLegacyPathHandling=false;Switch.System.IO.BlockLongPaths=false" /></runtime></configuration>');
 await run('C:/Program Files/dotnet/dotnet.exe',['C:/Program Files/dotnet/sdk/9.0.301/Roslyn/bincore/csc.dll','/nologo','/langversion:latest','/target:exe','/out:'+exe,...['mscorlib','System','System.Core','System.Xml','System.Xml.Linq'].map(n=>'/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/'+n+'.dll'),cs],{windowsHide:true,timeout:20000});
 // The capacity boundary persists and flushes 2048 historical originals; allow
 // measured run reached 1664/2048 at 113.8s with steady progress; 165s bounds
 // the estimated ~140s full workload without reducing its durable operations.
 try{process.stdout.write((await run(exe,[],{windowsHide:true,timeout:165000})).stdout);}
 catch(error){if(error.stdout)process.stdout.write(error.stdout);throw error;}
}finally{
 if(process.env.REBIRTH_KEEP_FIXTURE==='1')process.stdout.write(temp+'\n');
 else{if(!resolve(temp).toLowerCase().startsWith((resolve(tmpdir())+sep).toLowerCase())||!basename(temp).startsWith('rebirth-outcome-capacity-'))throw Error('Refuse cleanup outside generated fixture');await rm(temp,{recursive:true});}
}


