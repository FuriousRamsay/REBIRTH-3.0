using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

#nullable disable

internal static class RebirthSurvivorNetworkCodec
{

    // Installed build 25661859 sizes native transport from serialized bytes, not GetLength.
    // Retain packet estimates for explicit diagnostics and wire regression checks.
    // BinaryWriter.Write(string) uses UTF-8 plus a 7-bit byte-count prefix; estimate
    // the same cleaned string as WriteString without controlling native allocation.
    public static int EstimateString(string value, int maxLength)
    {
        string cleaned = Clean(value, maxLength);
        int byteCount = Encoding.UTF8.GetByteCount(cleaned);
        return SevenBitEncodedIntLength(byteCount) + byteCount;
    }

    public static string ReadBoundedString(BinaryReader reader, int maxLength)
    {
        if (reader == null) throw new ArgumentNullException("reader");
        if (maxLength < 0) throw new ArgumentOutOfRangeException("maxLength");
        string value = reader.ReadString() ?? string.Empty;
        int maxBytes = Encoding.UTF8.GetMaxByteCount(maxLength);
        if (value.Length > maxLength || Encoding.UTF8.GetByteCount(value) > maxBytes)
            throw new InvalidDataException("Network string exceeds the configured bound.");
        return value;
    }

    private static int SevenBitEncodedIntLength(int value)
    {
        uint remaining = unchecked((uint)Math.Max(0, value));
        int bytes = 1;
        while (remaining >= 0x80u)
        {
            remaining >>= 7;
            bytes++;
        }
        return bytes;
    }

    public static int EstimateCreationRequest(RebirthSurvivorCreationNetworkRequest request)
    {
        RebirthSurvivorCreationNetworkRequest value = request ?? new RebirthSurvivorCreationNetworkRequest();
        int length = 4 + 4 + 8 + 1 + 2 + 2; // fixed fields + list counts
        length += EstimateString(value.ClientDefinitionHash, RebirthSurvivorNetworkProtocol.MaxHashLength);
        length += EstimateString(value.ClientDefinitionVersion, RebirthSurvivorNetworkProtocol.MaxVersionLength);
        length += EstimateString(value.SourceProfileId, RebirthSurvivorNetworkProtocol.MaxIdLength);
        length += EstimateString(value.SourceProfileName, RebirthSurvivorNetworkProtocol.MaxProfileNameLength);
        length += EstimateString(value.BackgroundId, RebirthSurvivorNetworkProtocol.MaxIdLength);
        length += EstimateString(value.DietId, RebirthSurvivorNetworkProtocol.MaxIdLength);

        int traitCount = Math.Min(value.TraitIds.Count, RebirthSurvivorNetworkProtocol.MaxTraitIds);
        for (int i = 0; i < traitCount; i++)
            length += EstimateString(value.TraitIds[i], RebirthSurvivorNetworkProtocol.MaxIdLength);

        List<string> choiceKeys = new List<string>(value.CreationChoices.Keys);
        choiceKeys.Sort(StringComparer.Ordinal);
        int choiceCount = Math.Min(choiceKeys.Count, RebirthSurvivorNetworkProtocol.MaxCreationChoices);
        for (int i = 0; i < choiceCount; i++)
        {
            string key = choiceKeys[i];
            length += EstimateString(key, RebirthSurvivorNetworkProtocol.MaxChoiceKeyLength);
            string choiceValue;
            value.CreationChoices.TryGetValue(key, out choiceValue);
            length += EstimateString(choiceValue, RebirthSurvivorNetworkProtocol.MaxChoiceValueLength);
        }
        return length + 32; // NetPackage/header/alignment safety margin.
    }

    public static int EstimateCreationResponse(RebirthSurvivorCreationNetworkResponse response)
    {
        RebirthSurvivorCreationNetworkResponse value = response ?? new RebirthSurvivorCreationNetworkResponse();
        int length = 4 + 8 + 1 + 1 + 1 + 8 + 1;
        length += EstimateString(value.ServerDefinitionHash, RebirthSurvivorNetworkProtocol.MaxHashLength);
        length += EstimateString(value.ServerDefinitionVersion, RebirthSurvivorNetworkProtocol.MaxVersionLength);
        length += EstimateString(value.MessageCode, RebirthSurvivorNetworkProtocol.MaxIdLength);
        length += EstimateCreationResult(value.ValidationResult);
        return length + 32;
    }

