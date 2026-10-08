using System.Collections;
using System.Collections.Generic;
using InControl;
using Newtonsoft.Json.Linq;

#nullable disable

/// <summary>
/// "Play the game" endpoints: perception (crosshair target, nearby blocks) and actions (look, walk,
/// activate, attack/use, toolbelt, raw input, self-damage). Actions go through virtual input
/// (RebirthGameBridgeInput) so the game handles them exactly like a real player's keyboard/mouse.
/// </summary>
public static class RebirthGameBridgePlayer
{
    private static int movementGeneration;

    /// <summary>Cancel a running walkto/move (used by the guard reflex when an enemy shows up).</summary>
    public static void CancelMovement() { movementGeneration++; }

    private static int activeMoves;
    /// <summary>True while a walkto/move is steering the player (the instincts then don't turn the camera).</summary>
    public static bool IsMoving { get { return activeMoves > 0; } }

    public static bool TryDispatch(BridgeRequest req)
    {
        switch (req.Path)
        {
            case "/target": Target(req); return true;
            case "/findblocks": FindBlocks(req); return true;
            case "/lookat":
            case "/look": LookAt(req); return true;
            case "/walkto": WalkTo(req); return true;
            case "/move": Move(req); return true;
            case "/activate": Activate(req); return true;
            case "/press": Press(req); return true;
            case "/key": Key(req); return true;
            case "/release": ReleaseInput(req); return true;
            case "/actions": req.Complete(new JObject { ["actions"] = JObject.FromObject(RebirthGameBridgeInput.ListActions()) }); return true;
            case "/select": Select(req); return true;
            case "/damage": Damage(req); return true;
            case "/stop": Stop(req); return true;
            default: return false;
        }
    }

    private static EntityPlayerLocal Player(BridgeRequest req)
    {
        EntityPlayerLocal p = GameManager.Instance != null && GameManager.Instance.World != null
            ? GameManager.Instance.World.GetPrimaryPlayer() : null;
        if (p == null) req.Fail("no local player (state=" + RebirthGameBridgeHandlers.DescribeGameState() + ")", 409);
        return p;
    }

    private static PlayerAction Action(BridgeRequest req, string name)
    {
        PlayerAction a = RebirthGameBridgeInput.Find(name);
        if (a == null) req.Fail("unknown input action '" + name + "' (see /actions)", 404);
        return a;
    }

    private static void Run(IEnumerator routine) { RebirthGameBridgePump.Instance.Run(routine); }

    // ------------------------------------------------------------------ perception

    /// <summary>What is under the crosshair: block (name, position, activation prompt) or entity.</summary>
    public static JObject DescribeTarget(EntityPlayerLocal p)
    {
        var o = new JObject();
        WorldRayHitInfo hi = p.HitInfo;
        if (hi == null || !hi.bHitValid) { o["hit"] = false; return o; }
        o["hit"] = true;
        o["distance"] = Round(Mathf.Sqrt(hi.hit.distanceSq));
        o["point"] = Vec(hi.hit.pos);

        Entity e = hi.transform != null ? hi.transform.GetComponentInParent<Entity>() : null;
        if (e != null && e != p)
        {
            var eo = new JObject { ["id"] = e.entityId, ["class"] = e.EntityClass != null ? e.EntityClass.entityClassName : e.GetType().Name };
            var alive = e as EntityAlive;
            if (alive != null) { eo["health"] = alive.Health; eo["dead"] = alive.IsDead(); }
            o["entity"] = eo;
            return o;
        }

        Vector3i bp = hi.hit.blockPos;
        BlockValue bv = p.world.GetBlock(bp.x, bp.y, bp.z);
        if (!bv.isair)
        {
            Block b = bv.Block;
            var bo = new JObject
            {
                ["name"] = b.GetBlockName(),
                ["label"] = SafeLocalizedName(b),
                ["position"] = new JArray(bp.x, bp.y, bp.z),
                ["face"] = hi.hit.blockFace.ToString()
            };
            try
            {
                string prompt = b.GetActivationText(p.world, bv, bp, p);
                if (!string.IsNullOrEmpty(prompt)) bo["activationText"] = StripColors(prompt);
            }
            catch { }
            o["block"] = bo;
        }
        return o;
    }

    private static void Target(BridgeRequest req)
    {
        EntityPlayerLocal p = Player(req);
        if (p == null) return;
        req.Complete(new JObject { ["target"] = DescribeTarget(p), ["rotation"] = Vec(p.rotation), ["position"] = Vec(p.position) });
    }

    /// <summary>Scan blocks around the player whose name or label contains ?name=. Nearest first.</summary>
    private static void FindBlocks(BridgeRequest req)
    {
        EntityPlayerLocal p = Player(req);
        if (p == null) return;
        string name = req.QueryString("name");
        if (name == null) { req.Fail("missing ?name= (substring of block name or display name)"); return; }
        int radius = Mathf.Clamp(req.QueryInt("radius", 24), 1, 48);
        int limit = req.QueryInt("limit", 10);
        Vector3i c = p.GetBlockPosition();

        var hits = new List<KeyValuePair<float, Vector3i>>();
        var labels = new Dictionary<int, bool>();
        for (int x = -radius; x <= radius; x++)
            for (int y = -radius; y <= radius; y++)
                for (int z = -radius; z <= radius; z++)
                {
                    Vector3i pos = new Vector3i(c.x + x, c.y + y, c.z + z);
                    if (pos.y < 0 || pos.y > 255) continue;
                    BlockValue bv = p.world.GetBlock(pos.x, pos.y, pos.z);
                    if (bv.isair || bv.ischild) continue;
                    bool match;
                    if (!labels.TryGetValue(bv.type, out match))
                    {
                        Block b = bv.Block;
                        match = b != null && (Contains(b.GetBlockName(), name) || Contains(SafeLocalizedName(b), name));
                        labels[bv.type] = match;
                    }
                    if (match) hits.Add(new KeyValuePair<float, Vector3i>(x * x + y * y + z * z, pos));
                }
        hits.Sort((a, b) => a.Key.CompareTo(b.Key));

        var arr = new JArray();
        for (int i = 0; i < hits.Count && i < limit; i++)
        {
            Vector3i pos = hits[i].Value;
            Block b = p.world.GetBlock(pos.x, pos.y, pos.z).Block;
            arr.Add(new JObject
            {
                ["name"] = b.GetBlockName(),
                ["label"] = SafeLocalizedName(b),
                ["position"] = new JArray(pos.x, pos.y, pos.z),
                ["distance"] = Round(Mathf.Sqrt(hits[i].Key))
            });
        }
        req.Complete(new JObject { ["count"] = hits.Count, ["blocks"] = arr });
    }

    // ------------------------------------------------------------------ looking

    /// <summary>
    /// Aim the camera. Accepts ?x&amp;y&amp;z (a point, or a block position with ?block=1 to aim at its
    /// center), ?entity=id, or ?yaw&amp;pitch (absolute). Returns what ends up under the crosshair.
    /// </summary>
    private static void LookAt(BridgeRequest req)
    {
        EntityPlayerLocal p = Player(req);
        if (p == null) return;
        Func<Vector3?> point;
        string err = ResolvePoint(req, p, out point);
        if (err != null) { req.Fail(err); return; }
        float yaw = req.QueryFloat("yaw", p.rotation.y), pitch = req.QueryFloat("pitch", p.rotation.x);
        Run(LookRoutine(req, p, point, yaw, pitch));
    }

    private static IEnumerator LookRoutine(BridgeRequest req, EntityPlayerLocal p, Func<Vector3?> point, float yaw, float pitch)
    {
        yield return TurnToRoutine(p, point, yaw, pitch, 4f);
        yield return null;
        yield return null; // crosshair raycast refresh
        req.Complete(new JObject { ["rotation"] = Vec(p.rotation), ["target"] = DescribeTarget(p) });
    }

    /// <summary>
    /// Where to look for ?entity=ID (tracked live, body centre from the model's bones), or a fixed
    /// ?x&amp;y&amp;z point (block centre with ?block=1). Null function when neither is given.
    /// </summary>
    private static string ResolvePoint(BridgeRequest req, EntityPlayerLocal p, out Func<Vector3?> point)
    {
        point = null;
        string entity = req.QueryString("entity");
        if (entity != null)
        {
            int id;
            Entity e = int.TryParse(entity, out id) ? p.world.GetEntity(id) : null;
            if (e == null) return "entity " + entity + " not found";
            var alive = e as EntityAlive;
            float along = req.QueryString("part", "body") == "head" ? 0f : 0.45f;
            point = () => e == null ? (Vector3?)null : alive != null ? EntityAimPoint(alive, along) : e.position + Vector3.up * 0.8f;
            return null;
        }
        if (req.QueryString("x") == null) return null;
        float x = req.QueryFloat("x", float.NaN), y = req.QueryFloat("y", float.NaN), z = req.QueryFloat("z", float.NaN);
        if (float.IsNaN(x) || float.IsNaN(y) || float.IsNaN(z)) return "x, y and z are all required";
        Vector3 fixedPoint = req.QueryBool("block", false) ? new Vector3(x + 0.5f, y + 0.5f, z + 0.5f) : new Vector3(x, y, z);
        point = () => fixedPoint;
        return null;
    }

