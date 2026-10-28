import {readFile,writeFile,mkdtemp,rm} from 'node:fs/promises';
import {execFile} from 'node:child_process';
import {promisify} from 'node:util';
import {tmpdir} from 'node:os';
import {join,resolve,dirname} from 'node:path';
import {fileURLToPath} from 'node:url';
// PREPARED, NOT RUN. Extracts the actual native CVar death-word decoder only.
// Buff storage is a disclosed double; no native hold/lifecycle/RPC claim.
const here=dirname(fileURLToPath(import.meta.url)),root=resolve(here,'../..');
const source=await readFile(join(root,'Scripts/QuickStack/RebirthLogisticsOwnerReceipt.cs'),'utf8');
const first=source.indexOf('    internal static bool TryOriginalDeaths('),last=source.indexOf('    internal static bool IsHeld(',first);
if(first<0||last<=first)throw new Error('Native death decoder extraction boundary changed');
const fixture=await readFile(join(here,'mpA_logistics_owner_death_scope_fixture.cs'),'utf8');
const temp=await mkdtemp(join(tmpdir(),'rebirth-mpa-life-')),run=promisify(execFile);
try{
 const cs=join(temp,'check.cs'),exe=join(temp,'check.exe');
 await writeFile(cs,'using System;using System.Collections.Generic;\ninternal static class RebirthLogisticsOwnerReceipt{private const string Prefix="rbLogisticsOwner";\n'+source.slice(first,last)+'\ninternal static bool Read(EntityPlayerLocal player,out int deaths)=>TryOriginalDeaths(player,out deaths);}\n'+fixture);
 await run('C:/Program Files/dotnet/dotnet.exe',['C:/Program Files/dotnet/sdk/9.0.301/Roslyn/bincore/csc.dll','/nologo','/target:exe','/out:'+exe,...['mscorlib','System','System.Core'].map(x=>'/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/'+x+'.dll'),cs],{windowsHide:true,timeout:10000});
 console.log((await run(exe,[],{windowsHide:true,timeout:10000})).stdout.trim());
}finally{await rm(temp,{recursive:true,force:true});}
