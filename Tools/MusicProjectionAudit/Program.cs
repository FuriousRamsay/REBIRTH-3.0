using System;
using System.Collections.Generic;
partial class Audit
{
    sealed class Cassette { internal string Id,SlotId,Fingerprint,Name; internal int Index,Count; internal bool Audio,Stored; internal ItemValue Value; }
    readonly List<Cassette> library=new(),carried=new(),catalogue=new(),bagProjection=new();
    Cassette selected,drag; bool audioTab,sort,projectionDirty=true;
    long generation=-1,audioRevision=-1,textRevision=-1; object definitions; string culture,search="";
    int libraryRow,bagRow,draws,playbacks; Label searchInput=new(); Label capacity=new(),bagCount=new();
    EntityPlayerLocal Player=new();
    static string Name(string id)=>id+RebirthUiProjectionTextCache.Revision;
    static void Text(Label label,string value){label.Text=value;}
    void DrawDetails(){draws++;}
    void RefreshPlayback(EntityPlayerLocal player){playbacks++;}
    static int passed;
    static void Check(bool condition,string name){if(!condition)throw new Exception(name);Console.WriteLine("PASS "+name);passed++;}
    static void Main()
    {
        var a=new Audit();RebirthMusicLibraryClient.Items.Add("musicA");
        a.Player.bag.ItemGrid.items=new[]{new ItemStack(new ItemValue("musicA","old"),2)};
        a.Refresh();var first=a.carried[0];
        Check(a.draws==1&&ItemValue.Clones==1,"initial projection clones once");
        a.Refresh();Check(a.draws==1&&a.playbacks==2&&ReferenceEquals(first,a.carried[0]),"unchanged skips redraw but playback refreshes");
        a.selected=a.carried[0];a.Player.bag.ItemGrid.items[0].itemValue.Payload="mutated";a.Refresh();
        Check(a.draws==2&&a.carried[0].Value.Payload=="mutated"&&a.selected==null,"in-place native metadata change invalidates exact selected custody");
        a.selected=a.carried[0];a.Player.bag.ItemGrid.items[0].count=3;a.Refresh();
        Check(a.selected.Count==3&&a.draws==3,"count change rebinds selected row");
        a.Player.bag.ItemGrid.items=Array.Empty<ItemStack>();a.Refresh();
        Check(a.carried.Count==0&&a.selected==null,"removed cassette disappears");
        a.selected=a.library[0];RebirthMusicLibraryClient.Items[0]="musicB";a.Refresh();
        Check(a.library[0].Id=="musicB"&&a.selected==null,"catalogue content mutation without revision detected");
        a.searchInput.Text="no match";a.Refresh();Check(a.library.Count==0,"search invalidates filter");
        a.searchInput.Text="";a.Refresh();int before=a.draws;RebirthUiProjectionTextCache.Revision++;a.Refresh();
        Check(a.draws==before+1&&a.library[0].Name=="musicB1","text revision redraws names");
        before=a.draws;RebirthSurvivorDefinitionRegistry.Bundle=new object();a.Refresh();Check(a.draws==before+1,"definition identity redraws");
        a.selected=a.library[0];a.drag=a.selected;RebirthMusicLibraryClient.Generation++;a.Refresh();
        Check(a.selected==null&&a.drag==null,"custody generation clears selection and drag");
        a.audioTab=true;a.projectionDirty=true;RebirthAudiobookLibraryClient.Items.Add("audioA");RebirthAudiobookLibraryClient.SlotIds.Add("slot1");a.Refresh();
        Check(a.library[0].Audio&&a.library[0].SlotId=="slot1","tab changes catalogue source");
        a.selected=a.library[0];RebirthAudiobookLibraryClient.SlotIds[0]="slot2";a.Refresh();Check(a.selected==null,"same item replaced audio custody slot invalidates selection");
        a.Player.bag.ItemGrid.items=new[]{new ItemStack(new ItemValue("musicZ","z"),1),new ItemStack(new ItemValue("musicA","a"),1)};
        a.sort=true;a.projectionDirty=true;a.Refresh();before=a.draws;int clones=ItemValue.Clones;a.Refresh();
        Check(a.carried[0].Id=="musicA"&&a.draws==before&&ItemValue.Clones==clones,"sorted unchanged inventory retains exact unsorted observation");
        before=a.draws;RebirthMusicLibraryService.Fail=true;try{a.Refresh();throw new Exception("expected encoding failure");}catch(InvalidOperationException){}
        RebirthMusicLibraryService.Fail=false;a.Refresh();Check(a.draws==before+1,"failed projection retries complete redraw");
        before=a.draws;RebirthMusicLibraryClient.Current=false;a.Refresh();Check(a.draws==before,"invalid owner context does not publish projection");
        Console.WriteLine(passed+" scenarios passed; extracted actual Refresh, simulated native dependencies.");
    }
}
class Label{public string Text="";}
class EntityPlayerLocal{public World world=new();public Bag bag=new();}
class World{public bool IsRemote()=>false;}
class Bag{public Grid ItemGrid=new();}
class Grid{public ItemStack[] items=Array.Empty<ItemStack>();}
class ItemStack{public ItemValue itemValue;public int count;public ItemStack(ItemValue v,int c){itemValue=v;count=c;}public bool IsEmpty()=>count<=0;}
class ItemValue{public static int Clones;public string Payload;public ItemClass ItemClass;public ItemValue(string id,string p){ItemClass=new(id);Payload=p;}public ItemValue Clone(){Clones++;return new(ItemClass.GetItemName(),Payload);}}
class ItemClass{string id;public ItemClass(string id){this.id=id;}public string GetItemName()=>id;}
static class RebirthMusicLibraryClient{public static bool Current=true;public static bool EnsureCurrent(EntityPlayerLocal p)=>Current;public static long Generation;public static List<string> Items=new();}
static class RebirthAudiobookLibraryClient{public static long Revision;public static List<string> Items=new(),SlotIds=new();public static void RefreshLocal(EntityPlayerLocal p){} }
static class RebirthUiProjectionTextCache{public static long Revision;}
static class RebirthSurvivorDefinitionRegistry{public static object Bundle=new();}
static class RebirthMusicLibraryService{public const int Capacity=120;public static bool IsMusicCassette(string id)=>id.StartsWith("music");public static bool Fail;public static string Encode(ItemValue v){if(Fail)throw new InvalidOperationException();return v.ItemClass.GetItemName()+":"+v.Payload;}}
static class RebirthAudiobookLibraryPersistence{public const int Capacity=120;}
static class RebirthProgressionRuntimeConfig{public static bool TryGetAudiobook(string id,out object ignored){ignored=null;return id.StartsWith("audio");}}