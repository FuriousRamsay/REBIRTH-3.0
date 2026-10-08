import {readFile,writeFile,mkdir} from 'node:fs/promises';
import {execFile} from 'node:child_process';
import {promisify} from 'node:util';
import {join,resolve,dirname} from 'node:path';
import {fileURLToPath} from 'node:url';
import {createHash} from 'node:crypto';
const run=promisify(execFile),here=dirname(fileURLToPath(import.meta.url)),root=resolve(here,'../../../..');
function member(s,sig){let a=s.indexOf(sig);if(a<0)throw Error(sig);let b=s.indexOf('{',a),d=1,i=b+1;for(;d&&i<s.length;i++){if(s[i]==='{')d++;if(s[i]==='}')d--;}if(d)throw Error('unbalanced');return s.slice(a,i);}
const inputs={};
async function load(path){const text=await readFile(join(root,path),'utf8');inputs[path]=createHash('sha256').update(text).digest('hex').toUpperCase();return text;}
let base=await load('Scripts/Survivor/Network/RebirthAudiobookLibraryClient.cs');
if(inputs['Scripts/Survivor/Network/RebirthAudiobookLibraryClient.cs']!=='B121CDA2769BCCFFABAF9093554A5BB0A52C3EEA45BA6F95D6EE979EAA765853')throw Error('baseline drift; re-review before run');
let candidate=await load('Tools/DemoInventoryAcceptanceTeam/Walkman/RebirthAudiobookLibraryClient.CANDIDATE.cs.txt');
let fixture=await load('Tools/DemoInventoryAcceptanceTeam/Walkman/ReservationCounterexample/fixture.cs');
for(const[m,p]of[['SCOPE','Network/RebirthSurvivorRequestScope.cs'],['PROOF','Support/RebirthMusicSourceProof.cs']]){let s=await load('Scripts/Survivor/'+p);fixture=fixture.replace('// '+m,s.slice(s.indexOf('public static class')));}
let action=await load('Scripts/Survivor/Progression/ItemActionListenAudiobookRebirth.cs'),music=await load('Scripts/Survivor/Network/RebirthMusicLibraryNetPackage.cs');
fixture=fixture.replace('// DISPATCH',member(action,'    internal static void Dispatch(')).replace('// SEND',member(music,'    internal static bool CanSend('));
const results={};
for(const[name,source]of[['Baseline',base],['Candidate',candidate]]){
 const out=join(here,name);await mkdir(out,{recursive:true});const cs=join(out,'actual_source_fixture.cs'),exe=join(out,'check.exe');await writeFile(cs,fixture.replace('// CLIENT',source.slice(source.indexOf('public static class'))));
 await run('C:/Program Files/dotnet/dotnet.exe',['C:/Program Files/dotnet/sdk/9.0.301/Roslyn/bincore/csc.dll','/nologo','/target:exe','/out:'+exe,...['mscorlib','System','System.Core'].map(n=>'/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/'+n+'.dll'),cs],{windowsHide:true,timeout:10000});
 try{const r=await run(exe,[],{windowsHide:true,timeout:10000});results[name]={exitCode:0,stdout:r.stdout};}catch(e){if(typeof e.code!=='number')throw e;results[name]={exitCode:e.code,stdout:e.stdout,stderr:e.stderr};}
 await writeFile(join(out,'RESULT.txt'),results[name].stdout);console.log(name+' exit='+results[name].exitCode+'\n'+results[name].stdout.trim());
}
await writeFile(join(here,'SOURCE_HASHES.json'),JSON.stringify(inputs,null,2));await writeFile(join(here,'RESULTS.json'),JSON.stringify(results,null,2));
if(results.Baseline.exitCode!==1||results.Candidate.exitCode!==0||!(results.Baseline.stdout.match(/^FAIL /gm)?.length===5)||!(results.Candidate.stdout.match(/^PASS /gm)?.length===5))throw Error('unexpected counterexample outcomes');
console.log('EXPECTED BASELINE FAIL5 / CANDIDATE PASS5. Synthetic adapter evidence only; no game, native custody, callbacks or MP acceptance.');