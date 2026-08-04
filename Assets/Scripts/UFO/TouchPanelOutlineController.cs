using System.Collections;
using UniRx;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// television の画面 (Play_Canvas) に重ねた TouchPanel > Play (30 / 60 / 90 / Quit の当たり判定用オブジェクト) へ
/// マウスレイキャストを行い、対応する Play_Canvas 側パネルの Outline 表示 / 非表示を切り替えます。
/// TouchPanel > Play の各子オブジェクトは Play_Canvas 上の UI と位置を揃えるための透明な当たり判定用オブジェクトで、
/// 実際に見せたい輪郭は Play_Canvas 側の Panel_30 / Panel_60 / Panel_90 / Quit&gt;Button にすでにアタッチされた
/// UnityEngine.UI.Outline を使う。
/// UFOキャッチャー再生中（画面が固定される間）のみ判定を行い、それ以外は常に非表示にする。
/// さらに 30 / 60 / 90 は左クリックで「選択」でき、選択音 → 砂嵐演出 → Play_Canvas2 への切り替えを行う。
/// このスクリプトは TouchPanel オブジェクトにアタッチして使用する。
/// </summary>
public class TouchPanelOutlineController : MonoBehaviour
{
    [System.Serializable]
    private class TouchTarget
    {
        [Tooltip("TouchPanel 配下の当たり判定用オブジェクト。空欄なら touchAreaName から自動取得")]
        public Transform touchArea;

        [Tooltip("touchArea が未設定の場合に TouchPanel から検索するパス（例: 'Play/30'）")]
        public string touchAreaName;

        [Tooltip("表示を切り替える Outline コンポーネント。空欄なら playCanvasPath から自動取得")]
        public Outline outline;

        [Tooltip("outline が未設定の場合に Play_Canvas 直下から検索するパス（例: 'Panel_30' や 'Quit/Button'）")]
        public string playCanvasPath;

        [Tooltip("アウトラインが表示された瞬間（ホバー開始時）に再生する SE。未設定なら無音")]
        public AudioClip hoverEnterClip;

        [Tooltip("選択された時に Play_Canvas2 側のテキストへ反映する内容（30/60/90 のみ使用）")]
        [TextArea]
        public string resultText;

        [Tooltip("選択された時のプレイ時間（秒）。30/60/90 のみ使用し、Play_Canvas2 の Play クリック時に使われる")]
        public float durationSeconds;

        [Tooltip("選択された時の消費 Devil Coins。30/60/90 のみ使用し、Play_Canvas2 の Play クリック時に MoneyManager から引かれる")]
        public float durationCost;

        [Tooltip("true の場合、outline を Play_Canvas ではなく Play_Canvas2 側から検索する（Play2 グループ用）")]
        public bool useCanvas2;

        [Tooltip("true の場合ロック中として扱う。ホバー時のアウトラインが赤くなり、クリックしても選択できず拒否演出が出る")]
        public bool isLocked;

        [Tooltip("ロック中に表示するロックアイコン（例: Rock_60 / Rock_90）。空欄なら lockIndicatorPath から自動取得")]
        public GameObject lockIndicator;

        [Tooltip("lockIndicator が未設定の場合に Play_Canvas 直下から検索する名前（例: 'Rock_60'）")]
        public string lockIndicatorPath;

        [System.NonSerialized] public Color normalOutlineColor;
        [System.NonSerialized] public bool wasHovered;
    }

    [Header("マウスレイ発信元カメラ")]
    [Tooltip("null の場合 UFOCameraController の現在のアクティブカメラ（UFOプレイ中は frontCamera）を使用")]
    [SerializeField] private Camera rayCamera;

    [Tooltip("Raycast の最大距離 (m)")]
    [SerializeField] private float maxDistance = 50f;

    [Header("ホバー SE")]
    [Tooltip("SE 再生用の AudioSource。未設定なら自身に AddComponent して使う")]
    [SerializeField] private AudioSource audioSource;

    [Range(0f, 5f)]
    [Tooltip("SE のボリューム（1 を超えるとブースト）")]
    [SerializeField] private float hoverVolume = 1f;

