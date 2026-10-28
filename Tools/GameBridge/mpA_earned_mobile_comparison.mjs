// PREPARED NOT_RUN. Extracts the actual production scoped readback method.
// Run only when compilation/fixtures are authorized; read HAV3N first.
import {readFile,writeFile,mkdtemp,rm} from 'node:fs/promises';
import {execFile} from 'node:child_process';import {promisify} from 'node:util';
import {tmpdir} from 'node:os';import {join,resolve,dirname,basename,sep} from 'node:path';import {fileURLToPath} from 'node:url';
const run=promisify(execFile),here=dirname(fileURLToPath(import.meta.url)),root=resolve(here,'../..');
const source=await readFile(join(root,'Scripts/Vehicles/Camper/RebirthCamperStationSaveEvidence.cs'),'utf8');
const marker='internal bool ReadStationSnapshot(Func<RebirthCamperStationState,bool> predicate)',at=source.indexOf(marker);
if(at<0||source.indexOf(marker,at+marker.length)>=0)throw Error('Missing/ambiguous actual comparison');
let depth=0,end=-1;for(let i=source.indexOf('{',at);i<source.length;i++){if(source[i]==='{')depth++;if(source[i]==='}'&&--depth===0){end=i+1;break;}}
if(end<0)throw Error('Unterminated comparison');
let fixture=await readFile(join(here,'mpA_earned_mobile_comparison_fixture.cs'),'utf8');
if(fixture.split('// ACTUAL_COMPARISON').length!==2)throw Error('Fixture insertion mismatch');
fixture=fixture.replace('// ACTUAL_COMPARISON',source.slice(at,end));
const temp=await mkdtemp(join(tmpdir(),'rebirth-earned-mobile-comparison-'));
try{
    const cs=join(temp,'fixture.cs'),exe=join(temp,'fixture.exe');await writeFile(cs,fixture);
    await run('C:/Program Files/dotnet/dotnet.exe',['C:/Program Files/dotnet/sdk/9.0.301/Roslyn/bincore/csc.dll','/nologo','/langversion:latest','/target:exe','/out:'+exe,...['mscorlib','System','System.Core'].map(n=>'/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/'+n+'.dll'),cs],{windowsHide:true,timeout:20000});
    console.log((await run(exe,[],{windowsHide:true,timeout:20000})).stdout.trim());
}
finally{
    if(process.env.REBIRTH_KEEP_FIXTURE==='1')console.log(temp);
    else{if(!resolve(temp).toLowerCase().startsWith((resolve(tmpdir())+sep).toLowerCase())||!basename(temp).startsWith('rebirth-earned-mobile-comparison-'))throw Error('Refuse cleanup outside generated temp');await rm(temp,{recursive:true});}
}
