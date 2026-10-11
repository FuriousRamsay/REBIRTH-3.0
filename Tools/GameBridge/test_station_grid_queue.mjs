import {readFile,writeFile,mkdtemp,unlink,rmdir} from 'node:fs/promises';
import {execFile} from 'node:child_process';
import {promisify} from 'node:util';
import {tmpdir} from 'node:os';
import {join,resolve,dirname} from 'node:path';
import {fileURLToPath} from 'node:url';
const run=promisify(execFile);
const here=dirname(fileURLToPath(import.meta.url));
const root=resolve(here,'../..');
const source=await readFile(join(root,'Scripts/Crafting/UI/RebirthStationGridQueue.cs'),'utf8');
const gridSource=await readFile(join(root,'Scripts/Crafting/UI/RebirthStationGridIngredients.cs'),'utf8');
const native=await readFile('C:/Users/Etienne/Desktop/__0/_0/__Rebirth/7DTD Base 3.2/Recipe.cs','utf8');
const a=source.indexOf('public static class RebirthStationGridQueue');
const c=native.indexOf('  public void Write(BinaryWriter _bw)'),d=native.indexOf('  public override string ToString()',c);if(a<0||c<0||d<=c)throw new Error('Actual class/native codec missing');
const scopeSource=await readFile(join(root,'Scripts/Survivor/Network/RebirthSurvivorRequestScope.cs'),'utf8');
const admissionSource=await readFile(join(root,'Scripts/Crafting/UI/RebirthStationGridAdmission.cs'),'utf8');
const snapshotCodec=await readFile(join(root,'Scripts/Crafting/UI/RebirthStationGridSnapshotCodec.cs'),'utf8');
const intentSource=await readFile(join(root,'Scripts/Survivor/Persistence/RebirthStationTerminalIntent.cs'),'utf8');
const refundSource=await readFile(join(root,'Scripts/Crafting/UI/RebirthStationCancellationRefund.cs'),'utf8');
const persistenceSource=await readFile(join(root,'Scripts/Survivor/Persistence/RebirthStationPreparationPersistence.cs'),'utf8');
const fixture=await readFile(join(here,'test_station_grid_queue_fixture.cs'),'utf8');
function extractClass(text, declaration) {
 const start=text.indexOf(declaration);
 if(start<0||text.indexOf(declaration,start+declaration.length)>=0)throw new Error('Missing or ambiguous production declaration: '+declaration);
 return text.slice(start);
}
for(const marker of ['// PERSISTENCE_CLASS','// ADMISSION_CLASS','// SNAPSHOT_CODEC_CLASS','// GRID_CLASS','// PRODUCTION_CLASS','// NATIVE_CODEC']) {
 if(fixture.split(marker).length!==2)throw new Error('Missing or duplicate fixture marker: '+marker);
}
const temp=await mkdtemp(join(tmpdir(),'rebirth-owner-test-'));
try {
 const cs=join(temp,'check.cs'), exe=join(temp,'check.exe');
 await writeFile(cs,fixture.replace('// PERSISTENCE_CLASS',extractClass(persistenceSource,'public static class RebirthStationPreparationPersistence')).replace('// ADMISSION_CLASS',extractClass(scopeSource,'public static class RebirthSurvivorRequestScope')+extractClass(admissionSource,'public sealed class RebirthStationGridAdmission')).replace('// SNAPSHOT_CODEC_CLASS',extractClass(snapshotCodec,'public static class RebirthStationGridSnapshotCodec')).replace('// GRID_CLASS',extractClass(gridSource,'public static class RebirthStationGridIngredients')).replace('// PRODUCTION_CLASS',source.slice(a)+extractClass(intentSource,'public sealed class RebirthStationTerminalIntent')+extractClass(refundSource,'internal sealed class RebirthStationCancellationRefund')).replace('// NATIVE_CODEC',native.slice(c,d)));
 await run('C:/Program Files/dotnet/dotnet.exe',[
 'C:/Program Files/dotnet/sdk/9.0.301/Roslyn/bincore/csc.dll','/nologo','/target:exe','/out:'+exe,
 '/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/mscorlib.dll',
 '/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/System.dll',
 '/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/System.Core.dll','/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/System.Xml.dll','/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/System.Xml.Linq.dll',cs],{windowsHide:true,timeout:10000});
 const result=await run(exe,[],{windowsHide:true,timeout:10000});
 console.log(result.stdout.trim());
 console.log("SCOPE: current REBIRTH source; Recipe codec from supplied 7DTD Base 3.2 reference; item codec/native collaborators doubled. Not installed 3.3 runtime or multiplayer qualification.");
} finally {
 for(const name of ['check.cs','check.exe']) await unlink(join(temp,name)).catch(e=>{if(e.code!=='ENOENT')throw e;});
 await rmdir(temp);
}
