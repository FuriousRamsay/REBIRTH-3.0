import {alignHydrationSourcePaths,alignHydrationFixture,writeHydrationFixtureConfig} from './mp018_existing_cohort_boundaries.mjs';
// AUTHORED_NOT_RUN. Execute only after all production/scenario coding completes.
import {readFile,writeFile,mkdtemp,rm} from 'node:fs/promises';import {execFile} from 'node:child_process';import {promisify} from 'node:util';
import {tmpdir} from 'node:os';import {join,resolve,dirname,basename,sep} from 'node:path';import {fileURLToPath} from 'node:url';
const here=dirname(fileURLToPath(import.meta.url)),root=resolve(here,'../..'),run=promisify(execFile);
const paths=['Scripts/Survivor/Domain/RebirthGearInventoryPlan.cs','Scripts/Survivor/Domain/RebirthGearInventoryPlanCodec.cs','Scripts/Survivor/Domain/RebirthGearEncodedSnapshot.cs','Scripts/Metabolism/RebirthHydrationTransferPlan.cs','Scripts/Metabolism/RebirthHydrationTransferOriginal.cs','Scripts/Survivor/Network/RebirthSurvivorRequestScope.cs','Scripts/Vehicles/Camper/RebirthCamperTransferJournal.cs','Scripts/Metabolism/RebirthHydrationTransferJournal.cs','Scripts/Metabolism/RebirthHydrationServerSourceHold.cs','Scripts/Metabolism/RebirthMetabolismStateRepository.HydrationSource.cs','Scripts/Metabolism/RebirthHydrationMetabolismReceiptCodec.cs','Scripts/Metabolism/RebirthHydrationGeneration.cs','Scripts/Metabolism/RebirthMetabolismStateRepository.HydrationGeneration.cs','Scripts/Metabolism/RebirthHydrationTransferScope.cs','Scripts/Metabolism/RebirthHydrationTransferJournal.CompletionHistory.cs'];
alignHydrationSourcePaths(paths);
const sources=await Promise.all(paths.map(path=>readFile(join(root,path),'utf8')));
let shared=await readFile(join(here,'mp018_hydration_journal_fixture.cs'),'utf8');const program=shared.indexOf('internal static class Program {');if(program<0)throw Error('Journal adapter boundary changed');shared=shared.slice(0,program);
const spawned='internal bool IsDead()=>Dead;';if(shared.split(spawned).length!==2)throw Error('Native actor boundary changed');shared=shared.replace(spawned,spawned+'internal bool Spawned=true;internal bool IsSpawned()=>Spawned;');
let body=await readFile(join(here,'mp018_server_source_hold_fixture.cs'),'utf8');
const repository='internal static class RebirthMetabolismStateRepository {internal static RebirthMetabolismState State;internal static bool SourceSaved=true;internal static bool TrySaveRemoteHydrationBefore(RebirthHydrationServerSourceHold.Lease lease)=>SourceSaved;internal static bool TryGet(EntityPlayer player,out RebirthMetabolismState state){state=State;return state!=null;}}';
if(body.split(repository).length!==2)throw Error('Source repository adapter changed');body=body.replace(repository,await readFile(join(here,'mp018_server_source_checkpoint_fixture.cs'),'utf8'));
body=body.replace('// SOURCE_CHECKPOINT_CASES',`
  int saves=RebirthMetabolismStateRepository.Saves;Need(RebirthMetabolismStateRepository.HasSavedRemoteHydrationBefore(lease)&&RebirthMetabolismStateRepository.Saves==saves,"source observation wrote repository");
  Fresh();RebirthMetabolismStateRepository.Suppress=true;Need(!RebirthHydrationServerSourceHold.Lease.TryAcquire(player,original,out _)&&RebirthHydrationServerSourceHold.HoldsPlayer(player),"suppressed save accepted stale source file");
  RebirthMetabolismStateRepository.Suppress=false;Need(RebirthHydrationServerSourceHold.Lease.TryAcquire(player,original,out lease),"suppressed source save did not resume");
  RebirthMetabolismStateRepository.AfterRead=()=>SingletonMonoBehaviour<ConnectionManager>.Instance.Clients.Peer=new ClientInfo();Need(!RebirthMetabolismStateRepository.HasSavedRemoteHydrationBefore(lease)&&RebirthHydrationServerSourceHold.HoldsPlayer(player),"peer loss during saved read qualified source");
  Fresh();RebirthMetabolismStateRepository.ThrowAfter=true;Need(!RebirthHydrationServerSourceHold.Lease.TryAcquire(player,original,out _)&&RebirthHydrationServerSourceHold.HoldsPlayer(player),"unknown source publication cleared custody");
  RebirthMetabolismStateRepository.ThrowAfter=false;saves=RebirthMetabolismStateRepository.Saves;Need(RebirthHydrationServerSourceHold.Lease.TryAcquire(player,original,out lease)&&RebirthMetabolismStateRepository.Saves==saves,"already saved source repeated whole-store write");
`);
const generationCases=await readFile(join(here,'mp018_generation_cases.cs'),'utf8');const generationPrint=body.indexOf('  Console.WriteLine(');if(generationPrint<0)throw Error('Source scenario boundary changed');body=body.slice(0,generationPrint)+generationCases+'\n'+body.slice(generationPrint);
shared=shared.replace('internal sealed class GameManager {','internal sealed partial class GameManager {');shared=shared.replace('internal string StorageKey;','internal string StorageKey;internal string CanonicalId=>StorageKey;');
let fixture=shared+'\n'+body+'\n'+await readFile(join(here,'mp018_generation_adapters.cs'),'utf8');
if(fixture.split('// ACTUAL_PLAN_SOURCES').length!==2)throw Error('Actual hydration plan marker changed');
const imports='using System;using System.IO;using System.Xml;using System.Security.Cryptography;using System.Collections.Generic;using System.Globalization;using System.Linq;using System.Xml.Linq;using Stack=RebirthGearInventoryPlan.Stack;';
const bodies=sources.map(source=>source.replace(/^using[^\r\n]+;\r?$/gm,'')).join(String.fromCharCode(10));
fixture=imports+String.fromCharCode(10)+fixture.replace('// ACTUAL_PLAN_SOURCES',bodies);
fixture=alignHydrationFixture(fixture);
const temp=await mkdtemp(join(tmpdir(),'rh-'));
try{
 const cs=join(temp,'fixture.cs'),exe=join(temp,'fixture.exe');await writeFile(cs,fixture);await writeHydrationFixtureConfig(exe);
 await run('C:/Program Files/dotnet/dotnet.exe',['C:/Program Files/dotnet/sdk/9.0.301/Roslyn/bincore/csc.dll','/nologo','/langversion:latest','/target:exe','/out:'+exe,...['mscorlib','System','System.Core','System.Xml','System.Xml.Linq'].map(n=>'/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/'+n+'.dll'),cs],{windowsHide:true,timeout:20000});
 process.stdout.write((await run(exe,[],{windowsHide:true,timeout:20000})).stdout);
}finally{
 if(process.env.REBIRTH_KEEP_FIXTURE==='1')process.stdout.write(temp+'\n');
 else{if(!resolve(temp).toLowerCase().startsWith((resolve(tmpdir())+sep).toLowerCase())||!basename(temp).startsWith('rh-'))throw Error('Unsafe fixture cleanup');await rm(temp,{recursive:true});}
}









