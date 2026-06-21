using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 「newpinballvec」にアタッチする回転ノブ。
/// PinballSessionController.PinBallState == 1（AtP1）の間だけ操作可能。
///
///   - マウスカーソルを乗せると輪郭をハイライト（hoverColor）。
///   - 上で左ボタンを押すと「つかむ」。ドラッグするとローカル Z 軸まわりに回転する。
///   - 回転角は初期姿勢を 0° として minAngle〜maxAngle（既定 -30〜+30）にクランプ。
///   - ポインタの公転に追従する「ノブ」式（ピボット = この Transform の原点、回転軸 = ローカル Z）。
///
/// 輪郭は既存の OutlineHighlightUtil（ポストプロセス式）を流用。
/// </summary>
[DisallowMultipleComponent]
public class PinballVecRotator : MonoBehaviour
{
    [Header("セッション参照")]
    [Tooltip("State を読む PinballSessionController。null なら自動取得")]
    public PinballSessionController session;

    [Tooltip("マウスレイ発信元のカメラ。null なら Camera.main")]
    public Camera lookCamera;

    [Header("ハイライト（輪郭）")]
    [Tooltip("ホバー中の輪郭色")]
    public Color hoverColor = new Color(0.2f, 0.6f, 1f, 1f);

    [Tooltip("輪郭の太さ（ピクセル）")]
    [Min(1f)]
    public float outlineWidth = 4f;

    [Tooltip("Raycast の最大距離（m）")]
    public float maxDistance = 100f;

    [Header("回転範囲（ローカル Z 軸）")]
    [Tooltip("初期姿勢を 0° とした最小角度")]
    public float minAngle = -30f;

    [Tooltip("初期姿勢を 0° とした最大角度")]
    public float maxAngle = 30f;

    private Collider[] _colliders;
    private List<Renderer> _outlineRenderers;
    private MaterialPropertyBlock _mpb;

    private Quaternion _baseLocalRot;  // 初期ローカル姿勢（0°）
    private float _currentAngle;       // base からの符号付き角度
    private bool _hovered;
    private bool _dragging;
    private Vector3 _grabDir;          // つかんだ瞬間のピボット→ポインタ方向（回転面上）
    private float _grabAngle;          // つかんだ瞬間の _currentAngle

    /// <summary>初期姿勢を 0° とした現在の符号付き回転角（度）。</summary>
    public float CurrentAngle => _currentAngle;

    /// <summary>角度（初期姿勢からの度）を直接設定する。minAngle〜maxAngle にクランプして即適用。</summary>
    public void SetAngle(float angle)
    {
        _currentAngle = Mathf.Clamp(angle, minAngle, maxAngle);
        transform.localRotation = _baseLocalRot * Quaternion.AngleAxis(_currentAngle, Vector3.forward);
    }

    private void Awake()
    {
        if (session == null) session = FindAnyObjectByType<PinballSessionController>();
        _colliders = GetComponentsInChildren<Collider>(true);
        if (_colliders == null || _colliders.Length == 0)
            Debug.LogWarning($"[PinballVecRotator] '{name}' に Collider がありません。マウス判定できません。", this);

        var list = new List<Renderer>();
        GetComponentsInChildren<Renderer>(true, list);
        _outlineRenderers = new List<Renderer>(OutlineHighlightUtil.FilterRenderers(list));
        _mpb = new MaterialPropertyBlock();
        _baseLocalRot = transform.localRotation;
    }

    private void OnDisable()
    {
        _hovered = false;
        _dragging = false;
        SetOutline(false);
    }

    private void Update()
    {
        bool active = session != null && session.PinBallState == 1;
        if (!active)
        {
            if (_dragging || _hovered) { _dragging = false; _hovered = false; }
            SetOutline(false);
            return;
        }

        Camera cam = lookCamera != null ? lookCamera : Camera.main;
        if (cam == null || Mouse.current == null) { SetOutline(false); return; }

        Vector2 mousePos = Mouse.current.position.ReadValue();
        Ray ray = cam.ScreenPointToRay(mousePos);

        if (!_dragging)
        {
            _hovered = (_colliders != null && _colliders.Length > 0) && CheckOver(ray);
        }
        SetOutline(_hovered || _dragging);

        var lmb = Mouse.current.leftButton;

        // つかむ
        if (lmb.wasPressedThisFrame && _hovered)
        {
            if (TryGetPointerDir(ray, out Vector3 dir))
            {
                _dragging = true;
                _grabDir = dir;
                _grabAngle = _currentAngle;
            }
        }

        // 回す
        if (_dragging)
        {
            if (lmb.isPressed)
            {
                if (TryGetPointerDir(ray, out Vector3 dir))
                {
                    Vector3 axis = GetAxis();
                    float delta = Vector3.SignedAngle(_grabDir, dir, axis);
                    _currentAngle = Mathf.Clamp(_grabAngle + delta, minAngle, maxAngle);
                    transform.localRotation = _baseLocalRot * Quaternion.AngleAxis(_currentAngle, Vector3.forward);
                }
            }
            else
            {
                _dragging = false;
            }
        }
    }

    /// <summary>回転軸（ワールド）。ローカル Z まわりの回転では Z 方向は不変なので transform.forward で安定。</summary>
    private Vector3 GetAxis() => transform.forward.normalized;

    /// <summary>マウスレイを「ピボットを通り軸を法線とする平面」に当て、ピボット→交点方向（面上）を得る。</summary>
    private bool TryGetPointerDir(Ray ray, out Vector3 dir)
    {
        dir = Vector3.zero;
        Vector3 axis = GetAxis();
        Vector3 pivot = transform.position;
        Plane plane = new Plane(axis, pivot);
        if (!plane.Raycast(ray, out float enter)) return false;
        Vector3 hit = ray.GetPoint(enter);
        Vector3 v = Vector3.ProjectOnPlane(hit - pivot, axis);
        if (v.sqrMagnitude < 1e-8f) return false;
        dir = v.normalized;
        return true;
    }

    private bool CheckOver(Ray ray)
    {
        for (int i = 0; i < _colliders.Length; i++)
        {
            var c = _colliders[i];
            if (c == null || !c.enabled) continue;
            if (c.Raycast(ray, out RaycastHit _, maxDistance)) return true;
        }
        return false;
    }

    private void SetOutline(bool show)
    {
        OutlineHighlightUtil.SetActive(_outlineRenderers, show, hoverColor, outlineWidth, _mpb);
    }
}
