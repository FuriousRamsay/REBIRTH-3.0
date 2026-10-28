// Deferred actual-store/decoder fixture. No execution during coding.
import {readFile,writeFile,mkdtemp,rm,readdir} from 'node:fs/promises';import {execFile} from 'node:child_process';import {promisify} from 'node:util';
import {tmpdir} from 'node:os';import {join,resolve,dirname} from 'node:path';import {fileURLToPath} from 'node:url';
const run=promisify(execFile),here=dirname(fileURLToPath(import.meta.url)),root=resolve(here,'../..');
function body(s,signature){const a=s.indexOf(signature);if(a<0)throw Error(signature);let b=s.indexOf('{',a),d=1,i=b+1;for(;d&&i<s.length;i++){if(s[i]==='{')d++;else if(s[i]==='}')d--;}if(d)throw Error(signature);return s.slice(a,i);}
const read=p=>readFile(join(root,p),'utf8');let fixture=await read('Tools/GameBridge/mpB_npc_domain_stores_fixture.cs');
const social=await read('Scripts/NPC/Social/RebirthNpcSocialService.cs'),advanced=await read('Scripts/NPC/Progression/RebirthNpcAdvancedProgression.cs'),domain=await read('Scripts/NPC/Progression/RebirthNpcProgressionDomain.cs');
const types=[body(await read('Scripts/NPC/Foundation/RebirthNpcFoundation.cs'),'public readonly struct RebirthNpcStableId'),body(await read('Scripts/NPC/Progression/RebirthNpcProfessionProgression.cs'),'public enum RebirthNpcProfession'),
 body(social,'public enum RebirthNpcSocialEventKind'),body(social,'public sealed class RebirthNpcRelationshipSnapshot'),body(social,'public sealed class RebirthNpcSocialMemorySnapshot'),
 body(domain,'public sealed class RebirthNpcProgressionEntryRecord'),body(domain,'public sealed class RebirthNpcProgressionRecord'),body(advanced,'public sealed class RebirthNpcAdvancedProgressionRecord'),body(advanced,'public sealed class RebirthNpcMentorshipSession')];
fixture=fixture.replace('// PRODUCTION_TYPES',types.join('\n'));
const stores=[body(await read('Scripts/NPC/Persistence/RebirthNpcSocialPersistence.cs'),'public static partial class RebirthNpcSocialPersistenceStore'),body(await read('Scripts/NPC/Progression/RebirthNpcProgressionPersistence.cs'),'public static partial class RebirthNpcProgressionPersistenceStore'),body(advanced,'public static partial class RebirthNpcAdvancedProgressionPersistenceStore'),body(await read('Scripts/NPC/Economy/RebirthNpcEconomyLogisticsClosure.cs'),'public static partial class RebirthNpcEconomyPersistenceStore')];
fixture=fixture.replace('// PRODUCTION_STORES',stores.join('\n'));
fixture=fixture.replace('// PRODUCTION_HELPERS',[
 body(await read('Scripts/NPC/Persistence/RebirthNpcPersistenceFile.cs'),'public static partial class RebirthNpcPersistenceFile'),
 body(await read('Scripts/Survivor/Persistence/RebirthAtomicXmlFile.cs'),'public static class RebirthAtomicXmlFile'),body(await read('Scripts/Persistence/RebirthDurableFileCommit.cs'),'public static class RebirthDurableFileCommit')].join('\n'));
// Preserve the current original-save retirement and prepared recovery proofs.
// These are actual helpers, not positive retirement or world-scope doubles.
for(const path of ['Scripts/NPC/Persistence/RebirthNpcOriginalSaveBinding.cs','Scripts/NPC/Persistence/RebirthNpcPreparedRecovery.cs','Scripts/NPC/Persistence/RebirthNpcSaveScope.cs','Scripts/NPC/Persistence/RebirthNpcRecoveryGeneration.cs','Scripts/NPC/Persistence/RebirthNpcSocialPersistence.Trade.cs'])
 fixture+='\n'+(await read(path)).replace(/^using [^;]+;\r?\n/gm,'');
fixture+='\ninternal static class RebirthNpcRecoveryCurrentBacking {'+body(await read('Scripts/NPC/Persistence/RebirthNpcRecoveryCurrentBacking.cs'),'internal static XmlDocument Read(')+'}';
fixture=fixture.replace('// ACTUAL_REVISION_RETIREMENT',body(await read('Scripts/NPC/Persistence/RebirthNpcSaveQuarantineProgressionViews.cs'),'internal static bool CanRetireEmptyRevisions'));
const installedProgram=await read('Tools/GameBridge/mpB_npc_installed_publication_program.cs');
fixture=fixture.replace('// ACTUAL_INSTALLED_PUBLICATION_PROGRAM',installedProgram);
fixture+='\n#if ACTUAL_PUBLICATIONS\n'+(await read('Scripts/NPC/Persistence/RebirthNpcRecoveryPublications.cs')).replace(/^using [^;]+;\r?\n/gm,'')+'\n#endif\n';
const temp=await mkdtemp(join(tmpdir(),'rebirth-mpB-npc-'));
try{const sdk=process.env.MPB_DOTNET_SDK_ROOT||'C:/Program Files/dotnet/sdk';const versions=(await readdir(sdk)).filter(x=>/^\d+\.\d+\.\d+$/.test(x)).sort((a,b)=>a.localeCompare(b,undefined,{numeric:true}));if(!versions.length)throw Error('Roslyn SDK missing');const cs=join(temp,'fixture.cs'),exe=join(temp,'fixture.exe');await writeFile(cs,fixture);
 await run('dotnet',[join(sdk,versions.at(-1),'Roslyn/bincore/csc.dll'),'/nologo','/target:exe','/out:'+exe,...['mscorlib','System','System.Core','System.Xml','System.Xml.Linq'].map(x=>'/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/'+x+'.dll'),cs],{windowsHide:true,timeout:30000});console.log((await run(exe,[temp],{windowsHide:true,timeout:30000})).stdout.trim());
 const installedExe=join(temp,'installed-publication.exe');
 await run('dotnet',[join(sdk,versions.at(-1),'Roslyn/bincore/csc.dll'),'/nologo','/define:ACTUAL_PUBLICATIONS','/target:exe','/out:'+installedExe,...['mscorlib','System','System.Core','System.Xml','System.Xml.Linq'].map(x=>'/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/'+x+'.dll'),cs],{windowsHide:true,timeout:30000});
 console.log((await run(installedExe,[temp],{windowsHide:true,timeout:30000})).stdout.trim());
}finally{await rm(temp,{recursive:true,force:true});}
