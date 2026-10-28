// AUTHORED_NOT_RUN. Invoke only after the user's all-coding test checkpoint.
import {readFile,writeFile,mkdtemp,rm} from 'node:fs/promises';
import {execFile} from 'node:child_process';
import {promisify} from 'node:util';
import {tmpdir} from 'node:os';
import {join,resolve,dirname,basename,sep} from 'node:path';
import {fileURLToPath} from 'node:url';
const run=promisify(execFile),here=dirname(fileURLToPath(import.meta.url)),root=resolve(here,'../..');
let fixture=await readFile(join(here,'mpB_054_pending_tools_fixture.cs'),'utf8');
const body=await readFile(join(root,'Scripts/Crafting/Recycling/RebirthProductiveEarnedNativeBody.cs'),'utf8');
const work=await readFile(join(root,'Scripts/Crafting/Recycling/RebirthProductiveEarnedNativePartialSuccessor.cs'),'utf8');
function method(source,signature){const at=source.indexOf(signature);if(at<0||source.indexOf(signature,at+signature.length)>=0)throw Error('Ambiguous actual source');let depth=0,start=source.indexOf('{',at);for(let i=start;i<source.length;i++){if(source[i]==='{')depth++;if(source[i]==='}'&&--depth===0)return source.slice(at,i+1);}throw Error('Unclosed actual source');}
const actual=method(body,'    internal static bool ValidPendingToolReplacement(');
fixture=fixture.replace('// ACTUAL_PARTIAL_WORK_SOURCE',method(work,'    internal static bool TryPartialNativeWork('));
const marker='// ACTUAL_PARTIAL_DELTA_SOURCE';
if(fixture.split(marker).length!==2)throw Error('Expected one actual source extraction slot');
fixture=fixture.replace(marker,actual.replace(/^using [^;]+;\r?\n/gm,''));
const temp=await mkdtemp(join(tmpdir(),'rebirth-pending-tools-'));
try{
 const cs=join(temp,'fixture.cs'),exe=join(temp,'fixture.exe');await writeFile(cs,fixture);
 await run('C:/Program Files/dotnet/dotnet.exe',['C:/Program Files/dotnet/sdk/9.0.301/Roslyn/bincore/csc.dll','/nologo','/langversion:latest','/target:exe','/out:'+exe,...['mscorlib','System','System.Core'].map(n=>'/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/'+n+'.dll'),cs],{windowsHide:true,timeout:20000});
 process.stdout.write((await run(exe,[],{windowsHide:true,timeout:20000})).stdout);
}finally{
 if(process.env.REBIRTH_KEEP_FIXTURE==='1')process.stdout.write(temp+'\n');
 else{if(!resolve(temp).toLowerCase().startsWith((resolve(tmpdir())+sep).toLowerCase())||!basename(temp).startsWith('rebirth-pending-tools-'))throw Error('Refuse cleanup outside generated fixture');await rm(temp,{recursive:true});}
}
