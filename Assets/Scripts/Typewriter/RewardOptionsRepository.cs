using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Resources/RewardOptions.txt から報酬テキストを読み込み、未選択からランダムに 2 個ピックする。
/// シーン間でも選択履歴を保持するため static のままにしてある。
/// </summary>
public static class RewardOptionsRepository
{
    private const string ResourcePath = "RewardOptions";

    private static List<string> _allOptions;
    private static readonly HashSet<string> _selected = new HashSet<string>(StringComparer.Ordinal);

    public static IReadOnlyList<string> AllOptions
    {
        get { EnsureLoaded(); return _allOptions; }
    }

    public static int RemainingCount
    {
        get
        {
            EnsureLoaded();
            int n = 0;
            foreach (var opt in _allOptions) if (!_selected.Contains(opt)) n++;
            return n;
        }
    }

    public static void EnsureLoaded()
    {
        if (_allOptions != null) return;
        _allOptions = new List<string>();
        var asset = Resources.Load<TextAsset>(ResourcePath);
        if (asset == null)
        {
            Debug.LogWarning($"[RewardOptionsRepository] Resources/{ResourcePath}.txt が見つかりません");
            return;
        }
        var lines = asset.text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var raw in lines)
        {
            string line = raw.Trim();
            if (line.Length == 0) continue;
            if (line.StartsWith("#")) continue;
            _allOptions.Add(line);
        }
    }

    /// <summary>未選択の選択肢から重複なくランダムに最大 count 個返す。</summary>
    public static List<string> PickRandom(int count = 2)
    {
        EnsureLoaded();
        var pool = new List<string>();
        foreach (var opt in _allOptions) if (!_selected.Contains(opt)) pool.Add(opt);

        var result = new List<string>();
        for (int i = 0; i < count && pool.Count > 0; i++)
        {
            int idx = UnityEngine.Random.Range(0, pool.Count);
            result.Add(pool[idx]);
            pool.RemoveAt(idx);
        }
        return result;
    }

    public static void MarkSelected(string option)
    {
        if (string.IsNullOrEmpty(option)) return;
        _selected.Add(option);
    }

    /// <summary>選択履歴を全クリア (ニューゲーム時などに呼ぶ)</summary>
    public static void ResetSelections()
    {
        _selected.Clear();
    }

    /// <summary>強制的に再読み込み (Editor デバッグ用)</summary>
    public static void ReloadFromResources()
    {
        _allOptions = null;
        EnsureLoaded();
    }
}
