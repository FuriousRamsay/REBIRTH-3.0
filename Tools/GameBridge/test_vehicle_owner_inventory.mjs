import {readFile,writeFile,mkdtemp,unlink,rmdir} from 'node:fs/promises';
import {execFile} from 'node:child_process';
import {promisify} from 'node:util';
import {tmpdir} from 'node:os';
import {join,resolve,dirname} from 'node:path';
import {fileURLToPath} from 'node:url';
const run=promisify(execFile),here=dirname(fileURLToPath(import.meta.url)),root=resolve(here,'../..');
function method(source,signature){const a=source.indexOf(signature);if(a<0)throw new Error(signature);let b=source.indexOf('{',a),depth=1,i=b+1;for(;depth&&i<source.length;i++){if(source[i]==='{')depth++;if(source[i]==='}')depth--;}return source.slice(a,i);}
const source=await readFile(join(root,'Scripts/Vehicles/Restoration/RebirthVehicleOwnerInventoryPlan.cs'),'utf8');
const item=await readFile(join(root,'Scripts/Vehicles/Restoration/RebirthVehicleTransferItem.cs'),'utf8');
const owner=await readFile(join(root,'Scripts/Vehicles/Restoration/RebirthVehicleOwnerTransfer.cs'),'utf8');
const batch=await readFile(join(root,'Scripts/Vehicles/Restoration/RebirthVehicleOwnerDebitBatch.cs'),'utf8');
const native=await readFile('C:/Users/Etienne/Desktop/__0/_0/__Rebirth/7DTD Base 3.2/ItemStack.cs','utf8');
const scope=(await readFile(join(root,'Scripts/Survivor/Network/RebirthSurvivorRequestScope.cs'),'utf8')).replace('using System;','');
const fixture=(await readFile(join(here,'test_vehicle_owner_inventory_fixture.cs'),'utf8'))
 .replace('// SOURCE',scope+'\n'+source.replace(/^using .*;\r?$/gm,'')+'\n'+item.replace(/^using .*;\r?$/gm,'')+'\n'+owner.replace(/^using .*;\r?$/gm,'')+'\n'+batch.replace(/^using .*;\r?$/gm,''))
 .replace('// NATIVE',['  public bool CanStackWith(','  public bool CanStack(','  public bool CanStackPartly(','  public bool CanStackPartlyWith('].map(sig=>method(native,sig)).join('\n'));
const temp=await mkdtemp(join(tmpdir(),'rebirth-snapshot-test-'));
try {const cs=join(temp,'check.cs'),exe=join(temp,'check.exe');await writeFile(cs,fixture);
 await run('C:/Program Files/dotnet/dotnet.exe',['C:/Program Files/dotnet/sdk/9.0.301/Roslyn/bincore/csc.dll','/nologo','/target:exe','/out:'+exe,...['mscorlib','System','System.Core','System.Xml','System.Xml.Linq'].map(x=>'/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/'+x+'.dll'),cs],{windowsHide:true,timeout:10000});console.log((await run(exe,[],{windowsHide:true,timeout:10000})).stdout.trim());
} finally {for(const n of ['check.cs','check.exe'])await unlink(join(temp,n)).catch(e=>{if(e.code!=='ENOENT')throw e;});await rmdir(temp);}