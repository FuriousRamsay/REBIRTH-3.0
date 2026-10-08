import {readFile,writeFile,mkdtemp,rm} from 'node:fs/promises';
import {execFile} from 'node:child_process';
import {promisify} from 'node:util';
import {tmpdir} from 'node:os';
import {join,resolve,dirname} from 'node:path';
import {fileURLToPath} from 'node:url';
const run=promisify(execFile),here=dirname(fileURLToPath(import.meta.url)),root=resolve(here,'../..');
const temp=await mkdtemp(join(tmpdir(),'rebirth-atomic-'));
try {
 const cs=join(temp,'check.cs'),exe=join(temp,'check.exe');
 await writeFile(cs,await readFile(join(root,'Scripts/Survivor/Persistence/RebirthAtomicXmlFile.cs'),'utf8')+'\n'+await readFile(join(here,'test_atomic_xml_file_fixture.cs'),'utf8'));
 await run('C:/Program Files/dotnet/dotnet.exe',['C:/Program Files/dotnet/sdk/9.0.301/Roslyn/bincore/csc.dll','/nologo','/target:exe','/out:'+exe,...['mscorlib','System','System.Core','System.Xml','System.Xml.Linq'].map(x=>'/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/'+x+'.dll'),cs],{windowsHide:true,timeout:10000});
 console.log((await run(exe,[temp],{windowsHide:true,timeout:10000})).stdout.trim());
} finally {await rm(temp,{recursive:true,force:true});}