// AUTHORED_NOT_RUN. Actual native owner loop and save-prefix members.
import {readFile,writeFile,mkdtemp,rm} from 'node:fs/promises';
import {execFile} from 'node:child_process';import {promisify} from 'node:util';
import {tmpdir} from 'node:os';import {join,resolve,dirname,basename,sep} from 'node:path';import {fileURLToPath} from 'node:url';
const here=dirname(fileURLToPath(import.meta.url)),root=resolve(here,'../..'),run=promisify(execFile);
const source=await readFile(join(root,'Scripts/NPC/Interaction/RebirthNpcTradeOwnerExchange.cs'),'utf8');
function member(signature){const start=source.indexOf(signature);if(start<0)throw Error('Missing actual member '+signature);let i=source.indexOf('{',start),depth=1;const expression=source.indexOf('=>',start);if(expression>=0&&expression<i)return source.slice(start,source.indexOf(';',expression)+1);for(i++;depth&&i<source.length;i++){if(source[i]==='{')depth++;if(source[i]==='}')depth--;}return source.slice(start,i);}
const signatures=['internal static bool TryApply(','internal static bool TryApplyLocal(','private static bool ApplyHeld(','internal static bool Consume(','private static bool Current(','private static bool Same(','private static ItemStack[][] Expected(','private static bool Equal(','private static bool Prefix(','private static bool SavePrefix('];
const fixture=(await readFile(join(here,'mp048_owner_loop_fixture.cs'),'utf8')).replace('// ACTUAL_OWNER_LOOP',signatures.map(member).join('\n'));
const temp=await mkdtemp(join(tmpdir(),'rebirth-trade-owner-loop-source-'));
try{
 const cs=join(temp,'fixture.cs'),exe=join(temp,'fixture.exe');await writeFile(cs,fixture);
 await run('C:/Program Files/dotnet/dotnet.exe',['C:/Program Files/dotnet/sdk/9.0.301/Roslyn/bincore/csc.dll','/nologo','/langversion:latest','/target:exe','/out:'+exe,...['mscorlib','System','System.Core'].map(n=>'/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/'+n+'.dll'),cs],{windowsHide:true,timeout:20000});
 process.stdout.write((await run(exe,[],{windowsHide:true,timeout:20000})).stdout);
}finally{
 if(!resolve(temp).toLowerCase().startsWith((resolve(tmpdir())+sep).toLowerCase())||!basename(temp).startsWith('rebirth-trade-owner-loop-source-'))throw Error('unsafe fixture cleanup');
 await rm(temp,{recursive:true});
}
