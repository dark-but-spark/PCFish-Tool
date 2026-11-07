using System;
using System.Collections.Generic;
using System.IO;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppNN.PF.Core.Managers;
using Il2CppNN.PF.Core.Network;
using Il2CppNN.PF.Core.Util;
using Il2CppNN.PF.Models;
using Il2CppNN.PF.UI.Breed;
using MelonLoader;
using MelonLoader.Utils;
using UnityEngine;

namespace PCFishTool
{
    internal static class AutoBreed
    {
        internal static bool Enabled = true;
        internal static float FirstDelaySeconds = 60f;
        internal static float MeanMinutes = 20f;
        internal static float SigmaMinutes = 4f;
        internal static float MinMinutes = 10f;
        internal static float MaxMinutes = 30f;
        internal static float IntervalSeconds = 10f;
        internal static float IntervalSigmaSeconds = 2f;
        internal static float IntervalMinSeconds = 5f;
        internal static float IntervalMaxSeconds = 20f;

        private enum Phase
        {
            Idle,
            Opening,
            SelectFirst,
            SelectSecond,
            ClickBreed
        }

        private static readonly System.Random Random = new System.Random();
        private static float _nextActionAt;
        private static float _stepAt;
        private static float _breedSentAt;
        private static bool _breeding;
        private static bool _initialized;
        private static bool _openedUi;
        private static Phase _phase;
        private static FishModel _high;
        private static FishModel _low;
        private static UIBreed _ui;

        internal static void Initialize()
        {
            LoadConfig();
            _nextActionAt = Time.time + Math.Max(5f, FirstDelaySeconds);
            _initialized = true;
            if (State.DebugLog)
                MelonLogger.Msg("[PCFishTool] auto-breed enabled; first check in " + Math.Max(5f, FirstDelaySeconds) + "s");
        }

        internal static void LoadConfig()
        {
            try
            {
                string path = Path.Combine(MelonEnvironment.GameRootDirectory, "UserData", "PCFishTool.cfg");
                if (!File.Exists(path)) return;
                foreach (string raw in File.ReadAllLines(path))
                {
                    string line = raw.Trim();
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    string key = line.Substring(0, eq).Trim();
                    string value = line.Substring(eq + 1).Trim();
                    if (key.Equals("auto_breed", StringComparison.OrdinalIgnoreCase))
                        Enabled = !value.Equals("false", StringComparison.OrdinalIgnoreCase);
                    else if (key.Equals("auto_breed_first_delay_seconds", StringComparison.OrdinalIgnoreCase))
                        FirstDelaySeconds = ParseFloat(value, FirstDelaySeconds);
                    else if (key.Equals("auto_breed_mean_minutes", StringComparison.OrdinalIgnoreCase))
                        MeanMinutes = ParseFloat(value, MeanMinutes);
                    else if (key.Equals("auto_breed_sigma_minutes", StringComparison.OrdinalIgnoreCase))
                        SigmaMinutes = ParseFloat(value, SigmaMinutes);
                    else if (key.Equals("auto_breed_min_minutes", StringComparison.OrdinalIgnoreCase))
                        MinMinutes = ParseFloat(value, MinMinutes);
                    else if (key.Equals("auto_breed_max_minutes", StringComparison.OrdinalIgnoreCase))
                        MaxMinutes = ParseFloat(value, MaxMinutes);
                    else if (key.Equals("auto_breed_interval_seconds", StringComparison.OrdinalIgnoreCase))
                        IntervalSeconds = ParseFloat(value, IntervalSeconds);
                    else if (key.Equals("auto_breed_interval_sigma_seconds", StringComparison.OrdinalIgnoreCase))
                        IntervalSigmaSeconds = ParseFloat(value, IntervalSigmaSeconds);
                    else if (key.Equals("auto_breed_interval_min_seconds", StringComparison.OrdinalIgnoreCase))
                        IntervalMinSeconds = ParseFloat(value, IntervalMinSeconds);
                    else if (key.Equals("auto_breed_interval_max_seconds", StringComparison.OrdinalIgnoreCase))
                        IntervalMaxSeconds = ParseFloat(value, IntervalMaxSeconds);
                }
            }
            catch
            {
            }
        }

        private static float ParseFloat(string value, float fallback)
        {
            float f;
            return float.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out f) ? f : fallback;
        }

