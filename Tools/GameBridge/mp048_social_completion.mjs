// AUTHORED_NOT_RUN. Whole actual trade-social store partial; explicit save adapters.
import {readFile,writeFile,mkdtemp,rm} from 'node:fs/promises';
import {execFile} from 'node:child_process';import {promisify} from 'node:util';
import {tmpdir} from 'node:os';import {join,resolve,dirname,basename,sep} from 'node:path';import {fileURLToPath} from 'node:url';
const here=dirname(fileURLToPath(import.meta.url)),root=resolve(here,'../..'),run=promisify(execFile);
const source=await readFile(join(root,'Scripts/NPC/Persistence/RebirthNpcSocialPersistence.Trade.cs'),'utf8');
const fixture=await readFile(join(here,'mp048_social_completion_fixture.cs'),'utf8');
const temp=await mkdtemp(join(tmpdir(),'rebirth-trade-social-source-'));
try{
 const cs=join(temp,'fixture.cs'),production=join(temp,'production.cs'),exe=join(temp,'fixture.exe');await writeFile(cs,fixture);await writeFile(production,source);
 await run('C:/Program Files/dotnet/dotnet.exe',['C:/Program Files/dotnet/sdk/9.0.301/Roslyn/bincore/csc.dll','/nologo','/langversion:latest','/target:exe','/out:'+exe,...['mscorlib','System','System.Core','System.Xml'].map(n=>'/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/'+n+'.dll'),cs,production],{windowsHide:true,timeout:20000});
 process.stdout.write((await run(exe,[],{windowsHide:true,timeout:20000})).stdout);
}finally{
 if(!resolve(temp).toLowerCase().startsWith((resolve(tmpdir())+sep).toLowerCase())||!basename(temp).startsWith('rebirth-trade-social-source-'))throw Error('unsafe fixture cleanup');
 await rm(temp,{recursive:true});
}
