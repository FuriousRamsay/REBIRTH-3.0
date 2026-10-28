import {readFile,writeFile,mkdtemp,rm} from 'node:fs/promises';
import {execFile} from 'node:child_process';
import {promisify} from 'node:util';
import {tmpdir} from 'node:os';
import {join,resolve,dirname} from 'node:path';
import {fileURLToPath} from 'node:url';
// PREPARED, NOT RUN. Actual pure partition/owed-plan source; native integration is not simulated.
const here=dirname(fileURLToPath(import.meta.url)),root=resolve(here,'../..');
const source=await readFile(join(root,'Scripts/QuickStack/RebirthLogisticsDeathWitness.cs'),'utf8');
const first=source.indexOf('internal static class RebirthLogisticsDeathPartition'),last=source.indexOf('internal static class RebirthLogisticsDeathJournal',first);
if(first<0||last<=first)throw new Error('Death partition extraction boundary changed');
const bounds=source.match(/internal const int MaximumBytes=[^;]+MaximumParcels=[^;]+;/);
if(!bounds)throw Error('Actual death parcel bounds missing');
const fixture=(await readFile(join(here,'mpA_logistics_death_partition_fixture.cs'),'utf8')).replace('/* ACTUAL_DEATH_BOUNDS */',bounds[0]);
const temp=await mkdtemp(join(tmpdir(),'rebirth-mpa-death-')),run=promisify(execFile);
try{
 const cs=join(temp,'check.cs'),exe=join(temp,'check.exe');
 await writeFile(cs,'using System;using System.Linq;using System.Collections.Generic;\n'+source.slice(first,last)+'\n'+fixture);
 await run('C:/Program Files/dotnet/dotnet.exe',['C:/Program Files/dotnet/sdk/9.0.301/Roslyn/bincore/csc.dll','/nologo','/target:exe','/out:'+exe,...['mscorlib','System','System.Core'].map(x=>'/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/'+x+'.dll'),cs],{windowsHide:true,timeout:10000});
 console.log((await run(exe,[],{windowsHide:true,timeout:10000})).stdout.trim());
}finally{await rm(temp,{recursive:true,force:true});}
