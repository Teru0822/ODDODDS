using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// マウスカーソルが当たったオブジェクトの輪郭をハイライトするスクリプト。
/// Input System Package 環境用。アタッチしたオブジェクトとその子の Collider に対して
/// カメラ → マウス座標のレイで判定し、当たっている間 OutlineHighlightUtil で輪郭表示する。
/// 輪郭は既存の Hidden/InteractableOutline シェーダ (ステンシルマスク式) を流用。
/// </summary>
[DisallowMultipleComponent]
public class MouseHoverOutline : MonoBehaviour
{
    [Header("ハイライト (アウトライン)")]
    [Tooltip("輪郭の色")]
    public Color outlineColor = new Color(0.2f, 0.5f, 1f, 1f);

    [Tooltip("輪郭の太さ (法線方向の押し出し量)")]
    [Range(0f, 0.05f)]
    public float outlineWidth = 0.005f;

    [Tooltip("対象 Renderer を自動収集 (false なら highlightTargets を手動指定)")]
    public bool autoCollectRenderers = true;

    [Tooltip("autoCollectRenderers = false 時の手動指定")]
    public Renderer[] highlightTargets;

    [Header("マウス検知")]
    [Tooltip("マウスレイ発信元のカメラ。null なら Camera.main")]
    public Camera hoverCamera;

    [Tooltip("Raycast の最大距離 (m)")]
    public float maxDistance = 1000f;

    [Tooltip("他オブジェクトに遮られていてもハイライトする (false=オクルージョン考慮)")]
    public bool ignoreOcclusion = false;

    [Tooltip("Physics.Raycast に使う LayerMask (オクルージョン考慮時に有効)")]
    public LayerMask raycastLayerMask = ~0; // 全レイヤー

    [Header("デバッグ")]
    [Tooltip("Awake/ホバー切替/未取得の警告を Console に出力")]
    public bool logEvents = false;

    private Collider[] _colliders;
    private List<Renderer> _outlineRenderers;
    private MaterialPropertyBlock _mpb;
    private bool _ready;
    private bool _hovered;

    /// <summary>現在マウスカーソルが乗っているか (TitlePlayButton 等がクリック判定で参照する)</summary>
    public bool IsHovered => _hovered;

    private void Awake()
    {
        Setup();
    }

    private void Setup()
    {
        if (_ready) return;
        _colliders = GetComponentsInChildren<Collider>(true);

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
        Apply(false);
        if (logEvents)
        {
            Debug.Log($"[MouseHoverOutline] '{name}' setup: colliders={_colliders.Length}, renderers={_outlineRenderers.Count}", this);
        }
    }

    private void OnDisable()
    {
        if (_hovered)
        {
            _hovered = false;
            Apply(false);
        }
    }

    private void Update()
    {
        if (hoverCamera == null) hoverCamera = Camera.main;
        if (hoverCamera == null)
        {
            if (logEvents && Time.frameCount % 60 == 0)
                Debug.LogWarning($"[MouseHoverOutline] '{name}': カメラ未取得 (Camera.main が null)。タイトルシーンのカメラに 'MainCamera' タグを付けるか、hoverCamera を明示指定してください", this);
            return;
        }
        if (Mouse.current == null) return;
        if (_colliders == null || _colliders.Length == 0)
        {
            if (logEvents && Time.frameCount % 60 == 0)
                Debug.LogWarning($"[MouseHoverOutline] '{name}': Collider が見つかりません。Mesh Collider 等を付けてください", this);
            return;
        }

        Vector2 mousePos = Mouse.current.position.ReadValue();
        Ray ray = hoverCamera.ScreenPointToRay(mousePos);

        bool over = ignoreOcclusion ? CheckOverSelfDirect(ray) : CheckOverSelfWithOcclusion(ray);
        if (over != _hovered)
        {
            _hovered = over;
            Apply(_hovered);
            if (logEvents) Debug.Log($"[MouseHoverOutline] '{name}': hover={_hovered}", this);
        }
    }

    private bool CheckOverSelfDirect(Ray ray)
    {
        for (int i = 0; i < _colliders.Length; i++)
        {
            var c = _colliders[i];
            if (c == null || !c.enabled) continue;
            if (c.Raycast(ray, out RaycastHit _, maxDistance)) return true;
        }
        return false;
    }

    private bool CheckOverSelfWithOcclusion(Ray ray)
    {
        if (!Physics.Raycast(ray, out RaycastHit hit, maxDistance, raycastLayerMask, QueryTriggerInteraction.Collide)) return false;
        for (int i = 0; i < _colliders.Length; i++)
        {
            if (hit.collider == _colliders[i]) return true;
        }
        return false;
    }

    private void Apply(bool show)
    {
        if (!_ready) Setup();
        OutlineHighlightUtil.SetActive(_outlineRenderers, show, outlineColor, outlineWidth, _mpb);
    }
}
