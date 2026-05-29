using System.Collections;
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

    private enum State { Idle, Transitioning, ReadyToLoad, Loading }
    private State _state = State.Idle;

    private void Awake()
    {
        if (hoverOutline == null) hoverOutline = GetComponent<MouseHoverOutline>();
        if (impAnimator == null && imp != null) impAnimator = imp.GetComponentInChildren<Animator>();
    }

    private void Update()
    {
        if (Mouse.current == null) return;
        if (!Mouse.current.leftButton.wasPressedThisFrame) return;

        if (logEvents) Debug.Log($"[TitlePlayButton] LeftClick detected, state={_state}, hovered={(hoverOutline != null ? hoverOutline.IsHovered.ToString() : "no hoverOutline")}", this);

        switch (_state)
        {
            case State.Idle:
                // Play ボタンがホバー中の時だけ受理
                if (hoverOutline != null && hoverOutline.IsHovered)
                {
                    StartCoroutine(RunTransition());
                }
                break;
            case State.ReadyToLoad:
                // Imp が idle 状態なら、画面上のどこをクリックしても遷移
                StartCoroutine(LoadGameScene());
                break;
        }
    }

    private IEnumerator RunTransition()
    {
        // Play ボタン押下直後から「左クリックでシーン遷移」を受理できるよう、
        // 待機状態 (ReadyToLoad) に即座に入る。imp の歩行アニメーション等はバックグラウンドで進行する。
        _state = State.ReadyToLoad;
        if (logEvents) Debug.Log($"[TitlePlayButton] State → ReadyToLoad (押下直後。次の左クリックで '{targetSceneName}' へ遷移)", this);

        // ホバーハイライトを終了 (二重クリックや視覚的混乱の防止)
        if (hoverOutline != null) hoverOutline.enabled = false;

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

        // 退場スライドと Imp 歩行を並行実行
        Coroutine slideCo = StartCoroutine(SlideAwayObjects());
        Coroutine walkCo = StartCoroutine(WalkImpToPos2());
        yield return slideCo;
        yield return walkCo;

        // 到着 → idle (無限ループ)。クリック受理は冒頭で既に有効化済み。
        if (impAnimator != null && !string.IsNullOrEmpty(idleAnimationName))
        {
            impAnimator.CrossFade(idleAnimationName, animationCrossFade);
        }
        if (logEvents) Debug.Log("[TitlePlayButton] Imp が pos2 到着 → idle ループ開始", this);
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
                // 移動済みで画面外にあるため、機能上は deactivate せずに残しても問題ない。
                if (go == gameObject || transform.IsChildOf(go.transform))
                {
                    if (logEvents) Debug.Log($"[TitlePlayButton] '{go.name}' は自身/祖先のため SetActive(false) を回避 (Update を生かす)", this);
                    continue;
                }
                go.SetActive(false);
            }
        }
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
}
