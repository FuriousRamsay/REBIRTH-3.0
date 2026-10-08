import {readFile,writeFile,mkdtemp,unlink,rmdir} from 'node:fs/promises';
import {execFile} from 'node:child_process';
import {promisify} from 'node:util';
import {tmpdir} from 'node:os';
import {join,resolve,dirname} from 'node:path';
import {fileURLToPath} from 'node:url';
const run=promisify(execFile),here=dirname(fileURLToPath(import.meta.url)),root=resolve(here,'../..');
const targets=[['Backgrounds/RebirthCombatPatrolTrackingSignatureService.cs','IsServer'],['Progression/AdvancedDisciplines/RebirthWildAffinityService.cs','IsAuthoritative']];
let classes='';
for(let n=0;n<targets.length;n++){
 const [path,name]=targets[n],source=await readFile(join(root,'Scripts/Survivor',path),'utf8');
 const a=source.indexOf('private static bool '+name+'(');if(a<0)throw new Error(path);
 let b=source.indexOf('{',a),depth=1,i=b+1;for(;depth&&i<source.length;i++){if(source[i]==='{')depth++;if(source[i]==='}')depth--;}
 classes+='class Check'+n+' {'+source.slice(a,i)+' public static bool Run(){return '+name+'();}}\n';
}
const fixture=`using System;
class World { public bool Remote; public bool IsRemote(){return Remote;} }
class GameManager { public static GameManager Instance; public World World; }
class ConnectionManager {public bool IsServer;}
class SingletonMonoBehaviour<T> {public static T Instance;}
class RebirthSurvivorMode {public static bool Enabled=true;public static bool IsEnabledForCurrentWorld(){return Enabled;}}
${classes}
class Program {
 static void Main(){
  int count=0;
  for(int w=0;w<3;w++)for(int c=0;c<3;c++){
   GameManager.Instance=new GameManager{World=w==0?null:new World{Remote=w==2}};
   SingletonMonoBehaviour<ConnectionManager>.Instance=c==0?null:new ConnectionManager{IsServer=c==1};
   bool expected=w==1&&c==1;
   if(Check0.Run()!=expected||Check1.Run()!=expected)throw new Exception("Authority mismatch "+w+","+c);
   count++;
  }
  GameManager.Instance=null;if(Check0.Run()||Check1.Run())throw new Exception("Missing manager");
  GameManager.Instance=new GameManager{World=new World()};SingletonMonoBehaviour<ConnectionManager>.Instance=new ConnectionManager{IsServer=true};
  RebirthSurvivorMode.Enabled=false;if(Check0.Run())throw new Exception("Disabled mode");
  Console.WriteLine("PASS: both actual authority methods across nine world/connection combinations; absent manager and tracking mode guard. Controlled stubs only.");
 }
}`;
const temp=await mkdtemp(join(tmpdir(),'rebirth-snapshot-test-'));
try {const cs=join(temp,'check.cs'),exe=join(temp,'check.exe');await writeFile(cs,fixture);
 await run('C:/Program Files/dotnet/dotnet.exe',['C:/Program Files/dotnet/sdk/9.0.301/Roslyn/bincore/csc.dll','/nologo','/target:exe','/out:'+exe,...['mscorlib','System','System.Core'].map(x=>'/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/'+x+'.dll'),cs],{windowsHide:true,timeout:10000});console.log((await run(exe,[],{windowsHide:true,timeout:10000})).stdout.trim());
} finally {for(const n of ['check.cs','check.exe'])await unlink(join(temp,n)).catch(e=>{if(e.code!=='ENOENT')throw e;});await rmdir(temp);}