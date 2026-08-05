using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
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

    [Header("ホバー SE")]
    [Tooltip("ホバー開始時 (アウトラインが出る瞬間) に再生する AudioClip。null なら無音")]
    public AudioClip hoverEnterClip;

    [Tooltip("再生用 AudioSource。null なら自身に AddComponent して使う")]
    public AudioSource audioSource;

    [Range(0f, 5f)]
    [Tooltip("SE ボリューム (1 を超えるブースト可)")]
    public float hoverVolume = 1f;

    [Tooltip("再生ピッチのランダム範囲 (x=最小, y=最大)。x=y で固定")]
    public Vector2 hoverPitchRange = new Vector2(1f, 1f);

    [Tooltip("再生開始オフセット (秒)。元音源の先頭にある無音区間をスキップしてラグを消す。0 で先頭から")]
    [Min(0f)]
    public float hoverEnterStartOffset = 0f;

    [Header("クリック SE (Press / Release)")]
    [Tooltip("左クリック押し込み時 (アウトライン上で press) の SE")]
    public AudioClip pressDownClip;

    [Tooltip("press SE のピッチランダム範囲 (x=min, y=max、x=y で固定)")]
    public Vector2 pressDownPitchRange = new Vector2(1f, 1f);

    [Tooltip("press SE の先頭オフセット (秒、無音スキップ)")]
    [Min(0f)]
    public float pressDownStartOffset = 0f;

    [Tooltip("左クリック離す瞬間 (press もアウトライン上だった場合のみ) の SE")]
    public AudioClip releaseClip;

    [Tooltip("release SE のピッチランダム範囲")]
    public Vector2 releasePitchRange = new Vector2(1f, 1f);

    [Tooltip("release SE の先頭オフセット (秒、無音スキップ)")]
    [Min(0f)]
    public float releaseStartOffset = 0f;

    [Range(0f, 5f)]
    [Tooltip("クリック系 SE のボリューム (1超でブースト可)")]
    public float clickVolume = 1f;

    [Header("プレス時の押し込み移動 (ローカル X 軸、Slerp)")]
    [Tooltip("ON にすると press-while-hovered 中だけ対象を localPosition の X 軸方向にずらす (Slerp 補間)。release で元位置に戻る")]
    public bool pressMoveEnabled = false;

    [Tooltip("動かす対象の Transform。null なら自身 (MouseHoverOutline がアタッチされた GameObject)")]
    public Transform pressMoveTarget;

    [Tooltip("ローカル X 軸方向の移動量 (m)。負値で逆向き")]
    public float pressMoveLocalX = 0.01f;

    [Tooltip("press 押し込み完了 / release で戻る までの所要時間 (秒)")]
    [Min(0.0001f)]
    public float pressMoveDuration = 0.1f;

    [Tooltip("press から release まで押し込みを維持する。ボタン自身が動いて hover が一時的に外れても戻らないようにする (標準的なボタン挙動)。OFF にすると hover を外した瞬間に戻る")]
    public bool pressMoveLatchUntilRelease = true;

    [Header("クリック確定イベント (press → release が両方アウトライン上)")]
    [Tooltip("Inspector でクリック時の処理を直接バインドできる UnityEvent")]
    public UnityEvent onClicked;

    /// <summary>C# 側でクリックを購読するための event (TitlePlayButton 等が使う)。</summary>
    public event System.Action OnClicked;

    [Tooltip("SE の空間ブレンド (0=2D 距離無関係, 1=3D 距離減衰あり)")]
    [Range(0f, 1f)]
    public float audioSpatialBlend = 0f;

    [Header("デバッグ")]
    [Tooltip("Awake/ホバー切替/未取得の警告を Console に出力")]
    public bool logEvents = false;

    private Collider[] _colliders;
    private bool _pressedOnThis; // press-while-hovered で true、release または hover 外しで false

    // 押し込み移動用
    private float _pressMoveProgress;       // 0=rest, 1=fully pressed
    private Vector3 _pressMoveBase;         // 現在の土台位置 (FloatingMotion 等が書いた値)
    private Vector3 _pressMoveLastSetPos;   // 前フレーム末に自分が書いた値
    private bool _pressMoveInitialized;
    private List<Renderer> _outlineRenderers;
    private MaterialPropertyBlock _mpb;
    private bool _ready;
    private bool _hovered;

    /// <summary>現在マウスカーソルが乗っているか (TitlePlayButton 等がクリック判定で参照する)</summary>
    public bool IsHovered => _hovered;
    private bool _isOpenUI = false;

    /// <summary>現在UIが表示されているか（GameUIやSettingUIを開いているときは、Update処理を停止させる）</summary>
    public bool IsOpenUI {get{return _isOpenUI;} set{_isOpenUI = value;}}
    

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
        EnsureAudioSource();
        _ready = true;
        Apply(false);
        if (logEvents)
        {
            Debug.Log($"[MouseHoverOutline] '{name}' setup: colliders={_colliders.Length}, renderers={_outlineRenderers.Count}", this);
        }
    }

    private void EnsureAudioSource()
    {
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
        }
        audioSource.spatialBlend = audioSpatialBlend;
    }

    private void PlayHoverEnterSE()
    {
        PlayClipWithOffset(hoverEnterClip, hoverPitchRange, hoverEnterStartOffset, hoverVolume);
    }

    /// <summary>AudioClip を再生。startOffset > 0 なら途中再生 (Play+time)、=0 なら PlayOneShot。</summary>
    private void PlayClipWithOffset(AudioClip clip, Vector2 pitchRange, float startOffset, float volume)
    {
        if (audioSource == null || clip == null) return;

        audioSource.pitch = Mathf.Approximately(pitchRange.x, pitchRange.y)
            ? pitchRange.x
            : Random.Range(pitchRange.x, pitchRange.y);

        if (startOffset > 0f)
        {
            audioSource.clip = clip;
            audioSource.volume = volume;
            audioSource.time = Mathf.Clamp(startOffset, 0f, Mathf.Max(0f, clip.length - 0.01f));
            audioSource.Play();
        }
        else
        {
            audioSource.PlayOneShot(clip, volume);
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

        if(_isOpenUI)
        {
            Debug.LogWarning("_isOpenUIがtrueなので、更新しない");
            return;//別のUIが表示されている間はUpdate処理を停止
        }


        Vector2 mousePos = Mouse.current.position.ReadValue();
        Ray ray = hoverCamera.ScreenPointToRay(mousePos);

        bool over = ignoreOcclusion ? CheckOverSelfDirect(ray) : CheckOverSelfWithOcclusion(ray);
        if (over != _hovered)
        {
            _hovered = over;
            Apply(_hovered);
            if (_hovered) PlayHoverEnterSE(); // アウトラインが出た瞬間に SE 再生
            if (logEvents) Debug.Log($"[MouseHoverOutline] '{name}': hover={_hovered}", this);
        }

        // Press / Release 検出
        var lmb = Mouse.current.leftButton;
        if (lmb.wasPressedThisFrame && _hovered)
        {
            _pressedOnThis = true;
            PlayClipWithOffset(pressDownClip, pressDownPitchRange, pressDownStartOffset, clickVolume);
            if (logEvents) Debug.Log($"[MouseHoverOutline] '{name}': press-down", this);
        }
        if (lmb.wasReleasedThisFrame)
        {
            if (_pressedOnThis && _hovered)
            {
                PlayClipWithOffset(releaseClip, releasePitchRange, releaseStartOffset, clickVolume);
                if (logEvents) Debug.Log($"[MouseHoverOutline] '{name}': release → click 確定", this);
                onClicked?.Invoke();
                OnClicked?.Invoke();
            }
            _pressedOnThis = false;
        }
    }

    /// <summary>
    /// 押し込み移動は LateUpdate で適用する。FloatingMotion など Update で localPosition を
    /// 上書きするスクリプトの「後」に走らせることで競合を防ぐ。
    /// 前フレームで自分が加えた offset を引いてから今フレームの offset を加算する差分方式なので、
    /// 浮遊などの他コンポーネントの動きとも共存できる。
    /// </summary>
    private void LateUpdate()
    {
        if (!pressMoveEnabled) return;
        Transform moveTarget = pressMoveTarget != null ? pressMoveTarget : transform;
        if (moveTarget == null) return;

        // 土台を動的検出: 前フレーム末に自分が書いた値と現在位置を比べて、違っていたら
        // 他スクリプト (FloatingMotion 等) が上書きしたとみなして現在位置を新しい土台にする。
        // 同じなら土台は維持 (自分の offset は土台ではなく上乗せ分として扱う)。
        Vector3 cur = moveTarget.localPosition;
        if (!_pressMoveInitialized || (cur - _pressMoveLastSetPos).sqrMagnitude > 1e-10f)
        {
            _pressMoveBase = cur;
            _pressMoveInitialized = true;
        }

        // press 中は押し込み位置を維持する (ボタン自身の移動で hover が一時的に外れても戻らないように)。
        bool wantPressed = _pressedOnThis && (pressMoveLatchUntilRelease || _hovered);
        float dir = wantPressed ? 1f : -1f;
        float dt = Time.deltaTime / Mathf.Max(0.0001f, pressMoveDuration);
        _pressMoveProgress = Mathf.Clamp01(_pressMoveProgress + dir * dt);

        // ターゲット自身のローカル X 軸を親空間ベクトルに変換した方向に押し込む
        Vector3 selfXInParent = moveTarget.localRotation * Vector3.right;
        Vector3 pressedPos = _pressMoveBase + selfXInParent * pressMoveLocalX;
        Vector3 newPos = Vector3.Slerp(_pressMoveBase, pressedPos, _pressMoveProgress);
        moveTarget.localPosition = newPos;
        _pressMoveLastSetPos = newPos;
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
