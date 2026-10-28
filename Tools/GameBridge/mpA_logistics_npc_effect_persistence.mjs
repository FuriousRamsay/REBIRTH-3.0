import {readFile,writeFile,mkdtemp,rm} from 'node:fs/promises';
import {execFile} from 'node:child_process';
import {promisify} from 'node:util';
import {tmpdir} from 'node:os';
import {join,resolve,dirname} from 'node:path';
import {fileURLToPath} from 'node:url';
// PREPARED, NOT RUN. Actual native-domain XML row codec and record validator;
// reuses explicit native custody and original-journal doubles from codec fixture.
const here=dirname(fileURLToPath(import.meta.url)),root=resolve(here,'../..');
const [codec,persistence,transactions,doubles,fixture]=await Promise.all([
 readFile(join(root,'Scripts/QuickStack/RebirthLogisticsNpcEffectReceipt.cs'),'utf8'),readFile(join(root,'Scripts/NPC/Persistence/RebirthNpcInventoryPersistence.cs'),'utf8'),
 readFile(join(root,'Scripts/NPC/Inventory/RebirthNpcInventoryTransactions.cs'),'utf8'),readFile(join(here,'mpA_logistics_npc_effect_receipt_fixture.cs'),'utf8'),readFile(join(here,'mpA_logistics_npc_effect_persistence_fixture.cs'),'utf8')]);
const a=persistence.indexOf('    private static XmlElement Write('),b=persistence.indexOf('    // Detached validation only;',a),c=transactions.indexOf('    public static bool ValidatePersistentRecord('),d=transactions.indexOf('    public static bool TryRestorePersistentRecord(',c);
if(a<0||b<=a||c<0||d<=c)throw new Error('Actual inventory row codec/validation anchors changed');
const source=codec+'\n'+doubles+'\npublic static class RebirthNpcInventoryTransactionService{private const int MaxReplayTransactionsPerInventory=256;\n'+transactions.slice(c,d)+'}\n'+fixture.replace('/* ACTUAL_ROW_CODEC */',persistence.slice(a,b));
const temp=await mkdtemp(join(tmpdir(),'rebirth-mpa-npc-effect-file-')),run=promisify(execFile);
try{
 const cs=join(temp,'check.cs'),exe=join(temp,'check.exe');await writeFile(cs,source);
 await run('C:/Program Files/dotnet/dotnet.exe',['C:/Program Files/dotnet/sdk/9.0.301/Roslyn/bincore/csc.dll','/nologo','/target:exe','/main:InventoryReceiptFixture','/out:'+exe,...['mscorlib','System','System.Core','System.Xml','System.Xml.Linq'].map(x=>'/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/'+x+'.dll'),cs],{windowsHide:true,timeout:10000});
 console.log((await run(exe,[],{windowsHide:true,timeout:10000})).stdout.trim());
}finally{await rm(temp,{recursive:true,force:true});}
