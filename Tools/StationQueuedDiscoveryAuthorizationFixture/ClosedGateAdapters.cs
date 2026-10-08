using System.Collections.Generic;
partial class RebirthRecipeCapabilityIntegration{
 private static readonly Dictionary<TileEntityWorkstation,string> HeldQueues=new();public static string Held(TileEntityWorkstation s)=>HeldQueues.TryGetValue(s,out var r)?r:null;
 private static void HoldActiveQueue(TileEntityWorkstation s,RecipeQueueItem q,string recipe,string why){HeldQueues[s]=why;}}
static class RebirthSurvivorMode{public static bool Enabled=true;public static bool IsEnabledForCurrentWorld()=>Enabled;}
