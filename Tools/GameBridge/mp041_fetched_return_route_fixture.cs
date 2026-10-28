// AUTHORED_NOT_RUN. Actual workspace return producer; no inventory/refund/drop substitute exists in this cohort.
using System;using System.Collections.Generic;
static class RebirthSurvivorUiText{internal static string L(string key,string fallback)=>fallback;}
class EntityPlayerLocal{internal bool Remote;}class ItemStack{internal int count=3;}
static class RebirthCookingGridCustody{internal static int Calls;internal static bool Accept;internal static long Request;internal static IList<ItemStack> Items;internal static bool ReturnFetched(object workspace,EntityPlayerLocal player,long id,IList<ItemStack> items){Calls++;Request=id;Items=items;return Accept;}}
class Workspace
{
 string status="unchanged";
 // ACTUAL_FETCHED_RETURN
 static void Main(){foreach(bool remote in new[]{false,true})for(int variant=0;variant<5;variant++){var w=new Workspace();var player=new EntityPlayerLocal{Remote=remote};var items=new List<ItemStack>{new ItemStack()};RebirthCookingGridCustody.Calls=0;RebirthCookingGridCustody.Accept=variant==0;w.ReturnPulledIngredients(variant==2?null:player,variant==3?null:items,variant==4?0:17);if(RebirthCookingGridCustody.Calls!=(variant<2?1:0)||items[0].count!=3)throw new Exception("workspace return bypassed original custody "+remote+"/"+variant);if(variant<2&&(RebirthCookingGridCustody.Request!=17||!ReferenceEquals(items,RebirthCookingGridCustody.Items)))throw new Exception("return changed original identity/payload");if(variant==1&&!w.status.Contains("retained"))throw new Exception("refused original return was discarded");}Console.WriteLine("MP041 actual fetched return producer: local/remote exact originals, capacity refusal and malformed callbacks authored; no direct owner/drop grant supplied.");}
}
