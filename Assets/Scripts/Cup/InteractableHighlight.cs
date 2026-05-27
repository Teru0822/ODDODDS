using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// cup / exchange / typewriter など、レティクルで照準された時にアウトライン (輪郭) ハイライト + raycast 判定を行う共通基底。
/// 各 Mesh/SkinnedMeshRenderer の複製を子に生成し、Inverted Hull アウトラインシェーダで描画。
/// ApplyHighlight(true/false) で複製の active を切り替える。
/// </summary>
public abstract class InteractableHighlight : MonoBehaviour
{
    [Header("ハイライト (アウトライン)")]
    [Tooltip("輪郭の色")]
    public Color highlightColor = new Color(0.35f, 0.85f, 1f, 1f);

    [Tooltip("輪郭の太さ (オブジェクト空間で法線に沿った押し出し量)")]
    [Range(0f, 0.05f)]
    public float outlineWidth = 0.005f;

    [Tooltip("ハイライト対象にする Renderer を自動収集する (false なら highlightTargets を手動指定)")]
    public bool autoCollectRenderers = true;

    [Tooltip("autoCollectRenderers = false の時、手動で対象 Renderer を指定")]
    public Renderer[] highlightTargets;

    [Header("Raycast 設定")]
    [Tooltip("Trigger Collider も raycast 対象に含める (cup の Trigger だけで判定したい時に必要)")]
    public bool includeTriggersInRaycast = true;

    private List<Renderer> _outlineRenderers;
    private MaterialPropertyBlock _mpb;
    private bool _ready;

    protected virtual void Awake()
    {
        SetupHighlight();
    }

    private void SetupHighlight()
    {
        if (_ready) return;

        Renderer[] sources;
        if (autoCollectRenderers)
        {
            var list = new List<Renderer>();
            GetComponentsInChildren<Renderer>(true, list);
            sources = OutlineHighlightUtil.FilterRenderers(list);
        }
        else
        {
            sources = highlightTargets ?? new Renderer[0];
        }

        _outlineRenderers = OutlineHighlightUtil.CreateOutlineCopies(sources);
        _mpb = new MaterialPropertyBlock();
        _ready = true;
        ApplyHighlight(false);
    }

    /// <summary>ハイライト適用。show=false で解除。</summary>
    public void ApplyHighlight(bool show)
    {
        if (!_ready) SetupHighlight();
        OutlineHighlightUtil.SetActive(_outlineRenderers, show, highlightColor, outlineWidth, _mpb);
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

    /// <summary>サブクラスから「現在 interactable か」を判定 (cup なら『ボールが入っている』など)</summary>
    public virtual bool IsInteractable(CupPickupController pickup) => true;

    /// <summary>レティクルがこのオブジェクトを照準し始めた時に呼ばれる (CupPickupController から)</summary>
    public virtual void OnLookEnter() { }

    /// <summary>レティクルがこのオブジェクトから外れた時に呼ばれる (CupPickupController から)</summary>
    public virtual void OnLookExit() { }
}
