using System;using System.IO;using System.Xml;
class Program
{
 static int n;static void Check(bool condition,string name){if(!condition)throw new Exception(name);n++;}
 static void Main(string[] args)
 {
  string directory=Path.Combine(Path.GetTempPath(),"RebirthPreparedFixture-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
  try
  {
   var world=new World();GameManager.Instance.World=world;GameIO.Directory=directory;RebirthNpcWorldIntegrationService.loadedNativeWorld=world;RebirthNpcWorldIntegrationService.loadedNativeDirectory=directory;
   var stable=new RebirthNpcStableId(12,34);var person=new RebirthNpcPersistentRecord();person.Identity.StableNpcId=stable;RebirthNpcAggregatePersistenceStore.Person=person;
   Check(RebirthNpcPreparedPersonAdmission.TryValidate(person,out var profile,out _)&&profile=="specialist.medic","authored specialist retained");
   person.Lifecycle.TombstoneState=true;Check(!RebirthNpcPreparedPersonAdmission.TryValidate(person,out _,out _),"tombstone refused");person.Lifecycle.TombstoneState=false;
   person.Presence.PresenceState="AwaitingRespawn";Check(!RebirthNpcPreparedPersonAdmission.TryValidate(person,out _,out _),"respawn not bypassed");person.Presence.PresenceState="UnloadedPersistent";
   Check(RebirthNpcWorldIntegrationService.TryPrepareExistingPerson("original",stable,"medic",new Vector3(1,2,3),40,out var original,out _),"existing stable prepared durably");
   Check(original.StableId==stable.ToString()&&original.Profile==profile,"intent preserves original person");
   Check(RebirthNpcWorldIntegrationService.TryPrepareExistingPerson("original",stable,"medic",new Vector3(1,2,3),40,out var retry,out _)&&retry.CreatedTicks==original.CreatedTicks,"retry same immutable request no new id/time");
   Check(!RebirthNpcWorldIntegrationService.TryPrepareExistingPerson("original",stable,"medic",new Vector3(2,2,3),40,out _,out _),"changed replay payload refused");
   Check(!RebirthNpcPreparedPopulationDispatcher.TryReconcile("original",out _,out var reason)&&reason.Contains("not admitted"),"fresh native reconstruction remains blocked");
   Check(original.TryMarkAttempted(out var attempted),"attempt phase");Check(attempted.TryMarkConstructed(9,out var constructed),"constructed phase");Check(constructed.TryMarkPublishing(out var publishing),"publishing phase");
   n+=CompletionFixture.Run(publishing);
   RebirthNpcWorldIntegrationService.PendingSpawns[original.ReplayKey]=publishing;
   var npc=new EntityRebirthNPC{world=world,RebirthRuntimeState=new RebirthNpcRuntimeState{StableId=stable}};RebirthNpcRuntimeRegistry.Npc=npc;world.Entities[9]=npc;
   person.Profile.ProfileId="wrong";Check(!RebirthNpcPreparedPopulationDispatcher.TryReconcile("original",out _,out _),"saved profile mismatch refused before effect");person.Profile.ProfileId=profile;
   // Persist the pre-existing publishing phase as a native journal would before publication.
   var journal=new XmlDocument();journal.AppendChild(journal.CreateElement("rebirthNpcWorldIntegration"));journal.DocumentElement.SetAttribute("version","1");
   using(var writer=journal.DocumentElement.CreateNavigator().AppendChild()){RebirthNpcSpawnReplayCodec.Write(writer,RebirthNpcWorldIntegrationService.Replay);RebirthNpcPendingSpawnPersistence.Write(writer,RebirthNpcWorldIntegrationService.PendingSpawns);}
   journal.Save(Path.Combine(directory,"RebirthNpcWorldIntegration.xml"));
   person.HumanAppearance=new RebirthHumanNpcAppearanceDescriptor(RebirthHumanNpcModelPipeline.SDCS,73,"BaseMale");
   npc.RebirthRuntimeState.HasHumanAppearance=true;npc.RebirthRuntimeState.HumanAppearance=new RebirthHumanNpcAppearanceDescriptor(RebirthHumanNpcModelPipeline.SDCS,74,"BaseMale");
   Check(!RebirthNpcPreparedPopulationDispatcher.TryReconcile("original",out _,out _),"saved appearance mismatch refused before terminal write");
   npc.RebirthRuntimeState.HumanAppearance=person.HumanAppearance.Value;
   RebirthNpcPersistenceFile.Fail=true;Check(!RebirthNpcPreparedPopulationDispatcher.TryReconcile("original",out _,out _),"failed terminal publication refused");
   Check(RebirthNpcWorldIntegrationService.PendingSpawns.ContainsKey(original.ReplayKey)&&!RebirthNpcWorldIntegrationService.Replay.Contains(original.ReplayKey),"failure preserves pending authority");RebirthNpcPersistenceFile.Fail=false;

   var oldIdentities=RebirthNpcWorldIntegrationService.ByStable;var oldPending=RebirthNpcWorldIntegrationService.PendingSpawns;var oldReplay=RebirthNpcWorldIntegrationService.Replay;
   Check(RebirthNpcPreparedPopulationDispatcher.TryReconcile("original",out var returned,out _)&&returned==stable,"observed original published person completes");
   {Check(!ReferenceEquals(oldIdentities,RebirthNpcWorldIntegrationService.ByStable),"staged identities swapped after primary");Check(oldPending.ContainsKey(original.ReplayKey)&&!oldReplay.Contains(original.ReplayKey),"precommit containers remain immutable on success");Check(!ReferenceEquals(oldPending,RebirthNpcWorldIntegrationService.PendingSpawns)&&!ReferenceEquals(oldReplay,RebirthNpcWorldIntegrationService.Replay),"pending and terminal containers published together after disk witness");}
   var disk=new XmlDocument();disk.Load(Path.Combine(directory,"RebirthNpcWorldIntegration.xml"));
   Check(RebirthNpcWorldIdentityLoad.TryRead(disk.DocumentElement,out var identities,out var pending,out var completed)&&pending.Count==0&&completed.Contains(original.ReplayKey)&&identities[stable].DisplayName=="Original Medic","exact terminal disk witness preserves name and stable");
   Check(RebirthNpcWorldIntegrationService.TryCompletePublishedExistingPerson("original",world,npc,out _),"durable original-person completion retry qualified");
   Check(RebirthNpcPreparedPopulationDispatcher.TryReconcile("original",out var alreadyComplete,out _)&&alreadyComplete==stable,"dispatcher completed retry uses durable original binding");
   RebirthNpcWorldIntegrationService.ReloadFixture();
   Check(RebirthNpcWorldIntegrationService.Completions[original.ReplayKey].Generation==person.Presence.EmbodimentGeneration&&RebirthNpcPreparedPopulationDispatcher.TryReconcile("original",out var reloadStable,out _)&&reloadStable==stable,"actual owner reload preserves completion generation and original-person retry");
   string boundSnapshot=disk.OuterXml;disk.DocumentElement.SetAttribute("version","1");disk.DocumentElement.RemoveChild(disk.DocumentElement.SelectSingleNode("completedSpawns"));disk.Save(Path.Combine(directory,"RebirthNpcWorldIntegration.xml"));
   Check(!RebirthNpcWorldIntegrationService.TryCompletePublishedExistingPerson("original",world,npc,out _),"legacy unbound disk cannot prove original completion");RebirthNpcWorldIntegrationService.ReloadFixture();
   Check(RebirthNpcWorldIntegrationService.Completions.Count==0&&!RebirthNpcPreparedPopulationDispatcher.TryReconcile("original",out _,out _),"actual legacy owner reload retains unbound refusal");disk.LoadXml(boundSnapshot);disk.Save(Path.Combine(directory,"RebirthNpcWorldIntegration.xml"));RebirthNpcWorldIntegrationService.ReloadFixture();
   // Exercise the actual production uncertainty guard, not a proposed replacement.
   {
    RebirthNpcWorldIntegrationService.ResetFixture();RebirthNpcWorldIntegrationService.PendingSpawns[original.ReplayKey]=publishing;journal.Save(Path.Combine(directory,"RebirthNpcWorldIntegration.xml"));
    GameIO.HoldAfterComplete=true;
    Check(!RebirthNpcPreparedPopulationDispatcher.TryReconcile("original",out _,out _),"uncertain terminal witness refuses memory publication");GameIO.HoldAfterComplete=false;
    string committedBytes=File.ReadAllText(Path.Combine(directory,"RebirthNpcWorldIntegration.xml"));
    Check(RebirthNpcWorldIntegrationService.worldSnapshotPublicationUncertain&&RebirthNpcWorldIntegrationService.PendingSpawns.ContainsKey(original.ReplayKey),"uncertain final commit holds old authority explicitly");
    bool held=false;try{RebirthNpcWorldIntegrationService.ForceCheckpoint();}catch(IOException){held=true;}
    Check(held&&committedBytes==File.ReadAllText(Path.Combine(directory,"RebirthNpcWorldIntegration.xml")),"forced checkpoint cannot regress committed terminal disk witness");
    Check(!RebirthNpcWorldIntegrationService.TrySetDisplayName(stable,"Lost Edit",out _),"uncertain journal prevents rename loss before recovery");
    Check(!RebirthNpcWorldIntegrationService.TryPrepareExistingPerson("new-held",stable,"medic",new Vector3(1,2,3),40,out _,out _),"held journal admits no new pending intent before recovery");
    // Actual production recovery is now connected.
    {
     uint oldGeneration=person.Presence.EmbodimentGeneration;person.Presence.EmbodimentGeneration++;
     Check(!RebirthNpcPreparedPopulationDispatcher.TryReconcile("original",out _,out _),"recovery changed owner generation refused");person.Presence.EmbodimentGeneration=oldGeneration;
     Check(RebirthNpcPreparedPopulationDispatcher.TryReconcile("original",out var recoveredId,out _)&&recoveredId==stable,"qualified final-file recovery publishes original person");
     Check(!RebirthNpcWorldIntegrationService.worldSnapshotPublicationUncertain&&RebirthNpcWorldIntegrationService.Completions[original.ReplayKey].Stable==stable&&!RebirthNpcWorldIntegrationService.PendingSpawns.ContainsKey(original.ReplayKey),"recovery restores exact completion mapping and releases write hold");
     Check(committedBytes==File.ReadAllText(Path.Combine(directory,"RebirthNpcWorldIntegration.xml")),"recovery never rewrites disk or replays native effect");
    }

    RebirthNpcWorldIntegrationService.ResetFixture();
   }
   GameIO.Directory=directory+"-other";Check(!RebirthNpcWorldIntegrationService.TryCompletePublishedExistingPerson("original",world,npc,out _),"cross-save completion refused");GameIO.Directory=directory;
   npc.Dead=true;Check(!RebirthNpcWorldIntegrationService.TryCompletePublishedExistingPerson("original",world,npc,out _),"dead observed entity refused");
   Console.WriteLine("PASS "+n+" actual extracted existing-person preparation/terminal writer and linked admission/dispatcher checks; native APIs doubled, no native construction or ordinary population enabled.");
  }
  finally{Directory.Delete(directory,true);}
 }
}