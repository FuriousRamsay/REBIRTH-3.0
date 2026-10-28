// AUTHORED_NOT_RUN. Run only after all production and scenario coding is complete.
import {readFile,writeFile,mkdtemp,readdir} from 'node:fs/promises';import {execFile} from 'node:child_process';import {promisify} from 'node:util';
import {tmpdir} from 'node:os';import {join,resolve,dirname} from 'node:path';import {fileURLToPath} from 'node:url';
const root=resolve(dirname(fileURLToPath(import.meta.url)),'../..'),run=promisify(execFile);
function block(source,key){const start=source.indexOf(key);if(start<0||source.indexOf(key,start+1)>=0)throw Error('Unique actual block required '+key);let at=source.indexOf('{',start)+1,depth=1;for(;depth&&at<source.length;at++){if(source[at]==='{')depth++;else if(source[at]==='}')depth--;}if(depth)throw Error('Unclosed source block');return source.slice(start,at);}
const grid=await readFile(join(root,'Scripts/Crafting/Cooking/RebirthCookingGridCustody.cs'),'utf8');
const terminal=await readFile(join(root,'Scripts/Crafting/Cooking/RebirthCookingGridCustody.FetchedTerminal.cs'),'utf8');
const binary=await readFile(join(root,'Scripts/Vehicles/Camper/RebirthCamperTransferJournal.cs'),'utf8');
const identity=await readFile(join(root,'Scripts/Survivor/Persistence/RebirthStablePlayerIdentity.cs'),'utf8');
const durable=await readFile(join(root,'Scripts/Crafting/RemoteCrafting/RebirthRemoteResourceDurableJournal.cs'),'utf8');
let fixture=await readFile(join(root,'Tools/GameBridge/mp010_fetched_terminal_fixture.cs'),'utf8');
const journalStart=grid.indexOf('private static RebirthCamperTransferJournal Journal()=>'),journalEnd=grid.indexOf(';',journalStart);
if(journalStart<0||journalEnd<=journalStart)throw Error('Grid journal boundary changed');
for(const [marker,value] of [['// ACTUAL_TERMINAL_PARTIAL',terminal.replace(/^using .*;\r?\n/gm,'')],['// ACTUAL_BINARY_JOURNAL',binary.replace(/^using .*;\r?\n/gm,'')],['// ACTUAL_OWNER_KEY',block(identity,'public static string ComputeStorageKey(')],['// ACTUAL_DIGEST',block(durable,'internal static string Digest(')],['// ACTUAL_OPERATION',block(durable,'internal static Guid Operation(')],['// ACTUAL_GRID_JOURNAL',grid.slice(journalStart,journalEnd+1)],['// ACTUAL_GRID_RECORDS',block(grid,'private static IEnumerable<RebirthCamperTransferJournal.Record> Records(')],['// ACTUAL_HAS_CLAIM',block(grid,'internal static bool HasFetchClaim(')],['// ACTUAL_GRID_RECOVER',block(grid,'internal static bool Recover(')]]){
 if(fixture.split(marker).length!==2)throw Error('Unique source marker required '+marker);fixture=fixture.replace(marker,value);
}
const out=await mkdtemp(join(tmpdir(),'rebirth-mp010-terminal-')),cs=join(out,'actual.cs'),exe=join(out,'actual.exe');await writeFile(cs,fixture);
const sdk=process.env.REBIRTH_DOTNET_SDK_ROOT||'C:/Program Files/dotnet/sdk',versions=(await readdir(sdk)).filter(x=>/^\d+\.\d+\.\d+$/.test(x)).sort((a,b)=>a.localeCompare(b,undefined,{numeric:true}));if(!versions.length)throw Error('Roslyn unavailable; source preserved '+cs);
await run('dotnet',[join(sdk,versions.at(-1),'Roslyn/bincore/csc.dll'),'/nologo','/langversion:latest','/target:exe','/out:'+exe,...['mscorlib','System','System.Core','System.Xml','System.Xml.Linq'].map(x=>'/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/'+x+'.dll'),cs],{windowsHide:true,timeout:30000});
console.log((await run(exe,[],{windowsHide:true,timeout:30000})).stdout.trim());
