// AUTHORED_NOT_RUN. Actual native role epoch entry points.
import {readFile,writeFile,mkdtemp,rm} from 'node:fs/promises';
import {execFile} from 'node:child_process';import {promisify} from 'node:util';
import {tmpdir} from 'node:os';import {join,resolve,dirname,basename,sep} from 'node:path';import {fileURLToPath} from 'node:url';
const here=dirname(fileURLToPath(import.meta.url)),root=resolve(here,'../..'),run=promisify(execFile);
const source=await readFile(join(root,'Scripts/NPC/Network/RebirthNpcNetworkFraming.cs'),'utf8');
function between(from,to){const a=source.indexOf(from),b=source.indexOf(to,a);if(a<0||b<=a)throw Error('actual epoch member missing');return source.slice(a,b);}
const members=between('    public static uint GetServerEpoch()', '    public static uint IssueConnectionEpoch(')+between('    public static void BeginClientEpoch(', '    public static bool AcceptClientEpoch(');
const fixture=(await readFile(join(here,'mp049_role_epoch_fixture.cs'),'utf8')).replace('// ACTUAL_ROLE_EPOCHS',members);
const temp=await mkdtemp(join(tmpdir(),'rebirth-role-epoch-source-'));
try{
 const cs=join(temp,'fixture.cs'),exe=join(temp,'fixture.exe');await writeFile(cs,fixture);
 await run('C:/Program Files/dotnet/dotnet.exe',['C:/Program Files/dotnet/sdk/9.0.301/Roslyn/bincore/csc.dll','/nologo','/langversion:latest','/target:exe','/out:'+exe,...['mscorlib','System','System.Core'].map(n=>'/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/'+n+'.dll'),cs],{windowsHide:true,timeout:20000});
 process.stdout.write((await run(exe,[],{windowsHide:true,timeout:20000})).stdout);
}finally{
 if(!resolve(temp).toLowerCase().startsWith((resolve(tmpdir())+sep).toLowerCase())||!basename(temp).startsWith('rebirth-role-epoch-source-'))throw Error('unsafe fixture cleanup');
 await rm(temp,{recursive:true});
}
