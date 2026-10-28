// AUTHORED_NOT_RUN. Run only after the all-coding checkpoint.
import {readFile,writeFile,mkdtemp,readdir} from 'node:fs/promises';
import {execFile} from 'node:child_process';
import {promisify} from 'node:util';
import {tmpdir} from 'node:os';
import {join,resolve,dirname} from 'node:path';
import {fileURLToPath} from 'node:url';
const root=resolve(dirname(fileURLToPath(import.meta.url)),'../..'),run=promisify(execFile);
const [preparation,admission,template]=await Promise.all(['Scripts/NPC/Persistence/RebirthNpcSaveQuarantinePreparation.cs','Scripts/NPC/Persistence/RebirthNpcServerNativeAdmission.cs','Tools/GameBridge/mp049_native_admission_fixture.cs'].map(path=>readFile(join(root,path),'utf8')));
function member(source,signature){const start=source.indexOf(signature);if(start<0||source.indexOf(signature,start+1)>=0)throw Error('Unique production member required: '+signature);let end=source.indexOf('{',start)+1,depth=1;for(;depth&&end<source.length;end++){if(source[end]==='{')depth++;else if(source[end]==='}')depth--;}if(depth)throw Error('Incomplete production member');return source.slice(start,end);}
const requiredStart=preparation.indexOf('private static readonly string[] Required='),requiredEnd=preparation.indexOf(';',requiredStart);
if(requiredStart<0||requiredEnd<requiredStart)throw Error('Actual production roster unavailable');
const fixture=template.replace('// ACTUAL_REQUIRED',preparation.slice(requiredStart,requiredEnd+1)).replace('// ACTUAL_MEMBERSHIP',member(preparation,'internal static bool IsDetachedServerMembership(')).replace('// ACTUAL_COVERAGE',member(preparation,'internal static bool IsExactServerCoverage(')).replace('// ACTUAL_ADMISSION',admission.replace(/^using[^\r\n]+;\r?$/gm,''));
const temp=await mkdtemp(join(tmpdir(),'rebirth-mp049-native-admission-')),cs=join(temp,'actual.cs'),exe=join(temp,'actual.exe');await writeFile(cs,fixture);
const sdk=process.env.REBIRTH_DOTNET_SDK_ROOT||'C:/Program Files/dotnet/sdk',versions=(await readdir(sdk)).filter(v=>/^\d+\.\d+\.\d+$/.test(v)).sort((a,b)=>a.localeCompare(b,undefined,{numeric:true}));if(!versions.length)throw Error('Roslyn unavailable; source retained '+cs);
await run('dotnet',[join(sdk,versions.at(-1),'Roslyn/bincore/csc.dll'),'/nologo','/target:exe','/out:'+exe,...['mscorlib','System','System.Core'].map(x=>'/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/'+x+'.dll'),cs],{windowsHide:true,timeout:30000});
console.log((await run(exe,[],{windowsHide:true,timeout:30000})).stdout.trim());
