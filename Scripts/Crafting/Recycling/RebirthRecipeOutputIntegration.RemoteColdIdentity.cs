using System;
using System.Xml.Linq;

public static partial class RebirthRecipeOutputIntegration
{
    // Prefix publication uses action for an external cache; final publication
    // also carries nonce. Both identify the same original cached action.
    internal static bool MatchesRemoteColdRequestSource(XElement remote,XElement request,bool externalCached)
    {
        string nonce=(string)remote?.Attribute("nonce"),source=(string)remote?.Attribute("source");
        if(string.IsNullOrEmpty(nonce)||string.IsNullOrEmpty(source)||
            request?.Name!=(externalCached?"cachedViewer":"nativeViewer")||
            (string)request.Attribute("source")!=source||
            (string)request.Attribute(externalCached?"action":"nonce")!=nonce)return false;
        return externalCached?
            request.Attribute("nonce")==null||(string)request.Attribute("nonce")==nonce:
            request.Attribute("action")==null;
    }
}
