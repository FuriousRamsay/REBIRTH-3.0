// AUTHORED_NOT_RUN. Invoke only after the user's all-coding test checkpoint.
import {readFile,writeFile,mkdtemp,rm} from 'node:fs/promises';
import {execFile} from 'node:child_process';
import {promisify} from 'node:util';
import {tmpdir} from 'node:os';
import {join,resolve,dirname,basename,sep} from 'node:path';
import {fileURLToPath} from 'node:url';
const run=promisify(execFile),here=dirname(fileURLToPath(import.meta.url)),root=resolve(here,'../..');
let fixture=await readFile(join(here,'mpB_054_pending_phase_fixture.cs'),'utf8');
const actual=await readFile(join(root,'Scripts/Crafting/Recycling/RebirthProductiveEarnedNativePendingPhase.cs'),'utf8');
const coordinator=await readFile(join(root,'Scripts/Crafting/Recycling/RebirthProductiveEarnedCoordinator.cs'),'utf8');
const adoption=await readFile(join(root,'Scripts/Crafting/Recycling/RebirthProductiveEarnedCoordinator.RemoteNativeAdoption.cs'),'utf8');
const gateStart=adoption.indexOf('    private static bool IsNativeSettlementInvocation('),gateEnd=adoption.indexOf(';',gateStart);
if(gateStart<0||gateEnd<=gateStart)throw Error('Actual settlement invocation gate missing');
fixture+='\ninternal static partial class RebirthProductiveEarnedCoordinator {\n'+adoption.slice(gateStart,gateEnd+1)+'\n}\n';
const entryStart=coordinator.indexOf('internal static void ObserveEntered('),entryBody=coordinator.indexOf('{',entryStart);
if(entryStart<0||entryBody<0)throw Error('Actual native entry source missing');
let entryEnd=entryBody+1,entryDepth=1;
for(;entryEnd<coordinator.length&&entryDepth;entryEnd++){if(coordinator[entryEnd]==='{')entryDepth++;if(coordinator[entryEnd]==='}')entryDepth--;}
if(entryDepth||fixture.split('// ACTUAL_NATIVE_ENTRY').length!==2)throw Error('Actual native entry extraction changed');
fixture=fixture.replace('// ACTUAL_NATIVE_ENTRY',coordinator.slice(entryStart,entryEnd));
const marker='// ACTUAL_PENDING_PHASE_SOURCE';
if(fixture.split(marker).length!==2)throw Error('Expected one actual source extraction slot');
fixture=fixture.replace(marker,actual.replace(/^using [^;]+;\r?\n/gm,''));
const temp=await mkdtemp(join(tmpdir(),'rebirth-pending-phase-'));
try{
 const cs=join(temp,'fixture.cs'),exe=join(temp,'fixture.exe');await writeFile(cs,fixture);
 await run('C:/Program Files/dotnet/dotnet.exe',['C:/Program Files/dotnet/sdk/9.0.301/Roslyn/bincore/csc.dll','/nologo','/langversion:latest','/target:exe','/out:'+exe,...['mscorlib','System','System.Core'].map(n=>'/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/'+n+'.dll'),cs],{windowsHide:true,timeout:20000});
 process.stdout.write((await run(exe,[],{windowsHide:true,timeout:20000})).stdout);
}finally{
 if(process.env.REBIRTH_KEEP_FIXTURE==='1')process.stdout.write(temp+'\n');
 else{if(!resolve(temp).toLowerCase().startsWith((resolve(tmpdir())+sep).toLowerCase())||!basename(temp).startsWith('rebirth-pending-phase-'))throw Error('Refuse cleanup outside generated fixture');await rm(temp,{recursive:true});}
}