    public static int EstimateCreationResult(RebirthSurvivorCreationResult result)
    {
        if (result == null) return 1;
        int length = 1 + 1 + 4 + 1 + 4 + 4 + 1 + 1 + 2; // + Skill Knowledge list count
        length += EstimateString(result.DefinitionHash, RebirthSurvivorNetworkProtocol.MaxHashLength);
        length += EstimateString(result.DefinitionVersion, RebirthSurvivorNetworkProtocol.MaxVersionLength);
        length += EstimateString(result.BackgroundId, RebirthSurvivorNetworkProtocol.MaxIdLength);
        length += EstimateString(result.DietId, RebirthSurvivorNetworkProtocol.MaxIdLength);

        int traitCount = Math.Min(result.TraitIds.Count, RebirthSurvivorNetworkProtocol.MaxTraitIds);
        for (int i = 0; i < traitCount; i++)
            length += EstimateString(result.TraitIds[i], RebirthSurvivorNetworkProtocol.MaxIdLength);

        int attrCount = Math.Min(result.Attributes.Count, RebirthSurvivorNetworkProtocol.MaxAttributes);
        for (int i = 0; i < attrCount; i++)
        {
            RebirthResolvedAttributeStart a = result.Attributes[i];
            length += EstimateString(a.AttributeId, RebirthSurvivorNetworkProtocol.MaxIdLength) + 8;
        }

        List<string> skillIds = new List<string>(result.StartingSkills.Keys);
        skillIds.Sort(StringComparer.Ordinal);
        int skillCount = Math.Min(skillIds.Count, RebirthSurvivorNetworkProtocol.MaxSkills);
        for (int i = 0; i < skillCount; i++)
            length += EstimateString(skillIds[i], RebirthSurvivorNetworkProtocol.MaxIdLength) + 4;

        List<string> skillKnowledgeIds = new List<string>(result.StartingSkillKnowledge.Keys);
        skillKnowledgeIds.Sort(StringComparer.Ordinal);
        int skillKnowledgeCount = Math.Min(skillKnowledgeIds.Count, RebirthSurvivorNetworkProtocol.MaxSkillKnowledge);
        for (int i = 0; i < skillKnowledgeCount; i++)
            length += EstimateString(skillKnowledgeIds[i], RebirthSurvivorNetworkProtocol.MaxIdLength) + 4;

        int knowledgeCount = Math.Min(result.StartingKnowledgeIds.Count, RebirthSurvivorNetworkProtocol.MaxKnowledgeIds);
        for (int i = 0; i < knowledgeCount; i++)
            length += EstimateString(result.StartingKnowledgeIds[i], RebirthSurvivorNetworkProtocol.MaxIdLength);

        int errorCount = Math.Min(result.Errors.Count, RebirthSurvivorNetworkProtocol.MaxErrorEntries);
        for (int i = 0; i < errorCount; i++)
        {
            RebirthSurvivorCreationError e = result.Errors[i];
            length += 2;
            length += EstimateString(e.SubjectId, RebirthSurvivorNetworkProtocol.MaxIdLength);
            length += EstimateString(e.RelatedId, RebirthSurvivorNetworkProtocol.MaxIdLength);
        }
        return length;
    }