    [Header("判定対象 (Play)")]
    [SerializeField] private TouchTarget touch30 = new TouchTarget { touchAreaName = "Play/30", playCanvasPath = "Panel_30", resultText = "              30秒\n1500 Devil Coins", durationSeconds = 30f, durationCost = 1500f };
    [SerializeField] private TouchTarget touch60 = new TouchTarget { touchAreaName = "Play/60", playCanvasPath = "Panel_60", resultText = "              60秒\n2500 Devil Coins", durationSeconds = 60f, durationCost = 2500f, isLocked = true, lockIndicatorPath = "Rock_60" };
    [SerializeField] private TouchTarget touch90 = new TouchTarget { touchAreaName = "Play/90", playCanvasPath = "Panel_90", resultText = "              90秒\n4000 Devil Coins", durationSeconds = 90f, durationCost = 4000f, isLocked = true, lockIndicatorPath = "Rock_90" };
    [SerializeField] private TouchTarget touchQuit = new TouchTarget { touchAreaName = "Play/Quit", playCanvasPath = "Quit/Button" };

    [Header("ロック中の選択 (拒否演出)")]
    [Tooltip("ロック中の 60 / 90 をホバーした時のアウトライン色")]
    [SerializeField] private Color lockedOutlineColor = new Color(1f, 0f, 0f, 0.5f);

    [Tooltip("ロック中の 60 / 90 をクリックした時に鳴らす SE（共通）")]
    [SerializeField] private AudioClip lockedSound;

    [Range(0f, 5f)]
    [Tooltip("拒否 SE のボリューム")]
    [SerializeField] private float lockedVolume = 1f;

    [Tooltip("拒否演出（UIが揺れる）の振幅")]
    [SerializeField] private float lockedShakeStrength = 12f;

    [Tooltip("拒否演出（UIが揺れる）の継続時間（秒）")]
    [SerializeField] private float lockedShakeDuration = 0.3f;

    [Header("判定対象 (Play2)")]
    [SerializeField] private TouchTarget touch2Play = new TouchTarget { touchAreaName = "Play2/Play", playCanvasPath = "Play", useCanvas2 = true };
    [SerializeField] private TouchTarget touch2Back = new TouchTarget { touchAreaName = "Play2/Back", playCanvasPath = "Back", useCanvas2 = true };

    [Header("選択 (クリック) → Play_Canvas2 遷移")]
    [Tooltip("30 / 60 / 90 を左クリックで選択した瞬間に鳴らす SE（3つ共通）")]
    [SerializeField] private AudioClip selectSound;

    [Range(0f, 5f)]
    [Tooltip("選択 SE のボリューム")]
    [SerializeField] private float selectVolume = 1f;

    [Tooltip("選択結果（秒数 / Devil Coins）を表示する Play_Canvas2 側のテキスト。未設定なら playCanvas2TextPath から自動取得")]
    [SerializeField] private TextMeshProUGUI play2ResultText;

    [Tooltip("play2ResultText が未設定の場合に Play_Canvas2 直下から検索する名前")]
    [SerializeField] private string playCanvas2TextPath = "Text (TMP)";

    [Header("選択 (クリック) → Play2 の Play / Back")]
    [Tooltip("Play2 の Play / Back を左クリックで選択した瞬間に鳴らす SE（共通）")]
    [SerializeField] private AudioClip select2Sound;

    [Range(0f, 5f)]
    [Tooltip("選択 SE のボリューム")]
    [SerializeField] private float select2Volume = 1f;

    private TouchTarget[] _targets;
    private TouchTarget[] _selectableTargets;
    private TelevisionStaticController _tvController;
    private Transform _playGroup;
    private Transform _play2Group;
    private bool _selectionTriggered;
    private float _selectedDurationSeconds = 30f;
    private float _selectedCost;

