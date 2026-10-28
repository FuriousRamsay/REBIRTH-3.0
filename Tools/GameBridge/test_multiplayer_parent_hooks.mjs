// Deferred offline validation. Do not invoke before the all-coding gate.
// Executes actual small production classes with explicitly scoped native doubles.
import {readFile,writeFile,mkdtemp,rm,readdir} from 'node:fs/promises';
import {execFile} from 'node:child_process';
import {promisify} from 'node:util';
import {tmpdir} from 'node:os';
import {join,resolve,dirname} from 'node:path';
import {fileURLToPath} from 'node:url';
const run=promisify(execFile);
const root=resolve(dirname(fileURLToPath(import.meta.url)),'../..');
const repair=await readFile(join(root,'Scripts/Survivor/Repair/RebirthRepairOriginalCorrection.cs'),'utf8');
function hook(name){
    // Both selected production classes intentionally consist of one line body.
    // Fail if their shape changes rather than silently test a substituted method.
    const key='internal static class '+name;
    const at=repair.indexOf(key);
    if(at<0||repair.indexOf(key,at+key.length)>=0)throw Error('Unique actual hook required: '+name);
    const lines=repair.slice(at).split(/\r?\n/);
    if(!/^\{private static void Postfix\(.*\)\{.*\}\}$/.test(lines[1]))throw Error('Review changed hook extraction: '+name);
    return lines.slice(0,2).join('\n');
}
const repairFixture=await readFile(join(root,'Tools/GameBridge/RebirthRepairUploadHookFixture.cs'),'utf8');
const fuelFixture=await readFile(join(root,'Tools/GameBridge/RebirthCamperFuelStateFixture.cs'),'utf8');
const fuelSource=await readFile(join(root,'Scripts/Vehicles/Camper/RebirthCamperStationFuelState.cs'),'utf8');
const queueSource=await readFile(join(root,'Scripts/Workstations/RebirthWorkstationFuelPreservation.cs'),'utf8');
function block(source,key){
    const start=source.indexOf(key);if(start<0)throw Error('Missing actual classifier member: '+key);
    let pos=source.indexOf('{',start),depth=1,end=pos+1;
    for(;depth&&end<source.length;end++){if(source[end]==='{')depth++;else if(source[end]==='}')depth--;}
    if(depth)throw Error('Unbalanced classifier member: '+key);
    return source.slice(start,end);
}
const queueFixture=(await readFile(join(root,'Tools/GameBridge/RebirthFuelQueueFixture.cs'),'utf8')).replace('// PRODUCTION_QUEUE_CLASSIFIER',
    ['public enum ProductiveWorkState','public struct ProductiveWorkSnapshot','private static ProductiveWorkSnapshot EvaluateQueue('].map(key=>block(queueSource,key)).join('\n'));
const tradeSource=await readFile(join(root,'Scripts/NPC/Interaction/RebirthNpcTradeService.cs'),'utf8');
const tradeFixture=(await readFile(join(root,'Tools/GameBridge/RebirthTradeOfferRegistryFixture.cs'),'utf8')).replace('// PRODUCTION_TRADE_REGISTRY',
    ['public sealed class RebirthNpcTradeOffer\n','public static class RebirthNpcTradeOfferRegistry'].map(key=>block(tradeSource.replace(/\r\n/g,'\n'),key)).join('\n'));
const outputSource=await readFile(join(root,'Scripts/Crafting/Recycling/RebirthRecipeOutputs.cs'),'utf8');
const outputFixture=(await readFile(join(root,'Tools/GameBridge/RebirthOutputDefinitionFixture.cs'),'utf8'))
 .replace('// PRODUCTION_OUTPUT_TYPES',['public sealed class RebirthAdditionalRecipeOutput','public sealed class RebirthRecipeOutputDefinition'].map(key=>block(outputSource,key)).join('\n'))
 .replace('// PRODUCTION_OUTPUT_METHODS',['private static void Register(','public static bool TryGet(','internal static string DefinitionIdentity('].map(key=>block(outputSource,key)).join('\n'));
