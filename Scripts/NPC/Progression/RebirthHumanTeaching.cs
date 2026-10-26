using System;
using System.Linq;
using System.Xml.Linq;
using System.Collections.Generic;
using UnityEngine;

// Dialogue lessons, independent of the assignment queue. A persisted outcome bridges the
// NPC aggregate and player snapshot; the same receipt is retried after an interrupted save.
internal static class RebirthHumanTeaching
{
    private sealed class Session { internal EntityPlayer Player; internal EntityRebirthHumanoidNPC Npc; internal string Skill; internal bool Reverse; internal float Ends; }
    private static readonly Dictionary<RebirthNpcStableId,Session> Sessions=new Dictionary<RebirthNpcStableId,Session>();
    private static readonly HashSet<RebirthNpcStableId> Pending=new HashSet<RebirthNpcStableId>();
    private static readonly HashSet<RebirthNpcStableId> ActiveLessons=new HashSet<RebirthNpcStableId>();
    private static readonly Dictionary<RebirthNpcStableId,float> LastLessonTick=new Dictionary<RebirthNpcStableId,float>();
    private static World scope; private static float nextRecovery,nextLessonTick;private static int lessonCursor;
    internal static bool IsBusy(RebirthNpcStableId id)=>Sessions.ContainsKey(id)||Pending.Contains(id);
    private static string Text(XElement node,string key)=>(string)node?.Attribute(key)??"";
    private static void EnsureScope()
    {var world=GameManager.Instance?.World;if(!ReferenceEquals(scope,world)){scope=world;Sessions.Clear();Pending.Clear();ActiveLessons.Clear();LastLessonTick.Clear();nextRecovery=nextLessonTick=0;lessonCursor=0;}}
    internal static void Recover(RebirthNpcStableId id)
    {
        EnsureScope();var journal=ReadJournal(id);if(journal!=null && Text(journal,"state")=="Pending") Pending.Add(id);
        if(RebirthHumanProgression.TryGet(id,out var record)&&record.Progression.TeachingLessons.Count>0)
        {ActiveLessons.Add(id);LastLessonTick[id]=Time.unscaledTime;}
    }
    private static XElement ReadJournal(RebirthNpcStableId id)
    {
        if(!RebirthNpcAggregatePersistenceStore.TryGet(id,out var person))return null;
        return (person.UnknownOptionalElements??new string[0]).Select(RebirthCompanionDeskStore.Parse).SingleOrDefault(n=>n.Name=="humanLesson");
    }
    private static XElement ReadSavedJournal(RebirthNpcStableId id)
    {
        if(!RebirthNpcAggregatePersistenceStore.TryReadSavedPerson(id,out var person))return null;
        return (person.UnknownOptionalElements??new string[0]).Select(RebirthCompanionDeskStore.Parse).SingleOrDefault(n=>n.Name=="humanLesson");
    }
    internal static List<string> Subjects(EntityPlayer player,RebirthNpcStableId id,bool reverse)
    {
        var list=new List<string>();
        if(!Participants(player,id,out _,out _,out var pc,out var nc))return list;
        foreach(var skill in RebirthSurvivorDefinitionRegistry.Bundle.Progression.Skills)
            if(Preview(pc,nc,skill.Id,reverse,out _,out _))list.Add(skill.Id);
        return list.OrderBy(RebirthSkillDisplayNames.Get,StringComparer.CurrentCultureIgnoreCase).ToList();
    }
    private static bool Participants(EntityPlayer player,RebirthNpcStableId id,out EntityRebirthHumanoidNPC npc,out RebirthStablePlayerIdentity identity,
        out RebirthWorldCharacterRecord pc,out RebirthWorldCharacterRecord nc)
    {
        npc=null;identity=null;pc=nc=null;
        if(player==null || player.world==null || player.world.IsRemote() || player.IsDead() || RebirthNpcPersistenceCoordinator.IsRecoveryInstalling ||
            !RebirthNpcRuntimeRegistry.TryGetEntityId(id,out int entityId) || (npc=player.world.GetEntity(entityId) as EntityRebirthHumanoidNPC)==null || npc.IsDead() ||
            !RebirthHumanAiPolicy.IsHuman(npc) || Vector3.Distance(player.position,npc.position)>RebirthTeachingService.MaximumDistance || npc.GetAttackTarget()!=null ||
            !RebirthSkillAwardService.TryGetEligible(player,out identity,out pc) || !RebirthHumanProgression.TryGet(id,out nc))return false;
        // NPCs owned by another player do not provide this owner's companion dialogue services.
        var runtime=npc.RebirthRuntimeState;
        var persistent=GameManager.Instance.GetPersistentPlayerList()?.GetPlayerDataFromEntityID(player.entityId)?.PrimaryId;
        return runtime.OwnershipKind==RebirthNpcOwnershipKind.None || persistent!=null &&
            (runtime.OwnerId==persistent.ToString() || runtime.OwnerId==identity.CanonicalId || runtime.OwnerId==identity.StorageKey);
    }
    private static string History(RebirthWorldCharacterRecord pc,string skill,bool reverse)=>"human-lesson:"+pc.StablePlayerKey+":"+(reverse?"npc-to-player:":"player-to-npc:")+skill;
    private static bool Preview(RebirthWorldCharacterRecord pc,RebirthWorldCharacterRecord nc,string skill,bool reverse,out float transfer,out float instructorTheory)
    {
        transfer=instructorTheory=0;
        if(!RebirthSurvivorDefinitionRegistry.TryGetSkill(skill,out var definition)||definition.Advanced||skill==RebirthSurvivorIds.SkillTeaching)return false;
        var instructor=reverse?nc:pc;var student=reverse?pc:nc;
        if(!instructor.Progression.Skills.TryGetValue(skill,out var practice)||practice.Value<RebirthTeachingService.MinimumInstructorSkill ||
            !instructor.Progression.SkillKnowledge.TryGetValue(skill,out var theory)||!student.Progression.SkillKnowledge.TryGetValue(skill,out var studentTheory))return false;
        float teaching=instructor.Progression.Skills.TryGetValue(RebirthSurvivorIds.SkillTeaching,out var t)?t.Value:0;
        instructorTheory=theory.Value;
        float requested=RebirthTeachingService.CalculateLessonTransfer(theory.Value,studentTheory.Value,teaching);
        nc.Progression.TeachingHistory.TryGetValue(History(pc,skill,reverse),out var history);
        return RebirthTheoryInstructionPolicy.TryCalculate(studentTheory.Value,theory.Value,requested,RebirthSkillKnowledgeService.GetMinimum(),RebirthSkillKnowledgeService.GetMaximum(),
            RebirthTeachingService.MinimumInstructorKnowledge,RebirthTeachingService.MinimumKnowledgeGap,3f,RebirthTeachingService.PairSubjectCooldownSeconds,
            history?.LastCompletedUtcTicks??0,DateTime.UtcNow.Ticks,out transfer,out _,out _);
    }
    internal static bool Begin(EntityPlayer player,RebirthNpcStableId id,string skill,bool reverse,out string detail)
    {
        EnsureScope();
        detail=Localization.Get("xuiRbHumanLessonUnavailable");
        if(IsBusy(id)||Sessions.Values.Any(s=>ReferenceEquals(s.Player,player)) || !Participants(player,id,out var npc,out _,out var pc,out var nc))return false;
        if(string.IsNullOrEmpty(skill))skill=Subjects(player,id,reverse).FirstOrDefault();
        if(string.IsNullOrEmpty(skill)||!Preview(pc,nc,skill,reverse,out _,out _))return false;
        Sessions[id]=new Session{Player=player,Npc=npc,Skill=skill,Reverse=reverse,Ends=Time.unscaledTime+RebirthTeachingService.SessionDurationSeconds};
        detail=string.Format(Localization.Get("xuiRbHumanLessonStarted"),RebirthSkillDisplayNames.Get(skill));return true;
    }
    internal static void Tick()
    {
        var world=GameManager.Instance?.World;
        if(world==null||world.IsRemote()||RebirthNpcPersistenceCoordinator.IsRecoveryInstalling)return;
        EnsureScope();
        foreach(var pair in Sessions.ToArray())
        {
            var session=pair.Value;
            if(!Participants(session.Player,pair.Key,out var npc,out var identity,out var pc,out var nc)||!ReferenceEquals(session.Npc,npc))
            {Sessions.Remove(pair.Key);continue;}
            if(Time.unscaledTime<session.Ends)continue;
            Sessions.Remove(pair.Key);
            if(!Preview(pc,nc,session.Skill,session.Reverse,out var transfer,out var instructorTheory))continue;
            string operation=Guid.NewGuid().ToString("N"),receipt="human-lesson:"+operation;
            float before=(session.Reverse?pc:nc).Progression.SkillKnowledge[session.Skill].Value;
            float lastingSeconds=0f,lastingMultiplier=1f;
            bool lasting=RebirthTeachingService.TryGetLastingLesson(session.Reverse?nc:pc,
                out lastingSeconds,out lastingMultiplier);
            var journal=new XElement("humanLesson",new XAttribute("schema",1),new XAttribute("state","Pending"),new XAttribute("id",operation),
                new XAttribute("owner",pc.StablePlayerKey),new XAttribute("actor",identity.CanonicalId),new XAttribute("creation",pc.Origin.CreationId),
                new XAttribute("skill",session.Skill),new XAttribute("reverse",session.Reverse),new XAttribute("before",before),new XAttribute("after",before+transfer),
                new XAttribute("transfer",transfer),new XAttribute("at",DateTime.UtcNow.Ticks),
                new XAttribute("lasting",lasting?1:0),new XAttribute("lastingSeconds",lasting?lastingSeconds:0f),
                new XAttribute("lastingMultiplier",lasting?lastingMultiplier:1f));
            if(!session.Reverse)nc.Progression.SkillKnowledge[session.Skill].Value=before+transfer;
            string key=History(pc,session.Skill,session.Reverse);
            nc.Progression.TeachingHistory.TryGetValue(key,out var priorHistory);
            int completionCount=priorHistory==null?1:priorHistory.CompletionCount==int.MaxValue?int.MaxValue:Math.Max(0,priorHistory.CompletionCount)+1;
            nc.Progression.TeachingHistory[key]=new RebirthTeachingHistoryRuntimeState{Key=key,StudentStorageKey=session.Reverse?pc.StablePlayerKey:"npc:"+pair.Key,
                SkillId=session.Skill,LastCompletedUtcTicks=(long)journal.Attribute("at"),CompletionCount=completionCount};
            try { RebirthHumanProgression.Save(pair.Key,nc,journal);Pending.Add(pair.Key); }
            catch(Exception error){Pending.Add(pair.Key);Log.Warning("[REBIRTH Human Lesson] outcome retained: "+error.Message);}
        }
        TickActiveLessons(world);
        if(Time.unscaledTime<nextRecovery)return;nextRecovery=Time.unscaledTime+2;
        foreach(var id in Pending.ToArray())
        {
            try{Complete(id);}catch(Exception error){Log.Warning("[REBIRTH Human Lesson] retry retained: "+error.Message);}
        }
    }
    private static void TickActiveLessons(World world)
    {
        float now=Time.unscaledTime;
        if(now<nextLessonTick||ActiveLessons.Count==0)return;
        nextLessonTick=now+5f;
        var ids=ActiveLessons.OrderBy(x=>x.ToString(),StringComparer.Ordinal).ToArray();
        int count=Math.Min(8,ids.Length);
        for(int i=0;i<count;i++)
        {
            var id=ids[(lessonCursor+i)%ids.Length];
            if(!RebirthNpcRuntimeRegistry.TryGetEntityId(id,out int entityId)||
               world.GetEntity(entityId) is not EntityRebirthHumanoidNPC npc||npc.IsDead()||
               npc.IsPreparedRestorationPending)
            {LastLessonTick[id]=now;continue;}
            if(!LastLessonTick.TryGetValue(id,out float previous))
            {LastLessonTick[id]=now;continue;}
            float elapsed=Mathf.Clamp(now-previous,0f,30f);
            if(elapsed<=0f)continue;
            try
            {
                if(!RebirthHumanProgression.TryGet(id,out var record))continue;
                if(record.Progression.TeachingLessons.Count==0)
                {ActiveLessons.Remove(id);LastLessonTick.Remove(id);continue;}
                foreach(var pair in record.Progression.TeachingLessons.ToArray())
                {
                    var lesson=pair.Value;
                    if(lesson==null||lesson.RemainingActiveSeconds<=elapsed)
                        record.Progression.TeachingLessons.Remove(pair.Key);
                    else lesson.RemainingActiveSeconds-=elapsed;
                }
                if(!RebirthHumanProgression.Save(id,record))continue;
                LastLessonTick[id]=previous+elapsed;
                if(record.Progression.TeachingLessons.Count==0)
                {ActiveLessons.Remove(id);LastLessonTick.Remove(id);}
            }
            catch(Exception error){Log.Warning("[REBIRTH Human Lesson] timed effect retained: "+error.Message);}
        }
        lessonCursor=(lessonCursor+count)%Math.Max(1,ids.Length);
    }
    private static void Complete(RebirthNpcStableId id)
    {
        var journal=ReadJournal(id);
        if(journal==null||Text(journal,"state")!="Pending"){Pending.Remove(id);return;}
        // The in-memory aggregate can be ahead of disk after a save-scope or
        // checkpoint failure. Never transfer Theory or award the instructor
        // until the exact operation is observable in the current primary file.
        var saved=ReadSavedJournal(id);
        if(saved==null||!XNode.DeepEquals(saved,journal))return;
        var player=GameManager.Instance.World.Players.list.FirstOrDefault(p=>RebirthStablePlayerIdentity.TryResolveServerEntity(p,out var candidate)&&candidate.StorageKey==Text(journal,"owner"));
        if(player==null||!RebirthSkillAwardService.TryGetEligible(player,out var identity,out var pc)||pc.StablePlayerKey!=Text(journal,"owner")||pc.Origin.CreationId!=Text(journal,"creation"))return;
        string skill=Text(journal,"skill"),receipt="human-lesson:"+Text(journal,"id");bool reverse=(bool)journal.Attribute("reverse");float amount=(float)journal.Attribute("transfer");
        if(reverse)
        {
            if(!pc.Progression.SkillAwardReceipts.Contains(receipt))
            {
                if(!pc.Progression.SkillKnowledge.TryGetValue(skill,out var theory))return;
                theory.Value=Math.Max(theory.Value,(float)journal.Attribute("after"));
                if((int?)journal.Attribute("lasting")==1)
                {
                    float seconds=(float)journal.Attribute("lastingSeconds");
                    float multiplier=(float)journal.Attribute("lastingMultiplier");
                    if(float.IsNaN(seconds)||float.IsInfinity(seconds)||seconds<60f||seconds>7200f||
                       float.IsNaN(multiplier)||float.IsInfinity(multiplier)||multiplier<1f||multiplier>3f)return;
                    if(!pc.Progression.TeachingLessons.TryGetValue(skill,out var prior)||prior==null)
                        pc.Progression.TeachingLessons[skill]=new RebirthTeachingLessonRuntimeState
                        {SkillId=skill,RemainingActiveSeconds=seconds,GainMultiplier=multiplier,
                         InstructorStorageKey="npc:"+id,OutcomeId=Text(journal,"id")};
                }
                pc.Progression.SkillAwardReceipts.Add(receipt);pc.Touch("human-lesson:"+skill);
            }
            if(!RebirthWorldCharacterRepository.SaveIfDirty(identity,"human-lesson-student"))return;
            var evidence=Evidence(receipt,amount);
            if(!RebirthHumanProgression.Award(id,evidence))return;
        }
        else
        {
            var evidence=Evidence(receipt,amount);
            if(!RebirthSkillAwardService.TryAwardMigratedTrainingEvidence(player,evidence,out _,out _,out _))return;
        }
        if(!RebirthHumanProgression.TryGet(id,out var nc))return;
        if(!reverse&&(int?)journal.Attribute("lasting")==1)
        {
            float seconds=(float)journal.Attribute("lastingSeconds");
            float multiplier=(float)journal.Attribute("lastingMultiplier");
            if(float.IsNaN(seconds)||float.IsInfinity(seconds)||seconds<60f||seconds>7200f||
               float.IsNaN(multiplier)||float.IsInfinity(multiplier)||multiplier<1f||multiplier>3f)return;
            if(!nc.Progression.TeachingLessons.TryGetValue(skill,out var prior)||prior==null)
                nc.Progression.TeachingLessons[skill]=new RebirthTeachingLessonRuntimeState
                {SkillId=skill,RemainingActiveSeconds=seconds,GainMultiplier=multiplier,
                 InstructorStorageKey=pc.StablePlayerKey,OutcomeId=Text(journal,"id")};
        }
        journal.SetAttributeValue("state","Completed");
        if(!RebirthHumanProgression.Save(id,nc,journal))return;
        Pending.Remove(id);
        if(nc.Progression.TeachingLessons.Count>0)
        {ActiveLessons.Add(id);LastLessonTick[id]=Time.unscaledTime;}
        RebirthSkillAwardService.QueueOwnerPublication(player);
    }
    private static RebirthSkillTrainingEvidence Evidence(string receipt,float amount)=>new RebirthSkillTrainingEvidence
    {
        SkillId=RebirthSurvivorIds.SkillTeaching,SourceKey=receipt,DurableReceiptId=receipt,ReferenceDescription="persisted Theory transferred between player and human NPC",
        AuthoritativeSuccess=true,Mode=RebirthSkillTrainingEvidenceMode.DiscreteAward,CreditedWork=1f,DiscreteRawAward=Mathf.Clamp(.25f+amount*.1f,.25f,.6f)
    };
}
