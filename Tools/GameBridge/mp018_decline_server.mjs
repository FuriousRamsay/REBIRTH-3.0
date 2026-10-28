import {alignHydrationSourcePaths,alignHydrationFixture,writeHydrationFixtureConfig} from './mp018_existing_cohort_boundaries.mjs';
// AUTHORED_NOT_RUN. Execute only after all production/scenario coding completes.
import {readFile,writeFile,mkdtemp,rm} from 'node:fs/promises';import {execFile} from 'node:child_process';import {promisify} from 'node:util';
import {tmpdir} from 'node:os';import {join,resolve,dirname,basename,sep} from 'node:path';import {fileURLToPath} from 'node:url';
const here=dirname(fileURLToPath(import.meta.url)),root=resolve(here,'../..'),run=promisify(execFile);
const paths=['Scripts/Survivor/Domain/RebirthGearInventoryPlan.cs','Scripts/Survivor/Domain/RebirthGearInventoryPlanCodec.cs','Scripts/Survivor/Domain/RebirthGearEncodedSnapshot.cs','Scripts/Metabolism/RebirthHydrationTransferPlan.cs','Scripts/Metabolism/RebirthHydrationTransferOriginal.cs','Scripts/Survivor/Network/RebirthSurvivorRequestScope.cs','Scripts/Vehicles/Camper/RebirthCamperTransferJournal.cs','Scripts/Metabolism/RebirthHydrationTransferJournal.cs','Scripts/Metabolism/RebirthHydrationServerSourceHold.cs','Scripts/Metabolism/RebirthHydrationRemoteOwnerWitness.cs','Scripts/Survivor/Support/RebirthGearReceiptReader.cs','Scripts/Crafting/RemoteCrafting/RebirthRemoteResourceFragments.cs','Scripts/Metabolism/RebirthHydrationOriginalTransport.cs','Scripts/Metabolism/RebirthHydrationProposalServer.cs','Scripts/Metabolism/RebirthHydrationRemoteGrant.cs','Scripts/Metabolism/RebirthHydrationGrantServer.cs','Scripts/Metabolism/RebirthHydrationPreparedServer.cs','Scripts/Metabolism/RebirthHydrationTransferJournal.PreparedRejection.cs','Scripts/Metabolism/RebirthHydrationServerSourceHold.PreparedRejection.cs','Scripts/Metabolism/RebirthHydrationTransferJournal.RejectedHistory.cs','Scripts/Metabolism/RebirthHydrationProposalServer.Rejected.cs','Scripts/Metabolism/RebirthHydrationRejectedServer.cs','Scripts/Metabolism/RebirthHydrationDeclineServer.cs','Scripts/Metabolism/RebirthHydrationTransferJournal.CompletionHistory.cs'];
alignHydrationSourcePaths(paths);
const sources=await Promise.all(paths.map(path=>readFile(join(root,path),'utf8')));
let shared=await readFile(join(here,'mp018_hydration_journal_fixture.cs'),'utf8');const program=shared.indexOf('internal static class Program {');if(program<0)throw Error('Journal adapter boundary changed');shared=shared.slice(0,program);
const spawned='internal bool IsDead()=>Dead;';if(shared.split(spawned).length!==2)throw Error('Native actor boundary changed');shared=shared.replace(spawned,spawned+'internal bool Spawned=true;internal bool IsSpawned()=>Spawned;');
let body=await readFile(join(here,'mp018_server_source_hold_fixture.cs'),'utf8');const witness=await readFile(join(here,'mp018_remote_owner_witness_fixture.cs'),'utf8');const parts=witness.split('// WITNESS_CASES');if(parts.length!==2||body.split('// SOURCE_CHECKPOINT_CASES').length!==2)throw Error('Witness adapter boundary changed');
shared=shared.replace('internal sealed class ClientInfo {','internal sealed class ClientInfo {internal NativeFile latestPlayerData;');
const proposalAdapters='internal static class RebirthToolbeltCapacity {internal static int GetOwnedSlotCount(EntityPlayer player,int length)=>4;}';
body=body.replace('// SOURCE_CHECKPOINT_CASES',parts[1]+'\n'+await readFile(join(here,'mp018_proposal_server_cases.cs'),'utf8')+'\n'+await readFile(join(here,'mp018_prepared_server_cases.cs'),'utf8')+'\n'+await readFile(join(here,'mp018_decline_server_cases.cs'),'utf8'));
shared+='\n'+proposalAdapters;let fixture=shared+'\n'+parts[0]+'\n'+body;
fixture=fixture.replace('internal sealed class ConnectionManager {','internal sealed partial class ConnectionManager {');
fixture=fixture.replace('internal static bool SourceSaved=true;','internal static bool SourceSaved=true;internal static bool HasSavedRemoteHydrationBefore(RebirthHydrationServerSourceHold.Lease lease)=>SourceSaved;');
fixture+='\n'+await readFile(join(here,'mp018_prepared_server_adapters.cs'),'utf8');
fixture+='\n'+await readFile(join(here,'mp018_decline_server_adapters.cs'),'utf8');
fixture+='\n'+await readFile(join(here,'mp018_survivor_scope_adapter.cs'),'utf8');
if(fixture.split('// ACTUAL_PLAN_SOURCES').length!==2)throw Error('Actual hydration plan marker changed');
const imports='using System;using System.IO;using System.Xml;using System.Text;using System.Security.Cryptography;using System.Collections.Generic;using System.Globalization;using System.Linq;using System.Xml.Linq;using Stack=RebirthGearInventoryPlan.Stack;';
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










