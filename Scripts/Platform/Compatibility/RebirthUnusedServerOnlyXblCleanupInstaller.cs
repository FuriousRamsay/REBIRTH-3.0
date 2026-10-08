using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using HarmonyLib;
using Platform;
using Platform.XBL;
using UnityEngine;

// Candidate only. A target or patch mismatch leaves native cleanup unchanged.
public static class RebirthUnusedServerOnlyXblCleanupInstaller
{
    public const string Owner = "rebirth.3.3.serveronly-xbl-cleanup";
    private static readonly Harmony Harmony = new Harmony(Owner);
    private static bool installed;
    private static MethodInfo[] methods;
    public static string LastRefusal { get; private set; }
    private static readonly string[] Bodies = {
        "54F61D9C4F7F8FC3C98BA63D8F04AF1E9F8FDE48820817F0864E5401AE33A75C",
        "D69136161D58377B60A1F8F07947EF7A5D3E33323E72C453CCCC3F623DD0A99A",
        "1754762C9A3206E1D267BC5647E603D53226A77DB371A2B2DA1605AB2B8779F9",
        "D4F9480D62343B568B474F209804811E8945CC7D19D6D3FC1FE28B3229740266",
        "1940B856770A18EBDEFFBCB778C81899D9B2E02DD3B403E63516F61D1B2D2A74",
        "66DE9F019B1217FDBA56941F85C8919429E152DAFB3E145C17D1812D811B512C",
        "684888C0EBB17F374298B65EE2807526C066094C701BCC7EBBE1C1095F494FC1",
        "3E652F9EC0DDCDD64C52C9CBA261A1F70BBA3FFA41FF4296D6EBA45E6514B378",
        "2A982943039C2F4F7D88862D2969ABB55D0712334D26074A76A83FD5396E0955",
        "6D420773A3D2A17038FA2362D86ABFB4F7ABA3662BDD28BCED038EFF577E8091",
        "DFB7277D7FB40AB56E824649EC841E17AEFB214D47B9889E2FD8D2ACB12CFF38",
        "82B2F39F86B12751F0EE19122EE0D9F4ECE3807D0EE18ABEB9AFF467A76A9B2E",
        "DA8CE4F1AC4A1223C99E25E87CE5AC6316B9DFF02C5F1740B81CA70C58A50777" };
    private static bool Refuse(string reason) { LastRefusal = reason; return false; }
    private static string Hash(byte[] bytes)
    { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", ""); }
    private static MethodInfo Find(Type type, string name, params Type[] args)
    { return type.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly, null, args, null); }
    private static bool ExactTarget()
    {
        var v = Constants.cVersionInformation;
        if (v.Major != 3 || v.Minor != 30 || v.Build != 18 || Application.isEditor)
            return Refuse("unreviewed_version_or_editor");
        var assembly = typeof(XblPlatformApi).Assembly;
        if (assembly.ManifestModule.ModuleVersionId != new Guid("1a9a4203-3d95-4c90-b094-8926dec1ee9c") ||
            Hash(File.ReadAllBytes(assembly.Location)) != "FCEEC27300FFD3A1F97B097E43B60F3B07597F441E7ECBC6E1B59EEBB234C705")
            return Refuse("unreviewed_native_module");
        var gdk = typeof(Unity.XGamingRuntime.SDK).Assembly;
        if (gdk.ManifestModule.ModuleVersionId != new Guid("9b5f05c7-5d63-4437-bb0c-296cf4518b1e") || Hash(File.ReadAllBytes(gdk.Location)) != "2DDEDA58CB707525E0FE0512B7D95CC4B2C314378135030C72934963D2E94116") return Refuse("unreviewed_gdk_module");
        methods = new[] { Find(typeof(AbsPlatform), "Init"), Find(typeof(AbsPlatform), "Destroy"),
            Find(typeof(PlatformManager), "Init"), Find(typeof(PlatformManager), "Destroy"),
            Find(typeof(XblPlatformApi), "InitClientApis"), Find(typeof(XblPlatformApi), "Destroy"),
            Find(typeof(Api), "Init", typeof(IPlatform)), Find(typeof(Api), "InitServerApis"),
            Find(typeof(Factory), "CreateInstances"), Find(typeof(Platform.Steam.Factory), "CreateInstances"),
            Find(typeof(Platform.Steam.User), "Login", typeof(LoginUserCallback)),
            Find(typeof(Platform.MultiPlatform.Factory), "CreateInstances"), Find(typeof(Platform.EOS.Factory), "CreateInstances") };
        for (int i = 0; i < methods.Length; i++)
            if (methods[i] == null || methods[i].Module != assembly.ManifestModule ||
                methods[i].GetMethodBody() == null || Hash(methods[i].GetMethodBody().GetILAsByteArray()) != Bodies[i])
                return Refuse("unreviewed_native_method_body");
        return true;
    }
    public static bool PatchInventoryKnown()
    {
        try {
            if (methods == null) return Refuse("target_not_qualified");
            var prefix = typeof(RebirthUnusedServerOnlyXblCleanupCandidate).GetMethod("Prefix", BindingFlags.Static | BindingFlags.NonPublic);
            foreach (var patched in HarmonyLib.Harmony.GetAllPatchedMethods())
                if (patched.DeclaringType != null && patched.DeclaringType.Assembly == typeof(Unity.XGamingRuntime.SDK).Assembly) return Refuse("unfamiliar_gdk_patch");
            foreach (var method in methods) {
                var info = HarmonyLib.Harmony.GetPatchInfo(method);
                if (info == null) continue;
                if (info.Postfixes.Count != 0 || info.Transpilers.Count != 0 || info.Finalizers.Count != 0)
                    return Refuse("unfamiliar_lifecycle_patch");
                foreach (var patch in info.Prefixes)
                    if (!method.Equals(methods[5]) || patch.owner != Owner || !patch.PatchMethod.Equals(prefix))
                        return Refuse("unfamiliar_lifecycle_patch");
            }
            return true;
        } catch { return Refuse("patch_inventory_unavailable"); }
    }
    public static bool CleanupQualified()
    {
        try { return installed && ExactTarget() && PatchInventoryKnown(); }
        catch { return Refuse("cleanup_qualification_unavailable"); }
    }
    public static bool Install()
    {
        try {
            if (installed) return PatchInventoryKnown();
            if (!ExactTarget() || !PatchInventoryKnown()) return false;
            if (!RebirthHarmonyBootstrap.PatchClassOnce(Harmony, typeof(RebirthUnusedServerOnlyXblCleanupCandidate)))
                return Refuse("patch_registration_failed");
            installed = true;
            return PatchInventoryKnown();
        } catch { return Refuse("installation_qualification_unavailable"); }
    }
}


