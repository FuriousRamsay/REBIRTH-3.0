// AUTHORED_NOT_RUN. Invoke only after the user's all-coding checkpoint.
import {readFile,writeFile,mkdtemp,rm} from 'node:fs/promises';
import {execFile} from 'node:child_process';
import {promisify} from 'node:util';
import {tmpdir} from 'node:os';
import {join,resolve,dirname,basename,sep} from 'node:path';
import {fileURLToPath} from 'node:url';
const run=promisify(execFile),here=dirname(fileURLToPath(import.meta.url)),root=resolve(here,'../..');
let fixture=await readFile(join(here,'mp040_normal_entry_fixture.cs'),'utf8');
const actual=await readFile(join(root,'Scripts/Survivor/Progression/RebirthInfrastructureWorkService.cs'),'utf8');
const facadeStart=actual.indexOf('    public static bool TryService('),facadeEnd=actual.indexOf('    private static bool PrepareOwnedService(',facadeStart);
const actionStart=actual.indexOf('public sealed class ItemActionElectricalServiceRebirth '),packageStart=actual.indexOf('public sealed class NetPackageRebirthElectricalServiceRequest',actionStart),actionEnd=actual.lastIndexOf('[Preserve]',packageStart);
if(facadeStart<0||facadeEnd<=facadeStart||actionStart<0||actionEnd<=actionStart)throw Error('Normal electrical entry extraction boundaries changed');
for(const [marker,source] of [['// ACTUAL_SERVICE_FACADE',actual.slice(facadeStart,facadeEnd)],['// ACTUAL_NATIVE_ENTRY',actual.slice(actionStart,actionEnd)]]){
 if(fixture.split(marker).length!==2)throw Error('Expected one extraction slot: '+marker);
 fixture=fixture.replace(marker,source);
}
const temp=await mkdtemp(join(tmpdir(),'rebirth-normal-electrical-'));
try{
 const cs=join(temp,'fixture.cs'),exe=join(temp,'fixture.exe');await writeFile(cs,fixture);
 await run('C:/Program Files/dotnet/dotnet.exe',['C:/Program Files/dotnet/sdk/9.0.301/Roslyn/bincore/csc.dll','/nologo','/langversion:latest','/target:exe','/out:'+exe,...['mscorlib','System','System.Core','System.Xml','System.Xml.Linq'].map(n=>'/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/'+n+'.dll'),cs],{windowsHide:true,timeout:20000});
 process.stdout.write((await run(exe,[],{windowsHide:true,timeout:20000})).stdout);
}finally{
 if(process.env.REBIRTH_KEEP_FIXTURE==='1')process.stdout.write(temp+'\n');
 else{if(!resolve(temp).toLowerCase().startsWith((resolve(tmpdir())+sep).toLowerCase())||!basename(temp).startsWith('rebirth-normal-electrical-'))throw Error('Refuse cleanup outside generated fixture');await rm(temp,{recursive:true});}
}

