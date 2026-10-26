using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using UnityEngine;

// Explicitly formed world groups only. Never infer membership from nearby
// independent spawns, which could convert an old hireable standalone person.
internal static class RebirthHumanGroupRoles
{
    private const string Element = "humanGroup";
    private const string Skill = RebirthSurvivorIds.SkillLeadership;
    private static World recoveryWorld;
    private static float nextRecovery;
    private static int recoveryCursor;

    internal static void Tick()
    {
        var world = GameManager.Instance?.World;
        if(world == null || world.IsRemote() || !ThreadManager.IsMainThread() ||
           RebirthNpcPersistenceCoordinator.IsRecoveryInstalling) return;
        if(!ReferenceEquals(recoveryWorld, world)) { recoveryWorld = world; nextRecovery = 0f; recoveryCursor = 0; }
        RebirthP4NaturalGroupOccurrence.Tick(world);
        if(Time.unscaledTime < nextRecovery) return;
        nextRecovery = Time.unscaledTime + 2f;
        int resumed = 0;
        var snapshot = RebirthNpcRuntimeRegistry.GetSnapshot();
        if(snapshot.Length == 0) return;
        int inspected = Math.Min(32, snapshot.Length);
        for(int i = 0; i < inspected; i++)
        {
            if(resumed >= 2) break;
            var runtime = snapshot[(recoveryCursor + i) % snapshot.Length];
            if(runtime == null) continue;
            if(RebirthNpcRuntimeRegistry.TryGetEntityId(runtime.StableId,out var liveId) &&
                world.GetEntity(liveId) is EntityRebirthHumanoidNPC live && live.IsFreshGroupBootstrapPending)
                live.TryReleaseFreshGroupBootstrap();
            if(!RebirthNpcAggregatePersistenceStore.TryGet(runtime.StableId, out var person)) continue;
            XElement node;
            try { node = Group(person); }
            catch(Exception error) when (error is InvalidDataException || error is System.Xml.XmlException) { continue; }
            if(node == null && resumed < 1 && person.NativeReconstruction != null &&
                RebirthNpcRuntimeRegistry.TryGetEntityId(runtime.StableId,out var leaderEntityId) &&
                world.GetEntity(leaderEntityId) is EntityRebirthHumanoidNPC leaderNpc &&
                leaderNpc.IsFreshGroupBootstrapPending &&
                EntityClass.list != null && EntityClass.list.TryGetValue(leaderNpc.entityClass,out var nativeClass) &&
                (nativeClass.entityClassName == "npcRebirthGroupLeaderManSDCS" ||
                 nativeClass.entityClassName == "npcRebirthGroupLeaderWomanSDCS"))
            {
                resumed++;
                var forward = leaderNpc.RootTransform == null ? Vector3.forward : leaderNpc.RootTransform.forward;
                var right = new Vector3(forward.z, 0f, -forward.x).normalized;
                if(right.sqrMagnitude < .5f) right = Vector3.right;
                RebirthNpcFreshGroupSpawn.TryComposeFollowers(runtime.StableId,
                    new[] { leaderNpc.position + right * 2.5f, leaderNpc.position - right * 2.5f },
                    out _, out _);
                continue;
            }
            if(node == null || (string)node.Attribute("state") != "forming" ||
               (string)node.Attribute("role") != "leader" ||
               (string)node.Attribute("leader") != runtime.StableId.ToString()) continue;
            var rawMembers = ((string)node.Attribute("members") ?? "").Split(',');
            if(rawMembers.Length < 1 || rawMembers.Length > 4) continue;
            var members = new List<RebirthNpcStableId>();
            foreach(var raw in rawMembers)
            {
                if(!RebirthNpcStableId.TryParse(raw, out var id)) { members.Clear(); break; }
                members.Add(id);
            }
            if(members.Count != rawMembers.Length) continue;
            resumed++;
            TryEstablish(runtime.StableId, members);
        }
        recoveryCursor = (recoveryCursor + inspected) % snapshot.Length;
    }

