import {readFile,writeFile,mkdtemp,unlink,rmdir} from 'node:fs/promises';
import {execFile} from 'node:child_process';
import {promisify} from 'node:util';
import {tmpdir} from 'node:os';
import {join,resolve,dirname} from 'node:path';
import {fileURLToPath} from 'node:url';
const run=promisify(execFile);
const here=dirname(fileURLToPath(import.meta.url));
const root=resolve(here,'../..');
const sellTransfer=await readFile(join(root,'Scripts/Survivor/Support/RebirthBackpackSellStashTransfer.cs'),'utf8');
const sellConservation=await readFile(join(root,'Scripts/Survivor/Support/RebirthBackpackSellStashConservation.cs'),'utf8');
const source=await readFile(join(root,'Scripts/Survivor/Support/RebirthBackpackLibraryTransfer.cs'),'utf8');
const a=source.indexOf('public static class RebirthBackpackLibraryTransfer');if(a<0)throw new Error('Production class missing');
const conservation=await readFile(join(root,'Scripts/Survivor/Support/RebirthBackpackLibraryConservation.cs'),'utf8');
const receipt=await readFile(join(root,'Scripts/Survivor/Domain/RebirthBackpackLibraryReceipt.cs'),'utf8');
const persistence=await readFile(join(root,'Scripts/Survivor/Persistence/RebirthBackpackLibraryPersistence.cs'),'utf8');
const journal=await readFile(join(root,'Scripts/Survivor/Support/RebirthBackpackLibraryJournal.cs'),'utf8');
const owner=await readFile(join(root,'Scripts/Survivor/Support/RebirthBackpackLibraryOwnerTransfer.cs'),'utf8');
const evidence=await readFile(join(root,'Scripts/Survivor/Support/RebirthBackpackLibrarySavedEvidence.cs'),'utf8');
const server=await readFile(join(root,'Scripts/Survivor/Support/RebirthBackpackLibraryServer.cs'),'utf8');
const reservation=await readFile(join(root,'Scripts/Survivor/Support/RebirthBackpackLibraryReservation.cs'),'utf8');
const guard=await readFile(join(root,'Scripts/Survivor/Support/RebirthBackpackLibraryCursorGuard.cs'),'utf8');
const prefixStart=guard.indexOf('    public static bool Prefix('), prefixBody=guard.indexOf('{',prefixStart);
let prefixEnd=prefixBody+1, prefixDepth=1;for(;prefixDepth;prefixEnd++){if(guard[prefixEnd]==='{')prefixDepth++;if(guard[prefixEnd]==='}')prefixDepth--;}
const prefix=guard.slice(prefixStart,prefixEnd);
const actionStart=guard.indexOf('    public static bool Prefix(BaseItemActionEntry'),actionBody=guard.indexOf('{',actionStart);
let actionEnd=actionBody+1,actionDepth=1;for(;actionDepth;actionEnd++){if(guard[actionEnd]==='{')actionDepth++;if(guard[actionEnd]==='}')actionDepth--;}
const actionPrefix=guard.slice(actionStart,actionEnd).replace('bool Prefix(','bool ActionPrefix(');
const instantMethods=[];
for(const [path,name] of [['Scripts/Survivor/Progression/ItemActionStudyLiteratureRebirth.cs','StudyInstant'],['Scripts/Survivor/Progression/ItemActionListenAudiobookRebirth.cs','AudioInstant'],['Scripts/Survivor/Support/ItemActionUseTraitSupportRebirth.cs','SupportInstant']]){
 const sellTransfer=await readFile(join(root,'Scripts/Survivor/Support/RebirthBackpackSellStashTransfer.cs'),'utf8');
const sellConservation=await readFile(join(root,'Scripts/Survivor/Support/RebirthBackpackSellStashConservation.cs'),'utf8');
const source=await readFile(join(root,path),'utf8'),start=source.indexOf('    public override bool ExecuteInstantAction(');
 if(start<0)throw new Error(path+' instant action missing');let end=source.indexOf('{',start)+1,depth=1;
 for(;depth&&end<source.length;end++){if(source[end]==='{')depth++;if(source[end]==='}')depth--;}
 if(depth)throw new Error(path+' unclosed method');instantMethods.push(source.slice(start,end).replace('public override bool ExecuteInstantAction','public static bool '+name));
}
const partialStart=guard.indexOf('    public static bool Prefix(Bag'),partialBody=guard.indexOf('{',partialStart);
let partialEnd=partialBody+1,partialDepth=1;for(;partialDepth;partialEnd++){if(guard[partialEnd]==='{')partialDepth++;if(guard[partialEnd]==='}')partialDepth--;}
const partialPrefix=guard.slice(partialStart,partialEnd).replace('bool Prefix(','bool PartialStackPrefix(');
const addStart=guard.indexOf('    public static bool Prefix(Bag __instance,ItemStack'),addBody=guard.indexOf('{',addStart);
let addEnd=addBody+1,addDepth=1;for(;addDepth;addEnd++){if(guard[addEnd]==='{')addDepth++;if(guard[addEnd]==='}')addDepth--;}
const addPrefix=guard.slice(addStart,addEnd).replace('bool Prefix(','bool BagAddPrefix(');
const beltStart=guard.indexOf('    public static bool Prefix(Inventory'),beltBody=guard.indexOf('{',beltStart);
let beltEnd=beltBody+1,beltDepth=1;for(;beltDepth;beltEnd++){if(guard[beltEnd]==='{')beltDepth++;if(guard[beltEnd]==='}')beltDepth--;}
const beltPrefix=guard.slice(beltStart,beltEnd).replace('bool Prefix(','bool BeltPlacementPrefix(');
const beltAddStart=guard.indexOf('    public static bool Prefix(Inventory __instance,ItemStack'),beltAddBody=guard.indexOf('{',beltAddStart);
let beltAddEnd=beltAddBody+1,beltAddDepth=1;for(;beltAddDepth;beltAddEnd++){if(guard[beltAddEnd]==='{')beltAddDepth++;if(guard[beltAddEnd]==='}')beltAddDepth--;}
const beltAddPrefix=guard.slice(beltAddStart,beltAddEnd).replace('bool Prefix(','bool BeltAddPrefix(');
const beltPartialStart=guard.indexOf('    public static bool Prefix(Inventory __instance,int startIndex'),beltPartialBody=guard.indexOf('{',beltPartialStart);
let beltPartialEnd=beltPartialBody+1,beltPartialDepth=1;for(;beltPartialDepth;beltPartialEnd++){if(guard[beltPartialEnd]==='{')beltPartialDepth++;if(guard[beltPartialEnd]==='}')beltPartialDepth--;}
const beltPartialPrefix=guard.slice(beltPartialStart,beltPartialEnd).replace('bool Prefix(','bool BeltPartialPrefix(');
const materialStart=guard.indexOf('    public static bool Prefix(XUiM_PlayerInventory __instance,ref bool'),materialBody=guard.indexOf('{',materialStart);
let materialEnd=materialBody+1,materialDepth=1;for(;materialDepth;materialEnd++){if(guard[materialEnd]==='{')materialDepth++;if(guard[materialEnd]==='}')materialDepth--;}
const materialPrefix=guard.slice(materialStart,materialEnd).replace('bool Prefix(','bool MaterialPrefix(');
const cookingIngredients=await readFile(join(root,'Scripts/Crafting/Cooking/RebirthCookingLocalIngredients.cs'),'utf8');
const wire=await readFile(join(root,'Scripts/Survivor/Support/RebirthBackpackLibraryWireCodec.cs'),'utf8');
const assembly=await readFile(join(root,'Scripts/Survivor/Support/RebirthBackpackLibraryOfferAssembly.cs'),'utf8');
const inbox=await readFile(join(root,'Scripts/Survivor/Support/RebirthBackpackLibraryOfferInbox.cs'),'utf8');
const chunk=await readFile(join(root,'Scripts/Survivor/Support/RebirthBackpackLibraryOfferChunk.cs'),'utf8');
const ownerInbox=await readFile(join(root,'Scripts/Survivor/Support/RebirthBackpackLibraryOwnerInbox.cs'),'utf8');
const clientOffers=await readFile(join(root,'Scripts/Survivor/Network/RebirthBackpackLibraryClientOffers.cs'),'utf8');
const preparePackage=await readFile(join(root,'Scripts/Survivor/Network/RebirthBackpackLibraryPrepareNetPackage.cs'),'utf8');
const offerPackage=await readFile(join(root,'Scripts/Survivor/Network/RebirthBackpackLibraryOfferNetPackage.cs'),'utf8');
const recoveryDelivery=await readFile(join(root,'Scripts/Survivor/Network/RebirthBackpackLibraryRecoveryNetPackage.cs'),'utf8');
const continuation=await readFile(join(root,'Scripts/Survivor/Support/RebirthBackpackQuickTransferContinuation.cs'),'utf8');
const refundProducer=await readFile(join(root,'Scripts/Crafting/RemoteCrafting/RemoteResourceRefundProducer.cs'),'utf8');
const refundCheckpoint=await readFile(join(root,'Scripts/Crafting/RemoteCrafting/RemoteResourceRefundSaveCheckpoint.cs'),'utf8');
const refundRepository=await readFile(join(root,'Scripts/Survivor/Persistence/RebirthWorldCharacterRepository.cs'),'utf8');
const refundWitnessStart=refundRepository.indexOf('    internal static bool HasSavedRemoteResourceRefunds');
const refundWitnessMethod=refundRepository.slice(refundWitnessStart,refundRepository.indexOf('    // Exact current final-file witness',refundWitnessStart)).replaceAll('RebirthStablePlayerIdentity','RefundIdentityDouble');
const custodyStart=refundRepository.indexOf('    private static bool HasPendingItemCustody');
const repositoryCustody='public static class RepositoryCustodyFixture { '+refundRepository.slice(custodyStart,refundRepository.indexOf('    private static bool TryLoadValidatedRecord',custodyStart)).replace('private static bool','public static bool')+' }';
const refundSupport=await readFile(join(root,'Scripts/Crafting/RemoteCrafting/RemoteResourceRefundSupportPersistence.cs'),'utf8');
const refundJournal=await readFile(join(root,'Scripts/Crafting/RemoteCrafting/RemoteResourceRefundJournal.cs'),'utf8');
const refundRecord=await readFile(join(root,'Scripts/Crafting/RemoteCrafting/RemoteResourceRefundRecord.cs'),'utf8');
const resourceClient=await readFile(join(root,'Scripts/Crafting/RemoteCrafting/RemoteResourceClientTransactions.cs'),'utf8');
const outcomeJournal=resourceClient.slice(resourceClient.indexOf('internal sealed class RemoteResourceTransactionOutcome'),resourceClient.indexOf('[HarmonyPatch(typeof(GameManager), nameof(GameManager.Update))]')).replaceAll('PlatformUserIdentifierAbs','InternalPlayerId');
const grantContext=resourceClient.slice(resourceClient.indexOf('public static class RemoteResourceClientGrantContext'),resourceClient.indexOf('[HarmonyPatch(typeof(ItemActionEntryCraft)'));
const countStart=resourceClient.indexOf('    public static int CountConsumableLocal');
const pendingMethod=resourceClient.slice(resourceClient.indexOf('    public static bool HasPendingInventoryOperation'),resourceClient.indexOf('    public static bool IsDedicatedClient'));
const countContext='public static class RemoteResourceClientTransactionCoordinator { public static System.Collections.Generic.Dictionary<ulong,object> pendingLeases=new System.Collections.Generic.Dictionary<ulong,object>();public static int outcomeDeliveryDepth; '+pendingMethod+resourceClient.slice(countStart,resourceClient.indexOf('public static class RemoteResourceClientGrantContext')).trim();
const sectionDestination=await readFile(join(root,'Scripts/Survivor/Support/RebirthBackpackSectionDestination.cs'),'utf8');
const sellUi=await readFile(join(root,'Scripts/Survivor/UI/XUiC_RebirthBackpackSellStash.cs'),'utf8');
const sellPrepare=await readFile(join(root,'Scripts/Survivor/Network/RebirthBackpackSellStashPrepareNetPackage.cs'),'utf8');
const sellClientViews=await readFile(join(root,'Scripts/Survivor/Network/RebirthBackpackSellStashClientViews.cs'),'utf8');
const sellViewTransport=await readFile(join(root,'Scripts/Survivor/Network/RebirthBackpackSellStashViewNetPackage.cs'),'utf8');
const sellViewCache=await readFile(join(root,'Scripts/Survivor/Support/RebirthBackpackSellStashViewCache.cs'),'utf8');
const sellViewResponse=await readFile(join(root,'Scripts/Survivor/Support/RebirthBackpackSellStashViewResponse.cs'),'utf8');
const sellViewCodec=await readFile(join(root,'Scripts/Survivor/Support/RebirthBackpackSellStashViewCodec.cs'),'utf8');
const sellView=await readFile(join(root,'Scripts/Survivor/Support/RebirthBackpackSellStashView.cs'),'utf8');
const libraryView=await readFile(join(root,'Scripts/Survivor/Support/RebirthBackpackLibraryView.cs'),'utf8');
const viewCodec=await readFile(join(root,'Scripts/Survivor/Support/RebirthBackpackLibraryViewCodec.cs'),'utf8');
const requestScope=await readFile(join(root,'Scripts/Survivor/Network/RebirthSurvivorRequestScope.cs'),'utf8');
const creationWire=await readFile(join(root,'Scripts/Survivor/Network/RebirthBackpackLibraryCreationWire.cs'),'utf8');
const studyIntent=await readFile(join(root,'Scripts/Survivor/Support/RebirthBackpackLibraryStudyIntent.cs'),'utf8');
const libraryUi=await readFile(join(root,'Scripts/Survivor/UI/XUiC_RebirthBackpackLibrary.cs'),'utf8');
const viewCache=await readFile(join(root,'Scripts/Survivor/Support/RebirthBackpackLibraryViewCache.cs'),'utf8');
const viewResponse=await readFile(join(root,'Scripts/Survivor/Support/RebirthBackpackLibraryViewResponse.cs'),'utf8');
const viewTransport=await readFile(join(root,'Scripts/Survivor/Network/RebirthBackpackLibraryViewNetPackage.cs'),'utf8');
const clientViews=await readFile(join(root,'Scripts/Survivor/Network/RebirthBackpackLibraryClientViews.cs'),'utf8');
const settlement=await readFile(join(root,'Scripts/Survivor/Persistence/RebirthBackpackLibrarySettlement.cs'),'utf8');
const settlementTransport=await readFile(join(root,'Scripts/Survivor/Network/RebirthBackpackLibrarySettlementNetPackage.cs'),'utf8');
const fixture=await readFile(join(here,'test_library_transfer_fixture.cs'),'utf8');
const temp=await mkdtemp(join(tmpdir(),'rebirth-owner-test-'));
try {
 const cs=join(temp,'check.cs'), exe=join(temp,'check.exe');
 await writeFile(cs,fixture.replace('// REFUND_PRODUCER_CLASS',refundProducer.slice(refundProducer.indexOf('internal static class RemoteResourceRefundProducer'))).replace('// OUTCOME_JOURNAL_CLASS',outcomeJournal).replace('// REFUND_CHECKPOINT_CLASS',refundCheckpoint.slice(refundCheckpoint.indexOf('internal static class RemoteResourceRefundSaveCheckpoint'))).replace('// REFUND_WITNESS_METHOD',refundWitnessMethod).replace('// REFUND_CUSTODY_CLASS',repositoryCustody).replace('// REFUND_SUPPORT_CLASS',refundSupport.slice(refundSupport.indexOf('internal static class RemoteResourceRefundSupportPersistence'))).replace('// REFUND_JOURNAL_CLASS',refundJournal.slice(refundJournal.indexOf('internal sealed class RemoteResourceRefundJournal'))).replace('// REFUND_RECORD_CLASS',refundRecord.slice(refundRecord.indexOf('internal sealed class RemoteResourceRefundRecord'))).replace('// REMOTE_GRANT_CLASS',grantContext+'\n'+countContext).replace('// QUICK_CONTINUATION_CLASS',continuation.slice(continuation.indexOf('public sealed class RebirthBackpackQuickTransferContinuation'))).replace('// SECTION_DESTINATION_CLASS',sectionDestination.slice(sectionDestination.indexOf('public static class RebirthBackpackSectionDestination'))).replace('// SELL_UI_CLASS',sellUi.slice(sellUi.indexOf('public sealed class XUiC_RebirthBackpackSellStash'))).replace('// SELL_PREPARE_CLASS',sellPrepare.slice(sellPrepare.indexOf('public sealed class NetPackageRebirthBackpackSellStashPrepareRequest'))).replace('// SELL_CLIENT_VIEWS_CLASS',sellClientViews.slice(sellClientViews.indexOf('public static class RebirthBackpackSellStashClientViews'))).replace('// SELL_VIEW_TRANSPORT_CLASSES',sellViewTransport.slice(sellViewTransport.indexOf('public sealed class NetPackageRebirthBackpackSellStashViewRequest')).replace(/\[Preserve\]\s*/g,'')).replace('// SELL_VIEW_CACHE_CLASS',sellViewCache.slice(sellViewCache.indexOf('public sealed class RebirthBackpackSellStashViewCache'))).replace('// SELL_VIEW_RESPONSE_CLASS',sellViewResponse.slice(sellViewResponse.indexOf('public sealed class RebirthBackpackSellStashViewResponse'))).replace('// SELL_VIEW_CODEC_CLASS',sellViewCodec.slice(sellViewCodec.indexOf('public static class RebirthBackpackSellStashViewCodec'))).replace('// SELL_VIEW_CLASS',sellView.slice(sellView.indexOf('public sealed class RebirthBackpackSellStashView'))).replace('// SELL_TRANSFER_CLASS',sellTransfer.slice(sellTransfer.indexOf('public static class RebirthBackpackSellStashTransfer'))).replace('// SELL_CONSERVATION_CLASS',sellConservation.slice(sellConservation.indexOf('public static class RebirthBackpackSellStashConservation'))).replace('// STUDY_INTENT_CLASS',creationWire.slice(creationWire.indexOf('public static class RebirthBackpackLibraryCreationWire'))+'\n'+requestScope.slice(requestScope.indexOf('public static class RebirthSurvivorRequestScope'))+'\n'+studyIntent.slice(studyIntent.indexOf('public sealed class RebirthBackpackLibraryStudyIntent'))).replace('// LIBRARY_UI_CLASS',libraryUi.slice(libraryUi.indexOf('public sealed class XUiC_RebirthBackpackLibrary'))).replace('// PREPARE_PACKAGE_CLASS',preparePackage.slice(preparePackage.indexOf('public sealed class NetPackageRebirthBackpackLibraryPrepareRequest'))).replace('// SETTLEMENT_TRANSPORT_CLASSES',settlementTransport.slice(settlementTransport.indexOf('public sealed class NetPackageRebirthBackpackLibrarySettleRequest')).replace(/\[Preserve\]\s*/g,'')).replace('// SETTLEMENT_CLASS',settlement.slice(settlement.indexOf('public sealed class RebirthBackpackLibrarySettlement'))).replace('// VIEW_TRANSPORT_CLASSES',viewTransport.slice(viewTransport.indexOf('public sealed class NetPackageRebirthBackpackLibraryViewRequest')).replace(/\[Preserve\]\s*/g,'')).replace('// CLIENT_VIEWS_CLASS',clientViews.slice(clientViews.indexOf('public static class RebirthBackpackLibraryClientViews'))).replace('// LIBRARY_VIEW_RESPONSE_CLASS',viewResponse.slice(viewResponse.indexOf('public sealed class RebirthBackpackLibraryViewResponse'))).replace('// LIBRARY_VIEW_CACHE_CLASS',viewCache.slice(viewCache.indexOf('public sealed class RebirthBackpackLibraryViewCache'))).replace('// LIBRARY_VIEW_CODEC_CLASS',viewCodec.slice(viewCodec.indexOf('public static class RebirthBackpackLibraryViewCodec'))).replace('// LIBRARY_VIEW_CLASS',libraryView.slice(libraryView.indexOf('public sealed class RebirthBackpackLibraryView'))).replace('// RECOVERY_REQUEST_CLASS',recoveryDelivery.slice(recoveryDelivery.indexOf('public sealed class NetPackageRebirthBackpackLibraryRecoveryRequest'),recoveryDelivery.indexOf('public static class RebirthBackpackLibraryRecoveryDelivery'))).replace('// RECOVERY_DELIVERY_CLASS',recoveryDelivery.slice(recoveryDelivery.indexOf('public static class RebirthBackpackLibraryRecoveryDelivery'))).replace('// OFFER_PACKAGE_CLASS',offerPackage.slice(offerPackage.indexOf('public sealed class NetPackageRebirthBackpackLibraryOfferChunk'))).replace('// CLIENT_OFFERS_CLASS',clientOffers.slice(clientOffers.indexOf('public static class RebirthBackpackLibraryClientOffers'))).replace('// OWNER_INBOX_CLASS',ownerInbox.slice(ownerInbox.indexOf('public sealed class RebirthBackpackLibraryOwnerInbox'))).replace('// OFFER_CHUNK_CLASS',chunk.slice(chunk.indexOf('public sealed class RebirthBackpackLibraryOfferChunk'))).replace('// OFFER_INBOX_CLASS',inbox.slice(inbox.indexOf('public sealed class RebirthBackpackLibraryOfferInbox'))).replace('// OFFER_ASSEMBLY_CLASS',assembly.slice(assembly.indexOf('public sealed class RebirthBackpackLibraryOfferAssembly'))).replace('// WIRE_CODEC_CLASS',wire.slice(wire.indexOf('public static class RebirthBackpackLibraryWireCodec'))).replace('// PRODUCTION_CLASS',source.slice(a)).replace('// CONSERVATION_CLASS',conservation.slice(conservation.indexOf('public static class RebirthBackpackLibraryConservation'))).replace('// RECEIPT_CLASS',receipt.slice(receipt.indexOf('public sealed class RebirthBackpackLibraryReceipt'))).replace('// PERSISTENCE_CLASS',persistence.slice(persistence.indexOf('public enum RebirthBackpackLibraryPhase'))).replace('// JOURNAL_CLASS',journal.slice(journal.indexOf('public static class RebirthBackpackLibraryJournal'))).replace('// OWNER_CLASS',owner.slice(owner.indexOf('public enum RebirthBackpackLibraryOwnerResult'))).replace('// EVIDENCE_CLASS',evidence.slice(evidence.indexOf('public static class RebirthBackpackLibrarySavedEvidence'))).replace('// SERVER_CLASS',server.slice(server.indexOf('public enum RebirthBackpackLibraryViewStatus'))).replace('// RESERVATION_CLASS',reservation.slice(reservation.indexOf('public static class RebirthBackpackLibraryReservation'))).replace('// CURSOR_PREFIX',prefix).replace('// ACTION_PREFIX',actionPrefix).replace('// INSTANT_METHODS',instantMethods.join('\n')).replace('// PARTIAL_STACK_PREFIX',partialPrefix).replace('// BAG_ADD_PREFIX',addPrefix).replace('// BELT_PLACEMENT_PREFIX',beltPrefix).replace('// BELT_ADD_PREFIX',beltAddPrefix).replace('// BELT_PARTIAL_PREFIX',beltPartialPrefix).replace('// MATERIAL_PREFIX',materialPrefix).replace('// COOKING_INGREDIENTS_CLASS',cookingIngredients.slice(cookingIngredients.indexOf('public static class RebirthCookingLocalIngredients'))));
 await run('C:/Program Files/dotnet/dotnet.exe',[
 'C:/Program Files/dotnet/sdk/9.0.301/Roslyn/bincore/csc.dll','/nologo','/target:exe','/out:'+exe,
 '/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/mscorlib.dll',
 '/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/System.dll',
 '/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/System.Core.dll','/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/System.Xml.dll','/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/System.Xml.Linq.dll',cs],{windowsHide:true,timeout:10000});
 const result=await run(exe,[],{windowsHide:true,timeout:10000});
 console.log(result.stdout.trim());
} finally {
 for(const name of ['check.cs','check.exe']) await unlink(join(temp,name)).catch(e=>{if(e.code!=='ENOENT')throw e;});
 await rmdir(temp);
}





