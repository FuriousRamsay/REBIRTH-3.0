import {readFile,writeFile,mkdtemp,unlink,rmdir} from 'node:fs/promises';
import {execFile} from 'node:child_process';
import {promisify} from 'node:util';
import {tmpdir} from 'node:os';
import {join,resolve,dirname} from 'node:path';
import {fileURLToPath} from 'node:url';
const run=promisify(execFile),here=dirname(fileURLToPath(import.meta.url)),root=resolve(here,'../..');
function method(source,signature){const a=source.indexOf(signature);if(a<0)throw new Error(signature);let b=source.indexOf('{',a),depth=1,i=b+1;for(;depth&&i<source.length;i++){if(source[i]==='{')depth++;if(source[i]==='}')depth--;}return source.slice(a,i);}
const source=await readFile(join(root,'Scripts/Survivor/Progression/RebirthStudySessionSaveBinding.cs'),'utf8');
const scope=await readFile(join(root,'Scripts/Survivor/Network/RebirthSurvivorRequestScope.cs'),'utf8');
const reading=await readFile(join(root,'Scripts/Survivor/Progression/RebirthLiteratureStudySessionService.cs'),'utf8');
const audio=await readFile(join(root,'Scripts/Survivor/Progression/RebirthAudiobookListeningSessionService.cs'),'utf8');
const lifecycle=await readFile(join(root,'Scripts/Survivor/Persistence/RebirthSurvivorPersistenceLifecycle.cs'),'utf8');
const fixture=(await readFile(join(here,'test_study_disconnect_binding_fixture.cs'),'utf8')).replace('// READING_CHECKPOINT',method(reading,'    internal static int CheckpointForShutdown(')).replace('// AUDIO_CHECKPOINT',method(audio,'    internal static int CheckpointForShutdown(')).replace('// LIFECYCLE',method(lifecycle,'    private static void FlushAndReset(')).replace('// SCOPE',scope.slice(scope.indexOf('public static class'))).replace('// SOURCE',source.slice(source.indexOf('internal sealed class RebirthStudySessionSaveBinding')));
const temp=await mkdtemp(join(tmpdir(),'rebirth-snapshot-test-'));
try {const cs=join(temp,'check.cs'),exe=join(temp,'check.exe');await writeFile(cs,fixture);
 await run('C:/Program Files/dotnet/dotnet.exe',['C:/Program Files/dotnet/sdk/9.0.301/Roslyn/bincore/csc.dll','/nologo','/target:exe','/out:'+exe,...['mscorlib','System','System.Core'].map(x=>'/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/'+x+'.dll'),cs],{windowsHide:true,timeout:10000});console.log((await run(exe,[],{windowsHide:true,timeout:10000})).stdout.trim());
} finally {for(const n of ['check.cs','check.exe'])await unlink(join(temp,n)).catch(e=>{if(e.code!=='ENOENT')throw e;});await rmdir(temp);}