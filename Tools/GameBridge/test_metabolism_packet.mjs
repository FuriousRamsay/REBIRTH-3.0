import {readFile,writeFile,mkdtemp,unlink,rmdir} from 'node:fs/promises';
import {execFile} from 'node:child_process';
import {promisify} from 'node:util';
import {tmpdir} from 'node:os';
import {join,resolve,dirname} from 'node:path';
import {fileURLToPath} from 'node:url';
const run=promisify(execFile);
const here=dirname(fileURLToPath(import.meta.url));
const root=resolve(here,'../..');
function member(source,marker){const a=source.indexOf(marker);if(a<0)throw new Error(marker);const begin=source.indexOf('{',a);let depth=0;for(let i=begin;i<source.length;i++){if(source[i]==='{')depth++;else if(source[i]==='}'&&--depth===0)return source.slice(a,i+1);}throw new Error('Unclosed '+marker);}
const model=await readFile(join(root,'Scripts/Metabolism/RebirthMetabolismModels.cs'),'utf8');
const source=await readFile(join(root,'Scripts/Network/RebirthMetabolismNetPackages.cs'),'utf8');
const codec=await readFile(join(root,'Scripts/Survivor/Network/RebirthSurvivorNetworkCodec.cs'),'utf8');
const helper='internal static class RebirthSurvivorNetworkCodec {'+['public static int EstimateString(','public static string ReadBoundedString(','private static int SevenBitEncodedIntLength(','public static string Clean(','public static void WriteString('].map(m=>member(codec,m)).join('\n')+'}';
const fixture=await readFile(join(here,'test_metabolism_packet_fixture.cs'),'utf8');
const temp=await mkdtemp(join(tmpdir(),'rebirth-owner-test-'));
try {
 const cs=join(temp,'check.cs'), exe=join(temp,'check.exe');
 await writeFile(cs,fixture.replace('// PRODUCTION_MODEL',member(model,'public struct RebirthMetabolismSnapshot')).replace('// PRODUCTION_CODEC',helper).replace('// PRODUCTION_PACKETS',member(source,'public class NetPackageRebirthMetabolismState')+'\n'+member(source,'public sealed class NetPackageRebirthMetabolismScopedState')));
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
