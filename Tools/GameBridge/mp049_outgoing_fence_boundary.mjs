// AUTHORED_NOT_RUN. Composes actual production methods; adapters are disclosed.
import {readFile,writeFile,mkdtemp,rm} from 'node:fs/promises';
import {execFile} from 'node:child_process';import {promisify} from 'node:util';
import {tmpdir} from 'node:os';import {join,resolve,dirname,basename,sep} from 'node:path';import {fileURLToPath} from 'node:url';
const here=dirname(fileURLToPath(import.meta.url)),root=resolve(here,'../..'),run=promisify(execFile);
const fence=await readFile(join(root,'Scripts/NPC/Persistence/RebirthNpcSaveQuarantineWriterFence.cs'),'utf8');
const lifecycle=await readFile(join(root,'Scripts/NPC/Foundation/RebirthNpcLifecycle.cs'),'utf8');
const fenceStart=fence.indexOf('    private static QuarantineWriterFence quarantineWriterFence;'),fenceEnd=fence.lastIndexOf('}');
const hookStart=lifecycle.indexOf('    [ThreadStatic]private static bool nativeEpochBoundary;'),hookEnd=lifecycle.indexOf('    private static void QualifyWholeBundleAdmission()',hookStart);
let fixture=await readFile(join(here,'mp049_outgoing_fence_boundary_fixture.cs'),'utf8');
if(fenceStart<0||fenceEnd<=fenceStart||hookStart<0||hookEnd<=hookStart)throw Error('Actual source boundaries changed');
for(const marker of ['// ACTUAL_FENCE_MEMBERS','// ACTUAL_NATIVE_EPOCH_BOUNDARY'])if(fixture.split(marker).length!==2)throw Error('Fixture slot changed');
fixture=fixture.replace('// ACTUAL_FENCE_MEMBERS',fence.slice(fenceStart,fenceEnd)).replace('// ACTUAL_NATIVE_EPOCH_BOUNDARY',lifecycle.slice(hookStart,hookEnd));
const temp=await mkdtemp(join(tmpdir(),'rebirth-outgoing-fence-boundary-'));
try{
 const cs=join(temp,'fixture.cs'),exe=join(temp,'fixture.exe');await writeFile(cs,fixture);
 await run('C:/Program Files/dotnet/dotnet.exe',['C:/Program Files/dotnet/sdk/9.0.301/Roslyn/bincore/csc.dll','/nologo','/langversion:latest','/target:exe','/out:'+exe,...['mscorlib','System','System.Core'].map(n=>'/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/'+n+'.dll'),cs],{windowsHide:true,timeout:20000});
 process.stdout.write((await run(exe,[],{windowsHide:true,timeout:20000})).stdout);
}finally{
 if(!resolve(temp).toLowerCase().startsWith((resolve(tmpdir())+sep).toLowerCase())||!basename(temp).startsWith('rebirth-outgoing-fence-boundary-'))throw Error('Unsafe fixture cleanup');
 await rm(temp,{recursive:true});
}
