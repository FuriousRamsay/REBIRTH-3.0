// Offline CLR serialization dependency. The game assembly is modified and its
// strong name is rejected by this CLR. This cached package is an explicit
// adapter dependency; it does not qualify the game's native serializer build.
import {readFile} from 'node:fs/promises';
import {createHash} from 'node:crypto';
import {join,resolve} from 'node:path';
export async function fixtureJsonDependency(){
 const path=resolve(process.env.REBIRTH_FIXTURE_JSON_PATH||join(process.env.USERPROFILE,'.nuget','packages','newtonsoft.json','13.0.1','lib','net45','Newtonsoft.Json.dll'));
 const bytes=await readFile(path);
 process.stdout.write(JSON.stringify({fixture_dependency:'Newtonsoft.Json',path,sha256:createHash('sha256').update(bytes).digest('hex'),scope:'offline serialization adapter; native game assembly excluded'})+'\n');
 return path;
}
