// AUTHORED_NOT_RUN. Composes the actual bounded player original history reader.
import {readFile,writeFile,mkdtemp,readdir} from 'node:fs/promises';
import {execFile} from 'node:child_process';import {promisify} from 'node:util';
import {tmpdir} from 'node:os';import {join,resolve,dirname} from 'node:path';import {fileURLToPath} from 'node:url';
const root=resolve(dirname(fileURLToPath(import.meta.url)),'../..'),run=promisify(execFile);
const production=await readFile(join(root,'Scripts/Metabolism/Consumption/RebirthConsumptionDurableDelivery.ColdOriginal.cs'),'utf8');
const boundary=production.indexOf('    private static void PumpLoadedInventoryOriginals()');
if(boundary<0)throw Error('Actual reader boundary missing');
const source=production.slice(0,boundary).replace(/^using .*;\r?\n/gm,'')+'\n}';
const fixture=(await readFile(join(root,'Tools/GameBridge/mp007_player_original_history_fixture.cs'),'utf8')).replace('// ACTUAL_SOURCE',source);
const temp=await mkdtemp(join(tmpdir(),'rebirth-mp007-original-history-')),cs=join(temp,'actual.cs'),exe=join(temp,'actual.exe');await writeFile(cs,fixture);
const sdk=process.env.REBIRTH_DOTNET_SDK_ROOT||'C:/Program Files/dotnet/sdk',versions=(await readdir(sdk)).filter(x=>/^\d+\.\d+\.\d+$/.test(x)).sort((a,b)=>a.localeCompare(b,undefined,{numeric:true}));
if(!versions.length)throw Error('Roslyn unavailable; authored source retained at '+cs);
await run('dotnet',[join(sdk,versions.at(-1),'Roslyn/bincore/csc.dll'),'/nologo','/target:exe','/out:'+exe,...['mscorlib','System','System.Core','System.Xml','System.Xml.Linq'].map(x=>'/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/'+x+'.dll'),cs],{windowsHide:true,timeout:30000});
console.log((await run(exe,[],{windowsHide:true,timeout:30000})).stdout.trim());
