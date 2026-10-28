import {readFile,writeFile,mkdtemp,rm} from 'node:fs/promises';
import {execFile} from 'node:child_process';
import {promisify} from 'node:util';
import {tmpdir} from 'node:os';
import {join,resolve,dirname} from 'node:path';
import {fileURLToPath} from 'node:url';
// PREPARED, NOT RUN. Actual fetch dog receipt/CAS, two-cell native aggregate,
// DTO, actor authorization, item codec and final terminal-reader doubles.
const here=dirname(fileURLToPath(import.meta.url)),root=resolve(here,'../..');
const domain=await readFile(join(root,'Scripts/QuickStack/RebirthRemoteResourceDogTransfer.cs'),'utf8'),dog=await readFile(join(root,'Scripts/NPC/Dog/RebirthDogSystems.cs'),'utf8');
const a=domain.indexOf('    internal static bool Current('),b=dog.indexOf('    internal static bool CommitRemoteResourceOriginalSlots('),c=dog.indexOf('    public static bool CanAcceptAll(',b);
if(a<0||b<0||c<=b)throw new Error('Actual fetch dog receipt/CAS extraction anchors changed');
let fixture=await readFile(join(here,'mpA_logistics_dog_receipt_fixture.cs'),'utf8');fixture=fixture.slice(0,fixture.indexOf('public static class Fixture'));
fixture=fixture.replace('/* ACTUAL_DOG_CAS */',dog.slice(b,c));
const tail=await readFile(join(here,'mpA_remote_dog_effect_fixture.cs'),'utf8');
const source='using System.Text;\nusing System.Collections.Generic;\nusing System.Globalization;\n'+domain.slice(0,a)+' internal static bool Current(EntityPlayer actor,RebirthRemoteResourceNpcEffectPlan plan)=>!actor.Wrong;\n}\n'+fixture+tail;
const temp=await mkdtemp(join(tmpdir(),'rebirth-mpa-fetch-dog-')),run=promisify(execFile);
try{const cs=join(temp,'check.cs'),exe=join(temp,'check.exe');await writeFile(cs,source);await run('C:/Program Files/dotnet/dotnet.exe',['C:/Program Files/dotnet/sdk/9.0.301/Roslyn/bincore/csc.dll','/nologo','/target:exe','/out:'+exe,...['mscorlib','System','System.Core','System.Xml','System.Xml.Linq'].map(x=>'/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/'+x+'.dll'),cs],{windowsHide:true,timeout:10000});console.log((await run(exe,[],{windowsHide:true,timeout:10000})).stdout.trim());}finally{await rm(temp,{recursive:true,force:true});}
