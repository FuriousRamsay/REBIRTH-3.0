using System;
using System.Collections.Generic;
using System.Text;

#nullable disable

/// <summary>
/// Runtime invariant checker for the custom Personal Crafting composition. It audits the actual
/// current XUi rectangles after responsive layout/state updates; it does not change geometry.
/// </summary>
public static class RebirthPersonalCraftingLayoutAudit
{
    public sealed class Result
    {
        public bool Passed;
        public int Fingerprint;
        public string Report = string.Empty;
    }

    private struct R
    {
        public int X, Y, W, H;
        public int Right => X + W;
        public int Bottom => Y + H;
        public override string ToString() => X + "," + Y + " " + W + "x" + H;
    }

    public static Result Evaluate(XUiC_RebirthPersonalCrafting owner)
    {
        Result result = new Result { Passed = true };
        if (owner == null)
        {
            result.Passed = false;
            result.Report = "FAIL owner=<null>";
            return result;
        }

        List<string> failures = new List<string>();
        List<string> notes = new List<string>();

        R root = Read(owner, "rebirthPersonalCraftingRoot");
        R top = Read(owner, "rebirthCraftingTopZone");
        R body = Read(owner, "rebirthCraftingBodyZone");
        R left = Read(owner, "rebirthCraftingLeftZone");
        R center = Read(owner, "rebirthCraftingCenterZone");
        R right = Read(owner, "rebirthCraftingRightZone");
        R recipes = Read(owner, "rebirthCraftingRecipesRegion");
        R outcome = Read(owner, "rebirthCraftingOutcomeRegion");
        R details = Read(owner, "rebirthCraftingDetailsRegion");
        R requirements = Read(owner, "rebirthCraftingRequirementsRegion");
        R inventory = Read(owner, "rebirthCraftingInventoryRegion");
        R queue = Read(owner, "rebirthCraftingQueueRegion");
        R actions = Read(owner, "rebirthCraftingActionsStrip");

        RequirePositive("root", root, failures);
        RequireContained("top/root", top, new R { X = 0, Y = 0, W = root.W, H = root.H }, failures);
        RequireContained("body/root", body, new R { X = 0, Y = 0, W = root.W, H = root.H }, failures);
        RequireSeparated("top/body", top, body, failures);

        R bodyBounds = new R { X = 0, Y = 0, W = body.W, H = body.H };
        R centerBounds = new R { X = 0, Y = 0, W = center.W, H = center.H };

        if (owner.IsInventoryOnlyMode)
        {
            RequireContained("center/body", center, bodyBounds, failures);
            int expectedCenterX = Math.Max(0, (body.W - center.W) / 2);
            if (Math.Abs(center.X - expectedCenterX) > 1)
                failures.Add("compact Inventory panel must be centered beneath fixed navigation: center=" + center + " body=" + body);
            if (center.Bottom != body.H)
                failures.Add("compact Inventory bottom must align with full Crafting body bottom: center=" + center + " body=" + body);
            if (center.W >= body.W && body.W > 1000)
                failures.Add("compact Inventory panel must remain narrower than fixed navigation/body: center=" + center + " body=" + body);
            if (!IsVisible(owner, "rebirthCraftingWorldStatus"))
                failures.Add("world status must remain visible because navigation is surface-independent");
            if (IsVisible(owner, "rebirthCraftingRootBackground") || IsVisible(owner, "rebirthCraftingRootFrame"))
                failures.Add("full Crafting root shell must remain transparent in compact Inventory");
            RequireContained("details/center", details, centerBounds, failures);
            RequireContained("inventory/center", inventory, centerBounds, failures);
            RequireSeparated("details/inventory", details, inventory, failures);
            if (inventory.Y < details.Bottom)
                failures.Add("compact Inventory must begin below Selected Item with no Requirements gap overlap: inv=" + inventory + " details=" + details);
            if (IsVisible(owner, "rebirthCraftingRequirementsRegion"))
                failures.Add("Requirements must be hidden in compact Inventory");
            if (IsVisible(owner, "rebirthCraftingLeftZone"))
                failures.Add("left recipe/outcome zone must be hidden in compact Inventory");
            if (right.X < body.W + 1000)
                failures.Add("queue authority must be parked off-screen in compact Inventory: right=" + right);
            notes.Add("surface=Inventory fixed-nav + centered compact panel + bottom-aligned");

            XUiController context = owner.GetChildById("rebirthCraftingItemContext");
            if (context?.ViewComponent != null && context.ViewComponent.IsVisible)
            {
                R itemContext = Read(owner, "rebirthCraftingItemContext");
                RequireContained("itemContext/center", itemContext, centerBounds, failures);
                if (itemContext.X != details.X || itemContext.Y != details.Y ||
                    itemContext.W != details.W || itemContext.H != details.H)
                    failures.Add("compact Inventory item context must exactly replace top details rectangle: item=" + itemContext + " details=" + details);
                notes.Add("itemContext=" + itemContext);

                R itemActions = Read(owner, "rebirthCraftingItemActionList");
                RequireContained("itemActions/itemContext", itemActions, new R { X = 0, Y = 0, W = itemContext.W, H = itemContext.H }, failures);
            }

            int fp = 17;
            Fingerprint(ref fp, root); Fingerprint(ref fp, top); Fingerprint(ref fp, body);
            Fingerprint(ref fp, center); Fingerprint(ref fp, details); Fingerprint(ref fp, inventory);
            result.Fingerprint = fp;
        }
        else
        {
            RequireContained("left/body", left, bodyBounds, failures);
            RequireContained("center/body", center, bodyBounds, failures);
            RequireContained("right/body", right, bodyBounds, failures);
            RequireSeparated("left/center", left, center, failures);
            RequireSeparated("center/right", center, right, failures);
            RequireSeparated("left/right", left, right, failures);
            if (!IsVisible(owner, "rebirthCraftingWorldStatus"))
                failures.Add("world status must remain visible in Crafting navigation");
            if (!IsVisible(owner, "rebirthCraftingRootBackground") || !IsVisible(owner, "rebirthCraftingRootFrame"))
                failures.Add("full Crafting root shell must be visible");

            RequireContained("recipes/left", recipes, new R { X = 0, Y = 0, W = left.W, H = left.H }, failures);
            RequireContained("outcome/left", outcome, new R { X = 0, Y = 0, W = left.W, H = left.H }, failures);
            RequireSeparated("recipes/outcome", recipes, outcome, failures);

            RequireContained("details/center", details, centerBounds, failures);
            RequireContained("recipeActions/details", actions, new R { X = 0, Y = 0, W = details.W, H = details.H }, failures);
            RequireContained("requirements/center", requirements, centerBounds, failures);
            RequireContained("inventory/center", inventory, centerBounds, failures);
            RequireSeparated("details/requirements", details, requirements, failures);
            RequireSeparated("requirements/inventory", requirements, inventory, failures);
            RequireSeparated("details/inventory", details, inventory, failures);
            if (inventory.Y < requirements.Bottom)
                failures.Add("inventory top is not fixed below Requirements: inv=" + inventory + " req=" + requirements);
            if (inventory.H < 170)
                failures.Add("inventory region is too short for header + four complete viewport rows: " + inventory.H);

            XUiController context = owner.GetChildById("rebirthCraftingItemContext");
            if (context?.ViewComponent != null && context.ViewComponent.IsVisible)
            {
                R itemContext = Read(owner, "rebirthCraftingItemContext");
                R itemActions = Read(owner, "rebirthCraftingItemActionList");
                RequireContained("itemActions/itemContext", itemActions, new R { X = 0, Y = 0, W = itemContext.W, H = itemContext.H }, failures);
            }

            RequireContained("queue/right", queue, new R { X = 0, Y = 0, W = right.W, H = right.H }, failures);
            if (queue.H != right.H)
                failures.Add("queue height must remain independent/full right column: queue=" + queue.H + " right=" + right.H);

            int fp = 17;
            Fingerprint(ref fp, root); Fingerprint(ref fp, top); Fingerprint(ref fp, body);
            Fingerprint(ref fp, left); Fingerprint(ref fp, center); Fingerprint(ref fp, right);
            Fingerprint(ref fp, recipes); Fingerprint(ref fp, outcome); Fingerprint(ref fp, details);
            Fingerprint(ref fp, requirements); Fingerprint(ref fp, inventory); Fingerprint(ref fp, queue);
            result.Fingerprint = fp;
            notes.Add("surface=Crafting full three-column");
        }

        result.Passed = failures.Count == 0;
        StringBuilder sb = new StringBuilder();
        sb.Append(result.Passed ? "PASS" : "FAIL")
          .Append(" fingerprint=").Append(result.Fingerprint)
          .Append(" root=").Append(root)
          .Append(" top=").Append(top)
          .Append(" body=").Append(body)
          .Append(" columns[L=").Append(left).Append(" C=").Append(center).Append(" R=").Append(right).Append("]")
          .Append(" center[D=").Append(details).Append(" Req=").Append(requirements).Append(" Inv=").Append(inventory).Append("]")
          .Append(" queue=").Append(queue);
        if (notes.Count > 0) sb.Append(" notes=").Append(string.Join(" | ", notes.ToArray()));
        if (failures.Count > 0) sb.Append(" errors=").Append(string.Join(" | ", failures.ToArray()));
        result.Report = sb.ToString();
        return result;
    }

