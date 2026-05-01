using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Il2CppInterop.Runtime.Injection;
using UnityEngine;
using Nosebleed.Pancake.GameConfig;
using Nosebleed.Pancake.Models;
using Nosebleed.Pancake.Modal;
using Nosebleed.Pancake.View;
using UnityEngine.Localization;

namespace EvoTrackerMod;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
public class EvoTrackerPlugin : BasePlugin
{
    public const string PluginGuid = "com.an.evotrackermod";
    public const string PluginName = "Evo Tracker Mod";
    public const string PluginVersion = "1.6.0";

    public static EvoTrackerPlugin Instance { get; private set; }
    public Harmony HarmonyInstance { get; private set; }

    public PlayerModel CachedPlayer { get; set; }
    public List<EvoRecipe> AllRecipes { get; } = new();
    public List<CardChoiceInfo> CurrentChoices { get; } = new();
    public bool IsChooseCardModalOpen { get; set; }
    public Dictionary<string, string> GroupLocalizedNames { get; } = new();
    private bool _recipesBuilt;

    public override void Load()
    {
        Instance = this;
        Log.LogInfo($"{PluginName} v{PluginVersion} loading...");

        ClassInjector.RegisterTypeInIl2Cpp<EvoTrackerUI>();

        var uiHost = new GameObject("EvoTrackerUI");
        uiHost.AddComponent<EvoTrackerUI>();
        UnityEngine.Object.DontDestroyOnLoad(uiHost);

        HarmonyInstance = new Harmony(PluginGuid);
        HarmonyInstance.PatchAll(typeof(EvoTrackerPlugin).Assembly);

        Log.LogInfo($"{PluginName} loaded successfully.");
    }

    public void TryRebuildRecipes()
    {
        if (_recipesBuilt && AllRecipes.Count > 0) return;
        RebuildRecipes();
    }

    public void RebuildRecipes()
    {
        AllRecipes.Clear();
        _recipesBuilt = false;

        try
        {
            var allGroups = Resources.FindObjectsOfTypeAll<CardGroup>();
            if (allGroups == null || allGroups.Length == 0)
            {
                Log.LogWarning("No CardGroup found, will retry later.");
                return;
            }

            Log.LogInfo($"Total CardGroups: {allGroups.Length}");

            var evoMap = new Dictionary<int, (CardConfig evoCard, List<CardGroup> components)>();

            foreach (var group in allGroups)
            {
                try
                {
                    if (!group.HasEvolution) continue;
                    var evoCard = group.EvolvedCardConfig;
                    if (evoCard == null) continue;

                    int evoId = ((UnityEngine.Object)evoCard).GetInstanceID();

                    if (!evoMap.ContainsKey(evoId))
                        evoMap[evoId] = (evoCard, new List<CardGroup>());
                    evoMap[evoId].components.Add(group);
                }
                catch { }
            }

            foreach (var kvp in evoMap)
            {
                AllRecipes.Add(new EvoRecipe
                {
                    EvolvedCard = kvp.Value.evoCard,
                    RequiredGroups = kvp.Value.components
                });
            }

            _recipesBuilt = AllRecipes.Count > 0;
            BuildLocalizedNameCache();

            Log.LogInfo("--- 卡组翻译对照 ---");
            var loggedGroupIds = new HashSet<string>();
            foreach (var recipe in AllRecipes)
            {
                foreach (var group in recipe.RequiredGroups)
                {
                    if (group == null) continue;
                    var gid = group.AssetId ?? "";
                    if (!loggedGroupIds.Add(gid)) continue;
                    string internalName = ((UnityEngine.Object)group).name;
                    string localName = SafeGroupName(group);
                    Log.LogInfo($"  {internalName} -> {localName}");
                }
            }

            Log.LogInfo("--- 进化配方 ---");
            foreach (var recipe in AllRecipes)
            {
                var components = recipe.RequiredGroups.Select(g => SafeGroupName(g));
                string evoName = recipe.GetName();
                Log.LogInfo($"  {string.Join(" + ", components)} => {evoName}");
            }
            Log.LogInfo($"共 {AllRecipes.Count} 个进化配方, 来自 {allGroups.Length} 个卡组。");
        }
        catch (Exception ex)
        {
            Log.LogError($"RebuildRecipes failed: {ex}");
        }
    }

