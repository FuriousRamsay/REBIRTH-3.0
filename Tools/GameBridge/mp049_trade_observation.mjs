// AUTHORED_NOT_RUN. Actual custody tracker, explicit release witness adapter.
import {readFile,writeFile,mkdtemp,rm} from 'node:fs/promises';
import {execFile} from 'node:child_process';import {promisify} from 'node:util';
import {tmpdir} from 'node:os';import {join,resolve,dirname,basename,sep} from 'node:path';import {fileURLToPath} from 'node:url';
const here=dirname(fileURLToPath(import.meta.url)),root=resolve(here,'../..'),run=promisify(execFile);
const source=await readFile(join(root,'Scripts/NPC/Interaction/RebirthNpcTradeOwnerLoad.cs'),'utf8');
const start=source.indexOf('    internal sealed class Retirement'),end=source.indexOf('    internal static Func<RebirthNpcTradeOwnerExchange.Release,bool>[] CaptureRetirement()',start);if(start<0||end<=start)throw Error('actual release witness missing');
let retirement=source.slice(start,end);
// Admission of an incoming native namespace is a separate source cohort.
// This fixture exercises actual original-observation disposal only.
const admissionStart=retirement.indexOf('        internal bool MatchesIncoming('),admissionEnd=retirement.indexOf('        internal bool ReleaseDetachedObservation()',admissionStart);
if(admissionStart<0||admissionEnd<=admissionStart)throw Error('Native admission exclusion boundary changed');
retirement=retirement.slice(0,admissionStart)+retirement.slice(admissionEnd);
const fixture=(await readFile(join(here,'mp049_trade_observation_fixture.cs'),'utf8')).replace('// ACTUAL_OBSERVATION_RETIREMENT',retirement);
const temp=await mkdtemp(join(tmpdir(),'rebirth-trade-observation-source-'));
try{
 const cs=join(temp,'fixture.cs'),exe=join(temp,'fixture.exe');await writeFile(cs,fixture);
 await run('C:/Program Files/dotnet/dotnet.exe',['C:/Program Files/dotnet/sdk/9.0.301/Roslyn/bincore/csc.dll','/nologo','/langversion:latest','/target:exe','/out:'+exe,...['mscorlib','System','System.Core'].map(n=>'/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/'+n+'.dll'),cs],{windowsHide:true,timeout:20000});
 process.stdout.write((await run(exe,[],{windowsHide:true,timeout:20000})).stdout);
}finally{
 if(!resolve(temp).toLowerCase().startsWith((resolve(tmpdir())+sep).toLowerCase())||!basename(temp).startsWith('rebirth-trade-observation-source-'))throw Error('unsafe fixture cleanup');
 await rm(temp,{recursive:true});
}


