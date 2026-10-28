// Deferred actual-method fixture; do not execute until the parent validation batch.
import {readFile,writeFile,mkdtemp,rm,readdir} from 'node:fs/promises';
import {execFile} from 'node:child_process';import {promisify} from 'node:util';
import {tmpdir} from 'node:os';import {join,resolve,dirname} from 'node:path';import {fileURLToPath} from 'node:url';
const run=promisify(execFile),root=resolve(dirname(fileURLToPath(import.meta.url)),'../..');
function body(s,signature){let a=s.indexOf(signature);if(a<0)throw Error(signature);let b=s.indexOf('{',a),d=1,i=b+1;for(;d&&i<s.length;i++){if(s[i]==='{')d++;else if(s[i]==='}')d--;}if(d)throw Error(signature);return s.slice(a,i);}
const boss=await readFile(join(root,'Scripts/Modules/BossEvents/RebirthBossEvents.cs'),'utf8'),fire=await readFile(join(root,'Scripts/Fire/RebirthFireVisualManager.cs'),'utf8');
let source=await readFile(join(root,'Tools/GameBridge/mpB_fire_boss_fixture.cs'),'utf8');
const reward=boss.slice(boss.indexOf('public static class RebirthBossEventRewardService'));
source=source.replace('// PRODUCTION_REWARD_METHODS',['public static void Update(','internal static RebirthBossEventInstance FindReward(','internal static void BindOwner(','internal static bool AllowRewardLock('].map(x=>body(reward,x)).join('\n'));
source=source.replace('// PRODUCTION_PENDING_DELTA',body(fire,'private struct PendingDelta'));
const comparator=body(fire,'delegate(PendingDelta a, PendingDelta b)').slice('delegate(PendingDelta a, PendingDelta b)'.length).trim();
source=source.replace('/* PRODUCTION_COMPARATOR */',comparator.slice(1,-1));
const publication=await readFile(join(root,'Scripts/Modules/BossEvents/RebirthBossNativePublication.cs'),'utf8');
let publicationFixture=await readFile(join(root,'Tools/GameBridge/mpB_boss_native_publication_fixture.cs'),'utf8');
publicationFixture=publicationFixture.replace('// ACTUAL_CHECKSUM_METHODS',body(boss,'private static void AppendChecksum(')+'\n'+body(boss,'private static long VerifyChecksum(')).replace('// EVENT_ENUM',body(boss,'public enum RebirthBossEventState')).replace('// ORIGINAL_MODEL',body(boss,'public sealed class RebirthBossSpawnOriginal')).replace('// ACTUAL_METHODS',['internal static bool ValidDeadRemoval(','internal static string RemovedDeathProof(','internal static bool TryPublishedEntity(','internal static bool NativeBirth(','private static bool Flag(','private static void Channel(','private static void Skip(','private static int StringLength(','internal static bool CanResumePreparedOriginal('].map(key=>body(publication,key)).join('\n')+'\n'+body(boss,'internal static bool CanCompleteOriginal('));
const temp=await mkdtemp(join(tmpdir(),'rebirth-mpB-fire-boss-'));
try{const sdk=process.env.MPB_DOTNET_SDK_ROOT||'C:/Program Files/dotnet/sdk';const versions=(await readdir(sdk)).filter(x=>/^\d+\.\d+\.\d+$/.test(x)).sort((a,b)=>a.localeCompare(b,undefined,{numeric:true}));if(!versions.length)throw Error('Roslyn SDK missing');for(const [name,text]of [['legacyReward',source],['nativePublication',publicationFixture]]){const cs=join(temp,name+'.cs'),exe=join(temp,name+'.exe');await writeFile(cs,text);await run('dotnet',[join(sdk,versions.at(-1),'Roslyn/bincore/csc.dll'),'/nologo','/target:exe','/out:'+exe,...['mscorlib','System','System.Core'].map(x=>'/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/'+x+'.dll'),cs],{windowsHide:true,timeout:30000});console.log((await run(exe,[],{windowsHide:true,timeout:30000})).stdout.trim());}}finally{await rm(temp,{recursive:true,force:true});}
