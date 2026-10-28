// AUTHORED_NOT_RUN. Deferred actual quote phase and binary journal source.
import {readFile,writeFile,mkdtemp,rm} from 'node:fs/promises';
import {execFile} from 'node:child_process';import {promisify} from 'node:util';
import {tmpdir} from 'node:os';import {join,resolve,dirname,basename,sep} from 'node:path';import {fileURLToPath} from 'node:url';
const here=dirname(fileURLToPath(import.meta.url)),root=resolve(here,'../..'),run=promisify(execFile);
const source=await readFile(join(root,'Scripts/NPC/Interaction/RebirthNpcTradeHostQuote.cs'),'utf8');
function between(start,end){const a=source.indexOf(start),b=source.indexOf(end,a);if(a<0||b<=a)throw Error('actual quote phase member missing');return source.slice(a,b);}
const journal=await readFile(join(root,'Scripts/Vehicles/Camper/RebirthCamperTransferJournal.cs'),'utf8');
const fixture=(await readFile(join(here,'mp048_quote_phase_fixture.cs'),'utf8'))
 .replace('// ACTUAL_QUOTE_BINARY_JOURNAL',journal.slice(journal.indexOf('public sealed class RebirthCamperTransferJournal')))
 .replace('// ACTUAL_QUOTE_READ',between('        private static void Safe(', '        internal static bool TryIssue('))
 .replace('// ACTUAL_QUOTE_PHASES',between('        internal bool TryEnterPermission(', '        internal static bool TryReadEntered('))
 .replace('// ACTUAL_WARM_QUOTE',between('        internal static bool TryMarkLocalUnused(RebirthNpcTradeOwnerExchange.LocalUnusedBefore', '        internal static bool TryRestoreExpiredUnused(').replaceAll('HostQuote','QuotePhase'));
const temp=await mkdtemp(join(tmpdir(),'rebirth-quote-phase-source-'));
try{
 const cs=join(temp,'fixture.cs'),exe=join(temp,'fixture.exe');await writeFile(cs,fixture);
 await run('C:/Program Files/dotnet/dotnet.exe',['C:/Program Files/dotnet/sdk/9.0.301/Roslyn/bincore/csc.dll','/nologo','/langversion:latest','/target:exe','/out:'+exe,...['mscorlib','System','System.Core','System.Xml','System.Xml.Linq'].map(n=>'/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/'+n+'.dll'),cs],{windowsHide:true,timeout:20000});
 process.stdout.write((await run(exe,[],{windowsHide:true,timeout:20000})).stdout);
}finally{
 if(!resolve(temp).toLowerCase().startsWith((resolve(tmpdir())+sep).toLowerCase())||!basename(temp).startsWith('rebirth-quote-phase-source-'))throw Error('unsafe fixture cleanup');
 await rm(temp,{recursive:true});
}
