using System;
using System.Collections.Generic;
using System.Globalization;

// Decode only a response already delivered by the authenticated interaction
// transport. A decoded quote remains a description, never a payment receipt.
internal static class RebirthNpcTradeQuoteWire
{
    internal static bool TryRead(IDictionary<string,string> values,string actor,string npc,string endpoint,out RebirthNpcTradeQuote quote)
    {
        quote=null;
        if(values==null||values.Count!=13)return false;
        string Read(string key)
        {
            if(!values.TryGetValue(key,out var raw)||string.IsNullOrEmpty(raw)||raw.Length>4096)return null;
            var value=raw.Replace("%3A",":").Replace("%2C",",").Replace("%3D","=").Replace("%3B",";").Replace("%25","%");
            var canonical=value.Replace("%","%25").Replace(";","%3B").Replace("=","%3D").Replace(",","%2C").Replace(":","%3A");
            return canonical==raw?value:null;
        }
        string operation=Read("quote"),nativeNpc=Read("npc"),nativeActor=Read("actor"),nativeEndpoint=Read("endpoint"),
            item=Read("item"),currency=Read("currency"),direction=Read("direction");
        if(!Guid.TryParseExact(operation,"N",out var id)||id==Guid.Empty||
            !RebirthNpcStableId.TryParse(nativeNpc,out var npcId)||npcId.ToString()!=nativeNpc||nativeNpc!=npc||
            string.IsNullOrWhiteSpace(nativeActor)||nativeActor!=actor||string.IsNullOrWhiteSpace(nativeEndpoint)||nativeEndpoint!=endpoint||
            string.IsNullOrWhiteSpace(item)||item.Length>512||string.IsNullOrWhiteSpace(currency)||currency.Length>512||
            nativeActor.Length>512||nativeEndpoint.Length>512||direction!="BuyFromNpc"&&direction!="SellToNpc")return false;
        if(!int.TryParse(Read("quantity"),NumberStyles.None,CultureInfo.InvariantCulture,out var quantity)||quantity<=0||
            !int.TryParse(Read("unitPrice"),NumberStyles.None,CultureInfo.InvariantCulture,out var unit)||unit<=0||
            !int.TryParse(Read("total"),NumberStyles.None,CultureInfo.InvariantCulture,out var total)||(long)quantity*unit!=total||
            !uint.TryParse(Read("inventoryRevision"),NumberStyles.None,CultureInfo.InvariantCulture,out var revision)||revision==uint.MaxValue||
            !ulong.TryParse(Read("recoveryGeneration"),NumberStyles.None,CultureInfo.InvariantCulture,out var recovery)||
            !long.TryParse(Read("expiresUtcTicks"),NumberStyles.None,CultureInfo.InvariantCulture,out var expires)||expires<=0)return false;
        quote=new RebirthNpcTradeQuote{QuoteId=id,NpcId=npcId,ActorId=nativeActor,EndpointId=nativeEndpoint,
            Direction=direction=="BuyFromNpc"?RebirthNpcTradeDirection.BuyFromNpc:RebirthNpcTradeDirection.SellToNpc,
            ItemKey=item,CurrencyItemKey=currency,Quantity=quantity,UnitPrice=unit,TotalPrice=total,
            ExpectedNpcInventoryRevision=revision,RecoveryGeneration=recovery,ExpiresClockTicks=expires};
        return true;
    }
}
