// AUTHORED_NOT_RUN. Invoke only after the user's all-coding test checkpoint.
import {readFile,writeFile,mkdtemp,rm} from 'node:fs/promises';
import {execFile} from 'node:child_process';
import {promisify} from 'node:util';
import {tmpdir} from 'node:os';
import {join,resolve,dirname,basename,sep} from 'node:path';
import {fileURLToPath} from 'node:url';
const run=promisify(execFile),here=dirname(fileURLToPath(import.meta.url)),root=resolve(here,'../..');
let fixture=await readFile(join(here,'mpB_054_tool_transfer_fixture.cs'),'utf8');
const actual=await readFile(join(root,'Scripts/Crafting/Recycling/RebirthProductiveEarnedPendingToolTransfer.cs'),'utf8');
const conservation=await readFile(join(root,'Scripts/QuickStack/RebirthLogisticsDeathWitness.cs'),'utf8');
const at=conservation.indexOf('    internal static bool Conserved('),body=conservation.indexOf('{',at);
if(at<0||body<0)throw Error('Native conservation source missing');
let end=body+1,depth=1;
for(;end<conservation.length&&depth;end++){if(conservation[end]==='{')depth++;if(conservation[end]==='}')depth--;}
if(depth||fixture.split('// ACTUAL_NATIVE_CONSERVATION_SOURCE').length!==2)throw Error('Native conservation extraction changed');
fixture=fixture.replace('// ACTUAL_NATIVE_CONSERVATION_SOURCE',conservation.slice(at,end));
const marker='// ACTUAL_TOOL_TRANSFER_SOURCE';
if(fixture.split(marker).length!==2)throw Error('Expected one actual source extraction slot');
fixture=fixture.replace(marker,actual.replace(/^using [^;]+;\r?\n/gm,''));
const temp=await mkdtemp(join(tmpdir(),'rebirth-tool-transfer-'));
try{
 const cs=join(temp,'fixture.cs'),exe=join(temp,'fixture.exe');await writeFile(cs,fixture);
 await run('C:/Program Files/dotnet/dotnet.exe',['C:/Program Files/dotnet/sdk/9.0.301/Roslyn/bincore/csc.dll','/nologo','/langversion:latest','/target:exe','/out:'+exe,...['mscorlib','System','System.Core'].map(n=>'/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/'+n+'.dll'),cs],{windowsHide:true,timeout:20000});
 process.stdout.write((await run(exe,[],{windowsHide:true,timeout:20000})).stdout);
}finally{
 if(process.env.REBIRTH_KEEP_FIXTURE==='1')process.stdout.write(temp+'\n');
 else{if(!resolve(temp).toLowerCase().startsWith((resolve(tmpdir())+sep).toLowerCase())||!basename(temp).startsWith('rebirth-tool-transfer-'))throw Error('Refuse cleanup outside generated fixture');await rm(temp,{recursive:true});}
}
