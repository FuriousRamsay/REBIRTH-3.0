import {alignHydrationSourcePaths,alignHydrationFixture,writeHydrationFixtureConfig} from './mp018_existing_cohort_boundaries.mjs';
// AUTHORED_NOT_RUN. Execute only after all production/scenario coding completes.
import {readFile,writeFile,mkdtemp,rm} from 'node:fs/promises';import {execFile} from 'node:child_process';import {promisify} from 'node:util';
import {tmpdir} from 'node:os';import {join,resolve,dirname,basename,sep} from 'node:path';import {fileURLToPath} from 'node:url';
const here=dirname(fileURLToPath(import.meta.url)),root=resolve(here,'../..'),run=promisify(execFile);
const paths=['Scripts/Survivor/Domain/RebirthGearInventoryPlan.cs','Scripts/Survivor/Domain/RebirthGearInventoryPlanCodec.cs','Scripts/Survivor/Domain/RebirthGearEncodedSnapshot.cs','Scripts/Metabolism/RebirthHydrationTransferPlan.cs','Scripts/Metabolism/RebirthHydrationTransferOriginal.cs','Scripts/Survivor/Network/RebirthSurvivorRequestScope.cs','Scripts/Metabolism/RebirthHydrationOwnerHold.cs','Scripts/Metabolism/RebirthHydrationOwnerCheckpoint.cs','Scripts/Survivor/Support/RebirthGearReceiptReader.cs','Scripts/Metabolism/RebirthHydrationColdOwnerWitness.cs','Scripts/Vehicles/Camper/RebirthCamperTransferJournal.cs','Scripts/Metabolism/RebirthHydrationTransferJournal.CompletionHistory.cs','Scripts/Metabolism/RebirthHydrationTransferJournal.ColdLocalCompletion.cs','Scripts/Metabolism/RebirthMetabolismStateRepository.HydrationColdAfter.cs','Scripts/Metabolism/RebirthHydrationMetabolismReceiptCodec.cs','Scripts/Metabolism/RebirthHydrationColdOwnerWitness.Recovery.cs','Scripts/Metabolism/RebirthHydrationOwnerHold.ColdRecovery.cs','Scripts/Metabolism/RebirthHydrationColdRecoveryLease.cs','Scripts/Metabolism/RebirthMetabolismStateRepository.HydrationColdRecovery.cs','Scripts/Metabolism/RebirthHydrationTransferJournal.ColdRecoveryTerminal.cs','Scripts/Metabolism/RebirthHydrationColdRecoveryLease.Terminal.cs','Scripts/Metabolism/RebirthHydrationOwnerHold.ColdRecoveryTerminal.cs'];
alignHydrationSourcePaths(paths);
const sources=await Promise.all(paths.map(path=>readFile(join(root,path),'utf8')));
let fixture=await readFile(join(here,'mp018_owner_checkpoint_fixture.cs'),'utf8');
const shared=await readFile(join(here,'mp018_owner_hold_fixture.cs'),'utf8');
const split=shared.indexOf('internal static class Program {');
if(split<0||shared.split('// ACTUAL_PLAN_SOURCES').length!==2)throw Error('Shared hydration owner adapter boundary changed');
fixture=shared.slice(0,split)+String.fromCharCode(10)+fixture;
fixture+='\n'+await readFile(join(here,'mp018_survivor_scope_adapter.cs'),'utf8');
fixture=fixture.replace('internal sealed class RebirthStablePlayerIdentity {','internal sealed class RebirthStablePlayerIdentity {internal static bool TryResolveServerEntity(EntityPlayerLocal player,out RebirthStablePlayerIdentity owner)=>TryFromLocalPlatform(out owner);');
fixture+='\n'+await readFile(join(here,'mp018_cold_owner_adapter.cs'),'utf8');
const coldOwnerCases=await readFile(join(here,'mp018_cold_owner_cases.cs'),'utf8');const coldOwnerPrint=fixture.indexOf('  Console.WriteLine(');if(coldOwnerPrint<0)throw Error('Cold owner case boundary changed');fixture=fixture.slice(0,coldOwnerPrint)+coldOwnerCases+'\n'+fixture.slice(coldOwnerPrint);
const completionWal=await readFile(join(here,'mp018_local_terminal_fixture.cs'),'utf8');const completionWalEnd=completionWal.indexOf('// PROGRAM_PREFIX');if(completionWalEnd<0)throw Error('Cold WAL adapter boundary changed');fixture+='\n'+completionWal.slice(0,completionWalEnd)+'\n'+await readFile(join(here,'mp018_cold_joint_adapter.cs'),'utf8');
const completionCases=await readFile(join(here,'mp018_cold_joint_cases.cs'),'utf8');const completionPrint=fixture.indexOf('  Console.WriteLine(');if(completionPrint<0)throw Error('Cold completion cases boundary changed');fixture=fixture.slice(0,completionPrint)+completionCases+'\n'+fixture.slice(completionPrint);
const jointSchema=await readFile(join(here,'mp018_metabolism_settlement_fixture.cs'),'utf8');const jointSchemaEnd=jointSchema.indexOf('// PROGRAM_PREFIX');if(jointSchemaEnd<0)throw Error('Cold joint schema boundary changed');fixture+='\n'+jointSchema.slice(0,jointSchemaEnd);
const nativeItem='internal sealed class ItemStack {internal int count;internal string Data="";internal bool IsEmpty()=>count==0;}';if(fixture.split(nativeItem).length!==2)throw Error('Cold joint native item boundary changed');fixture=fixture.replace(nativeItem,'internal sealed class ItemStack {internal int count;internal string Data="";internal ItemValue itemValue=>new ItemValue{Data=Data};internal bool IsEmpty()=>count==0;}');
fixture=fixture.replace('internal string SurvivorCreationId;internal int Revision;','internal string SurvivorCreationId;internal int Revision;internal void Touch(){Revision++;}');
fixture=fixture.replace('internal sealed class ItemValue {internal string Data;}','internal sealed class ItemValue {internal string Data;internal object ItemClass=>this;internal bool IsEmpty()=>string.IsNullOrEmpty(Data);}');
fixture=fixture.replace('internal static class RebirthNativeItemCodec {','internal static class RebirthNativeItemCodec {internal static bool TryDecode(string data,out ItemValue value){value=new ItemValue{Data=data};return !string.IsNullOrEmpty(data);}');
fixture=fixture.replace('internal ItemValue itemValue=>new ItemValue{Data=Data};','internal ItemValue itemValue=>new ItemValue{Data=Data};internal static ItemStack Empty=>new ItemStack();internal ItemStack(){}internal ItemStack(ItemValue value,int count){Data=value.Data;this.count=count;}internal ItemStack Clone()=>new ItemStack{count=count,Data=Data};');
fixture+='\n'+await readFile(join(here,'mp018_cold_settlement_adapter.cs'),'utf8');
const partialCases=await readFile(join(here,'mp018_cold_settlement_cases.cs'),'utf8');const partialPrint=fixture.indexOf('  Console.WriteLine(');if(partialPrint<0)throw Error('Partial settlement case boundary changed');fixture=fixture.slice(0,partialPrint)+partialCases+'\n'+fixture.slice(partialPrint);
const coldTerminalCases=await readFile(join(here,'mp018_cold_terminal_cases.cs'),'utf8');const coldTerminalPrint=fixture.indexOf('  Console.WriteLine(');if(coldTerminalPrint<0)throw Error('Cold terminal cases boundary changed');fixture=fixture.slice(0,coldTerminalPrint)+coldTerminalCases+'\n'+fixture.slice(coldTerminalPrint);
if(fixture.split('// ACTUAL_PLAN_SOURCES').length!==2)throw Error('Actual hydration plan marker changed');
const imports='using System.Security.Cryptography;using System;using System.IO;using System.Collections.Generic;using System.Globalization;using System.Linq;using System.Xml.Linq;using Stack=RebirthGearInventoryPlan.Stack;';
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






