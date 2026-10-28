// AUTHORED_NOT_RUN. Do not invoke until all production/scenario coding is done.
// Extract actual admission, known-upload gate and saved-custody methods; native
// serialization/event/file adapters in the fixture remain explicit doubles.
import {readFile,writeFile,mkdtemp,readdir} from 'node:fs/promises';
import {execFile} from 'node:child_process';import {promisify} from 'node:util';
import {tmpdir} from 'node:os';import {join,resolve,dirname} from 'node:path';import {fileURLToPath} from 'node:url';
const run=promisify(execFile),root=resolve(dirname(fileURLToPath(import.meta.url)),'../..');
function method(source,key){const start=source.indexOf(key);if(start<0||source.indexOf(key,start+1)>=0)throw Error('Unique production method required: '+key);const arrow=source.indexOf('=>',start),brace=source.indexOf('{',start);if(arrow>=0&&arrow<brace)return source.slice(start,source.indexOf(';',arrow)+1);let depth=1,end=brace+1;for(;depth&&end<source.length;end++){if(source[end]==='{')depth++;else if(source[end]==='}')depth--;}if(depth)throw Error('Unclosed production method');return source.slice(start,end);}
const host=await readFile(join(root,'Scripts/Crafting/Recycling/RebirthProductiveEarnedUiDeferredTransport.SameOwnerNativeHost.cs'),'utf8');
const pds=await readFile(join(root,'Scripts/Crafting/Recycling/RebirthProductiveEarnedUiDeferredTransport.NativeViewerPds.cs'),'utf8');
const saved=pds.slice(pds.indexOf('internal sealed partial class SavedViewerFile'),pds.indexOf('private sealed class Attempt'));
let fixture=await readFile(join(root,'Tools/GameBridge/mpB_015_same_owner_fixture.cs'),'utf8');
for(const [marker,actual] of [
    ['// PRODUCTION_PHASE_ADMISSION',method(host,'internal static bool TryAdmitRemoteNativeEvent(')],
    ['// PRODUCTION_UPLOAD_GATE',method(pds,'internal static bool CanProcessUpload(')],
    ['// PRODUCTION_HEAD_MATCH',method(saved,'internal bool MatchesPreviousHead(')],
    ['// PRODUCTION_SAVED_CUSTODY',method(saved,'internal bool SavedCustodyCurrent(')]
]){if(fixture.split(marker).length!==2)throw Error('Fixture marker '+marker);fixture=fixture.replace(marker,actual);}
const temporary=await mkdtemp(join(tmpdir(),'rebirth-mp015-same-owner-')),cs=join(temporary,'actual.cs'),exe=join(temporary,'actual.exe');await writeFile(cs,fixture);
const sdk=process.env.REBIRTH_DOTNET_SDK_ROOT||'C:/Program Files/dotnet/sdk',versions=(await readdir(sdk)).filter(x=>/^\d+\.\d+\.\d+$/.test(x)).sort((a,b)=>a.localeCompare(b,undefined,{numeric:true}));if(!versions.length)throw Error('Roslyn unavailable; source retained '+cs);
await run('dotnet',[join(sdk,versions.at(-1),'Roslyn/bincore/csc.dll'),'/nologo','/target:exe','/out:'+exe,...['mscorlib','System','System.Core','System.Xml','System.Xml.Linq'].map(x=>'/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/'+x+'.dll'),cs],{windowsHide:true,timeout:30000});
console.log((await run(exe,[],{windowsHide:true,timeout:30000})).stdout.trim());
// Retain source/executable for later review; no game/network/mod build here.
