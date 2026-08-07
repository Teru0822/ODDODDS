using System.Collections;
using MiniGames.Transitions;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// タイトルシーンの Play ボタン制御。MouseHoverOutline を同居させ、
/// ホバー中のクリックで以下のシーケンスを実行:
///   1) Inspector 指定の slideAwayObjects をワールド +Z に slideDistance 移動 → 非表示
///   2) imp を pos1 → pos2 へ walkAnimation を再生しながら歩行
///   3) pos2 到着で idleAnimation に CrossFade (無限ループ前提)
/// その後の任意の左クリックで GameScene へ遷移し、displayIndexToActivate を有効化する。
/// </summary>
[DisallowMultipleComponent]
public class TitlePlayButton : MonoBehaviour
{
    [Header("ホバー (クリック判定に使用)")]
    [Tooltip("クリック判定に使う MouseHoverOutline。null なら自身に付いているものを自動取得")]
    public MouseHoverOutline hoverOutline;

    [Header("クリック時に退場するオブジェクト")]
    [Tooltip("ワールド +Z 方向に slideDistance 進めた後、deactivateAfterSlide=true なら SetActive(false) する対象")]
    public GameObject[] slideAwayObjects;

    [Tooltip("退場時の移動距離 (m、ワールド +Z)")]
    public float slideDistance = 100f;

    [Tooltip("退場アニメ秒数")]
    public float slideDuration = 1.0f;

    [Tooltip("退場後に SetActive(false) で完全に消す (自身/親は安全のため自動除外)")]
    public bool deactivateAfterSlide = true;

    [Header("デバッグ")]
    [Tooltip("状態遷移・コルーチン・シーン遷移を Console に出力")]
    public bool logEvents = true;

    [Header("Imp 歩行")]
    [Tooltip("歩かせる Imp の Transform")]
    public Transform imp;

    [Tooltip("Imp の歩行開始位置")]
    public Transform pos1;

    [Tooltip("Imp の歩行終了位置 (idle に切り替わる地点)")]
    public Transform pos2;

    [Tooltip("Imp の Animator。null なら imp から自動取得")]
    public Animator impAnimator;

    [Tooltip("歩行中アニメーション (Animator State 名)")]
    public string walkAnimationName = "imp_arma|walk";

    [Tooltip("到着後の待機アニメーション (Animator State 名、Loop=ON 推奨)")]
    public string idleAnimationName = "inm_arma|idle";

    [Tooltip("歩行速度 (m/s)")]
    public float walkSpeed = 1.5f;

    [Tooltip("アニメーション CrossFade 秒数")]
    public float animationCrossFade = 0.2f;

    [Tooltip("Imp を pos1→pos2 の方向に向ける (Y 軸基準)")]
    public bool faceWalkDirection = true;

    [Header("シーン遷移 (Play ボタン押下後の任意の左クリック)")]
    [Tooltip("遷移先シーン名 (Build Settings に追加されていること)")]
    public string targetSceneName = "MainScene";

    [Tooltip("遷移時に有効化するディスプレイインデックス (0=Display1, 3=Display4)")]
    [Range(0, 7)]
    public int displayIndexToActivate = 3;

    [Header("サウンド (Play押下時)")]
    [Tooltip("Playボタン押下時の効果音（大きなものが横切る音など）を鳴らすAudioSource")]
    public AudioSource slideAwayAudioSource;
    [Tooltip("鳴らすAudioClip")]
    public AudioClip slideAwaySound;
    [Tooltip("SEを鳴らすまでの遅延時間（秒）。映像に対して音が早い場合はこの数値を大きくしてください")]
    public float slideAwaySoundDelay = 0.5f;

    [Header("シーケンス (悪魔会話・遷移)")]
    [Tooltip("タイトル専用の悪魔会話マネージャー")]
    public MiniGames.Title.TitleConversationManager titleConversationManager;
    [Tooltip("モヤとロード画面を管理するマネージャー")]
    public MiniGames.Transitions.SceneTransitionManager sceneTransitionManager;

    private enum State { Idle, Transitioning, ReadyToLoad, Loading }
    private State _state = State.Idle;

    private void Awake()
    {
        if (hoverOutline == null) hoverOutline = GetComponent<MouseHoverOutline>();
        if (impAnimator == null && imp != null) impAnimator = imp.GetComponentInChildren<Animator>();
        if (hoverOutline == null)
        {
            Debug.LogWarning("[TitlePlayButton] MouseHoverOutline が未取得。Play クリックが反応しません。同一 GameObject にアタッチするか Inspector で指定してください", this);
        }
    }

    private void OnEnable()
    {
        if (hoverOutline != null) hoverOutline.OnClicked += HandlePlayButtonClicked;
    }

