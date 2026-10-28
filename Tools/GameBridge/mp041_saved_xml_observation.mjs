// AUTHORED_NOT_RUN. Run only after the user's all-coding checkpoint.
import {readFile,writeFile,mkdtemp,rm} from 'node:fs/promises';
import {execFile} from 'node:child_process';
import {promisify} from 'node:util';
import {tmpdir} from 'node:os';
import {join,resolve,dirname,basename,sep} from 'node:path';
import {fileURLToPath} from 'node:url';
const run=promisify(execFile),here=dirname(fileURLToPath(import.meta.url)),root=resolve(here,'../..');
let fixture=await readFile(join(here,'mp041_saved_xml_observation_fixture.cs'),'utf8');
const actual=await readFile(join(root,'Scripts/NPC/Persistence/RebirthNpcSavedXmlObservation.cs'),'utf8');
const marker='// ACTUAL_SAVED_XML';if(fixture.split(marker).length!==2)throw Error('Expected one actual source slot');
fixture=fixture.replace(marker,actual.replace(/^using[^\n]*\n/gm,''));
const temp=await mkdtemp(join(tmpdir(),'rebirth-saved-xml-fixture-'));
try{
 const cs=join(temp,'fixture.cs'),exe=join(temp,'fixture.exe');await writeFile(cs,fixture);
 await run('C:/Program Files/dotnet/dotnet.exe',['C:/Program Files/dotnet/sdk/9.0.301/Roslyn/bincore/csc.dll','/nologo','/langversion:latest','/target:exe','/out:'+exe,...['mscorlib','System','System.Core','System.Xml'].map(n=>'/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/'+n+'.dll'),cs],{windowsHide:true,timeout:20000});
 process.stdout.write((await run(exe,[],{windowsHide:true,timeout:20000})).stdout);
}finally{
 if(process.env.REBIRTH_KEEP_FIXTURE==='1')process.stdout.write(temp+'\n');
 else{if(!resolve(temp).toLowerCase().startsWith((resolve(tmpdir())+sep).toLowerCase())||!basename(temp).startsWith('rebirth-saved-xml-fixture-'))throw Error('Refuse cleanup outside generated fixture');await rm(temp,{recursive:true});}
}
