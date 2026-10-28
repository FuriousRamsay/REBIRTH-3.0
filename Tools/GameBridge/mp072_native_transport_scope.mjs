// AUTHORED_NOT_RUN. Actual packet handlers, actual receive-dequeue observer and
// actual logistics peer/admission checks; native transport/roster are adapters.
import {readFile,writeFile,mkdtemp,readdir} from 'node:fs/promises';
import {execFile} from 'node:child_process';import {promisify} from 'node:util';
import {tmpdir} from 'node:os';import {join,resolve,dirname} from 'node:path';import {fileURLToPath} from 'node:url';
const root=resolve(dirname(fileURLToPath(import.meta.url)),'../..'),run=promisify(execFile);
const actual=(await readFile(join(root,'Scripts/QuickStack/RebirthLogisticsOriginalTransport.cs'),'utf8')).replace(/\r/g,'');
function body(text,key){let start=text.indexOf(key);if(start<0||text.indexOf(key,start+1)>=0)throw Error('Unique production member required: '+key);let end=text.indexOf('{',start)+1,depth=1;for(;depth&&end<text.length;end++){if(text[end]==='{')depth++;else if(text[end]==='}')depth--;}if(depth)throw Error('Production member boundary missing');return text.slice(start,end);}
let fixture=await readFile(join(root,'Tools/GameBridge/mp072_native_transport_scope_fixture.cs'),'utf8');
const helperStart=actual.indexOf('    internal static bool IsPurposePackage('),helperEnd=actual.indexOf('    private sealed class Job:',helperStart);
if(helperStart<0||helperEnd<helperStart)throw Error('Actual logistics helper boundaries missing');
fixture=fixture.replace('// ACTUAL_HELPERS',actual.slice(helperStart,helperEnd));
const sources={HeaderRequest:'HeaderAdmission',HeaderAck:'HeaderAdmission',NativeBodyIntent:'NativeBodyIntent',NativeBodyAck:'NativeBodyIntent',NativeBodyQuery:'NativeBodyIntent',HistoricalZero:'HistoricalBeforeCapsule',HistoricalZeroAck:'HistoricalBeforeCapsule',BodyReferenceNotice:'LoadedBodyNotice',BodyReferenceAck:'LoadedBodyNotice'};
for(const type of ['OriginalRequest','OriginalResult','PhysicalEvidence','PhysicalAck','TerminalPhysical',...Object.keys(sources)]){
 const text=sources[type]?(await readFile(join(root,'Scripts/QuickStack/RebirthLogistics'+sources[type]+'.cs'),'utf8')).replace(/\r/g,''):actual;
 const klass=body(text,'public sealed class NetPackageRebirthLogistics'+type+':NetPackage');
 fixture=fixture.replace('// ACTUAL_'+type,body(klass,'public override void ProcessPackage('));
}
fixture=fixture.replace('// ACTUAL_DEQUEUE',(await readFile(join(root,'Scripts/Crafting/Recycling/RebirthProductiveEarnedUiDeferredTransport.ReceivedConnection.cs'),'utf8')).replace(/^using .*;\r?\n/gm,''));
const notice=(await readFile(join(root,'Scripts/QuickStack/RebirthLogisticsLoadedBodyNotice.cs'),'utf8')).replace(/\r/g,'');
const noticeStart=notice.indexOf('    private static bool ClientConnectionCurrent('),noticeEnd=notice.indexOf('    private static bool Equivalent(',noticeStart);
if(noticeStart<0||noticeEnd<noticeStart)throw Error('Actual retained notice session boundary missing');
fixture=fixture.replace('// ACTUAL_NOTICE_CONNECTION',notice.slice(noticeStart,noticeEnd));
const owner=await readFile(join(root,'Scripts/QuickStack/RebirthLogisticsOwnerReceipt.cs'),'utf8');
fixture=fixture.replace('// ACTUAL_OWNER_UPLOAD',body(owner,'    private static void SaveAndUpload('));
if(process.argv.includes('--resources')){
 const network=(await readFile(join(root,'Scripts/Crafting/RemoteCrafting/RebirthRemoteResourceNetworkScope.cs'),'utf8')).replace(/^using .*;\r?\n/gm,'');
 const extension=(await readFile(join(root,'Tools/GameBridge/mp009_original_network_scope_fixture.cs'),'utf8')).split('// ADAPTER_TYPES');
 if(extension.length!==2)throw Error('Resource network fixture boundary changed');
 fixture=fixture.replace('static class RebirthRemoteResourceNetworkScope{internal static bool IsPacket(NetPackage packet)=>false;}',network).replace('// RESOURCE_SCOPE_TESTS',extension[0])+'\n'+extension[1];
 const request=(await readFile(join(root,'Scripts/Crafting/RemoteCrafting/RebirthRemoteResourcePlacedStationOriginal.CancellationRequestNative.cs'),'utf8')).replace(/^using .*;\r?\n/gm,'');
 fixture+='\n'+request;
}
const temp=await mkdtemp(join(tmpdir(),'rebirth-mp072-native-scope-')),cs=join(temp,'actual.cs'),exe=join(temp,'actual.exe');await writeFile(cs,fixture);
const sdk=process.env.REBIRTH_DOTNET_SDK_ROOT||'C:/Program Files/dotnet/sdk',versions=(await readdir(sdk)).filter(v=>/^\d+\.\d+\.\d+$/.test(v)).sort((a,b)=>a.localeCompare(b,undefined,{numeric:true}));
if(!versions.length)throw Error('Roslyn unavailable; authored source retained at '+cs);
await run('dotnet',[join(sdk,versions.at(-1),'Roslyn/bincore/csc.dll'),'/nologo','/target:exe','/out:'+exe,...['mscorlib','System','System.Core'].map(x=>'/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/'+x+'.dll'),cs],{windowsHide:true,timeout:30000});
console.log((await run(exe,[],{windowsHide:true,timeout:30000})).stdout.trim());
