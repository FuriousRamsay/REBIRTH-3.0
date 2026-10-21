using System;

internal static partial class RebirthProductiveEarnedCoordinator
{
    private static bool TryClassifyMobileRemoteToolFile(string prefix,string path,out Guid operation,out bool companion)
    {
        operation=Guid.Empty;companion=false;
        if(prefix==null||path==null||!path.StartsWith(prefix,StringComparison.Ordinal))return false;
        string name=path.Substring(prefix.Length);
        if(name.Length<32||!Guid.TryParseExact(name.Substring(0,32),"N",out operation)||operation==Guid.Empty)return false;
        string suffix=name.Substring(32);
        if(suffix.Length==0)return true;
        companion=suffix==".owner"||suffix==".saved"||suffix==".reentry"||suffix==".reentry.invoking";
        return companion;
    }
}
