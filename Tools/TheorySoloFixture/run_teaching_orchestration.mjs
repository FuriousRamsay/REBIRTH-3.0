import {readFile,writeFile,mkdtemp,rm} from 'node:fs/promises';
import {execFile} from 'node:child_process';import {promisify} from 'node:util';import {tmpdir} from 'node:os';import {join} from 'node:path';
const run=promisify(execFile),NL=String.fromCharCode(10),withoutUsings=s=>s.split(NL).filter(row=>!row.startsWith('using ')).join(NL);
function method(s,signature){let a=s.indexOf(signature);if(a<0)throw Error(signature);let p=s.indexOf('{',a),d=1,b=p+1;for(;d&&b<s.length;b++){if(s[b]==='{')d++;if(s[b]==='}')d--;}return s.slice(a,b);}
const temp=await mkdtemp(join(tmpdir(),'rebirth-teaching-'));
try{
const service=await readFile('Scripts/Survivor/Progression/RebirthTeachingService.cs','utf8'),scope=await readFile('Scripts/Survivor/Network/RebirthSurvivorRequestScope.cs','utf8');
let fixture=(await readFile('Tools/TheorySoloFixture/TeachingOrchestrationFixture.cs','utf8')).replace('// SOURCE',method(service,'    private static bool TryApplyDurableOutcome(')+NL+method(service,'    private static bool MatchesLessonCharacters(')).replace('// SCOPE',withoutUsings(scope));
const paths=['Scripts/Survivor/Progression/RebirthTeachingOutcomeStore.cs',...['RebirthTheorySoloTeachingCompletion.cs','RebirthTheorySoloLockpickLedger.cs','RebirthTheorySoloCombatLedger.cs','RebirthTheorySoloTeachingRetirement.cs','RebirthTheorySoloState.cs'].map(x=>'Scripts/Survivor/Progression/TheorySolo/'+x)];
const source='using System;using System.IO;using System.Linq;using System.Xml.Linq;using System.Globalization;using System.Collections.Generic;'+NL+(await Promise.all(paths.map(p=>readFile(p,'utf8')))).map(withoutUsings).join(NL);
const a=join(temp,'source.cs'),b=join(temp,'fixture.cs'),exe=join(temp,'check.exe');await writeFile(a,source);await writeFile(b,fixture);
await run('C:/Program Files/dotnet/dotnet.exe',['C:/Program Files/dotnet/sdk/9.0.301/Roslyn/bincore/csc.dll','/nologo','/target:exe','/out:'+exe,...['mscorlib','System','System.Core','System.Xml','System.Xml.Linq'].map(x=>'/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/'+x+'.dll'),a,b],{windowsHide:true,timeout:10000});console.log((await run(exe,[temp],{windowsHide:true,timeout:10000})).stdout.trim());
}finally{await rm(temp,{recursive:true,force:true});}