import {readFile,writeFile,mkdtemp,unlink,rmdir} from 'node:fs/promises';
import {execFile} from 'node:child_process';
import {promisify} from 'node:util';
import {tmpdir} from 'node:os';
import {join,resolve,dirname} from 'node:path';
import {fileURLToPath} from 'node:url';
const run=promisify(execFile),here=dirname(fileURLToPath(import.meta.url)),root=resolve(here,'../..');
function method(source,signature){const a=source.indexOf(signature);if(a<0)throw new Error(signature);let b=source.indexOf('{',a),depth=1,i=b+1;for(;depth&&i<source.length;i++){if(source[i]==='{')depth++;if(source[i]==='}')depth--;}return source.slice(a,i);}
const paths=['Scripts/Companions/RebirthCompanionWindow.cs','Scripts/Companions/RebirthCompanionInteractionWindow.cs','Scripts/Survivor/UI/XUiC_RebirthSurvivorCreator.cs'];
let classes='';
for(let i=0;i<paths.length;i++){
 const source=await readFile(join(root,paths[i]),'utf8');
 classes+='class Screen'+i+' { '+['void DragScroll','int GetThumbHeight','int ClampOffset'].map(sig=>method(source,'    private static '+sig+'(').replace('private static','public static')).join('\n')+' }\n';
}
const profile=await readFile(join(root,'Scripts/Survivor/UI/XUiC_RebirthSurvivorProfileManager.cs'),'utf8');
classes+='class ProfileScreen { public object[] profiles=new object[20]; public int offset=4,calls; const int VisibleRows=6,ProfileTrackHeight=455; void Render(){calls++;} '+[
 '    private void ProfileThumbDrag(', '    private int ClampOffset(', '    private static int GetThumbHeight('
].map(sig=>method(profile,sig).replace('private','public')).join('\n')+' }\n';
const fixture=(await readFile(join(here,'test_scroll_zero_motion_fixture.cs'),'utf8')).replace('// SOURCE',classes);
const temp=await mkdtemp(join(tmpdir(),'rebirth-snapshot-test-'));
try {const cs=join(temp,'check.cs'),exe=join(temp,'check.exe');await writeFile(cs,fixture);
 await run('C:/Program Files/dotnet/dotnet.exe',['C:/Program Files/dotnet/sdk/9.0.301/Roslyn/bincore/csc.dll','/nologo','/target:exe','/out:'+exe,...['mscorlib','System','System.Core'].map(x=>'/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/'+x+'.dll'),cs],{windowsHide:true,timeout:10000});console.log((await run(exe,[],{windowsHide:true,timeout:10000})).stdout.trim());
} finally {for(const n of ['check.cs','check.exe'])await unlink(join(temp,n)).catch(e=>{if(e.code!=='ENOENT')throw e;});await rmdir(temp);}