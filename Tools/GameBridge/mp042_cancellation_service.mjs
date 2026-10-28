// AUTHORED_NOT_RUN. Execute only after all production/scenario coding completes.
import {readFile,writeFile,mkdtemp,rm} from 'node:fs/promises';import {execFile} from 'node:child_process';import {promisify} from 'node:util';
import {tmpdir} from 'node:os';import {join,resolve,dirname,basename,sep} from 'node:path';import {fileURLToPath} from 'node:url';
const here=dirname(fileURLToPath(import.meta.url)),root=resolve(here,'../..'),run=promisify(execFile);
const source=await readFile(join(root,'Scripts/Crafting/Cooking/RebirthCookingRemoteCancellation.cs'),'utf8');
let fixture=await readFile(join(here,'mp042_cancellation_service_fixture.cs'),'utf8');
const token=source.indexOf('    private static bool Token('),tokenEnd=source.indexOf('    internal static bool TryCancel(',token),begin=source.indexOf('    internal static ItemStack[] Refunds('),end=source.lastIndexOf('}');
if(token<0||tokenEnd<=token||begin<0||end<=begin)throw Error('Complete cancellation source boundary changed');
for(const [marker,text] of [['// ACTUAL_TOKEN',source.slice(token,tokenEnd)],['// ACTUAL_REFUNDS_HANDLE',source.slice(begin,end)]]){
 if(fixture.split(marker).length!==2)throw Error('Fixture slot changed '+marker);fixture=fixture.replace(marker,text);
}
const temp=await mkdtemp(join(tmpdir(),'rebirth-cancellation-service-'));
const legacy=await readFile(join(root,'Scripts/Crafting/Cooking/RebirthCookingRemoteCancellation.Legacy.cs'),'utf8');fixture+='\n'+legacy.replace(/^using .*;\r?\n/gm,'');
try{
 const cs=join(temp,'fixture.cs'),exe=join(temp,'fixture.exe');await writeFile(cs,fixture);
 await run('C:/Program Files/dotnet/dotnet.exe',['C:/Program Files/dotnet/sdk/9.0.301/Roslyn/bincore/csc.dll','/nologo','/langversion:latest','/target:exe','/out:'+exe,...['mscorlib','System','System.Core','System.Xml','System.Xml.Linq'].map(n=>'/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/'+n+'.dll'),cs],{windowsHide:true,timeout:20000});
 process.stdout.write((await run(exe,[],{windowsHide:true,timeout:20000})).stdout);
}finally{
 if(process.env.REBIRTH_KEEP_FIXTURE==='1')process.stdout.write(temp+'\n');
 else{if(!resolve(temp).toLowerCase().startsWith((resolve(tmpdir())+sep).toLowerCase())||!basename(temp).startsWith('rebirth-cancellation-service-'))throw Error('Unsafe fixture cleanup');await rm(temp,{recursive:true});}
}
