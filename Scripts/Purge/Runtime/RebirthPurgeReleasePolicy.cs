internal static class RebirthPurgeReleasePolicy
{
    // Qualification builds exercise the real integration before release approval.
    internal static bool Enabled
    {
        get
        {
#if PURGE_QUALIFICATION
            return true;
#else
            return false;
#endif
        }
    }
}
