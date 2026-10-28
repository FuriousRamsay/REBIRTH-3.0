// PREPARED NOT RUN. Extracts actual pure production method bodies only.
import {readFile,writeFile,mkdtemp,rm} from 'node:fs/promises';
import {execFile} from 'node:child_process';import {promisify} from 'node:util';
import {tmpdir} from 'node:os';import {join,resolve,dirname,basename,sep} from 'node:path';import {fileURLToPath} from 'node:url';
const run=promisify(execFile),here=dirname(fileURLToPath(import.meta.url)),root=resolve(here,'../..');
function method(source,marker){const at=source.indexOf(marker);if(at<0||source.indexOf(marker,at+marker.length)>=0)throw Error('Missing/ambiguous actual source '+marker);let p=source.indexOf('{',at),depth=0;for(let i=p;i<source.length;i++){if(source[i]==='{')depth++;if(source[i]==='}'&&--depth===0)return source.slice(at,i+1);}throw Error('Unterminated actual method');}
const budget=await readFile(join(root,'Scripts/Crafting/Recycling/RebirthProductiveEarnedObservationBudget.cs'),'utf8');
let fixture=await readFile(join(here,'mpA_earned_observation_budget_fixture.cs'),'utf8');
async function constants(path,names){const text=await readFile(join(root,path),'utf8');return names.map(name=>{const hits=[...text.matchAll(new RegExp('(?:internal|public|private) const (?:int|long) '+name+'\\s*=[^;]+;','g'))];if(hits.length!==1)throw Error('Actual constant changed '+name);return hits[0][0].replace('private const','internal const');}).join('\n');}
const inserts=[['// ACTUAL_BUDGET',budget.slice(budget.indexOf('internal sealed class'))],
['// ACTUAL_ITEM_BOUNDS',await constants('Scripts/Inventory/RebirthNativeItemConformanceReader.cs',['MaximumNodes','MaximumChildSlots','MaximumAggregateStringBytes'])],
['// ACTUAL_WORKSPACE_BOUND',await constants('Scripts/Crafting/RemoteCrafting/RebirthRemoteResourcePlacedStationArtifact.cs',['MaximumOperationWorkspace'])],
['// ACTUAL_BODY_BOUND',await constants('Scripts/Crafting/Recycling/RebirthProductiveEarnedNativeBody.cs',['MaximumBodyBytes'])],
['// ACTUAL_ROW_BOUNDS',await constants('Scripts/Crafting/Recycling/RebirthProductiveEarnedRowProof.cs',['MaximumFrameBytes','MaximumArtifactBytes'])],
['// ACTUAL_SNAPSHOT_BOUND',await constants('Scripts/Survivor/Persistence/RebirthStationSnapshotEvidence.cs',['MaximumSnapshotBytes'])],
['// ACTUAL_PAYLOAD_BOUND',await constants('Scripts/Survivor/Persistence/RebirthStationRegionPayload.cs',['SectorBytes','MaximumPayloadBytes'])]];
for(const [marker,value] of inserts){if(fixture.split(marker).length!==2)throw Error('Insertion mismatch '+marker);fixture=fixture.replace(marker,value);}
const temp=await mkdtemp(join(tmpdir(),'rebirth-earned-budget-'));
try{const cs=join(temp,'fixture.cs'),exe=join(temp,'fixture.exe');await writeFile(cs,fixture);await run('C:/Program Files/dotnet/dotnet.exe',['C:/Program Files/dotnet/sdk/9.0.301/Roslyn/bincore/csc.dll','/nologo','/langversion:latest','/target:exe','/out:'+exe,...['mscorlib','System','System.Core'].map(n=>'/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/'+n+'.dll'),cs],{windowsHide:true,timeout:20000});console.log((await run(exe,[],{windowsHide:true,timeout:20000})).stdout.trim());}
finally{if(process.env.REBIRTH_KEEP_FIXTURE==='1')console.log(temp);else{if(!resolve(temp).toLowerCase().startsWith((resolve(tmpdir())+sep).toLowerCase())||!basename(temp).startsWith('rebirth-earned-budget-'))throw Error('Refuse cleanup outside generated temp');await rm(temp,{recursive:true});}}
