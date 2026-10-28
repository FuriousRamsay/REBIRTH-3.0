// AUTHORED_NOT_RUN. Invoke only after the user's all-coding test checkpoint.
import {readFile,writeFile,mkdtemp,rm} from 'node:fs/promises';
import {execFile} from 'node:child_process';
import {promisify} from 'node:util';
import {tmpdir} from 'node:os';
import {join,resolve,dirname,basename,sep} from 'node:path';
import {fileURLToPath} from 'node:url';
const run=promisify(execFile),here=dirname(fileURLToPath(import.meta.url)),root=resolve(here,'../..');
let fixture=await readFile(join(here,'mpB_054_pending_tool_callbacks_fixture.cs'),'utf8');
const actual=(await Promise.all(['RebirthProductiveEarnedPendingToolGestures.cs','RebirthProductiveEarnedPendingToolGestures.Owner.cs','RebirthProductiveEarnedPendingToolGestures.Before.cs','RebirthProductiveEarnedPendingToolGestures.Journal.cs','RebirthProductiveEarnedPendingToolGestures.JournalRead.cs','RebirthProductiveEarnedPendingToolGestures.Station.cs','RebirthProductiveEarnedPendingToolGestures.Publication.cs'].map(name=>readFile(join(root,'Scripts/Crafting/Recycling',name),'utf8')))).join('\n');
const marker='// ACTUAL_PENDING_TOOL_CALLBACK_SOURCE';
if(fixture.split(marker).length!==2)throw Error('Expected one actual source extraction slot');
fixture=fixture.replace(marker,actual.replace(/^using [^;]+;\r?\n/gm,''));
const temp=await mkdtemp(join(tmpdir(),'rebirth-pending-tool-callbacks-'));
try{
 const cs=join(temp,'fixture.cs'),exe=join(temp,'fixture.exe');await writeFile(cs,fixture);
 await run('C:/Program Files/dotnet/dotnet.exe',['C:/Program Files/dotnet/sdk/9.0.301/Roslyn/bincore/csc.dll','/nologo','/langversion:latest','/target:exe','/out:'+exe,...['mscorlib','System','System.Core'].map(n=>'/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/'+n+'.dll'),cs],{windowsHide:true,timeout:20000});
 process.stdout.write((await run(exe,[],{windowsHide:true,timeout:20000})).stdout);
}finally{
 if(process.env.REBIRTH_KEEP_FIXTURE==='1')process.stdout.write(temp+'\n');
 else{if(!resolve(temp).toLowerCase().startsWith((resolve(tmpdir())+sep).toLowerCase())||!basename(temp).startsWith('rebirth-pending-tool-callbacks-'))throw Error('Refuse cleanup outside generated fixture');await rm(temp,{recursive:true});}
}






