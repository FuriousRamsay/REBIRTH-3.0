using System.Collections.Generic;

// Source-linked simulation double only: does not read owner/world/receipt/native
// files and never calls gameplay adapters. Production ledger is tested separately.
internal static class RebirthGearTrace
{
    private static bool enabled;
    private static readonly Queue<string> stages=new Queue<string>();
    internal static bool Enabled=>enabled;
    internal static int Count=>stages.Count;
    internal static string[] Stages=>stages.ToArray();
    internal static void Configure(bool value,bool clear)
    {enabled=value;if(clear)stages.Clear();}
    internal static void Note(object owner,string transaction,string creation,string stage,bool? success=null,object sender=null)
    {Record(stage);}
    internal static void Note(object offer,string stage,bool? success=null)
    {Record(stage);}
    internal static void Witness(object offer,string checkpoint,string predicate,string section,int actualShape=-1,int expectedShape=-1,int index=-1,string actualHash=null,string expectedHash=null,string savedOwnerKey=null)
    {Record("saved-owner."+checkpoint);}
    private static void Record(string stage)
    {if(!enabled)return;stages.Enqueue(stage);while(stages.Count>192)stages.Dequeue();}
}

// Witness-source fixture adapter only. Detailed pure-plan diagnostics have no
// authority; simplified fixture plans do not model that production comparison.
internal static class RebirthGearSavedOwnerMismatch
{
    internal static bool Fail(object offer,object stage,string savedOwnerKey,string predicate,string section,int actual=-1,int expected=-1)
    {RebirthGearTrace.Witness(offer,stage.ToString(),predicate,section,actual,expected);return false;}
    internal static void Inventory(object offer,object stage,string savedOwnerKey,object plan,object bag,object belt)
    {RebirthGearTrace.Witness(offer,stage.ToString(),"fixture-plan-comparison","inventory");}
}

