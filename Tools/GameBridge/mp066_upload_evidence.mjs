// AUTHORED_NOT_RUN. Invoke only after the all-coding checkpoint.
import {readFile,writeFile,mkdtemp,rm} from 'node:fs/promises';
import {execFile} from 'node:child_process';import {promisify} from 'node:util';
import {tmpdir} from 'node:os';import {join,resolve,dirname,basename,sep} from 'node:path';import {fileURLToPath} from 'node:url';
const run=promisify(execFile),here=dirname(fileURLToPath(import.meta.url)),root=resolve(here,'../..');
const source=await readFile(join(root,'Scripts/Survivor/Repair/RebirthRepairOriginalCorrection.cs'),'utf8');
const first=source.indexOf('    private sealed class Upload'),last=source.indexOf('    private static readonly Dictionary<Guid,Client> Clients',first);
if(first<0||last<=first)throw Error('Native upload type extraction boundary changed');
const actual=await readFile(join(root,'Scripts/Survivor/Repair/RebirthRepairOriginalCorrection.UploadEvidence.cs'),'utf8');
let fixture=await readFile(join(here,'mp066_upload_evidence_fixture.cs'),'utf8');
for(const [marker,text] of [['// ACTUAL_UPLOAD_TYPE',source.slice(first,last)],['// ACTUAL_UPLOAD_EVIDENCE',actual.replace(/^using[^\n]*\n/gm,'')]]){
 if(fixture.split(marker).length!==2)throw Error('Expected one actual source slot '+marker);fixture=fixture.replace(marker,text);
}
const temp=await mkdtemp(join(tmpdir(),'rebirth-repair-upload-evidence-'));
try{
 const cs=join(temp,'fixture.cs'),exe=join(temp,'fixture.exe');await writeFile(cs,fixture);
 await run('C:/Program Files/dotnet/dotnet.exe',['C:/Program Files/dotnet/sdk/9.0.301/Roslyn/bincore/csc.dll','/nologo','/langversion:latest','/target:exe','/out:'+exe,...['mscorlib','System','System.Core'].map(n=>'/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/'+n+'.dll'),cs],{windowsHide:true,timeout:20000});
 process.stdout.write((await run(exe,[],{windowsHide:true,timeout:20000})).stdout);
}finally{
 if(process.env.REBIRTH_KEEP_FIXTURE==='1')process.stdout.write(temp+'\n');
 else{if(!resolve(temp).toLowerCase().startsWith((resolve(tmpdir())+sep).toLowerCase())||!basename(temp).startsWith('rebirth-repair-upload-evidence-'))throw Error('Refuse cleanup outside generated fixture');await rm(temp,{recursive:true});}
}