    /// <summary>
    /// A point on an entity's body from its live skeleton: along=0 is the head bone, 1 the pelvis.
    /// Follows the body when the zombie is knocked down, crawling or ragdolled. Falls back to height
    /// above the feet when the model has no bones.
    /// </summary>
    public static Vector3 EntityAimPoint(EntityAlive e, float along)
    {
        EModelBase m = e.emodel;
        Transform head = m != null ? m.GetHeadTransform() : null;
        Transform pelvis = m != null ? m.GetPelvisTransform() : null;
        // Bone transforms are in Unity space, which 7DTD shifts by a floating origin; entity positions are game
        // space. Convert (game = unity + Origin.position) or the aim point lands somewhere else entirely.
        if (head != null && pelvis != null) return Vector3.Lerp(head.position, pelvis.position, along) + Origin.position;
        return e.position + Vector3.up * e.GetEyeHeight() * Mathf.Lerp(0.95f, 0.5f, along);
    }

    /// <summary>Default human-like turn speed cap, degrees per second.</summary>
    public const float TurnSpeed = 300f;

    /// <summary>Yaw/pitch that put the crosshair on `point`.</summary>
    public static void AnglesTo(EntityPlayerLocal p, Vector3 point, out float yaw, out float pitch)
    {
        // Measured in-game: the crosshair ray starts at position + eye height, and in 7DTD a positive
        // rotation.x looks UP (opposite of Unity's camera convention).
        Vector3 eye = p.position + Vector3.up * p.GetEyeHeight();
        // The game's look/melee ray does not start at the eye: it starts at the camera, which can sit up to ~1.5 m behind it. Aiming from
        // the eye then sends the ray high (a steep downward aim passes 0.5-0.7 m above the chest at 1 m). Aim from the real origin.
        try { Vector3 o = p.GetLookRay().origin; if ((o - eye).magnitude < 3f) eye = o; } catch { }
        Vector3 d = point - eye;
        yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
        pitch = Mathf.Clamp(Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg, -89f, 89f);
    }

    // One shared camera "hand": every behaviour turns through this, so momentum carries over when one hands
    // off to another (walking -> fighting -> fleeing) instead of jolting.
    private static float turnYawVel, turnPitchVel;
    private static int lastTurnFrame = -10;

    /// <summary>
    /// Turn the camera one frame's worth toward yaw/pitch like a person moving a mouse: a critically damped
    /// spring - accelerates, moves fast for big turns (up to ~720 deg/s), decelerates smoothly into place,
    /// gentle for small corrections, never snaps or overshoots. `smoothTime` ~ how quickly it settles
    /// (0.07 snappy combat tracking, 0.12 normal, 0.3+ a slow look-around). Returns the remaining error in
    /// degrees. Call every frame with a STABLE target (don't randomise it per frame).
    /// </summary>
    public static float SmoothTurn(EntityPlayerLocal p, float yaw, float pitch, float smoothTime = 0.12f)
    {
        int frame = Time.frameCount;
        if (frame - lastTurnFrame > 3) { turnYawVel = 0f; turnPitchVel = 0f; } // starting fresh after a pause
        float dt = frame == lastTurnFrame ? 0f : Mathf.Min(Time.deltaTime, 0.05f); // one integration per frame
        lastTurnFrame = frame;

        Vector3 r = p.rotation;
        if (dt > 0f)
        {
            r.y = Mathf.SmoothDampAngle(r.y, yaw, ref turnYawVel, smoothTime, 1000f, dt);
            r.x = Mathf.SmoothDampAngle(r.x, pitch, ref turnPitchVel, smoothTime * 1.15f, 700f, dt);
            r.z = 0f;
            p.SetRotation(r);
            KeepBodyUpright(p, r.y);
        }
        return Mathf.Max(Mathf.Abs(Mathf.DeltaAngle(r.y, yaw)), Mathf.Abs(Mathf.DeltaAngle(r.x, pitch)));
    }

    /// <summary>
    /// EntityPlayerLocal.SetRotation applies the whole rotation, pitch included, to the body/physics transform as well as to the camera.
    /// The camera is a child of that transform, so the two pitches cancel and the melee ray (fired from a coroutine after the update)
    /// comes out FLAT whatever pitch we asked for - a real mouse look only turns the camera. Put the body back upright after every
    /// SetRotation so only the camera carries the pitch.
    /// </summary>
    public static void KeepBodyUpright(EntityPlayerLocal p, float yaw)
    {
        try
        {
            Transform pt = p.PhysicsTransform;
            if (pt != null) pt.eulerAngles = new Vector3(0f, yaw, 0f);
            Transform par = p.cameraTransform != null ? p.cameraTransform.parent : null;
            if (par != null) { Vector3 le = par.localEulerAngles; if (Mathf.Abs(Mathf.DeltaAngle(le.x, 0f)) > 0.01f) par.localEulerAngles = new Vector3(0f, le.y, le.z); }
        }
        catch { }
    }

    /// <summary>One frame of smooth aiming at a world point. Returns the remaining error in degrees.</summary>
    public static float SmoothAimAt(EntityPlayerLocal p, Vector3 point, float smoothTime = 0.12f)
    {
        float yaw, pitch;
        AnglesTo(p, point, out yaw, out pitch);
        return SmoothTurn(p, yaw, pitch, smoothTime);
    }

    /// <summary>Coroutine: turn smoothly until on target (error &lt; 0.5Â°) or `maxSeconds` elapse.
    /// `point` is re-evaluated every frame so moving targets are tracked.</summary>
    public static IEnumerator TurnToRoutine(EntityPlayerLocal p, Func<Vector3?> point, float yaw, float pitch, float maxSeconds = 3f)
    {
        float end = Time.realtimeSinceStartup + maxSeconds;
        while (Time.realtimeSinceStartup < end && p != null && !p.IsDead())
        {
            Vector3? target = point != null ? point() : null;
            float err = target.HasValue ? SmoothAimAt(p, target.Value) : SmoothTurn(p, yaw, pitch);
            if (err < 0.5f) break;
            yield return null;
        }
    }

    private static IEnumerator CompleteWithTargetAfterFrames(BridgeRequest req, EntityPlayerLocal p, int frames)
    {
        for (int i = 0; i < frames; i++) yield return null;
        req.Complete(new JObject { ["rotation"] = Vec(p.rotation), ["target"] = DescribeTarget(p) });
    }

