import {readFile,writeFile,mkdtemp,rm} from 'node:fs/promises';
import {execFile} from 'node:child_process';
import {promisify} from 'node:util';
import {tmpdir} from 'node:os';
import {join,resolve,dirname} from 'node:path';
import {fileURLToPath} from 'node:url';
// PREPARED, NOT RUN. Compilation/execution requires the parent's test checkpoint.
const here=dirname(fileURLToPath(import.meta.url)),root=resolve(here,'../..');
const tools=await readFile(join(root,'Scripts/Crafting/Recycling/RebirthProductiveToolWork.cs'),'utf8');
const delivery=await readFile(join(root,'Scripts/Crafting/Cooking/RebirthCookingOutputDelivery.cs'),'utf8');
const fixture=await readFile(join(here,'mpA_productive_output_fixture.cs'),'utf8');
const start=tools.indexOf('internal static class RebirthProductiveToolWork');
const end=tools.indexOf('    internal static IEnumerable<CodeInstruction> Rewrite(');
const sparse=delivery.indexOf('    private static void Sparse('),saved=delivery.indexOf('    private static bool Saved(');
if(start<0||end<start||sparse<0||saved<sparse)throw new Error('Actual production extraction anchor changed');
const source='using System;\nusing System.Collections.Generic;\nusing System.Runtime.CompilerServices;\n'+tools.slice(start,end)+'}\n'+fixture.replace('/* ACTUAL_SPARSE_METHODS */',delivery.slice(sparse,saved));
const temp=await mkdtemp(join(tmpdir(),'rebirth-mpa-productive-')),run=promisify(execFile);
try{
 const cs=join(temp,'check.cs'),exe=join(temp,'check.exe');await writeFile(cs,source);
 await run('C:/Program Files/dotnet/dotnet.exe',['C:/Program Files/dotnet/sdk/9.0.301/Roslyn/bincore/csc.dll','/nologo','/target:exe','/out:'+exe,...['mscorlib','System','System.Core'].map(x=>'/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/'+x+'.dll'),cs],{windowsHide:true,timeout:10000});
 console.log((await run(exe,[],{windowsHide:true,timeout:10000})).stdout.trim());
}finally{await rm(temp,{recursive:true,force:true});}
