import {readFile,writeFile,mkdtemp,unlink,rmdir} from 'node:fs/promises';
import {execFile} from 'node:child_process';
import {promisify} from 'node:util';
import {tmpdir} from 'node:os';
import {join,resolve,dirname} from 'node:path';
import {fileURLToPath} from 'node:url';
const run=promisify(execFile),here=dirname(fileURLToPath(import.meta.url)),root=resolve(here,'../..');
const source=(await readFile(join(root,'Scripts/Vehicles/Restoration/RebirthVehicleRestorationSystem.cs'),'utf8')).replaceAll('\r\n','\n');
function method(signature){const a=source.indexOf(signature);if(a<0)throw new Error(signature);let b=source.indexOf('{',a),depth=1,i=b+1;for(;depth&&i<source.length;i++){if(source[i]==='{')depth++;if(source[i]==='}')depth--;}return source.slice(a,i);}
const reservation=(await readFile(join(root,'Scripts/Vehicles/Restoration/RebirthVehicleTransferReservation.cs'),'utf8')).replace('using System;','');
const plan=(await readFile(join(root,'Scripts/Vehicles/Restoration/RebirthVehicleTransferPlan.cs'),'utf8')).replace(/^using .*;\r?$/gm,'');
const item=(await readFile(join(root,'Scripts/Vehicles/Restoration/RebirthVehicleTransferItem.cs'),'utf8')).replace(/^using .*;\r?$/gm,'');
const store=(await readFile(join(root,'Scripts/Vehicles/Restoration/RebirthVehicleTransferPlanStore.cs'),'utf8')).replace(/^using .*;\r?$/gm,'');
const recovery=(await readFile(join(root,'Scripts/Vehicles/Restoration/RebirthVehicleTransferRecovery.cs'),'utf8')).replace(/^using .*;\r?$/gm,'');
const target=(await readFile(join(root,'Scripts/Vehicles/Restoration/RebirthVehicleTransferTarget.cs'),'utf8')).replace(/^using .*;\r?$/gm,'');
const planner=(await readFile(join(root,'Scripts/Vehicles/Restoration/RebirthVehicleTransferPlanner.cs'),'utf8')).replace(/^using .*;\r?$/gm,'');
const fuel=(await readFile(join(root,'Scripts/Vehicles/Restoration/RebirthVehicleTransferFuel.cs'),'utf8')).replace(/^using .*;\r?$/gm,'');
const selection=(await readFile(join(root,'Scripts/Vehicles/Restoration/RebirthVehicleFuelSelection.cs'),'utf8')).replace(/^using .*;\r?$/gm,'');
const health=(await readFile(join(root,'Scripts/Vehicles/Restoration/RebirthVehicleTransferHealth.cs'),'utf8')).replace(/^using .*;\r?$/gm,'');
const ownerEnum=(await readFile(join(root,'Scripts/Vehicles/Restoration/RebirthVehicleOwnerTransfer.cs'),'utf8')).match(/public enum RebirthVehicleOwnerTransferResult[^}]+}/)[0];
const scope=(await readFile(join(root,'Scripts/Survivor/Network/RebirthSurvivorRequestScope.cs'),'utf8')).replace('using System;','');
const fixture=(await readFile(join(here,'test_vehicle_item_payload_fixture.cs'),'utf8')).replace('// INDEX METHOD',method('public static bool TryLocateTransferTarget(')).replace('// METHODS',method('public sealed class RebirthInstalledVehiclePart')+'\n'+method('public sealed class RebirthVehicleAssembly\n')+'\n'+method('public static class RebirthVehicleAssemblySerializer')+'\n'+reservation+'\n'+plan+'\n'+method('public enum RebirthVehiclePartSourceLocation')+'\n'+item+'\n'+store+'\n'+ownerEnum+'\n'+recovery+'\n'+method('public enum RebirthVehicleAssemblyCarrier')+'\n'+target+'\n'+method('public enum RebirthVehicleAssemblyAction')+'\n'+method('public sealed class RebirthVehicleSlotDefinition')+'\n'+method('public sealed class RebirthVehicleFamilyDefinition')+'\n'+planner+'\n'+fuel+'\n'+selection+'\n'+health+'\n'+scope);
const temp=await mkdtemp(join(tmpdir(),'rebirth-snapshot-test-'));
try {const cs=join(temp,'check.cs'),exe=join(temp,'check.exe');await writeFile(cs,fixture);
 await run('C:/Program Files/dotnet/dotnet.exe',['C:/Program Files/dotnet/sdk/9.0.301/Roslyn/bincore/csc.dll','/nologo','/target:exe','/out:'+exe,...['mscorlib','System','System.Core','System.Xml','System.Xml.Linq'].map(x=>'/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/'+x+'.dll'),cs],{windowsHide:true,timeout:10000});console.log((await run(exe,[],{windowsHide:true,timeout:10000})).stdout.trim());
} finally {for(const n of ['check.cs','check.exe'])await unlink(join(temp,n)).catch(e=>{if(e.code!=='ENOENT')throw e;});await rmdir(temp);}