import {readFile,writeFile,mkdtemp,unlink,rmdir} from 'node:fs/promises';
import {execFile} from 'node:child_process';
import {promisify} from 'node:util';
import {tmpdir} from 'node:os';
import {join,resolve,dirname} from 'node:path';
import {fileURLToPath} from 'node:url';
const run=promisify(execFile),here=dirname(fileURLToPath(import.meta.url)),root=resolve(here,'../..');
const paths = [
 'Progression/ItemActionListenAudiobookRebirth.cs',
 'Progression/ItemActionPlayMusicCassetteRebirth.cs',
 'Progression/ItemActionStudyLiteratureRebirth.cs',
 'Progression/AdvancedDisciplines/ItemActionUseRageCapsuleRebirth.cs',
 'Support/ItemActionEquipSurvivorGearRebirth.cs',
 'Support/ItemActionUseTraitSupportRebirth.cs'];
let classes='';
for(let n=0;n<paths.length;n++){
 const source=await readFile(join(root,'Scripts/Survivor',paths[n]),'utf8');
 const a=source.indexOf('public override void StopHolding('); if(a<0)throw new Error(paths[n]);
 let b=source.indexOf('{',a),depth=1,i=b+1;
 for(;depth&&i<source.length;i++){if(source[i]==='{')depth++;if(source[i]==='}')depth--;}
 classes+='class Action'+n+':ItemActionEat {'+source.slice(a,i)+'}\n';
}
const fixture=`using System;
class ItemActionData {public Inventory invData;}
class Inventory {public object holdingEntity;}
class ItemActionEat {
 public static int Calls;
 public class MyInventoryData:ItemActionData {public bool bEatingStarted;}
 public virtual void StopHolding(ItemActionData data) {
  if(!(data is MyInventoryData)||data.invData==null||data.invData.holdingEntity==null)throw new Exception("Unsafe native cleanup");
  Calls++;
 }
}
${classes}
class Check {
 static void Main(){
  ItemActionEat[] actions={${paths.map((_,i)=>'new Action'+i+'()').join(',')}};
  foreach(var action in actions){
   ItemActionEat.Calls=0;
   action.StopHolding(null);action.StopHolding(new ItemActionData());
   var data=new ItemActionEat.MyInventoryData {bEatingStarted=true};
   action.StopHolding(data);if(data.bEatingStarted)throw new Exception("Pending detached use");
   data.invData=new Inventory();data.bEatingStarted=true;
   action.StopHolding(data);if(data.bEatingStarted||ItemActionEat.Calls!=0)throw new Exception("Missing holder");
   data.invData.holdingEntity=new object();data.bEatingStarted=true;
   action.StopHolding(data);if(data.bEatingStarted||ItemActionEat.Calls!=1)throw new Exception("Valid cleanup skipped");
  }
  Console.WriteLine("PASS: six actual StopHolding methods reject invalid native cleanup, cancel pending use and preserve valid cleanup. Stub native base only.");
 }
}`;
const temp=await mkdtemp(join(tmpdir(),'rebirth-snapshot-test-'));
try {const cs=join(temp,'check.cs'),exe=join(temp,'check.exe');await writeFile(cs,fixture);
 await run('C:/Program Files/dotnet/dotnet.exe',['C:/Program Files/dotnet/sdk/9.0.301/Roslyn/bincore/csc.dll','/nologo','/target:exe','/out:'+exe,...['mscorlib','System','System.Core'].map(x=>'/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/'+x+'.dll'),cs],{windowsHide:true,timeout:10000});console.log((await run(exe,[],{windowsHide:true,timeout:10000})).stdout.trim());
} finally {for(const n of ['check.cs','check.exe'])await unlink(join(temp,n)).catch(e=>{if(e.code!=='ENOENT')throw e;});await rmdir(temp);}