    private void Awake()
    {
        _targets = new[] { touch30, touch60, touch90, touchQuit, touch2Play, touch2Back };
        _selectableTargets = new[] { touch30, touch60, touch90 };

        EnsureAudioSource();

        _tvController = FindAnyObjectByType<TelevisionStaticController>();
        Canvas playCanvas = _tvController != null ? _tvController.PlayCanvas : null;
        Canvas playCanvas2 = _tvController != null ? _tvController.PlayCanvas2 : null;

        if (play2ResultText == null && playCanvas2 != null && !string.IsNullOrEmpty(playCanvas2TextPath))
        {
            Transform textTarget = playCanvas2.transform.Find(playCanvas2TextPath);
            if (textTarget != null)
            {
                play2ResultText = textTarget.GetComponent<TextMeshProUGUI>();
            }
        }

        foreach (var t in _targets)
        {
            if (t.touchArea == null && !string.IsNullOrEmpty(t.touchAreaName))
            {
                t.touchArea = transform.Find(t.touchAreaName);
            }
            EnsureCollider(t.touchArea);

            Canvas searchCanvas = t.useCanvas2 ? playCanvas2 : playCanvas;
            if (t.outline == null && searchCanvas != null && !string.IsNullOrEmpty(t.playCanvasPath))
            {
                Transform target = searchCanvas.transform.Find(t.playCanvasPath);
                if (target != null)
                {
                    t.outline = target.GetComponent<Outline>();
                }
            }

            // 開始時は非表示にしておく（色は通常時の色として記憶しておき、ロック解除時に復元する）
            if (t.outline != null)
            {
                t.normalOutlineColor = t.outline.effectColor;
                t.outline.enabled = false;
            }

            if (t.lockIndicator == null && playCanvas != null && !string.IsNullOrEmpty(t.lockIndicatorPath))
            {
                Transform li = playCanvas.transform.Find(t.lockIndicatorPath);
                if (li != null)
                {
                    t.lockIndicator = li.gameObject;
                }
            }
            if (t.lockIndicator != null)
            {
                t.lockIndicator.SetActive(t.isLocked);
            }
        }

        // TouchPanel > Play / Play2 の当たり判定グループ。最初は Play のみ有効にする
        _playGroup = transform.Find("Play");
        _play2Group = transform.Find("Play2");
        SetActiveGroup(isPlay: true);
    }

    private void Start()
    {
        // ラウンド2（MoneyManager のターン2）に到達したら、60秒を自動解禁する（90秒解禁と同じ UnlockDuration を使用）。
        // MoneyManager は加法ロードされる別サブシーン側にいるため、MultiSceneLoader の非同期ロードが
        // 終わるまで Instance が null の可能性がある。Instance が現れるまで毎フレーム待ってから購読する。
        Observable.EveryUpdate()
            .Select(_ => MoneyManager.Instance)
            .Where(mm => mm != null)
            .First()
            .Subscribe(mm =>
            {
                // 現在のターン数、および以降のターン変化の両方をチェックする（Skip(1)はしない）
                mm.OnCurrentTurnChange
                    .Subscribe(turn =>
                    {
                        if (turn >= 2)
                        {
                            UnlockDuration(60f);
                        }
                    })
                    .AddTo(this);
            })
            .AddTo(this);
    }

