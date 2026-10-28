// AUTHORED_NOT_RUN. Execute only after the all-production-coding test gate.
import {readFile, writeFile, mkdtemp, rm, readdir} from 'node:fs/promises';
import {execFile} from 'node:child_process';
import {promisify} from 'node:util';
import {tmpdir} from 'node:os';
import {join, resolve, dirname} from 'node:path';
import {fileURLToPath} from 'node:url';
const run=promisify(execFile), here=dirname(fileURLToPath(import.meta.url)), root=resolve(here,'../..');
const source=await readFile(join(root,'Scripts/Survivor/Repair/RebirthRepairOriginalCorrection.Publication.cs'),'utf8');
const fixture=await readFile(join(here,'mpB_repair_publication_fixture.cs'),'utf8');
const hooks=await readFile(join(root,'Scripts/Survivor/Repair/RebirthRepairSignatureService.cs'),'utf8');
function hook(name){const start=hooks.indexOf('internal static class '+name);if(start<0)throw Error('Missing actual hook '+name);let at=hooks.indexOf('{',start),depth=1,end=at+1;for(;depth&&end<hooks.length;end++){if(hooks[end]==='{')depth++;else if(hooks[end]==='}')depth--;}if(depth)throw Error('Unclosed actual hook');return hooks.slice(start,end).replace(/^\s*\[HarmonyPriority\([^\r\n]+\)\]\s*$/gm,'');}
const actionFixture=await readFile(join(here,'RebirthRepairActionHookFixture.cs'),'utf8');
const correction=await readFile(join(root,'Scripts/Survivor/Repair/RebirthRepairOriginalCorrection.cs'),'utf8');
const marker='private static bool AcceptedNativeUpload(';
const actualStart=correction.indexOf(marker);if(actualStart<0)throw Error('Missing actual native accepted-head predicate');
let actualEnd=correction.indexOf('{',actualStart)+1,actualDepth=1;
for(;actualDepth&&actualEnd<correction.length;actualEnd++){if(correction[actualEnd]==='{')actualDepth++;else if(correction[actualEnd]==='}')actualDepth--;}
if(actualDepth)throw Error('Unclosed actual native accepted-head predicate');
const identityStart=correction.indexOf('private static bool TryRepairIdentity(');if(identityStart<0)throw Error('Missing actual repair identity producer');
let identityEnd=correction.indexOf('{',identityStart)+1,identityDepth=1;
for(;identityDepth&&identityEnd<correction.length;identityEnd++){if(correction[identityEnd]==='{')identityDepth++;else if(correction[identityEnd]==='}')identityDepth--;}
if(identityDepth)throw Error('Unclosed actual repair identity producer');
const actualAcceptance='static partial class RebirthRepairOriginalCorrection {\n'+correction.slice(actualStart,actualEnd)+'\n'+correction.slice(identityStart,identityEnd)+'\n}';
const activationStart=correction.indexOf('internal static class RebirthRepairOriginalActivation');
if(activationStart<0)throw Error('Missing actual activation hook');
let activationEnd=correction.indexOf('{',activationStart)+1,activationDepth=1;
for(;activationDepth&&activationEnd<correction.length;activationEnd++){if(correction[activationEnd]==='{')activationDepth++;else if(correction[activationEnd]==='}')activationDepth--;}
if(activationDepth)throw Error('Unclosed actual activation hook');
const actualActivation=correction.slice(activationStart,activationEnd);
const saveStart=correction.indexOf('private static bool Save(Receipt r,');if(saveStart<0)throw Error('Missing actual original Save');
let saveEnd=correction.indexOf('{',saveStart)+1,saveDepth=1;
for(;saveDepth&&saveEnd<correction.length;saveEnd++){if(correction[saveEnd]==='{')saveDepth++;else if(correction[saveEnd]==='}')saveDepth--;}
if(saveDepth)throw Error('Unclosed actual original Save');
const actualSave='internal static partial class RebirthRepairOriginalCorrection {\n'+correction.slice(saveStart,saveEnd)+'\n}';
const temporary=await mkdtemp(join(tmpdir(),'rebirth-repair-publication-'));
try {
  const sdk=process.env.MPB_DOTNET_SDK_ROOT||'C:/Program Files/dotnet/sdk';
  const versions=(await readdir(sdk)).filter(value=>/^\d+\.\d+\.\d+$/.test(value)).sort((a,b)=>a.localeCompare(b,undefined,{numeric:true}));
  if(!versions.length)throw Error('Roslyn unavailable');
  const file=join(temporary,'fixture.cs'), exe=join(temporary,'fixture.exe');
  await writeFile(file,fixture+'\n'+source.replace(/^using [^\r\n]+;\r?$/gm,'')+'\n'+actualSave);
  await run('dotnet',[join(sdk,versions.at(-1),'Roslyn/bincore/csc.dll'),'/nologo','/target:exe','/out:'+exe,
    ...['mscorlib','System','System.Core','System.Xml','System.Xml.Linq'].map(name=>'/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/'+name+'.dll'),file],{windowsHide:true,timeout:30000});
  console.log((await run(exe,[join(temporary,'save')],{windowsHide:true,timeout:30000})).stdout.trim());
  const actionFile=join(temporary,'actions.cs'),actionExe=join(temporary,'actions.exe');
  await writeFile(actionFile,actionFixture+'\n'+actualAcceptance+'\n'+actualActivation+'\n'+['RebirthRepairSignatureQueuePatch','RebirthRepairSignatureCancelPatch','RebirthRepairSignatureCompletionPatch'].map(hook).join('\n'));
  await run('dotnet',[join(sdk,versions.at(-1),'Roslyn/bincore/csc.dll'),'/nologo','/target:exe','/out:'+actionExe,
    ...['mscorlib','System','System.Core'].map(name=>'/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/'+name+'.dll'),actionFile],{windowsHide:true,timeout:30000});
  console.log((await run(actionExe,[],{windowsHide:true,timeout:30000})).stdout.trim());
} finally {await rm(temporary,{recursive:true,force:true});}
