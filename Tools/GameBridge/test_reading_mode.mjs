import {readFile,writeFile,mkdtemp,unlink,rmdir} from 'node:fs/promises';
import {execFile} from 'node:child_process';
import {promisify} from 'node:util';
import {tmpdir} from 'node:os';
import {join,resolve,dirname} from 'node:path';
import {fileURLToPath} from 'node:url';
const run=promisify(execFile),here=dirname(fileURLToPath(import.meta.url)),root=resolve(here,'../..');
const source=await readFile(join(root,'Scripts/Survivor/Progression/RebirthLiteratureService.cs'),'utf8');
function method(signature,sourceText=source){const source=sourceText;const a=source.indexOf(signature);if(a<0)throw new Error(signature);let b=source.indexOf('{',a),depth=1,i=b+1;for(;depth&&i<source.length;i++){if(source[i]==='{')depth++;if(source[i]==='}')depth--;}return source.slice(a,i);}
const trackingSource=await readFile(join(root,'Scripts/Survivor/Progression/RebirthLiteratureStudySessionService.cs'),'utf8');
const optionSource=await readFile(join(root,'Scripts/Options/RebirthSandboxOptionManager.cs'),'utf8');
const defaultField=optionSource.match(/public bool RequireTimedReading = (true|false);/)[0];
const encodeLine=optionSource.split(/\r?\n/).find(x=>x.includes('if (!state.RequireTimedReading) result +='));
const decodeBlock=method('            else if (optionIndex == (int)RebirthSandboxOptionId.RequireTimedReading)',optionSource).replace(/^\s*else /,'');
const helpers=['    private static char IndexToAlpha(','    private static string IndexToAlpha2(','    private static int AlphaToIndex(','    private static int Alpha2ToIndex('].map(x=>method(x,optionSource)).join('\n');
const optionFixture=`class ReadingState {${defaultField}} enum RebirthSandboxOptionId {RequireTimedReading=${optionSource.match(/RequireTimedReading\s*=\s*(\d+)/)[1]}}
class ReadingCodec { ${helpers}
public static string Encode(ReadingState state){string result="";${encodeLine}return result;}
public static bool Decode(string fragment,out ReadingState decoded){decoded=new ReadingState();if(fragment.Length==0)return true;if(fragment.Length!=3)return false;int optionIndex=Alpha2ToIndex(fragment[0],fragment[1]),valueIndex=AlphaToIndex(fragment[2]);if(valueIndex<0)return false;${decodeBlock}else return false;return true;}}
`;
const fixture=(await readFile(join(here,'test_reading_mode_fixture.cs'),'utf8')).replace('// METHODS',method('    public static bool TryReadMatchingInventoryItem(')).replace('// COMPLETION METHOD',method('    public static bool TryCompleteStudy(')).replace('// AUDIO COMPLETION METHOD',method('    public static bool TryCompleteAudiobook(')).replace('// TRACKING METHOD',method('    public static void BeginClientTracking(',trackingSource)).replace('// OPTION TYPES',optionFixture);
const temp=await mkdtemp(join(tmpdir(),'rebirth-snapshot-test-'));
try {const cs=join(temp,'check.cs'),exe=join(temp,'check.exe');await writeFile(cs,fixture);
 await run('C:/Program Files/dotnet/dotnet.exe',['C:/Program Files/dotnet/sdk/9.0.301/Roslyn/bincore/csc.dll','/nologo','/target:exe','/out:'+exe,...['mscorlib','System','System.Core'].map(x=>'/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/'+x+'.dll'),cs],{windowsHide:true,timeout:10000});console.log((await run(exe,[],{windowsHide:true,timeout:10000})).stdout.trim());
} finally {for(const n of ['check.cs','check.exe'])await unlink(join(temp,n)).catch(e=>{if(e.code!=='ENOENT')throw e;});await rmdir(temp);}