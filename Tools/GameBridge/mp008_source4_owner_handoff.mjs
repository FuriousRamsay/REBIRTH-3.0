// AUTHORED_NOT_RUN. Actual Source4 adapter; native publication/issuer are adapters.
import {readFile,writeFile,mkdtemp,readdir} from 'node:fs/promises';
import {execFile} from 'node:child_process';import {promisify} from 'node:util';
import {tmpdir} from 'node:os';import {join,resolve,dirname} from 'node:path';import {fileURLToPath} from 'node:url';
const root=resolve(dirname(fileURLToPath(import.meta.url)),'../..'),run=promisify(execFile);
const actual=await readFile(join(root,'Scripts/Crafting/Recycling/RebirthProductiveEarnedUiQueueOwner.Source4.cs'),'utf8');
const key='internal static bool AdoptSavedSource4(',start=actual.indexOf(key);if(start<0||actual.indexOf(key,start+1)>=0)throw Error('Unique actual Source4 adapter required');
let end=actual.indexOf('{',start)+1,depth=1;for(;depth&&end<actual.length;end++){if(actual[end]==='{')depth++;else if(actual[end]==='}')depth--;}
if(depth)throw Error('Actual Source4 adapter incomplete');
const fixture=await readFile(join(root,'Tools/GameBridge/mp008_source4_owner_handoff_fixture.cs'),'utf8');
const temp=await mkdtemp(join(tmpdir(),'rebirth-mp008-source4-')),cs=join(temp,'actual.cs'),exe=join(temp,'actual.exe');
const receiptStart=actual.indexOf('private static XElement Source4Receipt(');if(receiptStart<0||receiptStart>=start)throw Error('Actual receipt composer missing');
await writeFile(cs,'using System;using System.IO;using System.Text;using System.Xml.Linq;\ninternal static partial class RebirthProductiveEarnedUiQueueOwner{'+actual.slice(receiptStart,start)+actual.slice(start,end)+'}\n'+fixture);
const sdk=process.env.REBIRTH_DOTNET_SDK_ROOT||'C:/Program Files/dotnet/sdk';
const versions=(await readdir(sdk)).filter(x=>/^\d+\.\d+\.\d+$/.test(x)).sort((a,b)=>a.localeCompare(b,undefined,{numeric:true}));
if(!versions.length)throw Error('Roslyn unavailable; source retained '+cs);
await run('dotnet',[join(sdk,versions.at(-1),'Roslyn/bincore/csc.dll'),'/nologo','/target:exe','/out:'+exe,...['mscorlib','System','System.Core','System.Xml','System.Xml.Linq'].map(x=>'/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/'+x+'.dll'),cs],{windowsHide:true,timeout:30000});
console.log((await run(exe,[],{windowsHide:true,timeout:30000})).stdout.trim());
