using System;
using System.Text;

#nullable disable

public sealed class RebirthBuildScriptFileDecl
{
    public string FileName;
    public string ProfileId;
    public bool DryRunOnly;
    public bool ExecutesBuild;
    public string Purpose;
    public string Safety;
}

/// <summary>
/// Read-only registry for generated dry-run build script files.
/// This does not execute scripts or build anything.
/// </summary>
public static class RebirthBuildScriptFileRegistry
{
    private static readonly RebirthBuildScriptFileDecl[] s_files = new[]
    {
        new RebirthBuildScriptFileDecl
        {
            FileName = "BuildScripts/build_release_DRYRUN.bat",
            ProfileId = "release",
            DryRunOnly = true,
            ExecutesBuild = false,
            Purpose = "Print the future release build sequence.",
            Safety = "Does not call MSBuild/dotnet, change symbols, or create output."
        },
        new RebirthBuildScriptFileDecl
        {
            FileName = "BuildScripts/build_profiling_DRYRUN.bat",
            ProfileId = "profiling",
            DryRunOnly = true,
            ExecutesBuild = false,
            Purpose = "Print the future profiling build sequence.",
            Safety = "Does not call MSBuild/dotnet, change symbols, or create output."
        },
        new RebirthBuildScriptFileDecl
        {
            FileName = "BuildScripts/build_stripped_comparison_DRYRUN.bat",
            ProfileId = "stripped",
            DryRunOnly = true,
            ExecutesBuild = false,
            Purpose = "Print the future stripped comparison build sequence.",
            Safety = "Does not call MSBuild/dotnet, change symbols, or create output."
        },
        new RebirthBuildScriptFileDecl
        {
            FileName = "BuildScripts/build_all_profiles_DRYRUN.bat",
            ProfileId = "all",
            DryRunOnly = true,
            ExecutesBuild = false,
            Purpose = "Print the future sequence for all comparison profiles.",
            Safety = "Does not call MSBuild/dotnet, change symbols, or create output."
        }
    };

    public static string GetReport()
    {
        StringBuilder sb = new StringBuilder(4096);
        sb.AppendLine("[RebirthBuildScriptFiles] generated dry-run build script files.");
        sb.AppendLine("file | profile | dry-run | executes build | purpose | safety");

        for (int i = 0; i < s_files.Length; i++)
        {
            RebirthBuildScriptFileDecl f = s_files[i];
            sb.Append(f.FileName).Append(" | ")
              .Append(f.ProfileId).Append(" | ")
              .Append(f.DryRunOnly).Append(" | ")
              .Append(f.ExecutesBuild).Append(" | ")
              .Append(f.Purpose).Append(" | ")
              .AppendLine(f.Safety);
        }

        return sb.ToString();
    }

    public static string GetSafetyReport()
    {
        StringBuilder sb = new StringBuilder(4096);
        sb.AppendLine("[RebirthBuildScriptFiles] safety rules:");
        sb.AppendLine("  1. Phase 1V scripts are dry-run only.");
        sb.AppendLine("  2. Phase 1V scripts do not compile.");
        sb.AppendLine("  3. Phase 1V scripts do not change compiler symbols.");
        sb.AppendLine("  4. Phase 1V scripts do not change csproj.");
        sb.AppendLine("  5. Phase 1V scripts do not create build/package output.");
        sb.AppendLine("  6. Future executable build scripts must be a separate phase.");
        return sb.ToString();
    }

    public static string GetSummaryReport()
    {
        return "[RebirthBuildScriptFiles] Dry-run script files: " + s_files.Length
            + ". None execute a build.";
    }
}
