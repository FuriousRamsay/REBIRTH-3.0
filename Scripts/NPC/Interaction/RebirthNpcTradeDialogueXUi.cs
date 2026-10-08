using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine.Scripting;

#nullable disable

public static class RebirthNpcInteractionPayloadCodec
{
    public static List<string> SplitEscaped(string value, char delimiter)
    {
        List<string> result=new List<string>(); if(value==null){result.Add(string.Empty);return result;}
        int start=0; bool escaped=false;
        for(int i=0;i<value.Length;i++)
        {
            char c=value[i];
            if(escaped){escaped=false;continue;}
            if(c=='\\'){escaped=true;continue;}
            if(c==delimiter){result.Add(value.Substring(start,i-start));start=i+1;}
        }
        result.Add(value.Substring(start)); return result;
    }
    public static int IndexOfUnescaped(string value,char delimiter)
    {
        bool escaped=false; for(int i=0;i<(value??string.Empty).Length;i++)
        {char c=value[i];if(escaped){escaped=false;continue;}if(c=='\\'){escaped=true;continue;}if(c==delimiter)return i;}return -1;
    }
    public static string UnescapeBackslash(string value)
    {
        if(string.IsNullOrEmpty(value))return string.Empty;System.Text.StringBuilder b=new System.Text.StringBuilder(value.Length);bool escaped=false;
        for(int i=0;i<value.Length;i++){char c=value[i];if(escaped){b.Append(c);escaped=false;}else if(c=='\\')escaped=true;else b.Append(c);}if(escaped)b.Append('\\');return b.ToString();
    }
    public static Dictionary<string,string> ParseBackslashMap(string payload)
    {
        Dictionary<string,string> result=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        List<string> parts=SplitEscaped(payload,';'); if(parts.Count>32)return result;
        for(int i=0;i<parts.Count;i++){int at=IndexOfUnescaped(parts[i],'=');if(at<=0)continue;string key=UnescapeBackslash(parts[i].Substring(0,at));string value=UnescapeBackslash(parts[i].Substring(at+1));if(key.Length>0)result[key]=value;}return result;
    }
}

public static class RebirthNpcTradeDialogueUiService
{
    public const string TradeGroup="rebirthNpcTrade",DialogueGroup="rebirthNpcDialogue";
    public static bool OpenTrade(XUi xui,RebirthNpcInteractionProjection p,out string error){return Open<XUiC_RebirthNpcTrade>(xui,TradeGroup,p,out error);}
    public static bool OpenDialogue(XUi xui,RebirthNpcInteractionProjection p,out string error){return Open<XUiC_RebirthNpcDialogue>(xui,DialogueGroup,p,out error);}
    private static bool Open<T>(XUi xui,string group,RebirthNpcInteractionProjection p,out string error) where T:XUiController,IRebirthNpcProjectedWindow{error=string.Empty;if(xui==null||p==null){error="Interaction projection is unavailable.";return false;}XUiController g=xui.FindWindowGroupByName(group);T c=g!=null?g.GetChildByType<T>():null;if(c==null){error="NPC window is not registered: "+group;return false;}c.Prepare(p);xui.playerUI.windowManager.Open((GUIWindow)c.windowGroup,false);return true;}
}
public interface IRebirthNpcProjectedWindow{void Prepare(RebirthNpcInteractionProjection projection);}

