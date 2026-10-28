using System;
using System.Collections.Generic;
class Program
{
    static void Check(bool value,string label){if(!value)throw new Exception(label);Console.WriteLine("PASS "+label);}
    static EntityPlayer Player(){var p=new EntityPlayer();p.Stats.Food.Value=10;p.Stats.Water.Value=20;p.Buffs.SetCustomVar("$foodAmountAdd",3);p.Buffs.SetCustomVar("$waterAmountAdd",4);return p;}
    static void Main()
    {
        foreach(bool throws in new[]{false,true})
        {
            var p=Player();bool synchronized=false;
            p.callback=()=>{synchronized=p.MinEventContext.IsLocal;p.Stats.Food.Value=40;p.Stats.Water.Value=50;p.Buffs.SetCustomVar("$foodAmountAdd",99);p.Buffs.SetCustomVar("$waterAmountAdd",88);if(synchronized)p.Buffs.SetCustomVar("$buffInfectionAddCurePerc",5);if(throws)throw new Exception("simulated native event failure");};
            RebirthPreservedConsumableEffects.Apply(p,new ItemValue(),true,false);
            Check(synchronized,"remote authoritative event scope"+(throws?" with failure":""));
            Check(!p.MinEventContext.IsLocal,"prior event scope restored"+(throws?" after failure":""));
            Check(p.Stats.Food.Value==10&&p.Stats.Water.Value==20,"immediate reserves restored");
            Check(p.Buffs.GetCustomVar("$foodAmountAdd")==3&&p.Buffs.GetCustomVar("$waterAmountAdd")==4,"digestion payloads restored");
            Check(p.Buffs.GetCustomVar("$buffInfectionAddCurePerc")==5,"authored infection cure payload retained");
        }
        var client=Player();bool local=false;client.callback=()=>local=client.MinEventContext.IsLocal;
        RebirthPreservedConsumableEffects.Apply(client,new ItemValue(),false,false);
        Check(!local,"non-authoritative call does not promote scope");
        Console.WriteLine("SIMULATION ONLY: actual effect wrapper with native event/stat/buff doubles; no infection ticking, network packets or game validated.");
    }
}
public class EntityPlayer{public Stats Stats=new Stats();public Buffs Buffs=new Buffs();public Context MinEventContext=new Context();public Action callback;public void FireEvent(int e)=>callback?.Invoke();}
public sealed class EntityPlayerLocal:EntityPlayer{public Stealth Stealth=new Stealth();}
public sealed class Stealth{public void SetSmellEat(float amount){}}
public sealed class Stats{public Stat Food=new Stat(),Water=new Stat();}
public sealed class Stat{public float Value;}
public sealed class Context{public bool IsLocal;public ItemValue ItemValue;}
public sealed class Buffs{Dictionary<string,float> values=new Dictionary<string,float>();public float GetCustomVar(string key)=>values.TryGetValue(key,out var v)?v:0;public void SetCustomVar(string key,float value)=>values[key]=value;}
public sealed class ItemValue{public ItemClass ItemClass=new ItemClass();}
public sealed class ItemClass{public object[] Actions=new object[0];}
public sealed class ItemActionEat{public float smellUse;}
public static class QuestEventManager{public static QuestEvents Current=new QuestEvents();}
public sealed class QuestEvents{public void UsedItem(ItemValue value){}}
public static class MinEvent{public static int[] End=new[]{1};}
public static class Log{public static void Warning(string text){}public static void Out(string text){}}
