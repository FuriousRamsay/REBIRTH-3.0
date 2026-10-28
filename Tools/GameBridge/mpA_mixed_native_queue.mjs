// PREPARED NOT RUN. Compiles/runs only after a later explicit test checkpoint.
import {readFile,writeFile,mkdtemp,rm} from 'node:fs/promises';
import {execFile} from 'node:child_process';import {promisify} from 'node:util';
import {tmpdir} from 'node:os';import {join,resolve,dirname,basename,sep} from 'node:path';import {fileURLToPath} from 'node:url';import {createHash} from 'node:crypto';
const run=promisify(execFile),here=dirname(fileURLToPath(import.meta.url)),root=resolve(here,'../..');
const tools=await readFile(join(root,'Scripts/Crafting/Recycling/RebirthProductiveToolWork.cs'),'utf8');
const runtime=await readFile(join(root,'Scripts/Crafting/Recycling/RebirthRecyclerRuntime.cs'),'utf8');
const bonus=await readFile(join(root,'Scripts/Crafting/Recycling/RebirthRecyclerBonuses.cs'),'utf8');
const native=await readFile('E:/_Haven/shared/REBIRTHTemp/VehicleUIReportedInspect/TileEntityWorkstation.cs','utf8');
if(createHash('sha256').update(native).digest('hex').toUpperCase()!=='52773978CD57F5EA3F3495FEE79B987A7C094EEA6CE0AA29786269C09B838779')throw Error('Exact native extraction provenance changed');
let fixture=await readFile(join(here,'mpA_mixed_native_queue_fixture.cs'),'utf8');
function one(text,marker){const a=text.indexOf(marker);if(a<0||text.indexOf(marker,a+marker.length)>=0)throw Error('Missing/ambiguous source '+marker);return a;}
// Brace scanner ignores C# normal/verbatim strings, characters and comments.
function method(text,marker){const a=one(text,marker),start=text.indexOf('{',a);let depth=0,state='code';for(let i=start;i<text.length;i++){const c=text[i],n=text[i+1];if(state==='line'){if(c==='\n')state='code';continue;}if(state==='block'){if(c==='*'&&n==='/'){state='code';i++;}continue;}if(state==='quote'||state==='char'){if(c==='\\'){i++;continue;}if(c===(state==='quote'?'"':"'"))state='code';continue;}if(state==='verbatim'){if(c==='"'&&n==='"'){i++;continue;}if(c==='"')state='code';continue;}if(c==='/'&&n==='/'){state='line';i++;continue;}if(c==='/'&&n==='*'){state='block';i++;continue;}if(c==='@'&&n==='"'){state='verbatim';i++;continue;}if(c==='"'){state='quote';continue;}if(c==="'"){state='char';continue;}if(c==='{')depth++;if(c==='}'&&--depth===0)return text.slice(a,i+1);}throw Error('Unclosed actual method');}
const toolSource=tools.slice(one(tools,'internal static class RebirthProductiveToolWork'),one(tools,'// Ordinary open cards'));
const bonusSource=bonus.slice(one(bonus,'public static class RebirthRecyclerBonuses'),one(bonus,'    // The old preview estimator'))+'public static List<ItemStack> CompletionRewards(ItemValue source,Guid group)=>new List<ItemStack>();\n}';
const processSource=runtime.slice(one(runtime,'    internal static bool Process(TileEntityWorkstation station,float elapsed)'),one(runtime,'    internal static void Refresh('));
const group=runtime.slice(one(runtime,'    private sealed class Group'),one(runtime,'    private sealed class Processing'));
const prefix=method(runtime,'    private static bool Prefix(TileEntityWorkstation __instance,float __0,bool __runOriginal,out object __state)').replace('private static','internal static');
const finalizer=method(runtime,'    private static Exception Finalizer(Exception __exception,object __state)').replace('private static','internal static');
const cycle=method(native,'public void cycleRecipeQueue()');
let body=method(native,'public void HandleRecipeQueue(float _timePassed)').replace('public void HandleRecipeQueue','private void NativeBody');
const rewrites=[['recipeQueueItem.CraftingTimeLeft -= _timePassed;','RebirthRecyclerNativeLoop.SetTime(recipeQueueItem,recipeQueueItem.CraftingTimeLeft-_timePassed,this);'],['recipeQueueItem.CraftingTimeLeft += recipeQueueItem.OneItemCraftTime;','RebirthRecyclerNativeLoop.SetTime(recipeQueueItem,recipeQueueItem.CraftingTimeLeft+recipeQueueItem.OneItemCraftTime,this);'],['recipeQueueItem.CraftingTimeLeft += ((craftingTimeLeft < 0f) ? craftingTimeLeft : 0f);','RebirthRecyclerNativeLoop.SetTime(recipeQueueItem,recipeQueueItem.CraftingTimeLeft+((craftingTimeLeft<0f)?craftingTimeLeft:0f),this);'],['hasRecipeInQueue()','RebirthRecyclerNativeLoop.Continue(this)'],['bUserAccessing ||','RebirthRecyclerNativeLoop.UserAccessing(this) ||'],['!isBurning','!RebirthRecyclerNativeLoop.BurningForPaidCompletion(this)']];
for(const [from,to] of rewrites){one(body,from);body=body.replace(from,to);}
for(const [marker,value] of [['// ACTUAL_TOOL_SOURCE',toolSource],['// ACTUAL_BONUS_SOURCE',bonusSource],['// ACTUAL_GROUP',group],['// ACTUAL_PROCESS_SOURCE',processSource],['// ACTUAL_PREFIX',prefix],['// ACTUAL_FINALIZER',finalizer],['// ACTUAL_NATIVE_CYCLE',cycle],['// ACTUAL_NATIVE_TRANSLATED',body]]){one(fixture,marker);fixture=fixture.replace(marker,value);}
const processingPredicate=runtime.match(/internal static bool IsProcessing\(TileEntityWorkstation station\)=>[^;]+;/g);
if(processingPredicate?.length!==1)throw Error('Actual processing predicate changed');
fixture=fixture.replace('// ACTUAL_PROCESSING_PREDICATE',processingPredicate[0]);
const temp=await mkdtemp(join(tmpdir(),'rebirth-mp054-'));
try{const cs=join(temp,'fixture.cs'),exe=join(temp,'fixture.exe');await writeFile(cs,fixture);await run('C:/Program Files/dotnet/dotnet.exe',['C:/Program Files/dotnet/sdk/9.0.301/Roslyn/bincore/csc.dll','/nologo','/langversion:latest','/target:exe','/out:'+exe,...['mscorlib','System','System.Core'].map(n=>'/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/'+n+'.dll'),cs],{windowsHide:true,timeout:20000});console.log((await run(exe,[],{windowsHide:true,timeout:20000})).stdout.trim());}
finally{if(process.env.REBIRTH_KEEP_FIXTURE==='1')console.log(temp);else{if(!resolve(temp).toLowerCase().startsWith((resolve(tmpdir())+sep).toLowerCase())||!basename(temp).startsWith('rebirth-mp054-'))throw Error('Refuse cleanup outside generated temp');await rm(temp,{recursive:true});}}
