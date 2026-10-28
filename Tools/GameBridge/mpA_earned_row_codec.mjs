// PREPARED NOT RUN. Actual pure codec methods, no native authority simulation.
import {readFile,writeFile,mkdtemp,rm} from 'node:fs/promises';
import {execFile} from 'node:child_process';import {promisify} from 'node:util';
import {tmpdir} from 'node:os';import {join,resolve,dirname,basename,sep} from 'node:path';import {fileURLToPath} from 'node:url';
const run=promisify(execFile),here=dirname(fileURLToPath(import.meta.url)),root=resolve(here,'../..');
const source=await readFile(join(root,'Scripts/Crafting/Recycling/RebirthProductiveEarnedRowProof.cs'),'utf8');
function one(marker){const at=source.indexOf(marker);if(at<0||source.indexOf(marker,at+marker.length)>=0)throw Error('Missing/ambiguous actual source '+marker);return at;}
const start=one('internal static class RebirthProductiveEarnedRowProof');
const native=one('    // Uses the existing bounded native preflight BEFORE native queue/item decoders.');
const validation=one('    private static void Validate(Frame frame)');
// Only native decoder/capture methods are omitted. They require initialized game
// definitions and are explicitly NOT represented by the opaque frame bytes here.
const actual=source.slice(start,native)+source.slice(validation);
let fixture=await readFile(join(here,'mpA_earned_row_codec_fixture.cs'),'utf8');
if(fixture.split('// ACTUAL_PURE_CODEC').length!==2)throw Error('Fixture insertion mismatch');fixture=fixture.replace('// ACTUAL_PURE_CODEC',actual);
const temp=await mkdtemp(join(tmpdir(),'rebirth-earned-codec-'));
try{const cs=join(temp,'fixture.cs'),exe=join(temp,'fixture.exe');await writeFile(cs,fixture);await run('C:/Program Files/dotnet/dotnet.exe',['C:/Program Files/dotnet/sdk/9.0.301/Roslyn/bincore/csc.dll','/nologo','/langversion:latest','/target:exe','/out:'+exe,...['mscorlib','System','System.Core'].map(n=>'/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/'+n+'.dll'),cs],{windowsHide:true,timeout:20000});console.log((await run(exe,[],{windowsHide:true,timeout:20000})).stdout.trim());}
finally{if(process.env.REBIRTH_KEEP_FIXTURE==='1')console.log(temp);else{if(!resolve(temp).toLowerCase().startsWith((resolve(tmpdir())+sep).toLowerCase())||!basename(temp).startsWith('rebirth-earned-codec-'))throw Error('Refuse cleanup outside generated temp');await rm(temp,{recursive:true});}}
