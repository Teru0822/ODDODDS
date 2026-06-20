using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// ピンボール台への一連のインタラクションを管理する状態機械。
///
///   [Idle]    台に視点を向けると MouseHoverOutline が輪郭をハイライト。
///             左クリック → カメラが P1CAM へ Slerp 移動・回転（State==1 へ）。
///   [AtP1]    ショップ/エイミング画面。左クリックでの自動遷移はしない（PinballShopView /
///             PinballPlaybaseButton 等が State==1 を見て動く）。playbase2 ボタンが押されると
///             LaunchBall() が呼ばれ、選択中のボールを召喚→落下開始→カメラ P3CAM、Playing へ。
///   [Playing] 以降クリックは PinballFlipperController（フリッパー）へ。状態遷移はしない。
///
/// どの非 Idle 状態でも Escape キーで ReturnToIdle()（カメラをプレイヤーに戻し、召喚ボールも破棄）。
///
/// カメラは位置を補間 + 回転を Quaternion.Slerp で滑らかに移動する。
/// アウトライン表示は既存の MouseHoverOutline を流用（台にアタッチして参照を割り当てる）。
/// このスクリプトはシーンに依存せず、参照は Inspector で割り当てる（Scene_PinBall 内で設定）。
/// </summary>
public class PinballSessionController : MonoBehaviour
{
    [Header("ハイライト（台の MouseHoverOutline）")]
    [Tooltip("ピンボール台にアタッチした MouseHoverOutline。Idle 中の輪郭表示と『台を見ているか』判定に使う")]
    public MouseHoverOutline machineHover;

    [Header("カメラ")]
    [Tooltip("動かすカメラの Transform。【Multi-Scene 注意】別シーン(Scene_Environment 等)の" +
             "カメラはここに割り当てられない（保存時に null 化される）。空のままにして実行時に " +
             "Camera.main から自動取得させること。カメラには 'MainCamera' タグが必要。")]
    public Transform targetCamera;

    [Tooltip("各段階のカメラ目標（位置・回転を読む。空オブジェクトや配置用カメラでよい）")]
    public Transform p1Cam;
    public Transform p2Cam;
    public Transform p3Cam;

    [Min(0.01f)]
    [Tooltip("カメラ移動の所要時間（秒）")]
    public float moveDuration = 1.0f;

