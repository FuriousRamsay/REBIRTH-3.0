using System;
using System.IO;
using System.Xml;

#nullable disable

/// <summary>
/// Reads the REBIRTH sandbox snapshot for a dedicated server without extending EnumGamePrefs.
///
/// Base 3.1 validates every direct <property> child of <ServerSettings> before mods are loaded.
/// An unknown direct property therefore aborts dedicated-server startup before REBIRTH can patch it.
/// To stay compatible with the native parser, REBIRTH uses a nested section that vanilla ignores:
///
/// <RebirthSettings>
///   <property name="RebirthSandboxCode" value="RB..." />
/// </RebirthSettings>
///
/// </summary>
public static class RebirthDedicatedServerSandboxConfig
{
    public const string SectionName = "RebirthSettings";
    public const string PropertyName = "RebirthSandboxCode";

    public static bool TryLoadCode(out string code, out string source)
    {
        code = string.Empty;
        source = string.Empty;

        if (!GameManager.IsDedicatedServer)
            return false;

        string configPath = ResolveServerConfigPath();
        if (string.IsNullOrEmpty(configPath) || !File.Exists(configPath))
            return false;

        try
        {
            XmlDocument document = new XmlDocument();
            document.Load(configPath);
            XmlElement root = document.DocumentElement;
            if (root == null || !string.Equals(root.Name, "ServerSettings", StringComparison.OrdinalIgnoreCase))
                return false;

            XmlElement section = FindDirectChild(root, SectionName);
            if (section == null)
                return false;

            string configuredCode = section.GetAttribute(PropertyName);
            if (string.IsNullOrEmpty(configuredCode))
                configuredCode = section.GetAttribute("SandboxCode");

            if (string.IsNullOrEmpty(configuredCode))
            {
                foreach (XmlNode node in section.ChildNodes)
                {
                    XmlElement property = node as XmlElement;
                    if (property == null || !string.Equals(property.Name, "property", StringComparison.OrdinalIgnoreCase))
                        continue;

                    string name = property.GetAttribute("name");
                    if (!string.Equals(name, PropertyName, StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(name, "SandboxCode", StringComparison.OrdinalIgnoreCase))
                        continue;

                    configuredCode = property.GetAttribute("value");
                    break;
                }
            }

            if (string.IsNullOrWhiteSpace(configuredCode))
            {
                Log.Warning("[RebirthSandbox] " + SectionName + " exists in serverconfig but contains no "
                    + PropertyName + ". The per-world REBIRTH snapshot will be used.");
                return false;
            }

            configuredCode = configuredCode.Trim();
            if (!Validate(configuredCode, configPath, out code))
                return false;

            source = "dedicated-serverconfig:" + Path.GetFileName(configPath);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error("[RebirthSandbox] Failed reading dedicated-server REBIRTH settings from '"
                + configPath + "': " + ex.GetType().Name + ": " + ex.Message);
            return false;
        }
    }

    public static string ResolveServerConfigPath()
    {
        string configPath = GameUtils.GetLaunchArgument("configfile");
        if (string.IsNullOrEmpty(configPath))
            return string.Empty;

        if (!configPath.Contains("."))
            configPath += ".xml";

        if (!configPath.Contains("/") && !configPath.Contains("\\"))
            configPath = Path.Combine(GameIO.GetApplicationPath(), configPath);

        return configPath;
    }

    private static bool Validate(string candidate, string source, out string code)
    {
        code = string.Empty;
        RebirthSandboxState ignored;
        if (!RebirthSandboxOptionManager.TryDecode(candidate, out ignored))
        {
            Log.Error("[RebirthSandbox] Invalid " + PropertyName + " from " + source
                + ". Value must be a complete REBIRTH sandbox code beginning with '"
                + RebirthSandboxOptionManager.CodePrefix + "'. Falling back to the per-world snapshot.");
            return false;
        }

        code = candidate;
        return true;
    }

    private static XmlElement FindDirectChild(XmlElement parent, string name)
    {
        foreach (XmlNode node in parent.ChildNodes)
        {
            XmlElement element = node as XmlElement;
            if (element != null && string.Equals(element.Name, name, StringComparison.OrdinalIgnoreCase))
                return element;
        }
        return null;
    }
}
