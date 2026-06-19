using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// ピンボール台への一連のインタラクションを管理する状態機械。
///
///   [Idle]    台に視点を向けると MouseHoverOutline が輪郭をハイライト。
///             左クリック → カメラが P1CAM へ Slerp 移動・回転。
///   [AtP1]    左クリック → カメラが P2CAM へ移動し、BallSpawner の位置に newBall を召喚（落下はまだ）。
///   [AtP2]    左クリック → newBall の Rigidbody を起動して落下開始、カメラが P3CAM へ移動、
///             同時にフリッパー操作（左右クリックで newpinball_rebar22/21）を有効化。
///   [Playing] 以降クリックは PinballFlipperController（フリッパー）へ。状態遷移はしない。
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

    [Min(0f)]
    [Tooltip("落下開始時に加えるランダムな力の大きさ（impulse）。0 なら自由落下のみ")]
    public float releaseForce = 1.5f;

    [Tooltip("ランダム力を水平方向(XZ)のみにする。オフなら上下も含む全方向ランダム")]
    public bool releaseForceHorizontalOnly = true;

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

        if (_moving) return;
        if (_state == State.Playing) return; // 以降のクリックはフリッパーへ

        var mouse = Mouse.current;
        if (mouse == null) return;
        if (!mouse.leftButton.wasPressedThisFrame) return;

        switch (_state)
        {
            case State.Idle:
                // 台を見ている（ホバー）ときだけ開始
                if (machineHover != null && !machineHover.IsHovered) return;
                if (machineHover != null) machineHover.enabled = false; // ハイライト終了
                SetPlayerControl(false); // プレイヤーの視点操作を止める（カメラ Slerp が上書きで戻らないように）
                StartCoroutine(MoveCamera(p1Cam, () => _state = State.AtP1));
                break;

            case State.AtP1:
                SpawnBall();
                StartCoroutine(MoveCamera(p2Cam, () => _state = State.AtP2));
                break;

            case State.AtP2:
                ReleaseBall();
                _state = State.Playing;            // 即 Playing（以降クリックはフリッパーへ）
                _pendingEnableFlippers = true;     // 次フレームでフリッパー有効化
                StartCoroutine(MoveCamera(p3Cam, null));
                break;
        }
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
        if (newBallPrefab == null || ballSpawner == null)
        {
            Debug.LogWarning("[PinballSessionController] newBallPrefab または ballSpawner が未設定です。");
            return;
        }
        if (_ball != null) Destroy(_ball); // 念のため前のボールを除去

        _ball = Instantiate(newBallPrefab, ballSpawner.position, ballSpawner.rotation);
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

    private void ReleaseBall()
    {
        if (_ballRb == null) return;
        // Rigidbody を起動して落下開始（重力は LocalGravityBody が台ローカル -Y 方向にかけるので useGravity は OFF のまま）
        _ballRb.isKinematic = false;
        _ballRb.WakeUp();

        // 自由落下ではなく、ランダムな向きに少しだけ弾く（毎回違う落ち方になる）
        if (releaseForce > 0f)
        {
            Vector3 dir = releaseForceHorizontalOnly
                ? new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f)).normalized
                : Random.onUnitSphere;
            if (dir.sqrMagnitude < 1e-6f) dir = Vector3.forward; // 念のためゼロ回避
            _ballRb.AddForce(dir * releaseForce, ForceMode.Impulse);
        }
    }
}
