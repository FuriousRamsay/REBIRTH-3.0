// AUTHORED_NOT_RUN. Actual historical source metadata reader; native saved
// effect and native publication are explicit adapters in this source cohort.
import {readFile,writeFile,mkdtemp,readdir} from 'node:fs/promises';import {execFile} from 'node:child_process';import {promisify} from 'node:util';import {tmpdir} from 'node:os';import {join,resolve,dirname} from 'node:path';import {fileURLToPath} from 'node:url';
const root=resolve(dirname(fileURLToPath(import.meta.url)),'../..'),run=promisify(execFile);
const source=(await readFile(join(root,'Scripts/Crafting/RemoteCrafting/RebirthRemoteResourceDurableOutcome.CookingSources.cs'),'utf8')).replace(/^using .*;\r?\n/gm,'');
const fixture=(await readFile(join(root,'Tools/GameBridge/mp010_entered_source_metadata_fixture.cs'),'utf8')).replace('// ACTUAL_SOURCE',source);
const temp=await mkdtemp(join(tmpdir(),'rebirth-mp010-entered-source-')),cs=join(temp,'actual.cs'),exe=join(temp,'actual.exe');await writeFile(cs,fixture);
const sdk=process.env.REBIRTH_DOTNET_SDK_ROOT||'C:/Program Files/dotnet/sdk',versions=(await readdir(sdk)).filter(x=>/^\d+\.\d+\.\d+$/.test(x)).sort((a,b)=>a.localeCompare(b,undefined,{numeric:true}));if(!versions.length)throw Error('Roslyn unavailable; authored source retained at '+cs);
await run('dotnet',[join(sdk,versions.at(-1),'Roslyn/bincore/csc.dll'),'/nologo','/target:exe','/out:'+exe,...['mscorlib','System','System.Core','System.Xml','System.Xml.Linq'].map(x=>'/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/'+x+'.dll'),cs],{windowsHide:true,timeout:30000});console.log((await run(exe,[],{windowsHide:true,timeout:30000})).stdout.trim());
