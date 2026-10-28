// AUTHORED_NOT_RUN. Invoke only after the user's all-coding test checkpoint.
import {readFile,writeFile,mkdtemp,rm} from 'node:fs/promises';
import {execFile} from 'node:child_process';
import {promisify} from 'node:util';
import {tmpdir} from 'node:os';
import {join,resolve,dirname,basename,sep} from 'node:path';
import {fileURLToPath} from 'node:url';
const run=promisify(execFile),here=dirname(fileURLToPath(import.meta.url)),root=resolve(here,'../..');
let fixture=await readFile(join(here,'mp073_parcel_plan_fixture.cs'),'utf8');
const actual=await readFile(join(root,'Scripts/Vehicles/Camper/RebirthCamperRemovalParcelPlan.cs'),'utf8');
const start=actual.indexOf('internal sealed class RebirthCamperRemovalParcelPlan');
const create=actual.indexOf('    internal static bool TryCreate(',start),build=actual.indexOf('    private bool Build(',create),retain=actual.indexOf('    internal RebirthCamperTransferJournal.Record Retain()',build);
if(start<0||create<start||build<create||retain<build)throw Error('Actual plan boundaries changed');
const source=actual.slice(start,create)+actual.slice(build,retain)+'internal bool Freeze()=>Build(out Credited,out Retired,out Overflow);}';
fixture=fixture.replace('// ACTUAL_PLAN',source);
const temp=await mkdtemp(join(tmpdir(),'rebirth-parcel-plan-'));
try{
 const cs=join(temp,'fixture.cs'),exe=join(temp,'fixture.exe');await writeFile(cs,fixture);
 await run('C:/Program Files/dotnet/dotnet.exe',['C:/Program Files/dotnet/sdk/9.0.301/Roslyn/bincore/csc.dll','/nologo','/langversion:latest','/target:exe','/out:'+exe,...['mscorlib','System','System.Core','System.Xml','System.Xml.Linq'].map(n=>'/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/'+n+'.dll'),cs],{windowsHide:true,timeout:20000});
 process.stdout.write((await run(exe,[],{windowsHide:true,timeout:20000})).stdout);
}finally{
 if(process.env.REBIRTH_KEEP_FIXTURE==='1')process.stdout.write(temp+'\n');
 else{if(!resolve(temp).toLowerCase().startsWith((resolve(tmpdir())+sep).toLowerCase())||!basename(temp).startsWith('rebirth-parcel-plan-'))throw Error('Refuse cleanup outside generated fixture');await rm(temp,{recursive:true});}
}


