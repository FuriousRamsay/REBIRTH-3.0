using System;
using System.Globalization;
using System.Xml.Linq;

public sealed class RebirthStressState
{
    public float Value, ClearSeconds, DarkSeconds, UnseenSeconds, TeaSeconds, TeaRelief, TeaReliefSeconds, TeaCooldown, TeaDose, HitBudget, TotalBudget, BudgetSeconds;
    public string Location="Exploring";
    public RebirthStressState Clone() => (RebirthStressState)MemberwiseClone();
    public XElement Write()
    {
        var n=new XElement("stress");
        foreach(var f in typeof(RebirthStressState).GetFields())
            if(f.FieldType==typeof(float))n.SetAttributeValue(f.Name,((float)f.GetValue(this)).ToString(CultureInfo.InvariantCulture));
        return n;
    }
    public static RebirthStressState Read(XElement n)
    {
        var s=new RebirthStressState();if(n==null)return s;
        foreach(var f in typeof(RebirthStressState).GetFields())
            if(f.FieldType==typeof(float)&&float.TryParse((string)n.Attribute(f.Name),NumberStyles.Float,CultureInfo.InvariantCulture,out float v)&&!float.IsNaN(v)&&!float.IsInfinity(v))f.SetValue(s,Math.Max(0,v));
        s.Value=Math.Min(100,s.Value);return s;
    }
    public void Drink(float fraction)
    {
        fraction=Math.Max(0,Math.Min(1,fraction));
        TeaSeconds=Math.Min(300,TeaSeconds+300*fraction);
        if(TeaCooldown<=0){TeaDose=0;TeaCooldown=300;}
        float dose=Math.Min(fraction,Math.Max(0,1-TeaDose));
        TeaDose+=dose;if(dose>0){TeaRelief+=10*dose;TeaReliefSeconds=30;}
    }
    public void Add(float amount,float ceiling)
    {
        amount=Math.Max(0,Math.Min(amount,20-TotalBudget));
        amount=Math.Min(amount,Math.Max(0,ceiling-Value));
        Value+=amount;TotalBudget+=amount;
    }
    public void Hit(float multiplier)
    {
        float points=Math.Min(5*multiplier*(TeaSeconds>0?.7f:1),Math.Max(0,15-HitBudget));
        HitBudget+=points;Add(points,100);ClearSeconds=0;
    }
    public void Tick(float seconds,bool threat,bool critical,bool dark,bool night,bool sheltered,bool unseen,bool anxious,bool fearDark,float multiplier,float foodFraction=1,float waterFraction=1)
    {
        seconds=Math.Max(0,seconds);BudgetSeconds+=seconds;
        if(BudgetSeconds>=60){BudgetSeconds%=60;HitBudget=TotalBudget=0;}
        float protection=TeaSeconds>0?.7f:1;
        if(TeaReliefSeconds>0){float used=Math.Min(seconds,TeaReliefSeconds);Value=Math.Max(0,Value-TeaRelief*used/TeaReliefSeconds);TeaRelief-=TeaRelief*used/TeaReliefSeconds;TeaReliefSeconds-=used;}
        TeaSeconds=Math.Max(0,TeaSeconds-seconds);TeaCooldown=Math.Max(0,TeaCooldown-seconds);
        DarkSeconds=fearDark&&(night||dark)?DarkSeconds+seconds:0;
        UnseenSeconds=anxious&&unseen?UnseenSeconds+seconds:0;
        float hunger=Math.Max(0,Math.Min(1,(.5f-foodFraction)*2));
        float thirst=Math.Max(0,Math.Min(1,(.5f-waterFraction)*2));
        // A persistent unmet need sets a gradual stress floor, so ordinary recovery
        // cannot immediately erase its small per-second gain.
        float needTarget=30*(hunger+thirst);
        float needRate=1.5f*(hunger+thirst)*Math.Max(0,multiplier);
        float target=needTarget,rate=needRate;
        if(DarkSeconds>=60){target=Math.Max(target,sheltered?(dark?20:15):(dark?50:45));rate=Math.Max(rate,sheltered?(dark?1.5f:1):(dark?4:3));}
        if(UnseenSeconds>=30){target=Math.Max(target,45);rate=Math.Max(rate,3);}
        if(threat){ClearSeconds=0;Add(5*multiplier*protection*seconds/60,40);if(critical)Add(5*multiplier*protection*seconds/60,100);}
        else ClearSeconds+=seconds;
        if(Value<target)Add(rate*protection*seconds/60,target);
        // Reassuring surroundings let elevated stress settle toward their residual anxiety level.
        if(!threat&&ClearSeconds>=(anxious?90:30)&&Value>target)Value=Math.Max(target,Value-5*seconds/60);
        Value=Math.Max(0,Math.Min(100,Value));
    }
}
