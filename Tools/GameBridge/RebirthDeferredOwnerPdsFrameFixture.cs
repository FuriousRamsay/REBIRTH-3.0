// Actual strict PDS frame matcher, explicit native projection/grid doubles.
// NOT_RUN. Does not qualify native upload, Save callbacks or durable receipts.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;
class ItemStack {internal byte Value;}
class PlayerDataFile
{
    internal Dictionary<string,byte[]> Projections=new Dictionary<string,byte[]>();
    internal ItemStack[] Bag,Belt;
}
static class RebirthGearPlayerDataInventory
{internal static ItemStack[] ReadSlots(PlayerDataFile data,bool bag)=>bag?data.Bag:data.Belt;}
internal static partial class RebirthProductiveEarnedUiOwnerFile
{
    private const int Maximum=16*1024*1024;
    private static byte[] Projection(PlayerDataFile data,string recipe)=>data.Projections[recipe];
    private static byte[] Slots(ItemStack[] slots)=>new[]{(byte)slots.Length,(byte)0,(byte)0,(byte)0}.Concat(slots.Select(slot=>slot.Value)).ToArray();
    private static byte[] NativeImage(PlayerDataFile data)=>throw new NotSupportedException("Native Save is outside this matcher fixture");
    private static bool ReadFinal(string path,byte[] image)=>throw new NotSupportedException("No native persistence double is proof");
    private static bool Safe(string path)=>throw new NotSupportedException();
}
// PRODUCTION_HOST_PDS_HELPER
static class RebirthDeferredOwnerPdsFrameFixture
{
    static void Need(bool condition,string reason){if(!condition)throw new Exception(reason);}
    static XElement Frame()=>new XElement("nativeOwnerFrame",new XAttribute("version",1),
        new XElement("recipes",new XElement("recipe",new XAttribute("name","recipe"),new XElement("projection",Convert.ToBase64String(new byte[]{1,2})))),
        new XElement("bag",Convert.ToBase64String(new byte[]{1,0,0,0,3})),new XElement("belt",Convert.ToBase64String(new byte[]{1,0,0,0,4})));
    public static void Main()
    {
        var data=new PlayerDataFile{Bag=new[]{new ItemStack{Value=3}},Belt=new[]{new ItemStack{Value=4}}};data.Projections["recipe"]=new byte[]{1,2};
        Need(RebirthProductiveEarnedUiOwnerFile.MatchesDeferredOwnerFrame(data,Frame()),"Exact native frame refused");
        var changed=Frame();changed.Element("bag").Value=Convert.ToBase64String(new byte[]{1,0,0,0,9});
        Need(!RebirthProductiveEarnedUiOwnerFile.MatchesDeferredOwnerFrame(data,changed),"Changed actual inventory accepted");
        changed=Frame();changed.Element("recipes").Add(new XElement(changed.Element("recipes").Element("recipe")));
        Need(!RebirthProductiveEarnedUiOwnerFile.MatchesDeferredOwnerFrame(data,changed),"Duplicate recipe projection accepted");
        changed=Frame();changed.Add(new XElement("unknown"));
        Need(!RebirthProductiveEarnedUiOwnerFile.MatchesDeferredOwnerFrame(data,changed),"Unknown native frame field accepted");
        changed=Frame();changed.Element("recipes").Element("recipe").Element("projection").Value=Convert.ToBase64String(new byte[]{1,3});
        Need(!RebirthProductiveEarnedUiOwnerFile.MatchesDeferredOwnerFrame(data,changed),"Changed native progression accepted");
        changed=Frame();changed.Element("belt").Add(new XAttribute("extra",1));
        Need(!RebirthProductiveEarnedUiOwnerFile.MatchesDeferredOwnerFrame(data,changed),"Unrecognized grid attributes accepted");
        changed=Frame();changed.Add(new XText("unrecognized frame content"));
        Need(!RebirthProductiveEarnedUiOwnerFile.MatchesDeferredOwnerFrame(data,changed),"Unrecognized text accepted");
        changed=Frame();changed.Element("bag").Value=" "+changed.Element("bag").Value;
        Need(!RebirthProductiveEarnedUiOwnerFile.MatchesDeferredOwnerFrame(data,changed),"Noncanonical bytes accepted");
        changed=Frame();changed.Element("bag").Value="invalid base64";
        try{RebirthProductiveEarnedUiOwnerFile.MatchesDeferredOwnerFrame(data,changed);throw new Exception("Malformed bytes accepted");}catch(FormatException){}
        Need(data.Bag[0].Value==3&&data.Belt[0].Value==4&&data.Projections["recipe"].SequenceEqual(new byte[]{1,2}),"Matcher modified owner data");
    }
}

