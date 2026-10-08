using System;

// Per-open-window display state; never changes the tile entity or server name registry.
internal sealed class RebirthLootTitleProjection
{
    private object target;
    private string originalTitle;
    private string appliedTitle;

    public void Reset()
    {
        target = null;
        originalTitle = null;
        appliedTitle = null;
    }

    public string Resolve(object currentTarget, string currentTitle, string customTitle)
    {
        if (!ReferenceEquals(target, currentTarget))
        {
            target = currentTarget;
            originalTitle = currentTitle;
            appliedTitle = null;
        }
        // Native sign text can arrive asynchronously. Preserve that newer native
        // title instead of replacing it with an earlier block display name.
        if (appliedTitle == null || !string.Equals(currentTitle, appliedTitle, StringComparison.Ordinal))
            originalTitle = currentTitle;

        if (!string.IsNullOrEmpty(customTitle))
        {
            appliedTitle = customTitle;
            return customTitle;
        }

        string result = appliedTitle != null ? originalTitle : currentTitle;
        appliedTitle = null;
        return result;
    }
}
