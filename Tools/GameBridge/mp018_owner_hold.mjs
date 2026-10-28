import {alignHydrationSourcePaths,alignHydrationFixture,writeHydrationFixtureConfig} from './mp018_existing_cohort_boundaries.mjs';
// AUTHORED_NOT_RUN. Execute only after all production/scenario coding completes.
import {readFile,writeFile,mkdtemp,rm} from 'node:fs/promises';import {execFile} from 'node:child_process';import {promisify} from 'node:util';
import {tmpdir} from 'node:os';import {join,resolve,dirname,basename,sep} from 'node:path';import {fileURLToPath} from 'node:url';
const here=dirname(fileURLToPath(import.meta.url)),root=resolve(here,'../..'),run=promisify(execFile);
const paths=['Scripts/Survivor/Domain/RebirthGearInventoryPlan.cs','Scripts/Survivor/Domain/RebirthGearInventoryPlanCodec.cs','Scripts/Survivor/Domain/RebirthGearEncodedSnapshot.cs','Scripts/Metabolism/RebirthHydrationTransferPlan.cs','Scripts/Metabolism/RebirthHydrationTransferOriginal.cs','Scripts/Survivor/Network/RebirthSurvivorRequestScope.cs','Scripts/Metabolism/RebirthHydrationOwnerHold.cs'];
alignHydrationSourcePaths(paths);
const sources=await Promise.all(paths.map(path=>readFile(join(root,path),'utf8')));
let fixture=await readFile(join(here,'mp018_owner_hold_fixture.cs'),'utf8');
fixture+='\n'+await readFile(join(here,'mp018_survivor_scope_adapter.cs'),'utf8');
if(fixture.split('// ACTUAL_PLAN_SOURCES').length!==2)throw Error('Actual hydration plan marker changed');
const imports='using System;using System.IO;using System.Collections.Generic;using System.Globalization;using System.Linq;using System.Xml.Linq;using Stack=RebirthGearInventoryPlan.Stack;';
const bodies=sources.map(source=>source.replace(/^using[^\r\n]+;\r?$/gm,'')).join(String.fromCharCode(10));
fixture=imports+String.fromCharCode(10)+fixture.replace('// ACTUAL_PLAN_SOURCES',bodies);
fixture=alignHydrationFixture(fixture);
const temp=await mkdtemp(join(tmpdir(),'rh-'));
try{
 const cs=join(temp,'fixture.cs'),exe=join(temp,'fixture.exe');await writeFile(cs,fixture);await writeHydrationFixtureConfig(exe);
 await run('C:/Program Files/dotnet/dotnet.exe',['C:/Program Files/dotnet/sdk/9.0.301/Roslyn/bincore/csc.dll','/nologo','/langversion:latest','/target:exe','/out:'+exe,...['mscorlib','System','System.Core','System.Xml','System.Xml.Linq'].map(n=>'/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/'+n+'.dll'),cs],{windowsHide:true,timeout:20000});
 process.stdout.write((await run(exe,[],{windowsHide:true,timeout:20000})).stdout);
}finally{
 if(process.env.REBIRTH_KEEP_FIXTURE==='1')process.stdout.write(temp+'\n');
 else{if(!resolve(temp).toLowerCase().startsWith((resolve(tmpdir())+sep).toLowerCase())||!basename(temp).startsWith('rh-'))throw Error('Unsafe fixture cleanup');await rm(temp,{recursive:true});}
}





