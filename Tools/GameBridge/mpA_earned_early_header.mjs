// PREPARED NOT RUN. Extracts actual pure production method bodies only.
import {readFile,writeFile,mkdtemp,rm} from 'node:fs/promises';
import {execFile} from 'node:child_process';import {promisify} from 'node:util';
import {tmpdir} from 'node:os';import {join,resolve,dirname,basename,sep} from 'node:path';import {fileURLToPath} from 'node:url';
const run=promisify(execFile),here=dirname(fileURLToPath(import.meta.url)),root=resolve(here,'../..');
function method(source,marker){const at=source.indexOf(marker);if(at<0||source.indexOf(marker,at+marker.length)>=0)throw Error('Missing/ambiguous actual source '+marker);let p=source.indexOf('{',at),depth=0;for(let i=p;i<source.length;i++){if(source[i]==='{')depth++;if(source[i]==='}'&&--depth===0)return source.slice(at,i+1);}throw Error('Unterminated actual method');}
const proof=await readFile(join(root,'Scripts/Crafting/Recycling/RebirthProductiveEarnedRowProof.cs'),'utf8');
const start=proof.indexOf('internal static class RebirthProductiveEarnedRowProof'),native=proof.indexOf('    // Uses the existing bounded native preflight BEFORE native queue/item decoders.'),validation=proof.indexOf('    private static void Validate(Frame frame)');
if(start<0||native<=start||validation<=native)throw Error('Actual pure codec boundaries changed');
const pure=proof.slice(start,native)+proof.slice(validation);
const body=await readFile(join(root,'Scripts/Crafting/Recycling/RebirthProductiveEarnedNativeBody.cs'),'utf8');
const maximum=body.match(/internal const int MaximumBodyBytes=[^;]+;/g);if(maximum?.length!==1)throw Error('Actual body bound changed');
const comparison='internal static class RebirthProductiveEarnedNativeBody {'+maximum[0]+method(body,'    internal static byte[] ComparisonDigest(')+'}';
const header=await readFile(join(root,'Scripts/Crafting/Recycling/RebirthProductiveEarnedEarlyHeader.cs'),'utf8');
const manifest='internal static partial class RebirthProductiveEarnedRemoteHost {'+method(header,'    internal static string EarlyManifest(')+method(header,'    private static bool Manifest(')+'}';
let fixture=await readFile(join(here,'mpA_earned_early_header_fixture.cs'),'utf8');
for(const [marker,value] of [['// ACTUAL_PURE_CODEC',pure],['// ACTUAL_NATIVE_COMPARISON',comparison],['// ACTUAL_EARLY_MANIFEST',manifest]]){if(fixture.split(marker).length!==2)throw Error('Insertion mismatch');fixture=fixture.replace(marker,value);}
const temp=await mkdtemp(join(tmpdir(),'rebirth-earned-header-'));
try{const cs=join(temp,'fixture.cs'),exe=join(temp,'fixture.exe');await writeFile(cs,fixture);await run('C:/Program Files/dotnet/dotnet.exe',['C:/Program Files/dotnet/sdk/9.0.301/Roslyn/bincore/csc.dll','/nologo','/langversion:latest','/target:exe','/out:'+exe,...['mscorlib','System','System.Core'].map(n=>'/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/'+n+'.dll'),cs],{windowsHide:true,timeout:20000});console.log((await run(exe,[],{windowsHide:true,timeout:20000})).stdout.trim());}
finally{if(process.env.REBIRTH_KEEP_FIXTURE==='1')console.log(temp);else{if(!resolve(temp).toLowerCase().startsWith((resolve(tmpdir())+sep).toLowerCase())||!basename(temp).startsWith('rebirth-earned-header-'))throw Error('Refuse cleanup outside generated temp');await rm(temp,{recursive:true});}}
