using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;

// Native vehicle fuel is saved separately from assembly metadata. Preserve exact
// levels/capacity so a coordinator can compare both, never infer a flush from revision.
public sealed class RebirthVehicleTransferFuel
{
    public float BeforeLevel { get; private set; }
    public float AfterLevel { get; private set; }
    public float Capacity { get; private set; }
    public RebirthVehicleTransferFuel(float beforeLevel, float afterLevel, float capacity)
    {
        if (!Finite(beforeLevel) || !Finite(afterLevel) || !Finite(capacity) || capacity <= 0f
            || beforeLevel < 0f || afterLevel < 0f || beforeLevel > capacity || afterLevel > capacity
            || beforeLevel == afterLevel)
            throw new InvalidDataException("Invalid native vehicle fuel transition.");
        BeforeLevel = beforeLevel; AfterLevel = afterLevel; Capacity = capacity;
    }
    private static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
    public XElement ToXml()
    {
        return new XElement("fuel", new XAttribute("before", BeforeLevel.ToString("R", CultureInfo.InvariantCulture)),
            new XAttribute("after", AfterLevel.ToString("R", CultureInfo.InvariantCulture)),
            new XAttribute("capacity", Capacity.ToString("R", CultureInfo.InvariantCulture)));
    }
    public static RebirthVehicleTransferFuel Read(XElement element)
    {
        float before, after, capacity;
        if (element == null || element.Name != "fuel" || element.HasElements || element.Attributes().Count() != 3
            || !float.TryParse((string)element.Attribute("before"), NumberStyles.Float, CultureInfo.InvariantCulture, out before)
            || !float.TryParse((string)element.Attribute("after"), NumberStyles.Float, CultureInfo.InvariantCulture, out after)
            || !float.TryParse((string)element.Attribute("capacity"), NumberStyles.Float, CultureInfo.InvariantCulture, out capacity))
            throw new InvalidDataException("Invalid vehicle fuel transition record.");
        return new RebirthVehicleTransferFuel(before, after, capacity);
    }
}