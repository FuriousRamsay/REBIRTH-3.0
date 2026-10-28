// Deferred actual-source filesystem fixture. NOT_RUN. Invoke only after the
// user's all-production/scenario-coding gate has been explicitly satisfied.
// Does not build/install the mod or contact/control the game.
import {readFile,writeFile,mkdtemp,readdir,mkdir,symlink} from 'node:fs/promises';
import {randomUUID} from 'node:crypto';
import {execFile} from 'node:child_process';
import {promisify} from 'node:util';
import {tmpdir} from 'node:os';
import {join,resolve,dirname} from 'node:path';
import {fileURLToPath} from 'node:url';
const run=promisify(execFile);
const root=resolve(dirname(fileURLToPath(import.meta.url)),'../..');
const actual=await readFile(join(root,'Scripts/Survivor/Progression/RebirthWorkstationCompletionPublication.cs'),'utf8');
const template=await readFile(join(root,'Tools/GameBridge/RebirthWorkstationCompletionPublicationFixture.cs'),'utf8');
const marker='// PRODUCTION_PUBLICATION';
if(template.split(marker).length!==2)throw Error('Unique actual-source publication fixture marker required');
if(actual.split('internal static class RebirthWorkstationCompletionPublication').length!==2)throw Error('Unique actual production publisher required');
const source=template.replace(marker,actual.replace(/^using [^;]+;\r?\n/gm,''));
if(source.includes(marker))throw Error('Unresolved production fixture extraction');
const temporary=await mkdtemp(join(tmpdir(),'rebirth-mp015-publication-'));
const save=join(temporary,'native-save'),outside=join(temporary,'outside-scope');
const server=join(save,'RebirthData','Survivor','WorkstationCompletions','server');
await mkdir(server,{recursive:true});await mkdir(outside);
const junction=join(server,'fixture-junction');await symlink(outside,junction,'junction');
const reparseOperation=randomUUID().replaceAll('-','');
const cs=join(temporary,'actual-publication.cs'),exe=join(temporary,'actual-publication.exe');
await writeFile(cs,source);
const sdk=process.env.REBIRTH_DOTNET_SDK_ROOT||'C:/Program Files/dotnet/sdk';
const versions=(await readdir(sdk)).filter(value=>/^\d+\.\d+\.\d+$/.test(value)).sort((a,b)=>a.localeCompare(b,undefined,{numeric:true}));
if(!versions.length)throw Error('Roslyn SDK unavailable; extracted actual source retained at '+cs);
try{
    const compilation=await run('dotnet',[join(sdk,versions.at(-1),'Roslyn/bincore/csc.dll'),'/nologo','/target:exe','/out:'+exe,
        ...['mscorlib','System','System.Core','System.Xml','System.Xml.Linq'].map(value=>'/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/'+value+'.dll'),cs],
        {windowsHide:true,timeout:30000});
    const result=await run(exe,[],{windowsHide:true,timeout:30000,env:{...process.env,
        REBIRTH_MP015_PUBLICATION_SAVE:save,REBIRTH_MP015_REPARSE_PATH:join(junction,reparseOperation+'.xml'),
        REBIRTH_MP015_OUTSIDE_PATH:join(outside,reparseOperation+'.xml')}});
    console.log(JSON.stringify({case:'mp015-actual-publication',result:'PASS',source:cs,compilation:compilation.stdout,output:result.stdout,
        scope:'actual publisher/strict reader; real disposable files and Windows sharing lock; native scope doubles, no game/network/material-persistence qualification'}));
}catch(error){
    console.error(JSON.stringify({case:'mp015-actual-publication',result:'FAIL',source:cs,error:String(error),stdout:error.stdout,stderr:error.stderr}));throw error;
}
// Retain extracted source/executable and native-file evidence for review. No
// recursive deletion or cleanup of uncertain publication evidence is performed.
