using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ポストプロセス式アウトライン (Scene ビュー選択の見た目) 用レジストリ。
/// 各ハイライト系コンポーネントが Renderer 群を登録し、OutlineRendererFeature が
/// それを読み取って 「マスク → エッジ検出 → カメラ合成」 という URP 後段パスで輪郭を描く。
///
/// 旧バージョンでは反転ハル方式のメッシュ複製を作っていたが、複雑メッシュで
/// 不自然な輪郭になるため廃止。本ファイルの API シグネチャは互換維持しているので
/// 既存の InteractableHighlight / ShopBallSlot / MouseHoverOutline 等は変更不要。
/// </summary>
public static class OutlineHighlightUtil
{
    public struct OutlineRequest
    {
        public Renderer renderer;
        public Color color;
        public float width; // ピクセル単位 (新方式)
    }

    private static readonly List<OutlineRequest> _active = new List<OutlineRequest>();

    /// <summary>OutlineRendererFeature が毎フレーム参照する、現在ハイライトすべき Renderer 一覧。</summary>
    public static IReadOnlyList<OutlineRequest> Active
    {
        get
        {
            // GameObject が破棄された Renderer を掃除 (Unity-null チェック)
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                if (_active[i].renderer == null) _active.RemoveAt(i);
            }
            return _active;
        }
    }

    /// <summary>
    /// 後方互換: ソース Renderer 群をフィルタしてそのまま返す (旧方式ではメッシュ複製を生成していたが、
    /// 新方式ではそのまま使う)。
    /// </summary>
    public static List<Renderer> CreateOutlineCopies(IEnumerable<Renderer> sources)
    {
        var result = new List<Renderer>();
        if (sources == null) return result;
        foreach (var r in sources)
        {
            if (IsValidOutlineRenderer(r)) result.Add(r);
        }
        return result;
    }

    /// <summary>輪郭の表示/非表示。show=true で登録、false で解除。</summary>
    public static void SetActive(List<Renderer> renderers, bool show, Color color, float width, MaterialPropertyBlock _)
    {
        if (renderers == null) return;

        // 同じ Renderer の既存登録を消す (色変更や重複防止)
        for (int i = _active.Count - 1; i >= 0; i--)
        {
            for (int j = 0; j < renderers.Count; j++)
            {
                if (_active[i].renderer == renderers[j])
                {
                    _active.RemoveAt(i);
                    break;
                }
            }
        }

        if (!show) return;

        // width はピクセル単位。旧コード由来の小さい値 (0.005 等) はピクセル換算で見えなくなるので最低 1 px に
        float effectiveWidth = width < 1f ? 1f : width;
        foreach (var r in renderers)
        {
            if (r == null) continue;
            _active.Add(new OutlineRequest { renderer = r, color = color, width = effectiveWidth });
        }
    }

    /// <summary>Renderer リストから TextMeshPro / Particle / Trail / Line / Billboard を除外。</summary>
    public static Renderer[] FilterRenderers(IList<Renderer> all)
    {
        var list = new List<Renderer>(all.Count);
        foreach (var r in all)
        {
            if (IsValidOutlineRenderer(r)) list.Add(r);
        }
        return list.ToArray();
    }

    private static bool IsValidOutlineRenderer(Renderer r)
    {
        if (r == null) return false;
        if (r.GetComponent<TMPro.TextMeshPro>() != null) return false;
        if (r is ParticleSystemRenderer) return false;
        if (r is TrailRenderer) return false;
        if (r is LineRenderer) return false;
        if (r is BillboardRenderer) return false;
        return true;
    }
}