const vehicleRestorationSource=await readFile(join(root,'Scripts/Vehicles/Restoration/RebirthVehicleRestorationSystem.cs'),'utf8');
const vehicleWriteFixture=(await readFile(join(root,'Tools/GameBridge/RebirthVehicleInventoryWriteFixture.cs'),'utf8')).replace('// PRODUCTION_DIRECT_VEHICLE_METHODS',
 ['public static bool TryTakeSimpleItems(','private static bool TryGiveStackNoDrop(','private static ItemStack[] CloneSlots(','private static void RestoreSlots('].map(key=>block(vehicleRestorationSource,key)).join('\n'));
const vehicleWriteSource=await readFile(join(root,'Scripts/Vehicles/Restoration/RebirthVehicleInventoryWrite.cs'),'utf8');
const refundFixture=await readFile(join(root,'Tools/GameBridge/RebirthVehicleRefundDropFixture.cs'),'utf8');
const refundSource=await readFile(join(root,'Scripts/Vehicles/Restoration/RebirthVehicleRefundDropWitness.cs'),'utf8');
const stationSource=await readFile(join(root,'Scripts/Survivor/Progression/RebirthWorkstationCraftCompletion.cs'),'utf8');
const stationFixture=(await readFile(join(root,'Tools/GameBridge/RebirthStationSnapshotHookFixture.cs'),'utf8'))
 .replace('// PRODUCTION_SNAPSHOT_TYPES',['internal sealed class Snapshot','private sealed class Addition'].map(key=>block(stationSource,key)).join('\n'))
 .replace('// PRODUCTION_SNAPSHOT_HOOK',block(stationSource,'private static void AfterSnapshot('));
const commandActions=await readFile(join(root,'Scripts/Vehicles/Camper/RebirthCamperRemoteStationActions.cs'),'utf8');
const trackedStart=commandActions.indexOf('    // Warm invocation evidence only.');
const trackedEnd=commandActions.indexOf('    internal static bool Handle(',trackedStart);
if(trackedStart<0||trackedEnd<=trackedStart)throw Error('Missing actual tracked invocation scope');
const refusalFixture=(await readFile(join(root,'Tools/GameBridge/RebirthCamperCommandRefusalFixture.cs'),'utf8'))
 .replace('// PRODUCTION_TRACKED_SCOPE',commandActions.slice(trackedStart,trackedEnd));
