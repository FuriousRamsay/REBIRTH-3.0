// AUTHORED_NOT_RUN. Native PDS field/projection/grid doubles are explicit.
// This checks actual frame qualification, not native serialization or a save event.
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
sealed class PlayerDataFile
{
    internal byte[] Bag={1,2,3,4},Belt={5,6,7,8},Progression={9,10,11};
    internal MemoryStream equipmentData=new MemoryStream(new byte[]{12,13,14,15});
}
static class RebirthGearPlayerDataInventory
{internal static byte[] ReadSlots(PlayerDataFile data,bool bag)=>bag?data.Bag:data.Belt;}
static class RebirthProductiveEarnedUiOwnerFile
{
    private const int Maximum=16*1024*1024;
    private static byte[] Slots(byte[] bytes)=>bytes;
    private static byte[] Projection(PlayerDataFile data,string recipe)=>data.Progression;
    // ACTUAL_MATCHER
    // ACTUAL_ELEMENTS
}
static class Program
{
    static int cases;
    static string Image(byte[] value)=>Convert.ToBase64String(value);
    static XElement Frame(PlayerDataFile data,int version)
    {
        var result=new XElement("nativeOwnerFrame",new XAttribute("version",version),
            new XElement("recipes",new XElement("recipe",new XAttribute("name","armor"),new XElement("projection",Image(data.Progression)))),
            new XElement("bag",Image(data.Bag)),new XElement("belt",Image(data.Belt)));
        if(version==2)result.Add(new XElement("equipment",Image(data.equipmentData.ToArray())));
        return result;
    }
    static void Check(bool condition,string name){if(!condition)throw new Exception(name);cases++;}
    public static void Main()
    {
        var data=new PlayerDataFile();var original=Frame(data,2);
        Check(RebirthProductiveEarnedUiOwnerFile.MatchesDeferredOwnerFrame(data,original),"matching actual PDS equipment");
        data.equipmentData=new MemoryStream(new byte[]{12,13,14,16});
        Check(!RebirthProductiveEarnedUiOwnerFile.MatchesDeferredOwnerFrame(data,original),"bag/belt equality cannot hide different equipment");
        Check(RebirthProductiveEarnedUiOwnerFile.MatchesDeferredOwnerFrame(data,Frame(data,2)),"new exact native image accepted");
        var missing=Frame(data,2);missing.Element("equipment").Remove();
        Check(!RebirthProductiveEarnedUiOwnerFile.MatchesDeferredOwnerFrame(data,missing),"version 2 requires equipment");
        var duplicate=Frame(data,2);duplicate.Add(new XElement(duplicate.Element("equipment")));
        Check(!RebirthProductiveEarnedUiOwnerFile.MatchesDeferredOwnerFrame(data,duplicate),"duplicate equipment rejected");
        Check(RebirthProductiveEarnedUiOwnerFile.MatchesDeferredOwnerFrame(data,Frame(data,1)),"legacy version 1 retains old semantics");
        var disguised=Frame(data,2);disguised.SetAttributeValue("version",1);
        Check(!RebirthProductiveEarnedUiOwnerFile.MatchesDeferredOwnerFrame(data,disguised),"legacy shape cannot smuggle new field");
        var whitespace=Frame(data,2);whitespace.Element("equipment").Value+=" ";
        Check(!RebirthProductiveEarnedUiOwnerFile.MatchesDeferredOwnerFrame(data,whitespace),"equipment image must be canonical");
        Console.WriteLine("MP028 actual equipment owner-frame matcher: "+cases+" cases passed; native adapters remain doubles.");
    }
}
