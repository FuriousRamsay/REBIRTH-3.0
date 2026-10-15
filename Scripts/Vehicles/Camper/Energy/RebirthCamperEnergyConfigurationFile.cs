using System;
using System.Globalization;
using System.Xml;

// Explicit bootstrap loader, no background file watchers or global patch scanner.
public static class RebirthCamperEnergyConfigurationFile
{
    public static void Load(string absolutePath)
    {
        var settings=new XmlReaderSettings {DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=8192};
        var document=new XmlDocument {XmlResolver=null};
        using(var reader=XmlReader.Create(absolutePath,settings))document.Load(reader);
        var root=document.DocumentElement;
        if(root==null||root.Name!="CamperEnergy"||root.GetAttribute("version")!="1")throw new FormatException("Unsupported camper energy configuration.");
        // Optional for legacy config compatibility, but never allow an authored
        // multiplier to silently contradict the approved twice-normal policy.
        if(root.HasAttribute("engineerRangeMultiplier")&&Number(root,"engineerRangeMultiplier")!=2f)
            throw new FormatException("Engineer electrical/claim radius multiplier must be exactly 2.");
        var names=root.GetAttribute("sourcePriority").Split(',');
        if(names.Length!=3)throw new FormatException("Three unique native source kinds required.");
        var priority=new RebirthCamperEnergySourceKind[3];
        for(int i=0;i<3;i++)
            if(!Enum.TryParse(names[i].Trim(),false,out priority[i]))throw new FormatException("Unknown energy source priority.");
        // Validate adapter values before publishing the core configuration. A
        // malformed second range must not leave a partially updated config.
        float poiRange=Number(root,"poiRange"),defenseRange=Number(root,"defenseRange");
        int ordinaryLightWatts=root.HasAttribute("ordinaryPoiLightWatts")?int.Parse(root.GetAttribute("ordinaryPoiLightWatts"),CultureInfo.InvariantCulture):1;
        if(ordinaryLightWatts<1||ordinaryLightWatts>10000)throw new FormatException("Ordinary POI light demand must be 1..10000 provisional watts per light.");
        float normalRange=Number(root,"range");
        if(float.IsNaN(normalRange)||float.IsInfinity(normalRange)||normalRange<=0||normalRange*2f>200)
            throw new FormatException("Normal and doubled Engineer radius must fit the existing 200m projection bound.");
        if(float.IsNaN(poiRange)||float.IsInfinity(poiRange)||poiRange<=0||poiRange>200
            ||float.IsNaN(defenseRange)||float.IsInfinity(defenseRange)||defenseRange<=0||defenseRange>200)
            throw new FormatException("Finite positive bounded POI and defense ranges required.");
        RebirthCamperEnergyConfiguration.Configure(Number(root,"deploySeconds"),Number(root,"packSeconds"),normalRange,
            Number(root,"nativeTickSeconds"),int.Parse(root.GetAttribute("maximumSources"),CultureInfo.InvariantCulture),priority);
        RebirthCamperEnergyDeviceAdapter.Configure(poiRange,defenseRange,ordinaryLightWatts);
    }
    private static float Number(XmlElement root,string name)=>float.Parse(root.GetAttribute(name),CultureInfo.InvariantCulture);
}
