// Prepared offline actual-method fixture; NOT RUN during implementation.
import {readFile,writeFile,mkdtemp,rm,readdir} from 'node:fs/promises';
import {execFile} from 'node:child_process';import {promisify} from 'node:util';
import {tmpdir} from 'node:os';import {join,resolve,dirname} from 'node:path';import {fileURLToPath} from 'node:url';
const run=promisify(execFile),here=dirname(fileURLToPath(import.meta.url)),root=resolve(here,'../..');
function body(s,signature){let a=s.indexOf(signature);if(a<0)throw Error(signature);let b=s.indexOf('{',a),d=1,i=b+1;for(;d&&i<s.length;i++){if(s[i]==='{')d++;else if(s[i]==='}')d--;}if(d)throw Error(signature);return s.slice(a,i);}
const names=await readFile(join(root,'Scripts/BlockPickup/RebirthContainerNameRegistry.cs'),'utf8');
const grace=await readFile(join(root,'Scripts/TraderJobs/RebirthTraderQuestGrace.cs'),'utf8');
const placed=await readFile(join(root,'Scripts/Survivor/Provenance/RebirthPlacedWorkmanshipService.cs'),'utf8');
let fixture=await readFile(join(here,'mpB_store_recovery_fixture.cs'),'utf8');
fixture=fixture.replace('// NAME_MEMBERS',[
 body(names,'public sealed class Entry'),...['public static void Reset(','public static bool SetServer(','private static void EnsureLoaded(','private static bool TryLoadFile(','private static bool Save()','private static string PathName'].map(x=>body(names,x))
].join('\n'));
fixture=fixture.replace('// GRACE_RECORD',body(grace,'public sealed class RebirthTraderOfflineGraceRecord'));
fixture=fixture.replace('// GRACE_MEMBERS',['private static bool EnsureOfflineLoaded(','private static List<RebirthTraderOfflineGraceRecord> ReadOfflineFile(','private static void SaveOfflineLocked(','private static string GetOfflinePath()','public static void ResetOffline(','public static bool TryFlushOfflineForShutdown()'].map(x=>body(grace,x)).join('\n'));
fixture=fixture.replace('const ushort Version=1;const int MaxRecords=250000;', 'const ushort Version=2;const int MaxRecords=250000;');
fixture=fixture.replace('// PLACED_RECORDS',body(placed,'public enum RebirthPlacedProvenanceKind')+'\n'+body(placed,'public sealed class RebirthPlacedWorkmanshipRecord'));
fixture=fixture.replace('// PLACED_MEMBERS',['private static string Key(','private static string CurrentPersistencePath','private static string PersistencePath','private static bool HasCurrentStore','private static void OnGameStarting(','private static void OnGameStartDone(','private static void Reset(','public static bool CanReset','public static bool TryFlushForShutdown()','public static bool TryResetAfterCoordinatedSave()','private static void WriteString(','private static string ReadString(','private static void Load()','private static bool TryLoadFile(','public static void Save()','private static bool SafePath(','private static string FileDigest(','private static bool PreserveRecoveryFile(','private static bool SameRecords(','private static void MarkDirty()'].map(x=>body(placed,x)).join('\n'));
fixture=fixture.replace('// PROVENANCE_NATIVE_DECODER',body(await readFile(join(root,'Scripts/Survivor/Provenance/RebirthPlacedWorkmanshipIdentity.cs'),'utf8'),'private static RebirthPlacedWorkmanshipRecord Decode('));
fixture=fixture.replace('// DURABLE_HELPER',body(await readFile(join(root,'Scripts/Persistence/RebirthDurableFileCommit.cs'),'utf8'),'public static class RebirthDurableFileCommit'));
const temp=await mkdtemp(join(tmpdir(),'rebirth-mpB-stores-'));
try {
 const sdkRoot=process.env.MPB_DOTNET_SDK_ROOT||'C:/Program Files/dotnet/sdk';
 const sdks=(await readdir(sdkRoot)).filter(x=>/^\d+\.\d+\.\d+$/.test(x)).sort((a,b)=>a.localeCompare(b,undefined,{numeric:true}));if(!sdks.length)throw Error('Roslyn SDK missing');
 const cs=join(temp,'check.cs'),exe=join(temp,'check.exe');await writeFile(cs,fixture);
 await run('dotnet',[join(sdkRoot,sdks.at(-1),'Roslyn/bincore/csc.dll'),'/nologo','/target:exe','/out:'+exe,...['mscorlib','System','System.Core','System.Xml'].map(x=>'/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/'+x+'.dll'),cs],{windowsHide:true,timeout:30000});
 console.log((await run(exe,[temp],{windowsHide:true,timeout:30000})).stdout.trim());
}finally{await rm(temp,{recursive:true,force:true});}
