// AUTHORED_NOT_RUN. Execute only after all production/scenario coding completes.
import {readFile,writeFile,mkdtemp,rm} from 'node:fs/promises';import {execFile} from 'node:child_process';import {promisify} from 'node:util';
import {tmpdir} from 'node:os';import {join,resolve,dirname,basename,sep} from 'node:path';import {fileURLToPath} from 'node:url';
const here=dirname(fileURLToPath(import.meta.url)),root=resolve(here,'../..'),run=promisify(execFile);
const source=await readFile(join(root,'Scripts/Survivor/Progression/RebirthWorkstationCompletionPipeline.cs'),'utf8');
const begin=source.indexOf('    internal static void Receive('),end=source.indexOf('    private static void PrepareServer(',begin);
let fixture=await readFile(join(here,'mp015_receive_fixture.cs'),'utf8');
const fragments=await readFile(join(root,'Scripts/Crafting/RemoteCrafting/RebirthRemoteResourceFragments.cs'),'utf8');
if(fixture.split('// ACTUAL_FRAGMENTS').length!==2)throw Error('Actual fragment source marker changed');
fixture=fixture.replace('// ACTUAL_FRAGMENTS',fragments);
if(begin<0||end<=begin||fixture.split('// ACTUAL_RECEIVE').length!==2)throw Error('Actual completion receiver boundary changed');fixture=fixture.replace('// ACTUAL_RECEIVE',source.slice(begin,end));
fixture='using System.Linq;\n'+fixture;
const temp=await mkdtemp(join(tmpdir(),'rebirth-completion-receive-'));
try{
 const cs=join(temp,'fixture.cs'),exe=join(temp,'fixture.exe');await writeFile(cs,fixture);
 await run('C:/Program Files/dotnet/dotnet.exe',['C:/Program Files/dotnet/sdk/9.0.301/Roslyn/bincore/csc.dll','/nologo','/langversion:latest','/target:exe','/out:'+exe,...['mscorlib','System','System.Core','System.Xml','System.Xml.Linq'].map(n=>'/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/'+n+'.dll'),cs],{windowsHide:true,timeout:20000});
 process.stdout.write((await run(exe,[],{windowsHide:true,timeout:20000})).stdout);
}finally{
 if(process.env.REBIRTH_KEEP_FIXTURE==='1')process.stdout.write(temp+'\n');
 else{if(!resolve(temp).toLowerCase().startsWith((resolve(tmpdir())+sep).toLowerCase())||!basename(temp).startsWith('rebirth-completion-receive-'))throw Error('Unsafe fixture cleanup');await rm(temp,{recursive:true});}
}



