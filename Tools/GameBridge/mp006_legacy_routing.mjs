// AUTHORED_NOT_RUN. Actual entrypoints/guard; native transport and grids are doubles.
import {readFile,writeFile,mkdtemp,readdir} from 'node:fs/promises';
import {execFile} from 'node:child_process';import {promisify} from 'node:util';
import {tmpdir} from 'node:os';import {join,resolve,dirname} from 'node:path';import {fileURLToPath} from 'node:url';
const run=promisify(execFile),root=resolve(dirname(fileURLToPath(import.meta.url)),'../..');
function section(source,key){const start=source.indexOf(key);if(start<0||source.indexOf(key,start+1)>=0)throw Error('Unique source required '+key);const brace=source.indexOf('{',start),arrow=source.indexOf('=>',start);if(arrow>=0&&arrow<brace)return source.slice(start,source.indexOf(';',arrow)+1);let n=1,end=brace+1;for(;n&&end<source.length;end++){if(source[end]==='{')n++;else if(source[end]==='}')n--;}if(n)throw Error('Unclosed source');return source.slice(start,end);}
const core=await readFile(join(root,'Scripts/QuickStack/QuickStackCore.cs'),'utf8'),ext=await readFile(join(root,'Scripts/QuickStack/QuickStackExtensions.cs'),'utf8'),service=await readFile(join(root,'Scripts/QuickStack/LogisticsTransferService.cs'),'utf8');
let fixture=await readFile(join(root,'Tools/GameBridge/mp006_legacy_routing_fixture.cs'),'utf8');
for(const [marker,body] of [
 ['// ACTUAL_DEPOSIT',section(section(core,'public static class QuickStackService'),'public static void RequestLocal(')],
 ['// ACTUAL_RESTOCK',section(section(ext,'public static class QuickStackRestockService'),'public static void RequestLocal(')],
 ['// ACTUAL_OLD_DEPOSIT',section(section(core,'public sealed class NetPackageQuickStackRequest'),'public override void ProcessPackage(')],
 ['// ACTUAL_OLD_RESTOCK',section(section(ext,'public sealed class NetPackageQuickStackRestockRequest'),'public override void ProcessPackage(')],
 ['// ACTUAL_OWNER_GUARD',section(service,'internal static bool CanUseLegacyOwner(')]
]){if(fixture.split(marker).length!==2)throw Error(marker);fixture=fixture.replace(marker,body);}
const out=await mkdtemp(join(tmpdir(),'rebirth-mp006-routing-')),cs=join(out,'actual.cs'),exe=join(out,'actual.exe');await writeFile(cs,fixture);
const sdk=process.env.REBIRTH_DOTNET_SDK_ROOT||'C:/Program Files/dotnet/sdk',versions=(await readdir(sdk)).filter(x=>/^\d+\.\d+\.\d+$/.test(x)).sort((a,b)=>a.localeCompare(b,undefined,{numeric:true}));if(!versions.length)throw Error('Roslyn unavailable; source preserved '+cs);
await run('dotnet',[join(sdk,versions.at(-1),'Roslyn/bincore/csc.dll'),'/nologo','/target:exe','/out:'+exe,...['mscorlib','System','System.Core'].map(x=>'/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/'+x+'.dll'),cs],{windowsHide:true,timeout:30000});
console.log((await run(exe,[],{windowsHide:true,timeout:30000})).stdout.trim());