    public static int EstimateOwnerState(RebirthSurvivorOwnerStateSnapshot snapshot)
    {
        RebirthSurvivorOwnerStateSnapshot s = snapshot ?? new RebirthSurvivorOwnerStateSnapshot();
        int length = (s.ProtocolVersion >= 12 ? 8 : 0) + 4 + 1 + 1 + 8 + 1 + 20 + 1 + 1 + 1 + 1 + 2; // includes Skill Knowledge + Discipline list counts
        length += EstimateString(s.CreationId, RebirthSurvivorNetworkProtocol.MaxIdLength);
        length += EstimateString(s.ServerDefinitionHash, RebirthSurvivorNetworkProtocol.MaxHashLength);
        length += EstimateString(s.ServerDefinitionVersion, RebirthSurvivorNetworkProtocol.MaxVersionLength);
        length += EstimateString(s.OriginDefinitionHash, RebirthSurvivorNetworkProtocol.MaxHashLength);
        length += EstimateString(s.OriginDefinitionVersion, RebirthSurvivorNetworkProtocol.MaxVersionLength);
        length += EstimateString(s.SourceProfileName, RebirthSurvivorNetworkProtocol.MaxProfileNameLength);
        length += EstimateString(s.BackgroundId, RebirthSurvivorNetworkProtocol.MaxIdLength);
        length += EstimateString(s.DietId, RebirthSurvivorNetworkProtocol.MaxIdLength);

        int traitCount = Math.Min(s.TraitIds.Count, RebirthSurvivorNetworkProtocol.MaxTraitIds);
        for (int i = 0; i < traitCount; i++)
            length += EstimateString(s.TraitIds[i], RebirthSurvivorNetworkProtocol.MaxIdLength);

        int attrCount = Math.Min(s.Attributes.Count, RebirthSurvivorNetworkProtocol.MaxAttributes);
        for (int i = 0; i < attrCount; i++)
        {
            RebirthSurvivorOwnerAttributeSnapshot a = s.Attributes[i];
            length += EstimateString(a != null ? a.Id : string.Empty, RebirthSurvivorNetworkProtocol.MaxIdLength) + 8;
        }

        int skillCount = Math.Min(s.Skills.Count, RebirthSurvivorNetworkProtocol.MaxSkills);
        for (int i = 0; i < skillCount; i++)
        {
            RebirthSurvivorOwnerSkillSnapshot skill = s.Skills[i];
            length += EstimateString(skill != null ? skill.Id : string.Empty, RebirthSurvivorNetworkProtocol.MaxIdLength) + 8;
        }

        int skillKnowledgeCount = Math.Min(s.SkillKnowledge.Count, RebirthSurvivorNetworkProtocol.MaxSkillKnowledge);
        for (int i = 0; i < skillKnowledgeCount; i++)
        {
            RebirthSurvivorOwnerSkillKnowledgeSnapshot knowledge = s.SkillKnowledge[i];
            length += EstimateString(knowledge != null ? knowledge.Id : string.Empty, RebirthSurvivorNetworkProtocol.MaxIdLength) + 4;
        }

        int knowledgeCount = Math.Min(s.KnowledgeIds.Count, RebirthSurvivorNetworkProtocol.MaxKnowledgeIds);
        for (int i = 0; i < knowledgeCount; i++)
            length += EstimateString(s.KnowledgeIds[i], RebirthSurvivorNetworkProtocol.MaxIdLength);

        int disciplineCount = Math.Min(s.AcquiredDisciplineIds.Count, RebirthSurvivorNetworkProtocol.MaxDisciplineIds);
        for (int i = 0; i < disciplineCount; i++) length += EstimateString(s.AcquiredDisciplineIds[i], RebirthSurvivorNetworkProtocol.MaxIdLength);

        int accomplishmentCount=Math.Min(s.AccomplishmentIds.Count,64);length+=1;for(int i=0;i<accomplishmentCount;i++)length+=EstimateString(s.AccomplishmentIds[i],RebirthSurvivorNetworkProtocol.MaxIdLength);
        int trialCount=Math.Min(s.CompletedTrialIds.Count,64);length+=1;for(int i=0;i<trialCount;i++)length+=EstimateString(s.CompletedTrialIds[i],RebirthSurvivorNetworkProtocol.MaxIdLength);

        int supportCount = Math.Min(s.SupportEntries.Count, RebirthSurvivorNetworkProtocol.MaxSupportEntries);
        for (int i = 0; i < supportCount; i++)
        {
            RebirthSurvivorOwnerSupportSnapshot entry = s.SupportEntries[i];
            length += EstimateString(entry != null ? entry.ProfileId : string.Empty, RebirthSurvivorNetworkProtocol.MaxIdLength) + 20;
        }
        int gearCount = Math.Min(s.GearSlots.Count, RebirthSurvivorNetworkProtocol.MaxGearSlots);
        for (int i = 0; i < gearCount; i++)
        {
            RebirthSurvivorOwnerGearSnapshot entry = s.GearSlots[i];
            length += EstimateString(entry != null ? entry.SlotId : string.Empty, RebirthSurvivorNetworkProtocol.MaxIdLength);
            length += EstimateString(entry != null ? entry.ItemId : string.Empty, RebirthSurvivorNetworkProtocol.MaxIdLength);
        }
        if(s.ProtocolVersion>=10) length+=EstimateString(RebirthImprovisationProgressPersistence.Write(s.ImprovisationProgress).ToString(System.Xml.Linq.SaveOptions.DisableFormatting),16384);
        return length + 40;
    }
    public static string Clean(string value, int maxLength)
    {
        string s = (value ?? string.Empty).Trim();
        if (s.Length > maxLength) s = s.Substring(0, maxLength);
        return s;
    }

    public static void WriteString(BinaryWriter writer, string value, int maxLength)
    {
        writer.Write(Clean(value, maxLength));
    }

    public static string ReadString(BinaryReader reader, int maxLength, RebirthSurvivorCreationNetworkRequest request, string field)
    {
        string value = reader.ReadString() ?? string.Empty;
        if (value.Length > maxLength && request != null)
        {
            request.Malformed = true;
            request.MalformedReason = "field-too-long:" + field;
        }
        return value.Length > maxLength ? value.Substring(0, maxLength) : value;
    }

    public static string ReadString(BinaryReader reader, int maxLength)
    {
        string value = reader.ReadString() ?? string.Empty;
        return value.Length > maxLength ? value.Substring(0, maxLength) : value;
    }