    private static IEnumerator TurnOwned(EntityPlayerLocal p, Func<Vector3?> point, float yaw, float pitch, float maxSeconds, RebirthGameBridgeInput.OwnedInputScope scope)
    {
        if (!scope.Admitted) yield break;
        float end = Time.realtimeSinceStartup + maxSeconds;
        while (scope.Admitted && Time.realtimeSinceStartup < end && p != null && !p.IsDead())
        {
            Vector3? target = point != null ? point() : null;
            if (!scope.Admitted) yield break;
            float err = target.HasValue ? SmoothAimAt(p, target.Value) : SmoothTurn(p, yaw, pitch);
            if (err < 0.5f) break;
            yield return null;
        }
    }
    private static IEnumerator DoorOwned(EntityPlayerLocal p, Vector3i pos, Action<string> done, RebirthGameBridgeInput.OwnedInputScope scope)
    {
        if (!scope.Admitted) yield break;
        var dummy = new BridgeRequest { Method = "POST", Path = "/door" };
        Vector3 c = new Vector3(pos.x + 0.5f, pos.y + 0.5f, pos.z + 0.5f);
        // Press E up to three times (re-aiming each time): a miss or a half-second of lag is not a locked door.
        string prompt = "";
        for (int attempt = 0; attempt < 3; attempt++)
        {
            yield return ActivateRoutine(dummy, p, () => c, pos, 0f, scope);
            float until = Time.realtimeSinceStartup + 1.2f;
            while (Time.realtimeSinceStartup < until && !DoorIsOpen(p, pos)) yield return null;
            if (RebirthGameBridgeUi.AnyModalOpen()) yield return RebirthGameBridgeUi.CloseMenus(scope); // e.g. a keypad/lock prompt
            if (DoorIsOpen(p, pos) || p.world.GetBlock(pos.x, pos.y, pos.z).isair) { done("opened a door"); yield break; }
            try { BlockValue pb = p.world.GetBlock(pos.x, pos.y, pos.z); prompt = StripColors(pb.Block.GetActivationText(p.world, pb, pos, p)); } catch { }
            if (prompt.IndexOf("Locked", StringComparison.Ordinal) >= 0 && prompt.IndexOf("Unlocked", StringComparison.Ordinal) < 0) break;   // really locked
        }
        if (prompt.IndexOf("Locked", StringComparison.Ordinal) < 0 || prompt.IndexOf("Unlocked", StringComparison.Ordinal) >= 0) { done("couldn't open the door (not locked: " + prompt.Replace("\n", " ").Trim() + ")"); yield break; }

        string broke = null;
        yield return BreakDoorRoutine(p, pos, r => broke = r, scope);
        done("door was locked - " + broke);
    }
    private static IEnumerator BreakDoorOwned(EntityPlayerLocal p, Vector3i pos, Action<string> done, RebirthGameBridgeInput.OwnedInputScope scope)
    {
        if (!scope.Admitted) yield break;
        BlockValue bv = p.world.GetBlock(pos.x, pos.y, pos.z);
        int hp = BlockHitPoints(p, pos);
        if (hp > 1500) { done("too sturdy to break (" + hp + " hp)"); yield break; }
        string mat = bv.Block != null && bv.Block.blockMaterial != null ? (bv.Block.blockMaterial.id + " " + bv.Block.blockMaterial.DamageCategory) : "";
        bool metal = mat.IndexOf("metal", StringComparison.OrdinalIgnoreCase) >= 0 || mat.IndexOf("stone", StringComparison.OrdinalIgnoreCase) >= 0
            || mat.IndexOf("concrete", StringComparison.OrdinalIgnoreCase) >= 0;
        string tool = metal ? "meleeToolPickT1IronPickaxe" : "meleeToolAxeT1IronFireaxe";
        string result = null;
        yield return BreakBlockRoutine(p, pos, tool, r => result = r, scope);
        done((metal ? "metal door, " : "wooden door, ") + hp + " hp: " + result);
    }
    private static IEnumerator BreakBlockOwned(EntityPlayerLocal p, Vector3i pos, string tool, Action<string> done, RebirthGameBridgeInput.OwnedInputScope scope)
    {
        if (!scope.Admitted) yield break;
        int previous = p.inventory.holdingItemIdx;
        ItemStack original = previous >= 0 && previous < RebirthGameBridgeNeeds.ToolbeltSize(p) ? p.inventory.GetItem(previous) : null;
        int originalCount = original?.count ?? 0;
        float originalUseTimes = original?.itemValue?.UseTimes ?? 0f;
        string originalKey = RebirthGameBridgePoiStorage.OwnedItemKey(original);
        if (original != null && !original.IsEmpty() && originalKey == null) { scope.Refuse("original tool fingerprint unavailable"); done("original tool custody unresolved"); yield break; }
        string originalName = originalKey != null ? original.itemValue.ItemClass.GetItemName() : null;
        Func<ItemStack, bool> originalMatch = stack => originalKey != null && RebirthGameBridgePoiStorage.OwnedItemKey(stack) == originalKey;
        yield return RebirthGameBridgeNeeds.Prepare(p, ic => ic.GetItemName() == tool, null, tool, msg => { }, scope);
        string name;
        int slot = RebirthGameBridgeNeeds.ToolbeltSlot(p, ic => ic.GetItemName() == tool, out name);
        if (slot < 0) { done("no " + tool + " to break it"); yield break; }
        bool equipped = false;
        yield return RebirthGameBridgeNeeds.Equip(p, slot, name, ok => equipped = ok, scope);
        if (!equipped) { done("couldn't equip " + tool); yield break; }
        ItemStack chosen = p.inventory.GetItem(slot);
        ItemValue chosenValue = chosen?.itemValue;

        bool chosenWasOriginal = originalKey != null && ReferenceEquals(chosen, original)
            && ReferenceEquals(chosenValue, original.itemValue) && RebirthGameBridgePoiStorage.OwnedItemKey(chosen) == originalKey;
        string chosenKey = RebirthGameBridgePoiStorage.OwnedItemKey(chosen);
        int chosenCount = chosen?.count ?? 0;
        float chosenUseTimes = chosenValue?.UseTimes ?? 0f;
        if (ReferenceEquals(chosen, original) && originalKey != null && (chosenKey != originalKey || chosenCount != originalCount))
        { scope.Refuse("original genuine tool changed during preparation"); done("original tool custody unresolved"); yield break; }
        Func<bool> stillChosen = () =>
        {
            RebirthGameBridgeInput.OwnedWearReceipt ignored;
            return scope.Admitted && p.inventory.holdingItemIdx == slot
                && chosen != null && !chosen.IsEmpty() && ReferenceEquals(p.inventory.GetItem(slot), chosen)
                && ReferenceEquals(p.inventory.GetItem(slot).itemValue, chosenValue)
                && RebirthGameBridgePoiStorage.TryOwnedWearReceipt(scope, slot, chosen, chosenValue, chosenCount, chosenUseTimes, chosenKey, out ignored);
        };
        if (!stillChosen()) { scope.Refuse("selected genuine tool identity changed"); done("tool custody unresolved"); yield break; }
        Vector3 c = new Vector3(pos.x + 0.5f, pos.y + 0.5f, pos.z + 0.5f);
        float until = Time.realtimeSinceStartup + 40f;
        int swings = 0;
        while (scope.Admitted && Time.realtimeSinceStartup < until && !p.world.GetBlock(pos.x, pos.y, pos.z).isair && !DoorIsOpen(p, pos))
        {
            if (!stillChosen()) { scope.Refuse("selected genuine tool identity changed"); done("tool custody unresolved"); yield break; }
            if (!scope.Admitted) yield break;
            SmoothAimAt(p, c, 0.1f);
            if (!RebirthGameBridgeNeeds.HandsBusy(p)) { if (!scope.TryHoldFrames(RebirthGameBridgeInput.LocalActions().Primary, 3, 1f, stillChosen)) yield break; swings++; }
            yield return null;
        }
        scope.Release(RebirthGameBridgeInput.LocalActions().Primary);
        if (!stillChosen()) { scope.Refuse("selected genuine tool identity changed"); done("tool custody unresolved"); yield break; }
        if (chosenValue.MaxUseTimes > 0 && chosenValue.UseTimes >= chosenValue.MaxUseTimes)
        { scope.Refuse("genuine tool durability exhausted; item custody retained"); done("tool needs repair; restoration pending"); yield break; }
        bool broken = p.world.GetBlock(pos.x, pos.y, pos.z).isair;
        if (chosenWasOriginal && (broken || DoorIsOpen(p, pos)))
        {
            RebirthGameBridgeInput.OwnedWearReceipt receipt;
            if (!RebirthGameBridgePoiStorage.TryOwnedWearReceipt(scope, slot, chosen, chosenValue, originalCount, originalUseTimes, originalKey, out receipt)
                || !scope.PublishWear(receipt))
            { scope.Refuse("owned original tool wear proof failed; restoration pending"); done("original tool restoration pending"); yield break; }
        }
        if (originalKey != null && !chosenWasOriginal && scope.Admitted)
        {
            string foundName;
            int restore = RebirthGameBridgeNeeds.ToolbeltSlot(p, ic => ic.GetItemName() == originalName, out foundName, originalMatch);
            if (restore < 0)
            {
                yield return RebirthGameBridgeNeeds.Prepare(p, ic => ic.GetItemName() == originalName, null, "the original held tool", msg => { }, scope, originalMatch);
                if (!scope.Admitted) yield break;
                restore = RebirthGameBridgeNeeds.ToolbeltSlot(p, ic => ic.GetItemName() == originalName, out foundName, originalMatch);
            }
            bool restored = false;
            if (restore >= 0) yield return RebirthGameBridgeNeeds.Equip(p, restore, originalName, ok => restored = ok && originalMatch(p.inventory.GetItem(restore)), scope);
            if (!restored) { scope.Refuse("original held tool restoration pending"); done("original held tool restoration pending"); yield break; }
        }
        if (originalKey == null && previous != slot && previous >= 0 && previous < RebirthGameBridgeNeeds.ToolbeltSize(p))
        {
            ItemStack current = p.inventory.GetItem(previous);
            if (current != null && !current.IsEmpty()) { scope.Refuse("original empty tool slot changed; restoration pending"); done("original empty slot restoration pending"); yield break; }
            bool restoredEmpty = false;
            yield return RebirthGameBridgeNeeds.Equip(p, previous, null, ok => restoredEmpty = ok && (p.inventory.GetItem(previous) == null || p.inventory.GetItem(previous).IsEmpty()), scope);
            if (!restoredEmpty) { scope.Refuse("original empty slot restoration pending"); done("original empty slot restoration pending"); yield break; }
        }
        done(broken ? "broke through with the " + tool + " (" + swings + " hits)" : "couldn't break it in 40 s (" + swings + " hits)");
    }
    private static IEnumerator ActivateOwned(BridgeRequest req, EntityPlayerLocal p, Func<Vector3?> point, Vector3i? block, float holdSeconds, RebirthGameBridgeInput.OwnedInputScope scope)
    {
        if (!scope.Admitted) yield break;
        // Too far to reach: walk toward it first and stop within reach (never to a precomputed spot).
        if (block.HasValue) yield return ApproachOwned(p, new Vector3(block.Value.x + 0.5f, block.Value.y + 0.5f, block.Value.z + 0.5f), 2f, 60f, scope);
        if (point != null) yield return TurnToRoutine(p, point, p.rotation.y, p.rotation.x, 4f, scope); // look at it first
        yield return null;
        yield return null; // let the crosshair raycast update after aiming

        // Closed loop for blocks: low/flat models (campfires, bags, pots) can be missed by aiming at the
        // block centre, so verify the crosshair is on that block and try lower points on it if not.
        if (block.HasValue && !CrosshairOnBlock(p, block.Value))
        {
            Vector3i b = block.Value;
            foreach (float h in new[] { 0.25f, 0.1f, 0.4f, 0.75f })
            {
                Vector3 alt = new Vector3(b.x + 0.5f, b.y + h, b.z + 0.5f);
                yield return TurnToRoutine(p, () => alt, 0f, 0f, 2f, scope);
                yield return null;
                yield return null;
                if (CrosshairOnBlock(p, b)) break;
            }
        }
        JObject before = DescribeTarget(p);
        PlayerActionsLocal a = RebirthGameBridgeInput.LocalActions();
        if (holdSeconds > 0f) { if (!scope.TryHoldSeconds(a.Activate, holdSeconds)) yield break; }
        else { if (!scope.TryHoldFrames(a.Activate, 3)) yield break; }
        float wait = Time.realtimeSinceStartup + Mathf.Max(0.35f, holdSeconds + 0.25f);
        while (Time.realtimeSinceStartup < wait) yield return null;
        req.Complete(new JObject { ["target"] = before, ["openWindows"] = RebirthGameBridgeUi.OpenWindowIds() });
    }
    private static IEnumerator WalkRoutineOwned(BridgeRequest req, EntityPlayerLocal p, Vector3 target, float radius, bool run, float maxSeconds, int generation, Func<string> interruption, bool allowDoors, RebirthGameBridgeInput.OwnedInputScope scope)
    {
        if (!scope.Admitted) yield break;
        PlayerActionsLocal a = RebirthGameBridgeInput.LocalActions();
        float start = Time.realtimeSinceStartup;
        Vector3 startPos = p.position;
        Vector3 lastProgressPos = p.position;
        float lastProgressTime = start;
        int unstickAttempts = 0, terrainBlocks = 0;
        bool sprintRest = false;
        Vector3i doorPos;
        float nextProbe = 0f;
        Vector3 steer = target - p.position;
        bool jumpAhead = false;
        string lastTerrainWhy = null;
        activeMoves++;
        string result = null;

        var walkWorld = p.world;
        try
        {
        while (result == null)
        {
            if (!scope.Admitted) { result = scope.Failure ?? "interrupted"; break; }
            if (generation != movementGeneration) { result = "cancelled"; break; }
            if (p == null || p.IsDead()) { result = "dead"; break; }
            if (req.IsAbandoned) { result = "abandoned"; break; }
            if (!ReferenceEquals(p.world, walkWorld)) { result = "world_changed"; break; }
            string interrupted = interruption?.Invoke();
            if (interrupted != null) { result = interrupted; break; }

            Vector3 to = target - p.position;
            to.y = 0f;
            if (to.magnitude <= radius) { result = "arrived"; break; }
            if (Time.realtimeSinceStartup - start > maxSeconds) { result = "timeout"; break; }

            // Look where we're going: probe the ground ahead a few times a second and steer around drops/walls.
            if (Time.realtimeSinceStartup >= nextProbe)
            {
                nextProbe = Time.realtimeSinceStartup + 0.15f;
                RebirthGameBridgeTerrain.Line line;
                steer = RebirthGameBridgeTerrain.BestDirection(p.position, to, Mathf.Min(3f, to.magnitude + 0.5f), out line);
                jumpAhead = line.JumpNeeded;
                if (!line.Safe && line.SafeLength < 1f)
                {
                    if (++terrainBlocks > 12) { result = "blocked_by_terrain (" + line.Why + ")"; break; }
                }
                else terrainBlocks = 0;
                if (!line.Safe && lastTerrainWhy != line.Why) lastTerrainWhy = line.Why;
            }

            // Turn toward the chosen direction like a person, and only walk once roughly facing it.
            float yaw = Mathf.Atan2(steer.x, steer.z) * Mathf.Rad2Deg;
            if (!scope.Admitted) yield break;
            SmoothTurn(p, yaw, -5f, 0.15f);
            float facingError = Mathf.Abs(Mathf.DeltaAngle(p.rotation.y, yaw));
            if (facingError < 40f && terrainBlocks == 0) scope.TryHoldFrames(a.MoveForward, 1);
            // Sprint in bursts like a player: run while stamina allows, walk to get it back.
            float stam = p.Stats != null ? p.Stats.Stamina.Value / Mathf.Max(1f, p.Stats.Stamina.ModifiedMax) : 1f;
            if (stam < 0.3f) sprintRest = true; else if (stam > 0.7f) sprintRest = false;
            if (run && !sprintRest && facingError < 20f && terrainBlocks == 0) scope.TryHoldFrames(a.Run, 1);
            if (jumpAhead && facingError < 25f && p.onGround) { scope.TryHoldFrames(a.Jump, 4); jumpAhead = false; }

            // Stuck detection: less than 0.4m of progress in 1.2s.
            if ((p.position - lastProgressPos).sqrMagnitude > 0.16f)
            {
                lastProgressPos = p.position;
                lastProgressTime = Time.realtimeSinceStartup;
            }
            else if (Time.realtimeSinceStartup - lastProgressTime > 0.8f && DoorAhead(p, out doorPos))
            {
                if (!allowDoors) { result = "recovery_route_blocked_by_door"; break; }
                // A door in the way: open it (E); if it's locked, break through with the right tool.
                string doorResult = null;
                yield return DoorRoutine(p, doorPos, msg => doorResult = msg, scope);
                if (doorResult != null) lastTerrainWhy = doorResult;
                lastProgressPos = p.position;
                lastProgressTime = Time.realtimeSinceStartup;
            }
            else if (Time.realtimeSinceStartup - lastProgressTime > 1.2f)
            {
                unstickAttempts++;
                if (unstickAttempts > 6) { result = "stuck"; break; }
                if (unstickAttempts % 3 == 0)
                    scope.TryHoldSeconds(unstickAttempts % 2 == 0 ? a.MoveLeft : a.MoveRight, 0.7f);
                scope.TryHoldFrames(a.Jump, 6);
                lastProgressTime = Time.realtimeSinceStartup;
            }
            yield return null;
        }

        }
        finally
        {
        scope.Release(a.MoveForward);
        scope.Release(a.Run);
        scope.Release(a.MoveLeft);
        scope.Release(a.MoveRight);
        scope.Release(a.Jump);
        activeMoves--;
        }
        LastWalkResult = result;
        Vector3 left = target - p.position;
        left.y = 0f;
        req.Complete(new JObject
        {
            ["result"] = result,
            ["position"] = Vec(p.position),
            ["remainingDistance"] = Round(left.magnitude),
            ["travelled"] = Round((p.position - startPos).magnitude),
            ["seconds"] = Round(Time.realtimeSinceStartup - start),
            ["terrain"] = lastTerrainWhy // last obstacle steered around (drop, wall...), if any
        }, result == "arrived" ? 200 : 409);
    }
    private static IEnumerator ApproachOwned(EntityPlayerLocal p, Vector3 center, float reach, float maxWalkSeconds, RebirthGameBridgeInput.OwnedInputScope scope)
    {
        if (!scope.Admitted) yield break;
        Vector3 flat = center - p.position; flat.y = 0f;
        if (flat.magnitude <= reach + .4f) yield break;
        var request = new BridgeRequest { Method = "POST", Path = "/walkto" };
        yield return WalkRoutineOwned(request, p, center, reach, flat.magnitude > 12f, maxWalkSeconds, ++movementGeneration, null, true, scope);
    }
    private static IEnumerator ApproachActivateOwned(EntityPlayerLocal p, Vector3i block, float reach, float maxSeconds, RebirthGameBridgeInput.OwnedInputScope scope)
    {
        var request = new BridgeRequest { Method = "POST", Path = "/activate" };
        Vector3 center = new Vector3(block.x + .5f, block.y + .5f, block.z + .5f);
        yield return ApproachOwned(p, center, reach, maxSeconds, scope);
        yield return ActivateOwned(request, p, () => center, block, 0f, scope);
    }
    public static IEnumerator TurnToRoutine(EntityPlayerLocal p, Func<Vector3?> point, float yaw, float pitch, float maxSeconds, RebirthGameBridgeInput.OwnedInputScope parent)
    {
        using (var scope = new RebirthGameBridgeInput.OwnedInputScope(p, () => parent != null && parent.Admitted, parent))
        {
            try { yield return RebirthGameBridgeUi.RunGuarded(TurnOwned(p, point, yaw, pitch, maxSeconds, scope), scope); }
            finally { if (scope.Failure != null) parent?.Refuse(scope.Failure); }
        }
    }
    public static IEnumerator ActivateRoutine(BridgeRequest req, EntityPlayerLocal p, Func<Vector3?> point, Vector3i? block, float holdSeconds, RebirthGameBridgeInput.OwnedInputScope parent)
    {
        using (var scope = new RebirthGameBridgeInput.OwnedInputScope(p, () => parent != null && parent.Admitted, parent))
        {
            try { yield return RebirthGameBridgeUi.RunGuarded(ActivateOwned(req, p, point, block, holdSeconds, scope), scope); }
            finally { if (scope.CursorCustodyPending) parent?.MarkCursorCustodyPending(); else if (scope.Failure != null) parent?.Refuse(scope.Failure); }
        }
    }
    public static IEnumerator ApproachAndActivate(EntityPlayerLocal p, Vector3i block, Action<string> log, float reach, float maxWalkSeconds, RebirthGameBridgeInput.OwnedInputScope parent)
    {
        using (var scope = new RebirthGameBridgeInput.OwnedInputScope(p, () => parent != null && parent.Admitted, parent))
        {
            try { yield return RebirthGameBridgeUi.RunGuarded(ApproachActivateOwned(p, block, reach, maxWalkSeconds, scope), scope); }
            finally { if (scope.CursorCustodyPending) parent?.MarkCursorCustodyPending(); else if (scope.Failure != null) parent?.Refuse(scope.Failure); }
        }
    }
    public static IEnumerator WalkToGuarded(EntityPlayerLocal p, Vector3 target, float radius, float maxSeconds, Func<string> interruption, Action<string> done, RebirthGameBridgeInput.OwnedInputScope parent)
    {
        bool completed = false;
        using (var scope = new RebirthGameBridgeInput.OwnedInputScope(p, () => parent != null && parent.Admitted && (interruption == null || interruption() == null), parent))
        {
            try
            {
                var request = new BridgeRequest { Method = "POST", Path = "/walkto" };
                LastWalkResult = null;
                yield return RebirthGameBridgeUi.RunGuarded(WalkRoutineOwned(request, p, target, radius, false, maxSeconds, ++movementGeneration, interruption, false, scope), scope);
                completed = true; done(scope.Failure ?? LastWalkResult ?? "cancelled");
            }
            finally { if (scope.Failure != null) parent?.Refuse(scope.Failure); if (!completed) done("cancelled"); }
        }
    }

