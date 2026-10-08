using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json.Linq;

#nullable disable

/// <summary>
/// Endpoint implementations for the REBIRTH Game Bridge. Everything here runs on the Unity main thread.
/// </summary>
public static class RebirthGameBridgeHandlers
{
    private static readonly string[] HelpLines =
    {
        "GET  /ping                                   liveness + state (answered off main thread)",
        "GET  /help                                   this list",
        "GET  /state?sections=player,stats,buffs,cvars,inventory,equipment,skills,world,ui",
        "POST /console?cmd=...  (or body: one command per line)   run console commands; returns output + log lines they produced",
        "GET  /log?since=SEQ&level=log|warning|error&limit=200&contains=text",
        "GET  /cvar?names=a,b     POST /cvar?name=a&value=1.5",
        "POST /screenshot?name=label&maxWidth=1280    PNG saved under Tools/GameBridge/out/screenshots",
        "GET  /ui                 POST /ui?action=open|close&window=NAME",
        "GET  /entities?radius=40&limit=100&contains=zombie",
        "POST /look?yaw=DEG&pitch=DEG                 rotate the local player/camera",
        "POST /quit                                   quit the game (clean shutdown)",
        "--- playing (virtual keyboard/mouse through the game's own input actions) ---",
        "GET  /target                                 what's under the crosshair (block + activation prompt, or entity)",
        "GET  /findblocks?name=campfire&radius=24     nearest matching blocks (name or display name)",
        "POST /lookat?x&y&z[&block=1] | ?entity=ID | ?yaw&pitch",
        "POST /walkto?x&z[&y][&run=1][&radius=1.5][&maxSeconds=60]   walk with real movement input, auto-unstick",
        "POST /move?forward=1&strafe=0&seconds=1[&run][&jump][&crouch]",
        "POST /activate[?x&y&z&block=1|?entity=ID][&hold=SECONDS]  press E (optionally aim first)",
        "POST /press?action=Primary|Secondary|Jump|Reload|Inventory|gui.Cancel...[&seconds=][&hold=1]",
        "POST /release[?action=]   POST /stop        release held inputs / cancel walking",
        "GET  /actions                                all input action names",
        "POST /select?slot=1..10                      toolbelt slot via its hotkey",
        "POST /damage?amount=10&type=Bashing          hurt yourself (any EnumDamageTypes)",
        "POST /key?name=Escape|Tab|E|Space|...[&seconds=]  press a physical key (every action bound to it)",
        "POST /fight?[entity=ID|radius=30&contains=zombie][&range=2.1][&power=1][&retreatStamina=15][&resumeStamina=45][&fleeHealth=0][&maxSeconds=60]",
        "     in-game reflex loop: faces the target every frame, closes in, swings, backs off to recover stamina",
        "POST /flee?[entity=ID][&distance=20][&maxSeconds=20]   run away from an enemy",
        "GET  /guard   POST /guard?enabled=0|1&distance=6   instincts: fight back/flee when an enemy gets close, heal when hurt and clear, reload when quiet (&heal=0|1&reload=0|1)",
        "POST /give?item=NAME[&count=1][&quality=1][&toolbelt=1..10]   test setup: put an item in a toolbelt slot or the backpack",
        "POST /spawn?entity=zombieArlene[&distance=8][&angle=0]        test setup: spawn an entity in front of the player",
        "POST /restore                                test setup: full health and stamina",
        "POST /cleararea?radius=40                    test setup: kill enemies around the player (known starting state)",
        "GET  /surroundings?radius=30                 enemies around: distance, side, awake, approaching",
        "--- UI ---",
        "GET  /ui/tree[?window=ID][&all=1]            visible nodes: text, items, recipes, queue, clickables, rect=[x,y,w,h] (screenshot px)",
        "GET  /ui/find?text=|id=|item=|recipe=|ctrl=|path=[&clickable=1]",
        "POST /ui/click?<selector>|x&y[&scale=][&button=right][&shift=1][&ctrl=1][&double=1][&clicks=N]",
        "POST /ui/hover?<selector>|x&y                hover and read the tooltip",
        "POST /ui/drag?from.<selector>&to.<selector>  press, drag, release",
        "POST /ui/type?<selector>&value=TEXT[&submit=1]  set a text input",
        "POST /ui/scroll?<selector>&delta=-1"
    };