    public static void WriteCreationResult(BinaryWriter writer, RebirthSurvivorCreationResult result)
    {
        writer.Write(result != null);
        if (result == null) return;
        WriteString(writer, result.DefinitionHash, RebirthSurvivorNetworkProtocol.MaxHashLength);
        WriteString(writer, result.DefinitionVersion, RebirthSurvivorNetworkProtocol.MaxVersionLength);
        WriteString(writer, result.BackgroundId, RebirthSurvivorNetworkProtocol.MaxIdLength);
        WriteString(writer, result.DietId, RebirthSurvivorNetworkProtocol.MaxIdLength);

        int traitCount = Math.Min(result.TraitIds.Count, RebirthSurvivorNetworkProtocol.MaxTraitIds);
        writer.Write((byte)traitCount);
        for (int i = 0; i < traitCount; i++) WriteString(writer, result.TraitIds[i], RebirthSurvivorNetworkProtocol.MaxIdLength);
        writer.Write(result.RemainingCreationPoints);

        int attrCount = Math.Min(result.Attributes.Count, RebirthSurvivorNetworkProtocol.MaxAttributes);
        writer.Write((byte)attrCount);
        for (int i = 0; i < attrCount; i++)
        {
            RebirthResolvedAttributeStart a = result.Attributes[i];
            WriteString(writer, a.AttributeId, RebirthSurvivorNetworkProtocol.MaxIdLength);
            writer.Write(a.Current);
            writer.Write(a.Potential);
        }
        writer.Write(result.HealthPotential);
        writer.Write(result.UnencumberedSlotDelta);

        List<string> skillIds = new List<string>(result.StartingSkills.Keys);
        skillIds.Sort(StringComparer.Ordinal);
        int skillCount = Math.Min(skillIds.Count, RebirthSurvivorNetworkProtocol.MaxSkills);
        writer.Write((byte)skillCount);
        for (int i = 0; i < skillCount; i++)
        {
            string id = skillIds[i];
            WriteString(writer, id, RebirthSurvivorNetworkProtocol.MaxIdLength);
            writer.Write(result.StartingSkills[id]);
        }

        List<string> skillKnowledgeIds = new List<string>(result.StartingSkillKnowledge.Keys);
        skillKnowledgeIds.Sort(StringComparer.Ordinal);
        int skillKnowledgeCount = Math.Min(skillKnowledgeIds.Count, RebirthSurvivorNetworkProtocol.MaxSkillKnowledge);
        writer.Write((byte)skillKnowledgeCount);
        for (int i = 0; i < skillKnowledgeCount; i++)
        {
            string id = skillKnowledgeIds[i];
            WriteString(writer, id, RebirthSurvivorNetworkProtocol.MaxIdLength);
            writer.Write(result.StartingSkillKnowledge[id]);
        }

        int knowledgeCount = Math.Min(result.StartingKnowledgeIds.Count, RebirthSurvivorNetworkProtocol.MaxKnowledgeIds);
        writer.Write((ushort)knowledgeCount);
        for (int i = 0; i < knowledgeCount; i++) WriteString(writer, result.StartingKnowledgeIds[i], RebirthSurvivorNetworkProtocol.MaxIdLength);

        int errorCount = Math.Min(result.Errors.Count, RebirthSurvivorNetworkProtocol.MaxErrorEntries);
        writer.Write((byte)errorCount);
        for (int i = 0; i < errorCount; i++)
        {
            RebirthSurvivorCreationError e = result.Errors[i];
            writer.Write((ushort)e.Code);
            WriteString(writer, e.SubjectId, RebirthSurvivorNetworkProtocol.MaxIdLength);
            WriteString(writer, e.RelatedId, RebirthSurvivorNetworkProtocol.MaxIdLength);
        }
    }