    private void EnsureAudioSource()
    {
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f; // 2D再生
        }
    }

    /// <summary>
    /// TouchPanel の当たり判定用オブジェクトに Collider がなければ、SpriteRenderer のサイズに合わせて自動付与する。
    /// </summary>
    private static void EnsureCollider(Transform area)
    {
        if (area == null) return;
        if (area.GetComponent<Collider>() != null) return;

        BoxCollider box = area.gameObject.AddComponent<BoxCollider>();
        box.isTrigger = true;

        SpriteRenderer sr = area.GetComponent<SpriteRenderer>();
        if (sr != null && sr.sprite != null)
        {
            Bounds b = sr.sprite.bounds;
            box.center = b.center;
            box.size = new Vector3(b.size.x, b.size.y, Mathf.Max(b.size.z, 0.05f));
        }
    }

    private void Update()
    {
        if (!UFOCameraController.IsPlayingUfo)
        {
            HideAll();
            _selectionTriggered = false; // 次回プレイ開始時にまた選択できるようにリセット
            SetActiveGroup(isPlay: true); // Play 側の当たり判定に戻しておく
            return;
        }

        Camera cam = rayCamera != null ? rayCamera
            : (UFOCameraController.Instance != null ? UFOCameraController.Instance.GetActiveCamera() : Camera.main);

        if (cam == null || Mouse.current == null)
        {
            HideAll();
            return;
        }

        Vector2 mousePos = Mouse.current.position.ReadValue();
        Ray ray = cam.ScreenPointToRay(mousePos);

        // isTrigger の当たり判定用 Collider を使うため、プロジェクト設定に関わらず必ずヒットさせる
        Transform hitArea = null;
        if (Physics.Raycast(ray, out RaycastHit hit, maxDistance, ~0, QueryTriggerInteraction.Collide))
        {
            hitArea = hit.collider.transform;
        }

        // Play_Canvas が表示されている間のみ 30/60/90 の選択を受け付ける（Play_Canvas2 遷移後は再トリガーしない）
        bool canSelect = !_selectionTriggered && _tvController != null
            && _tvController.PlayCanvas != null && _tvController.PlayCanvas.gameObject.activeSelf;

        // Play_Canvas2 が表示されている間のみ Play2 の Play / Back の選択を受け付ける
        bool canSelectPlay2 = _tvController != null
            && _tvController.PlayCanvas2 != null && _tvController.PlayCanvas2.gameObject.activeSelf;

        bool leftClicked = Mouse.current.leftButton.wasPressedThisFrame;

        foreach (var t in _targets)
        {
            bool isHovered = t.touchArea != null && hitArea == t.touchArea;

            if (t.outline != null)
            {
                t.outline.enabled = isHovered;
                if (isHovered)
                {
                    t.outline.effectColor = t.isLocked ? lockedOutlineColor : t.normalOutlineColor;
                }
            }

            // ホバー開始の瞬間（未ホバー → ホバー）にだけ SE を鳴らす
            if (isHovered && !t.wasHovered)
            {
                PlayOneShot(t.hoverEnterClip, hoverVolume);
            }
            t.wasHovered = isHovered;
        }

        if (leftClicked)
        {
            if (canSelect)
            {
                foreach (var t in _selectableTargets)
                {
                    if (t.touchArea != null && hitArea == t.touchArea)
                    {
                        if (t.isLocked)
                        {
                            HandleLockedClicked(t);
                        }
                        else
                        {
                            HandleSelected(t);
                        }
                        break;
                    }
                }
            }

            if (touchQuit.touchArea != null && hitArea == touchQuit.touchArea)
            {
                HandleQuitClicked();
            }

            if (canSelectPlay2)
            {
                if (touch2Back.touchArea != null && hitArea == touch2Back.touchArea)
                {
                    HandlePlay2BackClicked();
                }
                else if (touch2Play.touchArea != null && hitArea == touch2Play.touchArea)
                {
                    HandlePlay2PlayClicked();
                }
            }
        }

        // Play_Canvas2 表示中は、Enter キーでもマウスクリックと同じ Play を実行できるようにする
        if (canSelectPlay2 && Keyboard.current != null &&
            (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.numpadEnterKey.wasPressedThisFrame))
        {
            HandlePlay2PlayClicked();
        }
    }

    /// <summary>
    /// Play_Canvas 上の Quit が左クリックされた時の処理。UFOキャッチャーモードを終了する。
    /// </summary>
    private void HandleQuitClicked()
    {
        HideAll();

        if (UFOCameraController.Instance != null)
        {
            UFOCameraController.Instance.RequestExitUfoMode();
        }
    }

    /// <summary>
    /// 30 / 60 / 90 のいずれかが左クリックで選択された時の処理。
    /// Play_Canvas2 側のテキストを更新し、選択 SE を鳴らし、アウトラインを消し、
    /// 当たり判定グループを Play → Play2 に切り替えたうえで、砂嵐演出を挟んで Play_Canvas2 へ切り替える。
    /// </summary>
    private void HandleSelected(TouchTarget target)
    {
        _selectionTriggered = true;
        _selectedDurationSeconds = target.durationSeconds;
        _selectedCost = target.durationCost;

        if (play2ResultText != null)
        {
            play2ResultText.text = target.resultText;
        }

        PlayOneShot(selectSound, selectVolume);
        HideAll();
        SetActiveGroup(isPlay: false);

        if (_tvController != null)
        {
            _tvController.PlayStaticThenShowCanvas(_tvController.PlayCanvas2);
        }
    }

    /// <summary>
    /// ロック中（60/90）の項目がクリックされた時の処理。選択はさせず、拒否 SE と揺れ演出のみ行う。
    /// </summary>
    private void HandleLockedClicked(TouchTarget target)
    {
        PlayOneShot(lockedSound, lockedVolume);

        if (target.outline != null)
        {
            RectTransform rt = target.outline.GetComponent<RectTransform>();
            StartCoroutine(ShakeUI(rt));
        }
    }

    private IEnumerator ShakeUI(RectTransform rt)
    {
        if (rt == null) yield break;

        Vector2 originalPos = rt.anchoredPosition;
        float elapsed = 0f;
        while (elapsed < lockedShakeDuration)
        {
            elapsed += Time.deltaTime;
            float damper = 1f - Mathf.Clamp01(elapsed / lockedShakeDuration);
            float offsetX = Random.Range(-1f, 1f) * lockedShakeStrength * damper;
            rt.anchoredPosition = originalPos + new Vector2(offsetX, 0f);
            yield return null;
        }
        rt.anchoredPosition = originalPos;
    }

    /// <summary>
    /// 指定した秒数（30/60/90）のロックを解除する。解除条件は別途実装予定のため、
    /// 現時点では外部（今後追加する解禁ロジック）からこのメソッドを呼び出すことで手動解除できるようにしておく。
    /// </summary>
    public void UnlockDuration(float durationSeconds)
    {
        foreach (var t in _selectableTargets)
        {
            if (!Mathf.Approximately(t.durationSeconds, durationSeconds)) continue;

            t.isLocked = false;
            if (t.lockIndicator != null) t.lockIndicator.SetActive(false);
            if (t.outline != null) t.outline.effectColor = t.normalOutlineColor;
        }
    }

    /// <summary>
    /// Play_Canvas2 の Back が左クリックされた時の処理。Esc/F キーで戻る場合と同じ処理を行う。
    /// </summary>
    private void HandlePlay2BackClicked()
    {
        PlayOneShot(select2Sound, select2Volume);
        ReturnToPlay1();
    }

    /// <summary>
    /// Play_Canvas2 の Play が左クリックされた時の処理。選択済みの秒数・Devil Coins で UFOキャッチャーの
    /// プレイセッションを開始する。所持金が足りない場合は開始せず拒否演出のみ行う。
    /// </summary>
    private void HandlePlay2PlayClicked()
    {
        if (UFOCameraController.Instance == null) return;

        if (MoneyManager.Instance != null && MoneyManager.Instance.CurrentMoney < _selectedCost)
        {
            HandlePlay2PlayRejected();
            return;
        }

        bool started = UFOCameraController.Instance.StartPlaySessionFromTelevision(_selectedDurationSeconds, _selectedCost);
        if (started)
        {
            PlayOneShot(select2Sound, select2Volume);
            HideAll();

            // プレイ開始によりメニュー操作は終了するため、Play / Play2 どちらの当たり判定も無効化する
            if (_playGroup != null) _playGroup.gameObject.SetActive(false);
            if (_play2Group != null) _play2Group.gameObject.SetActive(false);
        }
        else
        {
            HandlePlay2PlayRejected();
        }
    }

    /// <summary>
    /// Devil Coins 不足などでプレイを開始できなかった時の拒否演出（ロック中クリック時と同じ SE + 揺れを流用）。
    /// </summary>
    private void HandlePlay2PlayRejected()
    {
        PlayOneShot(lockedSound, lockedVolume);

        if (touch2Play.outline != null)
        {
            RectTransform rt = touch2Play.outline.GetComponent<RectTransform>();
            StartCoroutine(ShakeUI(rt));
        }
    }

    /// <summary>
    /// 砂嵐演出を挟んで Play_Canvas2 から Play_Canvas へ戻る。
    /// Esc/F キー（UFOCameraController.HandleUfoInput）と Play2 の Back クリックの両方から呼ばれる。
    /// </summary>
    public void ReturnToPlay1()
    {
        if (_tvController == null) return;

        HideAll();
        SetActiveGroup(isPlay: true);
        _selectionTriggered = false;
        _tvController.PlayStaticThenShowCanvas(_tvController.PlayCanvas);
    }

    /// <summary>
    /// TouchPanel 配下の Play / Play2 当たり判定グループの有効・無効を切り替える。
    /// </summary>
    private void SetActiveGroup(bool isPlay)
    {
        if (_playGroup != null) _playGroup.gameObject.SetActive(isPlay);
        if (_play2Group != null) _play2Group.gameObject.SetActive(!isPlay);
    }

    private void PlayOneShot(AudioClip clip, float volume)
    {
        if (clip == null || audioSource == null) return;
        audioSource.PlayOneShot(clip, volume);
    }

    private void OnDisable()
    {
        HideAll();
    }

    private void HideAll()
    {
        if (_targets == null) return;
        foreach (var t in _targets)
        {
            if (t.outline != null && t.outline.enabled)
            {
                t.outline.enabled = false;
            }
            t.wasHovered = false;
        }
    }
}
