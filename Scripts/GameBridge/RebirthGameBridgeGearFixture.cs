using Newtonsoft.Json.Linq;

// Test preparation only. Equipping and removal are still exercised with real UI input.
public static class RebirthGameBridgeGearFixture
{
    public static void Setup(BridgeRequest req)
    {
        var player = RebirthGameBridgeCombat.PlayerFor(req);
        if (player == null) return;
        if (req.Method != "POST" || GamePrefs.GetString(EnumGamePrefs.GameName) != "CodexTest" ||
            req.QueryString("confirm") != "CodexTest" || player.world.IsRemote())
        { req.Fail("Gear fixture setup requires local disposable CodexTest and explicit confirmation.", 403); return; }
        RebirthStablePlayerIdentity identity; RebirthWorldCharacterRecord record;
        if (!RebirthSkillAwardService.TryGetEligible(player, out identity, out record))
        { req.Fail("No eligible test character.", 409); return; }
        string background = req.QueryString("background");
        if (!string.IsNullOrEmpty(background))
        {
            RebirthBackgroundDefinition definition;
            if (!RebirthSurvivorDefinitionRegistry.TryGetBackground(background, out definition))
            { req.Fail("Unknown fixture background.", 400); return; }
            string previous = record.Origin.BackgroundId;
            // Disposable fixture only; ordinary gameplay never rewrites immutable origin.
            typeof(RebirthWorldOriginSnapshot).GetProperty("BackgroundId").SetValue(record.Origin, background, null);
            record.Touch("bridge-disposable-background-fixture");
            RebirthSurvivorNetworkService.SendOwnerState(player, 0L, true, "bridge-disposable-background-fixture");
            req.Complete(new JObject { ["fixtureOnly"] = true, ["before"] = previous, ["after"] = background });
            return;
        }
        string id = req.QueryString("attribute");
        RebirthAttributeRuntimeState attribute;
        if ((id != "strength" && id != "constitution") || !record.Progression.Attributes.TryGetValue(id, out attribute))
        { req.Fail("Only strength and constitution are gear-fixture attributes.", 400); return; }
        float value = req.QueryFloat("value", attribute.Current);
        float potential = req.QueryFloat("potential", attribute.Potential);
        if (float.IsNaN(value) || float.IsInfinity(value) || float.IsNaN(potential) || float.IsInfinity(potential) ||
            value < -100 || value > 100 || potential < value || potential > 100)
        { req.Fail("Invalid attribute fixture bounds.", 400); return; }
        var before = JObject.FromObject(attribute);
        attribute.Current = value; attribute.Potential = potential;
        record.Touch("bridge-disposable-gear-fixture");
        RebirthWorldCharacterRepository.SaveIfDirty(identity, "bridge-disposable-gear-fixture");
        RebirthSurvivorNetworkService.SendOwnerState(player, 0L, true, "bridge-disposable-gear-fixture");
        req.Complete(new JObject { ["fixtureOnly"] = true, ["before"] = before, ["after"] = JObject.FromObject(attribute) });
    }
}
