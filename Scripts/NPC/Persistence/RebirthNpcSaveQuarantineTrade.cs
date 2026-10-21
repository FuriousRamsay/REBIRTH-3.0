using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml;

public static partial class RebirthNpcTradeService
{
    private static World tradeWorld;
    private static bool CanPruneQuote(QuoteRecord record,long now)
    {return record!=null&&record.Quote!=null&&!localAttempts.ContainsKey(record.Quote.QuoteId)&&
        (record.OwnerUnused==null||record.Resolution==2&&record.OwnerUnused.ReadRetained())&&(record.OwnerPreparation==null||record.Resolution==1&&record.OwnerPreparation.ClosedRecord?.HasObservedSavedSocialCompletion==true)&&
        (record.Resolution!=0||!record.Consumed&&record.Quote.ExpiresClockTicks<=now);}
    private static bool TradeScope(bool bind=true)
    {
        var world=GameManager.Instance?.World;if(!ThreadManager.IsMainThread()||world?.IsRemote()!=false||SingletonMonoBehaviour<ConnectionManager>.Instance?.IsServer!=true||RebirthNpcPersistenceCoordinator.IsRecoveryInstalling)return false;
        lock(Sync){if(tradeWorld!=null&&!ReferenceEquals(tradeWorld,world))return false;if(tradeWorld==null){if(Quotes.Count!=0||QuoteOrder.Count!=0)return false;if(bind)tradeWorld=world;}return true;}
    }
    internal static bool CanRetireEmptyTrade{get{lock(Sync)return ThreadManager.IsMainThread()&&Quotes.Count==0&&QuoteOrder.Count==0&&localAttempts.Count==0;}}
    internal static bool CanBeginFreshGeneration(World world)
    {lock(Sync){if(localAttempts.Count!=0||RebirthNpcTradeLocalOwnerLoad.HasLifecycleCustody||!RebirthNpcTradeColdFence.CanBeginFreshGeneration(world)||tradeWorld!=null&&!ReferenceEquals(tradeWorld,world))return false;foreach(var record in Quotes.Values)if(record==null||record.Quote==null||record.Consumed&&record.Resolution==0)return false;return true;}}
    internal static void AppendQuarantineIds(HashSet<RebirthNpcStableId> ids)
    {RebirthNpcTradeColdFence.AppendQuarantineIds(ids);lock(Sync){foreach(var record in Quotes.Values){if(record?.Quote==null)throw new InvalidOperationException("Quote original missing");RebirthNpcSaveQuarantineIds.Add(ids,record.Quote.NpcId);}foreach(var value in localAttempts.Values){if(value?.Original==null)throw new InvalidOperationException("Local trade original missing");RebirthNpcSaveQuarantineIds.Add(ids,value.Original.CopyQuote().NpcId);}}}
    internal static RebirthNpcSaveQuarantineDomain PrepareTradeQuarantine(RebirthNpcSaveQuarantineScope scope)
    {
        lock(Sync)
        {
            if(!ThreadManager.IsMainThread()||scope==null||(tradeWorld!=null&&!ReferenceEquals(tradeWorld,scope.World))||(tradeWorld==null&&!CanRetireEmptyTrade))throw new InvalidOperationException("Original trade binding unavailable");
            var records=Quotes;var order=QuoteOrder;var world=tradeWorld;long socialTick=nextClosedSocialTick;var next=new Dictionary<Guid,QuoteRecord>();var nextOrder=new RebirthNpcFairRoundRobin<Guid>();var doc=TradeImage();
            string quoteImage=doc.OuterXml;var cold=RebirthNpcTradeColdFence.PrepareQuarantine(scope);doc.DocumentElement.AppendChild(cold.CopyEvidence(doc));
            var local=PrepareLocalOriginalQuarantine(scope.World);var owner=RebirthNpcTradeOwnerExchange.PrepareQuarantine(scope.World);
            var nativeLocal=RebirthNpcTradeLocalOwnerLoad.PrepareQuarantine(scope.World);
            doc.DocumentElement.AppendChild(local.CopyEvidence(doc));doc.DocumentElement.AppendChild(owner.CopyEvidence(doc));
            doc.DocumentElement.AppendChild(nativeLocal.CopyEvidence(doc));
            return new RebirthNpcSaveQuarantineDomain("trade-quotes",doc,
                ()=>{lock(Sync)return ReferenceEquals(Quotes,records)&&ReferenceEquals(QuoteOrder,order)&&ReferenceEquals(tradeWorld,world)&&TradeImage().OuterXml==quoteImage&&cold.IsCurrent&&local.IsCurrent&&owner.IsCurrent&&nativeLocal.IsCurrent;},
                ()=>{lock(Sync){Quotes=next;QuoteOrder=nextOrder;tradeWorld=null;nextClosedSocialTick=0;cold.Detach();local.Detach();owner.Detach();nativeLocal.Detach();}},
                ()=>{lock(Sync){var errors=new List<Exception>();foreach(var child in new[]{nativeLocal,owner,local,cold})try{child.Rollback();}catch(Exception error){errors.Add(error);}Quotes=records;QuoteOrder=order;tradeWorld=world;nextClosedSocialTick=socialTick;if(errors.Count!=0)throw new AggregateException(errors);}});
        }
    }
    private static XmlDocument TradeImage()
    {
        if(Quotes.Count>MaxQuotes||QuoteOrder.Count!=Quotes.Count)throw new InvalidOperationException("Quote retention bound/index unavailable");var doc=new XmlDocument{XmlResolver=null};var root=doc.CreateElement("tradeQuotes");doc.AppendChild(root);root.SetAttribute("resolutionAuthority","runtime-metadata-only-not-payment");root.SetAttribute("nextClosedSocialTick",nextClosedSocialTick.ToString(CultureInfo.InvariantCulture));long characters=0;
        var keys=new List<Guid>(Quotes.Keys);keys.Sort();foreach(var key in keys)
        {
            var record=Quotes[key];var q=record?.Quote;if(q==null||q.QuoteId!=key||q.NpcId.IsEmpty||record.Resolution>3||!record.Consumed&&record.Resolution!=0)throw new InvalidOperationException("Quote original invalid");var row=doc.CreateElement("quote");root.AppendChild(row);row.SetAttribute("id",key.ToString("N"));row.SetAttribute("npc",q.NpcId.ToString());row.SetAttribute("generation",q.RecoveryGeneration.ToString(CultureInfo.InvariantCulture));row.SetAttribute("consumed",record.Consumed?"1":"0");row.SetAttribute("resolution",record.Resolution.ToString(CultureInfo.InvariantCulture));Text(row,"actor",q.ActorId,ref characters);Text(row,"endpoint",q.EndpointId,ref characters);Text(row,"item",q.ItemKey,ref characters);Text(row,"currency",q.CurrencyItemKey,ref characters);row.SetAttribute("direction",((byte)q.Direction).ToString(CultureInfo.InvariantCulture));row.SetAttribute("quantity",q.Quantity.ToString(CultureInfo.InvariantCulture));row.SetAttribute("unitPrice",q.UnitPrice.ToString(CultureInfo.InvariantCulture));row.SetAttribute("totalPrice",q.TotalPrice.ToString(CultureInfo.InvariantCulture));row.SetAttribute("inventoryRevision",q.ExpectedNpcInventoryRevision.ToString(CultureInfo.InvariantCulture));row.SetAttribute("expires",q.ExpiresClockTicks.ToString(CultureInfo.InvariantCulture));
        }
        int preparationCount=0;
        foreach(var key in keys)
        {
            var issued=Quotes[key].Issued;if(issued==null)continue;
            if(issued.Quote.QuoteId!=key||issued.Image.Length>16384)throw new InvalidOperationException("Issued trade quote original invalid");
            var row=doc.CreateElement("issuedQuote");root.AppendChild(row);row.SetAttribute("id",key.ToString("N"));
            row.SetAttribute("directory",issued.DirectoryName);row.InnerText=issued.Image;
        }
        foreach(var key in keys)
        {
            var unused=Quotes[key].OwnerUnused;if(unused==null)continue;
            if(!Quotes[key].Consumed||Quotes[key].OwnerPreparation!=null||unused.Original.Operation!=key||!unused.Original.MatchesQuote(Quotes[key].Quote))
                throw new InvalidOperationException("Unused trade original custody invalid");
            var row=doc.CreateElement("ownerUnused");root.AppendChild(row);row.SetAttribute("id",key.ToString("N"));
            row.SetAttribute("path",unused.PathName);row.SetAttribute("digest",unused.Digest);row.InnerText=unused.Image;
        }
        foreach(var key in keys)
        {
            var preparation=Quotes[key].OwnerPreparation;if(preparation==null)continue;
            if(preparation.ClosedRecord==null&&++preparationCount>4||!Quotes[key].Consumed||preparation.Original.Operation!=key||
                !preparation.Original.MatchesQuote(Quotes[key].Quote)||preparation.Image.Length>64*1024*1024)
                throw new InvalidOperationException("Trade preparation custody invalid");
            // Keep the original body even if publication was partial or its
            // native source is no longer active. Neither is a payment proof.
            var row=doc.CreateElement("ownerPreparation");root.AppendChild(row);
            row.SetAttribute("id",key.ToString("N"));row.SetAttribute("path",preparation.PathName);
            row.SetAttribute("digest",RebirthRemoteResourceDurableJournal.Digest(preparation.Image));
            row.InnerText=preparation.Image;
            if(preparation.Terminal!=null)
            {
                var terminal=doc.CreateElement("ownerTerminal");root.AppendChild(terminal);
                terminal.SetAttribute("id",key.ToString("N"));terminal.SetAttribute("digest",preparation.Terminal.Digest);
                terminal.InnerText=preparation.Terminal.Image;
            }
            if(preparation.ClosedRecord!=null)
            {
                if(preparation.Terminal==null||Quotes[key].Resolution!=1)throw new InvalidOperationException("Closed trade terminal missing");
                var closed=doc.CreateElement("ownerClosed");root.AppendChild(closed);closed.SetAttribute("id",key.ToString("N"));
                closed.InnerText=preparation.ClosedRecord.Image;
            }
        }
        var seen=new HashSet<Guid>();foreach(var id in QuoteOrder.CopyOrder()){if(!seen.Add(id)||!Quotes.ContainsKey(id))throw new InvalidOperationException("Quote traversal mismatch");var row=doc.CreateElement("order");root.AppendChild(row);row.SetAttribute("id",id.ToString("N"));}return doc;
    }
    private static void Text(XmlElement row,string name,string value,ref long characters)
    {characters+=value?.Length??0;if((value?.Length??0)>65536||characters>4L*1024*1024)throw new InvalidOperationException("Quote text retention bound exceeded");row.SetAttribute(name+"Null",value==null?"1":"0");row.SetAttribute(name,value??string.Empty);}
}
