import {readFile,writeFile,mkdtemp,unlink,rmdir} from 'node:fs/promises';
import {execFile} from 'node:child_process';
import {promisify} from 'node:util';
import {tmpdir} from 'node:os';
import {join,resolve,dirname} from 'node:path';
import {fileURLToPath} from 'node:url';
const run=promisify(execFile),here=dirname(fileURLToPath(import.meta.url)),root=resolve(here,'../..');
function method(source,signature){const a=source.indexOf(signature);if(a<0)throw new Error(signature);let b=source.indexOf('{',a),depth=1,i=b+1;for(;depth&&i<source.length;i++){if(source[i]==='{')depth++;if(source[i]==='}')depth--;}return source.slice(a,i);}
const source=await readFile(join(root,'Scripts/Survivor/Progression/RebirthAudiobookListeningSessionService.cs'),'utf8');
const reading=await readFile(join(root,'Scripts/Survivor/Progression/RebirthLiteratureStudySessionService.cs'),'utf8');
const scope=await readFile(join(root,'Scripts/Survivor/Network/RebirthSurvivorRequestScope.cs'),'utf8');
const outgoingStart=source.indexOf('        Session outgoing;'),outgoingEnd=source.indexOf('        if(!RebirthLiteratureStudySessionService.StopForStudyModeSwitch',outgoingStart);
if(outgoingStart<0||outgoingEnd<outgoingStart)throw new Error('Outgoing session checkpoint missing');
const fixture=(await readFile(join(here,'test_audiobook_session_scope_fixture.cs'),'utf8')).replace('// SCOPE',scope.slice(scope.indexOf('public static class'))).replace('// COMPLETION',method(reading,'    private static void BeginCompletionSave(')+'\n'+method(reading,'    private static bool RetryCompletedSave(')+'\n'+method(reading,'    private static void FinishCompleted(')).replace('// SWITCHES',method(source,'    public static bool StopForStudyModeSwitch(').replace('StopForStudyModeSwitch','StopAudio')+'\n'+method(reading,'    public static bool StopForStudyModeSwitch(').replace('StopForStudyModeSwitch','StopReading')).replace('// OWNERSHIP',method(source,'    private static bool HasSessionCassette(')).replace('// OUTGOING',source.slice(outgoingStart,outgoingEnd));
const temp=await mkdtemp(join(tmpdir(),'rebirth-snapshot-test-'));
try {const cs=join(temp,'check.cs'),exe=join(temp,'check.exe');await writeFile(cs,fixture);
 await run('C:/Program Files/dotnet/dotnet.exe',['C:/Program Files/dotnet/sdk/9.0.301/Roslyn/bincore/csc.dll','/nologo','/target:exe','/out:'+exe,...['mscorlib','System','System.Core'].map(x=>'/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/'+x+'.dll'),cs],{windowsHide:true,timeout:10000});console.log((await run(exe,[],{windowsHide:true,timeout:10000})).stdout.trim());
} finally {for(const n of ['check.cs','check.exe'])await unlink(join(temp,n)).catch(e=>{if(e.code!=='ENOENT')throw e;});await rmdir(temp);}