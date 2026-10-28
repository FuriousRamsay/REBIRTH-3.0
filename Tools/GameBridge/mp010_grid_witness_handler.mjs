// AUTHORED_NOT_RUN. Execute only after all production/scenario coding is complete.
import {readFile,writeFile,mkdtemp,rm} from 'node:fs/promises';import {execFile} from 'node:child_process';import {promisify} from 'node:util';
import {tmpdir} from 'node:os';import {join,resolve,dirname,basename,sep} from 'node:path';import {fileURLToPath} from 'node:url';
const here=dirname(fileURLToPath(import.meta.url)),root=resolve(here,'../..'),run=promisify(execFile);
const source=await readFile(join(root,'Scripts/Crafting/Cooking/RebirthCookingGridCustody.cs'),'utf8');
const type=source.indexOf('public sealed class NetPackageRebirthCookingGridOwnerWitness:NetPackage');
const begin=source.indexOf('    public override void ProcessPackage(',type),end=source.indexOf('    public int GetLength()',begin);
const resultType=source.indexOf('public sealed class NetPackageRebirthCookingGridOwnerWitnessResult:NetPackage');
const resultBegin=source.indexOf('    public override void ProcessPackage(',resultType),resultEnd=source.indexOf('    public int GetLength()',resultBegin);
const adapters=await readFile(join(here,'mp010_no_payment_handler_fixture.cs'),'utf8'),boundary=adapters.indexOf('internal sealed class ActualHandler:NetPackage');
let fixture=await readFile(join(here,'mp010_grid_witness_handler_fixture.cs'),'utf8');
if(type<0||begin<type||end<=begin||boundary<0||fixture.split('// ACTUAL_HANDLER').length!==2)throw Error('Actual witness/adapters boundary changed');
if(resultType<0||resultBegin<resultType||resultEnd<=resultBegin||fixture.split('// ACTUAL_RESULT_HANDLER').length!==2)throw Error('Actual witness result boundary changed');
fixture=fixture.replace('// ACTUAL_RESULT_HANDLER',source.slice(resultBegin,resultEnd));
fixture=adapters.slice(0,boundary)+fixture.replace('// ACTUAL_HANDLER',source.slice(begin,end));
const temp=await mkdtemp(join(tmpdir(),'rebirth-grid-witness-handler-'));
try{
 const cs=join(temp,'fixture.cs'),exe=join(temp,'fixture.exe');await writeFile(cs,fixture);
 await run('C:/Program Files/dotnet/dotnet.exe',['C:/Program Files/dotnet/sdk/9.0.301/Roslyn/bincore/csc.dll','/nologo','/langversion:latest','/target:exe','/out:'+exe,...['mscorlib','System','System.Core'].map(n=>'/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/'+n+'.dll'),cs],{windowsHide:true,timeout:20000});
 process.stdout.write((await run(exe,[],{windowsHide:true,timeout:20000})).stdout);
}finally{
 if(process.env.REBIRTH_KEEP_FIXTURE==='1')process.stdout.write(temp+'\n');
 else{if(!resolve(temp).toLowerCase().startsWith((resolve(tmpdir())+sep).toLowerCase())||!basename(temp).startsWith('rebirth-grid-witness-handler-'))throw Error('Unsafe fixture cleanup');await rm(temp,{recursive:true});}
}
