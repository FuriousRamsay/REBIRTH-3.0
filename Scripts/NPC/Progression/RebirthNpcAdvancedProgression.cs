using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Xml;

#nullable disable

public sealed class RebirthNpcCertificationDefinition
{
    public string Id; public RebirthNpcProfession Profession; public int MinimumLevel; public string[] Prerequisites; public int MinimumCompletedOutcomes;
    public RebirthNpcCertificationDefinition(string id,RebirthNpcProfession profession,int minimumLevel,string[] prerequisites=null,int minimumCompletedOutcomes=0)
    { Id=id??string.Empty; Profession=profession; MinimumLevel=minimumLevel; Prerequisites=prerequisites??new string[0]; MinimumCompletedOutcomes=minimumCompletedOutcomes; }
}
public sealed class RebirthNpcSpecializationDefinition
{
    public string Id; public RebirthNpcProfession Profession; public int MinimumLevel; public string RequiredCertification; public float SpeedMultiplier=1f; public float YieldMultiplier=1f; public float QualityMultiplier=1f; public float ResourceEfficiencyMultiplier=1f; public float SuccessMultiplier=1f; public float TreatmentMultiplier=1f;
}
public sealed class RebirthNpcMentorshipSession
{
    public Guid SessionId; public RebirthNpcStableId MentorId; public RebirthNpcStableId TraineeId; public RebirthNpcProfession Profession; public long StartedUtcTicks; public long ExpiresUtcTicks; public long LastValidatedUtcTicks; public float MaximumRange; public float CurrentRange; public bool MentorPresent; public bool TraineePresent; public bool Active; public long Revision; public int CreditedOutcomes; public readonly HashSet<Guid> CreditedOutcomeIds=new HashSet<Guid>();
}
public sealed class RebirthNpcAdvancedProgressionRecord
{
    public RebirthNpcStableId NpcId; public long Revision; public readonly HashSet<string> Certifications=new HashSet<string>(StringComparer.OrdinalIgnoreCase); public readonly Dictionary<RebirthNpcProfession,string> Specializations=new Dictionary<RebirthNpcProfession,string>(); public readonly Dictionary<RebirthNpcProfession,int> CompletedOutcomes=new Dictionary<RebirthNpcProfession,int>();
}
public sealed class RebirthNpcCertificationGraphResult
{
    public bool Valid; public string[] Errors; public string ToReport(){return "valid="+Valid+" errors="+string.Join(" | ",Errors??new string[0]);}
}

