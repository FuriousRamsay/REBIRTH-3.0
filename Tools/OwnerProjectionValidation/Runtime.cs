using System;
using System.Collections.Generic;

// Offline boundary substitutes: this exercises the production immutable projection,
// not packet acceptance, Unity, client ownership or server authority.
public enum RebirthSurvivorOwnerCreationState { NotApplicable }
public static class RebirthSurvivorDefinitionRegistry { public static object Bundle; public static string SemanticHash; }
public class RebirthSurvivorOwnerSkillSnapshot { public string Id; public float Value; }
public class RebirthSurvivorOwnerSkillKnowledgeSnapshot { public string Id; public float Value; }
public class RebirthSurvivorOwnerAttributeSnapshot { public string Id; public float Current; }
public class RebirthSurvivorOwnerStateSnapshot
{
    public long CharacterRevision;
    public bool RebirthModeEnabled, HasCharacter;
    public RebirthSurvivorOwnerCreationState CreationState;
    public string BackgroundId;
    public List<string> TraitIds, KnowledgeIds;
    public List<RebirthSurvivorOwnerSkillSnapshot> Skills;
    public List<RebirthSurvivorOwnerSkillKnowledgeSnapshot> SkillKnowledge;
    public List<RebirthSurvivorOwnerAttributeSnapshot> Attributes;
}
public static class Program
{
    static int checks;
    static void Check(bool value, string name)
    {
        if (!value) throw new Exception(name);
        checks++; Console.WriteLine("PASS " + name);
    }
    public static void Main()
    {
        var item = new RebirthSurvivorOwnerSkillKnowledgeSnapshot { Id = "Cooking", Value = 12 };
        var state = new RebirthSurvivorOwnerStateSnapshot {
            SkillKnowledge = new List<RebirthSurvivorOwnerSkillKnowledgeSnapshot> {
                null, new RebirthSurvivorOwnerSkillKnowledgeSnapshot { Id = "" }, item,
                new RebirthSurvivorOwnerSkillKnowledgeSnapshot { Id = "COOKING", Value = 99 } },
            Skills = new List<RebirthSurvivorOwnerSkillSnapshot> {
                new RebirthSurvivorOwnerSkillSnapshot { Id = "AnimalHandling", Value = 7 } }
        };
        var first = new RebirthSurvivorOwnerScalars(state);
        float value;
        Check(first.TryGetTheory("cooking", out value) && value == 12, "case insensitive theory; first duplicate wins");
        Check(!first.TryGetTheory(null, out value) && value == 0, "null key returns zero");
        Check(!first.TryGetTheory("", out value) && value == 0, "empty key returns zero");
        Check(!first.TryGetTheory("Missing", out value) && value == 0, "unknown key returns zero");
        item.Value = 42;
        Check(first.TryGetTheory("Cooking", out value) && value == 12, "source mutation cannot alter accepted cache");
        var next = new RebirthSurvivorOwnerScalars(state);
        Check(next.TryGetTheory("Cooking", out value) && value == 42, "replacement cache receives new value");
        state.SkillKnowledge.Clear();
        Check(next.TryGetTheory("Cooking", out value) && value == 42, "source list clearing cannot alter cache");
        Check(first.TryGetSkill("animalhandling", out value) && value == 7, "animal handling skill retained");
        Check(!first.TryGetSkill("Cooking", out value), "theory and practice remain distinct");
        var empty = new RebirthSurvivorOwnerScalars(new RebirthSurvivorOwnerStateSnapshot());
        Check(!empty.TryGetTheory("Cooking", out value), "null lists accepted");
        Console.WriteLine(checks + "/" + checks + " checks passed. Offline projection only; no live network validation.");
    }
}
