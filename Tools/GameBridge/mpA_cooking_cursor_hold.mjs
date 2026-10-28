import {readFile,writeFile,mkdtemp,rm} from 'node:fs/promises';
import {execFile} from 'node:child_process';
import {promisify} from 'node:util';
import {tmpdir} from 'node:os';
import {join,resolve,dirname} from 'node:path';
import {fileURLToPath} from 'node:url';
// PREPARED, NOT RUN. Execution requires the parent's separate test checkpoint.
const here=dirname(fileURLToPath(import.meta.url)),root=resolve(here,'../..');
const actual=await readFile(join(root,'Scripts/Crafting/Cooking/RebirthCookingOutputDelivery.cs'),'utf8');
const start=actual.indexOf('// Fence the gesture before its first cursor/native-slot side effect.');
const end=actual.indexOf('[HarmonyPatch(typeof(XUiC_RecipeStack)',start);
if(start<0||end<=start)throw new Error('Actual cursor fence extraction boundaries changed');
const fixture=await readFile(join(here,'mpA_cooking_cursor_hold_fixture.cs'),'utf8');
// Personal recipe queue guards following these cursor classes have their own
// cohort; this extraction retains every cursor/slot gesture class in full.
const source='using System;\nusing System.Collections.Generic;\nusing System.Reflection;\nusing HarmonyLib;\n'+actual.slice(start,end)+'\n'+fixture;
const temp=await mkdtemp(join(tmpdir(),'rebirth-mpa-cursor-')),run=promisify(execFile);
try{
 const cs=join(temp,'check.cs'),exe=join(temp,'check.exe');await writeFile(cs,source);
 await run('C:/Program Files/dotnet/dotnet.exe',['C:/Program Files/dotnet/sdk/9.0.301/Roslyn/bincore/csc.dll','/nologo','/target:exe','/out:'+exe,...['mscorlib','System','System.Core'].map(x=>'/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/'+x+'.dll'),cs],{windowsHide:true,timeout:10000});
 console.log((await run(exe,[],{windowsHide:true,timeout:10000})).stdout.trim());
}finally{await rm(temp,{recursive:true,force:true});}
