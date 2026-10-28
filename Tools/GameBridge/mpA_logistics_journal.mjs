import {readFile,writeFile,mkdtemp,rm} from 'node:fs/promises';
import {execFile} from 'node:child_process';
import {promisify} from 'node:util';
import {tmpdir} from 'node:os';
import {join,resolve,dirname} from 'node:path';
import {fileURLToPath} from 'node:url';
// PREPARED, NOT RUN. Source compilation/execution awaits parent test checkpoint.
const here=dirname(fileURLToPath(import.meta.url)),root=resolve(here,'../..');
const production=await readFile(join(root,'Scripts/QuickStack/RebirthLogisticsTransferJournal.cs'),'utf8');
const planning=await readFile(join(root,'Scripts/QuickStack/RebirthLogisticsPlanning.cs'),'utf8');
const boundary=planning.indexOf('// Pure native-stack plan composition.');
const overlay=planning.indexOf('// Scoped routing overlay');
if(boundary<0||overlay<=boundary)throw new Error('Plan extraction boundary changed');
const source=production+'\n'+planning.slice(boundary,overlay);
const fixture=await readFile(join(here,'mpA_logistics_journal_fixture.cs'),'utf8');
const temp=await mkdtemp(join(tmpdir(),'rebirth-mpa-logistics-')),run=promisify(execFile);
try{
 const cs=join(temp,'check.cs'),exe=join(temp,'check.exe');await writeFile(cs,source+'\n'+fixture);
 await run('C:/Program Files/dotnet/dotnet.exe',['C:/Program Files/dotnet/sdk/9.0.301/Roslyn/bincore/csc.dll','/nologo','/target:exe','/out:'+exe,...['mscorlib','System','System.Core','System.Xml','System.Xml.Linq'].map(x=>'/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/'+x+'.dll'),cs],{windowsHide:true,timeout:10000});
 console.log((await run(exe,[temp],{windowsHide:true,timeout:10000})).stdout.trim());
}finally{await rm(temp,{recursive:true,force:true});}
