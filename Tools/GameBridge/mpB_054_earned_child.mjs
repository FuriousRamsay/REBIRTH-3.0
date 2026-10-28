// AUTHORED_NOT_RUN. Invoke only after the user's all-coding test checkpoint.
import {readFile,writeFile,mkdtemp,rm} from 'node:fs/promises';
import {execFile} from 'node:child_process';
import {promisify} from 'node:util';
import {tmpdir} from 'node:os';
import {join,resolve,dirname,basename,sep} from 'node:path';
import {fileURLToPath} from 'node:url';
const run=promisify(execFile),here=dirname(fileURLToPath(import.meta.url)),root=resolve(here,'../..');
let fixture=await readFile(join(here,'mpB_054_earned_child_fixture.cs'),'utf8');
const adoption=await readFile(join(root,'Scripts/Crafting/Recycling/RebirthProductiveEarnedCoordinator.RemoteNativeAdoption.cs'),'utf8');
const gateStart=adoption.indexOf('    private static bool IsNativeSettlementInvocation('),gateEnd=adoption.indexOf(';',gateStart);
if(gateStart<0||gateEnd<=gateStart)throw Error('Actual settlement invocation gate missing');
fixture+='\ninternal static partial class RebirthProductiveEarnedCoordinator {\n'+adoption.slice(gateStart,gateEnd+1)+'\n}\n';
const actual=await readFile(join(root,'Scripts/Crafting/Recycling/RebirthProductiveEarnedNativePartialSuccessor.cs'),'utf8');
const wal=await readFile(join(root,'Scripts/Crafting/Recycling/RebirthProductiveEarnedNativeSuccessorRecord.cs'),'utf8');
const child=await readFile(join(root,'Scripts/Crafting/Recycling/RebirthProductiveEarnedNativeChild.cs'),'utf8');
const cleared=await readFile(join(root,'Scripts/Crafting/Recycling/RebirthProductiveEarnedNativeClearedCleanup.cs'),'utf8');
const marker='// ACTUAL_PARTIAL_SUCCESSOR_SOURCE';
if(fixture.split(marker).length!==2)throw Error('Expected one actual source extraction slot');
fixture=fixture.replace(marker,(actual+'\n'+wal+'\n'+child+'\n'+cleared).replace(/^using [^;]+;\r?\n/gm,''));
const temp=await mkdtemp(join(tmpdir(),'rebirth-earned-child-'));
try{
 const cs=join(temp,'fixture.cs'),exe=join(temp,'fixture.exe');await writeFile(cs,fixture);
 await run('C:/Program Files/dotnet/dotnet.exe',['C:/Program Files/dotnet/sdk/9.0.301/Roslyn/bincore/csc.dll','/nologo','/langversion:latest','/target:exe','/out:'+exe,...['mscorlib','System','System.Core'].map(n=>'/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/'+n+'.dll'),cs],{windowsHide:true,timeout:20000});
 process.stdout.write((await run(exe,[],{windowsHide:true,timeout:20000})).stdout);
}finally{
 if(process.env.REBIRTH_KEEP_FIXTURE==='1')process.stdout.write(temp+'\n');
 else{if(!resolve(temp).toLowerCase().startsWith((resolve(tmpdir())+sep).toLowerCase())||!basename(temp).startsWith('rebirth-earned-child-'))throw Error('Refuse cleanup outside generated fixture');await rm(temp,{recursive:true});}
}
