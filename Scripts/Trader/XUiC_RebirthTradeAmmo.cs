using UnityEngine;
using UnityEngine.Scripting;

#nullable disable

/// <summary>
/// 2.6 Briston ammo exchange, offered by native traders. Ammo in the unlocked backpack is traded
/// for another type at its value, reduced by the exchange rate. Toolbelt and locked slots are kept.
/// Arrows change an amount by 1, or by 10 with Shift held.
/// </summary>
[Preserve]
public class XUiC_RebirthTradeAmmo : XUiController
{
    public const string WindowGroup = "rebirthTradeAmmo";

    private static readonly string[] Items = { "ammo9mmBulletBall", "ammo44MagnumBulletBall", "ammo762mmBulletBall", "ammoShotgunShell" };
    private static readonly string[] Labels = { "9mm", ".44", "7.62", "Shell" };
    private static readonly float[] Values = { 1590f, 1740f, 2090f, 1500f };
    private static readonly string[] Ids = { "9mm", "44", "762", "Shell" };

    private readonly XUiV_Label[] amountLabels = new XUiV_Label[Items.Length];
    private readonly int[] amounts = new int[Items.Length];
    private readonly int[] available = new int[Items.Length];
    private XUiV_Label total, description, targetLabel;
    private XUiV_Sprite targetIcon;
    private float exchangeRate;
    private int received, selected;

    public override void Init()
    {
        base.Init();
        total = GetChildById("lblTotal")?.ViewComponent as XUiV_Label;
        description = GetChildById("lblDescription")?.ViewComponent as XUiV_Label;
        targetLabel = GetChildById("lblTarget")?.ViewComponent as XUiV_Label;
        targetIcon = GetChildById("AmmoImage")?.ViewComponent as XUiV_Sprite;
        for (int i = 0; i < Items.Length; i++)
        {
            int index = i;
            amountLabels[i] = GetChildById("lbl" + Ids[i] + "Amount")?.ViewComponent as XUiV_Label;
            Press("btn" + Ids[i] + "Down", delegate { Step(index, -1); });
            Press("btn" + Ids[i] + "Up", delegate { Step(index, 1); });
        }
        Press("btnTargetBack", delegate { SetTarget((selected + Items.Length - 1) % Items.Length); });
        Press("btnTargetNext", delegate { SetTarget((selected + 1) % Items.Length); });
        var cancel = GetChildById("btnCancel") as XUiC_SimpleButton;
        if (cancel != null) cancel.OnPressed += delegate { Close(); };
        var confirm = GetChildById("btnConfirm") as XUiC_SimpleButton;
        if (confirm != null) confirm.OnPressed += delegate { Confirm(); };
    }

    private void Press(string id, System.Action action)
    {
        var button = GetChildById(id);
        if (button != null) button.OnPress += delegate { action(); };
    }

    public override void OnOpen()
    {
        base.OnOpen();
        exchangeRate = RateFor(xui.playerUI.entityPlayer);
        if (description != null)
            description.Text = Localization.Get("xuiTradeAmmoDescription").Replace("{0}", Mathf.RoundToInt(exchangeRate * 100f) + "%");
        SetTarget(0);
    }

    public override void OnClose()
    {
        base.OnClose();
        var manager = xui.playerUI.windowManager;
        if (manager.IsWindowOpen("dialog")) manager.Close("dialog");
    }

    // 2.6: 50% to 75%. Rebirth maps its combined Bartering and Trading value onto that same range.
    private static float RateFor(EntityPlayer player)
    {
        if (player == null) return 0.5f;
        if (RebirthXmlOptionConditionResolver.IsCharacterProgression("Rebirth"))
        {
            float top = Mathf.Max(0.0001f, RebirthProgressionRuntimeConfig.BarterPositive);
            return 0.5f + 0.25f * Mathf.Clamp01(player.Buffs.GetCustomVar(RebirthSkillWaveAService.BarterSellingCVar) / top);
        }
        var perk = player.Progression?.GetProgressionValue("perkBetterBarter");
        return 0.5f + Mathf.Clamp((perk != null ? perk.Level : 0) * 0.05f, 0f, 0.25f);
    }

