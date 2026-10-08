using System; using System.Collections.Generic; using System.IO; using System.Text; using System.Xml; using System.Globalization; partial class RebirthNpcWorldIntegrationService {    internal static bool TryPrepareExistingPerson(string replayId,RebirthNpcStableId stable,string entityClass,
        Vector3 position,float yaw,out RebirthNpcPendingSpawn request,out string reason)
    {
        request=null;reason="Existing persistent humanoid person required.";
        if(!IsServer()||GameManager.Instance?.World==null||GameManager.Instance.World.IsRemote())return false;
        EnsureInitialized();EnsurePersistenceLoaded();
        if(!RebirthNpcAggregatePersistenceStore.TryGet(stable,out var person)||
            !RebirthNpcPreparedPersonAdmission.TryValidate(person,out var profile,out reason)||
            !RebirthNpcPendingSpawn.TryCreate("spawn:"+replayId,stable,profile,entityClass,
                position.x,position.y,position.z,yaw,DateTime.UtcNow.Ticks,out var proposed))return false;
        if(!TryValidateSpawnPlacement(proposed)){reason="Loaded safe placement and exact physical/profile binding required.";return false;}
        lock(Sync)
        {
            if(worldSnapshotPublicationUncertain||!IsPreparedContextCurrentNoLock())return false;
            if(Replay.Contains(proposed.ReplayKey)){reason="Request already completed; reconcile the original live embodiment.";return false;}
            if(PendingSpawns.TryGetValue(proposed.ReplayKey,out var previous))
            {
                if(previous.StableId!=proposed.StableId||previous.Profile!=proposed.Profile||previous.EntityClass!=proposed.EntityClass||
                    previous.X!=proposed.X||previous.Y!=proposed.Y||previous.Z!=proposed.Z||previous.Yaw!=proposed.Yaw)
                {reason="Replay key is already bound to a different original request.";return false;}
                request=previous;
                try{SaveIfDirty();return HasSavedPendingSpawn(previous);}catch{reason="Original intent requires durable save retry.";return false;}
            }
            foreach(var pending in PendingSpawns.Values)if(pending.StableId==proposed.StableId)
            {reason="Original person already has a different pending request.";return false;}
            if(PendingSpawns.Count>=1024||Replay.Count>=RebirthNpcSpawnReplayCodec.MaximumRecords)return false;
            PendingSpawns.Add(proposed.ReplayKey,proposed);dirty=true;request=proposed;
            try{SaveIfDirty();if(!HasSavedPendingSpawn(proposed))return false;reason=string.Empty;return true;}
            catch{reason="Original intent requires durable save retry.";return false;}
        }
    }
    internal static bool TryGetCompletedBinding(string replayId,out RebirthNpcSpawnCompletion binding)
    {
        binding=null;if(!RebirthNpcSpawnReplayCodec.ValidKey("spawn:"+replayId)||!IsServer())return false;
        EnsureInitialized();EnsurePersistenceLoaded();lock(Sync)return IsPreparedContextCurrentNoLock()&&Completions.TryGetValue("spawn:"+replayId,out binding);
    }    internal static bool TryGetPreparedRequest(string replayId,out RebirthNpcPendingSpawn request)
    {
        request=null;if(!RebirthNpcSpawnReplayCodec.ValidKey("spawn:"+replayId)||!IsServer())return false;
        EnsureInitialized();EnsurePersistenceLoaded();lock(Sync)return IsPreparedContextCurrentNoLock()&&PendingSpawns.TryGetValue("spawn:"+replayId,out request);
    }
    internal static bool TryConfirmConstructedSpawn(string replayId,Entity candidate,out RebirthNpcPendingSpawn request)
    {
        request=null;var world=GameManager.Instance?.World;
        if(!IsServer()||world==null||world.IsRemote()||!(candidate is EntityRebirthNPC npc)||
            !ReferenceEquals(candidate.world,world)||candidate.entityId<=0||npc.IsDead()||npc.IsMarkedForUnload())return false;
        EnsureInitialized();
        lock(Sync)
        {
            string key="spawn:"+replayId;
            if(worldSnapshotPublicationUncertain||!PendingSpawns.TryGetValue(key,out var stored)||!stored.IsConstructed||Replay.Contains(key)||
                stored.NativeEntityId!=candidate.entityId||candidate.entityClass!=EntityClass.FromString(stored.EntityClass)||
                !RebirthNpcStableId.TryParse(stored.StableId,out var stable)||
                !RebirthNpcRuntimeRegistry.TryGet(candidate.entityId,out var runtime)||runtime.StableId!=stable||
                !string.Equals(runtime.ProfileId,stored.Profile,StringComparison.OrdinalIgnoreCase)||
                !RebirthNpcRuntimeRegistry.TryGetEntityId(stable,out var canonical)||canonical!=candidate.entityId)return false;
            var present=world.GetEntity(candidate.entityId);
            if(present!=null&&!ReferenceEquals(present,candidate))return false;
            try
            {
                SaveIfDirty();
                if(!HasSavedPendingSpawn(stored))return false;
                request=stored;return true;
            }
            catch{return false;}
        }
    }
    internal static bool TryObservePublishedSpawn(string replayId,World originalWorld,Entity candidate,
        out RebirthNpcPendingSpawn request)
    {
        request=null;
        if(originalWorld==null||!ReferenceEquals(GameManager.Instance?.World,originalWorld)||
            candidate==null||!ReferenceEquals(candidate.world,originalWorld)||
            !ReferenceEquals(originalWorld.GetEntity(candidate.entityId),candidate))return false;
        if(!TryConfirmConstructedSpawn(replayId,candidate,out var confirmed)||
            !ReferenceEquals(GameManager.Instance?.World,originalWorld)||
            !ReferenceEquals(candidate.world,originalWorld)||
            !ReferenceEquals(originalWorld.GetEntity(candidate.entityId),candidate))return false;
        request=confirmed;return true;
    }
    // Reconcile only an exact observed live publication, then atomically acknowledge its original journal key.
    internal static bool TryCompletePublishedExistingPerson(string replayId,World originalWorld,Entity candidate,out string reason)
    {
        reason="Original published embodiment requires reconciliation.";
        if(!IsServer()||originalWorld==null||!ReferenceEquals(GameManager.Instance?.World,originalWorld)||
            !(candidate is EntityRebirthNPC npc)||!ReferenceEquals(candidate.world,originalWorld)||candidate.entityId<=0||
            !ReferenceEquals(originalWorld.GetEntity(candidate.entityId),candidate)||npc.IsDead()||npc.IsMarkedForUnload())return false;
        var runtime=npc.RebirthRuntimeState;
        if(runtime==null||!RebirthNpcRuntimeRegistry.TryGetEntityId(runtime.StableId,out var canonical)||canonical!=candidate.entityId||
            !RebirthNpcAggregatePersistenceStore.TryGet(runtime.StableId,out var person)||
            !RebirthNpcPreparedPersonAdmission.TryValidate(person,out var profile,out reason,true)||
            !string.Equals(profile,runtime.ProfileId,StringComparison.OrdinalIgnoreCase)||
            person.HumanAppearance.HasValue&&(!runtime.HasHumanAppearance||!person.HumanAppearance.Value.Equals(runtime.HumanAppearance)))return false;
        string key="spawn:"+replayId;if(!RebirthNpcSpawnReplayCodec.ValidKey(key))return false;
        EnsureInitialized();EnsurePersistenceLoaded();
        lock(Sync)
        {
            if(!IsPreparedContextCurrentNoLock())return false;
            if(worldSnapshotPublicationUncertain)
            {
                if(!EntityClass.list.TryGetValue(candidate.entityClass,out var recoveryClass)||
                    !RebirthNpcCompletionSnapshot.TryLoad(Path.Combine(loadedNativeDirectory,FileName),key,runtime.StableId,
                        profile,recoveryClass.entityClassName,candidate.entityId,person.Presence.EmbodimentGeneration,out var recovered)||
                    !IsPreparedContextCurrentNoLock()||!ReferenceEquals(originalWorld.GetEntity(candidate.entityId),candidate)||npc.IsDead()||npc.IsMarkedForUnload()||
                    !RebirthNpcRuntimeRegistry.TryGetEntityId(runtime.StableId,out var recoveryCanonical)||recoveryCanonical!=candidate.entityId||
                    !RebirthNpcRuntimeRegistry.TryGet(candidate.entityId,out var recoveryRuntime)||!ReferenceEquals(recoveryRuntime,runtime)||
                    !RebirthNpcAggregatePersistenceStore.TryGet(runtime.StableId,out var currentPerson)||
                    !RebirthNpcPreparedPersonAdmission.TryValidate(currentPerson,out var recoveryProfile,out reason,true)||recoveryProfile!=profile||
                    currentPerson.Presence.EmbodimentGeneration!=person.Presence.EmbodimentGeneration||
                    currentPerson.HumanAppearance.HasValue&&(!runtime.HasHumanAppearance||!currentPerson.HumanAppearance.Value.Equals(runtime.HumanAppearance)))return false;
                var recoveredAmbient=new Dictionary<int,RebirthNpcWorldIdentityRecord>();
                foreach(var row in recovered.Identities.Values)if(row.AmbientEntityId>0)recoveredAmbient.Add(row.AmbientEntityId,row);
                ByStable=recovered.Identities;ByAmbient=recoveredAmbient;PendingSpawns=recovered.Pending;Replay=recovered.Replays;Completions=recovered.Completions;
                dirty=false;worldSnapshotPublicationUncertain=false;reason=string.Empty;return true;
            }
            if(Replay.Contains(key))
            {
                if(!Completions.TryGetValue(key,out var binding)||
                    !EntityClass.list.TryGetValue(candidate.entityClass,out var definition)||
                    !binding.Matches(runtime.StableId,profile,definition.entityClassName,candidate.entityId,person.Presence.EmbodimentGeneration)||
                    !HasSavedCompletedSpawn(key,runtime.StableId,candidate.entityId,profile,binding.EntityClass,binding.Generation))
                {reason="Completed key has no qualified original-person binding; automatic retry refused.";return false;}
                reason=string.Empty;return true;
            }
            if(!TryObservePublishedSpawn(replayId,originalWorld,candidate,out var request)||
                !request.IsPublishing||request.StableId!=runtime.StableId.ToString()||request.Profile!=profile)return false;
            if(ByAmbient.TryGetValue(candidate.entityId,out var collision)&&collision.StableId!=runtime.StableId)return false;
            var identities=new Dictionary<RebirthNpcStableId,RebirthNpcWorldIdentityRecord>(ByStable);
            var pending=new Dictionary<string,RebirthNpcPendingSpawn>(PendingSpawns,StringComparer.Ordinal);
            var completed=new HashSet<string>(Replay,StringComparer.Ordinal);
            var bindings=new Dictionary<string,RebirthNpcSpawnCompletion>(Completions,StringComparer.Ordinal);
            if(!RebirthNpcSpawnCompletion.TryCreate(request,person.Presence.EmbodimentGeneration,DateTime.UtcNow.Ticks,out var provenance))return false;
            bindings.Add(key,provenance);
            if(completed.Count>=RebirthNpcSpawnReplayCodec.MaximumRecords)return false;
            string display=person.Identity.GeneratedOrAssignedDisplayName;
            if(string.IsNullOrWhiteSpace(display))display=GenerateName(runtime.StableId,RebirthNpcProfileRegistry.ResolveRequired(profile).Category);
            var identity=new RebirthNpcWorldIdentityRecord{StableId=runtime.StableId,AmbientEntityId=candidate.entityId,
                ProfileId=profile,DisplayName=display,PromotedUtcTicks=DateTime.UtcNow.Ticks,Persistent=true};
            identities[runtime.StableId]=identity;pending.Remove(key);completed.Add(key);
            var ambient=new Dictionary<int,RebirthNpcWorldIdentityRecord>();
            foreach(var row in identities.Values)if(row.AmbientEntityId>0)
            {if(ambient.ContainsKey(row.AmbientEntityId))return false;ambient.Add(row.AmbientEntityId,row);}
            try
            {
                WriteSnapshotNoLock(identities,pending,completed,bindings);
                if(!HasSavedCompletedSpawn(key,runtime.StableId,candidate.entityId,profile,request.EntityClass,person.Presence.EmbodimentGeneration)||!IsPreparedContextCurrentNoLock()||
                    !ReferenceEquals(originalWorld.GetEntity(candidate.entityId),candidate)){worldSnapshotPublicationUncertain=true;return false;}
                ByStable=identities;ByAmbient=ambient;PendingSpawns=pending;Replay=completed;Completions=bindings;dirty=false;spawns++;
                reason=string.Empty;return true;
            }
            catch(Exception ex){worldSnapshotPublicationUncertain=!HasSavedPendingSpawn(request);reason="Terminal journal requires save reconciliation: "+ex.GetType().Name;return false;}
        }
    }
    private static bool HasSavedCompletedSpawn(string key,RebirthNpcStableId stable,int nativeId,string profile,string entityClass,uint generation)
    {
        return IsPreparedContextCurrentNoLock()&&RebirthNpcCompletionSnapshot.TryLoad(Path.Combine(loadedNativeDirectory,FileName),
            key,stable,profile,entityClass,nativeId,generation,out _);
    }    internal static bool HasSavedPendingSpawn(RebirthNpcPendingSpawn request)
    {
        if(request==null||!IsServer())return false;
        lock(Sync)
        {
            try
            {
                string directory=GameIO.GetSaveGameDir();if(string.IsNullOrEmpty(directory))return false;
                string path=Path.Combine(directory,FileName);
                if(!File.Exists(path)||new FileInfo(path).Length>64L*1024L*1024L)return false;
                var settings=new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=64L*1024L*1024L};
                var document=new XmlDocument{XmlResolver=null};
                using(var reader=XmlReader.Create(path,settings))document.Load(reader);
                if(!RebirthNpcWorldIdentityLoad.TryRead(document.DocumentElement,out var identities,out var saved,out var completed)||
                    completed.Contains(request.ReplayKey)||!saved.TryGetValue(request.ReplayKey,out var stored))return false;
                return System.Xml.Linq.XNode.DeepEquals(stored.Write(),request.Write());
            }
            catch{return false;}
        }
    }
    private static void LoadNoLock()
    {
        if (loaded || GameManager.Instance?.World == null) return;
        string dir = GameIO.GetSaveGameDir(); if (string.IsNullOrEmpty(dir)) return;
        string path = Path.Combine(dir, FileName);
        XmlDocument d; string source, error;
        if (!RebirthNpcPersistenceFile.TryLoad(path, doc => doc.DocumentElement != null &&
            doc.DocumentElement.Name == "rebirthNpcWorldIntegration" &&
            (doc.DocumentElement.GetAttribute("version")=="1"||doc.DocumentElement.GetAttribute("version") == SchemaVersion.ToString(CultureInfo.InvariantCulture)),
            out d, out source, out error))
        { RebirthNpcPersistenceFile.AssertWritable(path); loaded = true; loadedNativeWorld=GameManager.Instance.World;loadedNativeDirectory=dir;worldSnapshotPublicationUncertain=false; return; }
        try
        {
            if(!RebirthNpcWorldIdentityLoad.TryRead(d.DocumentElement,out var identities,out var pending,out var replays,out var completions))
                throw new InvalidDataException("NPC world identity snapshot invalid.");
            // Validate the complete snapshot before publishing any of its records.
            ByStable.Clear();ByAmbient.Clear();PendingSpawns.Clear();Replay.Clear();Completions.Clear();
            foreach(var pair in identities){ByStable.Add(pair.Key,pair.Value);if(pair.Value.AmbientEntityId>0)ByAmbient.Add(pair.Value.AmbientEntityId,pair.Value);}
            foreach(var pair in pending)PendingSpawns.Add(pair.Key,pair.Value);
            foreach(var replay in replays)Replay.Add(replay);
            foreach(var completion in completions)Completions.Add(completion.Key,completion.Value);
            loaded=true;loadedNativeWorld=GameManager.Instance.World;loadedNativeDirectory=dir;worldSnapshotPublicationUncertain=false;
        }
        catch (Exception ex) { RebirthNpcPersistenceFile.BlockWrite(path, ex.Message); throw; }
    }
    private static void SaveNoLock()
    {
        if(worldSnapshotPublicationUncertain||!IsPreparedContextCurrentNoLock())throw new IOException("NPC world snapshot context changed; write refused.");
        WriteSnapshotNoLock(ByStable,PendingSpawns,Replay,Completions);
    }
    private static void WriteSnapshotNoLock(Dictionary<RebirthNpcStableId,RebirthNpcWorldIdentityRecord> identities,
        Dictionary<string,RebirthNpcPendingSpawn> pending,HashSet<string> replays,
        Dictionary<string,RebirthNpcSpawnCompletion> completions)
    {
        string directory=GameIO.GetSaveGameDir();
        if(string.IsNullOrEmpty(directory))throw new IOException("NPC world identity save directory unavailable.");
        Directory.CreateDirectory(directory);
        string path=Path.Combine(directory,FileName),temporary=path+".tmp",backup=path+".bak";
        RebirthNpcPersistenceFile.AssertWritable(path);
        var settings=new XmlWriterSettings{Indent=true,Encoding=new UTF8Encoding(false),CloseOutput=false};
        using(var stream=new FileStream(temporary,FileMode.Create,FileAccess.Write,FileShare.None))
        {
            using(var writer=XmlWriter.Create(stream,settings))
            {
                writer.WriteStartElement("rebirthNpcWorldIntegration");
                writer.WriteAttributeString("version",SchemaVersion.ToString(CultureInfo.InvariantCulture));
                foreach(var record in identities.Values)
                {
                    writer.WriteStartElement("identity");
                    writer.WriteAttributeString("stableId",record.StableId.ToString());
                    writer.WriteAttributeString("ambientEntityId",record.AmbientEntityId.ToString(CultureInfo.InvariantCulture));
                    writer.WriteAttributeString("profileId",record.ProfileId);
                    writer.WriteAttributeString("displayName",record.DisplayName);
                    writer.WriteAttributeString("promotedUtcTicks",record.PromotedUtcTicks.ToString(CultureInfo.InvariantCulture));
                    writer.WriteEndElement();
                }
                RebirthNpcSpawnReplayCodec.Write(writer,replays);
                RebirthNpcPendingSpawnPersistence.Write(writer,pending);
                RebirthNpcSpawnCompletion.WriteSection(writer,completions);
                writer.WriteEndElement();writer.Flush();
            }
            stream.Flush(true);
        }
        if(new FileInfo(temporary).Length>64L*1024L*1024L)throw new InvalidDataException("NPC world snapshot exceeds supported bound.");
        var verification=new XmlDocument{XmlResolver=null};verification.Load(temporary);
        if(!RebirthNpcWorldIdentityLoad.TryRead(verification.DocumentElement,out _,out _,out _))throw new InvalidDataException("NPC world identity temporary snapshot invalid.");
        // Never delete or copy over the current final file before the replacement succeeds.
        if(File.Exists(path))File.Replace(temporary,path,backup,true);else File.Move(temporary,path);
    }
    public static bool TrySetDisplayName(RebirthNpcStableId id,string displayName,out string reason)
    {
        EnsureInitialized(); reason=string.Empty;
        if(!IsServer()){reason="Server authority required.";return false;}
        string clean=(displayName??string.Empty).Trim();
        if(clean.Length<1||clean.Length>32){reason="Name must be between 1 and 32 characters.";return false;}
        for(int i=0;i<clean.Length;i++) if(char.IsControl(clean[i])){reason="Name contains invalid characters.";return false;}
        lock(Sync)
        {
            if(worldSnapshotPublicationUncertain){reason="World journal uncertainty requires original-person reconciliation.";return false;}
            RebirthNpcWorldIdentityRecord r;
            if(ByStable.TryGetValue(id,out r)){r.DisplayName=clean;dirty=true;}
            else
            {
                RebirthNpcRuntimeState runtime; int entityId;
                if(!RebirthNpcRuntimeRegistry.TryGetEntityId(id,out entityId)||!RebirthNpcRuntimeRegistry.TryGet(entityId,out runtime)){reason="NPC identity is not active.";return false;}
                r=new RebirthNpcWorldIdentityRecord{AmbientEntityId=entityId,StableId=id,ProfileId=runtime.ProfileId,DisplayName=clean,PromotedUtcTicks=DateTime.UtcNow.Ticks,Persistent=true};
                ByStable[id]=r;ByAmbient[entityId]=r;dirty=true;
            }
        }
        SaveIfDirty(); reason="Name updated."; return true;
    }
}