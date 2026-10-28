// PREPARED NOT RUN. Extracts actual pure production method bodies only.
import {readFile,writeFile,mkdtemp,rm} from 'node:fs/promises';
import {execFile} from 'node:child_process';import {promisify} from 'node:util';
import {tmpdir} from 'node:os';import {join,resolve,dirname,basename,sep} from 'node:path';import {fileURLToPath} from 'node:url';
const run=promisify(execFile),here=dirname(fileURLToPath(import.meta.url)),root=resolve(here,'../..');
function method(source,marker){const at=source.indexOf(marker);if(at<0||source.indexOf(marker,at+marker.length)>=0)throw Error('Missing/ambiguous actual source '+marker);let p=source.indexOf('{',at),depth=0;for(let i=p;i<source.length;i++){if(source[i]==='{')depth++;if(source[i]==='}'&&--depth===0)return source.slice(at,i+1);}throw Error('Unterminated actual method');}
const saved=await readFile(join(root,'Scripts/Vehicles/Camper/RebirthCamperSavedVehicle.cs'),'utf8');
let fixture=await readFile(join(here,'mpA_earned_mobile_budget_fixture.cs'),'utf8');
for(const [marker,source] of [['// ACTUAL_COUNT_STREAM','    internal sealed class CountStream:Stream'],['// ACTUAL_PREFLIGHT','    private static bool TryMeasureVehicleRecord('],['// ACTUAL_SKIP','    private static void Skip('],['// ACTUAL_FLAG','    private static bool Flag(']]){if(fixture.split(marker).length!==2)throw Error('Insertion mismatch');fixture=fixture.replace(marker,method(saved,source));}
const temp=await mkdtemp(join(tmpdir(),'rebirth-earned-mobile-budget-'));
try{const cs=join(temp,'fixture.cs'),exe=join(temp,'fixture.exe');await writeFile(cs,fixture);await run('C:/Program Files/dotnet/dotnet.exe',['C:/Program Files/dotnet/sdk/9.0.301/Roslyn/bincore/csc.dll','/nologo','/langversion:latest','/target:exe','/out:'+exe,...['mscorlib','System','System.Core'].map(n=>'/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/'+n+'.dll'),cs],{windowsHide:true,timeout:20000});console.log((await run(exe,[],{windowsHide:true,timeout:20000})).stdout.trim());}
finally{if(process.env.REBIRTH_KEEP_FIXTURE==='1')console.log(temp);else{if(!resolve(temp).toLowerCase().startsWith((resolve(tmpdir())+sep).toLowerCase())||!basename(temp).startsWith('rebirth-earned-mobile-budget-'))throw Error('Refuse cleanup outside generated temp');await rm(temp,{recursive:true});}}
