// AUTHORED_NOT_RUN. Actual Block-only policy/planner/native-policy qualifier
// and actual durable settlement-element validator. Native grid read is adapted.
import {readFile,writeFile,mkdtemp,readdir} from 'node:fs/promises';import {execFile} from 'node:child_process';import {promisify} from 'node:util';import {tmpdir} from 'node:os';import {join,resolve,dirname} from 'node:path';import {fileURLToPath} from 'node:url';
const root=resolve(dirname(fileURLToPath(import.meta.url)),'../..'),run=promisify(execFile);
function member(text,key){const start=text.indexOf(key);if(start<0||text.indexOf(key,start+1)>=0)throw Error('Unique member required '+key);let end=text.indexOf('{',start)+1,depth=1;for(;depth&&end<text.length;end++){if(text[end]==='{')depth++;else if(text[end]==='}')depth--;}if(depth)throw Error('Incomplete member');return text.slice(start,end);}
const policy=await readFile(join(root,'Scripts/Crafting/RemoteCrafting/RebirthRemoteResourceBlockRefund.BeltPolicy.cs'),'utf8');
const actual=['internal static XElement BeltPolicy(','internal static bool ReadBeltPolicy(','private static bool SavedBeltPolicy(','internal static bool Plan(','private static void Insert('].map(key=>member(policy,key)).join('\n');
const journal=await readFile(join(root,'Scripts/Crafting/RemoteCrafting/RebirthRemoteResourceDurableJournal.cs'),'utf8');
let fixture=(await readFile(join(root,'Tools/GameBridge/mp009_block_belt_policy_fixture.cs'),'utf8')).replace('// ACTUAL_POLICY',actual).replace('// ACTUAL_SCHEMA',member(journal,'internal static void ValidateSettlementElement('));
const temp=await mkdtemp(join(tmpdir(),'rebirth-block-belt-')),cs=join(temp,'actual.cs'),exe=join(temp,'actual.exe');await writeFile(cs,fixture);
const sdk=process.env.REBIRTH_DOTNET_SDK_ROOT||'C:/Program Files/dotnet/sdk',versions=(await readdir(sdk)).filter(x=>/^\d+\.\d+\.\d+$/.test(x)).sort((a,b)=>a.localeCompare(b,undefined,{numeric:true}));if(!versions.length)throw Error('Roslyn unavailable; source retained '+cs);
await run('dotnet',[join(sdk,versions.at(-1),'Roslyn/bincore/csc.dll'),'/nologo','/target:exe','/out:'+exe,...['mscorlib','System','System.Core','System.Xml','System.Xml.Linq'].map(x=>'/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/'+x+'.dll'),cs],{windowsHide:true,timeout:30000});console.log((await run(exe,[],{windowsHide:true,timeout:30000})).stdout.trim());
