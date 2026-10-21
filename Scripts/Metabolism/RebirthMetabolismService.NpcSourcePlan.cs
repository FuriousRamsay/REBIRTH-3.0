using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using UnityEngine;

public static partial class RebirthMetabolismService
{
    internal enum NpcDeferredKind:byte{Consumption=1,StressDrink=2,Trait=3,HydrationCost=4,Treatment=5}
    internal sealed class NpcDeferred
    {
        internal NpcDeferredKind Kind;internal string Item,Context;internal float Value;internal bool Drink;
        internal void Apply(EntityPlayer actor)
        {
            switch(Kind)
            {
                case NpcDeferredKind.Consumption:RebirthStatisticsService.RecordConsumption(actor,Item,Drink,Value);break;
                case NpcDeferredKind.StressDrink:RebirthStressService.Drink(actor,Item,Value);break;
                case NpcDeferredKind.Trait:RebirthTraitSupportService.TryApplyFromItem(actor,Item,Context);break;
                case NpcDeferredKind.HydrationCost:actor.Stats.Water.Value=Mathf.Max(0f,actor.Stats.Water.Value-Value);break;
                case NpcDeferredKind.Treatment:RebirthStatisticsService.RecordMetabolismTreatment(actor,Item);break;
                default:throw new InvalidOperationException("Unknown original NPC deferred effect");
            }
        }
        internal XElement Write()=>new XElement("effect",new XAttribute("kind",(byte)Kind),new XAttribute("item",Item),new XAttribute("context",Context??string.Empty),new XAttribute("value",Value),new XAttribute("drink",Drink));
    }
    internal sealed class NpcSourcePlan
    {
        internal EntityPlayer Actor;internal RebirthMetabolismState State;internal ItemStack Expected,Replacement,Siblings,NativeItem;
        internal RebirthConsumeResult Result;internal readonly List<NpcDeferred> Deferred=new List<NpcDeferred>();
    }
    [ThreadStatic]private static NpcSourcePlan PlanningNpcSource;
    private static void ConsumeEffect(NpcDeferredKind kind,EntityPlayer actor,string item,float value=0f,bool drink=false,string context=null)
    {
        var effect=new NpcDeferred{Kind=kind,Item=item,Value=value,Drink=drink,Context=context};
        if(PlanningNpcSource!=null){if(PlanningNpcSource.Deferred.Count>=8)throw new InvalidOperationException("NPC effect plan exceeds original bound");PlanningNpcSource.Deferred.Add(effect);}
        else effect.Apply(actor);
    }
    internal static XElement DescribeNpcPlan(NpcSourcePlan plan)=>new XElement("plan",new XAttribute("version",1),
        new XElement("result",new XAttribute("consumed",plan.Result.ConsumedMl),new XAttribute("remaining",plan.Result.RemainingMl),new XAttribute("entry",plan.Result.AddedEntryId),new XAttribute("empty",plan.Result.CreatedEmptyItem),plan.Result.Message??string.Empty),
        new XElement("deferred",plan.Deferred.Select(effect=>effect.Write())));
    internal static NpcSourcePlan HydrateNpcPlan(EntityPlayer actor,RebirthMetabolismState state,ItemStack source,ItemStack serving,ItemStack replacement,ItemStack siblings,XElement image)
    {
        try
        {
            if(actor==null||state==null||source?.itemValue?.ItemClass==null||serving?.itemValue?.ItemClass==null||serving.count!=1||image?.Name!="plan"||image.Attributes().Count()!=1||(int?)image.Attribute("version")!=1||image.Elements().Count()!=2)return null;
            var result=image.Element("result");var deferred=image.Element("deferred");
            if(result==null||result.Attributes().Count()!=4||result.HasElements||result.Value.Length>1024||deferred==null||deferred.HasAttributes||deferred.Elements().Count()>8)return null;
            float consumed=(float)result.Attribute("consumed"),remaining=(float)result.Attribute("remaining");
            if(float.IsNaN(consumed)||float.IsInfinity(consumed)||consumed<0||float.IsNaN(remaining)||float.IsInfinity(remaining)||remaining<0)return null;
            var plan=new NpcSourcePlan{Actor=actor,State=state,Expected=source,NativeItem=serving,Replacement=replacement,Siblings=siblings,
                Result=new RebirthConsumeResult{Success=true,Failure=RebirthMetabolismConsumeFailure.None,ConsumedMl=consumed,RemainingMl=remaining,AddedEntryId=(int)result.Attribute("entry"),CreatedEmptyItem=(bool)result.Attribute("empty"),Message=result.Value}};
            foreach(var row in deferred.Elements())
            {
                if(row.Name!="effect"||row.HasElements||row.Attributes().Count()!=5)return null;
                byte kind=checked((byte)(int)row.Attribute("kind"));float value=(float)row.Attribute("value");string item=(string)row.Attribute("item"),context=(string)row.Attribute("context");
                if(kind<1||kind>5||float.IsNaN(value)||float.IsInfinity(value)||value<0||item!=source.itemValue.ItemClass.GetItemName()||context==null||context.Length>128||
                    kind==3&&context!="trait-support-metabolism-drink"&&context!="trait-support-metabolism-food"&&context!="trait-support-metabolism-supplement"||kind!=3&&context!=string.Empty)return null;
                plan.Deferred.Add(new NpcDeferred{Kind=(NpcDeferredKind)kind,Item=item,Context=context,Value=value,Drink=(bool)row.Attribute("drink")});
            }
            return XNode.DeepEquals(DescribeNpcPlan(plan),image)?plan:null;
        }
        catch{return null;}
    }
    private static void ConsumeNativeEffect(EntityPlayer actor,ItemValue value,bool drink)
    {
        if(PlanningNpcSource!=null){PlanningNpcSource.NativeItem=new ItemStack(value.Clone(),1);return;}
        FirePreservedUseEffects(actor,value,drink);
    }
    private static void PublishConsumeState(EntityPlayer actor)
    {if(PlanningNpcSource==null)SendSnapshotToOwner(actor,false);}
    internal static NpcSourcePlan PlanNpcSource(EntityPlayer actor,ItemStack source,RebirthMetabolismState detached,out RebirthConsumeResult outcome,bool autoSip=false)
    {
        outcome=new RebirthConsumeResult{Success=false,Failure=RebirthMetabolismConsumeFailure.NotAuthoritative,Message="No original NPC serving was admitted."};
        if(!ThreadManager.IsMainThread()||!IsServerAuthority||actor==null||detached==null||PlanningNpcSource!=null||RebirthCharacterCreationHoldService.IsHeld(actor))return null;
        var plan=new NpcSourcePlan{Actor=actor,State=detached};PlanningNpcSource=plan;
        try
        {
            plan.Result=ConsumeAt(actor,RebirthMetabolismSourceKind.Backpack,-1,source,autoSip,(expected,replacement,siblings)=>
            {
                if(plan.Expected!=null)return false;
                plan.Expected=expected.Clone();plan.Replacement=replacement?.Clone();plan.Siblings=siblings?.Clone();return true;
            });
            outcome=plan.Result;
            return plan.Result.Success&&plan.Expected!=null&&plan.NativeItem!=null?plan:null;
        }
        finally{PlanningNpcSource=null;}
    }
    internal static void ApplyNpcSourceDeferredEffects(NpcSourcePlan plan,bool nativeOwnerHydration=false)
    {
        if(plan==null||PlanningNpcSource!=null)throw new InvalidOperationException("No original NPC serving plan");
        foreach(var action in plan.Deferred)
            if(action.Kind!=NpcDeferredKind.HydrationCost||!nativeOwnerHydration&&plan.Actor is EntityPlayerLocal)action.Apply(plan.Actor);
        SendSnapshotToOwner(plan.Actor,false);
    }
}