    public static RebirthSurvivorCreationResult ReadCreationResult(BinaryReader reader)
    {
        if (!reader.ReadBoolean()) return null;
        string hash = ReadString(reader, RebirthSurvivorNetworkProtocol.MaxHashLength);
        string version = ReadString(reader, RebirthSurvivorNetworkProtocol.MaxVersionLength);
        string background = ReadString(reader, RebirthSurvivorNetworkProtocol.MaxIdLength);
        string diet = ReadString(reader, RebirthSurvivorNetworkProtocol.MaxIdLength);

        List<string> traits = new List<string>();
        int traitCount = reader.ReadByte();
        for (int i = 0; i < traitCount; i++)
        {
            string value = ReadString(reader, RebirthSurvivorNetworkProtocol.MaxIdLength);
            if (i < RebirthSurvivorNetworkProtocol.MaxTraitIds) traits.Add(value);
        }
        int points = reader.ReadInt32();

        List<RebirthResolvedAttributeStart> attrs = new List<RebirthResolvedAttributeStart>();
        int attrCount = reader.ReadByte();
        for (int i = 0; i < attrCount; i++)
        {
            string id = ReadString(reader, RebirthSurvivorNetworkProtocol.MaxIdLength);
            float current = reader.ReadSingle();
            float potential = reader.ReadSingle();
            if (i < RebirthSurvivorNetworkProtocol.MaxAttributes) attrs.Add(new RebirthResolvedAttributeStart(id, current, potential));
        }
        float healthPotential = reader.ReadSingle();
        int slotDelta = reader.ReadInt32();

        Dictionary<string, float> skills = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        int skillCount = reader.ReadByte();
        for (int i = 0; i < skillCount; i++)
        {
            string id = ReadString(reader, RebirthSurvivorNetworkProtocol.MaxIdLength);
            float value = reader.ReadSingle();
            if (i < RebirthSurvivorNetworkProtocol.MaxSkills && !skills.ContainsKey(id)) skills[id] = value;
        }

        Dictionary<string, float> skillKnowledge = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        int skillKnowledgeCount = reader.ReadByte();
        for (int i = 0; i < skillKnowledgeCount; i++)
        {
            string id = ReadString(reader, RebirthSurvivorNetworkProtocol.MaxIdLength);
            float value = reader.ReadSingle();
            if (i < RebirthSurvivorNetworkProtocol.MaxSkillKnowledge && !skillKnowledge.ContainsKey(id)) skillKnowledge[id] = value;
        }

        List<string> knowledge = new List<string>();
        int knowledgeCount = reader.ReadUInt16();
        for (int i = 0; i < knowledgeCount; i++)
        {
            string id = ReadString(reader, RebirthSurvivorNetworkProtocol.MaxIdLength);
            if (i < RebirthSurvivorNetworkProtocol.MaxKnowledgeIds) knowledge.Add(id);
        }

        List<RebirthSurvivorCreationError> errors = new List<RebirthSurvivorCreationError>();
        int errorCount = reader.ReadByte();
        for (int i = 0; i < errorCount; i++)
        {
            RebirthSurvivorCreationErrorCode code = (RebirthSurvivorCreationErrorCode)reader.ReadUInt16();
            string subject = ReadString(reader, RebirthSurvivorNetworkProtocol.MaxIdLength);
            string related = ReadString(reader, RebirthSurvivorNetworkProtocol.MaxIdLength);
            if (i < RebirthSurvivorNetworkProtocol.MaxErrorEntries) errors.Add(new RebirthSurvivorCreationError(code, subject, related));
        }

        return new RebirthSurvivorCreationResult(hash, version, background, diet, traits, points, attrs,
            healthPotential, slotDelta, skills, skillKnowledge, knowledge, errors);
    }

