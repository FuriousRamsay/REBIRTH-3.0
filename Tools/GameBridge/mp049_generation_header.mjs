// AUTHORED_NOT_RUN. Native/subscribe adapters are explicit; production methods actual.
import {readFile,writeFile,mkdtemp,rm} from 'node:fs/promises';import {execFile} from 'node:child_process';import {promisify} from 'node:util';
import {tmpdir} from 'node:os';import {join,resolve,dirname,basename,sep} from 'node:path';import {fileURLToPath} from 'node:url';
const here=dirname(fileURLToPath(import.meta.url)),root=resolve(here,'../..'),run=promisify(execFile);
const domain=await readFile(join(root,'Scripts/NPC/Persistence/RebirthNpcSaveQuarantineDomain.cs'),'utf8');
const generation=await readFile(join(root,'Scripts/NPC/Persistence/RebirthNpcSaveQuarantineClientGeneration.cs'),'utf8');
const header=await readFile(join(root,'Scripts/NPC/Network/RebirthNpcBaselineV2.cs'),'utf8');
function slice(source,first,last){const begin=source.indexOf(first),end=source.indexOf(last,begin);if(begin<0||end<=begin)throw Error('Actual source boundary changed: '+first);return source.slice(begin,end);}
const domainBody=slice(domain,'internal sealed class RebirthNpcSaveQuarantineDomain','// This is an original-scope read witness only.');
const generationBegin=generation.indexOf('    internal static RebirthNpcSaveQuarantineDomain PrepareClientGenerationQuarantine(');if(generationBegin<0)throw Error('Generation adapter missing');
const headerStart=header.indexOf('internal static class RebirthNpcBaselineClientHeader');if(headerStart<0)throw Error('Header class missing');const ownHeader=header.slice(headerStart);
let fixture=await readFile(join(here,'mp049_generation_header_fixture.cs'),'utf8');
for(const [marker,value] of [
 ['// ACTUAL_DOMAIN',domainBody],
 ['// ACTUAL_SCOPE_DIRECTORY_MEMBERS',slice(domain,'    private static StringComparison DirectoryComparison','    private RebirthNpcSaveQuarantineScope(')],
 ['// ACTUAL_GENERATION_MEMBERS',generation.slice(generationBegin,generation.lastIndexOf('}'))],
 ['// ACTUAL_HEADER_FIELDS',slice(ownHeader,'    private sealed class Header','    private static bool NativeCurrent(')],
 ['// ACTUAL_HEADER_ADAPTER',slice(ownHeader,'    internal static RebirthNpcSaveQuarantineDomain PrepareClientHeaderQuarantine(','    internal static bool HeaderCompatible(')]
]){if(fixture.split(marker).length!==2)throw Error('Fixture marker changed');fixture=fixture.replace(marker,value);}
const temp=await mkdtemp(join(tmpdir(),'rebirth-generation-header-'));
try{
 const cs=join(temp,'fixture.cs'),exe=join(temp,'fixture.exe');await writeFile(cs,fixture);
 await run('C:/Program Files/dotnet/dotnet.exe',['C:/Program Files/dotnet/sdk/9.0.301/Roslyn/bincore/csc.dll','/nologo','/langversion:latest','/target:exe','/out:'+exe,...['mscorlib','System','System.Core','System.Xml'].map(n=>'/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/'+n+'.dll'),cs],{windowsHide:true,timeout:20000});
 process.stdout.write((await run(exe,[],{windowsHide:true,timeout:20000})).stdout);
}finally{
 if(!resolve(temp).toLowerCase().startsWith((resolve(tmpdir())+sep).toLowerCase())||!basename(temp).startsWith('rebirth-generation-header-'))throw Error('Unsafe fixture cleanup');await rm(temp,{recursive:true});
}
