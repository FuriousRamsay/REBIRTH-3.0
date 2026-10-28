// AUTHORED_NOT_RUN. Execute only after all production/scenario coding is complete.
import {readFile,writeFile,mkdtemp,readdir} from 'node:fs/promises';import {execFile} from 'node:child_process';import {promisify} from 'node:util';
import {tmpdir} from 'node:os';import {join,resolve,dirname} from 'node:path';import {fileURLToPath} from 'node:url';
const root=resolve(dirname(fileURLToPath(import.meta.url)),'../..'),run=promisify(execFile);
const source=await readFile(join(root,'Scripts/Crafting/Cooking/RebirthCookingGridCustody.cs'),'utf8'),key='private static void Tick(ref ModEvents.SGameUpdateData data)',start=source.indexOf(key);
if(start<0||source.indexOf(key,start+1)>=0)throw Error('Unique actual grid Tick required');
let end=source.indexOf('{',start)+1,depth=1;for(;depth&&end<source.length;end++){if(source[end]==='{')depth++;else if(source[end]==='}')depth--;}if(depth)throw Error('Unclosed Tick');
let fixture=await readFile(join(root,'Tools/GameBridge/mp010_grid_saved_after_tick_fixture.cs'),'utf8');if(fixture.split('// ACTUAL_TICK').length!==2)throw Error('Unique source slot required');fixture=fixture.replace('// ACTUAL_TICK',source.slice(start,end));
const out=await mkdtemp(join(tmpdir(),'rebirth-mp010-saved-tick-')),cs=join(out,'actual.cs'),exe=join(out,'actual.exe');await writeFile(cs,fixture);
const sdk=process.env.REBIRTH_DOTNET_SDK_ROOT||'C:/Program Files/dotnet/sdk',versions=(await readdir(sdk)).filter(x=>/^\d+\.\d+\.\d+$/.test(x)).sort((a,b)=>a.localeCompare(b,undefined,{numeric:true}));if(!versions.length)throw Error('Roslyn unavailable; source preserved '+cs);
await run('dotnet',[join(sdk,versions.at(-1),'Roslyn/bincore/csc.dll'),'/nologo','/langversion:latest','/target:exe','/out:'+exe,...['mscorlib','System','System.Core','System.Xml','System.Xml.Linq'].map(x=>'/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/'+x+'.dll'),cs],{windowsHide:true,timeout:30000});
console.log((await run(exe,[],{windowsHide:true,timeout:30000})).stdout.trim());
