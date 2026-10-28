// AUTHORED_NOT_RUN. Deferred source cohort; no game or native publication.
import {readFile,writeFile,mkdtemp,rm} from 'node:fs/promises';
import {execFile} from 'node:child_process';import {promisify} from 'node:util';
import {tmpdir} from 'node:os';import {join,resolve,dirname,basename,sep} from 'node:path';import {fileURLToPath} from 'node:url';
const here=dirname(fileURLToPath(import.meta.url)),root=resolve(here,'../..'),run=promisify(execFile);
const planner=await readFile(join(root,'Scripts/NPC/Interaction/RebirthNpcTradeOwnerPlan.cs'),'utf8');
const original=await readFile(join(root,'Scripts/NPC/Interaction/RebirthNpcTradeOriginal.cs'),'utf8');
const wire=await readFile(join(root,'Scripts/NPC/Interaction/RebirthNpcTradeQuoteWire.cs'),'utf8');
const wireStart=wire.indexOf('internal static class RebirthNpcTradeQuoteWire');if(wireStart<0)throw Error('actual trade wire source missing');
const store=await readFile(join(root,'Scripts/NPC/Interaction/RebirthNpcTradeOwnerOriginalStore.cs'),'utf8');
const storeStart=store.indexOf('internal static class RebirthNpcTradeOwnerOriginalStore');if(storeStart<0)throw Error('actual trade owner store missing');
const receipt=await readFile(join(root,'Scripts/NPC/Interaction/RebirthNpcTradeOwnerReceipt.cs'),'utf8');
const receiptStart=receipt.indexOf('internal static class RebirthNpcTradeOwnerReceipt');if(receiptStart<0)throw Error('actual owner receipt codec missing');
const checkpoint=await readFile(join(root,'Scripts/NPC/Interaction/RebirthNpcTradeOwnerCheckpoint.cs'),'utf8');
const checkpointStart=checkpoint.indexOf('internal static class RebirthNpcTradeOwnerCheckpoint');if(checkpointStart<0)throw Error('actual trade owner checkpoint missing');
const npcPlan=await readFile(join(root,'Scripts/NPC/Interaction/RebirthNpcTradeNpcPlan.cs'),'utf8');
const npcStart=npcPlan.indexOf('internal sealed class RebirthNpcTradeNpcPlan');if(npcStart<0)throw Error('actual NPC trade plan missing');
const tradeService=await readFile(join(root,'Scripts/NPC/Interaction/RebirthNpcTradeService.cs'),'utf8');
const npcSaved=await readFile(join(root,'Scripts/NPC/Persistence/RebirthNpcInventoryPersistence.Trade.cs'),'utf8');
const savedStart=npcSaved.indexOf('    internal static bool MatchesTradeSavedImage('),savedEnd=npcSaved.lastIndexOf('\n}');if(savedStart<0||savedEnd<=savedStart)throw Error('actual NPC trade saved checkpoint/image reader missing');
const callbacks=await readFile(join(root,'Scripts/NPC/Persistence/RebirthNpcSaveQuarantineClientViews.cs'),'utf8');
const callbackStart=callbacks.indexOf('    private static XmlDocument QuarantineInteractionCallbackImage('),callbackEnd=callbacks.lastIndexOf('\n}');if(callbackStart<0||callbackEnd<=callbackStart)throw Error('actual callback purpose evidence missing');
function member(from,to){const begin=tradeService.indexOf(from),end=tradeService.indexOf(to,begin+from.length);if(begin<0||end<=begin)throw Error('actual trade member missing: '+from);return tradeService.slice(begin,end);}
const begin=planner.indexOf('internal sealed class RebirthNpcTradeOwnerPlan'),start=original.indexOf('internal sealed class RebirthNpcTradeOriginal');if(begin<0||start<0)throw Error('actual trade source missing');
let fixture=await readFile(join(here,'mp048_owner_plan_fixture.cs'),'utf8');fixture=fixture.replace('// ACTUAL_OWNER_PLAN',planner.slice(begin)).replace('static void Main()','public static void PlannerMain()');
fixture='using System.IO;using System.Linq;using System.Text;using System.Xml;using System.Xml.Linq;using System.Collections.Generic;using System.Globalization;\n'+fixture+'\n'+wire.slice(wireStart)+'\n'+(await readFile(join(here,'mp048_trade_original_fixture.cs'),'utf8')).replace('// ACTUAL_TRADE_ORIGINAL',original.slice(start));
const temp=await mkdtemp(join(tmpdir(),'rebirth-trade-original-'));
fixture+='\n'+receipt.slice(receiptStart)+'\n'+(await readFile(join(here,'mp048_owner_store_fixture.cs'),'utf8')).replace('// ACTUAL_TRADE_OWNER_STORE',store.slice(storeStart)).replace('// ACTUAL_TRADE_OWNER_CHECKPOINT',checkpoint.slice(checkpointStart));
fixture+='\n'+(await readFile(join(here,'mp048_npc_plan_fixture.cs'),'utf8')).replace('// ACTUAL_TRADE_NPC_PLAN',npcPlan.slice(npcStart));
fixture='using System.Threading;\n'+fixture+'\n'+(await readFile(join(here,'mp048_quote_fixture.cs'),'utf8'))
 .replace('// ACTUAL_TRADE_QUOTE',member('    private static RebirthNpcInteractionCommandResponse Quote(', '    private static RebirthNpcInteractionCommandResponse Commit('))
 .replace('// ACTUAL_TRADE_AVAILABLE',member('    private static int GetAvailableQuantity(', '    private static string Serialize('))
 .replace('// ACTUAL_TRADE_SERIALIZE',member('    private static string Serialize(', '    private static Dictionary<string, string> Parse('));
