// AUTHORED_NOT_RUN. Invoke only after the user's all-coding test checkpoint.
import {readFile,writeFile,mkdtemp,rm} from 'node:fs/promises';
import {execFile} from 'node:child_process';
import {promisify} from 'node:util';
import {tmpdir} from 'node:os';
import {join,resolve,dirname,basename,sep} from 'node:path';
import {fileURLToPath} from 'node:url';
const run=promisify(execFile),here=dirname(fileURLToPath(import.meta.url)),root=resolve(here,'../..');
let fixture=await readFile(join(here,'mp054_remote_closed_scan_fixture.cs'),'utf8');
const actual=await readFile(join(root,'Scripts/Crafting/Recycling/RebirthProductiveEarnedRemoteClosedRecord.cs'),'utf8');
function member(source,key){const start=source.indexOf(key);if(start<0||source.indexOf(key,start+1)>=0)throw Error('Unique source required '+key);let end=source.indexOf('{',start)+1,depth=1;for(;depth&&end<source.length;end++){if(source[end]==='{')depth++;else if(source[end]==='}')depth--;}return source.slice(start,end);}
const early=await readFile(join(root,'Scripts/Crafting/Recycling/RebirthProductiveEarnedEarlyHeader.cs'),'utf8');
const closed=await readFile(join(root,'Scripts/Crafting/Recycling/RebirthProductiveEarnedRemoteHost.NativeClosed.cs'),'utf8');
const fields=early.slice(early.indexOf('    private sealed class Early'),early.indexOf('    internal static string EarlyManifest('));
const members=['    private static bool Manifest(','    private static byte[] EarlyBytes(','    private static Early ReadEarly(','    private static void ObserveEarly('].map(key=>member(early,key)).join('\n');
fixture=fixture.replace('// ACTUAL_CLOSED_CODEC',actual.slice(actual.indexOf('internal static class RebirthProductiveEarnedRemoteClosedRecord'))).replace('// ACTUAL_EARLY_MEMBERS',fields+members).replace('// ACTUAL_CLOSED_EARLY',member(closed,'    private static bool TryClosedEarly('));
const temp=await mkdtemp(join(tmpdir(),'rebirth-remote-closed-scan-'));
try{
 const cs=join(temp,'fixture.cs'),exe=join(temp,'fixture.exe');await writeFile(cs,fixture);
 await run('C:/Program Files/dotnet/dotnet.exe',['C:/Program Files/dotnet/sdk/9.0.301/Roslyn/bincore/csc.dll','/nologo','/langversion:latest','/target:exe','/out:'+exe,...['mscorlib','System','System.Core','System.Xml','System.Xml.Linq'].map(n=>'/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/'+n+'.dll'),cs],{windowsHide:true,timeout:20000});
 process.stdout.write((await run(exe,[],{windowsHide:true,timeout:20000})).stdout);
}finally{
 if(process.env.REBIRTH_KEEP_FIXTURE==='1')process.stdout.write(temp+'\n');
 else{if(!resolve(temp).toLowerCase().startsWith((resolve(tmpdir())+sep).toLowerCase())||!basename(temp).startsWith('rebirth-remote-closed-scan-'))throw Error('Refuse cleanup outside generated fixture');await rm(temp,{recursive:true});}
}


