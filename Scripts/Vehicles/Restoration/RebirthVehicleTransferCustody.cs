using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;

// Immutable description of the existing cursor/native owner reservation.
// Construction/parsing is not authority; native receipt checks are independent.
public sealed class RebirthVehicleTransferCustody
{
    public Guid Reservation {get;private set;}
    public Guid Binding {get;private set;}
    public string OwnerImageHash {get;private set;}
    public bool CursorDebit {get;private set;}
    public bool CursorCredit {get;private set;}
    internal RebirthVehicleTransferCustody(Guid reserve,Guid binding,string hash,bool debit,bool credit)
    {
        if(reserve==Guid.Empty||binding==Guid.Empty||hash==null||hash.Length!=64||hash.Any(c=>!(c>='0'&&c<='9'||c>='a'&&c<='f')))throw new InvalidDataException("Invalid vehicle native reservation description.");
        Reservation=reserve;Binding=binding;OwnerImageHash=hash;CursorDebit=debit;CursorCredit=credit;
    }
    internal static string Hash(string value)
    {using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-","").ToLowerInvariant();}
    internal static string Slots(ItemStack[] slots)
    {
        if(slots==null||slots.Length>169)throw new InvalidDataException("Invalid native owner dimensions.");
        return string.Join(";",slots.Select(item=>item==null?"0:":item.count.ToString(CultureInfo.InvariantCulture)+":"+
            (item.itemValue==null||item.itemValue.IsEmpty()?string.Empty:RebirthNativeItemCodec.Encode(item.itemValue))));
    }
    internal static string Image(ItemStack[] bag,ItemStack[] belt)=>Hash(Slots(bag)+"|"+Slots(belt));
    internal static Guid Bind(Guid id,Guid reserve,string owner,string world,string creation,string fingerprint,string image)
    {
        using(var sha=SHA256.Create())
        {var hash=sha.ComputeHash(Encoding.UTF8.GetBytes("vehicle-custody-source-v1|"+id.ToString("N")+"|"+reserve.ToString("N")+"|"+owner+"|"+world+"|"+creation+"|"+fingerprint+"|"+image));var bytes=new byte[16];Array.Copy(hash,bytes,16);return new Guid(bytes);}
    }
    internal XElement Write()=>new XElement("custody",new XAttribute("reservation",Reservation),new XAttribute("binding",Binding),new XAttribute("image",OwnerImageHash),new XAttribute("cursorDebit",CursorDebit),new XAttribute("cursorCredit",CursorCredit));
    internal static RebirthVehicleTransferCustody Read(XElement element)
    {
        if(element==null||element.Name!="custody"||element.HasElements||element.Attributes().Count()!=5)throw new InvalidDataException();
        return new RebirthVehicleTransferCustody(Guid.Parse((string)element.Attribute("reservation")),Guid.Parse((string)element.Attribute("binding")),(string)element.Attribute("image"),(bool)element.Attribute("cursorDebit"),(bool)element.Attribute("cursorCredit"));
    }
}
