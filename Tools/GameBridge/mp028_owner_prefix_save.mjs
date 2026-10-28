// AUTHORED_NOT_RUN. Actual local/remote owner-prefix observer, Saved capability
// and publication. Native Saved production/PDS/save calls are explicit adapters.
import {readFile,writeFile,mkdtemp,readdir} from 'node:fs/promises';
import {execFile} from 'node:child_process';import {promisify} from 'node:util';
import {tmpdir} from 'node:os';import {join,resolve,dirname} from 'node:path';import {fileURLToPath} from 'node:url';
const run=promisify(execFile),root=resolve(dirname(fileURLToPath(import.meta.url)),'../..');
const [observer,original,remote,template]=await Promise.all([
 readFile(join(root,'Scripts/Crafting/Recycling/RebirthRecipeOutputIntegration.OwnerPrefix.cs'),'utf8'),
 readFile(join(root,'Scripts/Crafting/Recycling/RebirthRecipeOutputIntegration.UiOriginal.cs'),'utf8'),
 readFile(join(root,'Scripts/Survivor/Progression/RebirthWorkstationCompletionAdditionalOutputSaved.cs'),'utf8'),
 readFile(join(root,'Tools/GameBridge/mp028_owner_prefix_save_fixture.cs'),'utf8')]);
function slice(source,start,end){const a=source.indexOf(start),b=source.indexOf(end,a+start.length);if(a<0||b<=a||source.indexOf(start,a+1)>=0)throw Error('Unique actual source boundary required '+start);return source.slice(a,b);}
const stream=slice(original,'    private const int UiOriginalMaximum=','    private sealed class UiOutputPending');
const publication=slice(original,'    private static string UiOriginalHash(','    internal static object BeginUiOutputEffect(');
if(template.split('// ACTUAL_PUBLICATION').length!==2)throw Error('Publication fixture boundary changed');
const fixture='using System;using System.IO;using System.Linq;using System.Text;using System.Xml;using System.Xml.Linq;using System.Collections.Generic;using System.Runtime.CompilerServices;\n'+
 observer.replace(/^using[^\r\n]+;\r?$/gm,'')+'\n'+remote.replace(/^using[^\r\n]+;\r?$/gm,'')+'\n'+template.replace('// ACTUAL_PUBLICATION',stream+publication);
const temporary=await mkdtemp(join(tmpdir(),'rebirth-mp028-owner-save-')),cs=join(temporary,'actual.cs'),exe=join(temporary,'actual.exe');await writeFile(cs,fixture);
const sdk=process.env.REBIRTH_DOTNET_SDK_ROOT||'C:/Program Files/dotnet/sdk',versions=(await readdir(sdk)).filter(x=>/^\d+\.\d+\.\d+$/.test(x)).sort((a,b)=>a.localeCompare(b,undefined,{numeric:true}));if(!versions.length)throw Error('Roslyn unavailable; source retained '+cs);
await run('dotnet',[join(sdk,versions.at(-1),'Roslyn/bincore/csc.dll'),'/nologo','/target:exe','/out:'+exe,...['mscorlib','System','System.Core','System.Xml','System.Xml.Linq'].map(x=>'/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/'+x+'.dll'),cs],{windowsHide:true,timeout:30000});
console.log((await run(exe,[],{windowsHide:true,timeout:30000})).stdout.trim());
