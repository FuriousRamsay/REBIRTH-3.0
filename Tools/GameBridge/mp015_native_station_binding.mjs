// AUTHORED_NOT_RUN. Actual complete outbox + actual native station binding partial.
import {readFile,writeFile,mkdtemp,rm} from 'node:fs/promises';import {execFile} from 'node:child_process';import {promisify} from 'node:util';
import {tmpdir} from 'node:os';import {join,resolve,dirname,basename,sep} from 'node:path';import {fileURLToPath} from 'node:url';
const here=dirname(fileURLToPath(import.meta.url)),root=resolve(here,'../..'),run=promisify(execFile);
const outbox=await readFile(join(root,'Scripts/Survivor/Progression/RebirthWorkstationCompletionOutbox.cs'),'utf8');
const native=await readFile(join(root,'Scripts/Survivor/Progression/RebirthWorkstationCompletionOutbox.NativeStation.cs'),'utf8');
const shared=await readFile(join(here,'mp015_outbox_saved_stage_fixture.cs'),'utf8');
const split=shared.indexOf('internal static class Program {'),a=shared.indexOf('// BOUNDED_NATIVE_STATION_ADAPTER_BEGIN'),b=shared.indexOf('// BOUNDED_NATIVE_STATION_ADAPTER_END');
if(split<0||a<0||b<a||b>split)throw Error('Explicit bounded old-cohort adapter boundary changed');
const prefix=(shared.slice(0,a)+native.slice(native.indexOf('internal static partial class RebirthWorkstationCompletionOutbox'))+shared.slice(b+'// BOUNDED_NATIVE_STATION_ADAPTER_END'.length,split)).replace('// ACTUAL_OUTBOX',outbox);
const fixture='using System.Linq;\n'+prefix+'\n'+await readFile(join(here,'mp015_native_station_binding_fixture.cs'),'utf8');
const temp=await mkdtemp(join(tmpdir(),'rebirth-native-station-binding-'));
try{
 const cs=join(temp,'fixture.cs'),exe=join(temp,'fixture.exe');await writeFile(cs,fixture);
 await run('C:/Program Files/dotnet/dotnet.exe',['C:/Program Files/dotnet/sdk/9.0.301/Roslyn/bincore/csc.dll','/nologo','/langversion:latest','/target:exe','/out:'+exe,...['mscorlib','System','System.Core','System.Xml','System.Xml.Linq'].map(n=>'/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/'+n+'.dll'),cs],{windowsHide:true,timeout:20000});
 process.stdout.write((await run(exe,[],{windowsHide:true,timeout:20000})).stdout);
}finally{
 if(process.env.REBIRTH_KEEP_FIXTURE==='1')process.stdout.write(temp+'\n');
 else{if(!resolve(temp).toLowerCase().startsWith((resolve(tmpdir())+sep).toLowerCase())||!basename(temp).startsWith('rebirth-native-station-binding-'))throw Error('Unsafe fixture cleanup');await rm(temp,{recursive:true});}
}