[Preserve] public sealed class XUiC_RebirthNpcDialogue:XUiController,IRebirthNpcProjectedWindow
{
    private sealed class Topic{public string Id,Label;}
    private RebirthNpcInteractionProjection projection;private XUiV_Label speaker,disposition,response,feedback;private readonly XUiV_Label[] topicLabels=new XUiV_Label[6];private readonly Topic[] topics=new Topic[6];private Guid conversation;private long sequence;private int generation;
    public override void Init(){base.Init();speaker=L("speaker");disposition=L("disposition");response=L("response");feedback=L("feedback");for(int i=0;i<6;i++){int index=i;topicLabels[i]=L("topicLabel"+(i+1));XUiController c=GetChildById("topic"+(i+1));if(c!=null)c.OnPress+=delegate{Select(index);};}XUiController close=GetChildById("btnClose");if(close!=null)close.OnPress+=delegate{EndAndClose();};}
    public void Prepare(RebirthNpcInteractionProjection p){generation++;projection=p;conversation=Guid.Empty;for(int i=0;i<topics.Length;i++){topics[i]=null;Set(topicLabels[i],string.Empty);}Set(speaker,p.DisplayName);Set(response,string.Empty);Set(feedback,string.Empty);OpenConversation();}
    private void OpenConversation(){Dispatch("operation=open",delegate(Dictionary<string,string> v){Guid.TryParse(Get(v,"conversation"),out conversation);Set(speaker,Get(v,"speaker"));Set(disposition,Get(v,"disposition"));Set(response,Get(v,"opening"));ApplyTopics(Get(v,"topics"));});}
    private void ApplyTopics(string raw){for(int i=0;i<topics.Length;i++){topics[i]=null;Set(topicLabels[i],string.Empty);}if(string.IsNullOrWhiteSpace(raw))return;List<string> entries=RebirthNpcInteractionPayloadCodec.SplitEscaped(raw,',');for(int i=0;i<entries.Count&&i<topics.Length;i++){int at=RebirthNpcInteractionPayloadCodec.IndexOfUnescaped(entries[i],':');string id=at>0?entries[i].Substring(0,at):entries[i];string label=at>0?entries[i].Substring(at+1):id;topics[i]=new Topic{Id=RebirthNpcInteractionPayloadCodec.UnescapeBackslash(id),Label=RebirthNpcInteractionPayloadCodec.UnescapeBackslash(label)};Set(topicLabels[i],topics[i].Label);}}
    private void Select(int index){Topic topic=index>=0&&index<topics.Length?topics[index]:null;if(conversation==Guid.Empty||topic==null)return;Dispatch("operation=select;conversation="+conversation.ToString("N")+";topic="+topic.Id,delegate(Dictionary<string,string> v){Set(disposition,Get(v,"disposition"));Set(response,Get(v,"text"));});}
    private void EndAndClose(){if(conversation!=Guid.Empty)Dispatch("operation=end;conversation="+conversation.ToString("N"),null);generation++;xui.playerUI.windowManager.Close((GUIWindow)windowGroup);}
    private void Dispatch(string payload,Action<Dictionary<string,string>> accepted){if(projection==null)return;int callbackGeneration=generation;string callbackSession=projection.SessionKey;RebirthNpcInteractionCommand c=new RebirthNpcInteractionCommand{RequestId=callbackSession+":dialogue:"+(++sequence),SessionKey=callbackSession,Kind=RebirthNpcInteractionCommandKind.Dialogue,Payload=payload,ExpectedRevision=projection.RuntimeRevision};RebirthNpcInteractionNetworkClient.RequestCommand(c,delegate(RebirthNpcInteractionProjection refreshed,RebirthNpcInteractionCommandResponse r,string error){if(callbackGeneration!=generation||projection==null||!string.Equals(projection.SessionKey,callbackSession,StringComparison.Ordinal))return;if(refreshed!=null){if(!string.Equals(refreshed.SessionKey,callbackSession,StringComparison.Ordinal))return;projection=refreshed;}Set(feedback,!string.IsNullOrEmpty(error)?error:(r!=null?r.Detail:"No response."));if(r!=null&&r.Status==RebirthNpcInteractionCommandStatus.Accepted&&accepted!=null)accepted(Parse(r.ResponsePayload));});}
    private static Dictionary<string,string> Parse(string p){Dictionary<string,string>d=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);if(string.IsNullOrEmpty(p))return d;foreach(string part in p.Split(';')){int i=part.IndexOf('=');if(i>0)d[part.Substring(0,i)]=part.Substring(i+1);}return d;}private static string Unescape(string s){return(s??string.Empty).Replace("\\:",":").Replace("\\,",",").Replace("\\=","=").Replace("\\;",";").Replace("\\\\","\\");}private static string Get(Dictionary<string,string>d,string k){string v;return d.TryGetValue(k,out v)?Unescape(v):string.Empty;}private XUiV_Label L(string id){XUiController c=GetChildById(id);return c!=null?c.ViewComponent as XUiV_Label:null;}private static void Set(XUiV_Label l,string v){if(l!=null)l.Text=v??string.Empty;}
}