    public static void Dispatch(BridgeRequest req)
    {
        switch (req.Path)
        {
            case "/":
            case "/help": req.Complete(new JObject { ["endpoints"] = new JArray(HelpLines) }); return;
            case "/gearfixture": RebirthGameBridgeGearFixture.Setup(req); return;
            case "/state": State(req); return;
            case "/console": Console(req); return;
            case "/log": LogQuery(req); return;
            case "/cvar": CVar(req); return;
            case "/screenshot": Screenshot(req); return;
            case "/ui": Ui(req); return;
            case "/entities": Entities(req); return;
            case "/quit": Quit(req); return;
            default:
                if (RebirthGameBridgePlayer.TryDispatch(req) || RebirthGameBridgeUi.TryDispatch(req) || RebirthGameBridgeCombat.TryDispatch(req) || RebirthGameBridgeWorld.TryDispatch(req)) return;
                req.Fail("unknown endpoint " + req.Path + " (see /help)", 404);
                return;
        }
    }

    public static string DescribeGameState()
    {
        GameManager gm = GameManager.Instance;
        if (gm == null) return "starting";
        if (gm.World == null) return gm.IsStartingGame ? "loading" : "menu";
        if (gm.gameStateManager == null || !gm.gameStateManager.IsGameStarted()) return "loading";
        EntityPlayerLocal p = gm.World.GetPrimaryPlayer();
        if (p == null) return GameManager.IsDedicatedServer ? "server" : "loading";
        if (!p.IsSpawned()) return "spawning";
        if (p.IsDead()) return "dead";
        return "ingame";
    }

    private static EntityPlayerLocal Player()
    {
        GameManager gm = GameManager.Instance;
        return gm != null && gm.World != null ? gm.World.GetPrimaryPlayer() : null;
    }

    private static bool RequirePlayer(BridgeRequest req, out EntityPlayerLocal player)
    {
        player = Player();
        if (player != null) return true;
        req.Fail("no local player (state=" + DescribeGameState() + ")", 409);
        return false;
    }

    // ---------------------------------------------------------------- /state

