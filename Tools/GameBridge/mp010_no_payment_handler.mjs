// AUTHORED_NOT_RUN. Execute only after production and scenario coding completes.
import {readFile,writeFile,mkdtemp,rm} from 'node:fs/promises';
import {execFile} from 'node:child_process';import {promisify} from 'node:util';
import {tmpdir} from 'node:os';import {join,resolve,dirname,basename,sep} from 'node:path';import {fileURLToPath} from 'node:url';
const here=dirname(fileURLToPath(import.meta.url)),root=resolve(here,'../..'),run=promisify(execFile);
const source=await readFile(join(root,'Scripts/Crafting/Cooking/RebirthCookingPull.cs'),'utf8');
const type=source.indexOf('public sealed class NetPackageRebirthCookingPullNoPayment:NetPackage');
const begin=source.indexOf('    public override void ProcessPackage(',type),end=source.indexOf('    public int GetLength()',begin);
let fixture=await readFile(join(here,'mp010_no_payment_handler_fixture.cs'),'utf8');
if(type<0||begin<type||end<=begin||fixture.split('// ACTUAL_HANDLER').length!==2)throw Error('Actual handler extraction boundary changed');
fixture=fixture.replace('// ACTUAL_HANDLER',source.slice(begin,end));
const temp=await mkdtemp(join(tmpdir(),'rebirth-no-payment-handler-'));
try{
 const cs=join(temp,'fixture.cs'),exe=join(temp,'fixture.exe');await writeFile(cs,fixture);
 await run('C:/Program Files/dotnet/dotnet.exe',['C:/Program Files/dotnet/sdk/9.0.301/Roslyn/bincore/csc.dll','/nologo','/langversion:latest','/target:exe','/out:'+exe,...['mscorlib','System','System.Core'].map(n=>'/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/'+n+'.dll'),cs],{windowsHide:true,timeout:20000});
 process.stdout.write((await run(exe,[],{windowsHide:true,timeout:20000})).stdout);
}finally{
 if(process.env.REBIRTH_KEEP_FIXTURE==='1')process.stdout.write(temp+'\n');
 else{if(!resolve(temp).toLowerCase().startsWith((resolve(tmpdir())+sep).toLowerCase())||!basename(temp).startsWith('rebirth-no-payment-handler-'))throw Error('Unsafe fixture cleanup');await rm(temp,{recursive:true});}
}