    public static void WriteOwnerState(BinaryWriter writer, RebirthSurvivorOwnerStateSnapshot s)
    {
        if (s == null) s = new RebirthSurvivorOwnerStateSnapshot();
        writer.Write(s.ProtocolVersion);
        writer.Write(s.RebirthModeEnabled);
        writer.Write(s.HasCharacter);
        writer.Write((byte)s.CreationState);
        writer.Write(s.CharacterRevision);
        if(s.ProtocolVersion >= 12) writer.Write(s.GearRevision);
        WriteString(writer, s.CreationId, RebirthSurvivorNetworkProtocol.MaxIdLength);
        WriteString(writer, s.ServerDefinitionHash, RebirthSurvivorNetworkProtocol.MaxHashLength);
        WriteString(writer, s.ServerDefinitionVersion, RebirthSurvivorNetworkProtocol.MaxVersionLength);
        WriteString(writer, s.OriginDefinitionHash, RebirthSurvivorNetworkProtocol.MaxHashLength);
        WriteString(writer, s.OriginDefinitionVersion, RebirthSurvivorNetworkProtocol.MaxVersionLength);
        WriteString(writer, s.SourceProfileName, RebirthSurvivorNetworkProtocol.MaxProfileNameLength);
        WriteString(writer, s.BackgroundId, RebirthSurvivorNetworkProtocol.MaxIdLength);
        WriteString(writer, s.DietId, RebirthSurvivorNetworkProtocol.MaxIdLength);

        int traitCount = Math.Min(s.TraitIds.Count, RebirthSurvivorNetworkProtocol.MaxTraitIds);
        writer.Write((byte)traitCount);
        for (int i = 0; i < traitCount; i++) WriteString(writer, s.TraitIds[i], RebirthSurvivorNetworkProtocol.MaxIdLength);

        writer.Write(s.HealthPotential);
        writer.Write(s.MoodCurrent);
        writer.Write(s.MoodTarget);
        writer.Write(s.DietSatisfaction);
        writer.Write(s.HealthCapacity);
        writer.Write(s.RecentMealCount);
        writer.Write(s.RecentVarietyCount);
        writer.Write(s.LastMealCompatible);
        WriteString(writer, s.MoodPositiveCauseId, RebirthSurvivorNetworkProtocol.MaxIdLength);
        writer.Write(s.MoodPositiveCauseDelta);
        WriteString(writer, s.MoodNegativeCauseId, RebirthSurvivorNetworkProtocol.MaxIdLength);
        writer.Write(s.MoodNegativeCauseDelta);

        int attrCount = Math.Min(s.Attributes.Count, RebirthSurvivorNetworkProtocol.MaxAttributes);
        writer.Write((byte)attrCount);
        for (int i = 0; i < attrCount; i++)
        {
            RebirthSurvivorOwnerAttributeSnapshot a = s.Attributes[i];
            WriteString(writer, a != null ? a.Id : string.Empty, RebirthSurvivorNetworkProtocol.MaxIdLength);
            writer.Write(a != null ? a.Current : 0f);
            writer.Write(a != null ? a.Potential : 0f);
        }

        int skillCount = Math.Min(s.Skills.Count, RebirthSurvivorNetworkProtocol.MaxSkills);
        writer.Write((byte)skillCount);
        for (int i = 0; i < skillCount; i++)
        {
            RebirthSurvivorOwnerSkillSnapshot skill = s.Skills[i];
            WriteString(writer, skill != null ? skill.Id : string.Empty, RebirthSurvivorNetworkProtocol.MaxIdLength);
            writer.Write(skill != null ? skill.Value : 0f);
            writer.Write(skill != null ? skill.Progress : 0f);
        }

        int skillKnowledgeCount = Math.Min(s.SkillKnowledge.Count, RebirthSurvivorNetworkProtocol.MaxSkillKnowledge);
        writer.Write((byte)skillKnowledgeCount);
        for (int i = 0; i < skillKnowledgeCount; i++)
        {
            RebirthSurvivorOwnerSkillKnowledgeSnapshot knowledge = s.SkillKnowledge[i];
            WriteString(writer, knowledge != null ? knowledge.Id : string.Empty, RebirthSurvivorNetworkProtocol.MaxIdLength);
            writer.Write(knowledge != null ? knowledge.Value : 0f);
        }

        int knowledgeCount = Math.Min(s.KnowledgeIds.Count, RebirthSurvivorNetworkProtocol.MaxKnowledgeIds);
        writer.Write((ushort)knowledgeCount);
        for (int i = 0; i < knowledgeCount; i++) WriteString(writer, s.KnowledgeIds[i], RebirthSurvivorNetworkProtocol.MaxIdLength);

        int disciplineCount = Math.Min(s.AcquiredDisciplineIds.Count, RebirthSurvivorNetworkProtocol.MaxDisciplineIds);
        writer.Write((byte)disciplineCount);
        for(int i=0;i<disciplineCount;i++)WriteString(writer,s.AcquiredDisciplineIds[i],RebirthSurvivorNetworkProtocol.MaxIdLength);
        int accomplishmentCount=Math.Min(s.AccomplishmentIds.Count,64);writer.Write((byte)accomplishmentCount);for(int i=0;i<accomplishmentCount;i++)WriteString(writer,s.AccomplishmentIds[i],RebirthSurvivorNetworkProtocol.MaxIdLength);
        int trialCount=Math.Min(s.CompletedTrialIds.Count,64);writer.Write((byte)trialCount);for(int i=0;i<trialCount;i++)WriteString(writer,s.CompletedTrialIds[i],RebirthSurvivorNetworkProtocol.MaxIdLength);

        int supportCount = Math.Min(s.SupportEntries.Count, RebirthSurvivorNetworkProtocol.MaxSupportEntries);
        writer.Write((byte)supportCount);
        for (int i = 0; i < supportCount; i++)
        {
            RebirthSurvivorOwnerSupportSnapshot entry = s.SupportEntries[i];
            WriteString(writer, entry != null ? entry.ProfileId : string.Empty, RebirthSurvivorNetworkProtocol.MaxIdLength);
            writer.Write(entry != null ? entry.GraceRemainingActiveSeconds : 0f);
            writer.Write(entry != null ? entry.ManagedRemainingActiveSeconds : 0f);
            writer.Write(entry != null ? entry.PositiveRemainingActiveSeconds : 0f);
            writer.Write(entry != null ? entry.CooldownRemainingActiveSeconds : 0f);
            writer.Write(entry != null ? entry.Stacks : 0);
        }
        int gearCount = Math.Min(s.GearSlots.Count, RebirthSurvivorNetworkProtocol.MaxGearSlots);
        writer.Write((byte)gearCount);
        for (int i = 0; i < gearCount; i++)
        {
            RebirthSurvivorOwnerGearSnapshot entry = s.GearSlots[i];
            WriteString(writer, entry != null ? entry.SlotId : string.Empty, RebirthSurvivorNetworkProtocol.MaxIdLength);
            WriteString(writer, entry != null ? entry.ItemId : string.Empty, RebirthSurvivorNetworkProtocol.MaxIdLength);
        }
        writer.Write(s.PhysicalBagSlots);
        if(s.ProtocolVersion>=10) WriteString(writer,RebirthImprovisationProgressPersistence.Write(s.ImprovisationProgress).ToString(System.Xml.Linq.SaveOptions.DisableFormatting),16384);
    }

