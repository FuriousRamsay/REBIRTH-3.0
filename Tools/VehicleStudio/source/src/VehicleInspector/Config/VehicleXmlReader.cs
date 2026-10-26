using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using RebirthVehicleInspector.Model;

namespace RebirthVehicleInspector.Config
{
    internal sealed class VehicleXmlReader
    {
        private readonly string xmlPath;
        public VehicleXmlReader(string gameFolder) { xmlPath=Path.Combine(gameFolder,"Data","Config","vehicles.xml"); }
        public string SourcePath => xmlPath;

        public VehicleConfig Read(string vehicleName)
        {
            var c = new VehicleConfig { name=vehicleName, filePath=xmlPath, status="unknown" };
            if (string.IsNullOrWhiteSpace(vehicleName)) { c.status="not_bound"; return c; }
            if (!File.Exists(xmlPath)) { c.status="file_missing"; return c; }
            var doc = new XmlDocument { XmlResolver=null };
            try { doc.Load(xmlPath); }
            catch(Exception ex) { c.status="invalid_xml:" + ex.GetType().Name; return c; }
            XmlNode vehicle = null;
            foreach (XmlNode n in doc.SelectNodes("/vehicles/vehicle"))
                if (string.Equals(n.Attributes?["name"]?.Value,vehicleName,StringComparison.Ordinal)) { vehicle=n; break; }
            if (vehicle == null) { c.status="vehicle_not_found"; return c; }
            c.status="ok";
            var root = new List<Pair>(); var parts=new List<PartConfig>(); var seats=new List<SeatConfig>();
            foreach (XmlNode p in vehicle.ChildNodes)
            {
                if (p.Name!="property") continue;
                string cls=p.Attributes?["class"]?.Value;
                if (string.IsNullOrEmpty(cls))
                {
                    Pair pp=ReadProp(p); if(pp != null) root.Add(pp);
                    continue;
                }
                var childProps = new List<Pair>();
                foreach (XmlNode sub in p.ChildNodes) { Pair pp=ReadProp(sub); if(pp != null) childProps.Add(pp); }
                var part = new PartConfig { className=cls,properties=childProps.ToArray() }; parts.Add(part);
                if (cls.StartsWith("seat",StringComparison.OrdinalIgnoreCase) && int.TryParse(cls.Substring(4),out int index))
                {
                    seats.Add(new SeatConfig { index=index,
                        pose=Val(childProps,"pose"), position=Val(childProps,"position"),
                        rotation=Val(childProps,"rotation"), exit=Val(childProps,"exit"),
                        mod=Val(childProps,"mod"),modT=Val(childProps,"modT"),properties=childProps.ToArray()});
                }
            }
            c.rootProperties=root.ToArray(); c.parts=parts.ToArray(); c.seats=seats.OrderBy(s=>s.index).ToArray();
            c.prefab=Val(root,"prefab");
            return c;
        }
        private static Pair ReadProp(XmlNode n)
        {
            if (n.Name!="property") return null;
            string key=n.Attributes?["name"]?.Value, val=n.Attributes?["value"]?.Value;
            return key==null ? null : new Pair {key=key,value=val ?? string.Empty};
        }
        private static string Val(IEnumerable<Pair> props,string key)
        {
            foreach (var p in props) if(p.key==key) return p.value;
            return null;
        }
    }
}
