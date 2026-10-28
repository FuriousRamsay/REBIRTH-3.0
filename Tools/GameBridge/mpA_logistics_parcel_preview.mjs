import {readFile,writeFile,mkdtemp,rm} from 'node:fs/promises';
import {execFile} from 'node:child_process';
import {promisify} from 'node:util';
import {tmpdir} from 'node:os';
import {join,resolve,dirname} from 'node:path';
import {fileURLToPath} from 'node:url';
// PREPARED, NOT RUN. No world/native serializer or compressor correctness claim.
const here=dirname(fileURLToPath(import.meta.url)),root=resolve(here,'../..');
const full=await readFile(join(root,'Scripts/QuickStack/RebirthLogisticsParcelPreview.cs'),'utf8');
const boundary=full.indexOf('// One original planner at a time.');
if(boundary<0)throw new Error('Detached preview extraction boundary changed');
const source=full.slice(0,boundary);
const reservation=await readFile(join(root,'Scripts/QuickStack/RebirthLogisticsSnapshotReservation.cs'),'utf8');
const preflight=await readFile(join(root,'Scripts/QuickStack/RebirthLogisticsNativeBodyIntent.cs'),'utf8');
const frameMaximum=preflight.match(/internal const int MaximumFrame=[^;]+;/);
if(!frameMaximum)throw Error('Actual native frame bound missing');
const maximum=reservation.match(/internal const int MaximumNativeBytes=[^;]+;/);
if(!maximum)throw Error('Actual snapshot bound missing');
const fixture=(await readFile(join(here,'mpA_logistics_parcel_preview_fixture.cs'),'utf8'))+'\ninternal static class RebirthLogisticsSnapshotReservation {'+maximum[0]+'}\ninternal static partial class RebirthLogisticsNativeBodyPreflight {'+frameMaximum[0]+'}\n';
const temp=await mkdtemp(join(tmpdir(),'rebirth-mpa-parcel-')),run=promisify(execFile);
try{
 const cs=join(temp,'check.cs'),exe=join(temp,'check.exe');await writeFile(cs,source+'\n'+fixture);
 await run('C:/Program Files/dotnet/dotnet.exe',['C:/Program Files/dotnet/sdk/9.0.301/Roslyn/bincore/csc.dll','/nologo','/target:exe','/out:'+exe,...['mscorlib','System','System.Core'].map(x=>'/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/'+x+'.dll'),cs],{windowsHide:true,timeout:10000});
 console.log((await run(exe,[],{windowsHide:true,timeout:10000})).stdout.trim());
}finally{await rm(temp,{recursive:true,force:true});}
