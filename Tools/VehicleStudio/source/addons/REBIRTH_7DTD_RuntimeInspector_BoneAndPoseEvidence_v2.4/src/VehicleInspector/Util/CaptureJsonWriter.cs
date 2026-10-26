using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using RebirthVehicleInspector.Model;

namespace RebirthVehicleInspector.Util
{
    /// <summary>
    /// Strict JSON export for our own [Serializable] DTOs. Intentionally independent
    /// of Unity JsonUtility, which omitted nested custom records in the live 7DTD build.
    /// No polymorphic Unity/game objects are serialized, just bounded DTO public fields.
    /// </summary>
    internal static class CaptureJsonWriter
    {
        private const int MaxDepth=24;
        internal static void Write(TextWriter writer,CaptureFile capture)
        {
            if(writer==null) throw new ArgumentNullException(nameof(writer));
            if(capture==null) throw new ArgumentNullException(nameof(capture));
            WriteValue(writer,capture,0);
        }
        private static void WriteValue(TextWriter writer,object value,int depth)
        {
            if(depth>MaxDepth) throw new InvalidDataException("Capture record exceeds maximum JSON depth.");
            if(value==null) { writer.Write("null"); return; }
            Type type=value.GetType();
            if(type==typeof(string)) { WriteString(writer,(string)value); return; }
            if(type==typeof(bool)) { writer.Write((bool)value?"true":"false"); return; }
            if(type.IsEnum) { WriteString(writer,value.ToString()); return; }
            if(type.IsArray)
            {
                Array items=(Array)value;
                if(items.Rank!=1) throw new InvalidDataException("Only one-dimensional capture arrays are supported.");
                writer.Write('[');
                for(int i=0;i<items.Length;i++)
                {
                    if(i>0) writer.Write(',');
                    WriteValue(writer,items.GetValue(i),depth+1);
                }
                writer.Write(']'); return;
            }
            switch(Type.GetTypeCode(type))
            {
                case TypeCode.Byte: case TypeCode.SByte:
                case TypeCode.Int16: case TypeCode.UInt16:
                case TypeCode.Int32: case TypeCode.UInt32:
                case TypeCode.Int64: case TypeCode.UInt64:
                case TypeCode.Decimal:
                    writer.Write(Convert.ToString(value,CultureInfo.InvariantCulture));return;
                case TypeCode.Single:
                    float f=(float)value;
                    if(float.IsNaN(f)||float.IsInfinity(f)) throw new InvalidDataException("Non-finite float in capture DTO.");
                    writer.Write(f.ToString("R",CultureInfo.InvariantCulture));return;
                case TypeCode.Double:
                    double d=(double)value;
                    if(double.IsNaN(d)||double.IsInfinity(d)) throw new InvalidDataException("Non-finite double in capture DTO.");
                    writer.Write(d.ToString("R",CultureInfo.InvariantCulture));return;
            }
            if(type.Namespace!="RebirthVehicleInspector.Model" || !type.IsSerializable)
                throw new InvalidDataException("Unexpected type in capture model: "+type.FullName);
            FieldInfo[] fields=type.GetFields(BindingFlags.Instance|BindingFlags.Public);
            Array.Sort(fields,(a,b)=>string.CompareOrdinal(a.Name,b.Name));
            writer.Write('{');
            bool first=true;
            foreach(FieldInfo field in fields)
            {
                if(field.IsStatic || field.IsNotSerialized) continue;
                if(!first) writer.Write(',');
                first=false;
                WriteString(writer,field.Name);
                writer.Write(':');
                WriteValue(writer,field.GetValue(value),depth+1);
            }
            writer.Write('}');
        }
        private static void WriteString(TextWriter writer,string value)
        {
            writer.Write('"');
            foreach(char c in value)
            {
                switch(c)
                {
                    case '"': writer.Write("\\\""); break;
                    case '\\': writer.Write("\\\\"); break;
                    case '\b': writer.Write("\\b"); break;
                    case '\f': writer.Write("\\f"); break;
                    case '\n': writer.Write("\\n"); break;
                    case '\r': writer.Write("\\r"); break;
                    case '\t': writer.Write("\\t"); break;
                    default:
                        if(c<0x20 || c=='\u2028' || c=='\u2029')
                        { writer.Write("\\u"); writer.Write(((int)c).ToString("x4",CultureInfo.InvariantCulture)); }
                        else writer.Write(c);
                        break;
                }
            }
            writer.Write('"');
        }
    }
}
