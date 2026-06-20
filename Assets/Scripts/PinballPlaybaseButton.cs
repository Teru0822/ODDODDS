using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 「newpinballplaybase2」にアタッチする発射ボタン。
/// PinballSessionController.PinBallState == 1（AtP1）の間だけ操作可能。
///
///   - マウスカーソルを乗せると輪郭をハイライト（hoverColor）。
///   - 押し込み（左ボタン press）中はローカル -Z 方向へ pressDepth だけ沈む（Slerp 補間）。
///   - 離す（release）と元の位置へ浮き上がる。
///   - 「乗っている状態で押して、乗っている状態で離す」= クリック確定 → session.LaunchBall()。
///
/// 輪郭は既存の OutlineHighlightUtil（ポストプロセス式）を流用。
/// </summary>
[DisallowMultipleComponent]
public class PinballPlaybaseButton : MonoBehaviour
{
    [Header("セッション参照")]
    [Tooltip("State を読み、クリックで LaunchBall() を呼ぶ PinballSessionController。null なら自動取得")]
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

    [Header("押し込み（ローカル -Z）")]
    [Tooltip("押し込んだときローカル -Z 方向へ沈む距離（m）")]
    public float pressDepth = 0.01f;

    [Tooltip("沈む／戻るの所要時間（秒）")]
    [Min(0.0001f)]
    public float pressDuration = 0.08f;

    private Collider[] _colliders;
    private List<Renderer> _outlineRenderers;
    private MaterialPropertyBlock _mpb;

    private Vector3 _basePos;       // 静止時のローカル位置
    private bool _hovered;
    private bool _pressedOnThis;    // この上で press したか（release まで維持）
    private float _pressProgress;   // 0=rest, 1=沈み切り

    private void Awake()
    {
        if (session == null) session = FindAnyObjectByType<PinballSessionController>();
        _colliders = GetComponentsInChildren<Collider>(true);
        if (_colliders == null || _colliders.Length == 0)
            Debug.LogWarning($"[PinballPlaybaseButton] '{name}' に Collider がありません。マウス判定できません。", this);

        var list = new List<Renderer>();
        GetComponentsInChildren<Renderer>(true, list);
        _outlineRenderers = new List<Renderer>(OutlineHighlightUtil.FilterRenderers(list));
        _mpb = new MaterialPropertyBlock();
        _basePos = transform.localPosition;
    }

    private void OnDisable()
    {
        ResetVisualState();
    }

    private void Update()
    {
        bool active = session != null && session.PinBallState == 1;

        if (!active)
        {
            // State==1 以外では操作不可・輪郭オフ・押し込み解除
            if (_hovered || _pressedOnThis) ResetInteractionFlags();
            UpdatePressMotion(false);
            SetOutline(false);
            return;
        }

        Camera cam = lookCamera != null ? lookCamera : Camera.main;
        bool over = false;
        if (cam != null && Mouse.current != null && _colliders != null && _colliders.Length > 0)
        {
            Vector2 mousePos = Mouse.current.position.ReadValue();
            Ray ray = cam.ScreenPointToRay(mousePos);
            over = CheckOver(ray);
        }
        _hovered = over;
        SetOutline(_hovered);

        var lmb = Mouse.current != null ? Mouse.current.leftButton : null;
        if (lmb != null)
        {
            if (lmb.wasPressedThisFrame && _hovered) _pressedOnThis = true;
            if (lmb.wasReleasedThisFrame)
            {
                if (_pressedOnThis && _hovered && session != null)
                {
                    session.LaunchBall(); // クリック確定 → 発射
                }
                _pressedOnThis = false;
            }
        }

        // 押している間（離すまで）沈み込みを維持
        UpdatePressMotion(_pressedOnThis);
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

    private void UpdatePressMotion(bool pressed)
    {
        float dir = pressed ? 1f : -1f;
        _pressProgress = Mathf.Clamp01(_pressProgress + dir * Time.deltaTime / pressDuration);

        // ターゲット自身のローカル -Z 軸を親空間ベクトルへ変換した方向に沈める
        Vector3 minusZInParent = transform.localRotation * Vector3.back;
        Vector3 pressedPos = _basePos + minusZInParent * pressDepth;
        transform.localPosition = Vector3.Slerp(_basePos, pressedPos, _pressProgress);
    }

    private void SetOutline(bool show)
    {
        OutlineHighlightUtil.SetActive(_outlineRenderers, show, hoverColor, outlineWidth, _mpb);
    }

    private void ResetInteractionFlags()
    {
        _hovered = false;
        _pressedOnThis = false;
    }

    private void ResetVisualState()
    {
        ResetInteractionFlags();
        _pressProgress = 0f;
        transform.localPosition = _basePos;
        SetOutline(false);
    }
}
