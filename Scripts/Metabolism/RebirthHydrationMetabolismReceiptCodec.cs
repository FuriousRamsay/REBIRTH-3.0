using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

// Persistence description only. Successful decode never grants a native write,
// a saved-leg witness, operation replay or custody release.
internal static class RebirthHydrationMetabolismReceiptCodec
{
    private const int MaximumBytes=1024*1024;
    internal static XElement Write(RebirthHydrationTransferOriginal original,string creation,string nativeGeneration)
    {
        if(original!=null&&NativeMatches(original,nativeGeneration))
        {
            var node=Write(original,nativeGeneration);
            node.SetAttributeValue("scope","native");
            if(!Bounded(node))throw new InvalidDataException("Hydration receipt exceeds persistence bound");
            return node;
        }
        return Write(original,creation);
    }
    internal static bool TryRead(XElement node,string creation,string nativeGeneration,out RebirthHydrationTransferOriginal original)
    {
        original=null;
        try
        {
            if(node==null)return false;
            if(!node.HasAttributes)return TryRead(node,creation,out original);
            // Only the explicit native scope may bind a receipt to the separate
            // persisted generation. Legacy receipts keep their old exact scope.
            if(node.Attributes().Count()!=1||(string)node.Attribute("scope")!="native"||
                !CanonicalGeneration(nativeGeneration)||!Bounded(node))return false;
            var detached=new XElement(node);detached.Attribute("scope").Remove();
            if(!TryRead(detached,nativeGeneration,out var candidate)||!NativeMatches(candidate,nativeGeneration))return false;
            original=candidate;return true;
        }
        catch{return false;}
    }
    private static bool NativeMatches(RebirthHydrationTransferOriginal original,string generation)
        =>CanonicalGeneration(generation)&&original.Creation==generation;
    private static bool CanonicalGeneration(string value)
        =>value!=null&&Guid.TryParseExact(value,"N",out var id)&&id!=Guid.Empty&&id.ToString("N")==value;
    internal static XElement Write(RebirthHydrationTransferOriginal original,string creation)
    {
        if(original==null||!RebirthSurvivorRequestScope.Matches(original.Creation,creation))
            throw new InvalidDataException("Hydration receipt creation mismatch");
        var node=new XElement("hydrationTransferReceipt",original.Write());
        if(!Bounded(node))throw new InvalidDataException("Hydration receipt exceeds persistence bound");
        return node;
    }
    internal static bool TryRead(XElement node,string creation,out RebirthHydrationTransferOriginal original)
    {
        original=null;
        try
        {
            // The repository's atomic writer indents XML. Normalize only its
            // insignificant element whitespace before validating the canonical
            // receipt; bound the original input before making a detached copy.
            if(node==null||!Bounded(node))return false;
            node=new XElement(node);
            foreach(var text in node.DescendantNodes().OfType<XText>().Where(value=>value.GetType()==typeof(XText)&&string.IsNullOrWhiteSpace(value.Value)).ToArray())text.Remove();
            if(node==null||node.Name!="hydrationTransferReceipt"||node.HasAttributes||
                node.Elements().Count()!=1||node.Nodes().Any(child=>!(child is XElement))||!Bounded(node)||
                !RebirthHydrationTransferOriginal.TryRead(node.Elements().Single(),out var candidate)||
                !RebirthSurvivorRequestScope.Matches(candidate.Creation,creation))return false;
            original=candidate;return true;
        }
        catch{return false;}
    }
    private static bool Bounded(XElement node)
    {
        string image=node.ToString(SaveOptions.DisableFormatting);
        return image.Length<=MaximumBytes&&Encoding.UTF8.GetByteCount(image)<=MaximumBytes;
    }
}
