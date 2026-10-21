using System;
using System.IO;

internal static class RebirthNpcQualificationMutationGuard
{
    internal static bool Allows(string confirmation)
    {
        if(!string.Equals(confirmation,"CodexTest",StringComparison.Ordinal)||!ThreadManager.IsMainThread())return false;
        var game=GameManager.Instance;var world=game?.World;
        if(world==null||world.IsRemote()||game.IsEditMode()||ConnectionManager.Instance==null||!ConnectionManager.Instance.IsServer)return false;
        string save=GameIO.GetSaveGameDir();
        if(string.IsNullOrEmpty(save))return false;
        string name=Path.GetFileName(Path.GetFullPath(save).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar));
        return string.Equals(name,"CodexTest",StringComparison.Ordinal);
    }
    internal const string Refused="result=FAIL: mutating qualification refused. Requires an authoritative disposable save named CodexTest and explicit confirm=CodexTest. Inspection does not run progression awards.";
}
