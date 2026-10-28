// AUTHORED_NOT_RUN. Run only after all production/scenario coding.
import {readFile,writeFile,mkdtemp,readdir} from 'node:fs/promises';import {execFile} from 'node:child_process';import {promisify} from 'node:util';
import {tmpdir} from 'node:os';import {join,resolve,dirname} from 'node:path';import {fileURLToPath} from 'node:url';
const root=resolve(dirname(fileURLToPath(import.meta.url)),'../..'),run=promisify(execFile);
const [source,template]=await Promise.all(['Scripts/Survivor/Progression/RebirthWorkstationCompletionRemoteOutputDeferredClient.cs','Tools/GameBridge/mp028_loaded_cached_role_fixture.cs'].map(path=>readFile(join(root,path),'utf8')));
const method=source.slice(source.indexOf('    private static bool IsLoadedDeferredCachedFrame('),source.indexOf('    private sealed class LoadedDeferredLease'));
const temp=await mkdtemp(join(tmpdir(),'rebirth-mp028-loaded-cached-')),cs=join(temp,'actual.cs'),exe=join(temp,'actual.exe');await writeFile(cs,template.replace('// ACTUAL_CACHED_ROLE',method));
const sdk=process.env.REBIRTH_DOTNET_SDK_ROOT||'C:/Program Files/dotnet/sdk',versions=(await readdir(sdk)).filter(v=>/^\d+\.\d+\.\d+$/.test(v)).sort((a,b)=>a.localeCompare(b,undefined,{numeric:true}));if(!versions.length)throw Error('Roslyn unavailable; source retained '+cs);
await run('dotnet',[join(sdk,versions.at(-1),'Roslyn/bincore/csc.dll'),'/nologo','/target:exe','/out:'+exe,'/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/mscorlib.dll',cs],{windowsHide:true,timeout:30000});console.log((await run(exe,[],{windowsHide:true,timeout:30000})).stdout.trim());