    private void OnDisable()
    {
        if (hoverOutline != null) hoverOutline.OnClicked -= HandlePlayButtonClicked;
    }

    private void HandlePlayButtonClicked()
    {
        if (_state != State.Idle) return;
        if (logEvents) Debug.Log("[TitlePlayButton] Play ボタンクリック確定 (press → release) → RunTransition 開始", this);
        StartCoroutine(RunTransition());
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            SettingUIManager.Instance.CloseSettingMenu();
            SettingUIManager.Instance.IsCantSettingUI = false;
            if (logEvents) Debug.Log("[TitlePlayButton] ESC pressed, restarting scene.", this);
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
            return;
        }

        // 以降の Update 処理（ReadyToLoad中のクリック判定等）は、
        // 悪魔の会話が自動で遷移を呼ぶようになったため削除または無効化しました。
    }

    private IEnumerator RunTransition()
    {
        _state = State.Transitioning;
        if (logEvents) Debug.Log($"[TitlePlayButton] State → Transitioning (オブジェクト退場とImpの移動開始)", this);

        // ホバーハイライトを終了 (二重クリックや視覚的混乱の防止)
        if (hoverOutline != null) hoverOutline.enabled = false;

        DisableMotionOverrides();

        // SE再生 (タイミング調整のため遅延再生コルーチンを使用)
        StartCoroutine(PlayDelayedSound(slideAwaySoundDelay));

        // Imp を pos1 に配置 + 歩行方向に向ける + walk アニメ開始
        if (imp != null && pos1 != null)
        {
            imp.position = pos1.position;
            if (faceWalkDirection && pos2 != null)
            {
                Vector3 dir = pos2.position - pos1.position;
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.0001f) imp.rotation = Quaternion.LookRotation(dir);
            }
        }
        if (impAnimator != null && !string.IsNullOrEmpty(walkAnimationName))
        {
            impAnimator.CrossFade(walkAnimationName, animationCrossFade);
        }

        // 退場スライドはバックグラウンドで進行させ、Imp の歩行完了だけを待つ
        StartCoroutine(SlideAwayObjects());
        yield return StartCoroutine(WalkImpToPos2());

        // 到着 → idle
        if (impAnimator != null && !string.IsNullOrEmpty(idleAnimationName))
        {
            impAnimator.CrossFade(idleAnimationName, animationCrossFade);
        }
        if (logEvents) Debug.Log("[TitlePlayButton] Imp が pos2 到着。オブジェクト退場完了。", this);

        // --- ここから追加シーケンス ---
        // 悪魔の会話を開始
        if (titleConversationManager != null)
        {
            if (logEvents) Debug.Log("[TitlePlayButton] 悪魔の会話を開始します。", this);
            titleConversationManager.StartConversation(OnConversationComplete);
        }
        else
        {
            // 会話マネージャーがない場合はすぐに遷移へ進む
            if (logEvents) Debug.LogWarning("[TitlePlayButton] TitleConversationManager がアタッチされていません。会話をスキップして遷移します。", this);
            OnConversationComplete();
        }
    }

    private void OnConversationComplete()
    {
        if (logEvents) Debug.Log("[TitlePlayButton] 悪魔の会話が完了。モヤ暗転とロードを開始します。", this);
        if(sceneTransitionManager == null) sceneTransitionManager = FindFirstObjectByType<SceneTransitionManager>();//ゲームシーンからタイトルシーンに戻った際に無くなるため

        if (sceneTransitionManager != null)
        {
            sceneTransitionManager.TransitionToScene(targetSceneName);
        }
        else
        {
            // TransitionManager が無い場合のフォールバック（既存のロード）
            if (logEvents) Debug.LogWarning("[TitlePlayButton] SceneTransitionManager が設定されていません。通常の LoadScene を実行します。", this);
            StartCoroutine(LoadGameScene());
        }
    }

    private IEnumerator PlayDelayedSound(float delay)
    {
        if (delay > 0f)
        {
            yield return new WaitForSeconds(delay);
        }

        if (slideAwayAudioSource != null && slideAwaySound != null)
        {
            slideAwayAudioSource.PlayOneShot(slideAwaySound);
        }
    }

    private IEnumerator SlideAwayObjects()
    {
        if (slideAwayObjects == null || slideAwayObjects.Length == 0) yield break;

        var starts = new Vector3[slideAwayObjects.Length];
        for (int i = 0; i < slideAwayObjects.Length; i++)
        {
            if (slideAwayObjects[i] != null) starts[i] = slideAwayObjects[i].transform.position;
        }
        Vector3 offset = Vector3.forward * slideDistance; // ワールド +Z

        float t = 0f;
        float dur = Mathf.Max(0.001f, slideDuration);
        while (t < dur)
        {
            t += Time.deltaTime;
            float u = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / dur));
            for (int i = 0; i < slideAwayObjects.Length; i++)
            {
                if (slideAwayObjects[i] == null) continue;
                slideAwayObjects[i].transform.position = starts[i] + offset * u;
            }
            yield return null;
        }
        if (deactivateAfterSlide)
        {
            for (int i = 0; i < slideAwayObjects.Length; i++)
            {
                var go = slideAwayObjects[i];
                if (go == null) continue;
                // 自身 (または自身の祖先) を SetActive(false) すると本 MonoBehaviour も停止し、
                // 状態が ReadyToLoad に進まず Update も止まって 2 度目のクリックを拾えなくなる。
                // 見た目上・物理的に消去するために Renderer/Collider を無効化して非表示・非衝突化します。
                if (go == gameObject || transform.IsChildOf(go.transform))
                {
                    if (logEvents) Debug.Log($"[TitlePlayButton] '{go.name}' は自身/祖先のため SetActive(false) を回避し、Renderer/Collider を無効化して非表示にします", this);
                    HidePlayButtonVisuals(go);
                    continue;
                }
                go.SetActive(false);
            }
        }

        // スライド先オブジェクトに自身が含まれていなかった場合の安全策として、PLAYボタン自身も非表示にします
        HidePlayButtonVisuals(gameObject);
    }

    private IEnumerator WalkImpToPos2()
    {
        if (imp == null || pos2 == null) yield break;
        Vector3 start = imp.position;
        Vector3 end = pos2.position;
        float distance = Vector3.Distance(start, end);
        float dur = distance / Mathf.Max(0.01f, walkSpeed);

        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / dur);
            imp.position = Vector3.Lerp(start, end, u);
            yield return null;
        }
        imp.position = end;
    }

    private IEnumerator LoadGameScene()
    {
        _state = State.Loading;
        if (logEvents) Debug.Log($"[TitlePlayButton] LoadGameScene 開始 (target='{targetSceneName}', display={displayIndexToActivate})", this);

        // Display を有効化 (まだ inactive ならアクティベート)
        if (displayIndexToActivate >= 0 && displayIndexToActivate < Display.displays.Length)
        {
            var disp = Display.displays[displayIndexToActivate];
            if (!disp.active)
            {
                disp.Activate();
                if (logEvents) Debug.Log($"[TitlePlayButton] Display[{displayIndexToActivate}] を Activate しました", this);
                yield return null; // Activate 直後は 1 フレーム待つ (安全策)
            }
        }
        if (string.IsNullOrEmpty(targetSceneName))
        {
            Debug.LogWarning("[TitlePlayButton] targetSceneName が空のためシーン遷移をスキップ", this);
            yield break;
        }
        // Build Settings に存在しないシーン名を渡すと SceneManager.LoadScene は例外なく
        // サイレントに失敗するので事前検証
        if (SceneUtility.GetBuildIndexByScenePath(targetSceneName) < 0
            && !Application.CanStreamedLevelBeLoaded(targetSceneName))
        {
            Debug.LogError($"[TitlePlayButton] シーン '{targetSceneName}' が Build Settings に含まれていません。File → Build Settings → Scenes In Build に追加してください", this);
            yield break;
        }
        SceneManager.LoadScene(targetSceneName);
    }

    /// <summary>
    /// slideAwayObjects や PLAY ボタン自身の FloatingMotion などの挙動上書き系コンポーネントを無効化します。
    /// </summary>
    private void DisableMotionOverrides()
    {
        if (slideAwayObjects != null)
        {
            foreach (var go in slideAwayObjects)
            {
                if (go == null) continue;
                var motions = go.GetComponentsInChildren<FloatingMotion>(true);
                foreach (var motion in motions)
                {
                    motion.enabled = false;
                }
            }
        }

        // 自身（PLAY ボタン）の FloatingMotion も無効化
        var selfMotions = GetComponentsInChildren<FloatingMotion>(true);
        foreach (var motion in selfMotions)
        {
            motion.enabled = false;
        }
    }

    /// <summary>
    /// 指定されたゲームオブジェクト（PLAYボタン等）の Renderer と Collider を無効化して
    /// 見た目上・物理的に消去しますが、GameObject 自体はアクティブのままにしてスクリプトの動作を維持します。
    /// </summary>
    private void HidePlayButtonVisuals(GameObject go)
    {
        if (go == null) return;
        
        var renderers = go.GetComponentsInChildren<Renderer>(true);
        foreach (var r in renderers)
        {
            r.enabled = false;
        }

        var colliders = go.GetComponentsInChildren<Collider>(true);
        foreach (var c in colliders)
        {
            c.enabled = false;
        }
    }
}