    private static void State(BridgeRequest req)
    {
        string sectionsArg = req.QueryString("sections", "player,stats,buffs,cvars,inventory,equipment,skills,world,ui");
        var sections = new HashSet<string>(sectionsArg.Split(','), StringComparer.OrdinalIgnoreCase);
        var o = new JObject { ["state"] = DescribeGameState(), ["lastLogSeq"] = RebirthGameBridgeLog.LastSeq, ["guard"] = RebirthGameBridgeCombat.GuardStatus() };

        GameManager gm = GameManager.Instance;
        World world = gm != null ? gm.World : null;
        EntityPlayerLocal p = Player();
        // Explicit read-only observations; never apply options during acceptance reads.
        if (sections.Contains("combatOptions"))
        {
            var manager = RebirthSandboxOptionManager.Current;
            var mode = RebirthAlwaysStaggerRuntimePolicy.Mode;
            o["combatOptions"] = new JObject
            {
                ["observationVersion"] = 1,
                ["sampleTime"] = Time.realtimeSinceStartup,
                ["effectiveAlwaysStagger"] = mode.ToString(),
                ["effectiveAlwaysStaggerValue"] = (int)mode,
                ["configuredAlwaysStagger"] = manager != null ? (JToken)new JValue(manager.AlwaysStagger.ToString()) : JValue.CreateNull(),
                ["configuredAlwaysStaggerValue"] = manager != null ? (JToken)new JValue((int)manager.AlwaysStagger) : JValue.CreateNull(),
                ["configuredHybridPathSmoothing"] = manager != null ? (JToken)new JValue(manager.HybridPathSmoothing) : JValue.CreateNull(),
                ["effectiveHybridPathSmoothing"] = RebirthHybridPathSmoothing.Enabled,
                ["scope"] = "local_process_read_only_not_network_authority"
            };
        }

        // Read the game's existing five-second counter; no injected profiler or per-frame
        // tracking is needed for a low-overhead steady-state comparison.
        if (gm != null && sections.Contains("performance"))
            o["performance"] = new JObject { ["fps"] = gm.fps.Counter, ["sampleWindowSeconds"] = 5,
                ["width"] = Screen.width, ["height"] = Screen.height,
                ["targetFrameRate"] = Application.targetFrameRate, ["vSyncCount"] = QualitySettings.vSyncCount,
                ["focused"] = Application.isFocused, ["runInBackground"] = Application.runInBackground };

        // Explicit, read-only diagnostics: never enumerate cameras in the normal frame loop.
        // This distinguishes UI/preview cameras left active after closing a window from FPS caps.
        if (sections.Contains("rendering"))
        {
            var cameras = new JArray();
            foreach (Camera camera in Camera.allCameras)
            {
                RenderTexture target = camera.targetTexture;
                cameras.Add(new JObject { ["name"] = camera.name, ["depth"] = camera.depth,
                    ["targetWidth"] = target != null ? target.width : 0,
                    ["targetHeight"] = target != null ? target.height : 0 });
            }
            o["rendering"] = new JObject { ["activeCameras"] = cameras,
                ["managedBytes"] = GC.GetTotalMemory(false),
                ["collections0"] = GC.CollectionCount(0), ["collections2"] = GC.CollectionCount(2) };
        }

        if (world != null && sections.Contains("world"))
        {
            ulong t = world.worldTime;
            o["world"] = new JObject
            {
                ["worldTime"] = (long)t,
                ["day"] = GameUtils.WorldTimeToDays(t),
                ["hour"] = GameUtils.WorldTimeToHours(t),
                ["minute"] = GameUtils.WorldTimeToMinutes(t),
                ["isDaytime"] = world.IsDaytime(),
                ["gameWorld"] = GamePrefs.GetString(EnumGamePrefs.GameWorld),
                ["gameName"] = GamePrefs.GetString(EnumGamePrefs.GameName),
                ["paused"] = gm.IsPaused()
            };
        }

        if (p != null)
        {
            if (sections.Contains("player"))
            {
                o["player"] = new JObject
                {
                    ["entityId"] = p.entityId,
                    ["name"] = p.EntityName,
                    ["position"] = Vec(p.position),
                    ["rotation"] = Vec(p.rotation),
                    ["dead"] = p.IsDead(),
                    ["level"] = p.Progression != null ? p.Progression.Level : 0,
                    ["skillPoints"] = p.Progression != null ? p.Progression.SkillPoints : 0,
                    ["holdingSlot"] = p.inventory != null ? p.inventory.holdingItemIdx : -1,
                    ["holding"] = p.inventory != null ? ItemJson(p.inventory.GetItem(p.inventory.holdingItemIdx)) : null
                };
            }
            if (sections.Contains("stats") && p.Stats != null)
            {
                o["stats"] = new JObject
                {
                    ["health"] = StatJson(p.Stats.Health),
                    ["stamina"] = StatJson(p.Stats.Stamina),
                    ["food"] = StatJson(p.Stats.Food),
                    ["water"] = StatJson(p.Stats.Water)
                };
            }
            if (sections.Contains("buffs") && p.Buffs != null)
            {
                var arr = new JArray();
                foreach (BuffValue b in p.Buffs.ActiveBuffs)
                {
                    if (b == null || b.Invalid || b.Remove) continue;
                    BuffClass bc = b.BuffClass;
                    arr.Add(new JObject
                    {
                        ["name"] = b.BuffName,
                        ["elapsed"] = Round(b.DurationInSeconds),
                        ["durationMax"] = bc != null ? Round(bc.DurationMax) : 0f,
                        ["paused"] = b.Paused,
                        ["stacks"] = b.StackEffectMultiplier
                    });
                }
                o["buffs"] = arr;
            }
            if (sections.Contains("cvars") && p.Buffs != null && p.Buffs.CVars != null)
            {
                var cv = new JObject();
                var keys = new List<string>(p.Buffs.CVars.Keys);
                keys.Sort(StringComparer.OrdinalIgnoreCase);
                foreach (string k in keys) cv[k] = Round(p.Buffs.CVars[k]);
                o["cvars"] = cv;
            }
            if (sections.Contains("inventory"))
            {
                o["toolbelt"] = SlotsJson(p.inventory != null ? p.inventory.ItemGrid.items : null);
                o["backpack"] = SlotsJson(p.bag != null ? p.bag.ItemGrid.items : null);
                o["backpackCapacity"] = p.bag != null ? p.bag.ItemGrid.items.Length : 0;
                int carry = UnityEngine.Mathf.Clamp(UnityEngine.Mathf.RoundToInt(EffectManager.GetValue(PassiveEffects.CarryCapacity, _entity: p)), 0, (int)o["backpackCapacity"]);
                o["unencumberedSlots"] = carry;
                o["encumbranceSlots"] = (int)o["backpackCapacity"] - carry;
                o["desiredBackpackCapacity"] = RebirthSurvivorGearService.GetDesiredPhysicalBagSlots(p);
                o["toolbeltCapacity"] = RebirthToolbeltCapacity.GetSlotsForPlayer(p);
                o["visibleToolbeltSlots"] = RebirthGameBridgeUi.VisibleToolbeltSlotCount();
                RebirthWorldCharacterRecord gearRecord;
                if (RebirthWorldCharacterService.TryGet(p, out gearRecord) && gearRecord != null)
                {
                    o["survivorGear"] = JObject.FromObject(gearRecord.Support.EquippedGearBySlot);
                    o["gearAttributes"] = JObject.FromObject(gearRecord.Progression.Attributes);
                    o["gearBackground"] = gearRecord.Origin.BackgroundId;
                }
            }
            if (sections.Contains("equipment") && p.equipment != null)
            {
                var arr = new JArray();
                int equipmentSlots = p.equipment.GetSlotCount();
                for (int i = 0; i < equipmentSlots; i++)
                {
                    ItemValue iv = p.equipment.GetSlotItem(i);
                    if (iv == null || iv.IsEmpty()) continue;
                    JObject j = ItemValueJson(iv);
                    j["slot"] = i;
                    arr.Add(j);
                }
                o["equipment"] = arr;
            }
            if (sections.Contains("rebirth"))
            {
                var owner = RebirthSurvivorClientState.GetOwnerStateSnapshot();
                if (owner != null) o["rebirth"] = JObject.FromObject(owner);
            }
            if (sections.Contains("journal"))
            {
                RebirthJournalGuideService.Poll(p);
                var guides=new JArray();
                foreach(var e in RebirthJournalGuideService.Entries)
                    guides.Add(new JObject { ["id"]=e.Id,["title"]=e.Title,["read"]=RebirthJournalGuideService.IsRead(e),["image"]=e.Image });
                o["journal"]=new JObject { ["unread"]=RebirthJournalGuideService.UnreadCount,["error"]=RebirthJournalGuideService.Error,["entries"]=guides };
            }
            if (sections.Contains("challenges") && p.challengeJournal != null)
            {
                var challenges = new JArray();
                foreach (var challenge in p.challengeJournal.Challenges)
                {
                    if (challenge == null || challenge.ChallengeClass == null) continue;
                    var objectives = new JArray();
                    foreach (var objective in challenge.ObjectiveList)
                        objectives.Add(new JObject { ["text"] = objective.DescriptionText,
                            ["current"] = objective.Current, ["max"] = objective.MaxCount,
                            ["complete"] = objective.Complete });
                    challenges.Add(new JObject { ["id"] = challenge.ChallengeClass.Name,
                        ["state"] = challenge.ChallengeState.ToString(), ["objectives"] = objectives });
                }
                o["challenges"] = challenges;
            }
            if (sections.Contains("skills") && p.Progression != null && p.Progression.ProgressionValueQuickList != null)
            {
                var sk = new JObject();
                foreach (ProgressionValue pv in p.Progression.ProgressionValueQuickList)
                    if (pv != null && pv.Level > 0) sk[pv.Name] = pv.Level;
                o["skills"] = sk;
            }
        }

        if (sections.Contains("ui")) o["openWindows"] = OpenWindows();
        req.Complete(o);
    }

