// AUTHORED_NOT_RUN. Native execution and codecs are explicit fixture adapters.
import {readFile,writeFile,mkdtemp,readdir} from 'node:fs/promises';import {execFile} from 'node:child_process';import {promisify} from 'node:util';
import {tmpdir} from 'node:os';import {join,resolve,dirname} from 'node:path';import {fileURLToPath} from 'node:url';
const run=promisify(execFile),root=resolve(dirname(fileURLToPath(import.meta.url)),'../..');
const paths=['Scripts/Crafting/Recycling/RebirthRecipeOutputIntegration.OwnerPrefix.cs','Scripts/Crafting/Recycling/RebirthRecipeOutputIntegration.UiOriginal.cs',
 'Scripts/Survivor/Progression/RebirthWorkstationCompletionAdditionalOutputSaved.cs','Scripts/Crafting/Recycling/RebirthRecipeOutputIntegration.PersonalQueueSaved.cs',
 'Scripts/Crafting/Recycling/RebirthRecipeOutputIntegration.OwnerLoad.cs','Scripts/Crafting/Recycling/RebirthRecipeOutputIntegration.RecoveryGate.cs','Scripts/Crafting/RemoteCrafting/RebirthRemoteResourcePlacedStationSavedReceipt.cs',
 'Tools/GameBridge/mp028_owner_prefix_save_fixture.cs','Tools/GameBridge/mp028_personal_queue_save_fixture.cs','Tools/GameBridge/mp028_owner_load_fixture.cs',
 'Scripts/Crafting/Recycling/RebirthRecipeOutputIntegration.PersonalColdTerminal.cs'];
const [owner,original,remote,queue,load,gate,receipt,template,queueCases,loadCases,coldTerminal]=await Promise.all(paths.map(path=>readFile(join(root,path),'utf8')));
function slice(source,start,end){const a=source.indexOf(start),b=source.indexOf(end,a+start.length);if(a<0||b<=a||source.indexOf(start,a+1)>=0)throw Error('Unique source boundary required '+start);return source.slice(a,b);}
function inject(source,marker,value){if(source.split(marker).length!==2)throw Error('Unique fixture marker required '+marker);return source.replace(marker,value);}
let adapted=template;
for(const name of ['PERSONAL_QUEUE','NATIVE_LOAD','RECEIPT']){const start=' // EXCLUDED_'+name+'_BEGIN',end=' // EXCLUDED_'+name+'_END';adapted=adapted.replace(slice(adapted,start,end)+end,'');}
adapted=inject(adapted,'// ACTUAL_PUBLICATION',slice(original,'    private const int UiOriginalMaximum=','    private sealed class UiOutputPending')+
 slice(original,'    private static string UiOriginalHash(','    internal static object BeginUiOutputEffect('));
let cases=inject(queueCases,'// PERSONAL_PENDING_SOURCE',slice(original,'    private sealed class UiOutputPending','    private static bool HasPendingUiOutput('));
cases=inject(cases,'// ACTUAL_QUEUE_OBSERVER',slice(original,'    private static void ObserveUiOutputQueue(','    // Actual additional-output source only.'));
const strip=source=>source.replace(/^using[^\r\n]+;\r?$/gm,'');
const fixture='using System;using System.IO;using System.Linq;using System.Text;using System.Xml;using System.Xml.Linq;using System.Collections.Generic;using System.Runtime.CompilerServices;\n'+
 [owner,remote,queue,load,gate,receipt,coldTerminal].map(strip).join('\n')+'\n'+adapted+'\n'+cases+'\n'+loadCases;
const temporary=await mkdtemp(join(tmpdir(),'rebirth-mp028-owner-load-')),cs=join(temporary,'actual.cs'),exe=join(temporary,'actual.exe');await writeFile(cs,fixture);
const sdk=process.env.REBIRTH_DOTNET_SDK_ROOT||'C:/Program Files/dotnet/sdk',versions=(await readdir(sdk)).filter(x=>/^\d+\.\d+\.\d+$/.test(x)).sort((a,b)=>a.localeCompare(b,undefined,{numeric:true}));if(!versions.length)throw Error('Roslyn unavailable; source retained '+cs);
await run('dotnet',[join(sdk,versions.at(-1),'Roslyn/bincore/csc.dll'),'/nologo','/target:exe','/main:PersonalOwnerLoadCases','/out:'+exe,...['mscorlib','System','System.Core','System.Xml','System.Xml.Linq'].map(x=>'/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/'+x+'.dll'),cs],{windowsHide:true,timeout:30000});
console.log((await run(exe,[],{windowsHide:true,timeout:30000})).stdout.trim());
