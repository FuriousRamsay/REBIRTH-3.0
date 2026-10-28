// AUTHORED_NOT_RUN. Execute only after all production/scenario coding is complete.
import {readFile,writeFile,mkdtemp,rm} from 'node:fs/promises';import {execFile} from 'node:child_process';import {promisify} from 'node:util';
import {tmpdir} from 'node:os';import {join,resolve,dirname,basename,sep} from 'node:path';import {fileURLToPath} from 'node:url';
const here=dirname(fileURLToPath(import.meta.url)),root=resolve(here,'../..'),run=promisify(execFile);
const source=await readFile(join(root,'Scripts/Crafting/Cooking/RebirthCookingRefundDropObservation.cs'),'utf8');
let fixture=await readFile(join(here,'mp042_refund_drop_fixture.cs'),'utf8');
if(fixture.split('// ACTUAL_DROP').length!==2)throw Error('Actual drop fixture slot changed');
fixture=fixture.replace('// ACTUAL_DROP',source.replace(/^using[^\n]*\n/gm,''));
const temp=await mkdtemp(join(tmpdir(),'rebirth-refund-drop-'));
try{
 const cs=join(temp,'fixture.cs'),exe=join(temp,'fixture.exe');await writeFile(cs,fixture);
 await run('C:/Program Files/dotnet/dotnet.exe',['C:/Program Files/dotnet/sdk/9.0.301/Roslyn/bincore/csc.dll','/nologo','/langversion:latest','/target:exe','/out:'+exe,...['mscorlib','System','System.Core'].map(n=>'/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/'+n+'.dll'),cs],{windowsHide:true,timeout:20000});
 process.stdout.write((await run(exe,[],{windowsHide:true,timeout:20000})).stdout);
}finally{
 if(process.env.REBIRTH_KEEP_FIXTURE==='1')process.stdout.write(temp+'\n');
 else{if(!resolve(temp).toLowerCase().startsWith((resolve(tmpdir())+sep).toLowerCase())||!basename(temp).startsWith('rebirth-refund-drop-'))throw Error('Unsafe fixture cleanup');await rm(temp,{recursive:true});}
}
