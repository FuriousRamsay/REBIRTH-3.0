#if DEBUG
using System;
using System.IO;
using System.Xml;

// Detached cache-step control flow plus explicit real pending-writer admission.
// No World, generation metadata, native actor, publication or ACK is fabricated.
internal static class RebirthNpcRecoveryCacheStepFixture
{
    // Calls the real ordinary publication admission on the real pending group.
    // No snapshot capture/publication, service replacement or synthetic G occurs.
    internal static void VerifyActualWriterFence()
    {
        if(!RebirthNpcQualificationMutationGuard.Allows("CodexTest")||!RebirthNpcPersistenceCoordinator.IsRecoveryInstalling)
            throw new InvalidOperationException("Actual disposable installing recovery required");
        var group=RebirthNpcLiveRecoveryGroup.Read(GameIO.GetSaveGameDir());
        if(group==null||group.Phase!=2||!RebirthNpcRecoveryFaults.IsPaused(group.Operation))
            throw new InvalidOperationException("Actual paused installed group2 required");
        if(!RebirthNpcLiveRecoverySession.OwnsAllWriterFences(group.Directory,group.Operation))
            throw new InvalidOperationException("Exact active original publication fence ownership unavailable");
        foreach(var member in group.Members)
        {
            string path=Path.Combine(group.Directory,member.File);
            if(RebirthNpcPersistenceFile.TryBeginWrite(path,out var admitted,out var error))
            {admitted.Dispose();throw new InvalidOperationException("Ordinary writer admitted during cache retirement");}
            if(string.IsNullOrEmpty(error))throw new InvalidOperationException("Writer refusal evidence unavailable");
        }
    }
    internal static void Verify()
    {
        int value=1,retire=0,restore=0;bool fail=true;
        var document=new XmlDocument();document.LoadXml("<detachedCache />");
        var domain=new RebirthNpcSaveQuarantineDomain("detached-cache-fixture",document,()=>value==1,
            ()=>{value=0;retire++;if(fail)throw new IOException("Detached fixture boundary");},()=>{value=1;restore++;});
        var step=new RebirthNpcRecoveryPrimitiveCaches.Step(domain,false);
        bool refused=false;try{step.Retire();}catch(IOException){refused=true;}
        Require(refused&&value==1&&restore==1&&!step.Retired&&step.IsCurrent);
        fail=false;step.Retire();Require(value==0&&retire==2&&restore==1&&step.Retired);
        value=7;step.Retire();Require(value==7&&retire==2); // later installed-G action is untouched
        int installed=19,calls=0;
        var current=new RebirthNpcSaveQuarantineDomain("installed-cache-fixture",document,()=>installed==19,()=>{calls++;installed=0;},()=>{});
        var preserved=new RebirthNpcRecoveryPrimitiveCaches.Step(current,true);preserved.Retire();installed=23;preserved.Retire();Require(installed==23&&calls==0&&preserved.Retired);
        bool owns=true;int held=9,detaches=0;
        var fencedDomain=new RebirthNpcSaveQuarantineDomain("fenced-cache-fixture",document,()=>held==9,()=>{held=0;detaches++;},()=>{held=9;});
        var fenced=new RebirthNpcRecoveryPrimitiveCaches.Step(fencedDomain,false);fenced.BindFence(()=>owns);
        fenced.Retire();Require(fenced.Retired&&fenced.IsCurrent&&detaches==1);
        owns=false;Require(!fenced.IsCurrent);refused=false;
        try{fenced.Retire();}catch(InvalidOperationException){refused=true;}
        Require(refused&&detaches==1&&held==0); // retired shortcut cannot bypass lost ownership
        var remaining=new RebirthNpcRecoveryPrimitiveCaches.Step(fencedDomain,false);remaining.BindFence(()=>owns);
        refused=false;try{remaining.Retire();}catch(InvalidOperationException){refused=true;}
        Require(refused&&!remaining.Retired&&detaches==1); // next pointer never moves
    }
    private static void Require(bool value){if(!value)throw new InvalidOperationException("Actual detached recovery cache step failed");}
}
#endif
