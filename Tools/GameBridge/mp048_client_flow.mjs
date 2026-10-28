// AUTHORED_NOT_RUN. Actual flow; disclosed native transport/checkpoint adapters.
import {readFile,writeFile,mkdtemp,rm} from 'node:fs/promises';
import {execFile} from 'node:child_process';import {promisify} from 'node:util';
import {tmpdir} from 'node:os';import {join,resolve,dirname,basename,sep} from 'node:path';import {fileURLToPath} from 'node:url';
const here=dirname(fileURLToPath(import.meta.url)),root=resolve(here,'../..'),run=promisify(execFile);
const source=await readFile(join(root,'Scripts/NPC/Interaction/RebirthNpcTradeClientFlow.cs'),'utf8');
const start=source.indexOf('internal static class RebirthNpcTradeClientFlow');if(start<0)throw Error('actual flow source missing');
const domainSource=await readFile(join(root,'Scripts/NPC/Persistence/RebirthNpcSaveQuarantineDomain.cs'),'utf8');
const domainStart=domainSource.indexOf('internal sealed class RebirthNpcSaveQuarantineDomain'),domainEnd=domainSource.indexOf('// This is an original-scope');
if(domainStart<0||domainEnd<=domainStart)throw Error('actual typed quarantine domain missing');
const fixture=(await readFile(join(here,'mp048_client_flow_fixture.cs'),'utf8')).replace('// ACTUAL_CLIENT_FLOW',source.slice(start)).replace('// ACTUAL_QUARANTINE_DOMAIN',domainSource.slice(domainStart,domainEnd));
const temp=await mkdtemp(join(tmpdir(),'rebirth-trade-flow-source-'));
try{
 const cs=join(temp,'fixture.cs'),exe=join(temp,'fixture.exe');await writeFile(cs,fixture);
 await run('C:/Program Files/dotnet/dotnet.exe',['C:/Program Files/dotnet/sdk/9.0.301/Roslyn/bincore/csc.dll','/nologo','/langversion:latest','/target:exe','/out:'+exe,...['mscorlib','System','System.Core','System.Xml'].map(n=>'/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/'+n+'.dll'),cs],{windowsHide:true,timeout:20000});
 process.stdout.write((await run(exe,[],{windowsHide:true,timeout:20000})).stdout);
}finally{
 if(!resolve(temp).toLowerCase().startsWith((resolve(tmpdir())+sep).toLowerCase())||!basename(temp).startsWith('rebirth-trade-flow-source-'))throw Error('unsafe fixture cleanup');
 await rm(temp,{recursive:true});
}
