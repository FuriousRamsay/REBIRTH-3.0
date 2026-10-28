#requires -Version 7.0
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Initialize-TemporaryFiles.ps1')
$root=Join-Path $PSScriptRoot '../..'
$code=@"
using System;using System.IO;using System.Linq;using System.Collections.Generic;using System.Collections.ObjectModel;using System.Xml.Linq;using System.Globalization;
public class EntityPlayer{}
public class ItemClass {public int Id;public static ItemClass GetItemClass(string n)=>n=="unit_iron"?new ItemClass{Id=11}:n=="unit_clay"?new ItemClass{Id=12}:null;}
public class ItemValue {public int type;public bool HasModSlots;public Dictionary<string,object> Metadata;public bool IsEmpty()=>type==0;public bool HasMods()=>false;public ItemValue Clone()=>new ItemValue{type=type};public void Write(BinaryWriter w){w.Write(type);} }
public class ItemStack {public ItemValue itemValue;public int count;public ItemStack(ItemValue v,int n){itemValue=v;count=n;}public static ItemStack Empty=>new ItemStack(new ItemValue(),0);public bool IsEmpty()=>count==0||itemValue.IsEmpty();public ItemStack Clone()=>new ItemStack(itemValue.Clone(),count);}
public class Recipe {public ushort Version;public int itemValueType=5,count=1,craftingToolType,unlockExpGain,craftExpGain,craftingTier;public float craftingTime;public string craftingArea,tooltip;public bool materialBasedRecipe,wildcardForgeCategory,wildcardCampfireCategory,UseIngredientModifier,IsTrackable,isQuest,isChallenge,IsTracked,IsScrap,IsLearnable;public object tags,Effects;public List<ItemStack> ingredients=new List<ItemStack>();}
public class Writer:BinaryWriter {public Writer():base(new MemoryStream()){}public void SetBaseStream(Stream s){OutStream=s;}protected override void Dispose(bool d){Flush();}}
public class Pool {public Writer AllocSync(bool b)=>new Writer();}public static class MemoryPools {public static Pool poolBinaryWriter=new Pool();}
public static class XUiM_Recipes {public static List<Recipe> FilterRecipesByWorkstation(string s,IList<Recipe> r)=>r.ToList();}
public static class RebirthCraftingIngredientQuantity {public static bool TryResolveTotal(EntityPlayer p,Recipe r,ItemStack i,int tier,int b,out int n){long v=(long)i.count*b;n=v<=int.MaxValue?(int)v:0;return v<=int.MaxValue&&v>=0;}}
public static class RebirthStationGridQueue {public static bool Stamp(Recipe c,Recipe a,int b,int t,bool perBatch=false)=>true;}
public static class RebirthNativeItemCodec {public static string Encode(ItemValue v)=>v.type.ToString();public static bool TryDecode(string s,out ItemValue v){v=null;if(!int.TryParse(s,out int t))return false;v=new ItemValue{type=t};return true;}}
public static class RebirthStationNativeInputCodec {public static bool IsRoundTrippable(IList<ItemStack> s)=>s!=null&&s.All(x=>x.count<=65535);}
public static class RebirthStationInputLayout {public static bool TryReadPhysical(IList<ItemStack> s,int p,int m,out ItemStack[] v){v=null;if(s==null||s.Count!=p+m)return false;v=s.Take(p).Select(x=>x.Clone()).ToArray();return true;}}
public static class MaterialChecks {
static void Check(bool x,string s){if(!x)throw new Exception(s);}static ItemStack Cell(int t,int c)=>new ItemStack(new ItemValue{type=t},c);
public static void Run(){var whole=new[]{Cell(1,8),Cell(0,0),Cell(0,0),Cell(11,123456),Cell(12,0)};var r=new Recipe{materialBasedRecipe=true};r.ingredients.Add(Cell(11,123456));
Check(RebirthStationGridIngredients.TryPlanMaterial(null,r,whole,3,new[]{"iron","clay"},1,0,out var plan)&&plan.IsMaterialPayment&&plan.Inputs[0].Slot==3&&plan.Inputs[0].Count==123456&&plan.Inputs[0].MaterialName=="iron","exact allocation");
Check(RebirthStationGridIngredients.TryBuildDebitedGrid(plan,whole,out var after)&&after[3].count==0&&after[3].itemValue.type==11&&after[0].count==8&&whole[3].count==123456,"debit/zero/source");
ItemStack[] read=null;Check(RebirthStationMaterialInputCodec.TryWrite(whole,3,out var image)&&RebirthStationMaterialInputCodec.TryRead(image,out read,out int physical)&&physical==3&&read[3].count==123456&&read[4].itemValue.type==12,"counter image roundtrip");
Check(RebirthStationInputPaymentImage.TryMerge(whole,3,2,read,after,out var merged)&&merged[3].count==0&&merged[3].itemValue.type==11,"whole exact merge");whole[3].count--;Check(!RebirthStationInputPaymentImage.TryMerge(whole,3,2,read,after,out _),"stale counter rejected");whole[3].count++;
var bad=new XElement(image);bad.Element("counter").SetAttributeValue("count",2147483648L);Check(!RebirthStationMaterialInputCodec.TryRead(bad,out _,out _),"overflow rejected");
Check(!RebirthStationGridIngredients.TryPlanMaterial(null,r,whole,3,new[]{"clay","iron"},1,0,out _),"wrong configured slot rejected");Check(!RebirthStationGridIngredients.TryPlanMaterial(null,r,whole,3,new[]{"iron","iron"},1,0,out _),"duplicate material rejected");
Check(!RebirthStationGridIngredients.TryPlanMaterial(null,r,whole,3,new[]{"iron","clay"},2,0,out _),"insufficient counters rejected");whole[3].count=int.MaxValue;r.ingredients[0].count=int.MaxValue;Check(!RebirthStationGridIngredients.TryPlanMaterial(null,r,whole,3,new[]{"iron","clay"},2,0,out _),"batch overflow rejected");
Console.WriteLine("PASS actual material planner/image/merge: Int32 allocation, typed zero, physical cells unchanged, detached stock, stale/overflow/configuration/insufficient refusal. Native item codec doubled.");}}
"@
foreach($file in @('RebirthStationGridIngredients','RebirthStationGridSnapshotCodec','RebirthStationMaterialInputCodec','RebirthStationInputPaymentImage')){$text=[IO.File]::ReadAllText((Join-Path $root "Scripts/Crafting/UI/$file.cs"));$code+=[regex]::Replace($text,'(?m)^using [^;]+;\r?\n','')+"`n"}
Add-Type -TypeDefinition $code
[MaterialChecks]::Run()