// AUTHORED_NOT_RUN. Run only after all remaining coding is complete.
import {readFile,writeFile,mkdtemp,rm} from 'node:fs/promises';
import {execFile} from 'node:child_process';
import {promisify} from 'node:util';
import {tmpdir} from 'node:os';
import {join,resolve,dirname,basename,sep} from 'node:path';
import {fileURLToPath} from 'node:url';
const run=promisify(execFile),here=dirname(fileURLToPath(import.meta.url)),root=resolve(here,'../..');
let fixture=await readFile(join(here,'mp010_cooking_terminal_fixture.cs'),'utf8');
const dispatch=await readFile(join(root,'Scripts/Crafting/RemoteCrafting/RebirthRemoteResourceDurableDispatch.cs'),'utf8');
const start=dispatch.indexOf('    private static bool Scope('),end=dispatch.indexOf('    private static void Bind(',start);
if(start<0||end<=start||fixture.split('// ACTUAL_SCOPE').length!==2)throw Error('Actual scope boundary changed');
const pendingStart=dispatch.indexOf('    private sealed class Pending:IDisposable'),pendingEnd=dispatch.indexOf('    private static readonly Dictionary<Guid,Pending>',pendingStart);
if(pendingStart<0||pendingEnd<=pendingStart)throw Error('Actual pending lifetime boundary changed');
fixture=fixture.replace('// ACTUAL_SCOPE',dispatch.slice(pendingStart,pendingEnd)+dispatch.slice(start,end)+'\ninternal static bool CheckPending(EntityPlayer actor,int deaths,bool networkCurrent=true)=>CurrentPending(new Pending{Actor=actor,Owner="owner",Creation="creation",Deaths=deaths,Network=new RebirthRemoteResourceNetworkScope{Current=networkCurrent}});\n');
let terminal=await readFile(join(root,'Scripts/Crafting/RemoteCrafting/RebirthRemoteResourceDurableDispatch.CookingTerminal.cs'),'utf8');
terminal=terminal.slice(terminal.indexOf('internal static partial class'));
if(!terminal.startsWith('internal static partial class'))throw Error('Actual terminal class missing');
fixture+='\n'+terminal;
const outcome=await readFile(join(root,'Scripts/Crafting/RemoteCrafting/RebirthRemoteResourceDurableOutcome.cs'),'utf8');
const scopeStart=outcome.indexOf('    private static bool ActorScope('),scopeEnd=outcome.indexOf('    private static Dictionary<string,int> ReadMap(',scopeStart);
if(scopeStart<0||scopeEnd<=scopeStart)throw Error('Actual outcome scope boundary changed');
const refusal=await readFile(join(root,'Scripts/Crafting/RemoteCrafting/RebirthRemoteResourceDurableOutcome.CookingRefusal.cs'),'utf8');
fixture+='\n'+refusal.slice(refusal.indexOf('internal static partial class'));
fixture+='\ninternal static partial class RebirthRemoteResourceDurableOutcome {\n'+outcome.slice(scopeStart,scopeEnd)+'\ninternal static bool CheckScope(EntityPlayer actor,RebirthRemoteResourceDurableJournal.Record record)=>NoPaymentActorScope(actor,record);\n}\n';
const ackStart=outcome.indexOf('    internal static bool AcknowledgeNoPayment('),ackEnd=outcome.indexOf('    // Called only after GridCustody',ackStart);
if(ackStart<0||ackEnd<=ackStart)throw Error('Actual no-payment transition boundary changed');
fixture+='\ninternal static partial class RebirthRemoteResourceDurableOutcome {\n'+outcome.slice(ackStart,ackEnd)+'\nprivate static ItemStack[] ReadSlots(XElement node)=>RebirthCamperInventorySnapshot.Restore(Convert.FromBase64String(node.Value));\n}\n';
const temp=await mkdtemp(join(tmpdir(),'rebirth-cooking-terminal-'));
try{
 const cs=join(temp,'fixture.cs'),exe=join(temp,'fixture.exe');await writeFile(cs,fixture);
 await run('C:/Program Files/dotnet/dotnet.exe',['C:/Program Files/dotnet/sdk/9.0.301/Roslyn/bincore/csc.dll','/nologo','/langversion:latest','/target:exe','/out:'+exe,...['mscorlib','System','System.Core','System.Xml','System.Xml.Linq'].map(n=>'/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/'+n+'.dll'),cs],{windowsHide:true,timeout:20000});
 process.stdout.write((await run(exe,[],{windowsHide:true,timeout:20000})).stdout);
}finally{
 if(process.env.REBIRTH_KEEP_FIXTURE==='1')process.stdout.write(temp+'\n');
 else{if(!resolve(temp).toLowerCase().startsWith((resolve(tmpdir())+sep).toLowerCase())||!basename(temp).startsWith('rebirth-cooking-terminal-'))throw Error('Unsafe fixture cleanup');await rm(temp,{recursive:true});}
}
