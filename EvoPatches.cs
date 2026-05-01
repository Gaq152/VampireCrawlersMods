using System;
using System.Reflection;
using HarmonyLib;
using Nosebleed.Pancake.Models;
using Nosebleed.Pancake.Modal;
using Nosebleed.Pancake.GameConfig;
using Nosebleed.Pancake.View;

namespace EvoTrackerMod;

[HarmonyPatch(typeof(PlayerModel), nameof(PlayerModel.Update))]
public static class PlayerUpdatePatch
{
    private static bool _logged;
    private static int _retryCount;
    private static float _lastRetryTime;

    public static void Postfix(PlayerModel __instance)
    {
        var plugin = EvoTrackerPlugin.Instance;
        if (plugin == null) return;

        if (plugin.CachedPlayer == null)
        {
            if (!_logged)
            {
                plugin.Log.LogInfo("[Lifecycle] PlayerModel.Update - first capture");
                _logged = true;
            }
            plugin.CachedPlayer = __instance;
        }

        if (plugin.AllRecipes.Count == 0 && _retryCount < 10)
        {
            float now = UnityEngine.Time.time;
            if (now - _lastRetryTime > 3f)
            {
                _lastRetryTime = now;
                _retryCount++;
                plugin.RebuildRecipes();
            }
        }
    }
}

[HarmonyPatch(typeof(PlayerModel), nameof(PlayerModel.OnEncounterStarted))]
public static class EncounterStartPatch
{
    public static void Postfix(PlayerModel __instance)
    {
        var plugin = EvoTrackerPlugin.Instance;
        if (plugin == null) return;
        plugin.CachedPlayer = __instance;
        plugin.TryRebuildRecipes();
    }
}

[HarmonyPatch(typeof(ChooseCardModal), nameof(ChooseCardModal.OnOpened))]
public static class ChooseCardModalOpenPatch
{
    public static void Postfix(ChooseCardModal __instance)
    {
        var plugin = EvoTrackerPlugin.Instance;
        if (plugin == null) return;

        plugin.IsChooseCardModalOpen = true;
        plugin.TryRebuildRecipes();

        try
        {
            var player = __instance._playerModel;
            if (player != null)
                plugin.CachedPlayer = player;
        }
        catch { }
    }
}

[HarmonyPatch(typeof(ChooseCardModal), nameof(ChooseCardModal.OnClosed))]
public static class ChooseCardModalClosePatch
{
    public static void Postfix()
    {
        var plugin = EvoTrackerPlugin.Instance;
        if (plugin == null) return;
        plugin.IsChooseCardModalOpen = false;
        plugin.CurrentChoices.Clear();
    }
}

[HarmonyPatch(typeof(ChooseCardModal), nameof(ChooseCardModal.PopulateCardRewardChoices))]
public static class PopulateCardRewardPatch
{
    public static void Postfix(ChooseCardModal __instance)
    {
        var plugin = EvoTrackerPlugin.Instance;
        if (plugin == null) return;

        plugin.CurrentChoices.Clear();

        try
        {
            var views = __instance._cardChoiceViews;
            if (views == null) return;

            int count = views.Count;
            for (int i = 0; i < count; i++)
            {
                var view = views[i];
                if (view == null) continue;

                var config = view.CardConfig;
                if (config != null)
                {
                    plugin.CurrentChoices.Add(new CardChoiceInfo
                    {
                        Config = config,
                        ViewTransform = ((UnityEngine.Component)view).transform
                    });
                }
            }


        }
        catch (Exception ex)
        {
            plugin.Log.LogError($"PopulateCardRewardPatch failed: {ex}");
        }
    }
}
