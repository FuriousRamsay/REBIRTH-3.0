// AUTHORED_NOT_RUN. Invoke only after the user's all-coding test checkpoint.
import {readFile,writeFile,mkdtemp,rm} from 'node:fs/promises';
import {execFile} from 'node:child_process';
import {promisify} from 'node:util';
import {tmpdir} from 'node:os';
import {join,resolve,dirname,basename,sep} from 'node:path';
import {fileURLToPath} from 'node:url';
const run=promisify(execFile),here=dirname(fileURLToPath(import.meta.url)),root=resolve(here,'../..');
let fixture=await readFile(join(here,'mpB_054_earned_registration_fixture.cs'),'utf8');
const actual=await readFile(join(root,'Scripts/Crafting/Recycling/RebirthProductiveEarnedSavedNativeSource.Registration.cs'),'utf8');
const startup=await readFile(join(root,'Scripts/Crafting/Recycling/RebirthProductiveEarnedSavedNativeSource.Startup.cs'),'utf8');
const suffixStart=startup.indexOf('    private static readonly string[] startupPublicationSuffixes='),suffixEnd=startup.indexOf(';',suffixStart);
const pathStart=startup.indexOf('    private static string StartupOriginalPath('),pathEnd=startup.indexOf('    private static string SelectStartupPublication(',pathStart);
if(suffixStart<0||suffixEnd<=suffixStart||pathStart<0||pathEnd<=pathStart)throw Error('Actual startup path boundary changed');
fixture+='\ninternal static partial class RebirthProductiveEarnedSavedNativeSource {\n'+startup.slice(suffixStart,suffixEnd+1)+'\n'+startup.slice(pathStart,pathEnd)+'\n}\n';
const marker='// ACTUAL_EARNED_REGISTRATION_SOURCE';
if(fixture.split(marker).length!==2)throw Error('Expected one actual source extraction slot');
fixture=fixture.replace(marker,actual.replace(/^using [^;]+;\r?\n/gm,''));
const temp=await mkdtemp(join(tmpdir(),'rebirth-earned-registration-'));
try{
 const cs=join(temp,'fixture.cs'),exe=join(temp,'fixture.exe');await writeFile(cs,fixture);
 await run('C:/Program Files/dotnet/dotnet.exe',['C:/Program Files/dotnet/sdk/9.0.301/Roslyn/bincore/csc.dll','/nologo','/langversion:latest','/target:exe','/out:'+exe,...['mscorlib','System','System.Core'].map(n=>'/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/'+n+'.dll'),cs],{windowsHide:true,timeout:20000});
 process.stdout.write((await run(exe,[],{windowsHide:true,timeout:20000})).stdout);
}finally{
 if(process.env.REBIRTH_KEEP_FIXTURE==='1')process.stdout.write(temp+'\n');
 else{if(!resolve(temp).toLowerCase().startsWith((resolve(tmpdir())+sep).toLowerCase())||!basename(temp).startsWith('rebirth-earned-registration-'))throw Error('Refuse cleanup outside generated fixture');await rm(temp,{recursive:true});}
}
