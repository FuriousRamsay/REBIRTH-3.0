// AUTHORED_NOT_RUN. Invoke only after the user's all-coding test checkpoint.
import {readFile,writeFile,mkdtemp,rm} from 'node:fs/promises';
import {execFile} from 'node:child_process';
import {promisify} from 'node:util';
import {tmpdir} from 'node:os';
import {join,resolve,dirname,basename,sep} from 'node:path';
import {fileURLToPath} from 'node:url';
const run=promisify(execFile),here=dirname(fileURLToPath(import.meta.url)),root=resolve(here,'../..');
let fixture=await readFile(join(here,'mp054_viewer_tools_closed_fixture.cs'),'utf8');
const actual=await readFile(join(root,'Scripts/Crafting/Recycling/RebirthProductiveEarnedRemoteClosedRecord.cs'),'utf8');
const marker='// ACTUAL_CLOSED_CODEC';
if(fixture.split(marker).length!==2)throw Error('Expected one actual source extraction slot');
fixture=fixture.replace(marker,actual.slice(actual.indexOf('internal static class RebirthProductiveEarnedRemoteClosedRecord')));
const stages=await readFile(join(root,'Scripts/Crafting/Recycling/RebirthProductiveEarnedRemoteClient.NativeClosed.cs'),'utf8');
const viewer=await readFile(join(root,'Scripts/Crafting/Recycling/RebirthProductiveEarnedRemoteClient.ViewerToolsClosed.cs'),'utf8');
fixture=fixture.replace('// ACTUAL_STAGE_HELPERS',stages.slice(stages.indexOf('    private static byte[] NativeCloseStage('),stages.indexOf('    // Query authority only:'))).replace('// ACTUAL_VIEWER_METADATA',viewer.slice(viewer.indexOf('    private static bool ViewerToolMetadataCurrent('),viewer.indexOf('    private static bool QueueViewerToolsClosure(')));
const temp=await mkdtemp(join(tmpdir(),'rebirth-remote-closed-'));
try{
 const cs=join(temp,'fixture.cs'),exe=join(temp,'fixture.exe');await writeFile(cs,fixture);
 await run('C:/Program Files/dotnet/dotnet.exe',['C:/Program Files/dotnet/sdk/9.0.301/Roslyn/bincore/csc.dll','/nologo','/langversion:latest','/target:exe','/out:'+exe,...['mscorlib','System','System.Core','System.Xml','System.Xml.Linq'].map(n=>'/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/'+n+'.dll'),cs],{windowsHide:true,timeout:20000});
 process.stdout.write((await run(exe,[],{windowsHide:true,timeout:20000})).stdout);
}finally{
 if(process.env.REBIRTH_KEEP_FIXTURE==='1')process.stdout.write(temp+'\n');
 else{if(!resolve(temp).toLowerCase().startsWith((resolve(tmpdir())+sep).toLowerCase())||!basename(temp).startsWith('rebirth-remote-closed-'))throw Error('Refuse cleanup outside generated fixture');await rm(temp,{recursive:true});}
}


