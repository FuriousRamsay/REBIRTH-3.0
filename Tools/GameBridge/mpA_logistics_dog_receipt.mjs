import {readFile,writeFile,mkdtemp,rm} from 'node:fs/promises';
import {execFile} from 'node:child_process';
import {promisify} from 'node:util';
import {tmpdir} from 'node:os';
import {join,resolve,dirname} from 'node:path';
import {fileURLToPath} from 'node:url';
// PREPARED, NOT RUN. Actual receipt/stage and dog CAS method; native aggregate,
// item codec, identity/world and publication are explicit fixture doubles.
const here=dirname(fileURLToPath(import.meta.url)),root=resolve(here,'../..');
const domain=await readFile(join(root,'Scripts/QuickStack/RebirthLogisticsDogTransfer.cs'),'utf8');
const dog=await readFile(join(root,'Scripts/NPC/Dog/RebirthDogSystems.cs'),'utf8');
const a=domain.indexOf('    internal static string Identity('),b=dog.indexOf('    internal static bool CommitLogisticsOriginalSlots('),c=dog.indexOf('    // Named fetch original only:',b);
if(a<0||b<0||c<=b)throw new Error('Actual dog receipt/CAS extraction anchors changed');
const fixture=await readFile(join(here,'mpA_logistics_dog_receipt_fixture.cs'),'utf8');
const source='using System;\nusing System.IO;\nusing System.Linq;\nusing System.Collections.Generic;\nusing System.Globalization;\nusing System.Xml;\ninternal static class RebirthLogisticsDogTransfer{private const string Element="rebirthLogisticsInventoryReceipt";\n'+domain.slice(a)+'\n'+fixture.replace('/* ACTUAL_DOG_CAS */',dog.slice(b,c));
const temp=await mkdtemp(join(tmpdir(),'rebirth-mpa-dog-receipt-')),run=promisify(execFile);
try{
 const cs=join(temp,'check.cs'),exe=join(temp,'check.exe');await writeFile(cs,source);
 await run('C:/Program Files/dotnet/dotnet.exe',['C:/Program Files/dotnet/sdk/9.0.301/Roslyn/bincore/csc.dll','/nologo','/target:exe','/out:'+exe,...['mscorlib','System','System.Core','System.Xml'].map(x=>'/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/'+x+'.dll'),cs],{windowsHide:true,timeout:10000});
 console.log((await run(exe,[],{windowsHide:true,timeout:10000})).stdout.trim());
}finally{await rm(temp,{recursive:true,force:true});}