    public static IEnumerator DoorRoutine(EntityPlayerLocal p, Vector3i pos, Action<string> done, RebirthGameBridgeInput.OwnedInputScope parent)
    {
        bool completed = false;
        using (var scope = new RebirthGameBridgeInput.OwnedInputScope(p, () => parent != null && parent.Admitted, parent))
        {
            try { yield return RebirthGameBridgeUi.RunGuarded(DoorOwned(p, pos, message => { completed = true; done(message); }, scope), scope); }
            finally { if (scope.CursorCustodyPending) parent?.MarkCursorCustodyPending(); else if (scope.Failure != null) parent?.Refuse(scope.Failure); if (!completed) done(scope.Failure ?? "interrupted"); }
        }
    }

    public static IEnumerator BreakDoorRoutine(EntityPlayerLocal p, Vector3i pos, Action<string> done, RebirthGameBridgeInput.OwnedInputScope parent)
    {
        bool completed = false;
        using (var scope = new RebirthGameBridgeInput.OwnedInputScope(p, () => parent != null && parent.Admitted, parent))
        {
            try { yield return RebirthGameBridgeUi.RunGuarded(BreakDoorOwned(p, pos, message => { completed = true; done(message); }, scope), scope); }
            finally { if (scope.CursorCustodyPending) parent?.MarkCursorCustodyPending(); else if (scope.Failure != null) parent?.Refuse(scope.Failure); if (!completed) done(scope.Failure ?? "interrupted"); }
        }
    }

