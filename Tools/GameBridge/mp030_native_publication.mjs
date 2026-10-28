// AUTHORED_NOT_RUN. Actual vehicle native metadata/rollback methods.
import {readFile,writeFile,mkdtemp,rm} from 'node:fs/promises';
import {execFile} from 'node:child_process';import {promisify} from 'node:util';
import {tmpdir} from 'node:os';import {join,resolve,dirname,basename,sep} from 'node:path';import {fileURLToPath} from 'node:url';
const here=dirname(fileURLToPath(import.meta.url)),root=resolve(here,'../..'),run=promisify(execFile);
const source=await readFile(join(root,'Scripts/Vehicles/Restoration/RebirthVehicleRestorationSystem.cs'),'utf8');
function method(key){const start=source.indexOf(key);if(start<0)throw Error('Missing actual member '+key);let cursor=source.indexOf('{',start)+1,depth=1;for(;depth&&cursor<source.length;cursor++){if(source[cursor]==='{')depth++;else if(source[cursor]==='}')depth--;}if(depth)throw Error('Member boundary '+key);return source.slice(start,cursor);}
let fixture=await readFile(join(here,'mp030_native_publication_fixture.cs'),'utf8');
for(const [marker,keys] of [
 ['// ACTUAL_NATIVE_METADATA',['public static bool WriteAssemblyToVehicleItem(','public static bool TryReadAssemblyFromVehicleItem(']],
 ['// ACTUAL_INDEX',['public static void RegisterEntity(EntityVehicle entity, RebirthVehicleAssembly assembly)','public static bool TryGetEntity(EntityVehicle entity, out RebirthVehicleAssembly assembly)','public static void RegisterBlock(']],
 ['// ACTUAL_ROLLBACK',['private static bool TryRestoreEntityMutation(','private static bool TryRestoreBlockAssembly(','private static string AppendFailure(']]
]){if(fixture.split(marker).length!==2)throw Error('Fixture marker '+marker);fixture=fixture.replace(marker,keys.map(method).join('\n'));}
const temp=await mkdtemp(join(tmpdir(),'rebirth-mp030-native-publication-'));
try{const cs=join(temp,'fixture.cs'),exe=join(temp,'fixture.exe');await writeFile(cs,fixture);
 await run('C:/Program Files/dotnet/dotnet.exe',['C:/Program Files/dotnet/sdk/9.0.301/Roslyn/bincore/csc.dll','/nologo','/langversion:latest','/target:exe','/out:'+exe,...['mscorlib','System','System.Core'].map(n=>'/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/'+n+'.dll'),cs],{windowsHide:true,timeout:20000});
 process.stdout.write((await run(exe,[],{windowsHide:true,timeout:20000})).stdout);
}finally{if(process.env.REBIRTH_KEEP_FIXTURE==='1')process.stdout.write(temp+'\n');else{if(!resolve(temp).toLowerCase().startsWith((resolve(tmpdir())+sep).toLowerCase())||!basename(temp).startsWith('rebirth-mp030-native-publication-'))throw Error('Unsafe fixture cleanup');await rm(temp,{recursive:true});}}