fixture+='\n'+(await readFile(join(here,'mp048_npc_saved_fixture.cs'),'utf8')).replace('// ACTUAL_TRADE_NPC_SAVED',npcSaved.slice(savedStart,savedEnd));
fixture+='\n'+(await readFile(join(here,'mp048_callback_purpose_fixture.cs'),'utf8')).replace('// ACTUAL_TRADE_CALLBACK_IMAGE',callbacks.slice(callbackStart,callbackEnd));
const unusedHistory=await readFile(join(root,'Scripts/NPC/Interaction/RebirthNpcTradeUnusedHistory.cs'),'utf8');
const unusedStart=unusedHistory.indexOf('    internal static RebirthNpcTradeOriginal Describe('),unusedEnd=unusedHistory.indexOf('    private static string Read(',unusedStart);
if(unusedStart<0||unusedEnd<=unusedStart)throw Error('actual unused history description reader missing');
fixture+='\n'+(await readFile(join(here,'mp048_unused_history_fixture.cs'),'utf8')).replace('// ACTUAL_UNUSED_DESCRIPTION',unusedHistory.slice(unusedStart,unusedEnd));
const prefix=await readFile(join(root,'Scripts/NPC/Interaction/RebirthNpcTradePrefix.cs'),'utf8');
const prefixStart=prefix.indexOf('internal static class RebirthNpcTradePrefix');if(prefixStart<0)throw Error('actual trade prefix implementation missing');
fixture+='\n'+prefix.slice(prefixStart)+'\n'+await readFile(join(here,'mp048_prefix_fixture.cs'),'utf8');
try{
 const cs=join(temp,'fixture.cs'),exe=join(temp,'fixture.exe');await writeFile(cs,fixture);
 await run('C:/Program Files/dotnet/dotnet.exe',['C:/Program Files/dotnet/sdk/9.0.301/Roslyn/bincore/csc.dll','/nologo','/langversion:latest','/target:exe','/out:'+exe,...['mscorlib','System','System.Core','System.Xml','System.Xml.Linq'].map(n=>'/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/'+n+'.dll'),cs],{windowsHide:true,timeout:20000});
 process.stdout.write((await run(exe,[],{windowsHide:true,timeout:20000})).stdout);
}finally{
 if(!resolve(temp).toLowerCase().startsWith((resolve(tmpdir())+sep).toLowerCase())||!basename(temp).startsWith('rebirth-trade-original-'))throw Error('unsafe fixture cleanup');
 await rm(temp,{recursive:true});
}
