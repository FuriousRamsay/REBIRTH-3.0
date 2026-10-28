// AUTHORED_NOT_RUN. Invoke only after the project coding gate is lifted.
import {readFile,writeFile,mkdtemp,readdir} from 'node:fs/promises';
import {execFile} from 'node:child_process';import {promisify} from 'node:util';
import {tmpdir} from 'node:os';import {join,resolve,dirname} from 'node:path';import {fileURLToPath} from 'node:url';
const root=resolve(dirname(fileURLToPath(import.meta.url)),'../..'),run=promisify(execFile);
const source=await readFile(join(root,'Scripts/Crafting/Cooking/RebirthCookingGridCustody.cs'),'utf8');
const start=source.indexOf('    private static int PlanPreferred('),end=source.indexOf('    internal static bool Pull(',start);
if(start<0||end<0)throw Error('Actual preferred return planner not found');
let doubles=(await readFile(join(root,'Tools/GameBridge/mpA_additional_inventory_prediction_fixture.cs'),'utf8')).split('// PRODUCTION_PREDICTOR')[0];
doubles=doubles.replace('using System;','using System;using System.IO;');
doubles=doubles.replace('internal int Length;}','internal int Length;internal bool CanMoveToSlot(ItemStack item,int slot)=>item.itemValue.ItemClass.CanMoveToSlot(XUiC_ItemStack.StackLocationTypes.ToolBelt,slot);}');
const fixture=await readFile(join(root,'Tools/GameBridge/mp041_preferred_return_fixture.cs'),'utf8');
const text=doubles+fixture.replace('// PRODUCTION_PREFERRED_RETURN',source.slice(start,end));
const temporary=await mkdtemp(join(tmpdir(),'rebirth-preferred-return-')),cs=join(temporary,'actual.cs'),exe=join(temporary,'actual.exe');await writeFile(cs,text);
const sdk=process.env.REBIRTH_DOTNET_SDK_ROOT||'C:/Program Files/dotnet/sdk',versions=(await readdir(sdk)).filter(x=>/^\d+\.\d+\.\d+$/.test(x)).sort((a,b)=>a.localeCompare(b,undefined,{numeric:true}));
if(!versions.length)throw Error('Roslyn unavailable; retained '+cs);
await run('dotnet',[join(sdk,versions.at(-1),'Roslyn/bincore/csc.dll'),'/nologo','/target:exe','/out:'+exe,...['mscorlib','System','System.Core'].map(x=>'/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/'+x+'.dll'),cs],{windowsHide:true,timeout:30000});
console.log((await run(exe,[],{windowsHide:true,timeout:30000})).stdout.trim());
