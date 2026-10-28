// Offline production-extraction fixtures only. Never launches the game.
import {readdir,readFile,writeFile,appendFile,mkdir} from 'node:fs/promises';
import {execFile} from 'node:child_process';
import {promisify} from 'node:util';
import {createHash} from 'node:crypto';
import {dirname,resolve,join} from 'node:path';
import {fileURLToPath} from 'node:url';
const run=promisify(execFile),here=dirname(fileURLToPath(import.meta.url)),root=resolve(here,'../..');
const supplemental=process.argv.includes('--supplemental');
const label=process.argv.find(value=>value.startsWith('--label='))?.slice(8);
if(label&&!/^[a-z0-9_]+$/.test(label))throw Error('Invalid result label');
const only=process.argv.find(value=>value.startsWith('--only='))?.slice(7).split(',');
const output=join(root,'_Documentation/MultiplayerAudit/ProjectWide_20261014/offline_batch_20261018'+(label?'_'+label:supplemental?'_supplemental':''));
await mkdir(output,{recursive:true});
const available=(await readdir(here)).filter(name=>/^mp.*\.mjs$/.test(name)&&name!=='mp018_existing_cohort_boundaries.mjs');
if(only&&only.some(name=>!available.includes(name)))throw Error('Unknown selected fixture');
const names=available.filter(name=>only?only.includes(name):(/^mp(?:\d{3}|B_).*\.mjs$/.test(name)!==supplemental)).sort();
const hash=bytes=>createHash('sha256').update(bytes).digest('hex');
const manifest=[];
for(const name of names){
 const source=await readFile(join(here,name),'utf8');
 const paths=new Set(),modules=[source],seenModules=new Set([name]);
 for(let index=0;index<modules.length;index++){
  const module=modules[index];
  for(const match of module.matchAll(/(?:Scripts|Tools\/GameBridge)\/[A-Za-z0-9_./-]+\.cs/g))paths.add(match[0]);
  // Runners also read sibling fixture templates via join(here, 'name.cs').
  for(const match of module.matchAll(/['"]([A-Za-z0-9_.-]+\.cs)['"]/g)){
   const path='Tools/GameBridge/'+match[1];try{await readFile(join(root,path));paths.add(path);}catch(error){if(error.code!=='ENOENT')throw error;}
  }
  for(const match of module.matchAll(/from\s+['"]\.\/([A-Za-z0-9_.-]+\.mjs)['"]/g)){
   const path='Tools/GameBridge/'+match[1];paths.add(path);
   if(!seenModules.has(match[1])){seenModules.add(match[1]);modules.push(await readFile(join(root,path),'utf8'));}
  }
 }
 const inputs=[];for(const path of paths){try{inputs.push({path,sha256:hash(await readFile(join(root,path)))});}catch(error){inputs.push({path,missing:error.code});}}
 manifest.push({name,sha256:hash(source),literal_inputs:inputs});
}
await writeFile(join(output,'manifest.json'),JSON.stringify({scope:'isolated offline fixtures; native/gameplay/topology not qualified',runners:manifest},null,2));
const results=[];let next=0,stopped=false;
async function worker(){while(!stopped){
 const index=next++;if(index>=names.length)return;const name=names[index];
 let running;
 try{const processes=await run('pwsh.exe',['-NoLogo','-NoProfile','-Command',"[bool](Get-Process | Where-Object { $_.ProcessName -match '^7DaysToDie' }) | ConvertTo-Json"],{windowsHide:true,maxBuffer:1024*1024});running=JSON.parse(processes.stdout.trim());}
 catch(error){stopped=true;console.log('DEFERRED: process guard unavailable: '+error.message);return;}
 if(running){stopped=true;console.log('DEFERRED: game running; no further fixture started');return;}
 const started=new Date().toISOString(),args=[join(here,name),...(['mp028_loaded_group.mjs','mp028_loaded_native_member.mjs'].includes(name)?['--all']:[])];
 let result;
 try{const value=await run(process.execPath,args,{cwd:root,windowsHide:true,timeout:180000,maxBuffer:8*1024*1024});result={name,started,result:'PASS',exit_code:0,stdout:value.stdout,stderr:value.stderr};}
 catch(error){result={name,started,result:'FAIL',exit_code:error.code??null,killed:error.killed??false,stdout:error.stdout??'',stderr:error.stderr??'',message:error.message};}
 result.finished=new Date().toISOString();results.push(result);const completed=results.length;await writeFile(join(output,name+'.json'),JSON.stringify(result,null,2));
 await appendFile(join(output,'results.jsonl'),JSON.stringify(result)+'\n');
 console.log(`${completed}/${names.length} ${result.result} ${name}`);
}}
await Promise.all([worker(),worker()]);
const summary={finished:new Date().toISOString(),planned:names.length,executed:results.length,passed:results.filter(row=>row.result==='PASS').length,failed:results.filter(row=>row.result==='FAIL').length,deferred:names.length-results.length,failures:results.filter(row=>row.result==='FAIL').map(row=>row.name),scope:'offline fixture results only; per-runner doubles and native exclusions apply'};
await writeFile(join(output,'summary.json'),JSON.stringify(summary,null,2));console.log(JSON.stringify(summary));
process.exitCode=summary.failed||summary.deferred?1:0;
