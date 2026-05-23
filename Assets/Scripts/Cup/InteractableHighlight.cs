using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// cup / exchange など、レティクルで照準された時に水色ハイライト + raycast 判定を行うオブジェクト共通基底。
/// 子の Renderer をかき集めて MaterialPropertyBlock で _BaseColor / _Color をティントする。
/// </summary>
public abstract class InteractableHighlight : MonoBehaviour
{
    [Header("ハイライト")]
    [Tooltip("ハイライト色 (水色)")]
    public Color highlightColor = new Color(0.35f, 0.85f, 1f, 1f);

    [Range(0f, 1f)]
    [Tooltip("ハイライトの強さ (0=オリジナル, 1=完全置換)")]
    public float highlightStrength = 0.6f;

    [Tooltip("ハイライト対象にする Renderer を自動収集する (false なら highlightTargets を手動指定)")]
    public bool autoCollectRenderers = true;

    [Tooltip("autoCollectRenderers = false の時、手動で対象 Renderer を指定")]
    public Renderer[] highlightTargets;

    [Header("Raycast 設定")]
    [Tooltip("Trigger Collider も raycast 対象に含める (cup の Trigger だけで判定したい時に必要)")]
    public bool includeTriggersInRaycast = true;

    private Renderer[] _renderers;
    private Color[] _origColors;
    private MaterialPropertyBlock _mpb;
    private bool _highlightReady;

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int LegacyColorId = Shader.PropertyToID("_Color");

    protected virtual void Awake()
    {
        SetupHighlight();
    }

    private void SetupHighlight()
    {
        if (autoCollectRenderers)
        {
            var list = new List<Renderer>();
            GetComponentsInChildren<Renderer>(false, list);
            // TextMeshPro 3D の Renderer は除外
            list.RemoveAll(r => r == null || r.GetComponent<TMPro.TextMeshPro>() != null);
            _renderers = list.ToArray();
        }
        else
        {
            _renderers = highlightTargets ?? new Renderer[0];
        }

        _mpb = new MaterialPropertyBlock();
        _origColors = new Color[_renderers.Length];
        for (int i = 0; i < _renderers.Length; i++)
        {
            Color c = Color.white;
            var mat = _renderers[i] != null ? _renderers[i].sharedMaterial : null;
            if (mat != null)
            {
                if (mat.HasProperty(BaseColorId)) c = mat.GetColor(BaseColorId);
                else if (mat.HasProperty(LegacyColorId)) c = mat.GetColor(LegacyColorId);
            }
            _origColors[i] = c;
        }
        _highlightReady = true;
    }

    /// <summary>ハイライト適用。show=false で解除。</summary>
    public void ApplyHighlight(bool show)
    {
        if (!_highlightReady) SetupHighlight();
        if (_renderers == null) return;

        if (!show)
        {
            for (int i = 0; i < _renderers.Length; i++)
                if (_renderers[i] != null) _renderers[i].SetPropertyBlock(null);
            return;
        }

        for (int i = 0; i < _renderers.Length; i++)
        {
            var r = _renderers[i];
            if (r == null) continue;
            Color baseColor = _origColors != null && i < _origColors.Length ? _origColors[i] : Color.white;
            Color tinted = Color.Lerp(baseColor, highlightColor, highlightStrength);
            r.GetPropertyBlock(_mpb);
            _mpb.SetColor(BaseColorId, tinted);
            _mpb.SetColor(LegacyColorId, tinted);
            r.SetPropertyBlock(_mpb);
        }
    }

    /// <summary>Collider に対して raycast を実施し、最近接 hit を返す。Trigger は includeTriggersInRaycast に従う。</summary>
    public bool Raycast(Ray ray, out RaycastHit hit, float maxDistance)
    {
        hit = default;
        bool any = false;
        float best = maxDistance;
        var colliders = GetComponentsInChildren<Collider>(false);
        for (int i = 0; i < colliders.Length; i++)
        {
            var c = colliders[i];
            if (c == null || !c.enabled) continue;
            if (c.isTrigger && !includeTriggersInRaycast) continue;
            if (c.Raycast(ray, out RaycastHit h, best))
            {
                hit = h;
                best = h.distance;
                any = true;
            }
        }
        return any;
    }

    /// <summary>Collider が 1 つも見つからない場合に Console へ警告 (Setup ミスの即時検出用)</summary>
    protected void WarnIfNoColliders()
    {
        var colliders = GetComponentsInChildren<Collider>(true);
        if (colliders == null || colliders.Length == 0)
        {
            Debug.LogWarning($"[{GetType().Name}] '{name}' に Collider がありません。レティクルで照準できません。", this);
        }
    }

    /// <summary>サブクラスから「現在 interactable か」を判定（cup なら『ボールが入っている』など）</summary>
    public virtual bool IsInteractable(CupPickupController pickup) => true;
}
