// Prepared, NOT RUN. Actual production candidate predicate; temporary files only.
using System;
using System.IO;
internal static class RebirthBossPublicationCandidatesFixture
{
    internal static void Verify()
    {
        string root=Path.Combine(Path.GetTempPath(),"RebirthBossCandidates_"+Guid.NewGuid().ToString("N"));
        string path=Path.Combine(root,"RebirthBossEvents.dat");
        try
        {
            Need(!RebirthBossEventPersistence.HasRetainedPublicationCandidates(path));
            Directory.CreateDirectory(root);
            Need(!RebirthBossEventPersistence.HasRetainedPublicationCandidates(path));
            File.WriteAllText(Path.Combine(root,"unrelated.tmp"),"control");
            Need(!RebirthBossEventPersistence.HasRetainedPublicationCandidates(path));
            foreach(string suffix in new[]{".tmp",".bak",".corrupt.abc",".candidate.abc"})
            {
                string evidence=path+suffix;File.WriteAllText(evidence,"retained original");
                Need(RebirthBossEventPersistence.HasRetainedPublicationCandidates(path));
                Need(File.ReadAllText(evidence)=="retained original");
                File.Delete(evidence);
            }
            Need(!RebirthBossEventPersistence.HasRetainedPublicationCandidates(path));
        }
        finally
        {
            if(Directory.Exists(root))
            {
                foreach(string file in Directory.GetFiles(root, "*", SearchOption.TopDirectoryOnly))File.Delete(file);
                Directory.Delete(root,false);
            }
        }
    }
    private static void Need(bool value){if(!value)throw new InvalidOperationException("Boss candidate retention contract failed.");}
}