    private static bool Locked(Bag bag, int index)
    {
        var locks = bag.LockedSlots;
        return locks != null && index < locks.Length && locks[index];
    }

    private int UnlockedCount(string itemName)
    {
        var bag = xui.playerUI.entityPlayer?.bag;
        if (bag == null) return 0;
        int type = ItemClass.GetItem(itemName).type, count = 0;
        var slots = bag.ItemGrid.items;
        for (int i = 0; i < slots.Length; i++)
            if (!Locked(bag, i) && slots[i] != null && !slots[i].IsEmpty() && slots[i].itemValue.type == type)
                count += slots[i].count;
        return count;
    }

    private void SetTarget(int index)
    {
        selected = index;
        if (targetLabel != null) targetLabel.Text = Labels[index];
        if (targetIcon != null) targetIcon.SpriteName = Items[index];
        // 2.6: everything you can trade starts selected; you trade down from there.
        for (int i = 0; i < Items.Length; i++)
        {
            available[i] = i == selected ? 0 : UnlockedCount(Items[i]);
            amounts[i] = available[i];
        }
        Refresh();
    }

    private void Step(int index, int direction)
    {
        if (index == selected) return;
        bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        amounts[index] = Mathf.Clamp(amounts[index] + direction * (shift ? 10 : 1), 0, available[index]);
        Refresh();
    }

    private void Refresh()
    {
        float value = 0f;
        for (int i = 0; i < Items.Length; i++)
        {
            if (amountLabels[i] != null) amountLabels[i].Text = i == selected ? "-" : amounts[i].ToString();
            if (i != selected) value += amounts[i] * exchangeRate * Values[i];
        }
        received = (int)(value / Values[selected]);
        if (total != null) total.Text = received.ToString();
    }

    private void Confirm()
    {
        var player = xui.playerUI.entityPlayer;
        if (player == null || received <= 0) return;

        // Re-read the backpack: it can change while the window is open.
        for (int i = 0; i < Items.Length; i++)
            if (i != selected && amounts[i] > UnlockedCount(Items[i])) { SetTarget(selected); return; }

        for (int i = 0; i < Items.Length; i++)
            if (i != selected && amounts[i] > 0) Take(Items[i], amounts[i]);

        var reward = new ItemStack(ItemClass.GetItem(Items[selected]), received);
        var inventory = xui.PlayerInventory;
        if (!inventory.AddItem(reward, true) && !reward.IsEmpty()) inventory.DropItem(reward);
        Close();
    }

    private void Take(string itemName, int amount)
    {
        var bag = xui.playerUI.entityPlayer.bag;
        int type = ItemClass.GetItem(itemName).type;
        var slots = bag.ItemGrid.items;
        for (int i = 0; i < slots.Length && amount > 0; i++)
        {
            if (Locked(bag, i) || slots[i] == null || slots[i].IsEmpty() || slots[i].itemValue.type != type) continue;
            int used = Mathf.Min(amount, slots[i].count);
            amount -= used;
            var left = slots[i].Clone();
            left.count -= used;
            bag.SetSlot(i, left.count > 0 ? left : ItemStack.Empty.Clone());
        }
    }

    private void Close()
    {
        xui.playerUI.windowManager.Close(windowGroup.Id);
    }
}

/// <summary>Trader dialog response that opens the ammo exchange.</summary>
[Preserve]
public sealed class DialogActionRebirthTradeAmmo : BaseDialogAction
{
    public override void PerformAction(EntityPlayer player)
    {
        var local = player as EntityPlayerLocal;
        local?.PlayerUI?.windowManager?.Open(XUiC_RebirthTradeAmmo.WindowGroup, true);
    }
}
