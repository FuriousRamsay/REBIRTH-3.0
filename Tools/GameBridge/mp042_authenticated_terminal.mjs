// AUTHORED_NOT_RUN. Execute only after the project-wide coding phase.
import {readFile,writeFile,mkdtemp,readdir} from 'node:fs/promises';
import {execFile} from 'node:child_process';import {promisify} from 'node:util';
import {tmpdir} from 'node:os';import {join,resolve,dirname} from 'node:path';import {fileURLToPath} from 'node:url';
const here=dirname(fileURLToPath(import.meta.url)),root=resolve(here,'../..'),run=promisify(execFile);
let fixture=await readFile(join(here,'mp042_historical_terminal_fixture.cs'),'utf8');
function replace(a,b){if(!fixture.includes(a))throw Error('Fixture boundary changed: '+a);fixture=fixture.replace(a,b);}
replace('internal bool Dead,Spawned=true;','internal Inventory bag=new Inventory(),inventory=new Inventory();internal Buffs Buffs=new Buffs();internal bool Dead,Spawned=true;');
replace('internal World World;}','internal World World;internal int Saves;internal bool Publish=true;internal void SaveLocalPlayerData(){Saves++;if(Publish)RebirthGearNativePlayerFile.Data.Phase=World.Primary.Buffs.Value;}}');
replace('internal int Deaths;}','internal int Deaths;internal EntityPlayerLocal Player;internal World World;internal ItemStackGrid Bag,Belt;internal Func<bool> Current;}');
replace('public static void Main()=>RebirthCookingOwnerDelta.Exercise();','public static void Main()=>RebirthCookingOwnerDelta.ExerciseTerminal();');
const source=await readFile(join(root,'Scripts/Crafting/Cooking/RebirthCookingOwnerDelta.cs'),'utf8');
for(const [slot,a,b] of [['PLAN','    internal sealed class Plan','    private sealed class Attempt'],['ENCODING','    private static string Cells(','    internal static Plan Decode(']]){const start=source.indexOf(a),end=source.indexOf(b,start);if(start<0||end<=start)throw Error('Production boundary changed '+slot);replace('// ACTUAL_'+slot,source.slice(start,end));}
for(const part of ['Historical','Terminal'])fixture+='\n'+(await readFile(join(root,'Scripts/Crafting/Cooking/RebirthCookingOwnerDelta.'+part+'.cs'),'utf8')).replace(/^using .*;\r?\n/gm,'');
replace('// ACTUAL_HISTORICAL','');fixture+='\n'+await readFile(join(here,'mp042_authenticated_terminal_fixture.cs'),'utf8');
const out=await mkdtemp(join(tmpdir(),'rebirth-mp042-auth-terminal-')),cs=join(out,'actual.cs'),exe=join(out,'actual.exe');await writeFile(cs,fixture);
const sdk=process.env.REBIRTH_DOTNET_SDK_ROOT||'C:/Program Files/dotnet/sdk',versions=(await readdir(sdk)).filter(x=>/^\d+\.\d+\.\d+$/.test(x)).sort((a,b)=>a.localeCompare(b,undefined,{numeric:true}));if(!versions.length)throw Error('Roslyn unavailable; source preserved '+cs);
await run('dotnet',[join(sdk,versions.at(-1),'Roslyn/bincore/csc.dll'),'/nologo','/langversion:latest','/target:exe','/out:'+exe,...['mscorlib','System','System.Core','System.Xml','System.Xml.Linq'].map(x=>'/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/'+x+'.dll'),cs],{windowsHide:true,timeout:30000});console.log((await run(exe,[],{windowsHide:true,timeout:30000})).stdout.trim());