    private static bool IsVisible(XUiC_RebirthPersonalCrafting owner, string id)
    {
        XUiController c = owner.GetChildById(id);
        return c?.ViewComponent != null && c.ViewComponent.IsVisible;
    }

    private static R Read(XUiC_RebirthPersonalCrafting owner, string id)
    {
        XUiController c = owner.GetChildById(id);
        if (c?.ViewComponent == null) return new R();
        Vector2i p = c.ViewComponent.Position;
        Vector2i s = c.ViewComponent.Size;
        return new R { X = p.x, Y = -p.y, W = Math.Max(0, s.x), H = Math.Max(0, s.y) };
    }

    private static void RequirePositive(string name, R r, List<string> failures)
    {
        if (r.W <= 0 || r.H <= 0) failures.Add(name + " has non-positive size: " + r);
    }

    private static void RequireContained(string name, R child, R parent, List<string> failures)
    {
        if (child.W <= 0 || child.H <= 0 || child.X < parent.X || child.Y < parent.Y || child.Right > parent.Right || child.Bottom > parent.Bottom)
            failures.Add(name + " outside parent child=" + child + " parent=" + parent);
    }

    private static void RequireSeparated(string name, R a, R b, List<string> failures)
    {
        bool intersects = a.X < b.Right && a.Right > b.X && a.Y < b.Bottom && a.Bottom > b.Y;
        if (intersects) failures.Add(name + " illegal intersection a=" + a + " b=" + b);
    }

    private static void Fingerprint(ref int hash, R r)
    {
        unchecked
        {
            hash = hash * 31 + r.X; hash = hash * 31 + r.Y;
            hash = hash * 31 + r.W; hash = hash * 31 + r.H;
        }
    }
}