    internal static bool TryEstablish(RebirthNpcStableId leader, IList<RebirthNpcStableId> followers)
    {
        var world = GameManager.Instance?.World;
        if(world == null || world.IsRemote() || !ThreadManager.IsMainThread() ||
           RebirthNpcPersistenceCoordinator.IsRecoveryInstalling || leader.IsEmpty ||
           followers == null || followers.Count < 1 || followers.Count > 4) return false;
        var distinct = new HashSet<RebirthNpcStableId>(followers);
        if(distinct.Count != followers.Count || distinct.Contains(leader) || distinct.Any(id => id.IsEmpty)) return false;
        var ordered = distinct.OrderBy(id => id.ToString(), StringComparer.Ordinal).ToArray();
        string group = "human:" + leader;
        if(!ValidUnownedHuman(leader, group, "leader")) return false;
        foreach(var id in ordered) if(!ValidUnownedHuman(id, group, "follower")) return false;

        // The leader's journal is persisted first. Interrupted formation is
        // protected from hiring and can be retried using the exact same IDs.
        string members = string.Join(",", ordered.Select(id => id.ToString()).ToArray());
        if(!Put(leader, group, leader, "leader", "forming", members)) return false;
        if(!SetLeadership(leader, LeaderExperience(leader, ordered.Length), false)) return false;
        foreach(var id in ordered)
        {
            if(!Put(id, group, leader, "follower", "forming", "")) return false;
            if(!SetLeadership(id, 19f, true)) return false;
        }
        foreach(var id in ordered) if(!Put(id, group, leader, "follower", "active", "")) return false;
        return Put(leader, group, leader, "leader", "active", members);
    }

    internal static bool TryRead(RebirthNpcStableId id, out RebirthNpcStableId leader, out string role, out bool active)
    {
        leader = default(RebirthNpcStableId); role = ""; active = false;
        if(!RebirthNpcAggregatePersistenceStore.TryGet(id, out var person)) return false;
        XElement group;
        try { group = Group(person); }
        catch(Exception error) when (error is InvalidDataException || error is System.Xml.XmlException) { return false; }
        if(group == null || (int?)group.Attribute("schema") != 1 ||
           !RebirthNpcStableId.TryParse((string)group.Attribute("leader"), out leader) ||
           !string.Equals((string)group.Attribute("group"), "human:" + leader, StringComparison.Ordinal)) return false;
        role = (string)group.Attribute("role") ?? "";
        if(role != "leader" && role != "follower") return false;
        if(role == "leader" && id != leader || role == "follower" && id == leader) return false;
        active = (string)group.Attribute("state") == "active";
        return true;
    }

    internal static bool CanRecruit(RebirthNpcStableId id)
    {
        if(RebirthNpcRuntimeRegistry.TryGetEntityId(id,out var nativeId) &&
            GameManager.Instance?.World?.GetEntity(nativeId) is EntityRebirthHumanoidNPC held &&
            held.IsFreshGroupBootstrapPending) return false;
        if(!RebirthNpcAggregatePersistenceStore.TryGet(id, out var person)) return false;
        try { return Group(person) == null; }
        catch(Exception error) when (error is InvalidDataException || error is System.Xml.XmlException) { return false; }
    }

    private static bool ValidUnownedHuman(RebirthNpcStableId id, string expectedGroup, string expectedRole)
    {
        if(!RebirthNpcAggregatePersistenceStore.TryGet(id, out var person) || person.Identity == null ||
           (person.Identity.Category != "Survivor" && person.Identity.Category != "Bandit" &&
            person.Identity.Category != "SpecialHumanoid")) return false;
        var runtime = RebirthNpcRuntimeRegistry.GetSnapshot().FirstOrDefault(row => row != null && row.StableId == id);
        if(runtime == null || runtime.OwnershipKind != RebirthNpcOwnershipKind.None) return false;
        XElement group;
        try { group = Group(person); }
        catch(Exception error) when (error is InvalidDataException || error is System.Xml.XmlException) { return false; }
        return group == null || string.Equals((string)group.Attribute("group"), expectedGroup, StringComparison.Ordinal) &&
            string.Equals((string)group.Attribute("role"), expectedRole, StringComparison.Ordinal);
    }

