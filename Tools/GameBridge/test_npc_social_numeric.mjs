import {readFile,writeFile,mkdtemp,unlink,rmdir,readdir} from 'node:fs/promises';
import {execFile} from 'node:child_process';
import {promisify} from 'node:util';
import {tmpdir} from 'node:os';
import {join,resolve,dirname} from 'node:path';
import {fileURLToPath} from 'node:url';
const run=promisify(execFile),here=dirname(fileURLToPath(import.meta.url)),root=resolve(here,'../..');
const source=(await readFile(join(root,'Scripts/NPC/Persistence/RebirthNpcSocialPersistence.cs'),'utf8'))+'\n'+(await readFile(join(root,'Scripts/NPC/Foundation/RebirthNpcFoundation.cs'),'utf8'))+'\n'+(await readFile(join(root,'Scripts/NPC/Social/RebirthNpcSocialService.cs'),'utf8'));
function method(signature){const a=source.indexOf(signature);if(a<0)throw new Error(signature);let b=source.indexOf('{',a),depth=1,i=b+1;for(;depth&&i<source.length;i++){if(source[i]==='{')depth++;if(source[i]==='}')depth--;}return source.slice(a,i);}
const fileHelper=(await readFile(join(root,'Scripts/NPC/Persistence/RebirthNpcPersistenceFile.cs'),'utf8')).replace(/^using .*;\r?$/gm,'');
const fixture=(await readFile(join(here,'test_npc_social_numeric_fixture.cs'),'utf8')).replace('// METHODS',['private static bool ValidateIdentities(','private static bool Validate(','private static float PF(','private static long PL(','private static uint PU('].map(method).join('\n')).replace('// FILEHELPER',fileHelper+'\n'+method('public readonly struct RebirthNpcStableId')+'\n'+method('public enum RebirthNpcSocialEventKind'));
const temp=await mkdtemp(join(tmpdir(),'rebirth-snapshot-test-'));
try {const cs=join(temp,'check.cs'),exe=join(temp,'check.exe');await writeFile(cs,fixture);
 await run('C:/Program Files/dotnet/dotnet.exe',['C:/Program Files/dotnet/sdk/9.0.301/Roslyn/bincore/csc.dll','/nologo','/target:exe','/out:'+exe,...['mscorlib','System','System.Core','System.Xml'].map(x=>'/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/'+x+'.dll'),cs],{windowsHide:true,timeout:10000});console.log((await run(exe,[temp],{windowsHide:true,timeout:10000})).stdout.trim());
} finally {for(const n of await readdir(temp))await unlink(join(temp,n)).catch(e=>{if(e.code!=='ENOENT')throw e;});await rmdir(temp);}