    [Tooltip("移動の緩急（0→1）。EaseInOut 推奨")]
    public AnimationCurve moveEase = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("ボール")]
    [Tooltip("newBall を召喚する位置（空のゲームオブジェクト BallSpawner）")]
    public Transform ballSpawner;

    [Tooltip("召喚する newBall の Prefab")]
    public GameObject newBallPrefab;

    [Header("発射（playbase2 → 2秒後に newpinballvec のローカル -Y へ）")]
    [Tooltip("発射方向の基準となる newpinballvec の Transform。playbase2 押下時の『ローカル -Y 方向』を" +
             "記録して発射に使う。必ず割り当てること（未割り当てだと方向が定まらず自由落下になる）")]
    public Transform newpinballvec;

    [Min(0f)]
    [Tooltip("発射の impulse 強さ。0 なら自由落下のみ（向きの力なし）")]
    public float launchForce = 3f;

    [Min(0f)]
    [Tooltip("playbase2 でスポーン（State2）してから実際に発射（State3）するまでの待機秒数")]
    public float launchDelay = 2f;

    [Header("ボールへの追加力（台ローカル -Y）")]
    [Tooltip("追加力の向きの基準となるピンボール台の Transform。この台のローカル -Y 方向へ力を加える")]
    public Transform pinballTable;

    [Min(0f)]
    [Tooltip("標準重力に上乗せして台ローカル -Y へ加える力（N）")]
    public float ballExtraForce = 9f;

    [Header("フリッパー")]
    [Tooltip("Playing 開始時に有効化する PinballFlipperController（rebar22/21 を操作）")]
    public PinballFlipperController flipperController;

    private enum State { Idle, AtP1, AtP2, Playing }
    private State _state = State.Idle;

    /// <summary>
    /// 現在カメラがフォーカスしているピンボール段階。
    /// P1=1 / P2=2 / P3(Playing)=3 / それ以外(Idle・遷移中)=0。
    /// 他スクリプト（TrayController 等）がこの値の変化を見て連動する。
    /// </summary>
    public int PinBallState
    {
        get
        {
            switch (_state)
            {
                case State.AtP1: return 1;
                case State.AtP2: return 2;
                case State.Playing: return 3;
                default: return 0;
            }
        }
    }
    private bool _moving;
    private bool _pendingEnableFlippers;
    private GameObject _ball;
    private Rigidbody _ballRb;
    private App.Player.FirstPersonController _playerController;

    /// <summary>
    /// playbase2 ボタン押下時に発射するボール prefab。PinballShopView が選択中ショップボールを
    /// セットする。null の間は newBallPrefab にフォールバックする。
    /// </summary>
    private GameObject _selectedBallPrefab;

    /// <summary>
    /// 発射に使うボール prefab を外部（PinballShopView の選択）から指定する。
    /// null を渡すと newBallPrefab にフォールバックする。
    /// </summary>
    public void SetSelectedBallPrefab(GameObject prefab) => _selectedBallPrefab = prefab;

    private void Awake()
    {
        ResolveCamera(); // この時点でカメラのシーンが未ロードでも、後で遅延解決する
        // フリッパーは Playing になるまで無効
        if (flipperController != null) flipperController.enabled = false;
    }

    /// <summary>
    /// 動かすカメラを解決する。Multi-Scene 構成では Main Camera が別シーン（Scene_Environment）に
    /// あり Awake 時点でまだロードされていないことがあるため、必要なタイミングで都度 Camera.main を引く。
    /// </summary>
    private Transform ResolveCamera()
    {
        if (targetCamera == null && Camera.main != null) targetCamera = Camera.main.transform;
        return targetCamera;
    }

    /// <summary>
    /// プレイヤーの一人称コントローラ（視点・移動・ヘッドボブがカメラを毎フレーム上書きする）を
    /// 有効/無効にする。別シーン(Scene_Environment)の Player にあるため実行時に検索する。
    /// セッション中はこれを無効化しないと、カメラ Slerp が即座に上書きされて元の位置に戻ってしまう。
    /// </summary>
    private void SetPlayerControl(bool enabled)
    {
        if (_playerController == null)
            _playerController = FindAnyObjectByType<App.Player.FirstPersonController>();
        if (_playerController != null)
            _playerController.enabled = enabled;
        else
            Debug.LogWarning("[PinballSessionController] FirstPersonController が見つかりません（Scene_Environment 未ロード？）。" +
                             "見つからないとカメラがプレイヤー操作で元に戻る場合があります。");
    }

    private void Update()
    {
        // Playing 移行の次フレームでフリッパーを有効化（移行クリックが誤作動しないように1F遅らせる）
        if (_pendingEnableFlippers)
        {
            if (flipperController != null) flipperController.enabled = true;
            _pendingEnableFlippers = false;
        }

        // どの非 Idle 状態でも Escape でプレイヤー視点へ戻す（移動アニメ中も受け付ける）
        var keyboard = Keyboard.current;
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame && _state != State.Idle)
        {
            ReturnToIdle();
            return;
        }

        if (_moving) return;
        if (_state != State.Idle) return; // AtP1 以降のクリックは新コンポーネント（ショップ/ボタン）が処理する

        var mouse = Mouse.current;
        if (mouse == null) return;
        if (!mouse.leftButton.wasPressedThisFrame) return;

        // Idle: 台を見ている（ホバー）ときだけ開始 → カメラ P1（State==1）へ
        if (machineHover != null && !machineHover.IsHovered) return;
        if (machineHover != null) machineHover.enabled = false; // ハイライト終了
        SetPlayerControl(false); // プレイヤーの視点操作を止める（カメラ Slerp が上書きで戻らないように）
        StartCoroutine(MoveCamera(p1Cam, () => _state = State.AtP1));
    }

    private Vector3 _launchDir;

    /// <summary>
    /// playbase2 ボタンから呼ばれる「発射開始」。State==1（AtP1）のときのみ作動。
    ///   1) 選択中（または既定）のボールを召喚（まだ落下させない）。
    ///   2) このときの newpinballvec の「ローカル -Y」方向を発射方向として記録。
    ///   3) State を 2（AtP2）にし、カメラを P2CAM へ。
    ///   4) launchDelay 秒後に記録した向きへボールを発射し、State を 3（Playing）へ、カメラ P3CAM。
    /// 移動アニメ中・AtP1 以外では無視する。
    /// </summary>
    public void LaunchBall()
    {
        if (_state != State.AtP1) return;

        SpawnBall();

        // playbase2 を押した瞬間の newpinballvec の「ローカル -Y」方向を発射方向として確定。
        // newpinballvec はローカル Z 軸まわりに回転するので、-Y は回転角に応じて向きが変わる。
        if (newpinballvec != null)
        {
            // 角度が 0° ちょうどだと真下に落ちてスタックするため、±1° にランダムへ振る
            var rotator = newpinballvec.GetComponent<PinballVecRotator>();
            if (rotator != null && Mathf.Abs(rotator.CurrentAngle) < 0.01f)
            {
                rotator.SetAngle(Random.value < 0.5f ? 1f : -1f);
            }

            _launchDir = (-newpinballvec.up).normalized; // -up = ローカル -Y のワールド方向
        }
        else
        {
            _launchDir = Vector3.zero;
            Debug.LogWarning("[PinballSessionController] newpinballvec が未割り当てのため発射方向が定まりません（自由落下）。Inspector で newpinballvec を割り当ててください。");
        }

        _state = State.AtP2;
        StartCoroutine(MoveCamera(p2Cam, null));
        StartCoroutine(LaunchAfterDelay());
    }

    /// <summary>launchDelay 秒待ってから、記録済みの向きへボールを発射し Playing へ。</summary>
    private IEnumerator LaunchAfterDelay()
    {
        yield return new WaitForSeconds(launchDelay);

        ReleaseBall(_launchDir);
        _state = State.Playing;            // 以降クリックはフリッパーへ
        _pendingEnableFlippers = true;     // 次フレームでフリッパー有効化
        StartCoroutine(MoveCamera(p3Cam, null));
    }

    /// <summary>
    /// 非 Idle 状態から最初の Idle 状態へ完全に戻す（Escape）。
    /// カメラはプレイヤー操作の再有効化で自動的にプレイヤー視点へ復帰する。
    /// 召喚済みボールは破棄し、フリッパーとハイライトを初期状態に戻す。
    /// </summary>
    public void ReturnToIdle()
    {
        StopAllCoroutines();
        _moving = false;
        _pendingEnableFlippers = false;

        // 召喚済みボールを破棄
        if (_ball != null) Destroy(_ball);
        _ball = null;
        _ballRb = null;

        // フリッパーを無効化
        if (flipperController != null) flipperController.enabled = false;

        // ハイライト（台ホバー）を再有効化
        if (machineHover != null) machineHover.enabled = true;

        _state = State.Idle;

        // プレイヤーの視点・移動操作を戻す（カメラはこれで自動的にプレイヤーへ復帰）
        SetPlayerControl(true);
    }

    /// <summary>カメラを target の位置・回転へ補間移動（回転は Slerp）。</summary>
    private IEnumerator MoveCamera(Transform target, System.Action onArrive)
    {
        ResolveCamera(); // 別シーンのカメラがこの時点でロード済みなら取得
        if (targetCamera == null || target == null)
        {
            if (targetCamera == null)
                Debug.LogWarning("[PinballSessionController] 動かすカメラが見つかりません。" +
                                 "プレイヤーのカメラに 'MainCamera' タグが付いているか、Scene_Environment がロードされているか確認してください。");
            onArrive?.Invoke();
            yield break;
        }

        _moving = true;
        Vector3 startPos = targetCamera.position;
        Quaternion startRot = targetCamera.rotation;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / moveDuration;
            float e = moveEase.Evaluate(Mathf.Clamp01(t));
            targetCamera.position = Vector3.Lerp(startPos, target.position, e);
            targetCamera.rotation = Quaternion.Slerp(startRot, target.rotation, e);
            yield return null;
        }
        targetCamera.position = target.position;
        targetCamera.rotation = target.rotation;
        _moving = false;
        onArrive?.Invoke();
    }

    private void SpawnBall()
    {
        // 選択中ボールがあればそれを、無ければ既定の newBallPrefab を使う
        GameObject prefab = _selectedBallPrefab != null ? _selectedBallPrefab : newBallPrefab;
        if (prefab == null || ballSpawner == null)
        {
            Debug.LogWarning("[PinballSessionController] 発射するボール prefab（選択 or newBallPrefab）または ballSpawner が未設定です。");
            return;
        }
        if (_ball != null) Destroy(_ball); // 念のため前のボールを除去

        _ball = Instantiate(prefab, ballSpawner.position, ballSpawner.rotation);
        _ballRb = _ball.GetComponent<Rigidbody>();
        if (_ballRb != null)
        {
            // 召喚直後は固定（落下させない）
            _ballRb.isKinematic = true;
        }

        // 台のローカル -Y 方向へ重力をかけるコンポーネントを設定（無ければ追加）
        var localGravity = _ball.GetComponent<LocalGravityBody>();
        if (localGravity == null) localGravity = _ball.AddComponent<LocalGravityBody>();
        localGravity.gravitySource = pinballTable;
        localGravity.extraForce = ballExtraForce;
    }

    /// <summary>ボールの物理を起動し、worldDir 方向へ launchForce の impulse で発射する。</summary>
    private void ReleaseBall(Vector3 worldDir)
    {
        if (_ballRb == null) return;
        // Rigidbody を起動して落下開始（重力は LocalGravityBody が台ローカル -Y 方向にかけるので useGravity は OFF のまま）
        _ballRb.isKinematic = false;
        _ballRb.WakeUp();

        // playbase2 押下時の newpinballvec の向きへ発射（向き未設定なら自由落下）
        if (launchForce > 0f && worldDir.sqrMagnitude > 1e-6f)
        {
            _ballRb.AddForce(worldDir.normalized * launchForce, ForceMode.Impulse);
        }
    }
}
