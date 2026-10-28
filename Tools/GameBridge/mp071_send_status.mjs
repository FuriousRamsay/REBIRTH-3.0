// AUTHORED_NOT_RUN. Actual client send -> server Execute/Handle -> status -> client terminal.
// Reuses disclosed native/persistence doubles; real store/outbox IO has separate cohorts.
import {readFile,writeFile,mkdtemp,readdir} from 'node:fs/promises';
import {execFile} from 'node:child_process';import {promisify} from 'node:util';
import {tmpdir} from 'node:os';import {join,resolve,dirname} from 'node:path';import {fileURLToPath} from 'node:url';
const run=promisify(execFile),root=resolve(dirname(fileURLToPath(import.meta.url)),'../..');
function block(s,key){const start=s.indexOf(key);if(start<0||s.indexOf(key,start+1)>=0)throw Error('Unique source required '+key);let end=s.indexOf('{',start)+1,n=1;for(;n&&end<s.length;end++){if(s[end]==='{')n++;else if(s[end]==='}')n--;}if(n)throw Error('Unclosed method');return s.slice(start,end);}
const dir=join(root,'Scripts/Vehicles/Camper');
const [actions,status,transport,command,client,base,appendix]=await Promise.all([
 ...['RebirthCamperRemoteStationActions.cs','RebirthCamperRemoteStationCommandStatus.cs','RebirthCamperRemoteStationTransport.cs','RebirthCamperRemoteStationCommand.cs','RebirthCamperRemoteStationClient.cs'].map(x=>readFile(join(dir,x),'utf8')),
 readFile(join(root,'Tools/GameBridge/mp071_dispatch_status_fixture.cs'),'utf8'),readFile(join(root,'Tools/GameBridge/mp071_send_status_fixture.cs'),'utf8')]);
let fixture=base;
for(const [marker,value] of [['// ACTUAL_COMMAND',command.replace(/^using .*;\r?\n/gm,'')],['// ACTUAL_STATUS',status.replace(/^using .*;\r?\n/gm,'')],['// ACTUAL_EXECUTE',block(transport,'internal static bool Execute(')],['// ACTUAL_ACTIONS_BODY',actions.slice(actions.indexOf('[ThreadStatic] private static Invocation'),actions.lastIndexOf('}'))]])fixture=fixture.replace(marker,value);
fixture=fixture.replace('internal object GetEntity(int id)=>Player;','internal object GetEntity(int id)=>Player;internal EntityPlayerLocal GetPrimaryPlayer()=>Player as EntityPlayerLocal;');
fixture=fixture.replace('class EntityPlayer{','class EntityPlayerLocal:EntityPlayer{}\nclass EntityPlayer{');
fixture=fixture.replace('internal int Id;internal bool IsValid','internal int Id;internal RebirthCamperStationAddress(Guid vehicle,Guid installation){Id=1;}internal bool IsValid');
fixture=fixture.replace('internal sealed class Lease{','internal const byte StationCommand=2,FuelSwitch=4;\n internal sealed class Lease{');
fixture=fixture.replace('internal static int MetadataWrites;','internal static bool FixtureRefuse(EntityPlayer p,RebirthCamperStationAddress a,string c,object l,RebirthCamperRemoteStationCommand q){if(!TryAdmit(p,a,c,l,q,out _,out _,out _)||Invoked.Contains(q.Operation))return false;Refused.Add(q.Operation);return true;}\n internal static int MetadataWrites;');
fixture=fixture.replace('player=new EntityPlayer{world=world}','player=new EntityPlayerLocal{world=world}');
fixture=fixture.replace('static void Main()','static void BaselineMain()');
const at=fixture.indexOf('static void BaselineMain()');
fixture=fixture.slice(0,at)+`static void Main(){BaselineMain();foreach(bool survivor in new[]{false,true})foreach(RebirthCamperRemoteStationCommandKind kind in Enum.GetValues(typeof(RebirthCamperRemoteStationCommandKind)))foreach(bool before in new[]{false,true}){Setup(survivor);var q=Command(kind);RebirthCamperRemoteStationPlayerSource.ActorSource=kind==RebirthCamperRemoteStationCommandKind.CraftPlayer;QueueEvidence(q,kind==RebirthCamperRemoteStationCommandKind.ScrapQueue||kind==RebirthCamperRemoteStationCommandKind.ScrapCancel,kind==RebirthCamperRemoteStationCommandKind.Cancel||kind==RebirthCamperRemoteStationCommandKind.ScrapCancel);SendStatusFixture.Run((EntityPlayerLocal)player,address,token,lease,q,before);}Console.WriteLine("MP071 composed send/status: 28 original send-fault paths authored with actual production control flow.");}\n`+fixture.slice(at);
let suffix=appendix.replace('// ACTUAL_CLIENT_SEND',block(client,'private static void SendCommand(')).replace('// ACTUAL_CLIENT_STATUS',block(client,'internal static void ReceiveOriginalStatus('));fixture+='\n'+suffix;
const temp=await mkdtemp(join(tmpdir(),'rebirth-mp071-send-status-')),cs=join(temp,'actual.cs'),exe=join(temp,'actual.exe');await writeFile(cs,fixture);
const sdk=process.env.REBIRTH_DOTNET_SDK_ROOT||'C:/Program Files/dotnet/sdk',versions=(await readdir(sdk)).filter(x=>/^\d+\.\d+\.\d+$/.test(x)).sort((a,b)=>a.localeCompare(b,undefined,{numeric:true}));if(!versions.length)throw Error('Roslyn unavailable; retained '+cs);
await run('dotnet',[join(sdk,versions.at(-1),'Roslyn/bincore/csc.dll'),'/nologo','/target:exe','/out:'+exe,...['mscorlib','System','System.Core','System.Xml','System.Xml.Linq'].map(x=>'/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/'+x+'.dll'),cs],{windowsHide:true,timeout:30000});
console.log((await run(exe,[],{windowsHide:true,timeout:30000})).stdout.trim());