    public static IEnumerator BreakBlockRoutine(EntityPlayerLocal p, Vector3i pos, string tool, Action<string> done, RebirthGameBridgeInput.OwnedInputScope parent)
    {
        bool completed = false;
        using (var scope = new RebirthGameBridgeInput.OwnedInputScope(p, () => parent != null && parent.Admitted, parent))
        {
            try { yield return RebirthGameBridgeUi.RunGuarded(BreakBlockOwned(p, pos, tool, message => { completed = true; done(message); }, scope), scope); }
            finally { if (scope.CursorCustodyPending) parent?.MarkCursorCustodyPending(); else if (scope.Failure != null) parent?.Refuse(scope.Failure); if (!completed) done(scope.Failure ?? "interrupted"); }
        }
    }

    // ------------------------------------------------------------------ movement

    /// <summary>Walk (or run) to ?x&amp;z (y optional) using real movement input. Jumps/strafes when stuck.</summary>
    private static void WalkTo(BridgeRequest req)
    {
        EntityPlayerLocal p = Player(req);
        if (p == null) return;
        float x = req.QueryFloat("x", float.NaN), z = req.QueryFloat("z", float.NaN);
        if (float.IsNaN(x) || float.IsNaN(z)) { req.Fail("x and z are required"); return; }
        var target = new Vector3(x, req.QueryFloat("y", p.position.y), z);
        Run(WalkRoutine(req, p, target, req.QueryFloat("radius", 1.5f), req.QueryBool("run", false),
            req.QueryFloat("maxSeconds", 60f), ++movementGeneration));
    }

