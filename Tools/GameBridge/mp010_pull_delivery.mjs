// AUTHORED_NOT_RUN. Invoke only after all production/scenario coding is complete.
import {readFile,writeFile,mkdtemp,rm} from 'node:fs/promises';import {execFile} from 'node:child_process';import {promisify} from 'node:util';
import {tmpdir} from 'node:os';import {join,resolve,dirname,basename,sep} from 'node:path';import {fileURLToPath} from 'node:url';
const run=promisify(execFile),here=dirname(fileURLToPath(import.meta.url)),root=resolve(here,'../..');
const source=await readFile(join(root,'Scripts/Crafting/Cooking/RebirthCookingPull.cs'),'utf8');
const first=source.indexOf('    private sealed class Original'),last=source.indexOf('    private static readonly Dictionary<long,Original> waiting',first);
const begin=source.indexOf('    internal static void CompleteGridOriginal('),end=source.indexOf('    internal static string EncodeCanonical(',begin);
if(first<0||last<=first||begin<0||end<=begin)throw Error('Original pull source extraction boundaries changed');
const delivery=await readFile(join(root,'Scripts/Crafting/Cooking/RebirthCookingPull.Delivery.cs'),'utf8');
const recovery=await readFile(join(root,'Scripts/Crafting/Cooking/RebirthCookingPull.RequestRecovery.cs'),'utf8');
const lifeStart=source.indexOf('    private static bool CurrentDeliverySession('),lifeEnd=source.indexOf('    private static void Send(',lifeStart);
if(lifeStart<0||lifeEnd<=lifeStart)throw Error('Delivery life scope extraction changed');
let fixture=await readFile(join(here,'mp010_pull_delivery_fixture.cs'),'utf8');
for(const [marker,text] of [['// ACTUAL_ORIGINAL',source.slice(first,last)],['// ACTUAL_DELIVERY_SESSION',source.slice(lifeStart,lifeEnd)],['// ACTUAL_COMPLETE',source.slice(begin,end)],['// ACTUAL_DELIVERY',delivery.replace(/^using[^\n]*\n/gm,'')]]){
 if(fixture.split(marker).length!==2)throw Error('Expected one actual source slot '+marker);fixture=fixture.replace(marker,text);
}
fixture+='\n'+recovery.replace(/^using[^\n]*\n/gm,'');
const temp=await mkdtemp(join(tmpdir(),'rebirth-pull-delivery-'));
try{
 const cs=join(temp,'fixture.cs'),exe=join(temp,'fixture.exe');await writeFile(cs,fixture);
 await run('C:/Program Files/dotnet/dotnet.exe',['C:/Program Files/dotnet/sdk/9.0.301/Roslyn/bincore/csc.dll','/nologo','/langversion:latest','/target:exe','/out:'+exe,...['mscorlib','System','System.Core','System.Xml','System.Xml.Linq'].map(n=>'/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/'+n+'.dll'),cs],{windowsHide:true,timeout:20000});
 process.stdout.write((await run(exe,[],{windowsHide:true,timeout:20000})).stdout);
}finally{
 if(process.env.REBIRTH_KEEP_FIXTURE==='1')process.stdout.write(temp+'\n');
 else{if(!resolve(temp).toLowerCase().startsWith((resolve(tmpdir())+sep).toLowerCase())||!basename(temp).startsWith('rebirth-pull-delivery-'))throw Error('Refuse cleanup outside generated fixture');await rm(temp,{recursive:true});}
}
