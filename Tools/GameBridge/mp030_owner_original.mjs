// AUTHORED_NOT_RUN. Native file/setter adapters are explicit in the fixture.
import {readFile,writeFile,mkdtemp,rm} from 'node:fs/promises';
import {execFile} from 'node:child_process';
import {promisify} from 'node:util';
import {tmpdir} from 'node:os';
import {join,resolve,dirname,basename,sep} from 'node:path';
import {fileURLToPath} from 'node:url';
const here=dirname(fileURLToPath(import.meta.url)),root=resolve(here,'../..'),run=promisify(execFile);
const files=['Scripts/Vehicles/Restoration/RebirthVehicleOwnerOriginal.cs','Scripts/Vehicles/Restoration/RebirthVehicleOwnerCheckpoint.cs','Scripts/Vehicles/Restoration/RebirthVehicleOwnerCreditPlan.cs','Scripts/Vehicles/Restoration/RebirthVehicleNativePolicy.cs','Scripts/Vehicles/Restoration/RebirthVehicleTransferItem.cs','Scripts/Vehicles/Camper/RebirthCamperTransferJournal.cs'];
let fixture=await readFile(join(here,'mp030_owner_original_fixture.cs'),'utf8');
const sources=await Promise.all(files.map(file=>readFile(join(root,file),'utf8')));
fixture=fixture.replace('// ACTUAL_PRODUCTION',sources.map(source=>source.replace(/^using .*;\r?$/gm,'')).join('\n'));
const temp=await mkdtemp(join(tmpdir(),'rebirth-mp030-owner-original-'));
try{
 const cs=join(temp,'fixture.cs'),exe=join(temp,'fixture.exe');await writeFile(cs,fixture);
 await run('C:/Program Files/dotnet/dotnet.exe',['C:/Program Files/dotnet/sdk/9.0.301/Roslyn/bincore/csc.dll','/nologo','/langversion:latest','/target:exe','/out:'+exe,...['mscorlib','System','System.Core','System.Xml','System.Xml.Linq','System.Security'].map(name=>'/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/'+name+'.dll'),cs],{windowsHide:true,timeout:20000});
 process.stdout.write((await run(exe,[temp],{windowsHide:true,timeout:20000})).stdout);
}finally{
 if(process.env.REBIRTH_KEEP_FIXTURE==='1')process.stdout.write(temp+'\n');
 else{if(!resolve(temp).toLowerCase().startsWith((resolve(tmpdir())+sep).toLowerCase())||!basename(temp).startsWith('rebirth-mp030-owner-original-'))throw Error('Unsafe fixture cleanup');await rm(temp,{recursive:true});}
}
