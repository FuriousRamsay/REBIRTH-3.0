import {readFile,writeFile,mkdtemp,rm} from 'node:fs/promises';
import {execFile} from 'node:child_process';
import {promisify} from 'node:util';
import {tmpdir} from 'node:os';
import {join,resolve,dirname} from 'node:path';
import {fileURLToPath} from 'node:url';
// PREPARED, NOT RUN. Parent test checkpoint required for compilation/execution.
const here=dirname(fileURLToPath(import.meta.url)),root=resolve(here,'../..');
const actual=await readFile(join(root,'Scripts/NPC/Inventory/RebirthNpcInventoryTransactions.cs'),'utf8');
const start=actual.indexOf('    internal static RebirthNpcInventoryTransactionResult ApplyLogisticsOriginalPlan(');
const end=actual.indexOf('    // Fetch uses its own retained original',start);
if(start<0||end<=start)throw new Error('Actual NPC plan mutation extraction anchors changed');
const fixture=await readFile(join(here,'mpA_logistics_npc_plan_fixture.cs'),'utf8');
const source='using System;\nusing System.Collections.Generic;\nusing System.Linq;\nusing System.Threading;\nusing System.Xml.Linq;\n'+fixture.replace('/* ACTUAL_NPC_PLAN_METHOD */',actual.slice(start,end));
const temp=await mkdtemp(join(tmpdir(),'rebirth-mpa-npc-plan-')),run=promisify(execFile);
try{
 const cs=join(temp,'check.cs'),exe=join(temp,'check.exe');await writeFile(cs,source);
 await run('C:/Program Files/dotnet/dotnet.exe',['C:/Program Files/dotnet/sdk/9.0.301/Roslyn/bincore/csc.dll','/nologo','/target:exe','/out:'+exe,...['mscorlib','System','System.Core','System.Xml','System.Xml.Linq'].map(x=>'/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/'+x+'.dll'),cs],{windowsHide:true,timeout:10000});
 console.log((await run(exe,[],{windowsHide:true,timeout:10000})).stdout.trim());
}finally{await rm(temp,{recursive:true,force:true});}