    public static RebirthSurvivorOwnerStateSnapshot ReadOwnerState(BinaryReader reader)
    {
        RebirthSurvivorOwnerStateSnapshot s = new RebirthSurvivorOwnerStateSnapshot();
        s.ProtocolVersion = reader.ReadInt32();
        if(s.ProtocolVersion != 11 && s.ProtocolVersion != 12) throw new InvalidDataException("Unsupported owner snapshot layout");
        s.RebirthModeEnabled = reader.ReadBoolean();
        s.HasCharacter = reader.ReadBoolean();
        s.CreationState = (RebirthSurvivorOwnerCreationState)reader.ReadByte();
        if (s.CreationState < RebirthSurvivorOwnerCreationState.NotApplicable || s.CreationState > RebirthSurvivorOwnerCreationState.RecoveryRequired)
            s.CreationState = RebirthSurvivorOwnerCreationState.NotApplicable;
        s.CharacterRevision = reader.ReadInt64();
        s.GearRevision = s.ProtocolVersion >= 12 ? reader.ReadInt64() : -1;
        s.CreationId = ReadString(reader, RebirthSurvivorNetworkProtocol.MaxIdLength);
        s.ServerDefinitionHash = ReadString(reader, RebirthSurvivorNetworkProtocol.MaxHashLength);
        s.ServerDefinitionVersion = ReadString(reader, RebirthSurvivorNetworkProtocol.MaxVersionLength);
        s.OriginDefinitionHash = ReadString(reader, RebirthSurvivorNetworkProtocol.MaxHashLength);
        s.OriginDefinitionVersion = ReadString(reader, RebirthSurvivorNetworkProtocol.MaxVersionLength);
        s.SourceProfileName = ReadString(reader, RebirthSurvivorNetworkProtocol.MaxProfileNameLength);
        s.BackgroundId = ReadString(reader, RebirthSurvivorNetworkProtocol.MaxIdLength);
        s.DietId = ReadString(reader, RebirthSurvivorNetworkProtocol.MaxIdLength);

        int traitCount = reader.ReadByte();
        for (int i = 0; i < traitCount; i++)
        {
            string id = ReadString(reader, RebirthSurvivorNetworkProtocol.MaxIdLength);
            if (i < RebirthSurvivorNetworkProtocol.MaxTraitIds) s.TraitIds.Add(id);
        }

        s.HealthPotential = reader.ReadSingle();
        s.MoodCurrent = reader.ReadSingle();
        s.MoodTarget = reader.ReadSingle();
        s.DietSatisfaction = reader.ReadSingle();
        s.HealthCapacity = reader.ReadSingle();
        s.RecentMealCount = Math.Max(0, reader.ReadInt32());
        s.RecentVarietyCount = Math.Max(0, reader.ReadInt32());
        s.LastMealCompatible = reader.ReadBoolean();
        s.MoodPositiveCauseId = ReadString(reader, RebirthSurvivorNetworkProtocol.MaxIdLength);
        s.MoodPositiveCauseDelta = reader.ReadSingle();
        s.MoodNegativeCauseId = ReadString(reader, RebirthSurvivorNetworkProtocol.MaxIdLength);
        s.MoodNegativeCauseDelta = reader.ReadSingle();

        int attrCount = reader.ReadByte();
        for (int i = 0; i < attrCount; i++)
        {
            RebirthSurvivorOwnerAttributeSnapshot a = new RebirthSurvivorOwnerAttributeSnapshot();
            a.Id = ReadString(reader, RebirthSurvivorNetworkProtocol.MaxIdLength);
            a.Current = reader.ReadSingle();
            a.Potential = reader.ReadSingle();
            if (i < RebirthSurvivorNetworkProtocol.MaxAttributes) s.Attributes.Add(a);
        }

        int skillCount = reader.ReadByte();
        for (int i = 0; i < skillCount; i++)
        {
            RebirthSurvivorOwnerSkillSnapshot skill = new RebirthSurvivorOwnerSkillSnapshot();
            skill.Id = ReadString(reader, RebirthSurvivorNetworkProtocol.MaxIdLength);
            skill.Value = reader.ReadSingle();
            skill.Progress = reader.ReadSingle();
            if (i < RebirthSurvivorNetworkProtocol.MaxSkills) s.Skills.Add(skill);
        }

        int skillKnowledgeCount = reader.ReadByte();
        for (int i = 0; i < skillKnowledgeCount; i++)
        {
            RebirthSurvivorOwnerSkillKnowledgeSnapshot knowledge = new RebirthSurvivorOwnerSkillKnowledgeSnapshot();
            knowledge.Id = ReadString(reader, RebirthSurvivorNetworkProtocol.MaxIdLength);
            knowledge.Value = reader.ReadSingle();
            if (i < RebirthSurvivorNetworkProtocol.MaxSkillKnowledge) s.SkillKnowledge.Add(knowledge);
        }

        int knowledgeCount = reader.ReadUInt16();
        for (int i = 0; i < knowledgeCount; i++)
        {
            string id = ReadString(reader, RebirthSurvivorNetworkProtocol.MaxIdLength);
            if (i < RebirthSurvivorNetworkProtocol.MaxKnowledgeIds) s.KnowledgeIds.Add(id);
        }

        int disciplineCount=reader.ReadByte();
        for(int i=0;i<disciplineCount;i++){string id=ReadString(reader,RebirthSurvivorNetworkProtocol.MaxIdLength);if(i<RebirthSurvivorNetworkProtocol.MaxDisciplineIds)s.AcquiredDisciplineIds.Add(id);}
        int accomplishmentCount=reader.ReadByte();for(int i=0;i<accomplishmentCount;i++){string id=ReadString(reader,RebirthSurvivorNetworkProtocol.MaxIdLength);if(i<64)s.AccomplishmentIds.Add(id);}
        int trialCount=reader.ReadByte();for(int i=0;i<trialCount;i++){string id=ReadString(reader,RebirthSurvivorNetworkProtocol.MaxIdLength);if(i<64)s.CompletedTrialIds.Add(id);}

        int supportCount = reader.ReadByte();
        for (int i = 0; i < supportCount; i++)
        {
            RebirthSurvivorOwnerSupportSnapshot entry = new RebirthSurvivorOwnerSupportSnapshot();
            entry.ProfileId = ReadString(reader, RebirthSurvivorNetworkProtocol.MaxIdLength);
            entry.GraceRemainingActiveSeconds = reader.ReadSingle();
            entry.ManagedRemainingActiveSeconds = reader.ReadSingle();
            entry.PositiveRemainingActiveSeconds = reader.ReadSingle();
            entry.CooldownRemainingActiveSeconds = reader.ReadSingle();
            entry.Stacks = reader.ReadInt32();
            if (i < RebirthSurvivorNetworkProtocol.MaxSupportEntries) s.SupportEntries.Add(entry);
        }
        int gearCount = reader.ReadByte();
        for (int i = 0; i < gearCount; i++)
        {
            RebirthSurvivorOwnerGearSnapshot entry = new RebirthSurvivorOwnerGearSnapshot();
            entry.SlotId = ReadString(reader, RebirthSurvivorNetworkProtocol.MaxIdLength);
            entry.ItemId = ReadString(reader, RebirthSurvivorNetworkProtocol.MaxIdLength);
            if (i < RebirthSurvivorNetworkProtocol.MaxGearSlots) s.GearSlots.Add(entry);
        }
        s.PhysicalBagSlots = reader.ReadInt32();
        if(s.ProtocolVersion>=10)
        {
            string xml=reader.ReadString();
            if(xml.Length>16384)throw new System.IO.InvalidDataException("Owner improvisation section too large");
            System.Xml.Linq.XElement node;
            using(var text=new System.IO.StringReader(xml))
            using(var xmlReader=System.Xml.XmlReader.Create(text,new System.Xml.XmlReaderSettings{DtdProcessing=System.Xml.DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=16384}))
                node=System.Xml.Linq.XElement.Load(xmlReader);
            if(node.Name!="improvisation")throw new System.IO.InvalidDataException("Invalid owner improvisation section");
            Dictionary<string,float> values;string error;
            if(!RebirthImprovisationProgressPersistence.TryRead(new System.Xml.Linq.XElement("progression",node),out values,out error))
                throw new System.IO.InvalidDataException(error);
            foreach(var pair in values)s.ImprovisationProgress[pair.Key]=pair.Value;
        }
        return s;
    }
}
