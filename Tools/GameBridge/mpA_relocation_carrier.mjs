import {readFile,writeFile,mkdtemp,rm} from 'node:fs/promises';
import {execFile} from 'node:child_process';
import {promisify} from 'node:util';
import {tmpdir} from 'node:os';
import {join,resolve,dirname} from 'node:path';
import {fileURLToPath} from 'node:url';
// Prepared only; extracts the actual production equivalence helper.
const here=dirname(fileURLToPath(import.meta.url)),root=resolve(here,'../..');
const source=await readFile(join(root,'Scripts/Vehicles/Camper/RebirthCamperStationRelocationCorrelation.cs'),'utf8');
function method(signature){const a=source.indexOf(signature);if(a<0)throw Error(signature);let b=source.indexOf('{',a),depth=1,i=b+1;for(;depth&&i<source.length;i++){if(source[i]==='{')depth++;if(source[i]==='}')depth--;}return source.slice(a,i);}
const fixture=await readFile(join(here,'mpA_relocation_carrier_fixture.cs'),'utf8');
const body='using System; using System.Security.Cryptography;\npublic static class MpACorrelation {\n'+method('    internal static string Hash(')+'\n'+method('    internal static bool SameItem(')+'\n}\n'+fixture;
const temp=await mkdtemp(join(tmpdir(),'rebirth-mpa-carrier-')),run=promisify(execFile);
try {const cs=join(temp,'check.cs'),exe=join(temp,'check.exe');await writeFile(cs,body);
 await run('C:/Program Files/dotnet/dotnet.exe',['C:/Program Files/dotnet/sdk/9.0.301/Roslyn/bincore/csc.dll','/nologo','/target:exe','/out:'+exe,...['mscorlib','System','System.Core'].map(x=>'/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/'+x+'.dll'),cs],{windowsHide:true,timeout:10000});
 console.log((await run(exe,[],{windowsHide:true,timeout:10000})).stdout.trim());
}finally{await rm(temp,{recursive:true,force:true});}
