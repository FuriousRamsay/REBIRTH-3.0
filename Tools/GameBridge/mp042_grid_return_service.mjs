// AUTHORED_NOT_RUN. Execute only after all production/scenario coding completes.
import {readFile,writeFile,mkdtemp,rm} from 'node:fs/promises';import {execFile} from 'node:child_process';import {promisify} from 'node:util';
import {tmpdir} from 'node:os';import {join,resolve,dirname,basename,sep} from 'node:path';import {fileURLToPath} from 'node:url';
const here=dirname(fileURLToPath(import.meta.url)),root=resolve(here,'../..'),run=promisify(execFile);
const source=await readFile(join(root,'Scripts/Crafting/Cooking/RebirthCookingRemoteAdmissionBackend.cs'),'utf8');
let common=await readFile(join(here,'mp042_cancellation_service_fixture.cs'),'utf8');const boundary=common.indexOf('internal static partial class RebirthCookingRemoteCancellation {');
if(boundary<0)throw Error('Service adapter boundary changed');
let fixture=await readFile(join(here,'mp042_grid_return_service_fixture.cs'),'utf8');
for(const [marker,a,b] of [['// ACTUAL_OWED','    private static string Owed(','    internal static ItemStack[] PaidInputs('],['// ACTUAL_RETURN_GRID','    private static bool ReturnOwnerCurrent(','    internal static bool Held(']]){
 const begin=source.indexOf(a),end=source.indexOf(b,begin);if(begin<0||end<=begin||fixture.split(marker).length!==2)throw Error('Actual grid return boundary changed '+marker);fixture=fixture.replace(marker,source.slice(begin,end));
}
common=common.slice(0,boundary).replace('internal sealed class World {','internal sealed class World {internal bool IsRemote()=>false;internal object GetEntity(int id)=>Model.Current?Model.Player:null;').replace('internal bool IsDead()=>false;','internal bool IsDead()=>false;internal bool IsSpawned()=>true;').replace('return Model.Current&&location==','return Model.Current&&RebirthCookingRemoteAdmissionBackend.StationAccess&&location==');
fixture=common+fixture;
const temp=await mkdtemp(join(tmpdir(),'rebirth-grid-return-service-'));
try{
 const cs=join(temp,'fixture.cs'),exe=join(temp,'fixture.exe');await writeFile(cs,fixture);
 await run('C:/Program Files/dotnet/dotnet.exe',['C:/Program Files/dotnet/sdk/9.0.301/Roslyn/bincore/csc.dll','/nologo','/langversion:latest','/target:exe','/out:'+exe,...['mscorlib','System','System.Core','System.Xml','System.Xml.Linq'].map(n=>'/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/'+n+'.dll'),cs],{windowsHide:true,timeout:20000});
 process.stdout.write((await run(exe,[],{windowsHide:true,timeout:20000})).stdout);
}finally{
 if(process.env.REBIRTH_KEEP_FIXTURE==='1')process.stdout.write(temp+'\n');
 else{if(!resolve(temp).toLowerCase().startsWith((resolve(tmpdir())+sep).toLowerCase())||!basename(temp).startsWith('rebirth-grid-return-service-'))throw Error('Unsafe fixture cleanup');await rm(temp,{recursive:true});}
}
