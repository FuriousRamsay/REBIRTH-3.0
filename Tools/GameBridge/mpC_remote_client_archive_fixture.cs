using System;
using System.IO;

// Deferred source fixture. Link the ACTUAL production
// RebirthCamperTransferJournal.cs and RebirthRemoteResourceClientOriginalJournal.cs.
// No native owner/payment effects or execution coverage are simulated here.
internal static class RemoteClientArchiveChecks
{
    private static void Require(bool value,string label){if(!value)throw new Exception(label);}
    private static void Refuses(Action action,string label)
    {try{action();}catch(InvalidDataException){return;}throw new Exception(label);}
    private static void Main()
    {
        string root=Path.Combine(Path.GetTempPath(),"REBIRTH_mpC_archive_"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        var original=Guid.NewGuid();var vehicle=Guid.NewGuid();byte[] before={1,3,5},after={2,4,6};
        var journal=new RebirthRemoteResourceClientOriginalJournal(root);
        journal.Prepare(original,vehicle,"native-mode:fixture",0,before,after);
        journal.Advance(original,RebirthCamperTransferJournal.Phase.Prepared,RebirthCamperTransferJournal.Phase.Uncertain);
        journal.Advance(original,RebirthCamperTransferJournal.Phase.Uncertain,RebirthCamperTransferJournal.Phase.Applied);
        Require(!File.Exists(Path.Combine(root,original.ToString("N")+".0.rcj")),"terminal leaves active scan only after complete archive");
        Require(journal.Read(original).State==RebirthCamperTransferJournal.Phase.Applied,"same ID reads archived terminal");
        Require(journal.Prepare(original,vehicle,"native-mode:fixture",0,before,after).State==RebirthCamperTransferJournal.Phase.Applied,"duplicate original cannot replay");
        Refuses(()=>journal.Prepare(original,vehicle,"native-mode:fixture",0,new byte[]{9},after),"immutable archived body cannot be replaced");
        // Native process interruption during archive copying, before primary
        // cleanup: a copied prefix is evidence only, primary is still complete.
        var interrupted=Guid.NewGuid();var primary=new RebirthCamperTransferJournal(root);
        primary.Prepare(interrupted,vehicle,"native-mode:fixture",0,before,after);
        primary.Advance(interrupted,RebirthCamperTransferJournal.Phase.Prepared,RebirthCamperTransferJournal.Phase.Uncertain);
        primary.Advance(interrupted,RebirthCamperTransferJournal.Phase.Uncertain,RebirthCamperTransferJournal.Phase.Applied);
        string archive=Path.Combine(root,"completed",interrupted.ToString("N"));Directory.CreateDirectory(archive);
        string name=interrupted.ToString("N")+".0.rcj";File.Copy(Path.Combine(root,name),Path.Combine(archive,name));
        Require(journal.Read(interrupted).State==RebirthCamperTransferJournal.Phase.Applied,"partial mirror does not replace complete primary");
        journal.Archive(interrupted);
        string uncertain=interrupted.ToString("N")+".1.rcj";File.Copy(Path.Combine(archive,uncertain),Path.Combine(root,uncertain));
        Require(journal.Read(interrupted).State==RebirthCamperTransferJournal.Phase.Applied,"interrupted duplicate cleanup reads complete matching archive");
        journal.Archive(interrupted);
        File.WriteAllBytes(Path.Combine(root,uncertain),new byte[]{9,9,9});
        Refuses(()=>journal.Read(interrupted),"conflicting primary never silently falls back to archive");
        Console.WriteLine("PASS actual domain terminal archival; native MP scenarios remain separate. Artifacts: "+root);
    }
}