        internal static void Update()
        {
            if (!_initialized || !Enabled) return;

            if (_breeding)
            {
                if (Time.time - _breedSentAt > 30f)
                {
                    _breeding = false;
                    Abort("breed response timeout");
                }
                return;
            }

            if (_phase != Phase.Idle)
            {
                if (Time.time >= _stepAt) Step();
                return;
            }

            if (Time.time < _nextActionAt) return;
            BeginSequence();
        }

        private static void BeginSequence()
        {
            try
            {
                GameDataManager data = GameDataManager.Instance;
                if (data == null)
                {
                    ScheduleNormal();
                    return;
                }

                if (data.breedCount <= 0)
                {
                    if (State.DebugLog) MelonLogger.Msg("[PCFishTool] auto-breed: no breed count, waiting");
                    ScheduleNormal();
                    return;
                }

                FishModel high;
                FishModel low;
                if (!FindPair(out high, out low))
                {
                    if (State.DebugLog) MelonLogger.Msg("[PCFishTool] auto-breed: no eligible pair");
                    ScheduleNormal();
                    return;
                }

                UIBreed ui = FindBreedUi();
                if (ui == null)
                {
                    if (State.DebugLog) MelonLogger.Msg("[PCFishTool] auto-breed: UIBreed not found");
                    ScheduleNormal();
                    return;
                }

                _high = high;
                _low = low;
                _ui = ui;
                _phase = Phase.Opening;
                _stepAt = Time.time;

                UIManager manager = UIManager.Instance;
                bool needOpen = !ui.gameObject.activeInHierarchy;
                if (manager != null && manager.viewBreed != null && !manager.viewBreed.activeSelf)
                    needOpen = true;

                if (needOpen)
                {
                    if (manager != null)
                    {
                        manager.OpenFishList();
                        _openedUi = true;
                    }
                    _stepAt = Time.time + 1.2f;
                }
                else
                {
                    _phase = Phase.SelectFirst;
                }
            }
            catch (Exception ex)
            {
                if (State.DebugLog) MelonLogger.Warning("[PCFishTool] auto-breed error: " + ex.Message);
                ScheduleNormal();
            }
        }