    private void BuildLocalizedNameCache()
    {
        GroupLocalizedNames.Clear();
        try
        {
            var allConfigs = Resources.FindObjectsOfTypeAll<CardConfig>();
            if (allConfigs == null) return;

            foreach (var config in allConfigs)
            {
                try
                {
                    if (config == null || config.cardGroup == null) continue;
                    var gid = config.cardGroup.AssetId;
                    if (string.IsNullOrEmpty(gid) || GroupLocalizedNames.ContainsKey(gid)) continue;

                    var loc = config.NameLoc;
                    if (loc == null) continue;
                    var text = loc.GetLocalizedString();
                    if (IsValidLocalizedText(text))
                        GroupLocalizedNames[gid] = text;
                }
                catch { }
            }
            Log.LogInfo($"Cached {GroupLocalizedNames.Count} localized group names.");
        }
        catch (Exception ex)
        {
            Log.LogError($"BuildLocalizedNameCache failed: {ex}");
        }
    }

    public Dictionary<string, int> GetPlayerOwnedGroupCounts()
    {
        var result = new Dictionary<string, int>();
        if (CachedPlayer == null) return result;

        try
        {
            var queried = new HashSet<string>();
            foreach (var recipe in AllRecipes)
            {
                foreach (var group in recipe.RequiredGroups)
                {
                    if (group == null) continue;
                    var gid = group.AssetId;
                    if (string.IsNullOrEmpty(gid) || !queried.Add(gid)) continue;

                    try
                    {
                        result[gid] = CachedPlayer.GetOwnedCardCount(group);
                    }
                    catch
                    {
                        result[gid] = 0;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            if (_ownedLogCount < 10)
            {
                _ownedLogCount++;
                Log.LogError($"GetPlayerOwnedGroupCounts failed: {ex}");
            }
        }
        return result;
    }
    private int _ownedLogCount;

    public bool IsRecipeEvolved(EvoRecipe recipe)
    {
        if (CachedPlayer == null || recipe?.EvolvedCard == null) return false;
        try { return CachedPlayer.GetOwnedCardCount(recipe.EvolvedCard) > 0; }
        catch { return false; }
    }

    public static string SafeGroupName(CardGroup group)
    {
        try
        {
            var gid = group.AssetId;
            if (!string.IsNullOrEmpty(gid) &&
                Instance?.GroupLocalizedNames.TryGetValue(gid, out var locName) == true &&
                IsValidLocalizedText(locName))
                return locName;
            if (!string.IsNullOrEmpty(group.groupName)) return group.groupName;
            return ((UnityEngine.Object)group).name;
        }
        catch { return group.AssetId ?? "?"; }
    }

    public static string SafeCardName(CardConfig config)
    {
        try
        {
            var loc = config.NameLoc;
            if (loc != null)
            {
                var text = loc.GetLocalizedString();
                if (IsValidLocalizedText(text)) return text;
            }
            var internalName = CleanInternalName(((UnityEngine.Object)config).name);
            if (internalName == "?" || string.IsNullOrEmpty(internalName))
                return "未解锁";
            return internalName + "(未解锁)";
        }
        catch { return "未解锁"; }
    }

    private static bool IsValidLocalizedText(string text)
    {
        if (string.IsNullOrEmpty(text)) return false;
        if (text.Contains("DEBUG_NOT_USED")) return false;
        if (text.StartsWith("No translation found")) return false;
        return true;
    }

    private static string CleanInternalName(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return "?";
        int lastUnderscore = raw.LastIndexOf('_');
        if (lastUnderscore >= 0 && lastUnderscore < raw.Length - 1)
            return raw.Substring(lastUnderscore + 1);
        return raw;
    }
}

public class EvoRecipe
{
    public CardConfig EvolvedCard { get; set; }
    public List<CardGroup> RequiredGroups { get; set; } = new();

    public string GetName()
    {
        try
        {
            return EvolvedCard != null ? EvoTrackerPlugin.SafeCardName(EvolvedCard) : "???";
        }
        catch { return "???"; }
    }

    public string GetGroupDisplayName(CardGroup group)
    {
        return EvoTrackerPlugin.SafeGroupName(group);
    }

    public int GetOwnedCount(Dictionary<string, int> ownedCounts)
    {
        return RequiredGroups.Count(g =>
        {
            try
            {
                return g != null &&
                       ownedCounts.TryGetValue(g.AssetId, out var c) && c > 0;
            }
            catch { return false; }
        });
    }

    public bool IsPartiallyOwned(Dictionary<string, int> ownedCounts)
    {
        return GetOwnedCount(ownedCounts) > 0;
    }
}

public class CardChoiceInfo
{
    public CardConfig Config { get; set; }
    public Transform ViewTransform { get; set; }
}
