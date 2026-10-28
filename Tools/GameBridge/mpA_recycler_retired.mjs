// AUTHORED_NOT_RUN. Do not invoke before the all-coding gate is lifted.
import {readFile,writeFile,mkdtemp,readdir} from 'node:fs/promises';import {execFile} from 'node:child_process';import {promisify} from 'node:util';
import {tmpdir} from 'node:os';import {join,resolve,dirname} from 'node:path';import {fileURLToPath} from 'node:url';
const run=promisify(execFile),root=resolve(dirname(fileURLToPath(import.meta.url)),'../..');let fixture=await readFile(join(root,'Tools/GameBridge/mpA_recycler_retired_fixture.cs'),'utf8');
const source=await readFile(join(root,'Scripts/Crafting/Recycling/RebirthRecyclerPlacedSettlement.RemoteRetired.cs'),'utf8'),key='private static bool ReplyPlacedRetired(',start=source.indexOf(key);
if(start<0||source.indexOf(key,start+1)>=0)throw Error('unique actual method required');let end=source.indexOf('{',start)+1,depth=1;
for(;depth&&end<source.length;end++){if(source[end]==='{')depth++;else if(source[end]==='}')depth--;}
if(depth||fixture.split('// PRODUCTION_RETIRED').length!==2)throw Error('actual source boundary');fixture=fixture.replace('// PRODUCTION_RETIRED',source.slice(start,end));
const temporary=await mkdtemp(join(tmpdir(),'rebirth-recycler-retired-')),cs=join(temporary,'actual.cs'),exe=join(temporary,'actual.exe');await writeFile(cs,fixture);
const sdk=process.env.REBIRTH_DOTNET_SDK_ROOT||'C:/Program Files/dotnet/sdk',versions=(await readdir(sdk)).filter(x=>/^\d+\.\d+\.\d+$/.test(x)).sort((a,b)=>a.localeCompare(b,undefined,{numeric:true}));if(!versions.length)throw Error('Roslyn unavailable');
await run('dotnet',[join(sdk,versions.at(-1),'Roslyn/bincore/csc.dll'),'/nologo','/target:exe','/out:'+exe,...['mscorlib','System','System.Core','System.Xml','System.Xml.Linq'].map(x=>'/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/'+x+'.dll'),cs],{windowsHide:true,timeout:30000});
console.log((await run(exe,[],{windowsHide:true,timeout:30000})).stdout.trim());
