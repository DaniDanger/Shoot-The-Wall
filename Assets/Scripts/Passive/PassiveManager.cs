using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class PassiveManager : MonoBehaviour
{
    private class GeneratorRuntime
    {
        public PassiveGeneratorController ctrl;
        public float timer;
    }

    [Header("Generators (drag scene refs here)")]
    public List<PassiveGeneratorController> generators = new List<PassiveGeneratorController>();

    private readonly Dictionary<string, GeneratorRuntime> runtimeByKey = new Dictionary<string, GeneratorRuntime>(16);

    private static string LevelKey(PassiveGeneratorController c) => $"Passive_Wall{c.wallIndex}_{c.generatorId}_Level";
    private static string ProgressKey(PassiveGeneratorController c) => $"Passive_Wall{c.wallIndex}_{c.generatorId}_Progress";
    private static string MakeKey(int wallIndex, string id) => $"{wallIndex}:{id}";

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
        BuildRuntime();
    }

    private void BuildRuntime()
    {
        runtimeByKey.Clear();
        if (generators == null) return;
        for (int i = 0; i < generators.Count; i++)
        {
            var c = generators[i];
            if (c == null || string.IsNullOrEmpty(c.generatorId)) continue;
            var key = MakeKey(c.wallIndex, c.generatorId);
            if (runtimeByKey.ContainsKey(key)) continue;
            var rt = new GeneratorRuntime { ctrl = c, timer = PlayerPrefs.GetFloat(ProgressKey(c), 0f) };
            runtimeByKey[key] = rt;
        }
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        foreach (var kv in runtimeByKey)
        {
            var rt = kv.Value;
            var c = rt.ctrl;
            int lvl = GetLevelInternal(c);
            if (lvl <= 0)
            {
                if (rt.timer != 0f)
                {
                    rt.timer = 0f;
                    PlayerPrefs.SetFloat(ProgressKey(c), 0f);
                }
                continue;
            }

            float interval = Mathf.Max(0.0001f, c.tickInterval);
            rt.timer += Mathf.Max(0f, dt);
            if (rt.timer >= interval)
            {
                int ticks = Mathf.FloorToInt(rt.timer / interval);
                rt.timer -= ticks * interval;
                float fYieldPerTick = Mathf.Max(0f, ComputeYieldPerTick(c, lvl));
                if (c.kind == PassiveGeneratorController.GeneratorKind.Income)
                {
                    int total = Mathf.Max(0, Mathf.RoundToInt(fYieldPerTick) * ticks);
                    if (total > 0) CurrencyStore.AddToTotal(total);
                }
                else if (c.kind == PassiveGeneratorController.GeneratorKind.Damage)
                {
                    // Reserved for future damage passive (not used now)
                }
                else // Amplifier and others
                {
                    // For amplifier: add fractional bonus per tick
                    float add = fYieldPerTick * ticks;
                    if (add > 0f)
                    {
                        PassiveStore.AddWall1AmplifierBonus(add);
                        try { Debug.Log($"[AmplifierTick] +{add:0.###} -> total {PassiveStore.GetWall1AmplifierBonus():0.###}"); } catch { }
                    }
                }
            }
            PlayerPrefs.SetFloat(ProgressKey(c), Mathf.Max(0f, rt.timer));
        }
        PlayerPrefs.Save();
    }

    private static float ComputeYieldPerTick(PassiveGeneratorController c, int level)
    {
        if (c.yieldMode == PassiveGeneratorController.YieldMode.Fixed) return Mathf.Max(0f, c.baseYield);
        return Mathf.Max(0f, c.baseYield + c.yieldPerLevel * Mathf.Max(0, level));
    }

    private static int ComputeCost(PassiveGeneratorController c, int currentLevel)
    {
        float baseC = Mathf.Max(1, c.costBase);
        float growth = Mathf.Max(1f, c.costGrowth);
        float f = baseC * Mathf.Pow(growth, Mathf.Max(0, currentLevel));
        return Mathf.Max(1, Mathf.CeilToInt(f));
    }

    private static int GetLevelInternal(PassiveGeneratorController c)
    {
        return PlayerPrefs.GetInt(LevelKey(c), 0);
    }

    private static void SetLevelInternal(PassiveGeneratorController c, int level)
    {
        PlayerPrefs.SetInt(LevelKey(c), Mathf.Max(0, level));
        PlayerPrefs.Save();
    }

    // Public API for UI
    public bool TryGetStatus(int wallIndex, string id, out int level, out int nextCost, out float timer, out float interval)
    {
        var key = MakeKey(wallIndex, id);
        if (!runtimeByKey.TryGetValue(key, out var rt))
        {
            level = 0; nextCost = int.MaxValue; timer = 0f; interval = 1f; return false;
        }
        var c = rt.ctrl;
        level = GetLevelInternal(c);
        nextCost = ComputeCost(c, level);
        timer = rt.timer;
        interval = Mathf.Max(0.0001f, c.tickInterval);
        return true;
    }

    public bool TryBuyLevel(int wallIndex, string id)
    {
        var key = MakeKey(wallIndex, id);
        if (!runtimeByKey.TryGetValue(key, out var rt)) return false;
        var c = rt.ctrl;
        int lvl = GetLevelInternal(c);
        int cost = ComputeCost(c, lvl);
        bool paid = c.costCurrency == PassiveGeneratorController.CostCurrencyType.WallCurrency
            ? PassiveStore.TrySpendWall1Currency(cost)
            : CurrencyStore.TrySpendFromTotal(cost);
        if (!paid) return false;
        SetLevelInternal(c, lvl + 1);
        return true;
    }

    public int GetLevel(int wallIndex, string id)
    {
        var key = MakeKey(wallIndex, id);
        if (!runtimeByKey.TryGetValue(key, out var rt)) return 0;
        return GetLevelInternal(rt.ctrl);
    }
}


