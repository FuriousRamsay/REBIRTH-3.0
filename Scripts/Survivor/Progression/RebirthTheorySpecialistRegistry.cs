using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

// Authored expertise only. Runtime must resolve an actual server-owned NPC profile;
// a client-supplied profile name is never instructor authority.
public static class RebirthTheorySpecialistRegistry
{
    private static Dictionary<string, Dictionary<string, float>> profiles =
        new Dictionary<string, Dictionary<string, float>>(StringComparer.OrdinalIgnoreCase);
    public static int ProfileCount => profiles.Count;
    public static void Clear() { profiles = new Dictionary<string, Dictionary<string, float>>(StringComparer.OrdinalIgnoreCase); }
    public static bool TryGetTheory(string profileId, string skillId, out float theory)
    {
        theory = 0f;
        return profileId != null && skillId != null && profiles.TryGetValue(profileId, out var subjects)
            && subjects.TryGetValue(skillId, out theory);
    }
    public static string[] GetSubjects(string profileId)
    {
        if(profileId==null||!profiles.TryGetValue(profileId,out var subjects))return new string[0];
        var result=subjects.Keys.ToArray();
        Array.Sort(result,StringComparer.OrdinalIgnoreCase);
        return result; // Detached: callers cannot mutate authored expertise.
    }
    public static void Load(string path, Func<string, bool> validProfile, Func<string, bool> validSubject, float minimumTheory)
    {
        Clear();
        if (validProfile == null || validSubject == null) throw new ArgumentNullException("Specialist validators");
        if (float.IsNaN(minimumTheory) || float.IsInfinity(minimumTheory) || minimumTheory < 0f || minimumTheory > 100f) throw new InvalidDataException("Invalid instructor Theory minimum");
        if (!File.Exists(path)) return; // Existing configurations retain no specialists.
        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null,
            MaxCharactersInDocument = 1024 * 1024 };
        XDocument doc;
        using (var reader = XmlReader.Create(path, settings)) doc = XDocument.Load(reader);
        var root = doc.Root;
        if (root == null || root.Name != "survivor_theory_specialists" || root.Attributes().Count() != 1
            || (string)root.Attribute("schema_version") != "1") throw new InvalidDataException("Invalid Theory specialists root");
        var staged = new Dictionary<string, Dictionary<string, float>>(StringComparer.OrdinalIgnoreCase);
        foreach (var node in root.Elements())
        {
            string id = (string)node.Attribute("profile_id");
            if (node.Name != "specialist" || node.Attributes().Count() != 1 || string.IsNullOrWhiteSpace(id)
                || id.Length > 128 || !validProfile(id) || staged.Count >= 128 || staged.ContainsKey(id))
                throw new InvalidDataException("Invalid or duplicate specialist profile");
            var subjects = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
            foreach (var subject in node.Elements())
            {
                string skill = (string)subject.Attribute("skill_id");
                float value;
                if (subject.Name != "subject" || subject.HasElements || subject.Attributes().Count() != 2
                    || string.IsNullOrWhiteSpace(skill) || skill.Length > 128 || !validSubject(skill)
                    || subjects.Count >= 64 || subjects.ContainsKey(skill)
                    || !float.TryParse((string)subject.Attribute("theory"), NumberStyles.Float, CultureInfo.InvariantCulture, out value)
                    || float.IsNaN(value) || float.IsInfinity(value) || value < minimumTheory || value > 100f)
                    throw new InvalidDataException("Invalid specialist Theory subject");
                CheckText(subject); subjects.Add(skill, value);
            }
            CheckText(node);
            if (subjects.Count == 0) throw new InvalidDataException("Specialist has no subjects");
            staged.Add(id, subjects);
        }
        CheckText(root);
        profiles = staged; // Publish only a fully validated catalogue.
    }
    private static void CheckText(XElement node)
    {
        if (node.Nodes().Any(n => !(n is XElement) && !(n is XComment)
            && (!(n is XText) || !string.IsNullOrWhiteSpace(((XText)n).Value))))
            throw new InvalidDataException("Unexpected specialist XML content");
    }
}
