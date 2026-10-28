// AUTHORED_NOT_RUN. Do not invoke before the user-authorized coding gate ends.
// Extracts the actual pure codec; never launches/builds/installs the game/mod.
import {readFile,writeFile,mkdtemp,readdir} from 'node:fs/promises';
import {execFile} from 'node:child_process';import {promisify} from 'node:util';
import {tmpdir} from 'node:os';import {join,resolve,dirname} from 'node:path';import {fileURLToPath} from 'node:url';
const run=promisify(execFile),root=resolve(dirname(fileURLToPath(import.meta.url)),'../..');
function body(source,key){const start=source.indexOf(key);if(start<0||source.indexOf(key,start+1)>=0)throw Error('Unique production body required: '+key);const brace=source.indexOf('{',start);let level=1,end=brace+1;for(;level&&end<source.length;end++){if(source[end]==='{')level++;else if(source[end]==='}')level--;}if(level)throw Error('Unclosed production body');return source.slice(start,end);}
const codec=await readFile(join(root,'Scripts/Crafting/Recycling/RebirthProductiveEarnedUiDeferredTransport.HydrationMetadata.cs'),'utf8');
const host=await readFile(join(root,'Scripts/Crafting/Recycling/RebirthProductiveEarnedUiDeferredTransport.HydrationHost.cs'),'utf8');
const client=await readFile(join(root,'Scripts/Crafting/Recycling/RebirthProductiveEarnedUiDeferredTransport.HydrationClient.cs'),'utf8');
let fixture=await readFile(join(root,'Tools/GameBridge/mp015_hydration_metadata_fixture.cs'),'utf8');
for(const [marker,value] of [
 ['// PRODUCTION_COUNTER',body(codec,'private sealed class HydrationMetadataCounter')],
 ['// PRODUCTION_MEASURE',body(codec,'internal static long MeasureHydrationMetadataElement(')],
 ['// PRODUCTION_HASH',body(host,'private static string HydrationHash(')],
 ['// PRODUCTION_GUID',body(client,'private static Guid ReadGuid(')],
 ['// PRODUCTION_PACK',body(codec,'private static bool PackHydrationMetadata(')],
 ['// PRODUCTION_XML',body(codec,'private static XElement ReadHydrationMetadataXml(')],
 ['// PRODUCTION_EXPAND',body(codec,'private static XElement ExpandHydrationMetadata(')]
]){if(fixture.split(marker).length!==2)throw Error('Fixture marker '+marker);fixture=fixture.replace(marker,value);}
const output=await mkdtemp(join(tmpdir(),'rebirth-mp015-metadata-')),cs=join(output,'actual.cs'),exe=join(output,'actual.exe');await writeFile(cs,fixture);
const sdk=process.env.REBIRTH_DOTNET_SDK_ROOT||'C:/Program Files/dotnet/sdk',versions=(await readdir(sdk)).filter(x=>/^\d+\.\d+\.\d+$/.test(x)).sort((a,b)=>a.localeCompare(b,undefined,{numeric:true}));if(!versions.length)throw Error('Roslyn unavailable; extracted source preserved '+cs);
await run('dotnet',[join(sdk,versions.at(-1),'Roslyn/bincore/csc.dll'),'/nologo','/target:exe','/out:'+exe,...['mscorlib','System','System.Core','System.Xml','System.Xml.Linq'].map(x=>'/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/'+x+'.dll'),cs],{windowsHide:true,timeout:30000});
console.log((await run(exe,[],{windowsHide:true,timeout:30000})).stdout.trim());