    private static IEnumerator WalkRoutine(BridgeRequest req, EntityPlayerLocal p, Vector3 target, float radius, bool run, float maxSeconds, int generation, Func<string> interruption = null, bool allowDoors = true)
    {
        PlayerActionsLocal a = RebirthGameBridgeInput.LocalActions();
        float start = Time.realtimeSinceStartup;
        Vector3 startPos = p.position;
        Vector3 lastProgressPos = p.position;
        float lastProgressTime = start;
        int unstickAttempts = 0, terrainBlocks = 0;
        bool sprintRest = false;
        Vector3i doorPos;
        float nextProbe = 0f;
        Vector3 steer = target - p.position;
        bool jumpAhead = false;
        string lastTerrainWhy = null;
        activeMoves++;
        string result = null;

        var walkWorld = p.world;
        try
        {
        while (result == null)
        {
            if (generation != movementGeneration) { result = "cancelled"; break; }
            if (p == null || p.IsDead()) { result = "dead"; break; }
            if (req.IsAbandoned) { result = "abandoned"; break; }
            if (!ReferenceEquals(p.world, walkWorld)) { result = "world_changed"; break; }
            string interrupted = interruption?.Invoke();
            if (interrupted != null) { result = interrupted; break; }

            Vector3 to = target - p.position;
            to.y = 0f;
            if (to.magnitude <= radius) { result = "arrived"; break; }
            if (Time.realtimeSinceStartup - start > maxSeconds) { result = "timeout"; break; }

            // Look where we're going: probe the ground ahead a few times a second and steer around drops/walls.
            if (Time.realtimeSinceStartup >= nextProbe)
            {
                nextProbe = Time.realtimeSinceStartup + 0.15f;
                RebirthGameBridgeTerrain.Line line;
                steer = RebirthGameBridgeTerrain.BestDirection(p.position, to, Mathf.Min(3f, to.magnitude + 0.5f), out line);
                jumpAhead = line.JumpNeeded;
                if (!line.Safe && line.SafeLength < 1f)
                {
                    if (++terrainBlocks > 12) { result = "blocked_by_terrain (" + line.Why + ")"; break; }
                }
                else terrainBlocks = 0;
                if (!line.Safe && lastTerrainWhy != line.Why) lastTerrainWhy = line.Why;
            }

            // Turn toward the chosen direction like a person, and only walk once roughly facing it.
            float yaw = Mathf.Atan2(steer.x, steer.z) * Mathf.Rad2Deg;
            SmoothTurn(p, yaw, -5f, 0.15f);
            float facingError = Mathf.Abs(Mathf.DeltaAngle(p.rotation.y, yaw));
            if (facingError < 40f && terrainBlocks == 0) RebirthGameBridgeInput.HoldFrames(a.MoveForward, 1);
            // Sprint in bursts like a player: run while stamina allows, walk to get it back.
            float stam = p.Stats != null ? p.Stats.Stamina.Value / Mathf.Max(1f, p.Stats.Stamina.ModifiedMax) : 1f;
            if (stam < 0.3f) sprintRest = true; else if (stam > 0.7f) sprintRest = false;
            if (run && !sprintRest && facingError < 20f && terrainBlocks == 0) RebirthGameBridgeInput.HoldFrames(a.Run, 1);
            if (jumpAhead && facingError < 25f && p.onGround) { RebirthGameBridgeInput.HoldFrames(a.Jump, 4); jumpAhead = false; }

            // Stuck detection: less than 0.4m of progress in 1.2s.
            if ((p.position - lastProgressPos).sqrMagnitude > 0.16f)
            {
                lastProgressPos = p.position;
                lastProgressTime = Time.realtimeSinceStartup;
            }
            else if (Time.realtimeSinceStartup - lastProgressTime > 0.8f && DoorAhead(p, out doorPos))
            {
                if (!allowDoors) { result = "recovery_route_blocked_by_door"; break; }
                // A door in the way: open it (E); if it's locked, break through with the right tool.
                string doorResult = null;
                yield return DoorRoutine(p, doorPos, msg => doorResult = msg);
                if (doorResult != null) lastTerrainWhy = doorResult;
                lastProgressPos = p.position;
                lastProgressTime = Time.realtimeSinceStartup;
            }
            else if (Time.realtimeSinceStartup - lastProgressTime > 1.2f)
            {
                unstickAttempts++;
                if (unstickAttempts > 6) { result = "stuck"; break; }
                if (unstickAttempts % 3 == 0)
                    RebirthGameBridgeInput.HoldSeconds(unstickAttempts % 2 == 0 ? a.MoveLeft : a.MoveRight, 0.7f);
                RebirthGameBridgeInput.HoldFrames(a.Jump, 6);
                lastProgressTime = Time.realtimeSinceStartup;
            }
            yield return null;
        }

        }
        finally
        {
        RebirthGameBridgeInput.Release(a.MoveForward);
        RebirthGameBridgeInput.Release(a.Run);
        RebirthGameBridgeInput.Release(a.MoveLeft);
        RebirthGameBridgeInput.Release(a.MoveRight);
        RebirthGameBridgeInput.Release(a.Jump);
        activeMoves--;
        }
        LastWalkResult = result;
        Vector3 left = target - p.position;
        left.y = 0f;
        req.Complete(new JObject
        {
            ["result"] = result,
            ["position"] = Vec(p.position),
            ["remainingDistance"] = Round(left.magnitude),
            ["travelled"] = Round((p.position - startPos).magnitude),
            ["seconds"] = Round(Time.realtimeSinceStartup - start),
            ["terrain"] = lastTerrainWhy // last obstacle steered around (drop, wall...), if any
        }, result == "arrived" ? 200 : 409);
    }

    /// <summary>Result of the last walk ("arrived", "stuck", "timeout", "blocked_by_terrain (...)", ...).</summary>
    public static string LastWalkResult;

    /// <summary>Walk to a point (terrain-aware, doors handled) as a coroutine; reports the result.</summary>
    public static IEnumerator WalkTo(EntityPlayerLocal p, Vector3 target, float radius, bool run, float maxSeconds, Action<string> done)
    {
        var dummy = new BridgeRequest { Method = "POST", Path = "/walkto" };
        LastWalkResult = null;
        yield return WalkRoutine(dummy, p, target, radius, run, maxSeconds, ++movementGeneration);
        done(LastWalkResult ?? "unknown");
    }