        private static void Step()
        {
            try
            {
                switch (_phase)
                {
                    case Phase.Opening:
                    {
                        UIManager manager = UIManager.Instance;
                        if (manager != null && manager.viewBreed != null)
                            manager.viewBreed.SetActive(true);
                        if (_ui != null) _ui.gameObject.SetActive(true);
                        if (State.DebugLog) MelonLogger.Msg("[PCFishTool] auto-breed: opened breed UI");
                        _phase = Phase.SelectFirst;
                        _stepAt = Time.time + 0.4f;
                        break;
                    }
                    case Phase.SelectFirst:
                    {
                        if (_ui == null) { Abort("UIBreed lost"); return; }
                        try { _ui.ResetParentFishList(); } catch { }
                        bool ok = _ui.SetParent(_high.Id);
                        if (State.DebugLog) MelonLogger.Msg("[PCFishTool] auto-breed: select high " + _high.Id + " -> " + ok);
                        if (!ok) { Abort("select high failed"); return; }
                        _phase = Phase.SelectSecond;
                        _stepAt = Time.time + 0.35f;
                        break;
                    }
                    case Phase.SelectSecond:
                    {
                        bool ok = _ui.SetParent(_low.Id);
                        if (State.DebugLog) MelonLogger.Msg("[PCFishTool] auto-breed: select low " + _low.Id + " -> " + ok);
                        if (!ok) { Abort("select low failed"); return; }
                        _phase = Phase.ClickBreed;
                        _stepAt = Time.time + 0.35f;
                        break;
                    }
                    case Phase.ClickBreed:
                    {
                        _ui.CheckButton();
                        if (State.DebugLog)
                            MelonLogger.Msg("[PCFishTool] auto-breed: click breed L" + _high.Level + " G" + _high.Grade +
                                " high=" + _high.BreedCount + " low=" + _low.BreedCount);
                        _breeding = true;
                        _breedSentAt = Time.time;
                        _phase = Phase.Idle;
                        _ui.Breed();
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Abort(ex.Message);
            }
        }

        private static void Abort(string reason)
        {
            if (State.DebugLog) MelonLogger.Warning("[PCFishTool] auto-breed abort: " + reason);
            _phase = Phase.Idle;
            _breeding = false;
            CloseUi();
            ScheduleNormal();
        }

        internal static void OnBreedResult(NetworkResult status)
        {
            if (!_breeding) return;
            _breeding = false;

            if (status != NetworkResult.Success)
            {
                CloseUi();
                ScheduleNormal();
                return;
            }

            GameDataManager data = GameDataManager.Instance;
            if (data != null && data.breedCount > 0 && FindPair())
            {
                _nextActionAt = Time.time + SampleIntervalSeconds();
                if (State.DebugLog)
                    MelonLogger.Msg("[PCFishTool] auto-breed: next breed in " + Math.Round(_nextActionAt - Time.time, 1) + "s (breedCount=" + data.breedCount + ")");
                return;
            }

            CloseUi();
            ScheduleNormal();
        }

        private static void CloseUi()
        {
            if (!_openedUi) return;
            _openedUi = false;
            try
            {
                if (_ui != null) _ui.Close();
            }
            catch
            {
            }
            try
            {
                UIManager manager = UIManager.Instance;
                if (manager != null) manager.CloseFishList();
            }
            catch
            {
            }
        }

        private static UIBreed FindBreedUi()
        {
            try
            {
                GameManager manager = GameManager.Instance;
                if (manager != null && manager.uiBreed != null) return manager.uiBreed;
            }
            catch
            {
            }
            try
            {
                return UnityEngine.Object.FindObjectOfType<UIBreed>();
            }
            catch
            {
                return null;
            }
        }

        private static void ScheduleNormal()
        {
            _phase = Phase.Idle;
            float minutes = SampleMinutes();
            _nextActionAt = Time.time + minutes * 60f;
            if (State.DebugLog) MelonLogger.Msg("[PCFishTool] auto-breed: next check in " + Math.Round(minutes, 1) + " min");
        }

        private static float SampleMinutes()
        {
            double value = MeanMinutes + SigmaMinutes * NormalZ();
            if (value < MinMinutes) value = MinMinutes;
            if (value > MaxMinutes) value = MaxMinutes;
            return (float)value;
        }

        private static float SampleIntervalSeconds()
        {
            double value = IntervalSeconds + IntervalSigmaSeconds * NormalZ();
            if (value < IntervalMinSeconds) value = IntervalMinSeconds;
            if (value > IntervalMaxSeconds) value = IntervalMaxSeconds;
            return (float)value;
        }

        private static double NormalZ()
        {
            double u1 = 1.0 - Random.NextDouble();
            double u2 = 1.0 - Random.NextDouble();
            return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
        }

        private static bool FindPair()
        {
            FishModel high;
            FishModel low;
            return FindPair(out high, out low);
        }

        private static bool FindPair(out FishModel high, out FishModel low)
        {
            high = null;
            low = null;
            GameDataManager data = GameDataManager.Instance;
            if (data == null || data.fishList == null) return false;

            var groups = new Dictionary<long, List<FishModel>>();
            for (int i = 0; i < data.fishList.Count; i++)
            {
                FishModel fish = data.fishList[i];
                if (!IsBreedable(fish)) continue;

                long key = ((long)fish.Level << 32) | (uint)fish.Grade;
                List<FishModel> group;
                if (!groups.TryGetValue(key, out group))
                {
                    group = new List<FishModel>();
                    groups.Add(key, group);
                }
                group.Add(fish);
            }

            List<long> keys = new List<long>(groups.Keys);
            keys.Sort(delegate (long a, long b)
            {
                int gradeA = (int)(a & 0xffffffff);
                int gradeB = (int)(b & 0xffffffff);
                if (gradeA != gradeB) return gradeB.CompareTo(gradeA);
                int levelA = (int)(a >> 32);
                int levelB = (int)(b >> 32);
                return levelB.CompareTo(levelA);
            });

            foreach (long key in keys)
            {
                List<FishModel> group = groups[key];
                if (group.Count < 2) continue;

                group.Sort(delegate (FishModel a, FishModel b)
                {
                    return b.BreedCount.CompareTo(a.BreedCount);
                });

                high = group[0];
                low = group[group.Count - 1];
                return high != null && low != null;
            }

            return false;
        }

        private static bool IsBreedable(FishModel fish)
        {
            if (fish == null) return false;
            if (fish.BreedCount <= 0) return false;
            if (fish.IsLocked) return false;
            if (fish.IsPlaced) return false;
            if (fish.Type == "FS00000") return false;
            if (fish.Growth < 10) return false;
            try
            {
                if (GameUtil.IsFishCooltime(fish.BreedDatetime, fish.BreedNextDatetime)) return false;
            }
            catch
            {
            }
            return true;
        }
    }
}
