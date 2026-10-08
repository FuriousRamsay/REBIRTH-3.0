using System;
using System.Collections.Generic;
using System.IO;

// Semantic filter for trusted native serializer spans. Not authentication or settlement authority.
internal static class RebirthStationTerminalContents
{
    internal static bool Matches(byte[] outputBytes,byte[] completionBytes,RebirthStationGridAdmission admission,
        Recipe savedRecipe,CraftCompleteData expected,IList<ItemStack> expectedOutput)
    {
        if(outputBytes==null||completionBytes==null||outputBytes.Length<1||completionBytes.Length<2||
            outputBytes.Length>256*1024||completionBytes.Length>256*1024||expectedOutput==null||
            expectedOutput.Count<1||expectedOutput.Count>255||
            !RebirthStationCompletionReceipt.MatchesAdmission(admission,savedRecipe,expected))return false;
        try
        {
            if(!TryReadOutput(outputBytes,expectedOutput.Count,out var output))return false;
            for(int i=0;i<output.Length;i++)
            {
                var item=output[i];
                if(item?.itemValue==null||item.count<0||expectedOutput[i]?.itemValue==null||expectedOutput[i].count<0||
                    !RebirthStationGridIngredients.IsSameStackSnapshot(item,expectedOutput[i]))return false;
            }
            if(!RebirthNativeItemConformanceReader.TryDecodeStationCompletions(completionBytes,out var receipts))return false;
            int matches=0;
            foreach(var data in receipts)
            {
                if(data.CraftedItemStack?.itemValue==null||data.CraftedItemStack.count<0)return false;
                    bool marked=false;var metadata=data.CraftedItemStack.itemValue.Metadata;
                    if(metadata!=null)foreach(var key in metadata.Keys)if(key.StartsWith(RebirthStationCompletionReceipt.Prefix,StringComparison.Ordinal)){marked=true;break;}
                    if(!marked)continue;
                    if(!RebirthStationCompletionReceipt.TryReadIdentity(data,out var identity))return false;
                    if(identity.Job!=admission.JobId)continue;
                    if(++matches!=1||!RebirthStationCompletionReceipt.MatchesAdmission(admission,savedRecipe,data)||
                        data.CrafterEntityID!=expected.CrafterEntityID||data.RecipeName!=expected.RecipeName||
                        data.ItemScrapped!=expected.ItemScrapped||data.CraftExpGain!=expected.CraftExpGain||
                        data.RecipeUsedCount!=expected.RecipeUsedCount||
                        !RebirthStationGridIngredients.IsSameStackSnapshot(data.CraftedItemStack,expected.CraftedItemStack))return false;
                }
                return matches==1;
        }
        catch{return false;}
    }
    // Native output framing already has the row count. Add only the canonical codec version.
    internal static bool TryReadOutput(byte[] bytes,int expectedCount,out ItemStack[] output)
    {
        output=null;
        if(bytes==null||bytes.Length<1||bytes.Length>256*1024||expectedCount<1||expectedCount>255||
            bytes[0]!=expectedCount)return false;
        try
        {
            var framed=new byte[bytes.Length+1];framed[0]=1;
            Buffer.BlockCopy(bytes,0,framed,1,bytes.Length);
            int maximumBytes=256*1024+1;
            int maximumText=4*((maximumBytes+2)/3);
            return RebirthNativeItemConformanceReader.TryDecodeStackArrayV1(Convert.ToBase64String(framed),
                expectedCount,maximumBytes,maximumText,out output);
        }
        catch{output=null;return false;}
    }
}