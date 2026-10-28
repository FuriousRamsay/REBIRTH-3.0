// AUTHORED_NOT_RUN. Execute only after the all-coding checkpoint.
import {readFile,writeFile,mkdtemp,rm} from 'node:fs/promises';import {execFile} from 'node:child_process';import {promisify} from 'node:util';
import {tmpdir} from 'node:os';import {join,resolve,dirname,basename,sep} from 'node:path';import {fileURLToPath} from 'node:url';
const here=dirname(fileURLToPath(import.meta.url)),root=resolve(here,'../..'),run=promisify(execFile);
const source=await readFile(join(root,'Scripts/Crafting/Cooking/RebirthCookingOwnerDelta.cs'),'utf8');
let fixture=await readFile(join(here,'mp042_owner_delta_life_fixture.cs'),'utf8');
for(const [marker,a,b] of [['// ACTUAL_PLAN','    internal sealed class Plan','    private sealed class Attempt'],['// ACTUAL_CODEC_SAVED','    private static string Cells(','    private static bool Current(']]){
 const begin=source.indexOf(a),end=source.indexOf(b,begin);if(begin<0||end<=begin||fixture.split(marker).length!==2)throw Error('Actual owner delta boundary changed '+marker);fixture=fixture.replace(marker,source.slice(begin,end));
}
const rejected=await readFile(join(root,'Scripts/Crafting/Cooking/RebirthCookingOwnerDelta.Rejected.cs'),'utf8');
fixture+='\n'+rejected.replace(/^using[^\n]*\n/gm,'')+'\n'+await readFile(join(here,'mp042_rejected_owner_receipt_fixture.cs'),'utf8');
const temp=await mkdtemp(join(tmpdir(),'rebirth-owner-delta-life-'));
try{
 const cs=join(temp,'fixture.cs'),exe=join(temp,'fixture.exe');await writeFile(cs,fixture);
 await run('C:/Program Files/dotnet/dotnet.exe',['C:/Program Files/dotnet/sdk/9.0.301/Roslyn/bincore/csc.dll','/nologo','/langversion:latest','/target:exe','/out:'+exe,...['mscorlib','System','System.Core','System.Xml','System.Xml.Linq'].map(n=>'/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/'+n+'.dll'),cs],{windowsHide:true,timeout:20000});
 process.stdout.write((await run(exe,[],{windowsHide:true,timeout:20000})).stdout);
}finally{
 if(process.env.REBIRTH_KEEP_FIXTURE==='1')process.stdout.write(temp+'\n');
 else{if(!resolve(temp).toLowerCase().startsWith((resolve(tmpdir())+sep).toLowerCase())||!basename(temp).startsWith('rebirth-owner-delta-life-'))throw Error('Unsafe fixture cleanup');await rm(temp,{recursive:true});}
}
