using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;

public sealed class RebirthVehicleTransferHealth
{
    public int Before { get; private set; }
    public int After { get; private set; }
    public int Maximum { get; private set; }
    public RebirthVehicleTransferHealth(int before, int after, int maximum)
    {
        if (maximum < 1 || before <= 0 || before >= maximum || after <= before || after > maximum)
            throw new InvalidDataException("Invalid native vehicle repair transition.");
        Before = before; After = after; Maximum = maximum;
    }
    public XElement ToXml()
    {
        return new XElement("health", new XAttribute("before", Before), new XAttribute("after", After), new XAttribute("maximum", Maximum));
    }
    public static RebirthVehicleTransferHealth Read(XElement element)
    {
        int before, after, maximum;
        if (element == null || element.Name != "health" || element.HasElements || element.Attributes().Count() != 3
            || !int.TryParse((string)element.Attribute("before"), NumberStyles.Integer, CultureInfo.InvariantCulture, out before)
            || !int.TryParse((string)element.Attribute("after"), NumberStyles.Integer, CultureInfo.InvariantCulture, out after)
            || !int.TryParse((string)element.Attribute("maximum"), NumberStyles.Integer, CultureInfo.InvariantCulture, out maximum))
            throw new InvalidDataException("Invalid vehicle repair transition record.");
        return new RebirthVehicleTransferHealth(before, after, maximum);
    }
}