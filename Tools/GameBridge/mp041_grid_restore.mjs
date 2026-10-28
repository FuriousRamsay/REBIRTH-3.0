// AUTHORED_NOT_RUN. Actual original grid projection; no financial permission supplied.
import {readFile,writeFile,mkdtemp,readdir} from 'node:fs/promises';import {execFile} from 'node:child_process';import {promisify} from 'node:util';
import {tmpdir} from 'node:os';import {join,resolve,dirname} from 'node:path';import {fileURLToPath} from 'node:url';
const here=dirname(fileURLToPath(import.meta.url)),root=resolve(here,'../..'),run=promisify(execFile);
const common=await readFile(join(here,'mp042_cancellation_dispatch_fixture.cs'),'utf8'),boundary=common.indexOf('internal static class RebirthCookingOwnerDelta {');
if(boundary<0)throw Error('Native adapter boundary changed');
const source=await readFile(join(root,'Scripts/Crafting/Cooking/RebirthCookingRemoteAdmission.cs'),'utf8');
let fixture=await readFile(join(here,'mp041_grid_restore_fixture.cs'),'utf8');
for(const [marker,first,last] of [['// ACTUAL_ORIGINAL','    private sealed class ClientOriginal','    private static readonly List<ClientOriginal> Clients'],['// ACTUAL_RESTORE','    private static bool RestoreClientGrid(','    private static bool FinishAdmissionProjection(']]){
 const begin=source.indexOf(first),end=source.indexOf(last,begin);if(begin<0||end<=begin||fixture.split(marker).length!==2)throw Error('Actual source boundary changed '+marker);fixture=fixture.replace(marker,source.slice(begin,end));
}
const temp=await mkdtemp(join(tmpdir(),'rebirth-grid-restore-')),cs=join(temp,'actual.cs'),exe=join(temp,'actual.exe');await writeFile(cs,common.slice(0,boundary)+fixture);
const sdk=process.env.REBIRTH_DOTNET_SDK_ROOT||'C:/Program Files/dotnet/sdk',versions=(await readdir(sdk)).filter(x=>/^\d+\.\d+\.\d+$/.test(x)).sort((a,b)=>a.localeCompare(b,undefined,{numeric:true}));if(!versions.length)throw Error('Roslyn unavailable; retained '+cs);
await run('dotnet',[join(sdk,versions.at(-1),'Roslyn/bincore/csc.dll'),'/nologo','/langversion:latest','/target:exe','/out:'+exe,...['mscorlib','System','System.Core'].map(x=>'/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/'+x+'.dll'),cs],{windowsHide:true,timeout:30000});console.log((await run(exe,[],{windowsHide:true,timeout:30000})).stdout.trim());
