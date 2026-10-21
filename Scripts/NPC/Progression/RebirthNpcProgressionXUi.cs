// Retained entry point for callers: NPC progression lives in the existing manager.
public static class RebirthNpcProgressionUiService
{
    public static bool Open(XUi xui,RebirthNpcStableId id,out string error)
    {
        error=string.Empty;if(xui==null)return false;
        RebirthCompanionUiService.Open(xui,"N:"+id,"Stats");return true;
    }
}
