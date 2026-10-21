using System;
using System.Linq;
using System.Text;
using System.Xml.Linq;

internal static partial class RebirthProductiveEarnedUiCoordinator
{
    private static bool PrepareUnchangedRecipientQueue(QueueHandoff value)
    {
        if(value.Binding.RecipientSource?.Current()!=true)return false;
        using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
        {
            var b=value.Binding;var cards=b.Queue.GetRecipesToCraft();
            if(decoder==null||!value.Current()||cards==null||cards.Any(card=>card.isInventoryFull||card.AmountToRepair!=0||
                card.OriginalItem!=null&&!card.OriginalItem.IsEmpty())||!RebirthProductiveEarnedNativeBody.MatchesCurrent(b.Station,value.Native,value.Offset)||
                !RebirthProductiveEarnedNativeBody.TryCaptureUiProjection(b.Station,cards.Select(UiRow).ToArray(),
                    b.Window.inputWindow?.GetSlots(),b.Window.outputWindow?.GetSlots(),b.Window.fuelWindow?.GetSlots(),b.Window.toolWindow?.GetSlots(),
                    out var before,out value.After,out int offset,out value.AfterOffset)||
                !RebirthProductiveEarnedNativeBody.SameExceptElapsed(value.Native,value.Offset,before,offset)||
                !RebirthProductiveEarnedNativeBody.SameExceptElapsed(value.Native,value.Offset,value.After,value.AfterOffset))return false;
            value.Cards=cards;value.Rows=cards.Select(card=>RebirthProductiveEarnedRowProof.CaptureRow(UiRow(card))).ToArray();
            value.Definitions=cards.Select(card=>card.GetRecipe()==null?"":RebirthRecipeOutputs.DefinitionIdentity(card.GetRecipe())).ToArray();
            value.Grids=HandoffGrids(value);value.Owners=RebirthProductiveEarnedUiQueueOwner.CaptureHandoffOwners(b.Station);
            value.NoChange=true;
            var image=new XElement("nativeUiQueueHandoff",new XAttribute("version",1),new XAttribute("operation",b.Operation.ToString("N")),
                new XAttribute("source","recipient"),new XAttribute("terminal",Hash(value.TerminalReceipt)),new XAttribute("birth",b.Birth.ToString("N")),
                new XAttribute("shifts",0),new XAttribute("noChange",true),new XAttribute("beforeOffset",value.Offset),new XAttribute("afterOffset",value.AfterOffset),
                new XElement("before",Convert.ToBase64String(value.Native)),new XElement("after",Convert.ToBase64String(value.After)));
            value.Image=Encoding.UTF8.GetBytes(image.ToString(SaveOptions.DisableFormatting));return WriteExact(value.Path,value.Image);
        }
    }
}
