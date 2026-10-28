// Deferred actual-assembly fixture, NOT RUN. Caller prepares rejected native mode;
// this fixture never changes game/save flags, loads worlds or restores state.
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

internal static class RebirthBossSaveModeFixture
{
    internal static void VerifyRejectedPublication()
    {
        string directory=GameIO.GetSaveGameDir();
        if(directory.IndexOf("CodexTest",StringComparison.OrdinalIgnoreCase)<0)throw new InvalidOperationException("Disposable CodexTest required.");
        if(RebirthBossEventPersistence.IsReady&&GameManager.bSavingActive&&!GameManager.Instance.IsEditMode()&&ConnectionManager.Instance!=null&&ConnectionManager.Instance.IsServer&&ThreadManager.IsMainThread())
            throw new InvalidOperationException("Prepare a rejected publication mode first.");
        string before=Fingerprint(directory);bool rejected=false;
        try{RebirthBossEventPersistence.Save();}catch(InvalidOperationException){rejected=true;}
        if(!rejected||before!=Fingerprint(directory))throw new InvalidOperationException("Rejected save modified boss evidence.");
    }
    private static string Fingerprint(string directory)
    {
        if(!Directory.Exists(directory))return "absent";
        using(var hash=SHA256.Create())
            return string.Join("\n",Directory.GetFiles(directory,"RebirthBossEvents.dat*",SearchOption.TopDirectoryOnly).OrderBy(path=>path,StringComparer.Ordinal).Select(path=>Path.GetFileName(path)+":"+BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(path)))));
    }
}
