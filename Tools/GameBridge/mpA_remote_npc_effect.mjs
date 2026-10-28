import {readFile,writeFile,mkdtemp,rm} from 'node:fs/promises';
import {execFile} from 'node:child_process';
import {promisify} from 'node:util';
import {tmpdir} from 'node:os';
import {join,resolve,dirname} from 'node:path';
import {fileURLToPath} from 'node:url';
// PREPARED, NOT RUN. Actual fetch admission/CAS methods; disclosed native,
// durable DTO, capacity reservation and receipt-codec doubles. No disk/network proof.
const here=dirname(fileURLToPath(import.meta.url)),root=resolve(here,'../..');
const actual=await readFile(join(root,'Scripts/NPC/Inventory/RebirthNpcInventoryTransactions.cs'),'utf8');
const start=actual.indexOf('    private static bool RemoteOriginalCurrent('),end=actual.indexOf('    private static RebirthNpcInventoryTransactionResult ApplyCore(',start);
if(start<0||end<=start)throw new Error('Actual fetch original method anchors changed');
let fixture=await readFile(join(here,'mpA_logistics_npc_plan_fixture.cs'),'utf8');
fixture=fixture.slice(0,fixture.indexOf(' public static void Main(){'));
fixture=fixture.replace('key=="transfer"&&purpose==RebirthNpcInventoryAuthorityOperations.Transfer','key=="mutate"&&purpose==RebirthNpcInventoryAuthorityOperations.Mutate');
fixture=fixture.replace('public static bool IsNpcItemLocked(RebirthNpcStableId id,string key)=>false;','public static bool Locked; public static bool IsNpcItemLocked(RebirthNpcStableId id,string key)=>Locked;');
fixture=fixture.replace(' internal const int MaximumReceipts=128;',' internal const int MaximumReceipts=128; internal static string ExpectedRemoteResource(RebirthRemoteResourceNpcEffectPlan plan)=>plan.Operation.ToString("N")+":"+plan.Binding;');
fixture=fixture.replace('internal static class RebirthNpcOriginalEffectAdmission{','internal static class RebirthNpcOriginalEffectAdmission{ internal sealed class Lease{internal bool Current()=>true;} internal static bool Reserved=true; internal static bool IsReservedRemoteResource(RebirthRemoteResourceNpcEffectPlan plan)=>Reserved; internal static bool TryReserveRemoteResource(EntityPlayer actor,Guid op,string source,out Lease lease){lease=new Lease();return Reserved;}');
fixture=fixture.replace(' /* ACTUAL_NPC_PLAN_METHOD */',actual.slice(start,end)+'\n private static bool PruneSavedTerminalLogisticsEffects(RebirthNpcStableId id)=>true;');
const tail=await readFile(join(here,'mpA_remote_npc_effect_fixture.cs'),'utf8');
const source='using System;\nusing System.Collections.Generic;\nusing System.Linq;\nusing System.Threading;\nusing System.Xml.Linq;\n'+fixture+tail;
const temp=await mkdtemp(join(tmpdir(),'rebirth-mpa-fetch-npc-')),run=promisify(execFile);
try{const cs=join(temp,'check.cs'),exe=join(temp,'check.exe');await writeFile(cs,source);await run('C:/Program Files/dotnet/dotnet.exe',['C:/Program Files/dotnet/sdk/9.0.301/Roslyn/bincore/csc.dll','/nologo','/target:exe','/out:'+exe,...['mscorlib','System','System.Core','System.Xml','System.Xml.Linq'].map(x=>'/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/'+x+'.dll'),cs],{windowsHide:true,timeout:10000});console.log((await run(exe,[],{windowsHide:true,timeout:10000})).stdout.trim());}finally{await rm(temp,{recursive:true,force:true});}