[Preserve] public sealed class XUiC_RebirthNpcTrade:XUiController,IRebirthNpcProjectedWindow
{
    private sealed class Offer{public string Item,Currency;public int Buy,Sell,Max;}
    private RebirthNpcInteractionProjection projection;private XUiV_Label npcName,selection,quote,feedback;private XUiC_TextInput quantity;private readonly XUiV_Label[] offerLabels=new XUiV_Label[6];private readonly Offer[] offers=new Offer[6];private Offer selected;private string endpoint=string.Empty;private Guid quoteId;private long sequence;private int generation;
    public override void Init(){base.Init();npcName=L("npcName");selection=L("selection");quote=L("quote");feedback=L("feedback");quantity=T("quantity");for(int i=0;i<6;i++){int index=i;offerLabels[i]=L("offerLabel"+(i+1));XUiController c=GetChildById("offer"+(i+1));if(c!=null)c.OnPress+=delegate{Select(index);};}Wire("btnBuyQuote",delegate{Quote("BuyFromNpc");});Wire("btnSellQuote",delegate{Quote("SellToNpc");});Wire("btnCommit",Commit);Wire("btnRefresh",LoadCatalog);Wire("btnClose",delegate{generation++;xui.playerUI.windowManager.Close((GUIWindow)windowGroup);});}
    public void Prepare(RebirthNpcInteractionProjection p){generation++;projection=p;quoteId=Guid.Empty;selected=null;Set(npcName,p.DisplayName);Set(selection,string.Empty);Set(quote,string.Empty);Set(feedback,Localization.Get("xuiRebirthNpcTradeInstructions"));LoadCatalog();}
    private void LoadCatalog(){Dispatch("operation=catalog",delegate(Dictionary<string,string> v){endpoint=Get(v,"endpoint");ApplyOffers(GetRaw(v,"offers"));});}
    private void ApplyOffers(string raw){for(int i=0;i<offers.Length;i++){offers[i]=null;Set(offerLabels[i],string.Empty);}if(string.IsNullOrWhiteSpace(raw)){Set(feedback,Localization.Get("xuiRebirthNpcTradeNoOffers"));return;}string[] list=raw.Split(',');for(int i=0;i<list.Length&&i<offers.Length;i++){string[] f=list[i].Split(':');if(f.Length<5)continue;int buy,sell,max;if(!int.TryParse(f[1],out buy)||!int.TryParse(f[2],out sell)||!int.TryParse(f[3],out max))continue;string item=Decode(f[0]),currency=Decode(f[4]);offers[i]=new Offer{Item=item,Buy=buy,Sell=sell,Max=max,Currency=currency};Set(offerLabels[i],item+"  B:"+buy+" S:"+sell+"  max "+max);}if(offers[0]!=null)Select(0);}
    private void Select(int index){selected=index>=0&&index<offers.Length?offers[index]:null;quoteId=Guid.Empty;Set(quote,string.Empty);Set(selection,selected==null?string.Empty:selected.Item+" | "+selected.Currency);}
    private void Quote(string direction){if(selected==null){Set(feedback,Localization.Get("xuiRebirthNpcTradeSelectOffer"));return;}int q;string qs=quantity!=null?quantity.Text:string.Empty;if(!int.TryParse(qs,out q)||q<=0||q>selected.Max){Set(feedback,Localization.Get("xuiRebirthNpcTradeQuantityInvalid"));return;}Dispatch("operation=quote;endpoint="+Encode(endpoint)+";item="+Encode(selected.Item)+";direction="+direction+";quantity="+q.ToString(CultureInfo.InvariantCulture),delegate(Dictionary<string,string> v){Guid.TryParse(Get(v,"quote"),out quoteId);Set(quote,FormatQuote(v));});}
    private string FormatQuote(Dictionary<string,string> v){return Get(v,"item")+" x"+Get(v,"quantity")+" | "+Get(v,"total")+" "+Get(v,"currency")+" | "+Get(v,"direction");}
    private void Commit(){if(quoteId==Guid.Empty){Set(feedback,Localization.Get("xuiRebirthNpcTradeQuoteRequired"));return;}Dispatch("operation=commit;quote="+quoteId.ToString("N"),delegate(Dictionary<string,string> v){quoteId=Guid.Empty;Set(quote,string.Empty);LoadCatalog();});}
    private void Dispatch(string payload,Action<Dictionary<string,string>> accepted){if(projection==null)return;int callbackGeneration=generation;string callbackSession=projection.SessionKey;RebirthNpcInteractionCommand c=new RebirthNpcInteractionCommand{RequestId=callbackSession+":trade:"+(++sequence),SessionKey=callbackSession,Kind=RebirthNpcInteractionCommandKind.Trade,Payload=payload,ExpectedRevision=projection.RuntimeRevision};RebirthNpcInteractionNetworkClient.RequestCommand(c,delegate(RebirthNpcInteractionProjection refreshed,RebirthNpcInteractionCommandResponse r,string error){if(callbackGeneration!=generation||projection==null||!string.Equals(projection.SessionKey,callbackSession,StringComparison.Ordinal))return;if(refreshed!=null){if(!string.Equals(refreshed.SessionKey,callbackSession,StringComparison.Ordinal))return;projection=refreshed;}Set(feedback,!string.IsNullOrEmpty(error)?error:(r!=null?r.Detail:"No response."));if(r!=null&&r.Status==RebirthNpcInteractionCommandStatus.Accepted&&accepted!=null)accepted(Parse(r.ResponsePayload));});}
    private void Wire(string id,Action action){XUiController c=GetChildById(id);if(c!=null)c.OnPress+=delegate{action();};}private static Dictionary<string,string> Parse(string p){Dictionary<string,string>d=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);if(string.IsNullOrEmpty(p))return d;foreach(string part in p.Split(';')){int i=part.IndexOf('=');if(i>0)d[part.Substring(0,i)]=part.Substring(i+1);}return d;}private static string GetRaw(Dictionary<string,string>d,string k){string v;return d.TryGetValue(k,out v)?v:string.Empty;}private static string Get(Dictionary<string,string>d,string k){return Decode(GetRaw(d,k));}private static string Decode(string v){return(v??string.Empty).Replace("%3A",":").Replace("%2C",",").Replace("%3D","=").Replace("%3B",";").Replace("%25","%");}private static string Encode(string v){return(v??string.Empty).Replace("%","%25").Replace(";","%3B").Replace("=","%3D").Replace(",","%2C").Replace(":","%3A");}private XUiV_Label L(string id){XUiController c=GetChildById(id);return c!=null?c.ViewComponent as XUiV_Label:null;}private XUiC_TextInput T(string id){return GetChildById(id) as XUiC_TextInput;}private static void Set(XUiV_Label l,string v){if(l!=null)l.Text=v??string.Empty;}
}
