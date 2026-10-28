// AUTHORED_NOT_RUN. Actual per-output entry/acceptance + whole native-order predictor.
// Source-current, publication, native item codecs and setter execution are adapters.
import {readFile,writeFile,mkdtemp,readdir} from 'node:fs/promises';
import {execFile} from 'node:child_process';import {promisify} from 'node:util';
import {tmpdir} from 'node:os';import {join,resolve,dirname} from 'node:path';import {fileURLToPath} from 'node:url';
const run=promisify(execFile),root=resolve(dirname(fileURLToPath(import.meta.url)),'../..');
function method(source,key){const start=source.indexOf(key);if(start<0||source.indexOf(key,start+1)>=0)throw Error('Unique production method required: '+key);const brace=source.indexOf('{',start);let depth=1,end=brace+1;for(;depth&&end<source.length;end++){if(source[end]==='{')depth++;else if(source[end]==='}')depth--;}if(depth)throw Error('Unclosed method');return source.slice(start,end);}
const original=await readFile(join(root,'Scripts/Crafting/Recycling/RebirthRecipeOutputIntegration.UiOriginal.cs'),'utf8');
const prediction=await readFile(join(root,'Scripts/Crafting/Recycling/RebirthRecipeOutputInventoryPrediction.cs'),'utf8');
const doubles=await readFile(join(root,'Tools/GameBridge/mpA_additional_inventory_prediction_fixture.cs'),'utf8');
let fixture=await readFile(join(root,'Tools/GameBridge/mp028_output_prefix_fixture.cs'),'utf8');
for(const [marker,key] of [['// ACTUAL_ENTER','internal void EnterOutput('],['// ACTUAL_ACCEPT','internal void ObserveOutput('],['// ACTUAL_RESUME','private static bool ResumeUiOutput('],['// ACTUAL_FINISH_RESUME','private static bool FinishUiOutputInventoryResume('],['// ACTUAL_CACHED_RESUME','private static bool BeginUiOutputCachedResume('],['// ACTUAL_CACHED_PRIMARY','private static bool ConsumeUiOutputCachedPrimary(']]){if(fixture.split(marker).length!==2)throw Error('Fixture marker '+marker);fixture=fixture.replace(marker,method(original,key));}
const acceptedStart=original.indexOf('internal bool AcceptedOwnerCurrent()=>'),acceptedEnd=original.indexOf('internal void ObserveOutput(',acceptedStart);
if(acceptedStart<0||acceptedEnd<=acceptedStart)throw Error('Accepted owner comparison boundary changed');
if(fixture.split('// OWNER_CURRENT_SOURCE').length!==2)throw Error('Accepted owner fixture marker changed');
fixture=fixture.replace('// OWNER_CURRENT_SOURCE',original.slice(acceptedStart,acceptedEnd));
fixture='using System;using System.IO;using System.Linq;using System.Text;using System.Xml.Linq;using System.Collections.Generic;using XUiM_PlayerInventory=PrefixInventory;\n'+
 doubles.slice(doubles.indexOf('class XUiC_ItemStack'),doubles.indexOf('// PRODUCTION_PREDICTOR'))+
 prediction.slice(prediction.indexOf('internal sealed class RebirthRecipeOutputInventoryPrediction'))+'\n'+fixture;
const temporary=await mkdtemp(join(tmpdir(),'rebirth-mp028-prefix-')),cs=join(temporary,'actual.cs'),exe=join(temporary,'actual.exe');await writeFile(cs,fixture);
const sdk=process.env.REBIRTH_DOTNET_SDK_ROOT||'C:/Program Files/dotnet/sdk',versions=(await readdir(sdk)).filter(x=>/^\d+\.\d+\.\d+$/.test(x)).sort((a,b)=>a.localeCompare(b,undefined,{numeric:true}));if(!versions.length)throw Error('Roslyn unavailable; source retained '+cs);
await run('dotnet',[join(sdk,versions.at(-1),'Roslyn/bincore/csc.dll'),'/nologo','/target:exe','/out:'+exe,...['mscorlib','System','System.Core','System.Xml','System.Xml.Linq'].map(x=>'/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/'+x+'.dll'),cs],{windowsHide:true,timeout:30000});
console.log((await run(exe,[],{windowsHide:true,timeout:30000})).stdout.trim());
