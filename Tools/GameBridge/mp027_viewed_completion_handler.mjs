// AUTHORED_NOT_RUN. Ordinary actual introduction handlers plus actual planner.
import {readFile,writeFile,mkdtemp,readdir} from 'node:fs/promises';import {execFile} from 'node:child_process';import {promisify} from 'node:util';import {tmpdir} from 'node:os';import {join,resolve,dirname} from 'node:path';import {fileURLToPath} from 'node:url';
const here=dirname(fileURLToPath(import.meta.url)),root=resolve(here,'../..'),run=promisify(execFile);
let fixture=await readFile(join(here,'mp027_viewed_completion_plan_fixture.cs'),'utf8');
function replace(a,b){if(!fixture.includes(a))throw Error('Native adapter boundary changed '+a);fixture=fixture.replace(a,b);}
replace('using System;','using System;\nusing System.Text;\nusing System.Xml.Linq;');
replace('internal int entityId;','internal int entityId,Died;');
replace('internal object GetTileEntity(int position)=>Station;','internal object GetTileEntity(int position)=>Station;internal object GetTileEntity(Vector3i position)=>position.x==1&&position.y==2&&position.z==3?Station:null;');
replace('public static void Main()=>RebirthRecyclerRuntime.Exercise();','public static void Main()=>RebirthRecyclerPlacedSettlement.ExerciseHandler();');
replace('// ACTUAL_PLANNER',(await readFile(join(root,'Scripts/Crafting/Recycling/RebirthRecyclerRuntime.PlacedPlan.cs'),'utf8')).replace(/^using .*;\r?\n/gm,''));
fixture+='\n'+await readFile(join(here,'mp027_viewed_completion_handler_fixture.cs'),'utf8');
const source=await readFile(join(root,'Scripts/Crafting/Recycling/RebirthRecyclerPlacedSettlement.ViewedCompletion.cs'),'utf8');
function actual(key){const start=source.indexOf(key);if(start<0||source.indexOf(key,start+1)>=0)throw Error('Actual handler boundary changed '+key);let end=source.indexOf('{',start)+1,depth=1;for(;depth&&end<source.length;end++){if(source[end]==='{')depth++;else if(source[end]==='}')depth--;}if(depth)throw Error('Unclosed actual handler');return source.slice(start,end);}
const admissionStart=source.indexOf('private static bool IsViewedLocalAdmission('),admissionEnd=source.indexOf('private static bool TryBeginViewedLocalCompletion(',admissionStart);
if(admissionStart<0||admissionEnd<=admissionStart)throw Error('Actual scoped admission boundary changed');
replace('// ACTUAL_SERVER', ['private static void ReceiveViewedCompletion(','private static EntityPlayer ResolveViewedRecipient(','private static bool MatchesViewedFrames(','private static bool TryBeginViewedLocalCompletion('].map(actual).join('\n')+'\n'+source.slice(admissionStart,admissionEnd));
replace('// ACTUAL_REMOTE_FACTORY',actual('internal static ClosedAuthorityCompletion CaptureViewed('));
const out=await mkdtemp(join(tmpdir(),'rebirth-viewed-handler-')),cs=join(out,'actual.cs'),exe=join(out,'actual.exe');await writeFile(cs,fixture);
const sdk=process.env.REBIRTH_DOTNET_SDK_ROOT||'C:/Program Files/dotnet/sdk',versions=(await readdir(sdk)).filter(x=>/^\d+\.\d+\.\d+$/.test(x)).sort((a,b)=>a.localeCompare(b,undefined,{numeric:true}));if(!versions.length)throw Error('Roslyn unavailable; source preserved '+cs);
await run('dotnet',[join(sdk,versions.at(-1),'Roslyn/bincore/csc.dll'),'/nologo','/langversion:latest','/target:exe','/out:'+exe,...['mscorlib','System','System.Core','System.Xml','System.Xml.Linq'].map(x=>'/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/'+x+'.dll'),cs],{windowsHide:true,timeout:30000});console.log((await run(exe,[],{windowsHide:true,timeout:30000})).stdout.trim());
