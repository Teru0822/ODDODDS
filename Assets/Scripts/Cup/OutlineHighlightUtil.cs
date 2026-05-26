using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// ステンシルマスク + Inverted Hull 方式のアウトライン用ユーティリティ。
/// オブジェクトの全パーツを先にステンシルへ書き込み、その外側にだけ輪郭を描くことで
/// 「オブジェクト全体の一番外の輪郭だけ」をハイライトする。
/// InteractableHighlight / ShopBallSlot など複数のハイライト系コンポーネントが共有して使う。
/// </summary>
public static class OutlineHighlightUtil
{
    public const string OutlineShaderName = "Hidden/InteractableOutline";
    public const string MaskShaderName = "Hidden/InteractableOutlineMask";
    public static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
    public static readonly int OutlineWidthId = Shader.PropertyToID("_OutlineWidth");

    private static Material _sharedOutline;
    private static Material _sharedMask;

    public static Material SharedOutlineMaterial
    {
        get
        {
            if (_sharedOutline != null) return _sharedOutline;
            var sh = Shader.Find(OutlineShaderName);
            if (sh == null)
            {
                Debug.LogWarning($"[OutlineHighlightUtil] Shader '{OutlineShaderName}' が見つかりません。Assets/Shaders/InteractableOutline.shader を含めるか Graphics 設定の 'Always Included Shaders' に追加してください");
                return null;
            }
            _sharedOutline = new Material(sh) { name = "InteractableOutline (shared)" };
            return _sharedOutline;
        }
    }

    public static Material SharedMaskMaterial
    {
        get
        {
            if (_sharedMask != null) return _sharedMask;
            var sh = Shader.Find(MaskShaderName);
            if (sh == null)
            {
                Debug.LogWarning($"[OutlineHighlightUtil] Shader '{MaskShaderName}' が見つかりません。Assets/Shaders/InteractableOutlineMask.shader を含めるか Graphics 設定の 'Always Included Shaders' に追加してください");
                return null;
            }
            _sharedMask = new Material(sh) { name = "InteractableOutlineMask (shared)" };
            return _sharedMask;
        }
    }

    /// <summary>
    /// 指定 Renderer 群について、ステンシルマスク + アウトラインの複製 Renderer を子に作成して返す (Active=false)。
    /// 戻り値には mask 複製と outline 複製の両方が含まれる (SetActive で一括トグル)。
    /// </summary>
    public static List<Renderer> CreateOutlineCopies(IEnumerable<Renderer> sources)
    {
        var result = new List<Renderer>();
        var outlineMat = SharedOutlineMaterial;
        var maskMat = SharedMaskMaterial;
        if (outlineMat == null || sources == null) return result;

        foreach (var src in sources)
        {
            if (src == null) continue;
            if (maskMat != null)
            {
                var m = CreateCopy(src, maskMat, "OutlineMask");
                if (m != null) result.Add(m);
            }
            var o = CreateCopy(src, outlineMat, "Outline");
            if (o != null) result.Add(o);
        }
        return result;
    }

    private static Renderer CreateCopy(Renderer source, Material mat, string suffix)
    {
        var srcGO = source.gameObject;
        var go = new GameObject($"{srcGO.name}__{suffix}");
        go.transform.SetParent(srcGO.transform, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;
        go.layer = srcGO.layer;

        Renderer outR = null;
        if (source is MeshRenderer)
        {
            var srcMF = srcGO.GetComponent<MeshFilter>();
            if (srcMF == null || srcMF.sharedMesh == null) { Object.Destroy(go); return null; }
            go.AddComponent<MeshFilter>().sharedMesh = srcMF.sharedMesh;
            outR = go.AddComponent<MeshRenderer>();
        }
        else if (source is SkinnedMeshRenderer srcSMR)
        {
            if (srcSMR.sharedMesh == null) { Object.Destroy(go); return null; }
            var smr = go.AddComponent<SkinnedMeshRenderer>();
            smr.sharedMesh = srcSMR.sharedMesh;
            smr.bones = srcSMR.bones;
            smr.rootBone = srcSMR.rootBone;
            smr.localBounds = srcSMR.localBounds;
            outR = smr;
        }
        else
        {
            Object.Destroy(go);
            return null;
        }

        outR.sharedMaterial = mat;
        outR.shadowCastingMode = ShadowCastingMode.Off;
        outR.receiveShadows = false;
        outR.lightProbeUsage = LightProbeUsage.Off;
        outR.reflectionProbeUsage = ReflectionProbeUsage.Off;
        outR.allowOcclusionWhenDynamic = false;
        go.SetActive(false);
        return outR;
    }

    /// <summary>アウトライン/マスク複製の表示・非表示と色・幅を一括設定。</summary>
    public static void SetActive(List<Renderer> outlines, bool show, Color color, float width, MaterialPropertyBlock mpb)
    {
        if (outlines == null) return;
        for (int i = 0; i < outlines.Count; i++)
        {
            var r = outlines[i];
            if (r == null) continue;
            r.gameObject.SetActive(show);
            if (show)
            {
                if (mpb == null) mpb = new MaterialPropertyBlock();
                r.GetPropertyBlock(mpb);
                mpb.SetColor(OutlineColorId, color);
                mpb.SetFloat(OutlineWidthId, width);
                r.SetPropertyBlock(mpb);
            }
        }
    }

    /// <summary>Renderer リストから TextMeshPro / Particle / Trail / Line / Billboard を除外して MeshRenderer / SkinnedMeshRenderer のみを返す。</summary>
    public static Renderer[] FilterRenderers(IList<Renderer> all)
    {
        var list = new List<Renderer>(all.Count);
        foreach (var r in all)
        {
            if (r == null) continue;
            if (r.GetComponent<TMPro.TextMeshPro>() != null) continue;
            if (r is ParticleSystemRenderer) continue;
            if (r is TrailRenderer) continue;
            if (r is LineRenderer) continue;
            if (r is BillboardRenderer) continue;
            list.Add(r);
        }
        return list.ToArray();
    }
}
