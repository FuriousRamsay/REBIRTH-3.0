using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using UnityEngine;

// Hiring uses the actual owner's native inventory. The server never debits a
// remote EntityPlayer replica. Its retained before/after plan is independently
// checked against native saved player data before ownership can be published.
internal static class RebirthSurvivorHirePayment
{
    internal const string PaidPrefix = "rbSurvivorHire_";
    private const int MaximumCells = 32;
    private sealed class Cell
    {
        internal bool Bag; internal int Slot, Before, After; internal string Item;
        internal XElement Write() => new XElement("cell", new XAttribute("bag",Bag?1:0),
            new XAttribute("slot",Slot),new XAttribute("before",Before),new XAttribute("after",After),new XAttribute("item",Item));
    }
    private sealed class Original
    {
        internal Guid Operation; internal string World, Owner, Actor, Npc, Creation, Currency;
        internal int Deaths, Cost, OwnedBelt; internal bool Settled; internal List<Cell> Cells=new List<Cell>();
        internal string Marker => PaidPrefix+Operation.ToString("N");
        internal XElement Write() => new XElement("survivorHire",new XAttribute("version",1),
            new XAttribute("operation",Operation.ToString("N")),new XAttribute("world",World),new XAttribute("owner",Owner),
            new XAttribute("actor",Actor),new XAttribute("npc",Npc),new XAttribute("creation",Creation),
            new XAttribute("deaths",Deaths),new XAttribute("cost",Cost),new XAttribute("currency",Currency),
            new XAttribute("belt",OwnedBelt),new XAttribute("settled",Settled?1:0),Cells.Select(c=>c.Write()));
        internal static Original Read(XElement x)
        {
            if(x==null||x.Name!="survivorHire"||(int)x.Attribute("version")!=1)throw new InvalidDataException("Hire original format");
            var o=new Original {Operation=Guid.ParseExact((string)x.Attribute("operation"),"N"),World=(string)x.Attribute("world"),
                Owner=(string)x.Attribute("owner"),Actor=(string)x.Attribute("actor"),Npc=(string)x.Attribute("npc"),Creation=(string)x.Attribute("creation"),
                Deaths=(int)x.Attribute("deaths"),Cost=(int)x.Attribute("cost"),Currency=(string)x.Attribute("currency"),
                OwnedBelt=(int)x.Attribute("belt"),Settled=(int)x.Attribute("settled")==1};
            foreach(var c in x.Elements("cell"))o.Cells.Add(new Cell {Bag=(int)c.Attribute("bag")==1,Slot=(int)c.Attribute("slot"),
                Before=(int)c.Attribute("before"),After=(int)c.Attribute("after"),Item=(string)c.Attribute("item")});
            if(o.Operation==Guid.Empty||string.IsNullOrEmpty(o.World)||string.IsNullOrEmpty(o.Owner)||string.IsNullOrEmpty(o.Actor)||
                string.IsNullOrEmpty(o.Npc)||o.Cost<0||o.Cost>10000000||o.OwnedBelt<0||o.OwnedBelt>20||
                o.Cells.Count>MaximumCells||o.Cells.Any(c=>c.Slot<0||c.Slot>168||!c.Bag&&c.Slot>=o.OwnedBelt||c.Before<=0||c.After<0||c.After>=c.Before||
                    string.IsNullOrEmpty(c.Item)||c.Item.Length>4096)||o.Cells.GroupBy(c=>(c.Bag?"b":"t")+c.Slot).Any(g=>g.Count()!=1)||
                o.Cells.Sum(c=>(long)c.Before-c.After)!=o.Cost)throw new InvalidDataException("Hire original bounds");
            return o;
        }
    }

    internal static bool Eligible(EntityRebirthNPC npc) => npc!=null &&
        (npc.RebirthProfileId=="survivor.ambient"||npc.RebirthProfileId=="survivor.persistent");

