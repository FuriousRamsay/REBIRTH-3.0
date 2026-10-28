// AUTHORED_NOT_RUN. Execute only after all coding completes.
import {readFile,writeFile,mkdtemp,rm} from 'node:fs/promises';import {execFile} from 'node:child_process';import {promisify} from 'node:util';
import {tmpdir} from 'node:os';import {join,resolve,dirname,basename,sep} from 'node:path';import {fileURLToPath} from 'node:url';
const here=dirname(fileURLToPath(import.meta.url)),root=resolve(here,'../..'),run=promisify(execFile);
const source=await readFile(join(root,'Scripts/Crafting/Cooking/RebirthCookingGridCustody.cs'),'utf8');
const begin=source.indexOf('    internal static bool RejectUnappliedExternal('),end=source.indexOf('    internal static bool HasOwnedSnapshot(',begin);
let fixture=await readFile(join(here,'mp042_grid_rejection_fixture.cs'),'utf8');
if(begin<0||end<=begin||fixture.split('// ACTUAL_REJECT').length!==2)throw Error('Actual grid rejection boundary changed');fixture=fixture.replace('// ACTUAL_REJECT',source.slice(begin,end));
const completionStart=source.indexOf('    internal static bool CompleteExternal('),completionEnd=source.indexOf('    internal static void ConfirmAdmissionTerminal(',completionStart);if(completionStart<0||completionEnd<=completionStart||fixture.split('// ACTUAL_COMPLETE').length!==2)throw Error('Actual grid completion boundary changed');fixture=fixture.replace('// ACTUAL_COMPLETE',source.slice(completionStart,completionEnd));
const temp=await mkdtemp(join(tmpdir(),'rebirth-grid-rejection-'));
try{
 const cs=join(temp,'fixture.cs'),exe=join(temp,'fixture.exe');await writeFile(cs,fixture);
 await run('C:/Program Files/dotnet/dotnet.exe',['C:/Program Files/dotnet/sdk/9.0.301/Roslyn/bincore/csc.dll','/nologo','/langversion:latest','/target:exe','/out:'+exe,...['mscorlib','System','System.Core'].map(n=>'/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/'+n+'.dll'),cs],{windowsHide:true,timeout:20000});
 process.stdout.write((await run(exe,[],{windowsHide:true,timeout:20000})).stdout);
}finally{
 if(process.env.REBIRTH_KEEP_FIXTURE==='1')process.stdout.write(temp+'\n');
 else{if(!resolve(temp).toLowerCase().startsWith((resolve(tmpdir())+sep).toLowerCase())||!basename(temp).startsWith('rebirth-grid-rejection-'))throw Error('Unsafe fixture cleanup');await rm(temp,{recursive:true});}
}
