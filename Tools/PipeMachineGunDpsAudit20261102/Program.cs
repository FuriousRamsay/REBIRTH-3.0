using System;
static class Mathf {public static float Max(float a,float b)=>Math.Max(a,b);public static int FloorToInt(float x)=>(int)Math.Floor(x);}
static class Program {
 static int checks;const float reload=3.466667f;
 static void Near(float a,float b,string name){if(Math.Abs(a-b)>.002f)throw new Exception(name+": "+a+" expected "+b);checks++;}
 static float D(float damage,float rpm,int magazine,float reload)=>Actual.CalculateRangedSustainedDps(damage,rpm,0,magazine,reload,1,60,out _,out _);
 static int Main(){try{
 Near(Actual.ResolveRangedReloadBase(3.1f,reload,6.667f,0),3.1f,"completed measurement wins");
 Near(Actual.ResolveRangedReloadBase(0,reload,6.667f,0),reload,"unrelated shared-controller clip cannot override exact metadata");
 Near(Actual.ResolveRangedReloadBase(0,reload,0,0),reload,"cold preview matches held metadata");
 Near(Actual.ResolveRangedReloadBase(0,0,3.5f,2.66f),3.5f,"unannotated existing animation precedence preserved");
 Near(Actual.ResolveRangedReloadBase(0,0,0,2.66f),2.66f,"rifle authored fallback preserved");
 Near(Actual.ResolveRangedReloadBase(float.NaN,float.PositiveInfinity,-1,2.66f),2.66f,"invalid metadata rejected");
 Near(Actual.ResolveRangedReloadBase(0,0,0,0),0,"unknown remains distinguishable");
 float lowDamage=129.7f*60/335,highDamage=231.2f*60/358;
 Near(D(lowDamage,335,22,0),129.7f,"old zero reload reproduces lower screenshot burst");
 Near(D(highDamage,358,15,0),231.2f,"old zero reload reproduces higher screenshot burst");
 Near(D(lowDamage,335,22,reload),22*lowDamage/(21*60f/335+reload),"lower example full native cycle");
 Near(D(highDamage,358,15,reload),15*highDamage/(14*60f/358+reload),"higher example full native cycle");
 Near(D(54.53f,120,1,2.66f),20.5f,"single-shot rifle 20.5 sustained is plausible");
 Near(D(highDamage,358,15,reload/1.5f),15*highDamage/(14*60f/358+reload/1.5f),"live reload multiplier remains applied");
 Console.WriteLine($"PASS {checks} actual production reload/DPS checks; screenshot examples are conditional math, not recovered item seeds.");
 Console.WriteLine($"Same-damage examples with3.466667s reload: {D(lowDamage,335,22,reload):F2}, {D(highDamage,358,15,reload):F2} DPS");return 0;
 }catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}

