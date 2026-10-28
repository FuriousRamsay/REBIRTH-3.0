import {readFile,writeFile,mkdtemp,rm} from 'node:fs/promises';
import {execFile} from 'node:child_process';
import {promisify} from 'node:util';
import {tmpdir} from 'node:os';
import {join,resolve,dirname} from 'node:path';
import {fileURLToPath} from 'node:url';
// PREPARED, NOT RUN. The native ECD codec is an explicit bounded fixture double.
const here=dirname(fileURLToPath(import.meta.url)),root=resolve(here,'../..');
const actual=await readFile(join(root,'Scripts/QuickStack/RebirthLogisticsDeathWitness.cs'),'utf8');
const start=actual.indexOf('internal static class RebirthLogisticsEntity47');
const end=actual.indexOf('internal sealed class RebirthLogisticsBackpackPublication',start);
if(start<0||end<=start)throw new Error('Actual parser extraction anchors changed');
const reservation=await readFile(join(root,'Scripts/QuickStack/RebirthLogisticsSnapshotReservation.cs'),'utf8');
const maximum=reservation.match(/internal const int MaximumNativeBytes=[^;]+;/);
if(!maximum)throw Error('Actual snapshot bound missing');
const fixture=(await readFile(join(here,'mpA_logistics_entity47_fixture.cs'),'utf8'))+'\ninternal static class RebirthLogisticsSnapshotReservation {'+maximum[0]+'}\n';
const source='using System;\nusing System.IO;\nusing System.Linq;\n'+actual.slice(start,end)+'\n'+fixture;
const temp=await mkdtemp(join(tmpdir(),'rebirth-mpa-entity47-')),run=promisify(execFile);
try{
 const cs=join(temp,'check.cs'),exe=join(temp,'check.exe');await writeFile(cs,source);
 await run('C:/Program Files/dotnet/dotnet.exe',['C:/Program Files/dotnet/sdk/9.0.301/Roslyn/bincore/csc.dll','/nologo','/target:exe','/out:'+exe,...['mscorlib','System','System.Core'].map(x=>'/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/'+x+'.dll'),cs],{windowsHide:true,timeout:10000});
 console.log((await run(exe,[],{windowsHide:true,timeout:10000})).stdout.trim());
}finally{await rm(temp,{recursive:true,force:true});}