    private static XElement Group(RebirthNpcPersistentRecord person)
    {
        XElement found = null;
        foreach(var raw in person.UnknownOptionalElements ?? new string[0])
        {
            var node = RebirthCompanionDeskStore.Parse(raw);
            if(node.Name != Element) continue;
            if(found != null) throw new InvalidDataException("Duplicate human group extension");
            found = node;
        }
        return found;
    }

    private static bool Put(RebirthNpcStableId id, string group, RebirthNpcStableId leader,
        string role, string state, string members)
    {
        if(!RebirthNpcAggregatePersistenceStore.TryGet(id, out var person)) return false;
        var existing = Group(person);
        if(existing != null && (!string.Equals((string)existing.Attribute("group"), group, StringComparison.Ordinal) ||
            !string.Equals((string)existing.Attribute("role"), role, StringComparison.Ordinal) ||
            !string.Equals((string)existing.Attribute("leader"), leader.ToString(), StringComparison.Ordinal))) return false;
        if(existing != null && (string)existing.Attribute("state") == "active" &&
           (!string.Equals((string)existing.Attribute("members") ?? "", members, StringComparison.Ordinal) ||
            state == "forming")) return state == "forming" &&
                string.Equals((string)existing.Attribute("members") ?? "", members, StringComparison.Ordinal);
        if(existing != null && (string)existing.Attribute("state") == state &&
            string.Equals((string)existing.Attribute("members") ?? "", members, StringComparison.Ordinal)) return true;
        var node = new XElement(Element, new XAttribute("schema", 1), new XAttribute("group", group),
            new XAttribute("leader", leader), new XAttribute("role", role),
            new XAttribute("state", state), new XAttribute("members", members));
        if(!RebirthNpcAggregatePersistenceStore.TryMutateExisting(id, person.AggregateRevision, candidate =>
        {
            candidate.UnknownOptionalElements = (candidate.UnknownOptionalElements ?? new string[0])
                .Where(raw => RebirthCompanionDeskStore.Parse(raw).Name != Element)
                .Concat(new[] { node.ToString(SaveOptions.DisableFormatting) }).ToArray();
            return true;
        })) return false;
        RebirthNpcAggregatePersistenceStore.Save();
        return true;
    }

    private static bool SetLeadership(RebirthNpcStableId id, float value, bool follower)
    {
        if(!RebirthHumanProgression.TryGet(id, out var record) ||
           !record.Progression.Skills.TryGetValue(Skill, out var skill)) return false;
        float target = follower ? Math.Min(19f, skill.Value) : Math.Max(skill.Value, value);
        if(!follower && Math.Abs(skill.Value - target) < .001f ||
           follower && skill.Value + skill.Progress < 20f) return true;
        skill.Value = target;
        if(follower) skill.Progress = Math.Min(skill.Progress, Math.Max(0f, 19.999f - target));
        return RebirthHumanProgression.Save(id, record);
    }

    private static float LeaderExperience(RebirthNpcStableId id, int count)
    {
        // Conservative deterministic distribution: a minority reach 60+,
        // while a group's actual follower count always fits its leader's cap.
        byte[] hash;
        using(var sha = SHA256.Create()) hash = sha.ComputeHash(Encoding.UTF8.GetBytes(id + ":group-leadership-v1"));
        int percentile = hash[0] % 100;
        float generated = percentile < 5 ? 60 + hash[1] % 21 :
            percentile < 20 ? 40 + hash[1] % 20 :
            percentile < 50 ? 20 + hash[1] % 20 : 10 + hash[1] % 10;
        float minimum = count >= 4 ? 60 : count == 3 ? 40 : count == 2 ? 20 : 0;
        return Math.Max(minimum, generated);
    }
}
