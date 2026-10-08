import {readFile,writeFile,mkdtemp,unlink,rmdir} from 'node:fs/promises';
import {execFile} from 'node:child_process';
import {promisify} from 'node:util';
import {tmpdir} from 'node:os';
import {join,resolve,dirname} from 'node:path';
import {fileURLToPath} from 'node:url';
const run=promisify(execFile),here=dirname(fileURLToPath(import.meta.url)),root=resolve(here,'../..');
function method(source,signature){const a=source.indexOf(signature);if(a<0)throw Error(signature);let b=source.indexOf('{',a),depth=1,i=b+1;for(;depth;i++){if(source[i]==='{')depth++;if(source[i]==='}')depth--;}return source.slice(a,i);}
const known=method(await readFile(join(root,'Scripts/Crafting/Cooking/RebirthCookingCatalogue.cs'),'utf8'),'    public static bool Known(');
const has=method(await readFile(join(root,'Scripts/Survivor/Progression/RebirthKnowledgeService.cs'),'utf8'),'    public static bool HasKnowledge(');
const can=method(await readFile(join(root,'Scripts/Survivor/Progression/RebirthKnowledgeService.cs'),'utf8'),'    public static bool CanCraft(');
const backgrounds=await readFile(join(root,'Config/_Survivor/backgrounds.xml'),'utf8');
const chef=backgrounds.match(/<background id="background\.chef"[\s\S]*?<\/background>/)[0];
const butcher=backgrounds.match(/<background id="background\.butcher"[\s\S]*?<\/background>/)?.[0]??"";
const ids=[...backgrounds.matchAll(/<knowledge id="(recipe\.[^"]+)"/g)].map(m=>m[1]);
const rules=await readFile(join(root,'Config/_Survivor/crafting_progression.xml'),'utf8');
const pairs=[...rules.matchAll(/<recipe\s+([^>]+)>/g)].map(m=>[m[1].match(/\bid="([^"]+)"/)?.[1],m[1].match(/\bknowledge="([^"]+)"/)?.[1]]).filter(p=>ids.includes(p[1]));
for(const id of ids)if(!pairs.some(p=>p[1]===id))throw Error('Missing profile mapping: '+id);
const data=pairs.map(p=>'new string[]{'+p.map(x=>JSON.stringify(x)).join(',')+'}').join(',');
let fixture=await readFile(join(here,'test_profile_recipe_knowledge_fixture.cs'),'utf8');fixture=fixture.replace('// KNOWN',known).replace('// HAS',has).replace('// CAN',can).replace('// DATA','new string[][]{'+data+'}');
const temp=await mkdtemp(join(tmpdir(),'rebirth-profile-recipe-'));
try {const cs=join(temp,'check.cs'),exe=join(temp,'check.exe');await writeFile(cs,fixture);await run('C:/Program Files/dotnet/dotnet.exe',['C:/Program Files/dotnet/sdk/9.0.301/Roslyn/bincore/csc.dll','/nologo','/target:exe','/out:'+exe,...['mscorlib','System','System.Core'].map(x=>'/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/'+x+'.dll'),cs],{windowsHide:true,timeout:10000});console.log((await run(exe,[],{windowsHide:true,timeout:10000})).stdout.trim());}
finally {for(const n of ['check.cs','check.exe'])await unlink(join(temp,n)).catch(e=>{if(e.code!=='ENOENT')throw e;});await rmdir(temp);}