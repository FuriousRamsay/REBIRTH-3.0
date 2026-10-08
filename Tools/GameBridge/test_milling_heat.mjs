import {readFile,writeFile,mkdtemp,unlink,rmdir} from 'node:fs/promises';
import {execFile} from 'node:child_process';
import {promisify} from 'node:util';
import {tmpdir} from 'node:os';
import {join,resolve,dirname} from 'node:path';
import {fileURLToPath} from 'node:url';
const run=promisify(execFile);
const here=dirname(fileURLToPath(import.meta.url));
const root=resolve(here,'../..');
const batch=await readFile(join(root,'Scripts/Crafting/Cooking/RebirthCookingBatch.cs'),'utf8');
const rules=await readFile(join(root,'Scripts/Crafting/Cooking/RebirthCookingHeatRules.cs'),'utf8');
function section(source,start,end){const a=source.indexOf(start),b=source.indexOf(end,a);if(a<0||b<=a)throw new Error('Production method missing');return source.slice(a,b);}
const heat=section(batch,'    public static bool NeedsHeat(','    private static void ColdStationActive(');
const advance=section(rules,'    public static void Advance(','    public static string Method(');
const fixture=await readFile(join(here,'test_milling_heat_fixture.cs'),'utf8');
const temp=await mkdtemp(join(tmpdir(),'rebirth-owner-test-'));
try {
 const cs=join(temp,'check.cs'), exe=join(temp,'check.exe');
 await writeFile(cs,fixture.replace('// HEAT_METHOD',heat).replace('// ADVANCE_METHOD',advance));
 await run('C:/Program Files/dotnet/dotnet.exe',[
 'C:/Program Files/dotnet/sdk/9.0.301/Roslyn/bincore/csc.dll','/nologo','/target:exe','/out:'+exe,
 '/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/mscorlib.dll',
 '/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/System.dll',
 '/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/System.Core.dll',cs],{windowsHide:true,timeout:10000});
 const result=await run(exe,[],{windowsHide:true,timeout:10000});
 console.log(result.stdout.trim());
} finally {
 for(const name of ['check.cs','check.exe']) await unlink(join(temp,name)).catch(e=>{if(e.code!=='ENOENT')throw e;});
 await rmdir(temp);
}
