// AUTHORED_NOT_RUN. Invoke only after all production/scenario coding is complete.
import {readFile,writeFile,mkdtemp,readdir} from 'node:fs/promises';
import {execFile} from 'node:child_process';import {promisify} from 'node:util';
import {tmpdir} from 'node:os';import {join,resolve,dirname} from 'node:path';import {fileURLToPath} from 'node:url';
const root=resolve(dirname(fileURLToPath(import.meta.url)),'../..'),run=promisify(execFile);
function block(text,key){const start=text.indexOf(key);if(start<0||text.indexOf(key,start+1)>=0)throw Error('Unique actual method required: '+key);let at=text.indexOf('{',start)+1,depth=1;for(;depth&&at<text.length;at++){if(text[at]==='{')depth++;else if(text[at]==='}')depth--;}if(depth)throw Error('Unclosed actual method');return text.slice(start,at);}
const source=await readFile(join(root,'Scripts/Crafting/Cooking/RebirthCookingPull.cs'),'utf8');
const packet=block(source,'public sealed class NetPackageRebirthCookingPullOriginal:NetPackage');
const result=block(source,'public sealed class NetPackageRebirthCookingPulledOriginal:NetPackage');
const grid=await readFile(join(root,'Scripts/Crafting/Cooking/RebirthCookingGridCustody.cs'),'utf8');
let fixture=await readFile(join(root,'Tools/GameBridge/mp010_pull_life_handler_fixture.cs'),'utf8');
fixture=fixture.replace(/\r\n/g,'\n');
const first=packet.indexOf('private const uint Version='),last=packet.indexOf('public override NetPackageDirection',first);
if(first<0||last<=first)throw Error('Actual fields/version boundary changed');
const resultFirst=result.indexOf('private const uint Version='),resultLast=result.indexOf('public override NetPackageDirection',resultFirst);
if(resultFirst<0||resultLast<=resultFirst)throw Error('Actual result fields/version boundary changed');
for(const [marker,value] of [['// ACTUAL_FIELDS',packet.slice(first,last)],['// ACTUAL_READ',block(packet,'public override void read(')],['// ACTUAL_WRITE',block(packet,'public override void write(')],['// ACTUAL_HANDLER',block(packet,'public override void ProcessPackage(')],['// ACTUAL_READ_STRING',block(grid,'internal static string ReadWireString(')],['// ACTUAL_WRITE_STRING',block(grid,'internal static void WriteWireString(')]]){
 if(fixture.split(marker+'\n').length!==2)throw Error('Unique source marker required '+marker);fixture=fixture.replace(marker+'\n',value+'\n');
}
for(const [marker,value] of [['// ACTUAL_RESULT_FIELDS',result.slice(resultFirst,resultLast)],['// ACTUAL_RESULT_READ',block(result,'public override void read(')],['// ACTUAL_RESULT_WRITE',block(result,'public override void write(')]]){
 if(fixture.split(marker+'\n').length!==2)throw Error('Unique result marker required '+marker);fixture=fixture.replace(marker+'\n',value+'\n');
}
const out=await mkdtemp(join(tmpdir(),'rebirth-mp010-life-')),cs=join(out,'actual.cs'),exe=join(out,'actual.exe');await writeFile(cs,fixture);
const sdk=process.env.REBIRTH_DOTNET_SDK_ROOT||'C:/Program Files/dotnet/sdk',versions=(await readdir(sdk)).filter(x=>/^\d+\.\d+\.\d+$/.test(x)).sort((a,b)=>a.localeCompare(b,undefined,{numeric:true}));if(!versions.length)throw Error('Roslyn unavailable; source preserved '+cs);
await run('dotnet',[join(sdk,versions.at(-1),'Roslyn/bincore/csc.dll'),'/nologo','/target:exe','/out:'+exe,...['mscorlib','System','System.Core','System.Xml','System.Xml.Linq'].map(x=>'/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/'+x+'.dll'),cs],{windowsHide:true,timeout:30000});
console.log((await run(exe,[],{windowsHide:true,timeout:30000})).stdout.trim());
