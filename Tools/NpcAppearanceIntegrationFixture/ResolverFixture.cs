using System;
using System.Collections.Generic;
namespace UnityEngine
{
    public class TextAsset { public string text; public TextAsset(string value){text=value;} }
    public static class Resources { public static TextAsset Asset=new TextAsset("native-fixture-one"); public static object Load(string key){return key=="sdcs"?Asset:null;} }
}
public sealed class Archetype
{
    public static Archetype GetArchetype(string name){return name=="BaseMale"||name=="BaseFemale"?new Archetype():null;}
}
public static class SDCSDataUtils
{
    public enum HairTypes { Hair, Mustache, Chops, Beard }
    public struct HairColorData { public string PrefabName; }
    public static Dictionary<string,int> VariantData=new Dictionary<string,int>();
    public static List<string> EyeColorList=new List<string>();
    public static bool Broken; public static int Loads;
    public static void SetupData(){Loads++;VariantData.Clear();EyeColorList.Clear();if(!Broken){VariantData.Add("race",1);EyeColorList.AddRange(new[]{"Blue01","Green01"});}}
    public static List<string> GetRaceList(bool male){return new List<string>(VariantData.Keys);}
    public static List<string> GetVariantList(bool male,string race){return new List<string>{"1","2"};}
    public static List<string> GetEyeColorNames(){return EyeColorList;}
    public static List<string> GetHairNames(bool male,HairTypes type){return new List<string>{type+"01",type+"02"};}
    public static List<HairColorData> GetHairColorNames(){return new List<HairColorData>{new HairColorData{PrefabName="Brown01"},new HairColorData{PrefabName="Black01"}};}
}
static class ResolverFixture
{
    public static int Run()
    {
        int count=0;
        void Check(bool condition,string name){if(!condition)throw new Exception(name);count++;}
        void Reject(Action action,string name){bool rejected=false;try{action();}catch(InvalidOperationException){rejected=true;}Check(rejected,name);}
        var original=new RebirthHumanNpcAppearanceDescriptor(RebirthHumanNpcModelPipeline.SDCS,77,"BaseMale");
        var resolved=RebirthNpcAppearanceAuthorityResolver.Resolve(original,true,"Player");
        Check(resolved.Resolved!=null&&resolved.Seed==77&&resolved.SchemaVersion==2,"authority composes actual domain");
        Check(resolved.Equals(RebirthNpcAppearanceAuthorityResolver.Resolve(original,true,"Player")),"authority deterministic same source");
        var random=new System.Random(77);random.Next(1);int variant=random.Next(2)+1;string eye=new[]{"Blue01","Green01"}[random.Next(2)];
        string hair=new[]{"Hair01","Hair02"}[random.Next(2)];string colour=new[]{"Brown01","Black01"}[random.Next(2)];
        string mustache=new[]{"Mustache01","Mustache02"}[random.Next(2)];string chops=new[]{"Chops01","Chops02"}[random.Next(2)];string beard=new[]{"Beard01","Beard02"}[random.Next(2)];
        Check(resolved.Resolved.Variant==variant&&resolved.Resolved.Eye==eye&&resolved.Resolved.Hair==hair&&resolved.Resolved.HairColour==colour&&resolved.Resolved.Mustache==mustache&&resolved.Resolved.Chops==chops&&resolved.Resolved.Beard==beard,"legacy native draw order preserved");
        int loads=SDCSDataUtils.Loads;
        UnityEngine.Resources.Asset=null;
        Check(RebirthNpcAppearanceAuthorityResolver.Resolve(resolved,true,"Changed").Equals(resolved),"existing resolution wins without source or reroll");
        Reject(()=>RebirthNpcAppearanceAuthorityResolver.Resolve(original,true,"Player"),"missing dedicated text source refused");
        Check(SDCSDataUtils.Loads==loads,"missing source does not mutate native catalogues");
        UnityEngine.Resources.Asset=new UnityEngine.TextAsset("native-fixture-two");
        var changed=RebirthNpcAppearanceAuthorityResolver.Resolve(original,true,"Player");
        Check(changed.Resolved.CatalogueId!=resolved.Resolved.CatalogueId&&SDCSDataUtils.Loads==loads+1,"catalogue fingerprint change qualifies native text anew");
        SDCSDataUtils.Broken=true;UnityEngine.Resources.Asset=new UnityEngine.TextAsset("broken-fixture");
        Reject(()=>RebirthNpcAppearanceAuthorityResolver.Resolve(original,true,"Player"),"empty native mandatory definitions refused");
        SDCSDataUtils.Broken=false;UnityEngine.Resources.Asset=new UnityEngine.TextAsset("native-fixture-two");
        var female=RebirthNpcAppearanceAuthorityResolver.Resolve(new RebirthHumanNpcAppearanceDescriptor(RebirthHumanNpcModelPipeline.SDCS,0,"BaseFemale"),false,"Player");
        Check(!female.Resolved.IsMale&&female.Resolved.Mustache==""&&female.Resolved.Chops==""&&female.Resolved.Beard==""&&female.Seed==0,"female choices and original zero seed preserved");
        Reject(()=>RebirthNpcAppearanceAuthorityResolver.Resolve(new RebirthHumanNpcAppearanceDescriptor(RebirthHumanNpcModelPipeline.SDCS,5,""),true,"Player"),"ambiguous empty legacy archetype refused");
        return count;
    }
}