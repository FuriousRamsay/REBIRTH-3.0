using System;

/// <summary>Historical evidence for one exterior transfer only; forwarding is not effect/payment.</summary>
internal sealed class RebirthGameBridgePoiTransferPhase
{
    private sealed class Slots
    {
        private readonly ItemStack[] original;
        private readonly ItemStack[] stacks;
        private readonly ItemValue[] values;
        private readonly int[] counts;
        private readonly string[] keys;
        private readonly Func<ItemValue, string> fingerprint;
        private Slots(ItemStack[] original, Func<ItemValue, string> fingerprint)
        {
            this.original = original; this.fingerprint = fingerprint;
            stacks = (ItemStack[])original.Clone(); values = new ItemValue[original.Length];
            counts = new int[original.Length]; keys = new string[original.Length];
            for (int i = 0; i < original.Length; i++)
            {
                var stack = original[i]; if (stack == null) continue;
                if (stack.count < 0 || stack.itemValue == null) throw new InvalidOperationException();
                values[i] = stack.itemValue; counts[i] = stack.count;
                // Serialize the original value without cloning or normalizing even empty metadata.
                keys[i] = fingerprint(stack.itemValue); if (keys[i] == null) throw new InvalidOperationException();
            }
        }
        internal static Slots Capture(ItemStack[] slots, Func<ItemValue, string> fingerprint)
        { if (slots == null || fingerprint == null) return null; try { var proof = new Slots(slots, fingerprint); return proof.Matches(slots) ? proof : null; } catch { return null; } }
        internal bool Matches(ItemStack[] current)
        {
            if (!ReferenceEquals(original, current) || current.Length != stacks.Length) return false;
            try
            {
                for (int i = 0; i < current.Length; i++)
                {
                    var stack = current[i]; if (!ReferenceEquals(stack, stacks[i])) return false;
                    if (stack == null) continue;
                    if (!ReferenceEquals(stack.itemValue, values[i]) || stack.count != counts[i]
                        || fingerprint(stack.itemValue) != keys[i]) return false;
                    if (!ReferenceEquals(current[i], stack) || !ReferenceEquals(stack.itemValue, values[i]) || stack.count != counts[i]) return false;
                }
                return true;
            }
            catch { return false; }
        }
    }
    private readonly RebirthGameBridgeInput.OwnedInputScope root;
    private readonly Func<bool> identity;
    private readonly Func<ItemStack[]> bag, crate;
    private readonly Slots bagProof, crateProof;
    private bool awake, forwarded, fault, cursor, finished;
    private RebirthGameBridgePoiTransferPhase(RebirthGameBridgeInput.OwnedInputScope root, Func<bool> identity,
        Func<ItemStack[]> bag, Func<ItemStack[]> crate, Slots bagProof, Slots crateProof)
    { this.root=root; this.identity=identity; this.bag=bag; this.crate=crate; this.bagProof=bagProof; this.crateProof=crateProof; }
    internal static RebirthGameBridgePoiTransferPhase Capture(RebirthGameBridgeInput.OwnedInputScope root, Func<bool> identity,
        Func<ItemStack[]> bag, Func<ItemStack[]> crate, Func<ItemValue,string> fingerprint)
    {
        try
        {
            if (root == null || !root.Admitted || !identity() || RebirthGameBridgeUi.HeldItemName() != null) return null;
            var bagProof=Slots.Capture(bag(),fingerprint); var crateProof=Slots.Capture(crate(),fingerprint);
            if (bagProof==null || crateProof==null || !root.Admitted || !identity()) return null;
            var result=new RebirthGameBridgePoiTransferPhase(root,identity,bag,crate,bagProof,crateProof);
            return result.Unchanged() ? result : null;
        }
        catch { return null; }
    }
    internal void AwakeThreat()
    { if (finished) { Fault(); return; } awake=true; root.RefuseAwakeThreat(this); }
    internal void Forwarded() { forwarded=true; }
    internal void Fault() { fault=true; }
    internal void CursorPending() { cursor=true; fault=true; }
    internal bool GenuineThreat { get { return awake; } }
    internal bool Disqualified { get { return forwarded || fault || cursor || finished; } }
    private bool Unchanged()
    {
        try
        {
            if (!root.ContextCurrent || !identity() || RebirthGameBridgeUi.HeldItemName()!=null) return false;
            if (!bagProof.Matches(bag()) || !crateProof.Matches(crate())) return false;
            return root.ContextCurrent && identity() && RebirthGameBridgeUi.HeldItemName()==null;
        }
        catch { return false; }
    }
    internal bool Finish()
    { bool retry=awake && !Disqualified && Unchanged() && !Disqualified; finished=true; return retry; }
}