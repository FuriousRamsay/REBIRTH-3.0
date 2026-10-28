// AUTHORED_NOT_RUN. Deferred until the all-coding validation gate.
import {readFile,writeFile,mkdtemp,rm} from 'node:fs/promises';
import {execFile} from 'node:child_process';import {promisify} from 'node:util';
import {tmpdir} from 'node:os';import {join,resolve,dirname,basename,sep} from 'node:path';import {fileURLToPath} from 'node:url';
const here=dirname(fileURLToPath(import.meta.url)),root=resolve(here,'../..'),run=promisify(execFile);
const source=await readFile(join(root,'Scripts/NPC/Interaction/RebirthNpcTradeOwnerPlan.cs'),'utf8');
const begin=source.indexOf('internal sealed class RebirthNpcTradeOwnerPlan');if(begin<0)throw Error('actual planner missing');
const fixture=(await readFile(join(here,'mp048_owner_plan_fixture.cs'),'utf8')).replace('// ACTUAL_OWNER_PLAN',source.slice(begin));
const temp=await mkdtemp(join(tmpdir(),'rebirth-trade-owner-plan-'));
try{
 const cs=join(temp,'fixture.cs'),exe=join(temp,'fixture.exe');await writeFile(cs,fixture);
 await run('C:/Program Files/dotnet/dotnet.exe',['C:/Program Files/dotnet/sdk/9.0.301/Roslyn/bincore/csc.dll','/nologo','/langversion:latest','/target:exe','/out:'+exe,...['mscorlib','System','System.Core'].map(n=>'/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/'+n+'.dll'),cs],{windowsHide:true,timeout:20000});
 process.stdout.write((await run(exe,[],{windowsHide:true,timeout:20000})).stdout);
}finally{
 if(!resolve(temp).toLowerCase().startsWith((resolve(tmpdir())+sep).toLowerCase())||!basename(temp).startsWith('rebirth-trade-owner-plan-'))throw Error('unsafe fixture cleanup');
 await rm(temp,{recursive:true});
}
