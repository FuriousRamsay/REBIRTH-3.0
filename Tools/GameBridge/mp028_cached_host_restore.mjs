// AUTHORED_NOT_RUN. Do not invoke before all production/scenario coding.
import {readFile,writeFile,mkdtemp,readdir} from 'node:fs/promises';import {execFile} from 'node:child_process';import {promisify} from 'node:util';import {tmpdir} from 'node:os';import {join,resolve,dirname} from 'node:path';import {fileURLToPath} from 'node:url';
const root=resolve(dirname(fileURLToPath(import.meta.url)),'../..'),run=promisify(execFile);
const [prefixSource,finalSource,stageSource,template]=await Promise.all(['Scripts/Crafting/Recycling/RebirthProductiveEarnedUiDeferredTransport.CachedOutputRestart.cs','Scripts/Crafting/Recycling/RebirthProductiveEarnedUiDeferredTransport.CachedFinalOwnerRestart.cs','Scripts/Crafting/Recycling/RebirthProductiveEarnedUiDeferredTransport.RemoteOutputPrefixRestart.cs','Tools/GameBridge/mp028_cached_host_restore_fixture.cs'].map(path=>readFile(join(root,path),'utf8')));
const source=prefixSource+'\n'+finalSource;
const signature='private static SavedViewerFile RestoreRemoteOutputStage(',start=stageSource.indexOf(signature);
if(start<0||stageSource.indexOf(signature,start+1)>=0)throw Error('Unique production saved-stage reader required');
let end=stageSource.indexOf('{',start)+1,depth=1;
for(;depth&&end<stageSource.length;end++){if(stageSource[end]==='{')depth++;else if(stageSource[end]==='}')depth--;}
if(depth)throw Error('Complete production saved-stage reader required');
const fixture=template.replace('// ACTUAL_SAVED_STAGE_READER',stageSource.slice(start,end)).replace('// ACTUAL_CACHED_HOST_RESTORE',source.replace(/^using[^\r\n]+;\r?$/gm,''));
const temp=await mkdtemp(join(tmpdir(),'rebirth-mp028-cached-host-')),cs=join(temp,'actual.cs'),exe=join(temp,'actual.exe');await writeFile(cs,fixture);
const sdk=process.env.REBIRTH_DOTNET_SDK_ROOT||'C:/Program Files/dotnet/sdk',versions=(await readdir(sdk)).filter(v=>/^\d+\.\d+\.\d+$/.test(v)).sort((a,b)=>a.localeCompare(b,undefined,{numeric:true}));if(!versions.length)throw Error('Roslyn unavailable; source retained '+cs);
await run('dotnet',[join(sdk,versions.at(-1),'Roslyn/bincore/csc.dll'),'/nologo','/target:exe','/out:'+exe,...['mscorlib','System','System.Core','System.Xml','System.Xml.Linq'].map(x=>'/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/'+x+'.dll'),cs],{windowsHide:true,timeout:30000});console.log((await run(exe,[],{windowsHide:true,timeout:30000})).stdout.trim());
