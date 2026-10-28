// AUTHORED_NOT_RUN. Invoke only after the project-wide coding gate is complete.
// Actual saved-frame matcher; fixture adapters replace native projection/grid codecs.
import {readFile,writeFile,mkdtemp,readdir} from 'node:fs/promises';
import {execFile} from 'node:child_process';import {promisify} from 'node:util';
import {tmpdir} from 'node:os';import {join,resolve,dirname} from 'node:path';import {fileURLToPath} from 'node:url';
const run=promisify(execFile),root=resolve(dirname(fileURLToPath(import.meta.url)),'../..');
function method(source,key){const start=source.indexOf(key);if(start<0||source.indexOf(key,start+1)>=0)throw Error('Unique method required: '+key);const arrow=source.indexOf('=>',start),brace=source.indexOf('{',start);if(arrow>=0&&(brace<0||arrow<brace))return source.slice(start,source.indexOf(';',arrow)+1);let depth=1,end=brace+1;for(;depth&&end<source.length;end++){if(source[end]==='{')depth++;else if(source[end]==='}')depth--;}if(depth)throw Error('Unclosed method');return source.slice(start,end);}
const source=await readFile(join(root,'Scripts/Crafting/Recycling/RebirthProductiveEarnedUiOwnerFile.HostPds.cs'),'utf8');
let fixture=await readFile(join(root,'Tools/GameBridge/mp028_equipment_owner_frame_fixture.cs'),'utf8');
for(const [marker,key] of [['// ACTUAL_MATCHER','internal static bool MatchesDeferredOwnerFrame('],['// ACTUAL_ELEMENTS','private static bool DeferredFrameElementsOnly(']]){if(fixture.split(marker).length!==2)throw Error('Fixture marker '+marker);fixture=fixture.replace(marker,method(source,key));}
const temporary=await mkdtemp(join(tmpdir(),'rebirth-mp028-equipment-frame-')),cs=join(temporary,'actual.cs'),exe=join(temporary,'actual.exe');await writeFile(cs,fixture);
const sdk=process.env.REBIRTH_DOTNET_SDK_ROOT||'C:/Program Files/dotnet/sdk',versions=(await readdir(sdk)).filter(x=>/^\d+\.\d+\.\d+$/.test(x)).sort((a,b)=>a.localeCompare(b,undefined,{numeric:true}));if(!versions.length)throw Error('Roslyn unavailable; source retained '+cs);
await run('dotnet',[join(sdk,versions.at(-1),'Roslyn/bincore/csc.dll'),'/nologo','/target:exe','/out:'+exe,...['mscorlib','System','System.Core','System.Xml','System.Xml.Linq'].map(x=>'/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/'+x+'.dll'),cs],{windowsHide:true,timeout:30000});
console.log((await run(exe,[],{windowsHide:true,timeout:30000})).stdout.trim());