    internal static RebirthNpcInteractionCommandResponse Handle(RebirthNpcInteractionContext context, RebirthNpcInteractionCommand command)
    {
        try
        {
            var actor=RebirthNpcInteractionMutationHandlers.FindActorPlayer(context.ActorId);
            var world=GameManager.Instance?.World;
            if(!ThreadManager.IsMainThread()||world==null||world.IsRemote()||actor==null||actor.IsDead()||
                !RebirthNpcRuntimeRegistry.TryGetEntityId(context.NpcId,out int entity)||world.GetEntity(entity) is not EntityRebirthNPC npc||!Eligible(npc))
                return No("This survivor is unavailable for hiring.");
            var values=Parse(command.Payload);values.TryGetValue("operation",out var operation);
            if(operation=="commit")return Commit(context,actor,npc,values);
            if(!RebirthNpcTradeReach.CanBegin(actor,context.NpcId))return No("Move closer to the survivor.");
            if(!Price(npc,actor,out int cost,out string currency,out string detail))return No(detail);
            Original retained;
            if(TryLoad(context.NpcId,out retained)&&!retained.Settled)
            {
                if(!Matches(retained,actor,context))return No("This survivor has another retained hiring payment.");
                cost=retained.Cost;currency=retained.Currency;
            }
            if(operation!="prepare")return Yes("Hire for "+cost+" "+Localization.Get(currency)+"? Select Hire again to confirm.",
                "price="+cost.ToString(CultureInfo.InvariantCulture)+";currency="+currency);
            if(!values.TryGetValue("price",out var quoted)||quoted!=cost.ToString(CultureInfo.InvariantCulture))
                return No("The hiring price changed. Request a new price.");
            Original original;
            if(TryLoad(context.NpcId,out original))
            {
                if(!original.Settled)
                {
                    if(!Matches(original,actor,context))return No("This survivor has another retained hiring payment.");
                    return Prepared(original);
                }
            }
            if(!RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var owner)||!RebirthGearNativePlayerFile.TryRead(owner,out var saved)||
                saved.bDead||saved.id!=actor.entityId||saved.deaths!=actor.Died)return No("Waiting for your native player save. Select Hire again.");
            original=new Original {Operation=Guid.NewGuid(),World=world.worldState.Guid,Owner=owner.CanonicalId,Actor=context.ActorId,
                Npc=context.NpcId.ToString(),Creation=RebirthCamperCharacterAccess.CurrentCreation(actor),Deaths=actor.Died,Cost=cost,Currency=currency,
                OwnedBelt=RebirthToolbeltCapacity.GetOwnedSlotCount(actor,RebirthGearPlayerDataInventory.ReadSlots(saved,false).Length)};
            var bag=RebirthGearPlayerDataInventory.ReadSlots(saved,true);var belt=RebirthGearPlayerDataInventory.ReadSlots(saved,false);
            var item=ItemClass.GetItem(currency,false);int left=cost;
            AddCells(original,bag,true,bag.Length,item.type,ref left);AddCells(original,belt,false,original.OwnedBelt,item.type,ref left);
            if(left!=0||original.Cells.Count>MaximumCells)return No("You do not have enough available "+Localization.Get(currency)+" in your saved inventory.");
            Save(context.NpcId,original);return Prepared(original);
        }
        catch(Exception ex){Log.Warning("[REBIRTH Survivor Hire] "+ex.GetType().Name+": "+ex.Message);return No("Hiring is retained until its saved payment can be verified. No replacement payment was requested.");}
    }
    private static RebirthNpcInteractionCommandResponse Commit(RebirthNpcInteractionContext context,EntityPlayer actor,EntityRebirthNPC npc,Dictionary<string,string> values)
    {
        if(!TryLoad(context.NpcId,out var original))return No("The original hiring payment is unavailable.");
        if(!values.TryGetValue("id",out var id)||id!=original.Operation.ToString("N")||!Matches(original,actor,context))return No("Hiring payment does not belong to this character and survivor.");
        if(original.Settled)return No("This hiring payment has already been settled.");
        if(!RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var owner)||!RebirthGearNativePlayerFile.TryRead(owner,out var saved)||
            saved.bDead||saved.deaths!=original.Deaths||
            !RebirthRemoteResourcePlacedStationSavedReceipt.TryRead(saved,original.Marker,out float paid,out bool present)||!present||paid!=2f||
            !Frame(original,RebirthGearPlayerDataInventory.ReadSlots(saved,true),RebirthGearPlayerDataInventory.ReadSlots(saved,false),true))
            return No("Waiting for your saved hiring payment. Select Hire again to continue; you will not be charged twice.");
        RebirthNpcHireRecord hire;
        var result=RebirthNpcHireService.HirePaid(context.SessionKey,context.RuntimeRevision,out hire);
        if(result!=RebirthNpcInteractionResult.Allowed)return No("Your payment is retained; hiring will resume when the survivor is available ("+result+").");
        original.Settled=true;Save(context.NpcId,original);
        return Yes(Localization.Get("xuiRebirthNpcHired"),"settled=1");
    }
    private static bool Matches(Original o,EntityPlayer actor,RebirthNpcInteractionContext c)=>o.World==actor.world.worldState.Guid&&
        o.Actor==c.ActorId&&o.Npc==c.NpcId.ToString()&&o.Deaths==actor.Died&&o.Creation==RebirthCamperCharacterAccess.CurrentCreation(actor)&&
        RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var owner)&&owner.CanonicalId==o.Owner;
    private static void AddCells(Original o,ItemStack[] slots,bool bag,int count,int type,ref int left)
    {
        for(int i=0;i<count&&left>0;i++)
        {
            var s=slots[i];if(s==null||s.IsEmpty()||s.itemValue.type!=type||!RebirthNpcPlayerInventoryEndpoint.IsFungible(s.itemValue))continue;
            int take=Math.Min(s.count,left);left-=take;o.Cells.Add(new Cell {Bag=bag,Slot=i,Before=s.count,After=s.count-take,Item=RebirthNativeItemCodec.Encode(s.itemValue)});
        }
    }
    private static bool Frame(Original o,ItemStack[] bag,ItemStack[] belt,bool after)
    {
        foreach(var c in o.Cells)
        {
            var slots=c.Bag?bag:belt;if(slots==null||c.Slot>=slots.Length||!CellMatches(c,slots[c.Slot],after))return false;
        }
        return true;
    }
    private static bool CellMatches(Cell c,ItemStack s,bool after)
    {
        int count=after?c.After:c.Before;
        return s!=null&&(count==0?s.IsEmpty():s.count==count&&!s.IsEmpty()&&RebirthNativeItemCodec.Encode(s.itemValue)==c.Item);
    }
    // Called only by the actual local interaction owner after a server preparation.
    // Each cell is an immutable before/after pair; resuming a partially saved debit
    // never subtracts from an already-after cell a second time.
    internal static bool TryPayLocal(EntityPlayerLocal player,RebirthNpcInteractionProjection projection,string payload,string confirmedPrice,out string operation,out string error)
    {
        operation=null;error="The original payment is unavailable.";
        try
        {
            var values=Parse(payload);if(!values.TryGetValue("prepared",out var encoded)||encoded.Length>60000)return false;
            var o=Original.Read(XElement.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(encoded))));
            if(confirmedPrice!=o.Cost.ToString(CultureInfo.InvariantCulture)){error="Confirm the original hiring price before paying.";return false;}
            if(!ThreadManager.IsMainThread()||player==null||player.IsDead()||player.Died!=o.Deaths||projection==null||projection.NpcId.ToString()!=o.Npc||projection.ActorId!=o.Actor||
                !ReferenceEquals(GameManager.Instance?.World,player.world)||!ReferenceEquals(player.world.GetPrimaryPlayer(),player)||
                player.world.worldState.Guid!=o.World||RebirthCamperCharacterAccess.CurrentCreation(player)!=o.Creation||
                !RebirthStablePlayerIdentity.TryFromLocalPlatform(out var owner)||owner.CanonicalId!=o.Owner||
                RebirthToolbeltCapacity.GetOwnedSlotCount(player,player.inventory.ItemGrid.items.Length)!=o.OwnedBelt)return false;
            operation=o.Operation.ToString("N");
            var bag=player.bag.ItemGrid;var belt=player.inventory.ItemGrid;float phase=player.Buffs.GetCustomVar(o.Marker);
            if(phase==2f){error=null;GameManager.Instance.SaveLocalPlayerData();return true;}
            if(phase!=0f&&phase!=1f)return false;
            if(phase==0f&&!Frame(o,bag.items,belt.items,false)){error="Your inventory changed. Restore the quoted currency slots and select Hire again.";return false;}
            foreach(var c in o.Cells)
            {
                var grid=c.Bag?bag:belt;
                if(c.Slot>=grid.items.Length||!CellMatches(c,grid.items[c.Slot],false)&&!(phase==1f&&CellMatches(c,grid.items[c.Slot],true)))return false;
            }
            player.Buffs.SetCustomVar(o.Marker,1f,true);GameManager.Instance.SaveLocalPlayerData();
            foreach(var c in o.Cells)
            {
                var grid=c.Bag?bag:belt;if(CellMatches(c,grid.items[c.Slot],true))continue;
                if(!CellMatches(c,grid.items[c.Slot],false))return false;
                var next=c.After==0?ItemStack.Empty.Clone():grid.items[c.Slot].Clone();next.count=c.After;
                grid.SetItem(c.Slot,next);
                if(!CellMatches(c,grid.items[c.Slot],true))return false;
            }
            if(!Frame(o,bag.items,belt.items,true))return false;
            player.Buffs.SetCustomVar(o.Marker,2f,true);GameManager.Instance.SaveLocalPlayerData();error=null;return true;
        }
        catch(Exception ex){error="Your original payment is retained for continuation: "+ex.GetType().Name;return false;}
    }
    private static bool Price(EntityRebirthNPC npc,EntityPlayer player,out int cost,out string currency,out string error)
    {
        var p=npc.EntityClass.Properties;currency=p.GetString("HireCurrency");if(string.IsNullOrEmpty(currency))currency="casinoCoin";
        cost=0;error="Hiring terms are invalid.";
        if(!int.TryParse(p.GetString("HireCost"),out int baseCost))baseCost=500;
        if(!int.TryParse(p.GetString("PlayerLevelRestriction"),out int minLevel))minLevel=1;
        if(baseCost<0||baseCost>10000000||minLevel<1)return false;
        if(player.Progression.Level<minLevel){error="Reach player level "+minLevel+" before hiring this survivor.";return false;}
        float raw=npc.Buffs.GetCustomVar("$FR_NPC_Level");if(float.IsNaN(raw)||float.IsInfinity(raw)||raw<0||raw>10000)return false;
        float discount=1f;
        if(Progression.ProgressionClasses!=null&&Progression.ProgressionClasses.ContainsKey("perkBetterBarter"))
            discount-=Math.Min(5,Math.Max(0,player.Progression.GetProgressionValue("perkBetterBarter").Level))*.02f;
        if(Progression.ProgressionClasses!=null&&Progression.ProgressionClasses.ContainsKey("FuriousRamsayAchievementTradeAura"))
        {
            int aura=player.Progression.GetProgressionValue("FuriousRamsayAchievementTradeAura").Level;
            discount-=aura<=0?0f:aura==1?.2f:aura==2?.3f:.4f;
        }
        long gross=baseCost+500L*(int)raw;if(gross>10000000)return false;
        cost=(int)(gross*Math.Max(.5f,discount));var item=ItemClass.GetItem(currency,false);
        if(item==null||item.IsEmpty()||!RebirthNpcPlayerInventoryEndpoint.IsFungible(item))return false;
        error=null;return true;
    }
    private static bool TryLoad(RebirthNpcStableId id,out Original original)
    {
        original=null;
        if(!RebirthNpcAggregatePersistenceStore.TryGet(id,out var person))return false;
        foreach(var text in person.UnknownOptionalElements??new string[0])
        {
            if(text==null||text.Length>100000)throw new InvalidDataException("Hire extension bounds");
            using(var input=new StringReader(text))
            using(var reader=System.Xml.XmlReader.Create(input,new System.Xml.XmlReaderSettings {DtdProcessing=System.Xml.DtdProcessing.Prohibit,XmlResolver=null}))
            {
                var element=XElement.Load(reader);if(element.Name!="survivorHire")continue;
                if(original!=null)throw new InvalidDataException("Duplicate hire original");original=Original.Read(element);
            }
        }
        return original!=null;
    }
    private static void Save(RebirthNpcStableId id,Original o)
    {
        string image=o.Write().ToString(SaveOptions.DisableFormatting);
        // The optional extension participates in the existing aggregate checkpoint,
        // backup and recovery generation; it is not a second economy sidecar.
        RebirthNpcAggregatePersistenceStore.CaptureRuntimeEnvelopes();
        if(!RebirthNpcAggregatePersistenceStore.Mutate(id,person=>
        {
            var extensions=(person.UnknownOptionalElements??new string[0]).Where(text=>XElement.Parse(text).Name!="survivorHire").ToList();
            extensions.Add(image);person.UnknownOptionalElements=extensions.ToArray();
        }))throw new IOException("Hire original publication unavailable");
        RebirthNpcAggregatePersistenceStore.Save();
        if(!TryLoad(id,out var saved)||saved.Write().ToString(SaveOptions.DisableFormatting)!=image||
            !RebirthNpcAggregatePersistenceStore.TryReadSavedPerson(id,out var published)||
            !(published.UnknownOptionalElements??new string[0]).Contains(image))throw new IOException("Hire original primary readback");
    }
    private static RebirthNpcInteractionCommandResponse Prepared(Original o)=>Yes("Your confirmed hiring payment is ready.",
        "prepared="+Convert.ToBase64String(Encoding.UTF8.GetBytes(o.Write().ToString(SaveOptions.DisableFormatting))));
    internal static Dictionary<string,string> Parse(string text)
    {
        var result=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        foreach(var s in (text??string.Empty).Split(';')){int i=s.IndexOf('=');if(i>0)result[s.Substring(0,i)]=s.Substring(i+1);}return result;
    }
    private static RebirthNpcInteractionCommandResponse Yes(string detail,string payload)=>new RebirthNpcInteractionCommandResponse {Status=RebirthNpcInteractionCommandStatus.Accepted,Detail=detail,ResponsePayload=payload};
    private static RebirthNpcInteractionCommandResponse No(string detail)=>new RebirthNpcInteractionCommandResponse {Status=RebirthNpcInteractionCommandStatus.Rejected,Detail=detail,ResponsePayload=string.Empty};
}
