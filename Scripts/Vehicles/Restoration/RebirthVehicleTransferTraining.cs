using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;

// Frozen pricing for the pre-existing mechanics install/replace/repair event.
// A description never grants training; the native completion producer does.
public sealed class RebirthVehicleTransferTraining
{
    public float Raw{get;private set;}public string Source{get;private set;}
    internal RebirthVehicleTransferTraining(float raw,string source)
    {if(float.IsNaN(raw)||float.IsInfinity(raw)||raw<0f||raw>10f||string.IsNullOrEmpty(source)||source.Length>2048)throw new InvalidDataException("Invalid vehicle training original.");Raw=raw;Source=source;}
    internal XElement Write()=>new XElement("training",new XAttribute("raw",Raw.ToString("R",CultureInfo.InvariantCulture)),new XAttribute("source",Source));
    internal static RebirthVehicleTransferTraining Read(XElement element)
    {if(element==null||element.Name!="training"||element.HasElements||element.Attributes().Count()!=2||!float.TryParse((string)element.Attribute("raw"),NumberStyles.Float,CultureInfo.InvariantCulture,out var raw))throw new InvalidDataException("Invalid vehicle training frame.");return new RebirthVehicleTransferTraining(raw,(string)element.Attribute("source"));}
}