public static class RebirthNpcAdvancedProgressionService
{
    private static readonly object Sync=new object();
    private static readonly Dictionary<string,RebirthNpcCertificationDefinition> Certifications=new Dictionary<string,RebirthNpcCertificationDefinition>(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string,RebirthNpcSpecializationDefinition> Specializations=new Dictionary<string,RebirthNpcSpecializationDefinition>(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<RebirthNpcStableId,RebirthNpcAdvancedProgressionRecord> Records=new Dictionary<RebirthNpcStableId,RebirthNpcAdvancedProgressionRecord>();
    private static readonly Dictionary<Guid,RebirthNpcMentorshipSession> Mentorships=new Dictionary<Guid,RebirthNpcMentorshipSession>();
    private static bool initialized; private static long certificationUnlocks,mentorshipCredits,mentorshipTerminations,specializationResolutions,rejections;

    public static void EnsureInitialized()
    {
        lock(Sync)
        {
            if(initialized)return;
            RegisterDefaultsNoLock();
            RebirthNpcCertificationGraphResult graph=ValidateCertificationGraphNoLock();
            if(!graph.Valid)throw new InvalidOperationException("Invalid certification graph: "+string.Join("; ",graph.Errors));
            initialized=true;
        }
        RebirthNpcAdvancedProgressionPersistenceStore.EnsureLoaded();
    }
    private static void RegisterDefaultsNoLock()
    {
        foreach(RebirthNpcProfession p in Enum.GetValues(typeof(RebirthNpcProfession)))
        {
            string root="cert."+RebirthNpcProfessionDefinitions.TrackId(p).Substring(11)+".journeyman";
            string master="cert."+RebirthNpcProfessionDefinitions.TrackId(p).Substring(11)+".master";
            Certifications[root]=new RebirthNpcCertificationDefinition(root,p,5,null,5);
            Certifications[master]=new RebirthNpcCertificationDefinition(master,p,12,new[]{root},25);
            Specializations["spec."+RebirthNpcProfessionDefinitions.TrackId(p).Substring(11)+".efficiency"]=new RebirthNpcSpecializationDefinition{Id="spec."+RebirthNpcProfessionDefinitions.TrackId(p).Substring(11)+".efficiency",Profession=p,MinimumLevel=10,RequiredCertification=root,SpeedMultiplier=1.08f,ResourceEfficiencyMultiplier=1.10f};
            Specializations["spec."+RebirthNpcProfessionDefinitions.TrackId(p).Substring(11)+".quality"]=new RebirthNpcSpecializationDefinition{Id="spec."+RebirthNpcProfessionDefinitions.TrackId(p).Substring(11)+".quality",Profession=p,MinimumLevel=10,RequiredCertification=root,YieldMultiplier=1.08f,QualityMultiplier=1.10f,SuccessMultiplier=1.05f,TreatmentMultiplier=1.08f};
        }
    }
    public static RebirthNpcCertificationGraphResult ValidateCertificationGraph()
    {EnsureInitialized();lock(Sync)return ValidateCertificationGraphNoLock();}
    private static RebirthNpcCertificationGraphResult ValidateCertificationGraphNoLock()
    {
        var errors=new List<string>();
        foreach(var d in Certifications.Values)
        {
            if(string.IsNullOrWhiteSpace(d.Id))errors.Add("empty-id");
            if(d.MinimumLevel<1||d.MinimumLevel>20)errors.Add(d.Id+":invalid-level");
            foreach(string p in d.Prerequisites)
            {RebirthNpcCertificationDefinition x;if(!Certifications.TryGetValue(p,out x))errors.Add(d.Id+":missing-prerequisite:"+p);else if(x.Profession!=d.Profession)errors.Add(d.Id+":cross-profession-prerequisite:"+p);}
        }
        var visiting=new HashSet<string>(StringComparer.OrdinalIgnoreCase);var visited=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(string id in Certifications.Keys)Visit(id,visiting,visited,errors);
        return new RebirthNpcCertificationGraphResult{Valid=errors.Count==0,Errors=errors.ToArray()};
    }
    private static void Visit(string id,HashSet<string> visiting,HashSet<string> visited,List<string> errors)
    {
        if(visited.Contains(id))return;if(!visiting.Add(id)){errors.Add(id+":cycle");return;}
        RebirthNpcCertificationDefinition d;if(Certifications.TryGetValue(id,out d))foreach(string p in d.Prerequisites)Visit(p,visiting,visited,errors);
        visiting.Remove(id);visited.Add(id);
    }
    private static RebirthNpcAdvancedProgressionRecord GetRecordNoLock(RebirthNpcStableId id)
    {RebirthNpcAdvancedProgressionRecord r;if(!Records.TryGetValue(id,out r)){r=new RebirthNpcAdvancedProgressionRecord{NpcId=id,Revision=1};Records[id]=r;}return r;}

    public static float PrepareMentorshipMultiplier(Guid outcomeId,RebirthNpcStableId trainee,RebirthNpcProfession profession,long nowTicks)
    {
        EnsureInitialized();lock(Sync)
        {
            float result=1f;
            foreach(var s in Mentorships.Values)
            {
                if(!s.Active||s.TraineeId!=trainee||s.Profession!=profession)continue;
                string reason;if(!ValidateSessionNoLock(s,nowTicks,out reason)){TerminateNoLock(s,reason);continue;}
                if(s.CreditedOutcomeIds.Contains(outcomeId))continue;
                result=Math.Max(result,1.15f);
            }
            return result;
        }
    }
    public static void CommitMentorshipCredit(Guid outcomeId,RebirthNpcStableId trainee,RebirthNpcProfession profession,long nowTicks)
    {
        EnsureInitialized();var changed=new List<RebirthNpcMentorshipSession>();
        lock(Sync)
        {
            foreach(var s in Mentorships.Values)
            {
                if(!s.Active||s.TraineeId!=trainee||s.Profession!=profession)continue;
                string reason;if(!ValidateSessionNoLock(s,nowTicks,out reason)){TerminateNoLock(s,reason);changed.Add(Clone(s));continue;}
                if(s.CreditedOutcomeIds.Add(outcomeId)){s.CreditedOutcomes++;s.Revision++;mentorshipCredits++;TrimReplay(s.CreditedOutcomeIds);RebirthNpcAdvancedProgressionPersistenceStore.MarkDirty();changed.Add(Clone(s));}
            }
        }
        foreach(var s in changed)PublishSession(s);
    }
    private static void TrimReplay(HashSet<Guid> ids){if(ids.Count<=1024)return;foreach(Guid id in ids.Take(ids.Count-1024).ToArray())ids.Remove(id);}
    private static bool ValidateSessionNoLock(RebirthNpcMentorshipSession s,long now,out string reason)
    {
        if(now<=0)now=DateTime.UtcNow.Ticks;
        if(!s.Active){reason="inactive";return false;} if(s.MentorId.IsEmpty||s.TraineeId.IsEmpty||s.MentorId==s.TraineeId){reason="invalid-participants";return false;}
        if(now>=s.ExpiresUtcTicks){reason="expired";return false;} if(!s.MentorPresent||!s.TraineePresent){reason="participant-not-present";return false;} if(s.CurrentRange<0||s.CurrentRange>s.MaximumRange){reason="out-of-range";return false;}
        RebirthNpcProgressionView mentor;if(!RebirthNpcProgressionService.TryGetView(s.MentorId,RebirthNpcProfessionDefinitions.TrackId(s.Profession),out mentor)||mentor.Level<5){reason="mentor-ineligible";return false;}
        RebirthNpcProgressionView trainee;if(RebirthNpcProgressionService.TryGetView(s.TraineeId,RebirthNpcProfessionDefinitions.TrackId(s.Profession),out trainee)&&mentor.Level<=trainee.Level){reason="mentor-not-higher-level";return false;}
        s.LastValidatedUtcTicks=now;reason=string.Empty;return true;
    }
    private static void TerminateNoLock(RebirthNpcMentorshipSession s,string reason){if(!s.Active)return;s.Active=false;s.Revision++;mentorshipTerminations++;RebirthNpcAdvancedProgressionPersistenceStore.MarkDirty();}
    public static bool StartMentorship(Guid sessionId,RebirthNpcStableId mentor,RebirthNpcStableId trainee,RebirthNpcProfession profession,long durationTicks,float maximumRange,out string reason)
    {
        EnsureInitialized();RebirthNpcMentorshipSession published;
        lock(Sync)
        {
            if(sessionId==Guid.Empty||Mentorships.ContainsKey(sessionId)){reason="invalid-or-duplicate-session";rejections++;return false;}
            long now=DateTime.UtcNow.Ticks;var s=new RebirthNpcMentorshipSession{SessionId=sessionId,MentorId=mentor,TraineeId=trainee,Profession=profession,StartedUtcTicks=now,ExpiresUtcTicks=now+Math.Max(TimeSpan.TicksPerMinute,durationTicks),LastValidatedUtcTicks=now,MaximumRange=Math.Max(1f,maximumRange),CurrentRange=0f,MentorPresent=true,TraineePresent=true,Active=true,Revision=1};
            if(!ValidateSessionNoLock(s,now,out reason)){rejections++;return false;}Mentorships[sessionId]=s;RebirthNpcAdvancedProgressionPersistenceStore.MarkDirty();published=Clone(s);
        }
        PublishSession(published);return true;
    }
    public static bool UpdateMentorshipPresence(Guid sessionId,bool mentorPresent,bool traineePresent,float range,long nowTicks)
    {
        EnsureInitialized();RebirthNpcMentorshipSession published;bool active;
        lock(Sync){RebirthNpcMentorshipSession s;if(!Mentorships.TryGetValue(sessionId,out s))return false;s.MentorPresent=mentorPresent;s.TraineePresent=traineePresent;s.CurrentRange=range;string reason;if(!ValidateSessionNoLock(s,nowTicks,out reason))TerminateNoLock(s,reason);else{s.Revision++;RebirthNpcAdvancedProgressionPersistenceStore.MarkDirty();}active=s.Active;published=Clone(s);}
        PublishSession(published);return active;
    }

    public static void OnProfessionAwardAccepted(RebirthNpcStableId npc,RebirthNpcProfession profession,Guid outcomeId)
    {
        EnsureInitialized();lock(Sync)
        {
            var r=GetRecordNoLock(npc);int count;r.CompletedOutcomes.TryGetValue(profession,out count);r.CompletedOutcomes[profession]=count+1;r.Revision++;
            EvaluateCertificationsNoLock(r,profession);ResolveSpecializationNoLock(r,profession);RebirthNpcAdvancedProgressionPersistenceStore.MarkDirty();
        }
        PublishProjection(npc);
    }
    private static void EvaluateCertificationsNoLock(RebirthNpcAdvancedProgressionRecord r,RebirthNpcProfession profession)
    {
        RebirthNpcProgressionView v;if(!RebirthNpcProgressionService.TryGetView(r.NpcId,RebirthNpcProfessionDefinitions.TrackId(profession),out v))return;bool changed;
        do{changed=false;foreach(var d in Certifications.Values.Where(x=>x.Profession==profession).OrderBy(x=>x.MinimumLevel).ThenBy(x=>x.Id,StringComparer.OrdinalIgnoreCase)){if(r.Certifications.Contains(d.Id)||v.Level<d.MinimumLevel)continue;int count;r.CompletedOutcomes.TryGetValue(profession,out count);if(count<d.MinimumCompletedOutcomes)continue;if(d.Prerequisites.Any(x=>!r.Certifications.Contains(x)))continue;r.Certifications.Add(d.Id);r.Revision++;certificationUnlocks++;changed=true;}}while(changed);
    }
    private static void ResolveSpecializationNoLock(RebirthNpcAdvancedProgressionRecord r,RebirthNpcProfession profession)
    {
        RebirthNpcProgressionView v;if(!RebirthNpcProgressionService.TryGetView(r.NpcId,RebirthNpcProfessionDefinitions.TrackId(profession),out v))return;
        var eligible=Specializations.Values.Where(x=>x.Profession==profession&&v.Level>=x.MinimumLevel&&(string.IsNullOrEmpty(x.RequiredCertification)||r.Certifications.Contains(x.RequiredCertification))).OrderByDescending(x=>x.MinimumLevel).ThenBy(x=>x.Id,StringComparer.OrdinalIgnoreCase).ToArray();
        string resolved=eligible.Length==0?string.Empty:eligible[(int)(StableHash(r.NpcId.ToString()+"|"+profession)%eligible.Length)].Id;string old;if(!r.Specializations.TryGetValue(profession,out old)||!string.Equals(old,resolved,StringComparison.OrdinalIgnoreCase)){if(string.IsNullOrEmpty(resolved))r.Specializations.Remove(profession);else r.Specializations[profession]=resolved;r.Revision++;specializationResolutions++;}
    }
    private static uint StableHash(string s){unchecked{uint h=2166136261;foreach(char c in s){h^=c;h*=16777619;}return h;}}
    public static RebirthNpcSpecializationDefinition GetSpecialization(RebirthNpcStableId npc,RebirthNpcProfession profession)
    {EnsureInitialized();lock(Sync){RebirthNpcAdvancedProgressionRecord r;string id;RebirthNpcSpecializationDefinition d;if(Records.TryGetValue(npc,out r)&&r.Specializations.TryGetValue(profession,out id)&&Specializations.TryGetValue(id,out d))return d;return null;}}
    public static bool HasCertification(RebirthNpcStableId npc,string id){EnsureInitialized();lock(Sync){RebirthNpcAdvancedProgressionRecord r;return Records.TryGetValue(npc,out r)&&r.Certifications.Contains(id);}}
    public static float CapabilityMultiplier(RebirthNpcStableId npc,RebirthNpcProfession profession)
    {var d=GetSpecialization(npc,profession);return d==null?1f:Math.Max(d.SpeedMultiplier,Math.Max(d.QualityMultiplier,d.SuccessMultiplier));}
    public static bool IsWorkEligible(RebirthNpcStableId npc,RebirthNpcProfession profession,int requiredLevel,string requiredCertification)
    {RebirthNpcProgressionView v;if(!RebirthNpcProgressionService.TryGetView(npc,RebirthNpcProfessionDefinitions.TrackId(profession),out v)||v.Level<requiredLevel)return false;return string.IsNullOrEmpty(requiredCertification)||HasCertification(npc,requiredCertification);}
    private static void PublishProjection(RebirthNpcStableId id)
    {
        if(id.IsEmpty)return;
        RebirthNpcProgressionProjectionRevisionService.Bump(id);
        RebirthNpcProgressionReplicationService.PublishDelta(id);
    }
    private static void PublishSession(RebirthNpcMentorshipSession s)
    {
        if(s==null)return;PublishProjection(s.MentorId);if(s.TraineeId!=s.MentorId)PublishProjection(s.TraineeId);
    }
    public static string GetReport(RebirthNpcStableId? npc=null)
    {
        EnsureInitialized();lock(Sync){var b=new StringBuilder();b.Append("[REBIRTH NPC Advanced Progression] definitions=").Append(Certifications.Count).Append(" specializations=").Append(Specializations.Count).Append(" records=").Append(Records.Count).Append(" mentorships=").Append(Mentorships.Count).Append(" unlocks=").Append(certificationUnlocks).Append(" mentorshipCredits=").Append(mentorshipCredits).Append(" terminations=").Append(mentorshipTerminations).Append(" resolutions=").Append(specializationResolutions).Append(" rejected=").Append(rejections);if(npc.HasValue){RebirthNpcAdvancedProgressionRecord r;if(Records.TryGetValue(npc.Value,out r))b.Append("\n  npc=").Append(npc.Value).Append(" certs=").Append(string.Join(",",r.Certifications)).Append(" specs=").Append(string.Join(",",r.Specializations.Select(x=>x.Key+":"+x.Value)));}return b.ToString();}
    }
    public static RebirthNpcAdvancedProgressionRecord[] ExportRecords(){EnsureInitialized();lock(Sync)return Records.Values.Select(Clone).ToArray();}
    public static bool TryExportRecord(RebirthNpcStableId id,out RebirthNpcAdvancedProgressionRecord record){EnsureInitialized();lock(Sync){RebirthNpcAdvancedProgressionRecord r;if(Records.TryGetValue(id,out r)){record=Clone(r);return true;}record=null;return false;}}
    public static RebirthNpcMentorshipSession[] ExportMentorships(){EnsureInitialized();lock(Sync)return Mentorships.Values.Select(Clone).ToArray();}
    public static RebirthNpcMentorshipSession[] ExportMentorshipsFor(RebirthNpcStableId id){EnsureInitialized();lock(Sync)return Mentorships.Values.Where(x=>x.MentorId.Equals(id)||x.TraineeId.Equals(id)).Select(Clone).ToArray();}
    public static void Import(IEnumerable<RebirthNpcAdvancedProgressionRecord> records,IEnumerable<RebirthNpcMentorshipSession> sessions)
    {lock(Sync){Records.Clear();Mentorships.Clear();foreach(var r in records??new RebirthNpcAdvancedProgressionRecord[0])if(!r.NpcId.IsEmpty)Records[r.NpcId]=Clone(r);long now=DateTime.UtcNow.Ticks;foreach(var s in sessions??new RebirthNpcMentorshipSession[0]){var c=Clone(s);string reason;if(ValidateSessionNoLock(c,now,out reason))Mentorships[c.SessionId]=c;else c.Active=false;}}}
    private static RebirthNpcAdvancedProgressionRecord Clone(RebirthNpcAdvancedProgressionRecord x){var r=new RebirthNpcAdvancedProgressionRecord{NpcId=x.NpcId,Revision=x.Revision};foreach(var c in x.Certifications)r.Certifications.Add(c);foreach(var p in x.Specializations)r.Specializations[p.Key]=p.Value;foreach(var p in x.CompletedOutcomes)r.CompletedOutcomes[p.Key]=p.Value;return r;}
    private static RebirthNpcMentorshipSession Clone(RebirthNpcMentorshipSession x){var r=new RebirthNpcMentorshipSession{SessionId=x.SessionId,MentorId=x.MentorId,TraineeId=x.TraineeId,Profession=x.Profession,StartedUtcTicks=x.StartedUtcTicks,ExpiresUtcTicks=x.ExpiresUtcTicks,LastValidatedUtcTicks=x.LastValidatedUtcTicks,MaximumRange=x.MaximumRange,CurrentRange=x.CurrentRange,MentorPresent=x.MentorPresent,TraineePresent=x.TraineePresent,Active=x.Active,Revision=x.Revision,CreditedOutcomes=x.CreditedOutcomes};foreach(Guid id in x.CreditedOutcomeIds)r.CreditedOutcomeIds.Add(id);return r;}
    public static bool RemoveRecordForRepair(RebirthNpcStableId id){lock(Sync){bool removed=Records.Remove(id);foreach(Guid key in Mentorships.Where(x=>x.Value.MentorId.Equals(id)||x.Value.TraineeId.Equals(id)).Select(x=>x.Key).ToArray())Mentorships.Remove(key);return removed;}}
    public static void ResetForWorldChange(){lock(Sync){Records.Clear();Mentorships.Clear();}RebirthNpcAdvancedProgressionPersistenceStore.Reset(false);}
}

public static class RebirthNpcAdvancedProgressionPersistenceStore
{
    private static readonly object Sync=new object();private static bool loaded,dirty;private static long saves,loads,repairs;
    public static void EnsureLoaded()
    {
        if (!IsServer() || GameManager.Instance?.World == null) return;
        lock (Sync)
        {
            if (loaded) return;
            string p = PathName();
            if (string.IsNullOrEmpty(p)) return;
            if (!File.Exists(p) && !File.Exists(p + ".bak"))
            { RebirthNpcPersistenceFile.AssertWritable(p); loaded = true; return; }
            try
            {
                string source = File.Exists(p) ? p : p + ".bak";
                try { Read(source); }
                catch
                {
                    if (source.EndsWith(".bak", StringComparison.OrdinalIgnoreCase) || !File.Exists(p + ".bak")) throw;
                    source = p + ".bak";
                    Read(source);
                }
                if (source == p) loads++; else repairs++;
                RebirthNpcPersistenceFile.VerifiedRead(p);
                loaded = true;
            }
            catch (Exception ex)
            {
                RebirthNpcPersistenceFile.BlockWrite(p, ex.GetType().Name + ": " + ex.Message);
                throw;
            }
        }
    }
    public static void MarkDirty(){if(IsServer())dirty=true;}
    public static void Save(){if(!IsServer())return;EnsureLoaded();lock(Sync){if(!dirty && !RebirthNpcPersistenceCoordinator.IsCheckpointWrite)return;string p=PathName();RebirthNpcPersistenceFile.RequireCheckpointLoaded(loaded,p);RebirthNpcPersistenceFile.AssertWritable(p);Directory.CreateDirectory(Path.GetDirectoryName(p));string tmp=p+".tmp",bak=p+".bak";Write(tmp);if(File.Exists(p))File.Copy(p,bak,true);if(File.Exists(p))File.Delete(p);File.Move(tmp,p);dirty=false;saves++;}}
    public static void Reset(bool save){lock(Sync){if(save)Save();loaded=false;dirty=false;}}
    private static string PathName(){string d=GameIO.GetSaveGameDir();return string.IsNullOrEmpty(d)?string.Empty:Path.Combine(d,"RebirthNpcAdvancedProgression.xml");}
    private static void Write(string path){using(var w=XmlWriter.Create(path,new XmlWriterSettings{Indent=true,Encoding=new UTF8Encoding(false)})){w.WriteStartDocument();w.WriteStartElement("rebirthNpcAdvancedProgression");w.WriteAttributeString("schema","1");foreach(var r in RebirthNpcAdvancedProgressionService.ExportRecords()){w.WriteStartElement("npc");w.WriteAttributeString("id",r.NpcId.ToString());w.WriteAttributeString("revision",r.Revision.ToString(CultureInfo.InvariantCulture));foreach(string c in r.Certifications){w.WriteStartElement("cert");w.WriteAttributeString("id",c);w.WriteEndElement();}foreach(var s in r.Specializations){w.WriteStartElement("spec");w.WriteAttributeString("profession",((int)s.Key).ToString(CultureInfo.InvariantCulture));w.WriteAttributeString("id",s.Value);w.WriteEndElement();}foreach(var c in r.CompletedOutcomes){w.WriteStartElement("count");w.WriteAttributeString("profession",((int)c.Key).ToString(CultureInfo.InvariantCulture));w.WriteAttributeString("value",c.Value.ToString(CultureInfo.InvariantCulture));w.WriteEndElement();}w.WriteEndElement();}foreach(var s in RebirthNpcAdvancedProgressionService.ExportMentorships()){w.WriteStartElement("mentorship");w.WriteAttributeString("id",s.SessionId.ToString("N"));w.WriteAttributeString("mentor",s.MentorId.ToString());w.WriteAttributeString("trainee",s.TraineeId.ToString());w.WriteAttributeString("profession",((int)s.Profession).ToString(CultureInfo.InvariantCulture));w.WriteAttributeString("started",s.StartedUtcTicks.ToString(CultureInfo.InvariantCulture));w.WriteAttributeString("expires",s.ExpiresUtcTicks.ToString(CultureInfo.InvariantCulture));w.WriteAttributeString("range",s.MaximumRange.ToString(CultureInfo.InvariantCulture));w.WriteAttributeString("active",s.Active?"1":"0");w.WriteAttributeString("revision",s.Revision.ToString(CultureInfo.InvariantCulture));foreach(Guid id in s.CreditedOutcomeIds){w.WriteStartElement("credit");w.WriteAttributeString("id",id.ToString("N"));w.WriteEndElement();}w.WriteEndElement();}w.WriteEndElement();w.WriteEndDocument();}}
    private static void Read(string path){var doc=new XmlDocument();doc.Load(path);if(doc.DocumentElement==null||doc.DocumentElement.Name!="rebirthNpcAdvancedProgression"||doc.DocumentElement.GetAttribute("schema")!="1")throw new InvalidDataException("Unsupported NPC advanced progression schema.");var records=new List<RebirthNpcAdvancedProgressionRecord>();var sessions=new List<RebirthNpcMentorshipSession>();foreach(XmlNode n in doc.DocumentElement.SelectNodes("npc")){RebirthNpcStableId id;if(!RebirthNpcStableId.TryParse(A(n,"id"),out id))continue;var r=new RebirthNpcAdvancedProgressionRecord{NpcId=id,Revision=L(A(n,"revision"))};foreach(XmlNode x in n.SelectNodes("cert"))r.Certifications.Add(A(x,"id"));foreach(XmlNode x in n.SelectNodes("spec"))r.Specializations[(RebirthNpcProfession)I(A(x,"profession"))]=A(x,"id");foreach(XmlNode x in n.SelectNodes("count"))r.CompletedOutcomes[(RebirthNpcProfession)I(A(x,"profession"))]=I(A(x,"value"));records.Add(r);}foreach(XmlNode n in doc.DocumentElement.SelectNodes("mentorship")){Guid sid;RebirthNpcStableId mentor,trainee;if(!Guid.TryParseExact(A(n,"id"),"N",out sid)||!RebirthNpcStableId.TryParse(A(n,"mentor"),out mentor)||!RebirthNpcStableId.TryParse(A(n,"trainee"),out trainee))continue;var s=new RebirthNpcMentorshipSession{SessionId=sid,MentorId=mentor,TraineeId=trainee,Profession=(RebirthNpcProfession)I(A(n,"profession")),StartedUtcTicks=L(A(n,"started")),ExpiresUtcTicks=L(A(n,"expires")),MaximumRange=F(A(n,"range")),CurrentRange=0,MentorPresent=true,TraineePresent=true,Active=A(n,"active")=="1",Revision=L(A(n,"revision"))};foreach(XmlNode x in n.SelectNodes("credit")){Guid g;if(Guid.TryParseExact(A(x,"id"),"N",out g))s.CreditedOutcomeIds.Add(g);}s.CreditedOutcomes=s.CreditedOutcomeIds.Count;sessions.Add(s);}RebirthNpcAdvancedProgressionService.Import(records,sessions);}
    private static string A(XmlNode n,string k){var a=n.Attributes[k];return a==null?string.Empty:a.Value;}private static long L(string s){long v;return long.TryParse(s,NumberStyles.Integer,CultureInfo.InvariantCulture,out v)?v:0;}private static int I(string s){int v;return int.TryParse(s,NumberStyles.Integer,CultureInfo.InvariantCulture,out v)?v:0;}private static float F(string s){float v;return float.TryParse(s,NumberStyles.Float,CultureInfo.InvariantCulture,out v)?v:0f;}private static bool IsServer(){ConnectionManager c=SingletonMonoBehaviour<ConnectionManager>.Instance;return c==null||c.IsServer;}
}
