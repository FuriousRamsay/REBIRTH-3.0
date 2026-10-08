internal static class ActualHeldQuestGate
{
    internal static bool Prefix(ItemValue newValue)=>!RebirthNpcPreparedHandMaterialization.SuppressesQuest(newValue);
}
