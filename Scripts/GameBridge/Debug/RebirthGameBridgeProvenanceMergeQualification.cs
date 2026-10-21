#if REBIRTH_DEBUG
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

// Opt-in detached native qualification; never rewrites real player inventory.
public sealed class ConsoleCmdRebirthProvenanceMergeQualification:ConsoleCmdAbstract
{
    public override string[] getCommands()=>new[]{"rbprovenancetestmerge"};
    public override string getDescription()=>"Qualify actual native non-liquid provenance insertion and sort.";
    public override string getHelp()=>"rbprovenancetestmerge stackableItem liquidItem (local CodexTest Bridge only).";
    public override void Execute(List<string> args,CommandSenderInfo sender)
    {
        var player=GameManager.Instance?.World?.GetPrimaryPlayer() as EntityPlayerLocal;
        if(args.Count!=2||sender.RemoteClientInfo!=null||player==null||player.world.IsRemote()||player.IsDead()||!ThreadManager.IsMainThread()||!RebirthGameBridge.IsRunning||!RebirthProvenanceDetachedReservation.IsDisposableSave())
        {SdtdConsole.Instance.Output("*** ERROR "+getHelp());return;}
        var output=new JObject{["schema"]=1,["deferredFixture"]=true,["nativeMethods"]=true,["reservationScope"]="DEBUG detached-object double; actual production replacement loops, no live/durable reservation",["passed"]=false};
        bool stats=player.bPlayerStatsChanged;string liveBag=Image(player.bag.ItemGrid.items),liveBelt=Image(player.inventory.ItemGrid.items);var cases=new JArray();
        try
        {
            RebirthProvenanceDetachedReservation.Install();
            var plain=ItemClass.GetItem(args[0],true).Clone();var liquid=ItemClass.GetItem(args[1],true).Clone();
            Need(!plain.IsEmpty()&&!plain.ItemClass.HasQuality&&plain.ItemClass.MaxCount>=4&&!RebirthCookingItemStats.IsLiquid(plain),"Initialized non-liquid stackable item required");
            Need(!liquid.IsEmpty()&&RebirthCookingItemStats.IsLiquid(liquid),"Initialized actual liquid required");
            var general=plain.Clone();var different=plain.Clone();
            RebirthItemProvenanceAdapter.Stamp(general,new RebirthItemProvenanceSnapshot{Kind="fixture",SourceId="CodexTest",CreatorStableId="fixture",Workmanship=.25f,BatchToken="one"});
            RebirthItemProvenanceAdapter.Stamp(different,new RebirthItemProvenanceSnapshot{Kind="fixture",SourceId="CodexTest",CreatorStableId="fixture",Workmanship=.75f,BatchToken="two"});
            var electrical=plain.Clone();var otherElectrical=plain.Clone();
            RebirthElectricalItemProvenance.Stamp(electrical,new RebirthElectricalServiceRecord{Condition=25,Workmanship=.25f});
            RebirthElectricalItemProvenance.Stamp(otherElectrical,new RebirthElectricalServiceRecord{Condition=75,Workmanship=.75f});
            var unknownGeneral=plain.Clone();unknownGeneral.SetMetadata("RebirthProvVersion",int.MaxValue);
            var unknownElectrical=plain.Clone();unknownElectrical.SetMetadata("RebirthElectricalProvVersion",int.MaxValue);
            var malformedGeneral=plain.Clone();malformedGeneral.SetMetadata("RebirthProvVersion","invalid-version");
            var malformedElectrical=plain.Clone();malformedElectrical.SetMetadata("RebirthElectricalProvVersion","invalid-version");
            var invalidElectricalKind=electrical.Clone();invalidElectricalKind.SetMetadata("RebirthElectricalProvKind",99);
            var pairs=new[]{new[]{plain,general},new[]{general,different},new[]{plain,electrical},new[]{electrical,otherElectrical},new[]{plain,unknownGeneral},new[]{plain,unknownElectrical},new[]{plain,malformedGeneral},new[]{plain,malformedElectrical},new[]{plain,invalidElectricalKind}};
            foreach(var distinct in pairs)
            foreach(var pair in new[]{distinct,new[]{distinct[1],distinct[0]}})
            {
                foreach(bool belt in new[]{false,true})
                {
                    foreach(bool reserved in new[]{false,true})
                    {
                        Probe(player,pair[0],pair[1],belt,false,false,reserved,cases);
                        Probe(player,pair[0],pair[1],belt,false,true,reserved,cases);
                    }
                }
                Sort(pair[0],pair[1],null,cases);Sort(pair[0],pair[1],liquid,cases);
            }
            foreach(var value in new[]{plain,general,different,electrical,otherElectrical})
            {
                foreach(bool belt in new[]{false,true})foreach(bool reserved in new[]{false,true})Probe(player,value,value.Clone(),belt,true,false,reserved,cases);
                var input=new[]{new ItemStack(value.Clone(),1),new ItemStack(value.Clone(),2)};string before=Image(input);
                var sorted=StackSortUtil.CombineAndSortStacks(input);
                Need(Image(input)==before&&sorted.Count(x=>!x.IsEmpty())==1&&sorted.Single(x=>!x.IsEmpty()).count==3,"Equivalent actual native sort failed");
                cases.Add(new JObject{["native"]="StackSortUtil.CombineAndSortStacks",["equivalent"]=true,["passed"]=true});
            }
            output["reservationScope"]="DEBUG detached-object double; actual production replacement loops, no live/durable reservation";
            output["passed"]=true;
        }
        catch(Exception error){output["error"]=error.ToString();}
        finally
        {
            player.bPlayerStatsChanged=stats;
            bool unchanged=Image(player.bag.ItemGrid.items)==liveBag&&Image(player.inventory.ItemGrid.items)==liveBelt;
            output["liveInventoryUnchanged"]=unchanged;output["passed"]=(bool)output["passed"]&&unchanged;output["cases"]=cases;
        }
        SdtdConsole.Instance.Output(output.ToString(Formatting.None));
        if(!(bool)output["passed"])SdtdConsole.Instance.Output("*** ERROR provenance merge qualification failed");
    }
    private static string Image(ItemStack[] cells)=>string.Join("|",cells.Select(s=>s.count+":"+RebirthNativeItemCodec.Encode(s.itemValue)));
    private static void Need(bool okay,string message){if(!okay)throw new InvalidOperationException(message);}
    private static void Probe(EntityPlayerLocal actor,ItemValue destination,ItemValue source,bool belt,bool equivalent,bool empty,bool reserved,JArray results)
    {
        int max=destination.ItemClass.MaxCount;
        int physical=belt?RebirthToolbeltCapacity.PhysicalInventorySlots:2;
        var grid=belt?ItemStackGrid.Create(new Vector2i(physical,1),XUiC_ItemStack.StackLocationTypes.ToolBelt,false,false,null):null;
        var bag=belt?null:new Bag(new Vector2i(2,1),XUiC_ItemStack.StackLocationTypes.Backpack,null);
        var inventory=belt?new Inventory(grid,0){entity=actor}:null;
        var cells=belt?grid.items:bag.ItemGrid.items;
        for(int slot=0;slot<cells.Length;slot++)cells[slot]=new ItemStack(destination.Clone(),max);
        cells[0]=new ItemStack(destination.Clone(),max-1);cells[1]=empty?ItemStack.Empty.Clone():new ItemStack(destination.Clone(),max);
        if(belt)
        {
            Need(inventory.Length==RebirthToolbeltCapacity.PhysicalInventorySlots&&inventory.SlotCount==inventory.Length,"Detached native belt shape mismatch");
            Need(RebirthToolbeltCapacity.GetOwnedSlotCount(actor,inventory.Length)>=2&&RebirthToolbeltCapacity.GetSlotsForPlayer(actor)>=2,"Actual owner does not own both fixture slots");
            var movable=new ItemStack(source.Clone(),2);
            Need(movable.CanMoveTo(XUiC_ItemStack.StackLocationTypes.ToolBelt)&&inventory.CanMoveToSlot(movable,0)&&inventory.CanMoveToSlot(movable,1),"Actual native policy refuses detached fixture cells");
        }
        var incoming=new ItemStack(source.Clone(),2);string nativeSource=RebirthNativeItemCodec.Encode(incoming.itemValue);
        using(var reservation=reserved?RebirthProvenanceDetachedReservation.Enter(belt?(object)inventory:bag,!belt,0):null)
        {
        bool moves=equivalent&&!reserved;
        var partial=belt?inventory.TryStackItem(0,incoming):bag.TryStackItem(0,incoming);
        Need(partial.anyMoved==moves&&!partial.allMoved&&incoming.count==(moves?1:2),"Partial native move/remainder disagreement");
        Need(cells[0].count==(moves?max:max-1)&&RebirthNativeItemCodec.Encode(cells[0].itemValue)==RebirthNativeItemCodec.Encode(destination),"Destination identity/count changed");
        Need(RebirthNativeItemCodec.Encode(incoming.itemValue)==nativeSource,"Refused remainder metadata changed");
        if(empty)
        {
            bool placed;if(belt)placed=inventory.AddItem(incoming,0,2,out _);else placed=bag.AddItem(incoming);
            Need(placed&&cells[1].count==2&&RebirthNativeItemCodec.Encode(cells[1].itemValue)==nativeSource,"Native empty fallback lost original identity");
        }
        if(!empty&&!equivalent)
        {
            string beforeRefused=Image(cells);bool added=belt?inventory.AddItem(incoming,0,2,out _):bag.AddItem(incoming);
            Need(!added&&incoming.count==2&&Image(cells)==beforeRefused&&RebirthNativeItemCodec.Encode(incoming.itemValue)==nativeSource,"Full native insertion changed refused remainder");
        }
        results.Add(new JObject{["native"]=belt?"Inventory.TryStackItem":"Bag.TryStackItem",["equivalent"]=equivalent,["emptyFallback"]=empty,["reservationDouble"]=reserved,["reservedSlot"]=reserved?0:-1,["physicalSlots"]=cells.Length,["ownedSlots"]=belt?RebirthToolbeltCapacity.GetOwnedSlotCount(actor,inventory.Length):cells.Length,["passed"]=true});
        }
    }
    private static string Totals(ItemStack[] cells)=>string.Join("|",cells.Where(s=>!s.IsEmpty()).GroupBy(s=>RebirthNativeItemCodec.Encode(s.itemValue)).OrderBy(group=>group.Key,StringComparer.Ordinal).Select(group=>group.Key+":"+group.Sum(s=>(long)s.count)));
    private static void Sort(ItemValue left,ItemValue right,ItemValue liquid,JArray results)
    {
        var input=liquid==null?new[]{new ItemStack(left.Clone(),1),new ItemStack(right.Clone(),2)}:new[]{new ItemStack(left.Clone(),1),new ItemStack(right.Clone(),2),new ItemStack(liquid.Clone(),1)};
        string before=Image(input);var sorted=StackSortUtil.CombineAndSortStacks(input);
        Need(Image(input)==before,"Actual sort modified caller inputs");
        Need(Totals(input)==Totals(sorted),"Actual sort changed metadata identity totals");
        Need(sorted.Count(s=>!s.IsEmpty()&&s.itemValue.type==left.type)==2&&sorted.Where(s=>!s.IsEmpty()&&s.itemValue.type==left.type).Sum(s=>s.count)==3,"Actual sort merged distinct provenance");
        results.Add(new JObject{["native"]="StackSortUtil.CombineAndSortStacks",["unrelatedLiquid"]=liquid!=null,["passed"]=true});
    }
}
#endif
