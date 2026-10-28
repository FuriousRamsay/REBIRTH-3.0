// AUTHORED_NOT_RUN. Execute only after all production/scenario coding completes.
import {readFile,writeFile,mkdtemp,rm} from 'node:fs/promises';import {execFile} from 'node:child_process';import {promisify} from 'node:util';
import {tmpdir} from 'node:os';import {join,resolve,dirname,basename,sep} from 'node:path';import {fileURLToPath} from 'node:url';
const here=dirname(fileURLToPath(import.meta.url)),root=resolve(here,'../..'),run=promisify(execFile);
const source=await readFile(join(root,'Scripts/Survivor/Repair/RebirthRepairOriginalCorrection.cs'),'utf8');
const cancellation=await readFile(join(root,'Scripts/Survivor/Repair/RebirthRepairOriginalCorrection.CancellationTerminal.cs'),'utf8');
let fixture=await readFile(join(here,'mp066_repair_server_fixture.cs'),'utf8');
for(const [marker,a,b] of [['// ACTUAL_RECEIPT','    internal sealed class Receipt','    private sealed class Activation'],['// ACTUAL_SERVER','    internal static void ReceiveServer(','    internal static void ReceiveClient(']]){
 const begin=source.indexOf(a),end=source.indexOf(b,begin);if(begin<0||end<=begin||fixture.split(marker).length!==2)throw Error('Actual repair server boundary changed '+marker);fixture=fixture.replace(marker,source.slice(begin,end));
}
const cancelBegin=cancellation.indexOf('    private static void ConfirmHistoricalCancellation('),cancelEnd=cancellation.lastIndexOf('}');
if(cancelBegin<0||cancelEnd<=cancelBegin||fixture.split('// ACTUAL_HISTORICAL_CANCEL').length!==2)throw Error('Historical cancellation source boundary missing');
fixture=fixture.replace('// ACTUAL_HISTORICAL_CANCEL',cancellation.slice(cancelBegin,cancelEnd));
const baseBegin=source.indexOf('    private static void SettleEndedBase('),baseEnd=source.indexOf('    private static void Reply(',baseBegin);
if(baseBegin<0||baseEnd<=baseBegin||fixture.split('// ACTUAL_ENDED_BASE').length!==2)throw Error('Ended-base source boundary missing');
fixture=fixture.replace('// ACTUAL_ENDED_BASE',source.slice(baseBegin,baseEnd));
const temp=await mkdtemp(join(tmpdir(),'rebirth-repair-server-'));
try{
 const cs=join(temp,'fixture.cs'),exe=join(temp,'fixture.exe');await writeFile(cs,fixture);
 await run('C:/Program Files/dotnet/dotnet.exe',['C:/Program Files/dotnet/sdk/9.0.301/Roslyn/bincore/csc.dll','/nologo','/langversion:latest','/target:exe','/out:'+exe,...['mscorlib','System','System.Core','System.Xml','System.Xml.Linq'].map(n=>'/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/'+n+'.dll'),cs],{windowsHide:true,timeout:20000});
 process.stdout.write((await run(exe,[],{windowsHide:true,timeout:20000})).stdout);
}finally{
 if(process.env.REBIRTH_KEEP_FIXTURE==='1')process.stdout.write(temp+'\n');
 else{if(!resolve(temp).toLowerCase().startsWith((resolve(tmpdir())+sep).toLowerCase())||!basename(temp).startsWith('rebirth-repair-server-'))throw Error('Unsafe fixture cleanup');await rm(temp,{recursive:true});}
}
