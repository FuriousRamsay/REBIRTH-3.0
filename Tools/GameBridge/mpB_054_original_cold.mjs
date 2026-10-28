// AUTHORED_NOT_RUN. Invoke only after the user's all-coding test checkpoint.
import {readFile,writeFile,mkdtemp,rm} from 'node:fs/promises';
import {execFile} from 'node:child_process';
import {promisify} from 'node:util';
import {tmpdir} from 'node:os';
import {join,resolve,dirname,basename,sep} from 'node:path';
import {fileURLToPath} from 'node:url';
const run=promisify(execFile),here=dirname(fileURLToPath(import.meta.url)),root=resolve(here,'../..');
let fixture=await readFile(join(here,'mpB_054_original_cold_fixture.cs'),'utf8');
const actual=await readFile(join(root,'Scripts/Crafting/Recycling/RebirthProductiveEarnedSavedNativeSource.Child.cs'),'utf8');
const original=await readFile(join(root,'Scripts/Crafting/Recycling/RebirthProductiveEarnedSavedNativeSource.Original.cs'),'utf8');
const initial=await readFile(join(root,'Scripts/Crafting/Recycling/RebirthProductiveEarnedSavedNativeSource.InitialSuffix.cs'),'utf8');
const producer=await readFile(join(root,'Scripts/Crafting/Recycling/RebirthProductiveEarnedInitialNativeSuffix.cs'),'utf8');
const readStart=producer.indexOf('    internal const int MaximumInitialNativeSuffix='),readEnd=producer.indexOf('    internal static bool EffectiveNativeAfter(',readStart);
if(readStart<0||readEnd<=readStart)throw Error('Actual initial suffix reader boundary changed');
fixture+='\n'+initial.replace(/^using [^;]+;\r?\n/gm,'')+'\ninternal static partial class RebirthProductiveEarnedCoordinator {\n'+producer.slice(readStart,readEnd)+'\n}\n';
const record=await readFile(join(root,'Scripts/Crafting/Recycling/RebirthProductiveEarnedNativeSuccessorRecord.cs'),'utf8');
const marker='// ACTUAL_CHILD_COLD_SOURCE';
if(fixture.split(marker).length!==2)throw Error('Expected one actual source extraction slot');
fixture=fixture.replace(marker,(actual+'\n'+original+'\n'+record).replace(/^using [^;]+;\r?\n/gm,''));
const temp=await mkdtemp(join(tmpdir(),'rebirth-original-cold-'));
try{
 const cs=join(temp,'fixture.cs'),exe=join(temp,'fixture.exe');await writeFile(cs,fixture);
 await run('C:/Program Files/dotnet/dotnet.exe',['C:/Program Files/dotnet/sdk/9.0.301/Roslyn/bincore/csc.dll','/nologo','/langversion:latest','/target:exe','/out:'+exe,...['mscorlib','System','System.Core'].map(n=>'/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/'+n+'.dll'),cs],{windowsHide:true,timeout:20000});
 process.stdout.write((await run(exe,[],{windowsHide:true,timeout:20000})).stdout);
}finally{
 if(process.env.REBIRTH_KEEP_FIXTURE==='1')process.stdout.write(temp+'\n');
 else{if(!resolve(temp).toLowerCase().startsWith((resolve(tmpdir())+sep).toLowerCase())||!basename(temp).startsWith('rebirth-original-cold-'))throw Error('Refuse cleanup outside generated fixture');await rm(temp,{recursive:true});}
}
