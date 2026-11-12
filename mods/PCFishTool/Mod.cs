using System;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using Il2Cpp;
using Il2CppNN.PF.Core.Network;
using Il2CppNN.PF.Core.Util;
using Il2CppNN.PF.Models;
using Il2CppNN.PF.UI.Breed;
using Il2CppNN.PF.UI.Common;
using Il2CppNN.PF.UI.Merge;
using MelonLoader;
using MelonLoader.Utils;

[assembly: MelonInfo(typeof(PCFishTool.PCFishToolMod), "PCFish Tool", "0.3.0-beta.4", "dark-but-spark")]
[assembly: MelonGame("NANOO COMPANY Inc.", "PCFish")]

namespace PCFishTool
{
    public sealed class PCFishToolMod : MelonMod
    {
        public override void OnInitializeMelon()
        {
            try
            {
                string config = Path.Combine(MelonEnvironment.GameRootDirectory, "UserData", "PCFishTool.cfg");
                if (File.Exists(config))
                {
                    foreach (string line in File.ReadAllLines(config))
                    {
                        if (line.Trim().Equals("debug_log=true", StringComparison.OrdinalIgnoreCase))
                        {
                            State.DebugLog = true;
                            break;
                        }
                    }
                }
            }
            catch
            {
            }

            if (State.DebugLog)
            {
                LoggerInstance.Msg("PCFish Tool loaded (debug log enabled).");
                LoggerInstance.Msg("Auto-group: sample fish first, then Auto Add.");
                LoggerInstance.Msg("Result FX skip: enabled.");
            }
            HarmonyInstance.PatchAll();
            AutoBreed.Initialize();
        }

        public override void OnUpdate()
        {
            AutoBreed.Update();
        }
    }

    internal static class State
    {
        internal static UIMerge CurrentMerge;
        internal static string SampleId;
        internal static bool InAutoParent;
        internal static bool DebugLog;
    }

    [HarmonyPatch(typeof(UIMerge), "AutoParent")]
    internal static class AutoParentPatch
    {
        private static void Prefix(UIMerge __instance)
        {
            State.CurrentMerge = __instance;
            State.SampleId = null;
            State.InAutoParent = true;

            try
            {
                if (__instance.mergeType != MergeType.Standard) return;
                Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStringArray list = __instance.mergeFishList;
                if (list == null) return;
                for (int i = 0; i < list.Length; i++)
                {
                    string id = list[i];
                    if (!string.IsNullOrEmpty(id))
                    {
                        State.SampleId = id;
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning("[PCFishTool] AutoParent prefix: " + ex.Message);
            }
        }

        private static void Postfix()
        {
            State.InAutoParent = false;
            State.CurrentMerge = null;
            State.SampleId = null;
        }
    }

    [HarmonyPatch(typeof(GameUtil), "GetFishList")]
    internal static class GetFishListPatch
    {
        private static void Postfix(ref Il2CppSystem.Collections.Generic.List<FishModel> __result)
        {
            if (!State.InAutoParent || State.CurrentMerge == null || __result == null) return;
            if (State.CurrentMerge.mergeType != MergeType.Standard) return;

            try
            {
                var groups = new Dictionary<long, List<FishModel>>();
                FishModel sample = null;

                for (int i = 0; i < __result.Count; i++)
                {
                    FishModel fish = __result[i];
                    if (fish == null) continue;

                    long key = ((long)fish.Level << 32) | (uint)fish.Grade;
                    if (!groups.TryGetValue(key, out List<FishModel> group))
                    {
                        group = new List<FishModel>();
                        groups.Add(key, group);
                    }
                    group.Add(fish);

                    if (State.SampleId != null && fish.Id == State.SampleId)
                    {
                        sample = fish;
                    }
                }

                if (groups.Count == 0) return;

                List<FishModel> chosen = null;

                if (sample != null)
                {
                    long key = ((long)sample.Level << 32) | (uint)sample.Grade;
                    groups.TryGetValue(key, out chosen);
                }

                if (chosen == null)
                {
                    List<FishModel> bestAny = null;
                    List<FishModel> bestTen = null;
                    foreach (List<FishModel> group in groups.Values)
                    {
                        if (bestAny == null || group.Count > bestAny.Count) bestAny = group;
                        if (group.Count >= 10 && (bestTen == null || group.Count > bestTen.Count)) bestTen = group;
                    }
                    chosen = bestTen ?? bestAny;
                }

                if (chosen == null || chosen.Count == 0) return;

                for (int i = 0; i < chosen.Count; i++)
                    __result[i] = chosen[i];

                while (__result.Count > chosen.Count)
                    __result.RemoveAt(__result.Count - 1);

                FishModel probe = chosen[0];
                if (State.DebugLog) MelonLogger.Msg("[PCFishTool] pick L" + probe.Level + " G" + probe.Grade + " x" + chosen.Count +
                    (chosen.Count < 10 ? " (less than 10; Merge disabled)" : ""));
            }
            catch (Exception ex)
            {
                MelonLogger.Warning("[PCFishTool] GetFishList postfix: " + ex.Message);
            }
        }
    }

    internal static class ResultFxSkip
    {
        internal static void Skip(UIFishResultFx fx, string tag)
        {
            if (fx == null) return;
            if (fx.onFinishFx == null) return;
            fx.FinishFx();
            fx.onFinishFx = null;
            if (State.DebugLog) MelonLogger.Msg("[PCFishTool] skip result FX (" + tag + ")");
        }
    }

    [HarmonyPatch(typeof(UIBreed), "_Breed_b__24_2")]
    internal static class BreedResultPatch
    {
        private static void Postfix(UIBreed __instance, NetworkResult status)
        {
            AutoBreed.OnBreedResult(status);
            if (status != NetworkResult.Success) return;
            ResultFxSkip.Skip(__instance.uiFishResultFx, "breed");
        }
    }

    [HarmonyPatch(typeof(UIMerge), "_Merge_b__39_1")]
    internal static class MergeResultPatch1
    {
        private static void Postfix(UIMerge __instance, NetworkResult status)
        {
            if (status != NetworkResult.Success) return;
            ResultFxSkip.Skip(__instance.uiFishResultFx, "merge");
        }
    }

    [HarmonyPatch(typeof(UIMerge), "_Merge_b__39_2")]
    internal static class MergeResultPatch2
    {
        private static void Postfix(UIMerge __instance, NetworkResult status)
        {
            if (status != NetworkResult.Success) return;
            ResultFxSkip.Skip(__instance.uiFishResultFx, "merge");
        }
    }

    [HarmonyPatch(typeof(GatchaResultFX), "OnEnable")]
    internal static class GatchaOnEnablePatch
    {
        private static void Prefix(GatchaResultFX __instance)
        {
            __instance.effectDuration = 0f;
        }
    }
}







