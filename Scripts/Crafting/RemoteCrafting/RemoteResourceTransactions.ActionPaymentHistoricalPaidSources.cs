using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

public static partial class RemoteResourceTransactions
{
    // Only a fully saved paid obligation can retain this descriptor. It has
    // no native endpoint, Source, Grid, NPC adapter or financial mutation scope.
    private static ActionLeg RestoreHistoricalPaidActionSource(ActionTargetNativeOriginal target,XElement row,int index,
        ItemStack[] before,ItemStack[] after,bool[] locks,int[] slots,ItemStack[] paid,Dictionary<string,int> events)
    {
        if(target.BeforeFirstGrant||!events.TryGetValue("paid",out int paidSequence)||!events.TryGetValue("paying",out int payingSequence))return null;
        int kind=(int)row.Attribute("kind");string incarnation=(string)row.Attribute("incarnation"),id=(string)row.Attribute("id");
        if(kind<1||kind>5||string.IsNullOrWhiteSpace(id)||string.IsNullOrWhiteSpace(incarnation))return null;
        if(kind<5&&(!Guid.TryParseExact(incarnation,"N",out var nativeBirth)||nativeBirth==Guid.Empty))return null;
        string prefix="source-"+index;
        if(!events.TryGetValue(prefix+"-before-saved",out int beforeSequence)||!events.TryGetValue(prefix+"-after-saved",out int afterSequence)||
            beforeSequence>=payingSequence||afterSequence<=payingSequence||afterSequence>=paidSequence||events.Keys.Any(stage=>stage.StartsWith(prefix+"-",StringComparison.Ordinal)&&stage.Contains("return")))return null;
        if(kind==5)
        {
            if(!events.TryGetValue(prefix+"-debit-intent",out int intent)||!events.TryGetValue(prefix+"-debited",out int returned)||
                intent<=payingSequence||returned<=intent||returned>=afterSequence||row.Element("nativeBody")==null)return null;
        }
        else foreach(int slot in slots)
        {
            string slotPrefix=prefix+"-slot-"+slot;
            if(!events.TryGetValue(slotPrefix+"-debit-intent",out int intent)||!events.TryGetValue(slotPrefix+"-debited",out int returned)||
                intent<=payingSequence||returned<=intent||returned>=afterSequence)return null;
        }
        if(!ReadHistoricalActionSourceArtifact(target,prefix+"-before-saved",beforeSequence,kind==5?8:48)||
            !ReadHistoricalActionSourceArtifact(target,prefix+"-after-saved",afterSequence,kind==5?8:48))return null;
        var leg=new ActionLeg(id,null,before,after,locks,slots,paid,null,null,null,false,false,(int)row.Attribute("revision"),
            Guid.ParseExact((string)row.Attribute("debit"),"N"),Guid.ParseExact((string)row.Attribute("return"),"N"))
            {HistoricalPaid=true,BeforeSaved=true,AfterSaved=true,State=ActionLegState.Debited,Progress=ItemStack.Clone(after)};
        for(int n=0;n<slots.Length;n++){leg.AttemptedSlots[n]=true;leg.DebitedSlots[n]=true;}
        return leg;
    }
    private static bool ReadHistoricalActionSourceArtifact(ActionTargetNativeOriginal target,string stage,int sequence,int maximumMiB,string artifactName=null)
    {
        string eventPath=Path.Combine(target.Directory,sequence.ToString("D6")+"-"+stage+".xml"),artifactPath=Path.Combine(target.Directory,artifactName??stage+".native.xml");ActionPath(eventPath);ActionPath(artifactPath);
        long eventLength=new FileInfo(eventPath).Length,length=new FileInfo(artifactPath).Length;
        if(eventLength<1||eventLength>24L*1024*1024||length<1||length>(long)maximumMiB*1024*1024||
            !RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(8*eventLength+2*length+65536),out var memory))return false;
        using(memory)
        {
            byte[] eventBytes=ActionBytes(eventPath,(int)eventLength);var image=ReadActionBoundedXml(eventBytes,24*1024*1024);var receipt=image.Element("native");
            if(image.Name!="actionPaymentEvent"||(int?)image.Attribute("version")!=1||(int?)image.Attribute("sequence")!=sequence||(string)image.Attribute("stage")!=stage||
                image.Element("original")?.Value!=target.Original||receipt==null||(string)receipt.Attribute("file")!=Path.GetFileName(artifactPath)||(long?)receipt.Attribute("length")!=length)return false;
            string digest=(string)receipt.Attribute("digest");if(digest?.Length!=64||digest.Any(value=>value<'0'||value>'9'&&value<'a'||value>'f'))return false;
            byte[] bytes=ReadActionMaterial(artifactPath,(int)length);
            return bytes.LongLength==length&&ActionNativeMaterialDigest(bytes)==digest&&ActionEnvelopeFileEquals(eventPath,eventBytes)&&target.Current();
        }
    }
}