    // No door opening/breaking during custody recovery; validate before each movement frame.
    public static IEnumerator WalkToGuarded(EntityPlayerLocal p, Vector3 target, float radius,
        float maxSeconds, Func<string> interruption, Action<string> done)
    {
        var request = new BridgeRequest { Method = "POST", Path = "/walkto" };
        LastWalkResult = null;
        var walk = WalkRoutine(request, p, target, radius, false, maxSeconds, ++movementGeneration, interruption, false);
        try { while (walk.MoveNext()) yield return walk.Current; }
        finally { (walk as IDisposable)?.Dispose(); }
        done(LastWalkResult ?? "cancelled");
    }
    private static bool IsDoorBlock(BlockValue bv)
    {
        if (bv.isair || bv.Block == null) return false;
        string n = bv.Block.GetBlockName();
        return n.IndexOf("door", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("hatch", StringComparison.OrdinalIgnoreCase) >= 0
            || n.IndexOf("gate", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    /// <summary>A closed door (or hatch/gate) within ~1.8 m ahead, at foot or head height.</summary>
    public static bool DoorAhead(EntityPlayerLocal p, out Vector3i pos)
    {
        pos = default(Vector3i);
        Vector3 dir = Quaternion.Euler(0f, p.rotation.y, 0f) * Vector3.forward;
        for (float s = 0.5f; s <= 1.8f; s += 0.4f)
            for (int h = 0; h <= 1; h++)
            {
                Vector3 pt = p.position + dir * s + Vector3.up * (0.2f + h);
                var bp = new Vector3i(Mathf.FloorToInt(pt.x), Mathf.FloorToInt(pt.y), Mathf.FloorToInt(pt.z));
                BlockValue bv = p.world.GetBlock(bp.x, bp.y, bp.z);
                if (bv.ischild && bv.Block != null) bp = bv.Block.multiBlockPos.GetParentPos(bp, bv);
                TileEntity te = p.world.GetTileEntity(bp);
                TEFeatureDoor door;
                // Native door feature is authoritative; decorative door trims are not doors.
                if (te != null && te.TryGetSelfOrFeature(out door) && !door.IsOpen()) { pos = bp; return true; }
            }
        return false;
    }

    public static bool DoorIsOpen(EntityPlayerLocal p, Vector3i pos)
    {
        try
        {
            BlockValue value = p.world.GetBlock(pos);
            if (value.ischild && value.Block != null)
                pos = value.Block.multiBlockPos.GetParentPos(pos, value);
            TileEntity te = p.world.GetTileEntity(pos);
            TEFeatureDoor door;
            if (te != null && te.TryGetSelfOrFeature(out door)) return door.IsOpen();
        }
        catch { }
        return false;
    }

    /// <summary>
    /// Open a door like a player (look at it, E). If it stays shut it is locked: break through it with the
    /// right tool (fireaxe for wood, iron pickaxe for metal/stone), then carry on.
    /// </summary>
    public static IEnumerator DoorRoutine(EntityPlayerLocal p, Vector3i pos, Action<string> done)
    {
        var dummy = new BridgeRequest { Method = "POST", Path = "/door" };
        Vector3 c = new Vector3(pos.x + 0.5f, pos.y + 0.5f, pos.z + 0.5f);
        // Press E up to three times (re-aiming each time): a miss or a half-second of lag is not a locked door.
        string prompt = "";
        for (int attempt = 0; attempt < 3; attempt++)
        {
            yield return ActivateRoutine(dummy, p, () => c, pos, 0f);
            float until = Time.realtimeSinceStartup + 1.2f;
            while (Time.realtimeSinceStartup < until && !DoorIsOpen(p, pos)) yield return null;
            if (RebirthGameBridgeUi.AnyModalOpen()) yield return RebirthGameBridgeUi.CloseMenus(); // e.g. a keypad/lock prompt
            if (DoorIsOpen(p, pos) || p.world.GetBlock(pos.x, pos.y, pos.z).isair) { done("opened a door"); yield break; }
            try { BlockValue pb = p.world.GetBlock(pos.x, pos.y, pos.z); prompt = StripColors(pb.Block.GetActivationText(p.world, pb, pos, p)); } catch { }
            if (prompt.IndexOf("Locked", StringComparison.Ordinal) >= 0 && prompt.IndexOf("Unlocked", StringComparison.Ordinal) < 0) break;   // really locked
        }
        if (prompt.IndexOf("Locked", StringComparison.Ordinal) < 0 || prompt.IndexOf("Unlocked", StringComparison.Ordinal) >= 0) { done("couldn't open the door (not locked: " + prompt.Replace("\n", " ").Trim() + ")"); yield break; }

        string broke = null;
        yield return BreakDoorRoutine(p, pos, r => broke = r);
        done("door was locked - " + broke);
    }

    /// <summary>Hit points of a block (its full strength), or 0 when unknown.</summary>
    public static int BlockHitPoints(EntityPlayerLocal p, Vector3i pos)
    {
        try { BlockValue bv = p.world.GetBlock(pos.x, pos.y, pos.z); return bv.Block != null ? bv.Block.MaxDamage : 0; } catch { return 0; }
    }

    /// <summary>
    /// Break a door down: the iron pickaxe for metal (and stone/concrete) doors, the fireaxe for wooden ones. Doors stronger than 1500 hp are
    /// left alone (a vault door is not worth the minutes), and the result says so.
    /// </summary>
    public static IEnumerator BreakDoorRoutine(EntityPlayerLocal p, Vector3i pos, Action<string> done)
    {
        BlockValue bv = p.world.GetBlock(pos.x, pos.y, pos.z);
        int hp = BlockHitPoints(p, pos);
        if (hp > 1500) { done("too sturdy to break (" + hp + " hp)"); yield break; }
        string mat = bv.Block != null && bv.Block.blockMaterial != null ? (bv.Block.blockMaterial.id + " " + bv.Block.blockMaterial.DamageCategory) : "";
        bool metal = mat.IndexOf("metal", StringComparison.OrdinalIgnoreCase) >= 0 || mat.IndexOf("stone", StringComparison.OrdinalIgnoreCase) >= 0
            || mat.IndexOf("concrete", StringComparison.OrdinalIgnoreCase) >= 0;
        string tool = metal ? "meleeToolPickT1IronPickaxe" : "meleeToolAxeT1IronFireaxe";
        string result = null;
        yield return BreakBlockRoutine(p, pos, tool, r => result = r);
        done((metal ? "metal door, " : "wooden door, ") + hp + " hp: " + result);
    }

    /// <summary>Break a block with a tool: get the tool in hand, aim, keep hitting until it's gone.</summary>
    public static IEnumerator BreakBlockRoutine(EntityPlayerLocal p, Vector3i pos, string tool, Action<string> done)
    {
        int previous = p.inventory.holdingItemIdx;
        yield return RebirthGameBridgeNeeds.Prepare(p, ic => ic.GetItemName() == tool, tool, tool, msg => { });
        string name;
        int slot = RebirthGameBridgeNeeds.ToolbeltSlot(p, ic => ic.GetItemName() == tool, out name);
        if (slot < 0) { done("no " + tool + " to break it"); yield break; }
        bool equipped = false;
        yield return RebirthGameBridgeNeeds.Equip(p, slot, name, ok => equipped = ok);
        if (!equipped) { done("couldn't equip " + tool); yield break; }
        Vector3 c = new Vector3(pos.x + 0.5f, pos.y + 0.5f, pos.z + 0.5f);
        float until = Time.realtimeSinceStartup + 40f;
        int swings = 0;
        while (Time.realtimeSinceStartup < until && !p.world.GetBlock(pos.x, pos.y, pos.z).isair && !DoorIsOpen(p, pos))
        {
            SmoothAimAt(p, c, 0.1f);
            if (!RebirthGameBridgeNeeds.HandsBusy(p)) { RebirthGameBridgeInput.HoldFrames(RebirthGameBridgeInput.LocalActions().Primary, 3); swings++; }
            yield return null;
        }
        bool broken = p.world.GetBlock(pos.x, pos.y, pos.z).isair;
        if (previous >= 0 && previous != slot) yield return RebirthGameBridgeNeeds.Equip(p, previous, null, ok => { });
        done(broken ? "broke through with the " + tool + " (" + swings + " hits)" : "couldn't break it in 40 s (" + swings + " hits)");
    }
    /// <summary>Raw timed movement: ?forward=-1..1&amp;strafe=-1..1&amp;seconds=1&amp;run=0&amp;jump=0&amp;crouch=0.</summary>
    private static void Move(BridgeRequest req)
    {
        EntityPlayerLocal p = Player(req);
        if (p == null) return;
        PlayerActionsLocal a = RebirthGameBridgeInput.LocalActions();
        float seconds = Mathf.Clamp(req.QueryFloat("seconds", 1f), 0.05f, 30f);
        float forward = Mathf.Clamp(req.QueryFloat("forward", 0f), -1f, 1f);
        float strafe = Mathf.Clamp(req.QueryFloat("strafe", 0f), -1f, 1f);
        if (forward > 0f) RebirthGameBridgeInput.HoldSeconds(a.MoveForward, seconds, forward);
        if (forward < 0f) RebirthGameBridgeInput.HoldSeconds(a.MoveBack, seconds, -forward);
        if (strafe > 0f) RebirthGameBridgeInput.HoldSeconds(a.MoveRight, seconds, strafe);
        if (strafe < 0f) RebirthGameBridgeInput.HoldSeconds(a.MoveLeft, seconds, -strafe);
        if (req.QueryBool("run", false)) RebirthGameBridgeInput.HoldSeconds(a.Run, seconds);
        if (req.QueryBool("crouch", false)) RebirthGameBridgeInput.HoldSeconds(a.Crouch, seconds);
        if (req.QueryBool("jump", false)) RebirthGameBridgeInput.HoldFrames(a.Jump, 6);
        Run(MoveRoutine(req, p, seconds));
    }

    private static IEnumerator MoveRoutine(BridgeRequest req, EntityPlayerLocal p, float seconds)
    {
        Vector3 start = p.position;
        float end = Time.realtimeSinceStartup + seconds;
        activeMoves++;
        while (Time.realtimeSinceStartup < end) yield return null;
        yield return null;
        activeMoves--;
        req.Complete(new JObject { ["position"] = Vec(p.position), ["travelled"] = Round((p.position - start).magnitude), ["onGround"] = p.onGround });
    }

    private static void Stop(BridgeRequest req)
    {
        movementGeneration++;
        RebirthGameBridgeInput.ReleaseAll();
        req.Complete(new JObject { ["stopped"] = true });
    }

    // ------------------------------------------------------------------ actions

    /// <summary>Optionally aim (?x,y,z[&amp;block=1] or ?entity=), then press the Activate key ("E").</summary>
    private static void Activate(BridgeRequest req)
    {
        EntityPlayerLocal p = Player(req);
        if (p == null) return;
        Func<Vector3?> point;
        string err = ResolvePoint(req, p, out point);
        if (err != null) { req.Fail(err); return; }
        Vector3i? block = null;
        if (point != null && req.QueryBool("block", false))
            block = new Vector3i(Mathf.FloorToInt(req.QueryFloat("x", 0f)), Mathf.FloorToInt(req.QueryFloat("y", 0f)), Mathf.FloorToInt(req.QueryFloat("z", 0f)));
        Run(ActivateRoutine(req, p, point, block, req.QueryFloat("hold", 0f)));
    }

    private static bool CrosshairOnBlock(EntityPlayerLocal p, Vector3i block)
    {
        WorldRayHitInfo hi = p.HitInfo;
        return hi != null && hi.bHitValid && hi.hit.blockPos == block;
    }

    /// <summary>
    /// Walk toward a block and stop within reach (from whichever side we arrive - no fixed standing spot),
    /// then look at it and press E. Used by the instincts (e.g. rescuing cooking food) and by /activate.
    /// </summary>
    public static IEnumerator ApproachAndActivate(EntityPlayerLocal p, Vector3i block, Action<string> log, float reach = 2.0f, float maxWalkSeconds = 60f)
    {
        var dummy = new BridgeRequest { Method = "POST", Path = "/activate" };
        Vector3 center = new Vector3(block.x + 0.5f, block.y + 0.5f, block.z + 0.5f);
        yield return ApproachRoutine(p, center, reach, maxWalkSeconds);
        yield return ActivateRoutine(dummy, p, () => center, block, 0f);
    }

    /// <summary>Walk toward a point until within ~2 m (interaction reach), if further than that.</summary>
    private static IEnumerator ApproachRoutine(EntityPlayerLocal p, Vector3 center, float reach = 2.0f, float maxWalkSeconds = 60f)
    {
        Vector3 flat = center - p.position; flat.y = 0f;
        if (flat.magnitude <= reach + 0.4f) yield break;
        var dummy = new BridgeRequest { Method = "POST", Path = "/walkto" };
        yield return WalkRoutine(dummy, p, center, reach, flat.magnitude > 12f, maxWalkSeconds, ++movementGeneration);
    }

    public static IEnumerator ActivateRoutine(BridgeRequest req, EntityPlayerLocal p, Func<Vector3?> point, Vector3i? block, float holdSeconds)
    {
        // Too far to reach: walk toward it first and stop within reach (never to a precomputed spot).
        if (block.HasValue) yield return ApproachRoutine(p, new Vector3(block.Value.x + 0.5f, block.Value.y + 0.5f, block.Value.z + 0.5f));
        if (point != null) yield return TurnToRoutine(p, point, p.rotation.y, p.rotation.x, 4f); // look at it first
        yield return null;
        yield return null; // let the crosshair raycast update after aiming

        // Closed loop for blocks: low/flat models (campfires, bags, pots) can be missed by aiming at the
        // block centre, so verify the crosshair is on that block and try lower points on it if not.
        if (block.HasValue && !CrosshairOnBlock(p, block.Value))
        {
            Vector3i b = block.Value;
            foreach (float h in new[] { 0.25f, 0.1f, 0.4f, 0.75f })
            {
                Vector3 alt = new Vector3(b.x + 0.5f, b.y + h, b.z + 0.5f);
                yield return TurnToRoutine(p, () => alt, 0f, 0f, 2f);
                yield return null;
                yield return null;
                if (CrosshairOnBlock(p, b)) break;
            }
        }
        JObject before = DescribeTarget(p);
        PlayerActionsLocal a = RebirthGameBridgeInput.LocalActions();
        if (holdSeconds > 0f) RebirthGameBridgeInput.HoldSeconds(a.Activate, holdSeconds);
        else RebirthGameBridgeInput.HoldFrames(a.Activate, 3);
        float wait = Time.realtimeSinceStartup + Mathf.Max(0.35f, holdSeconds + 0.25f);
        while (Time.realtimeSinceStartup < wait) yield return null;
        req.Complete(new JObject { ["target"] = before, ["openWindows"] = RebirthGameBridgeUi.OpenWindowIds() });
    }

    /// <summary>
    /// Press or hold any input action: ?action=Primary|Secondary|Jump|Reload|gui.Cancel|...
    /// &amp;seconds=0 (0 = short press) &amp;hold=1 (hold until /release) &amp;value=1.
    /// </summary>
    private static void Press(BridgeRequest req)
    {
        string name = req.QueryString("action");
        if (name == null) { req.Fail("missing ?action= (see /actions)"); return; }
        PlayerAction action = Action(req, name);
        if (action == null) return;
        float value = req.QueryFloat("value", 1f);
        float seconds = req.QueryFloat("seconds", 0f);
        if (req.QueryBool("hold", false)) { RebirthGameBridgeInput.HoldSeconds(action, 0f, value); req.Complete(new JObject { ["holding"] = action.Name }); return; }
        if (seconds > 0f) RebirthGameBridgeInput.HoldSeconds(action, seconds, value);
        else RebirthGameBridgeInput.HoldFrames(action, req.QueryInt("frames", 3), value);
        Run(PressRoutine(req, action, seconds));
    }

    /// <summary>
    /// Press a physical key by name (?name=Escape|Tab|E|F|R|Space|Left Shift|1..0|Left Mouse Button...)
    /// &amp;seconds=0: every action bound to it fires, exactly as when the player presses that key.
    /// </summary>
    private static void Key(BridgeRequest req)
    {
        string name = req.QueryString("name");
        if (name == null) { req.Fail("missing ?name= (e.g. Escape, Tab, E, Space, Left Shift)"); return; }
        List<PlayerAction> actions = RebirthGameBridgeInput.FindByKey(name);
        if (actions.Count == 0) { req.Fail("no input action is bound to key '" + name + "' (see /actions for binding names)", 404); return; }
        // Default to a human-length tap (~0.1 s); some UI handlers misbehave on 1-3 frame presses.
        float seconds = req.QueryString("frames") != null ? 0f : req.QueryFloat("seconds", 0.1f);
        var names = new JArray();
        foreach (PlayerAction a in actions)
        {
            if (seconds > 0f) RebirthGameBridgeInput.HoldSeconds(a, seconds);
            else RebirthGameBridgeInput.HoldFrames(a, req.QueryInt("frames", 3));
            names.Add(a.Owner.GetType().Name.Replace("PlayerActions", "").ToLowerInvariant() + "." + a.Name);
        }
        Run(KeyRoutine(req, actions[0], seconds, names));
    }

    private static IEnumerator KeyRoutine(BridgeRequest req, PlayerAction first, float seconds, JArray names)
    {
        float end = Time.realtimeSinceStartup + seconds;
        while (RebirthGameBridgeInput.IsHeld(first) || Time.realtimeSinceStartup < end) yield return null;
        for (int i = 0; i < 6; i++) yield return null;
        req.Complete(new JObject { ["actions"] = names, ["openWindows"] = RebirthGameBridgeUi.OpenWindowIds() });
    }

    private static IEnumerator PressRoutine(BridgeRequest req, PlayerAction action, float seconds)
    {
        float end = Time.realtimeSinceStartup + seconds;
        while (RebirthGameBridgeInput.IsHeld(action) || Time.realtimeSinceStartup < end) yield return null;
        for (int i = 0; i < 5; i++) yield return null; // let the game react (UI opens, swing starts...)
        EntityPlayerLocal p = GameManager.Instance.World != null ? GameManager.Instance.World.GetPrimaryPlayer() : null;
        var o = new JObject { ["pressed"] = action.Name, ["openWindows"] = RebirthGameBridgeUi.OpenWindowIds() };
        if (p != null) o["target"] = DescribeTarget(p);
        req.Complete(o);
    }

    private static void ReleaseInput(BridgeRequest req)
    {
        string name = req.QueryString("action");
        if (name == null) { RebirthGameBridgeInput.ReleaseAll(); req.Complete(new JObject { ["released"] = "all" }); return; }
        PlayerAction action = Action(req, name);
        if (action == null) return;
        RebirthGameBridgeInput.Release(action);
        req.Complete(new JObject { ["released"] = action.Name });
    }

    /// <summary>Select a toolbelt slot (1-10) by pressing its hotkey.</summary>
    private static void Select(BridgeRequest req)
    {
        EntityPlayerLocal p = Player(req);
        if (p == null) return;
        int slot = req.QueryInt("slot", 0);
        int available = RebirthGameBridgeNeeds.ToolbeltSize(p);
        if (slot < 1 || slot > available) { req.Fail("?slot must be 1.." + available); return; }
        if (p.IsDead()) { req.Fail("cannot select a toolbelt slot while dead", 409); return; }
        Run(SelectRoutine(req, p, slot));
    }

    private static IEnumerator SelectRoutine(BridgeRequest req, EntityPlayerLocal p, int slot)
    {
        bool selected = false;
        yield return RebirthGameBridgeNeeds.Equip(p, slot - 1, null, ok => selected = ok);
        if (!selected)
        { req.Fail("toolbelt selection did not take effect; close menus and check player state", 409); yield break; }
        ItemStack s = p.inventory.GetItem(p.inventory.holdingItemIdx);
        req.Complete(new JObject
        {
            ["holdingSlot"] = p.inventory.holdingItemIdx + 1,
            ["holding"] = s == null || s.IsEmpty() ? null : new JObject { ["name"] = s.itemValue.ItemClass.GetItemName(), ["count"] = s.count }
        });
    }

    /// <summary>Damage the local player: ?amount=10&amp;type=Bashing (any EnumDamageTypes name).</summary>
    private static void Damage(BridgeRequest req)
    {
        EntityPlayerLocal p = Player(req);
        if (p == null) return;
        int amount = req.QueryInt("amount", 10);
        EnumDamageTypes type;
        try { type = (EnumDamageTypes)Enum.Parse(typeof(EnumDamageTypes), req.QueryString("type", "Bashing"), true); }
        catch { req.Fail("unknown damage type (e.g. Bashing, Piercing, Slashing, Heat, Falling, BloodLoss)"); return; }
        int before = p.Health;
        int dealt = p.DamageEntity(new DamageSource(EnumDamageSource.External, type), amount, false, 1f);
        Run(DamageRoutine(req, p, before, dealt));
    }

    private static IEnumerator DamageRoutine(BridgeRequest req, EntityPlayerLocal p, int before, int dealt)
    {
        for (int i = 0; i < 5; i++) yield return null;
        var buffs = new JArray();
        foreach (BuffValue b in p.Buffs.ActiveBuffs) if (b != null && !b.Invalid && b.DurationInSeconds < 2f) buffs.Add(b.BuffName);
        req.Complete(new JObject { ["healthBefore"] = before, ["healthAfter"] = p.Health, ["dealt"] = dealt, ["newBuffs"] = buffs });
    }

    // ------------------------------------------------------------------ helpers

    private static bool Contains(string s, string sub)
    {
        return s != null && s.IndexOf(sub, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static string SafeLocalizedName(Block b)
    {
        try { return b.GetLocalizedBlockName(); } catch { return b.GetBlockName(); }
    }

    public static string StripColors(string s)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        var sb = new System.Text.StringBuilder(s.Length);
        for (int i = 0; i < s.Length; i++)
        {
            if (s[i] == '[')
            {
                int close = s.IndexOf(']', i);
                if (close > i && close - i <= 8) { i = close; continue; } // [ff0000], [-], [b]...
            }
            sb.Append(s[i]);
        }
        return sb.ToString();
    }

    private static JArray Vec(Vector3 v)
    {
        return new JArray(Round(v.x), Round(v.y), Round(v.z));
    }

    private static float Round(float f)
    {
        return float.IsNaN(f) || float.IsInfinity(f) ? 0f : (float)Math.Round(f, 2);
    }
}

