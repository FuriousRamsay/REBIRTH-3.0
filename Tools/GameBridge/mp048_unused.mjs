// AUTHORED_NOT_RUN. Actual host refusal class; disclosed packet/PDS adapters.
import {readFile,writeFile,mkdtemp,rm} from 'node:fs/promises';
import {execFile} from 'node:child_process';import {promisify} from 'node:util';
import {tmpdir} from 'node:os';import {join,resolve,dirname,basename,sep} from 'node:path';import {fileURLToPath} from 'node:url';
const here=dirname(fileURLToPath(import.meta.url)),root=resolve(here,'../..'),run=promisify(execFile);
const source=await readFile(join(root,'Scripts/NPC/Interaction/RebirthNpcTradeHostUnused.cs'),'utf8');
const start=source.indexOf('    internal sealed class HostUnused'),end=source.lastIndexOf('\n}');
if(start<0||end<=start)throw Error('actual unused refusal class missing');
let fixture=(await readFile(join(here,'mp048_unused_fixture.cs'),'utf8')).replace('// ACTUAL_HOST_UNUSED',source.slice(start,end));
const exchange=await readFile(join(root,'Scripts/NPC/Interaction/RebirthNpcTradeOwnerExchange.cs'),'utf8');
const releaseStart=exchange.indexOf('    internal static bool TryReleaseUnused('),releaseEnd=exchange.indexOf('    internal static bool IsHeld(',releaseStart);
if(releaseStart<0||releaseEnd<=releaseStart)throw Error('actual unused owner release missing');
fixture+='\n'+(await readFile(join(here,'mp048_unused_release_fixture.cs'),'utf8')).replace('// ACTUAL_UNUSED_RELEASE',exchange.slice(releaseStart,releaseEnd));
const temp=await mkdtemp(join(tmpdir(),'rebirth-unused-source-'));
try{
 const cs=join(temp,'fixture.cs'),exe=join(temp,'fixture.exe');await writeFile(cs,fixture);
 await run('C:/Program Files/dotnet/dotnet.exe',['C:/Program Files/dotnet/sdk/9.0.301/Roslyn/bincore/csc.dll','/nologo','/langversion:latest','/target:exe','/out:'+exe,...['mscorlib','System','System.Core','System.Xml','System.Xml.Linq'].map(n=>'/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/'+n+'.dll'),cs],{windowsHide:true,timeout:20000});
 process.stdout.write((await run(exe,[],{windowsHide:true,timeout:20000})).stdout);
}finally{
 if(!resolve(temp).toLowerCase().startsWith((resolve(tmpdir())+sep).toLowerCase())||!basename(temp).startsWith('rebirth-unused-source-'))throw Error('unsafe fixture cleanup');
 await rm(temp,{recursive:true});
}
