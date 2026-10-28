// AUTHORED_NOT_RUN. Invoke only after the user's all-coding checkpoint.
import {readFile,writeFile,mkdtemp,rm} from 'node:fs/promises';
import {execFile} from 'node:child_process';
import {promisify} from 'node:util';
import {tmpdir} from 'node:os';
import {join,resolve,dirname,basename,sep} from 'node:path';
import {fileURLToPath} from 'node:url';
const run=promisify(execFile),here=dirname(fileURLToPath(import.meta.url)),root=resolve(here,'../..');
let fixture=await readFile(join(here,'mp041_refund_custody_fixture.cs'),'utf8');
const actual=await readFile(join(root,'Scripts/Crafting/Cooking/XUiC_RebirthCookingWorkspace.cs'),'utf8');
const refundStart=actual.indexOf('    private void WriteReturnedIngredientSlot('),refundEnd=actual.indexOf('    private void ReconcileIngredientOrigins(',refundStart);
const returnStart=actual.indexOf('    private bool ReturnIngredients('),returnEnd=actual.indexOf('    private static bool TryBatchQuantity(',returnStart);
const pendingStart=actual.indexOf('    internal bool HasPendingIngredientReturn=>'),pendingEnd=actual.indexOf('\n',pendingStart);
const reconcileEnd=actual.indexOf('    private void Select(',refundEnd);
if(refundStart<0||refundEnd<=refundStart||returnStart<0||returnEnd<=returnStart||pendingStart<0||pendingEnd<=pendingStart)throw Error('Cooking return custody extraction boundaries changed');
if(reconcileEnd<=refundEnd)throw Error('Cooking origin reconciliation extraction boundary changed');
for(const [marker,source] of [['// ACTUAL_REFUND',actual.slice(refundStart,refundEnd)],['// ACTUAL_RETURN_GRID',actual.slice(returnStart,returnEnd)],['// ACTUAL_PENDING_RETURN',actual.slice(pendingStart,pendingEnd)],['// ACTUAL_RECONCILE',actual.slice(refundEnd,reconcileEnd)]]){
 if(fixture.split(marker).length!==2)throw Error('Expected one extraction slot: '+marker);
 fixture=fixture.replace(marker,source);
}
const temp=await mkdtemp(join(tmpdir(),'rebirth-refund-custody-'));
try{
 const cs=join(temp,'fixture.cs'),exe=join(temp,'fixture.exe');await writeFile(cs,fixture);
 await run('C:/Program Files/dotnet/dotnet.exe',['C:/Program Files/dotnet/sdk/9.0.301/Roslyn/bincore/csc.dll','/nologo','/langversion:latest','/target:exe','/out:'+exe,...['mscorlib','System','System.Core'].map(n=>'/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/'+n+'.dll'),cs],{windowsHide:true,timeout:20000});
 process.stdout.write((await run(exe,[],{windowsHide:true,timeout:20000})).stdout);
}finally{
 if(process.env.REBIRTH_KEEP_FIXTURE==='1')process.stdout.write(temp+'\n');
 else{if(!resolve(temp).toLowerCase().startsWith((resolve(tmpdir())+sep).toLowerCase())||!basename(temp).startsWith('rebirth-refund-custody-'))throw Error('Refuse cleanup outside generated fixture');await rm(temp,{recursive:true});}
}
