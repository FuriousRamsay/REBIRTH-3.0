// AUTHORED_NOT_RUN. Do not invoke before the all-coding gate is lifted.
import {readFile,writeFile,mkdtemp,readdir} from 'node:fs/promises';import {execFile} from 'node:child_process';import {promisify} from 'node:util';
import {tmpdir} from 'node:os';import {join,resolve,dirname} from 'node:path';import {fileURLToPath} from 'node:url';
const run=promisify(execFile),root=resolve(dirname(fileURLToPath(import.meta.url)),'../..');
const source=await readFile(join(root,'Scripts/Crafting/Recycling/RebirthRecyclerPlacedSettlement.Plan.cs'),'utf8');
const key='internal bool MatchesOwnerCheckpoint(',start=source.indexOf(key);if(start<0||source.indexOf(key,start+1)>=0)throw Error('Unique production checkpoint required');
const brace=source.indexOf('{',start);let depth=1,end=brace+1;for(;depth&&end<source.length;end++){if(source[end]==='{')depth++;else if(source[end]==='}')depth--;}if(depth)throw Error('Unclosed production checkpoint');
let fixture=await readFile(join(root,'Tools/GameBridge/mpA_recycler_owner_checkpoint_fixture.cs'),'utf8');if(fixture.split('// PRODUCTION_CHECKPOINT').length!==2)throw Error('Fixture marker');fixture=fixture.replace('// PRODUCTION_CHECKPOINT',source.slice(start,end));
const ownerLoad=await readFile(join(root,'Scripts/Crafting/Recycling/RebirthRecyclerPlacedSettlement.OwnerLoad.cs'),'utf8');
const readKey='private static bool CompletedOwnerRead(',readStart=ownerLoad.indexOf(readKey);if(readStart<0||ownerLoad.indexOf(readKey,readStart+1)>=0)throw Error('Unique production read boundary required');
let readEnd=ownerLoad.indexOf('{',readStart)+1,readDepth=1;for(;readDepth&&readEnd<ownerLoad.length;readEnd++){if(ownerLoad[readEnd]==='{')readDepth++;else if(ownerLoad[readEnd]==='}')readDepth--;}
if(readDepth||fixture.split('// PRODUCTION_READ_BOUNDARY').length!==2)throw Error('Read boundary fixture marker');fixture=fixture.replace('// PRODUCTION_READ_BOUNDARY',ownerLoad.slice(readStart,readEnd));
const ownerFile=await readFile(join(root,'Scripts/Crafting/Recycling/RebirthRecyclerPlacedSettlement.OwnerFile.cs'),'utf8');
const stageKey='private static bool ValidOwnerCheckpointStage(',stageStart=ownerFile.indexOf(stageKey);if(stageStart<0||ownerFile.indexOf(stageKey,stageStart+1)>=0)throw Error('Unique checkpoint stage required');
let stageEnd=ownerFile.indexOf('{',stageStart)+1,stageDepth=1;for(;stageDepth&&stageEnd<ownerFile.length;stageEnd++){if(ownerFile[stageEnd]==='{')stageDepth++;else if(ownerFile[stageEnd]==='}')stageDepth--;}
if(stageDepth||fixture.split('// PRODUCTION_STAGE').length!==2)throw Error('Stage fixture marker');fixture=fixture.replace('// PRODUCTION_STAGE',ownerFile.slice(stageStart,stageEnd));
const temporary=await mkdtemp(join(tmpdir(),'rebirth-recycler-checkpoint-')),cs=join(temporary,'actual.cs'),exe=join(temporary,'actual.exe');await writeFile(cs,fixture);
const sdk=process.env.REBIRTH_DOTNET_SDK_ROOT||'C:/Program Files/dotnet/sdk',versions=(await readdir(sdk)).filter(x=>/^\d+\.\d+\.\d+$/.test(x)).sort((a,b)=>a.localeCompare(b,undefined,{numeric:true}));if(!versions.length)throw Error('Roslyn unavailable; source retained '+cs);
await run('dotnet',[join(sdk,versions.at(-1),'Roslyn/bincore/csc.dll'),'/nologo','/target:exe','/out:'+exe,...['mscorlib','System','System.Core'].map(x=>'/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/'+x+'.dll'),cs],{windowsHide:true,timeout:30000});
console.log((await run(exe,[],{windowsHide:true,timeout:30000})).stdout.trim());
