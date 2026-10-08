using System;
using System.Collections.Generic;
public struct TextureFullArray {public bool Changed;public bool IsDefault {get{return !Changed;}}}
public class ItemValue {public int type=1,Meta,Quality,Seed;public byte Flags,SelectedAmmoTypeIndex;public float UseTimes;public int[] Stats;public TextureFullArray TextureFullArray;public Dictionary<string,int> Metadata;public ItemValue[] modifications,cosmeticMods;public bool IsEmpty(){return type==0;}}
public static class Service {
// METHODS
}
public static class Checks {public static void Main(){for(int i=0;i<11;i++){var v=new ItemValue{Seed=123};switch(i){case 1:v.Meta=1;break;case 2:v.Flags=2;break;case 3:v.SelectedAmmoTypeIndex=1;break;case 4:v.Stats=new[]{1};break;case 5:v.TextureFullArray=new TextureFullArray{Changed=true};break;case 6:v.Quality=2;break;case 7:v.UseTimes=1;break;case 8:v.Metadata=new Dictionary<string,int>{{"a",1}};break;case 9:v.modifications=new[]{new ItemValue()};break;case 10:v.cosmeticMods=new[]{new ItemValue()};break;}if(Service.IsFungible(v)!=(i==0))throw new Exception("case "+i);}Console.WriteLine("PASS: production plain-item guard preserves ordinary seeded goods and rejects ten unrepresentable attribute cases");}}