    // ---------------------------------------------------------------- /console

    private static void Console(BridgeRequest req)
    {
        var commands = new List<string>();
        string single = req.QueryString("cmd");
        if (single != null) commands.Add(single);
        if (!string.IsNullOrEmpty(req.Body))
            foreach (string line in req.Body.Split('\n'))
            {
                string c = line.Trim();
                if (c.Length > 0 && !c.StartsWith("#")) commands.Add(c);
            }
        if (commands.Count == 0) { req.Fail("no command: pass ?cmd=... or a request body with one command per line"); return; }
        if (SdtdConsole.Instance == null) { req.Fail("console not available yet", 409); return; }

        var results = new JArray();
        bool anyError = false;
        foreach (string cmd in commands)
        {
            long before = RebirthGameBridgeLog.LastSeq;
            List<string> output = SdtdConsole.Instance.ExecuteSync(cmd, null);
            var outArr = output != null ? new JArray(output.ToArray()) : new JArray();
            List<RebirthGameBridgeLog.Entry> logs = RebirthGameBridgeLog.Since(before, 0, 200);
            bool err = false;
            foreach (string line in output ?? new List<string>())
                if (LooksLikeFailure(line)) err = true;
            foreach (var e in logs)
                if (RebirthGameBridgeLog.Rank(e.Type) >= 2) err = true;
            anyError |= err;
            results.Add(new JObject
            {
                ["cmd"] = cmd,
                ["hadError"] = err,
                ["output"] = outArr,
                ["log"] = RebirthGameBridgeLog.ToJson(logs)
            });
        }
        req.Complete(new JObject { ["hadError"] = anyError, ["results"] = results });
    }

