import {readFile,writeFile,mkdtemp,rm} from 'node:fs/promises';
import {execFile} from 'node:child_process';
import {promisify} from 'node:util';
import {tmpdir} from 'node:os';
import {join,resolve,dirname} from 'node:path';
import {fileURLToPath} from 'node:url';
// PREPARED ONLY. Do not execute before the parent validation checkpoint.
const here=dirname(fileURLToPath(import.meta.url)),root=resolve(here,'../..');
const production=await readFile(join(root,'Scripts/QuickStack/RebirthLogisticsOriginalTransport.cs'),'utf8');
const start=production.indexOf('internal static class RebirthLogisticsOriginalFrame');
const end=production.indexOf('public sealed class NetPackageRebirthLogisticsOriginalRequest',start);
if(start<0||end<=start)throw new Error('Actual frame source extraction boundary changed');
const fixture=await readFile(join(here,'mpA_logistics_transport_frame_fixture.cs'),'utf8');
const temp=await mkdtemp(join(tmpdir(),'rebirth-mpa-frame-')),run=promisify(execFile);
try{
 const cs=join(temp,'check.cs'),exe=join(temp,'check.exe');
 await writeFile(cs,'using System;using System.Linq;using System.Text;using System.Security.Cryptography;\n'+production.slice(start,end)+'\n'+fixture);
 await run('C:/Program Files/dotnet/dotnet.exe',['C:/Program Files/dotnet/sdk/9.0.301/Roslyn/bincore/csc.dll','/nologo','/target:exe','/out:'+exe,...['mscorlib','System','System.Core'].map(x=>'/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/'+x+'.dll'),cs],{windowsHide:true,timeout:10000});
 console.log((await run(exe,[],{windowsHide:true,timeout:10000})).stdout.trim());
}finally{await rm(temp,{recursive:true,force:true});}
