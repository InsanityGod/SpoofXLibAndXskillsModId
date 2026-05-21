using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Vintagestory.API.Common;
using Vintagestory.Common;
using Vintagestory.Server;

namespace SpoofXLibAndXskillsModId;

public class SpoofXLibAndXkillsModIdModSystem : ModSystem
{
    private const string modId = "spoofxlibandxkillsmodid";

    private readonly Type XSkillsType;
    private readonly Type XLibType;

    public SpoofXLibAndXkillsModIdModSystem()
    {
        XSkillsType = AccessTools.TypeByName("XSkills.XSkills");
        if (XSkillsType is not null) SpoofPatchClass.SpoofedModIDs.Add("xskills");

        XLibType = AccessTools.TypeByName("XLib.XLeveling.XLeveling");
        if (XLibType is not null) SpoofPatchClass.SpoofedModIDs.Add("xlib");

        if (!Harmony.HasAnyPatches(modId)) new Harmony(modId).PatchAll();
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "loadedMods")]
    public static extern ref Dictionary<string, ModContainer> GetLoadedMods(ModLoader instance);

    public override void StartPre(ICoreAPI api)
    {
        base.StartPre(api);
        var loadedModes = GetLoadedMods((ModLoader)api.ModLoader);

        if(XSkillsType is not null)
        {
            var XSkillsSystem = api.ModLoader.Systems.FirstOrDefault(XSkillsType.IsInstanceOfType);
            if(XSkillsSystem is not null && XSkillsSystem.Mod.Info.ModID != "xskills")
            {
                SpoofPatchClass.OriginalModIDs["xskills"] = XSkillsSystem.Mod.Info.ModID;
                loadedModes["xskills"] = loadedModes[XSkillsSystem.Mod.Info.ModID];
                loadedModes.Remove(XSkillsSystem.Mod.Info.ModID);
                XSkillsSystem.Mod.Info.ModID = "xskills";
            }
        }

        if(XLibType is not null)
        {
            var XLibSystem = api.ModLoader.Systems.FirstOrDefault(XLibType.IsInstanceOfType);
            if(XLibSystem is not null && XLibSystem.Mod.Info.ModID != "xlib")
            {
                SpoofPatchClass.OriginalModIDs["xlib"] = XLibSystem.Mod.Info.ModID;
                loadedModes["xlib"] = loadedModes[XLibSystem.Mod.Info.ModID];
                loadedModes.Remove(XLibSystem.Mod.Info.ModID);
                XLibSystem.Mod.Info.ModID = "xlib";
            }
        }
    }

    public override void Dispose()
    {
        base.Dispose();
        new Harmony(modId).UnpatchAll(modId);
        SpoofPatchClass.SpoofedModIDs.Clear();
        SpoofPatchClass.OriginalModIDs.Clear();
    }
}

[HarmonyPatch]
public static class SpoofPatchClass
{
    internal static readonly HashSet<string> SpoofedModIDs = [];
    internal static readonly Dictionary<string, string> OriginalModIDs = [];

    //Premature spoofing for the event that someone checks for modID before startPre
    [HarmonyPatch(typeof(ModLoader), nameof(ModLoader.IsModEnabled))]
    [HarmonyPrefix]
    public static bool Prefix(string modID, ref bool __result)
    {
        if(SpoofedModIDs.Contains(modID) || OriginalModIDs.ContainsValue(modID))
        {
            __result = true;
            return false;
        }
        return true;
    }

    //Prevents server from sending the wrong modID to clients, which would cause them to try and download the original mod
    [HarmonyPatch(typeof(ServerMain), "CreatePacketIdentification")]
    [HarmonyPostfix]
    public static void Postfix(Packet_Server __result)
    {
        if(__result?.Identification?.Mods is null) return;

        foreach(var mod in __result.Identification.Mods)
        {
            if(SpoofPatchClass.OriginalModIDs.TryGetValue(mod.Modid, out string original))
            {
                mod.Modid = original;
            }
        }
    }
}