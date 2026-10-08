using System;using System.IO;using System.Linq;using System.Collections.Generic;using System.Globalization;using System.Xml.Linq;
internal static partial class RebirthWorldCharacterRepository {
    private static bool TryLoadValidatedRecord(string candidatePath, RebirthStablePlayerIdentity identity,
        out RebirthWorldCharacterRecord record, out bool migrated, out string error, out bool hasPendingCustody)
    {
        record = null;
        migrated = false;
        error = string.Empty;
        hasPendingCustody = false;
        XDocument doc;
        if (!RebirthAtomicXmlFile.TryLoad(candidatePath, out doc, out error))
            return false;
        hasPendingCustody = HasPendingItemCustody(doc);
        string migrationError;
        if (!RebirthWorldCharacterMigrationRegistry.TryMigrateToCurrent(doc, out migrated, out migrationError))
        {
            error = migrationError;
            return false;
        }
        string parseError;
        if (!TryDeserialize(doc, out record, out parseError))
        {
            error = parseError;
            record = null;
            return false;
        }
        if (!string.Equals(record.StablePlayerKey, identity.StorageKey, StringComparison.Ordinal) ||
            !string.Equals(record.StablePlayerId, identity.CanonicalId, StringComparison.Ordinal))
        {
            error = "stable player identity inside record does not match server-derived identity";
            record = null;
            return false;
        }
        if (!record.IsComplete)
        {
            error = "record does not contain a complete committed origin";
            record = null;
            return false;
        }
        return true;
    }
    private static bool TryDeserialize(XDocument doc, out RebirthWorldCharacterRecord record, out string error)
    {
        record = null; error = string.Empty;
        try
        {
            XElement root = doc != null ? doc.Root : null;
            if (root == null || root.Name != "rebirthWorldCharacter") { error = "unexpected world-character root"; return false; }
            int schema; long revision; DateTime created; DateTime modified;
            if (!int.TryParse(A(root,"schemaVersion"), NumberStyles.Integer, CultureInfo.InvariantCulture, out schema)) { error = "invalid schemaVersion"; return false; }
            if (!long.TryParse(A(root,"revision"), NumberStyles.Integer, CultureInfo.InvariantCulture, out revision)) { error = "invalid revision"; return false; }
            if (!DateTime.TryParse(A(root,"createdAtUtc"), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out created)) { error = "invalid createdAtUtc"; return false; }
            if (!DateTime.TryParse(A(root,"modifiedAtUtc"), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out modified)) { error = "invalid modifiedAtUtc"; return false; }
            int migrationSource=schema, migrationTarget=schema; bool migrationApplied=false;
            string migrationSourceText=A(root,"migrationSourceSchema"), migrationTargetText=A(root,"migrationTargetSchema"), migrationAppliedText=A(root,"migrationApplied");
            if(migrationSourceText.Length>0&&!int.TryParse(migrationSourceText,NumberStyles.Integer,CultureInfo.InvariantCulture,out migrationSource)){error="invalid migrationSourceSchema";return false;}
            if(migrationTargetText.Length>0&&!int.TryParse(migrationTargetText,NumberStyles.Integer,CultureInfo.InvariantCulture,out migrationTarget)){error="invalid migrationTargetSchema";return false;}
            if(migrationAppliedText.Length>0&&!bool.TryParse(migrationAppliedText,out migrationApplied)){error="invalid migrationApplied";return false;}
            if(migrationSource<1||migrationTarget<1||migrationTarget!=schema){error="invalid migration metadata source="+migrationSource+" target="+migrationTarget+" schema="+schema;return false;}
            RebirthWorldOriginSnapshot origin; if (!TryDeserializeOrigin(root.Element("origin"), out origin, out error)) return false;
            RebirthWorldProgressionState progression; if (!TryDeserializeProgression(root.Element("progression"), A(root,"stablePlayerKey"), out progression, out error)) return false;
            if(!RebirthStationDiscoveryAdmissionPersistence.TryRead(root.Element("progression"),A(root,"stablePlayerKey"),origin.CreationId,out var discoveryAdmissions,out error))return false;
            foreach(var pair in discoveryAdmissions)progression.StationDiscoveryAdmissions.Add(pair.Key,pair.Value);
            if(!RebirthStationRecipeDiscoveryPersistence.MatchesCreation(progression.RecipeDiscoveries.Values,origin.CreationId)){error="Recipe discovery character mismatch";return false;}
            if(!RebirthTheoryStudyPersistence.MatchesOwner(progression.PendingTheoryStudy,origin.CreationId)){error="Theory study owner mismatch";return false;}
            if(!RebirthStationPreparationPersistence.MatchesOwner(progression.StationPreparations,origin.CreationId)){error="Station preparation owner mismatch";return false;}
            if(progression.StationRefundArchives.Values.Any(a=>a==null||!RebirthSurvivorRequestScope.Matches(a.CreationId,origin.CreationId))){error="Station refund archive character mismatch";return false;}
            if(!RebirthTheorySoloPersistence.MatchesOwner(progression.SoloTheory,origin.CreationId)){error="Solo Theory owner mismatch";return false;}
            RebirthWorldConditionState condition; if (!TryDeserializeCondition(root.Element("condition"), out condition, out error)) return false;
            RebirthWorldSupportState support; if (!TryDeserializeSupport(root.Element("support"),A(root,"stablePlayerKey"),origin.CreationId, out support, out error)) return false;
            record = new RebirthWorldCharacterRecord(schema, A(root,"stablePlayerId"), A(root,"stablePlayerKey"), revision, created, modified, origin, progression, condition, support,
                migrationSource,migrationTarget,A(root,"migrationPolicyId"),migrationApplied,A(root,"migrationOriginAudit"),A(root,"migrationProgressionAudit"));
            if (record.SchemaVersion != RebirthWorldCharacterRecord.CurrentSchemaVersion) { error = "world-character schema did not migrate to current"; record = null; return false; }
            return true;
        }
        catch (Exception ex) { error = ex.GetType().Name + ": " + ex.Message; record = null; return false; }
    }
}