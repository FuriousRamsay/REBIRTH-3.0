using System;
using System.Linq;

internal static partial class RebirthRepairOriginalCorrection
{
    private sealed class RepairAuditNativeItem
    {
        public int Occurrences;
        public bool BeforeMatches,AfterMatches,ReturnedInputMatches;
    }
    private static RepairAuditNativeItem ObserveRepairAuditItem(ItemStack[] bag,ItemStack[] belt,ItemStack[] equipment,ItemStack cursor,Receipt original)
    {
        if(bag==null||belt==null||equipment==null)return null;
        var rows=bag.Concat(belt).Concat(equipment).Where(value=>value!=null&&!value.IsEmpty()&&SameOperation(value.itemValue,original.Operation,OriginalKey)).ToArray();
        bool inCursor=cursor!=null&&!cursor.IsEmpty()&&SameOperation(cursor.itemValue,original.Operation,OriginalKey);
        bool Matches(string expected)=>!inCursor&&rows.Length==1&&rows[0].count==1&&expected.Length!=0&&SameExceptWear(rows[0].itemValue,ReadItem(expected));
        return new RepairAuditNativeItem{Occurrences=rows.Length+(inCursor?1:0),BeforeMatches=Matches(original.Before),AfterMatches=Matches(original.After),
            ReturnedInputMatches=Matches(original.CancelInput.Length==0?original.Input:original.CancelInput)};
    }
}
