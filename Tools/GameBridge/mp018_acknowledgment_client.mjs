import {alignHydrationSourcePaths,alignHydrationFixture,writeHydrationFixtureConfig} from './mp018_existing_cohort_boundaries.mjs';
// AUTHORED_NOT_RUN. Execute only after all production/scenario coding completes.
import {readFile,writeFile,mkdtemp,rm} from 'node:fs/promises';import {execFile} from 'node:child_process';import {promisify} from 'node:util';
import {tmpdir} from 'node:os';import {join,resolve,dirname,basename,sep} from 'node:path';import {fileURLToPath} from 'node:url';
const here=dirname(fileURLToPath(import.meta.url)),root=resolve(here,'../..'),run=promisify(execFile);
const paths=['Scripts/Survivor/Domain/RebirthGearInventoryPlan.cs','Scripts/Survivor/Domain/RebirthGearInventoryPlanCodec.cs','Scripts/Survivor/Domain/RebirthGearEncodedSnapshot.cs','Scripts/Metabolism/RebirthHydrationTransferPlan.cs','Scripts/Metabolism/RebirthHydrationTransferOriginal.cs','Scripts/Survivor/Network/RebirthSurvivorRequestScope.cs','Scripts/Metabolism/RebirthHydrationOwnerHold.cs','Scripts/Metabolism/RebirthHydrationTransferGrant.cs','Scripts/Metabolism/RebirthHydrationOwnerApply.cs','Scripts/Survivor/Support/RebirthGearOwnerApplySequence.cs','Scripts/Survivor/Support/RebirthGearNativeWritePermit.cs','Scripts/Metabolism/RebirthHydrationOwnerHold.RemoteGrant.cs','Scripts/Metabolism/RebirthHydrationRemoteClientGrant.cs','Scripts/Metabolism/RebirthHydrationOwnerApply.RemoteGrant.cs','Scripts/Metabolism/RebirthHydrationRemoteClientTerminal.cs','Scripts/Metabolism/RebirthHydrationRemoteClientGrant.Terminal.cs','Scripts/Metabolism/RebirthHydrationOwnerHold.RemoteTerminal.cs','Scripts/Metabolism/RebirthHydrationAcknowledgmentClient.cs'];
alignHydrationSourcePaths(paths);
const sources=await Promise.all(paths.map(path=>readFile(join(root,path),'utf8')));
const swap=(text,before,after)=>{if(text.split(before).length!==2)throw Error('Adapter boundary changed: '+before);return text.replace(before,after);};
let shared=await readFile(join(here,'mp018_owner_hold_fixture.cs'),'utf8');
const split=shared.indexOf('internal static class Program {');if(split<0)throw Error('Shared hold program boundary changed');shared=shared.slice(0,split);
shared=swap(shared,'internal sealed class Bag {','internal sealed partial class Bag {');shared=swap(shared,'internal sealed class Inventory {','internal sealed partial class Inventory {');
shared=swap(shared,'internal sealed class ItemStack {internal int count;internal string Data="";internal bool IsEmpty()=>count==0;}',
 'internal sealed class ItemStack {internal int count;internal string Data="";internal ItemValue itemValue=>new ItemValue{Data=Data};internal static ItemStack Empty=>new ItemStack();internal ItemStack(){}internal ItemStack(ItemValue value,int count){Data=value.Data;this.count=count;}internal ItemStack Clone()=>new ItemStack{count=count,Data=Data};internal bool IsEmpty()=>count==0;}');
let grantFixture=await readFile(join(here,'mp018_local_grant_fixture.cs'),'utf8');
const program=grantFixture.indexOf('internal static class Program {'),main=grantFixture.indexOf(' public static void Main(){');if(program<0||main<program)throw Error('Grant fixture program boundary changed');
let adapters=grantFixture.slice(0,program);
adapters=swap(adapters,'internal sealed class ItemValue {internal string Data="";}','internal sealed class ItemValue {internal string Data="";internal object ItemClass=>this;internal bool IsEmpty()=>string.IsNullOrEmpty(Data);}');
adapters=swap(adapters,'internal static class RebirthHydrationOwnerCheckpoint {internal static bool Saved=true;internal static bool HasSavedBefore(EntityPlayerLocal player,RebirthHydrationTransferOriginal original)=>Saved;}','');
let programPrefix=grantFixture.slice(program,main);
programPrefix=swap(programPrefix,'&&RebirthHydrationOwnerHold.TryAcquire(player,original)','');
let ownerAdapters=await readFile(join(here,'mp018_owner_apply_fixture.cs'),'utf8');const ownerMain=ownerAdapters.indexOf(' public static void Main(){');if(ownerMain<0)throw Error('Owner application program boundary changed');ownerAdapters=ownerAdapters.slice(0,ownerMain).replace('// PROGRAM_PREFIX',programPrefix);
ownerAdapters=swap(ownerAdapters,'internal static bool Saved=true,Applying=true,Applied=true;','internal static bool Saved=true,Applying=true,Applied=true;internal static bool HasSaved(EntityPlayerLocal player,RebirthHydrationTransferOriginal original,RebirthGearOwnerReceipt stage)=>Saved&&player.Buffs.GetCustomVar("rbGear_"+original.Operation.ToString("N"))==(float)stage;');
let client=await readFile(join(here,'mp018_remote_client_grant_fixture.cs'),'utf8');const terminalCases=await readFile(join(here,'mp018_remote_client_terminal_cases.cs'),'utf8')+'\n'+await readFile(join(here,'mp018_acknowledgment_client_cases.cs'),'utf8');const print=client.indexOf('  Console.WriteLine(');if(print<0)throw Error('Client scenario boundary changed');client=client.slice(0,print)+terminalCases+'\n'+client.slice(print);
shared=swap(shared,'internal sealed class ConnectionManager {','internal sealed partial class ConnectionManager {');
let fixture=shared+'\n'+adapters+'\n'+await readFile(join(here,'mp018_acknowledgment_client_adapters.cs'),'utf8')+'\n'+client.replace('// ACTUAL_OWNER_ADAPTERS',ownerAdapters);
fixture+='\n'+await readFile(join(here,'mp018_seen_checkpoint_adapter.cs'),'utf8');
fixture+='\n'+await readFile(join(here,'mp018_survivor_scope_adapter.cs'),'utf8');
if(fixture.split('// ACTUAL_PLAN_SOURCES').length!==2)throw Error('Actual hydration source marker changed');
const imports='using System;using System.IO;using System.Collections.Generic;using System.Globalization;using System.Linq;using System.Xml.Linq;using Stack=RebirthGearInventoryPlan.Stack;';
fixture=imports+'\n'+fixture.replace('// ACTUAL_PLAN_SOURCES',sources.map(source=>source.replace(/^using[^\r\n]+;\r?$/gm,'')).join('\n'));
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