const commandTransport=await readFile(join(root,'Scripts/Vehicles/Camper/RebirthCamperRemoteStationTransport.cs'),'utf8');
const commandStatus=await readFile(join(root,'Scripts/Vehicles/Camper/RebirthCamperRemoteStationCommandStatus.cs'),'utf8');
const commandClassifier=commandStatus.match(/internal static bool HasNativeOriginal\([^;]+;/)?.[0];
if(!commandClassifier)throw Error('Missing actual native-original classifier');
const commandFixture=(await readFile(join(root,'Tools/GameBridge/RebirthCamperCommandAdmissionFixture.cs'),'utf8'))
 .replace('// PRODUCTION_KIND_CLASSIFIER',commandClassifier)
 .replace('// PRODUCTION_COMMAND_EXECUTE',block(commandTransport,'internal static bool Execute('));
const commandClient=await readFile(join(root,'Scripts/Vehicles/Camper/RebirthCamperRemoteStationClient.cs'),'utf8');
const commandClientFixture=(await readFile(join(root,'Tools/GameBridge/RebirthCamperCommandStatusClientFixture.cs'),'utf8'))
 .replace('// PRODUCTION_CLIENT_STATUS',block(commandClient,'internal static void ReceiveOriginalStatus('))
 .replace('// PRODUCTION_CLIENT_COMMAND_REPLY',block(commandClient,'private static void ReceiveCommand('))
 .replace('// PRODUCTION_CLIENT_FUEL',block(commandClient,'internal static void SetBurning('))
 .replace('// PRODUCTION_CLIENT_SEND',block(commandClient,'private static void SendCommand('));
const outboxSource=await readFile(join(root,'Scripts/Vehicles/Camper/RebirthCamperRemoteCommandOutbox.cs'),'utf8');
const addressSource=await readFile(join(root,'Scripts/Vehicles/Camper/RebirthCamperStationAddress.cs'),'utf8');
const commandType=await readFile(join(root,'Scripts/Vehicles/Camper/RebirthCamperRemoteStationCommand.cs'),'utf8');
const outboxFixture=(await readFile(join(root,'Tools/GameBridge/RebirthCamperCommandOutboxFixture.cs'),'utf8'))
 .replace('// PRODUCTION_KIND_CLASSIFIER',commandClassifier)
 .replace('// PRODUCTION_OUTBOX_TYPES',[addressSource,commandType,outboxSource].map(s=>s.replace(/^using [^;]+;\r?\n/gm,'')).join('\n'));
const refusalStoreSource=await readFile(join(root,'Scripts/Vehicles/Camper/RebirthCamperRemoteStationCommandStatusStore.cs'),'utf8');
const invocationStoreSource=await readFile(join(root,'Scripts/Vehicles/Camper/RebirthCamperRemoteStationCommandStatusStore.Invocation.cs'),'utf8');
const refusalStoreFixture=(await readFile(join(root,'Tools/GameBridge/RebirthCamperCommandRefusalStoreFixture.cs'),'utf8'))
 .replace('// PRODUCTION_KIND_CLASSIFIER',commandClassifier)
 .replace('// PRODUCTION_BINDING',block(commandStatus,'internal static string Binding('))
 .replace('// PRODUCTION_TRACKED_SCOPE',commandActions.slice(trackedStart,trackedEnd))
 .replace('// PRODUCTION_SERVER_STORE_TYPES',[addressSource,commandType,refusalStoreSource,invocationStoreSource].map(s=>s.replace(/^using [^;]+;\r?\n/gm,'')).join('\n'));
const hitContext=await readFile(join(root,'Scripts/Fire/Actions/RebirthExtinguisherHitContext.cs'),'utf8');
const hitFixture=(await readFile(join(root,'Tools/GameBridge/RebirthExtinguisherHitContextFixture.cs'),'utf8'))
 .replace('// PRODUCTION_HIT_CONTEXT',hitContext.replace(/^using [^;]+;\r?\n/gm,''));
const hostPdsHelper=await readFile(join(root,'Scripts/Crafting/Recycling/RebirthProductiveEarnedUiOwnerFile.HostPds.cs'),'utf8');
const hostPdsFixture=(await readFile(join(root,'Tools/GameBridge/RebirthDeferredOwnerPdsFrameFixture.cs'),'utf8'))
 .replace('// PRODUCTION_HOST_PDS_HELPER',hostPdsHelper.replace(/^using [^;]+;\r?\n/gm,''));
const ownerPdsSource=await readFile(join(root,'Scripts/Crafting/Recycling/RebirthProductiveEarnedUiDeferredTransport.OwnerPds.cs'),'utf8');
const ownerHashFixture=(await readFile(join(root,'Tools/GameBridge/RebirthDeferredOwnerFrameHashFixture.cs'),'utf8'))
 .replace('// PRODUCTION_FRAME_HASH_STREAM',block(ownerPdsSource,'private sealed class FrameHashStream'))
 .replace('// PRODUCTION_FRAME_HASH',block(ownerPdsSource,'private static string Frame('));
const hostProtocol=await readFile(join(root,'Scripts/Crafting/Recycling/RebirthProductiveEarnedUiDeferredTransport.HostProtocol.cs'),'utf8');
const ownerFileSource=await readFile(join(root,'Scripts/Crafting/Recycling/RebirthProductiveEarnedUiOwnerFile.cs'),'utf8');
const ownerSaveStart=ownerFileSource.indexOf('    private static class Save');
const ownerSaveEnd=ownerFileSource.indexOf('        private static void PrefixLegacy(',ownerSaveStart);
if(ownerSaveStart<0||ownerSaveEnd<=ownerSaveStart)throw Error('Missing actual owner-save wrapper');
const ownerSaveBody=ownerFileSource.indexOf('{',ownerSaveStart)+1;
const ownerSaveFixture=(await readFile(join(root,'Tools/GameBridge/RebirthOwnerSaveCompositionFixture.cs'),'utf8'))
 .replace('// PRODUCTION_SAVE_WRAPPER',ownerFileSource.slice(ownerSaveBody,ownerSaveEnd));
const hydrationHost=await readFile(join(root,'Scripts/Crafting/Recycling/RebirthProductiveEarnedUiDeferredTransport.HydrationHost.cs'),'utf8');
const hostFrameFixture=(await readFile(join(root,'Tools/GameBridge/RebirthDeferredHostFrameFixture.cs'),'utf8'))
 .replace('// PRODUCTION_HOST_FRAME_TYPE',block(hostProtocol,'private sealed class HostOwnerFrame'))
 .replace('// PRODUCTION_HYDRATION_HASH',block(hydrationHost,'private static string HydrationHash('))
 .replace('// PRODUCTION_HOST_FRAME_PART',block(hostProtocol,'private static bool HostFramePart('));
const cases=[
    ['station-upload-hook',stationFixture],
    ['camper-command-admission',commandFixture],
    ['camper-command-status-client',commandClientFixture],
    ['camper-command-outbox',outboxFixture],
    ['repair-upload',repairFixture+'\n'+hook('RebirthRepairOriginalNativeUpload')+'\n'+hook('RebirthRepairOriginalNativePlayerData')],
    ['command-refusal',refusalFixture],
    ['owner-save-composition',ownerSaveFixture],
    ['deferred-host-frame',hostFrameFixture],
    ['deferred-owner-frame-hash',ownerHashFixture],
    ['deferred-owner-pds-frame',hostPdsFixture],
    ['extinguisher-hit-context',hitFixture],
    ['command-refusal-store',refusalStoreFixture],
    ['fuel-state',fuelFixture+'\n'+fuelSource.replace(/^using System;\s*/,'')],
    ['fuel-queue',queueFixture],
    ['trade-offers',tradeFixture],
    ['output-definition',outputFixture],
    ['vehicle-write',vehicleWriteFixture+'\n'+vehicleWriteSource.replace(/^using System;\s*/,'')],
    ['vehicle-refund-drop',refundFixture+'\n'+refundSource.replace(/^using [^;]+;\r?\n/gm,'')]
];
const temporary=await mkdtemp(join(tmpdir(),'rebirth-parent-hooks-'));
try{
    const sdk=process.env.REBIRTH_DOTNET_SDK_ROOT||'C:/Program Files/dotnet/sdk';
    const versions=(await readdir(sdk)).filter(x=>/^\d+\.\d+\.\d+$/.test(x)).sort((a,b)=>a.localeCompare(b,undefined,{numeric:true}));
    if(!versions.length)throw Error('Roslyn SDK unavailable');
    for(const [name,source] of cases){
        const cs=join(temporary,name+'.cs'),exe=join(temporary,name+'.exe');
        await writeFile(cs,source);
        await run('dotnet',[join(sdk,versions.at(-1),'Roslyn/bincore/csc.dll'),'/nologo','/target:exe','/out:'+exe,
            ...['mscorlib','System','System.Core','System.Xml','System.Xml.Linq'].map(x=>'/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/'+x+'.dll'),cs],
            {windowsHide:true,timeout:30000});
        await run(exe,[],{windowsHide:true,timeout:30000});
        console.log(JSON.stringify({case:name,result:'PASS',scope:'actual helper/hook with explicit native doubles; no persistence/network/gameplay qualification'}));
    }
}finally{await rm(temporary,{recursive:true,force:true});}






