// AUTHORED_NOT_RUN. No compiler/fixture execution during the coding-only phase.
import {readFile,writeFile,mkdtemp,readdir} from 'node:fs/promises';import {execFile} from 'node:child_process';import {promisify} from 'node:util';
import {tmpdir} from 'node:os';import {join,resolve,dirname} from 'node:path';import {fileURLToPath} from 'node:url';
const here=dirname(fileURLToPath(import.meta.url)),root=resolve(here,'../..'),run=promisify(execFile);
let fixture=await readFile(join(here,'mp071_fuel_original_fixture.cs'),'utf8');
for(const [marker,file] of [['FUEL','RebirthCamperStationFuelOriginal.cs'],['JOURNAL','RebirthCamperTransferJournal.cs'],['COMMAND','RebirthCamperRemoteStationCommand.cs']]){
 const source=await readFile(join(root,'Scripts/Vehicles/Camper',file),'utf8'),slot='// ACTUAL_'+marker;
 if(fixture.split(slot).length!==2)throw Error('Unique source slot required '+slot);fixture=fixture.replace(slot,source.replace(/^using .*;\r?\n/gm,''));
}
const status=await readFile(join(root,'Scripts/Vehicles/Camper/RebirthCamperRemoteStationCommandStatus.cs'),'utf8');
const start=status.indexOf('    internal static string Binding('),end=status.indexOf('    private static Guid NativeCancel(',start);
if(start<0||end<=start)throw Error('Actual binding boundary changed');fixture=fixture.replace('// ACTUAL_BINDING',status.slice(start,end));
function block(source,key){const start=source.indexOf(key);if(start<0||source.indexOf(key,start+1)>=0)throw Error('Unique actual method required '+key);let end=source.indexOf('{',start)+1,depth=1;for(;depth&&end<source.length;end++){if(source[end]==='{')depth++;else if(source[end]==='}')depth--;}if(depth)throw Error('Unclosed actual method');return source.slice(start,end);}
const energy=await readFile(join(root,'Scripts/Vehicles/Camper/Energy/RebirthCamperEnergyRuntime.cs'),'utf8');
const native=await readFile(join(root,'Scripts/Vehicles/Camper/RebirthCamperStationNativeCrafting.cs'),'utf8');
const nativeOriginal=await readFile(join(root,'Scripts/Vehicles/Camper/RebirthCamperStationNativeCrafting.Original.cs'),'utf8');
fixture=fixture.replace('// ACTUAL_NATIVE_ORIGINAL',nativeOriginal.replace(/^using .*;\r?\n/gm,''));
const constantsStart=native.indexOf('    private const string JobKey='),constantsEnd=native.indexOf('    internal static bool RestoreActorImages(',constantsStart);
if(constantsStart<0||constantsEnd<=constantsStart)throw Error('Native receipt key boundary changed');
fixture=fixture.replace('// ACTUAL_NATIVE_CONSTANTS',native.slice(constantsStart,constantsEnd));
for(const [marker,key] of [['JOB','internal static bool TryJob('],['REFUND','internal static bool TryRemainingRefund('],['PAID','private static bool PaidCount('],['PLAN','private static bool Plan('],['CRAFT','internal static bool Craft('],['CANCEL','internal static bool Cancel('],['CANCEL_ID','private static Guid CancelOperation(']])fixture=fixture.replace('// ACTUAL_NATIVE_'+marker,block(native,key));
const queueEntry=await readFile(join(root,'Scripts/Crafting/UI/RebirthStationNativeQueueEntry.cs'),'utf8');fixture=fixture.replace('// ACTUAL_VACANT_SLOT',block(queueEntry,'public static bool TryFindVacantSlot('));
for(const [marker,key] of [['HOLD','public bool Hold('],['PREPARE','private bool CanPrepare('],['COMMIT','public bool Commit(']])fixture=fixture.replace('// ACTUAL_ENERGY_'+marker,block(energy,key));
const tick=block(energy,'public void Tick('),boundary=tick.indexOf('        if(lastTick==0||now<lastTick)');if(boundary<0)throw Error('Actual energy Tick admission boundary changed');
fixture=fixture.replace('// ACTUAL_ENERGY_TICK_PREFIX',tick.slice(0,boundary)+'throw new InvalidOperationException("Native energy remainder intentionally outside this prefix fixture");\n}');
const out=await mkdtemp(join(tmpdir(),'rebirth-mp071-fuel-')),cs=join(out,'actual.cs'),exe=join(out,'actual.exe');await writeFile(cs,fixture);
const sdk=process.env.REBIRTH_DOTNET_SDK_ROOT||'C:/Program Files/dotnet/sdk',versions=(await readdir(sdk)).filter(x=>/^\d+\.\d+\.\d+$/.test(x)).sort((a,b)=>a.localeCompare(b,undefined,{numeric:true}));if(!versions.length)throw Error('Roslyn unavailable; source preserved '+cs);
await run('dotnet',[join(sdk,versions.at(-1),'Roslyn/bincore/csc.dll'),'/nologo','/langversion:latest','/target:exe','/out:'+exe,...['mscorlib','System','System.Core','System.Xml','System.Xml.Linq'].map(x=>'/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/'+x+'.dll'),cs],{windowsHide:true,timeout:30000});
console.log((await run(exe,[],{windowsHide:true,timeout:30000})).stdout.trim());
