using System;

// EXPLICIT DIAGNOSTIC DOUBLE ONLY. Native console, bounded logging, hashes and
// infection observations are not qualified by these standalone simulations.
// Exact production call signature; does not inspect/mutate arguments or assert.
internal static class RebirthConsumptionTrace
{
    internal static IDisposable Begin() => null;
    internal static void Note(string stage,EntityPlayer player=null,Guid publication=default(Guid),
        RebirthMetabolismSourceKind kind=default(RebirthMetabolismSourceKind),int slot=-1,
        ItemStack before=null,ItemStack after=null,string reason=null,bool? success=null,ItemValue item=null)
    { }
}
