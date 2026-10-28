// AUTHORED_NOT_RUN. Invoke only after the user's all-coding test checkpoint.
import {readFile,writeFile,mkdtemp,rm} from 'node:fs/promises';
import {execFile} from 'node:child_process';
import {promisify} from 'node:util';
import {tmpdir} from 'node:os';
import {join,resolve,dirname,basename,sep} from 'node:path';
import {fileURLToPath} from 'node:url';
const run=promisify(execFile),here=dirname(fileURLToPath(import.meta.url)),root=resolve(here,'../..');
let fixture=await readFile(join(here,'mp010_pull_session_fixture.cs'),'utf8');
const actual=await readFile(join(root,'Scripts/Crafting/Cooking/RebirthCookingPull.cs'),'utf8');
const marker='// ACTUAL_PULL_SESSION';
if(fixture.split(marker).length!==2)throw Error('Expected one actual source extraction slot');
const start=actual.indexOf('    private static bool CurrentPullSession(');
const end=actual.indexOf('    private static void Send(',start);
if(start<0||end<=start)throw Error('Pull session predicate boundary changed');
fixture=fixture.replace(marker,actual.slice(start,end));
const temp=await mkdtemp(join(tmpdir(),'rebirth-pull-session-'));
try{
 const cs=join(temp,'fixture.cs'),exe=join(temp,'fixture.exe');await writeFile(cs,fixture);
 await run('C:/Program Files/dotnet/dotnet.exe',['C:/Program Files/dotnet/sdk/9.0.301/Roslyn/bincore/csc.dll','/nologo','/langversion:latest','/target:exe','/out:'+exe,...['mscorlib','System','System.Core'].map(n=>'/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/'+n+'.dll'),cs],{windowsHide:true,timeout:20000});
 process.stdout.write((await run(exe,[],{windowsHide:true,timeout:20000})).stdout);
}finally{
 if(process.env.REBIRTH_KEEP_FIXTURE==='1')process.stdout.write(temp+'\n');
 else{if(!resolve(temp).toLowerCase().startsWith((resolve(tmpdir())+sep).toLowerCase())||!basename(temp).startsWith('rebirth-pull-session-'))throw Error('Refuse cleanup outside generated fixture');await rm(temp,{recursive:true});}
}