    // Many console commands report failures as plain output ("Unknown itemname given", "Entity not
    // found", ...) instead of the "*** ERROR" prefix, so flag the common phrasings too.
    private static readonly System.Text.RegularExpressions.Regex FailurePattern = new System.Text.RegularExpressions.Regex(
        @"^\*\*\* ERROR|^(unknown|invalid|error|cannot|can not|could not|couldn't|no such|failed)\b|\bnot found\b|\bfailed\b|\bwrong number of arguments\b",
        System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    private static bool LooksLikeFailure(string line)
    {
        if (line == null) return false;
        foreach (string part in line.Split('\n'))
        {
            string text = part.Trim();
            // Vector reports include prose such as "PASS parser rejects failed checks"
            // and counters such as "failed=0". Neither is a failed console command.
            if (text.StartsWith("PASS ", StringComparison.OrdinalIgnoreCase)) continue;
            if (System.Text.RegularExpressions.Regex.IsMatch(text, @"^FAIL\b|\b(?:fail|failed)=[1-9]\d*", System.Text.RegularExpressions.RegexOptions.IgnoreCase)) return true;
            text = System.Text.RegularExpressions.Regex.Replace(text, @"\bfailed=0\b", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (FailurePattern.IsMatch(text)) return true;
        }
        return false;
    }

    // ---------------------------------------------------------------- /log

    private static void LogQuery(BridgeRequest req)
    {
        long since;
        if (!long.TryParse(req.QueryString("since", "0"), out since)) since = 0;
        int minRank = RebirthGameBridgeLog.ParseMinRank(req.QueryString("level"));
        int limit = req.QueryInt("limit", 200);
        List<RebirthGameBridgeLog.Entry> entries = RebirthGameBridgeLog.Since(since, minRank, limit, req.QueryString("contains"));
        req.Complete(new JObject
        {
            ["lastLogSeq"] = RebirthGameBridgeLog.LastSeq,
            ["count"] = entries.Count,
            ["entries"] = RebirthGameBridgeLog.ToJson(entries),
            ["gameLog"] = Application.consoleLogPath
        });
    }

    // ---------------------------------------------------------------- /cvar

    private static void CVar(BridgeRequest req)
    {
        EntityPlayerLocal p;
        if (!RequirePlayer(req, out p)) return;

        if (req.Method == "POST")
        {
            string name = req.QueryString("name");
            if (name == null) { req.Fail("POST /cvar needs ?name=&value="); return; }
            float value = req.QueryFloat("value", float.NaN);
            if (float.IsNaN(value)) { req.Fail("invalid or missing ?value="); return; }
            p.SetCVar(name, value);
            req.Complete(new JObject { ["name"] = name, ["value"] = Round(p.GetCVar(name)) });
            return;
        }

        var o = new JObject();
        string names = req.QueryString("names") ?? req.QueryString("name");
        if (names == null)
        {
            foreach (var kv in p.Buffs.CVars) o[kv.Key] = Round(kv.Value);
        }
        else
        {
            foreach (string raw in names.Split(','))
            {
                string n = raw.Trim();
                if (n.Length == 0) continue;
                float v;
                o[n] = p.Buffs.CVars.TryGetValue(n, out v) ? (JToken)Round(v) : JValue.CreateNull();
            }
        }
        req.Complete(new JObject { ["cvars"] = o });
    }

    // ---------------------------------------------------------------- /screenshot

    private static void Screenshot(BridgeRequest req)
    {
        if (RebirthGameBridgePump.Instance == null) { req.Fail("pump not available", 500); return; }
        string label = SanitizeFileName(req.QueryString("name", "shot"));
        string dir = Path.Combine(RebirthGameBridge.OutputDir, "screenshots");
        Directory.CreateDirectory(dir);
        string file = Path.Combine(dir, DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + "_" + label + ".png");
        RebirthGameBridgePump.Instance.Run(CaptureRoutine(req, file, req.QueryInt("maxWidth", 1600)));
    }

    private static IEnumerator CaptureRoutine(BridgeRequest req, string file, int maxWidth)
    {
        yield return new WaitForEndOfFrame();
        Texture2D full = null, scaled = null;
        RenderTexture rt = null;
        try
        {
            int w = Screen.width, h = Screen.height;
            full = new Texture2D(w, h, TextureFormat.RGB24, false);
            full.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            full.Apply();
            Texture2D output = full;

            if (maxWidth > 0 && w > maxWidth)
            {
                int tw = maxWidth, th = Mathf.Max(1, Mathf.RoundToInt(h * (maxWidth / (float)w)));
                rt = RenderTexture.GetTemporary(tw, th, 0);
                Graphics.Blit(full, rt);
                RenderTexture prev = RenderTexture.active;
                RenderTexture.active = rt;
                scaled = new Texture2D(tw, th, TextureFormat.RGB24, false);
                scaled.ReadPixels(new Rect(0, 0, tw, th), 0, 0);
                scaled.Apply();
                RenderTexture.active = prev;
                output = scaled;
            }

            File.WriteAllBytes(file, output.EncodeToPNG());
            req.Complete(new JObject
            {
                ["path"] = file,
                ["width"] = output.width,
                ["height"] = output.height,
                ["scale"] = Round(output.width / (float)Screen.width), // pass as &scale= to /ui/click?x&y
                ["cursor"] = RebirthGameBridgeInput.CursorActive,
                ["state"] = DescribeGameState()
            });
        }
        catch (Exception ex)
        {
            req.Fail("screenshot failed: " + ex.Message, 500);
        }
        finally
        {
            if (rt != null) RenderTexture.ReleaseTemporary(rt);
            if (full != null) UnityEngine.Object.Destroy(full);
            if (scaled != null) UnityEngine.Object.Destroy(scaled);
        }
    }

    // ---------------------------------------------------------------- /ui

    private static void Ui(BridgeRequest req)
    {
        LocalPlayerUI ui = LocalPlayerUI.GetUIForPrimaryPlayer();
        GUIWindowManager wm = ui != null ? ui.windowManager : null;
        if (wm == null) { req.Fail("no player UI (state=" + DescribeGameState() + ")", 409); return; }

        if (req.Method == "POST")
        {
            string action = req.QueryString("action", "open").ToLowerInvariant();
            string window = req.QueryString("window");
            if (window == null) { req.Fail("missing ?window="); return; }
            if (action == "open")
            {
                if (wm.GetWindow(window) == null) { req.Fail("unknown window/window group '" + window + "'", 404); return; }
                wm.Open(window, req.QueryBool("modal", true));
            }
            else if (action == "close") wm.Close(window);
            else { req.Fail("action must be open or close"); return; }
            // GUIWindowManager applies opens/closes on a later frame; report the list after it settles.
            RebirthGameBridgePump.Instance.Run(CompleteAfterFrames(req, 2));
            return;
        }
        req.Complete(new JObject { ["openWindows"] = OpenWindows() });
    }

    private static IEnumerator CompleteAfterFrames(BridgeRequest req, int frames)
    {
        for (int i = 0; i < frames; i++) yield return null;
        req.Complete(new JObject { ["openWindows"] = OpenWindows() });
    }

    private static JArray OpenWindows()
    {
        var arr = new JArray();
        LocalPlayerUI ui = LocalPlayerUI.GetUIForPrimaryPlayer();
        GUIWindowManager wm = ui != null ? ui.windowManager : null;
        if (wm == null || wm.openWindows == null) return arr;
        foreach (GUIWindow w in wm.openWindows)
            if (w != null) arr.Add(w.Id);
        return arr;
    }

    // ---------------------------------------------------------------- /entities

    private static void Entities(BridgeRequest req)
    {
        EntityPlayerLocal p;
        if (!RequirePlayer(req, out p)) return;
        string exactTarget = req.QueryString("entity");
        if (exactTarget != null)
        {
            int targetId;
            if (!int.TryParse(exactTarget, NumberStyles.None, CultureInfo.InvariantCulture, out targetId) || targetId <= 0)
            { req.Fail("entity must be a positive integer", 400); return; }
            JObject snapshot;
            string failure;
            if (!RebirthGameBridgeTargetCombatSnapshot.TryCapture(p.world, targetId, out snapshot, out failure))
            { req.Fail(failure, 404); return; }
            req.Complete(new JObject { ["count"] = 1, ["entities"] = new JArray(snapshot), ["diagnostic"] = "read_only_exact_target_combat_snapshot" });
            return;
        }
        float radius = req.QueryFloat("radius", 40f);
        int limit = req.QueryInt("limit", 100);
        string contains = req.QueryString("contains");
        float r2 = radius * radius;

        var found = new List<KeyValuePair<float, Entity>>();
        foreach (Entity e in p.world.Entities.list)
        {
            if (e == null || e == p) continue;
            float d2 = (e.position - p.position).sqrMagnitude;
            if (d2 > r2) continue;
            if (contains != null && ClassName(e).IndexOf(contains, StringComparison.OrdinalIgnoreCase) < 0) continue;
            found.Add(new KeyValuePair<float, Entity>(d2, e));
        }
        found.Sort((a, b) => a.Key.CompareTo(b.Key));

        var arr = new JArray();
        for (int i = 0; i < found.Count && i < limit; i++)
        {
            Entity e = found[i].Value;
            var o = new JObject
            {
                ["id"] = e.entityId,
                ["class"] = ClassName(e),
                ["type"] = e.entityType.ToString(),
                ["distance"] = Round(Mathf.Sqrt(found[i].Key)),
                ["position"] = Vec(e.position),
                ["dead"] = e.IsDead()
            };
            var alive = e as EntityAlive;
            if (alive != null) o["health"] = alive.Health;
            var droppedItem = e as EntityItem;
            if (droppedItem != null) { o["item"] = ItemJson(droppedItem.itemStack); o["lifetimeSeconds"] = e.lifetime; }
            var recovery = e as EntityLootContainer;
            if (recovery != null)
            {
                o["contents"] = SlotsJson(recovery.bag.ItemGrid.items);
                o["elapsedTicks"] = recovery.deathUpdateTime;
                o["remainingTicks"] = Math.Max(0, recovery.timeStayAfterDeath - recovery.deathUpdateTime);
                o["configuredLifetimeSeconds"] = recovery.timeStayAfterDeath / 20f;
            }
            arr.Add(o);
        }
        req.Complete(new JObject { ["count"] = found.Count, ["entities"] = arr });
    }

    // ---------------------------------------------------------------- /look, /quit

    private static void Quit(BridgeRequest req)
    {
        req.Complete(new JObject { ["quitting"] = true });
        Log.Out("[REBIRTH GameBridge] Quit requested by bridge client.");
        Application.Quit();
    }

    // ---------------------------------------------------------------- helpers

    private static string ClassName(Entity e)
    {
        EntityClass ec = e.EntityClass;
        return ec != null ? ec.entityClassName : e.GetType().Name;
    }

    private static JArray SlotsJson(ItemStack[] slots)
    {
        var arr = new JArray();
        for (int i = 0; slots != null && i < slots.Length; i++)
        {
            JObject j = ItemJson(slots[i]);
            if (j == null) continue;
            j["slot"] = i;
            arr.Add(j);
        }
        return arr;
    }

    private static JObject ItemJson(ItemStack s)
    {
        if (s == null || s.IsEmpty()) return null;
        JObject j = ItemValueJson(s.itemValue);
        j["count"] = s.count;
        return j;
    }

    private static JObject ItemValueJson(ItemValue iv)
    {
        ItemClass ic = iv.ItemClass;
        var j = new JObject { ["name"] = ic != null ? ic.GetItemName() : ("#" + iv.type) };
        if (iv.Quality > 0) j["quality"] = (int)iv.Quality;
        if (iv.MaxUseTimes > 0) j["durability"] = Round(1f - iv.UseTimes / iv.MaxUseTimes);
        return j;
    }

    private static JObject StatJson(Stat s)
    {
        if (s == null) return null;
        return new JObject { ["value"] = Round(s.Value), ["max"] = Round(s.ModifiedMax) };
    }

    private static JArray Vec(Vector3 v)
    {
        return new JArray(Round(v.x), Round(v.y), Round(v.z));
    }

    private static float Round(float f)
    {
        if (float.IsNaN(f) || float.IsInfinity(f)) return 0f;
        return (float)Math.Round(f, 3);
    }

    private static string SanitizeFileName(string s)
    {
        var sb = new System.Text.StringBuilder();
        foreach (char c in s)
            sb.Append(char.IsLetterOrDigit(c) || c == '-' || c == '_' ? c : '_');
        return sb.Length > 0 ? sb.ToString(0, Math.Min(sb.Length, 60)) : "shot";
    }
}

