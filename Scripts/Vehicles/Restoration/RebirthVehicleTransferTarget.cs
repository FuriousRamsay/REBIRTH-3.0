using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;

// Entity targets resolve by the plan's stable assembly ID, never a recycled entity ID.
// Block targets additionally resolve at their saved world coordinates. World/session
// identity belongs to the containing scoped store; neither descriptor authorizes access.
public sealed class RebirthVehicleTransferTarget
{
    public RebirthVehicleAssemblyCarrier Carrier { get; private set; }
    public int X { get; private set; }
    public int Y { get; private set; }
    public int Z { get; private set; }

    public RebirthVehicleTransferTarget(RebirthVehicleAssemblyCarrier carrier, int x = 0, int y = 0, int z = 0)
    {
        if (carrier != RebirthVehicleAssemblyCarrier.RepairableBlock && carrier != RebirthVehicleAssemblyCarrier.VehicleEntity)
            throw new InvalidDataException("Invalid vehicle transfer carrier.");
        if (carrier == RebirthVehicleAssemblyCarrier.VehicleEntity && (x != 0 || y != 0 || z != 0))
            throw new InvalidDataException("Entity transfer must resolve by stable assembly identity.");
        Carrier = carrier; X = x; Y = y; Z = z;
    }

    public XElement ToXml()
    {
        return new XElement("target", new XAttribute("carrier", (byte)Carrier),
            new XAttribute("x", X), new XAttribute("y", Y), new XAttribute("z", Z));
    }

    public static RebirthVehicleTransferTarget Read(XElement element)
    {
        byte carrier; int x, y, z;
        if (element == null || element.Name != "target" || element.HasElements || element.Attributes().Count() != 4
            || !byte.TryParse((string)element.Attribute("carrier"), NumberStyles.Integer, CultureInfo.InvariantCulture, out carrier)
            || !int.TryParse((string)element.Attribute("x"), NumberStyles.Integer, CultureInfo.InvariantCulture, out x)
            || !int.TryParse((string)element.Attribute("y"), NumberStyles.Integer, CultureInfo.InvariantCulture, out y)
            || !int.TryParse((string)element.Attribute("z"), NumberStyles.Integer, CultureInfo.InvariantCulture, out z))
            throw new InvalidDataException("Invalid vehicle transfer target record.");
        return new RebirthVehicleTransferTarget((RebirthVehicleAssemblyCarrier)carrier, x, y, z);
    }
}