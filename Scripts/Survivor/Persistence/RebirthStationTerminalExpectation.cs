using System;
using System.Collections.Generic;

// Immutable session-owned semantic expectation; caller still authenticates terminal event and save.
internal sealed class RebirthStationTerminalExpectation
{
    private readonly RebirthStationGridAdmission admission;
    private readonly Recipe recipe;
    private readonly CraftCompleteData receipt;
    private readonly ItemStack[] output;
    private RebirthStationTerminalExpectation(RebirthStationGridAdmission admission,Recipe recipe,CraftCompleteData receipt,ItemStack[] output)
    {this.admission=admission;this.recipe=recipe;this.receipt=receipt;this.output=output;}
    internal static bool TryCreate(RebirthStationGridAdmission admission,IList<Recipe> definitions,
        CraftCompleteData receipt,IList<ItemStack> output,out RebirthStationTerminalExpectation expectation)
    {
        expectation=null;
        try
        {
            if(admission==null||output==null||output.Count<1||output.Count>255)return false;
            var saved=admission.Clone();
            if(!saved.TryMaterialize(definitions,out var recipe,out _,out _)||
                !RebirthStationCompletionReceipt.MatchesAdmission(saved,recipe,receipt))return false;
            var ownedReceipt=new CraftCompleteData(receipt.CrafterEntityID,receipt.CraftedItemStack.Clone(),
                receipt.RecipeName,receipt.ItemScrapped,receipt.CraftExpGain,receipt.RecipeUsedCount);
            if(!RebirthStationCompletionReceipt.MatchesAdmission(saved,recipe,ownedReceipt)||
                !RebirthStationGridIngredients.IsSameStackSnapshot(receipt.CraftedItemStack,ownedReceipt.CraftedItemStack))return false;
            var ownedOutput=new ItemStack[output.Count];
            for(int i=0;i<ownedOutput.Length;i++)
            {
                if(output[i]?.itemValue==null||output[i].count<0)return false;
                ownedOutput[i]=output[i].Clone();
                if(!RebirthStationGridIngredients.IsSameStackSnapshot(output[i],ownedOutput[i]))return false;
            }
            expectation=new RebirthStationTerminalExpectation(saved,recipe,ownedReceipt,ownedOutput);return true;
        }
        catch{return false;}
    }
    internal bool Matches(byte[] outputBytes,byte[] completionBytes)
    {return RebirthStationTerminalContents.Matches(outputBytes,completionBytes,admission,recipe,receipt,output);}
}