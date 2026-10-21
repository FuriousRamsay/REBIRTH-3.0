using System;
using System.Linq;
using System.Text;

// Native container/prefs and item encoding doubles; only extracted actual policy
// is qualified. No fake codec claim for native painted item preservation.
class ItemStack {public string Image;public bool IsEmpty()=>string.IsNullOrEmpty(Image);}
class PreferenceTracker {public ItemStack[] bag,toolbelt;public object[] equipment;public bool AnyPreferences=>bag!=null||toolbelt!=null||equipment!=null;}
class ItemStackGrid {public int Length;public bool[] SlotLocks;public PreferenceTracker Preferences;}
static class RebirthCamperInventorySnapshot
{public static byte[] Capture(ItemStack[] cells)=>Encoding.UTF8.GetBytes(string.Join("|",cells.Select(c=>c.Image??"empty")));}
partial class RebirthCamperTransferCommit {private static bool Equal(byte[] a,byte[] b)=>a.SequenceEqual(b);}
class Program
{
    static int count;
    static ItemStack E()=>new();static ItemStack I(string image)=>new(){Image=image};
    static ItemStack[] Cells(int n)=>Enumerable.Range(0,n).Select(_=>E()).ToArray();
    static void Check(bool ok,string name){if(!ok)throw new Exception(name);Console.WriteLine("PASS "+name);count++;}
    static void Allowed(ItemStackGrid grid,ItemStack[] old,ItemStack[] after,string name)
    {var locks=grid.SlotLocks?.ToArray();var prefs=grid.Preferences;var bag=prefs?.bag;RebirthCamperTransferCommit.AssertCargoPolicy(grid,old,after);Check((locks==null||locks.SequenceEqual(grid.SlotLocks))&&ReferenceEquals(prefs,grid.Preferences)&&ReferenceEquals(bag,grid.Preferences?.bag),name+" preserves native policy objects/bits");}
    static void Refused(ItemStackGrid grid,ItemStack[] old,ItemStack[] after,string text)
    {try{RebirthCamperTransferCommit.AssertCargoPolicy(grid,old,after);throw new Exception("Unexpected admission: "+text);}catch(InvalidOperationException ex){Check(ex.Message.Contains(text),"specific refusal "+text);}}
    static void Main()
    {
        var old=Cells(4);old[0]=I("paint/full-original");var grown=Cells(6);grown[0]=I("paint/full-original");
        var grid=new ItemStackGrid{Length=4,SlotLocks=new[]{true,false,false,false},Preferences=new(){bag=Cells(4)}};
        Allowed(grid,old,grown,"unrelated lock and empty preference image permit growth");
        grid.Preferences.bag[0]=I("preferred");Allowed(grid,old,grown,"retained physical preferred cell permits growth");
        var smaller=Cells(2);smaller[0]=I("paint/full-original");Allowed(grid,old,smaller,"unaffected locked/preferred prefix permits reduction");
        grid.SlotLocks[3]=true;Refused(grid,old,smaller,"Cargo slot 4 is locked");grid.SlotLocks[3]=false;
        grid.Preferences.bag[3]=I("desired-item");Refused(grid,old,smaller,"Cargo slot 4 has an item preference");grid.Preferences.bag[3]=E();
        grown[0]=I("different-native-image");Refused(grid,old,grown,"Cargo slot 1 is locked");grid.SlotLocks[0]=false;
        Refused(grid,old,grown,"Cargo slot 1 has an item preference");
        grid.Preferences.bag=Cells(4);Allowed(grid,old,grown,"changed unprotected position is permitted");
        grid.Preferences=new(){equipment=new object[]{"unrelated"},toolbelt=new[]{I("unrelated-belt")}};
        Allowed(grid,old,smaller,"unrelated equipment/toolbelt preferences do not block cargo resize");
        grid.Preferences=new(){bag=Cells(4)};Allowed(grid,Cells(4),Cells(0),"empty legacy preference array does not globally block zero-capacity migration");
        grid.Preferences.bag[1]=I("desired-item");Refused(grid,Cells(4),Cells(0),"Cargo slot 2 has an item preference");
        grid.Preferences.bag=null;grid.SlotLocks[1]=true;Refused(grid,Cells(4),Cells(0),"Cargo slot 2 is locked");
        Console.WriteLine($"PASS {count} actual cargo policy assertions with explicit native policy/codec doubles; no game/save/transfer proof.");
    }
}
