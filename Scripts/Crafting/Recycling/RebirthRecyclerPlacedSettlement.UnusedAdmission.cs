using System.IO;
using System.Text;
using System.Xml.Linq;

internal static partial class RebirthRecyclerPlacedSettlement
{
    private static void RetireUnusedAdmission(AuthorityOriginal original)
    {
        // This state is issued synchronously before BeginBefore succeeds.
        // No station principal or owner slot setter has been entered. The
        // outcome records that fact; no missing receipt is treated as proof.
        if(!original.UnusedAdmission||original.Station!=null||original.Owner!=null)return;
        var source=original.Source;
        var unused=new XElement("workstationCompletionIntent",new XAttribute("operation",source.Operation.ToString("N")),
            new XAttribute("world",source.WorldKey),new XElement("before",XElement.Parse(Encoding.UTF8.GetString(source.Image))),
            new XElement("recyclerUnusedAdmission",new XAttribute("nativeStationEntered",false),new XAttribute("nativeOwnerEntered",false)));
        string path=Path.Combine(RebirthWorkstationCompletionOutbox.Root("recyclerPlaced"),source.Operation.ToString("N")+".unused-admission.xml");
        if(!RebirthWorkstationCompletionPublication.Write(path,unused)||!XNode.DeepEquals(unused,RebirthWorkstationCompletionPublication.ReadPrimary(path)))return;
        if(original.Access!=null&&!original.Access.ObserveUnused(source))return;
        authorityOriginals.Remove(original);original.Dispose();
    }
}
