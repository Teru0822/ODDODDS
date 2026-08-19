using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Formats.Alembic.Importer;

/// <summary>
/// Tab キーで本をプレイヤーの目の前に出す演出。
/// 画面の左下から弧を描くように（Slerp で）せり上がってきて、
/// 移動しながら Alembic の「開く」アニメーションを再生する。
/// もう一度 Tab を押すと逆再生で閉じながら左下へ戻る。
///
/// 【必要なもの】
///   Book Prefab    … .abc をインポートして出来たオブジェクトをプレハブ化したもの。
///                    AlembicStreamPlayer は本体か子のどこかに付いていれば自動で拾う。
///   Open Reference … InputSystem_Actions の「OpenUI」アクション（Tab に割り当て済み）。
///
/// 【置き場所】
///   シーンに空オブジェクトを作って付ける。本はここから生成するので、
///   あらかじめシーンに置いておく必要はない。
/// </summary>
[DisallowMultipleComponent]
public class BookOpenController : MonoBehaviour
{
    private enum State { Hidden, Opening, Shown, Closing }

    [Header("本のオブジェクト")]
    [Tooltip("シーンに配置済みの本。ビルドで使うならこちらを推奨。 " +
             "Alembic はビルド時にシーンを走査して .abc を StreamingAssets へ集めるため、" +
             "実行時に Instantiate しただけの本はビルドに含まれず表示されない")]
    [SerializeField] private GameObject _bookInstance;

    [Tooltip("本のプレハブ。Book Instance が未設定のときだけ実行時に生成する（エディタ確認用）")]
    [SerializeField] private GameObject _bookPrefab;

    [Tooltip("起動時に生成しておく。初回 Tab のカクつきを防げる")]
    [SerializeField] private bool _spawnOnAwake = true;

    [Header("入力")]
    [Tooltip("開閉に使う入力。InputSystem_Actions の OpenUI (Tab) を指定する")]
    [SerializeField] private InputActionReference _openReference;

    [Tooltip("Tab の情報メニューを本に置き換える。オフにすると本とメニューが同時に出る")]
    [SerializeField] private bool _replaceInfoMenu = true;

    [Tooltip("Esc キーでも閉じられるようにする。開いている間 Esc は設定画面に渡さない")]
    [SerializeField] private bool _closeWithEscape = true;

    [Header("位置（カメラから見たローカル座標）")]
    [Tooltip("待機位置。画面の左下に隠れている状態")]
    [SerializeField] private Vector3 _hiddenLocalPosition = new Vector3(-0.45f, -0.55f, 0.35f);

    [Tooltip("表示位置。プレイヤーの目の前")]
    [SerializeField] private Vector3 _shownLocalPosition = new Vector3(0f, -0.12f, 0.5f);

    [Tooltip("待機時の角度。伏せて傾けておくと出てくる時の印象が良い")]
    [SerializeField] private Vector3 _hiddenLocalEuler = new Vector3(35f, -55f, 25f);

    [Tooltip("表示時の角度")]
    [SerializeField] private Vector3 _shownLocalEuler = new Vector3(12f, 0f, 0f);

    [Header("動き")]
    [Tooltip("出てくるのにかける時間(秒)")]
    [SerializeField] private float _openDuration = 0.6f;

    [Tooltip("しまうのにかける時間(秒)")]
    [SerializeField] private float _closeDuration = 0.45f;

    [Tooltip("移動の緩急。既定は ease-in-out")]
    [SerializeField] private AnimationCurve _moveCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("追加回転")]
    [Tooltip("出てくる間にさらに回す角度(度)。X=90 で、移動しながら手前へ90度起き上がる")]
    [SerializeField] private Vector3 _extraSpinEuler = new Vector3(90f, 0f, 0f);

    [Tooltip("追加回転の進み方。移動カーブとは別に調整できる")]
    [SerializeField] private AnimationCurve _spinCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Tooltip("本自身の軸で回す。オフにするとカメラ基準の軸で回る")]
    [SerializeField] private bool _spinInLocalSpace = true;

    [Header("Alembic アニメーション")]
    [Tooltip("開くアニメの長さ(秒)。0 なら Alembic ファイルの長さをそのまま使う")]
    [SerializeField] private float _animationDuration = 0f;

    [Tooltip("アニメの進み方。移動より遅らせると「持ち上げてから開く」印象になる")]
    [SerializeField] private AnimationCurve _animationCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Tooltip("閉じる時にアニメを逆再生する。オフなら開いたまましまう")]
    [SerializeField] private bool _reverseOnClose = true;

    [Header("開かない条件")]
    [Tooltip("ATM・インターホン・取り立て・UFOキャッチャー等の最中は反応しない")]
    [SerializeField] private bool _blockWhileBusy = true;

    [Header("デバッグ")]
    [SerializeField] private bool _logEvents = false;

    /// <summary>本の演出で Tab の情報メニューを置き換えているか。GameUIManager が参照する。</summary>
    public static bool SuppressInfoMenu { get; private set; }

    /// <summary>本が出ている（出てくる途中を含む）か。</summary>
    public static bool IsBookVisible { get; private set; }

    /// <summary>生成済みの本のインスタンス。ページ UI をぶら下げるのに使う。</summary>
    public GameObject BookInstance => _book;

    /// <summary>開き切って静止しているか。ページの表示切り替えに使う。</summary>
    public bool IsFullyOpen => _state == State.Shown;

    /// <summary>開く操作が行われた瞬間に発火する。</summary>
    public event System.Action OnBookOpened;

    /// <summary>閉じる操作が行われた瞬間に発火する。</summary>
    public event System.Action OnBookClosed;

    private GameObject _book;
    private AlembicStreamPlayer _stream;
    private Camera _camera;
    private App.Player.FirstPersonController _fpController;
    private Coroutine _cursorRestoreRoutine;

    private State _state = State.Hidden;
    private float _t;      // 0 = 左下に収納 / 1 = 目の前
    private float _animT;  // Alembic の再生位置 0..1

    private void OnEnable()
    {
        SuppressInfoMenu = _replaceInfoMenu;
        if (_openReference != null) _openReference.action.Enable();
    }

    private void OnDisable()
    {
        // 無効化のままフラグが残るとメニューが二度と開けなくなる
        SuppressInfoMenu = false;
        IsBookVisible = false;

        // 開いたまま無効化されると、プレイヤーが操作不能のまま取り残される
        if (_state != State.Hidden)
        {
            _state = State.Hidden;
            _t = 0f;
            if (_book != null) _book.SetActive(false);
            ReleaseControl();
        }
    }

    private void Start()
    {
        if (_bookInstance == null && _bookPrefab == null)
        {
            Debug.LogError("[BookOpenController] 本が未設定です。" +
                           "Book Instance（シーン配置済み）か Book Prefab のどちらかを指定してください。", this);
            enabled = false;
            return;
        }

        if (_spawnOnAwake) EnsureBook();
    }

    private void Update()
    {
        SuppressInfoMenu = _replaceInfoMenu;

        bool togglePressed = _openReference != null && _openReference.action.WasPressedThisFrame();
        bool escapePressed = _closeWithEscape && Keyboard.current != null
                             && Keyboard.current.escapeKey.wasPressedThisFrame;

        if (IsOpenOrOpening)
        {
            // 閉じる操作は常に受け付ける。他の演出が始まっても閉じられなくならないようにする
            if (togglePressed || escapePressed) Close();
        }
        else if (togglePressed && CanToggle())
        {
            Open();
        }

        AdvanceState();
        ApplyAnimation();
    }

    private void LateUpdate()
    {
        // カメラが動いた後に追従させて、視点移動時のガタつきを防ぐ
        ApplyTransform();
    }

    /// <summary>他の演出・操作中は本を出さない。レティクルを隠す条件と揃えてある。</summary>
    private bool CanToggle()
    {
        if (!_blockWhileBusy) return true;

        if (SettingUIManager.IsMenuOpen) return false;
        if (UFOCameraController.IsPlayingUfo) return false;
        if (RewardSelectionUI.IsTypewriterUIShowing) return false;
        if (App.ATM.ATMController.IsInteracting) return false;
        if (VisitorSystem.IsTalkingWithVisitor) return false;
        if (DebtCollectionManager.IsCollecting) return false;

        return true;
    }

    private bool IsOpenOrOpening => _state == State.Opening || _state == State.Shown;

    public void Open()
    {
        EnsureBook();
        if (_book == null) return;

        _book.SetActive(true);
        IsBookVisible = true;
        _state = State.Opening;

        // 開き直す瞬間に変な姿勢が見えないよう、その場で位置を確定させる
        ApplyTransform();
        AcquireControl();
        OnBookOpened?.Invoke();

        if (_logEvents) Debug.Log("[BookOpenController] 本を開きます", this);
    }

    public void Close()
    {
        if (_book == null) return;

        _state = State.Closing;
        ReleaseControl();
        OnBookClosed?.Invoke();

        if (_logEvents) Debug.Log("[BookOpenController] 本をしまいます", this);
    }

    /// <summary>
    /// 本を読んでいる間の入力状態にする。
    /// プレイヤー操作を止め、マウスカーソルを出し、Esc を本を閉じる用に専有する。
    /// </summary>
    private void AcquireControl()
    {
        if (_cursorRestoreRoutine != null)
        {
            StopCoroutine(_cursorRestoreRoutine);
            _cursorRestoreRoutine = null;
        }

        // 専有しておかないと、閉じるつもりの Esc で設定画面が開いてしまう
        if (_closeWithEscape) App.Input.GameInputGate.CaptureEscape(this);

        if (EnsureFpController()) _fpController.enabled = false;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    /// <summary>通常のプレイヤー操作へ戻す。</summary>
    private void ReleaseControl()
    {
        App.Input.GameInputGate.ReleaseEscape(this);

        if (EnsureFpController()) _fpController.enabled = true;

        if (isActiveAndEnabled)
        {
            _cursorRestoreRoutine = StartCoroutine(RestoreCursor());
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    /// <summary>
    /// Unity は Esc が押されるとカーソルロックを自動解除するため、
    /// 1 フレームの代入では戻り切らずカーソルが残ることがある。数フレーム再適用する。
    /// </summary>
    private IEnumerator RestoreCursor()
    {
        const int reapplyFrames = 5;

        for (int i = 0; i < reapplyFrames; i++)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            yield return null;
        }

        _cursorRestoreRoutine = null;
    }

    private bool EnsureFpController()
    {
        if (_fpController != null) return true;

        _fpController = FindFirstObjectByType<App.Player.FirstPersonController>();
        return _fpController != null;
    }

    private void AdvanceState()
    {
        switch (_state)
        {
            case State.Opening:
                _t = StepTowards(_t, 1f, _openDuration);
                if (_t >= 1f) _state = State.Shown;
                break;

            case State.Closing:
                _t = StepTowards(_t, 0f, _closeDuration);
                if (_t <= 0f)
                {
                    _state = State.Hidden;
                    IsBookVisible = false;
                    if (_book != null) _book.SetActive(false);
                }
                break;
        }

        // 閉じる時にアニメを戻さない設定なら、開き切った状態のまま固定する
        _animT = (_state == State.Closing && !_reverseOnClose) ? 1f : _t;
    }

    private static float StepTowards(float current, float target, float duration)
    {
        if (duration <= 0f) return target;

        return Mathf.MoveTowards(current, target, Time.deltaTime / duration);
    }

    /// <summary>
    /// 左下の待機位置から目の前へ Slerp で移動させる。
    /// カメラ基準のローカル座標どうしを Vector3.Slerp すると、原点まわりに弧を描いて
    /// せり上がってくる動きになる（直線補間にはならない）。
    /// </summary>
    private void ApplyTransform()
    {
        if (_book == null || _state == State.Hidden) return;
        if (!EnsureCamera()) return;

        float e = _moveCurve != null ? _moveCurve.Evaluate(_t) : _t;

        Vector3 localPos = Vector3.Slerp(_hiddenLocalPosition, _shownLocalPosition, e);
        Quaternion localRot = Quaternion.Slerp(Quaternion.Euler(_hiddenLocalEuler),
                                               Quaternion.Euler(_shownLocalEuler), e);

        // 待機→表示の姿勢変化に、さらに回転を上乗せする（既定は X 軸 90 度）
        float spinT = _spinCurve != null ? _spinCurve.Evaluate(_t) : _t;
        Quaternion spin = Quaternion.Euler(_extraSpinEuler * spinT);
        localRot = _spinInLocalSpace ? localRot * spin : spin * localRot;

        Transform cam = _camera.transform;
        _book.transform.SetPositionAndRotation(cam.TransformPoint(localPos), cam.rotation * localRot);
    }

    /// <summary>Alembic の再生位置を進捗に合わせて動かす。</summary>
    private void ApplyAnimation()
    {
        if (_stream == null || _state == State.Hidden) return;

        float span = _animationDuration > 0f ? _animationDuration : _stream.Duration;
        if (span <= 0f) return;

        float e = _animationCurve != null ? _animationCurve.Evaluate(_animT) : _animT;

        // CurrentTime は [0, Duration] にクランプされる仕様なので 0 起点で渡す
        _stream.CurrentTime = Mathf.Clamp(e * span, 0f, _stream.Duration);
    }

    private void EnsureBook()
    {
        if (_book != null) return;

        if (_bookInstance != null)
        {
            // シーンに置いてある本をそのまま使う。
            // Alembic のビルド後処理がシーンを走査して .abc を回収できるのはこちらだけ
            _book = _bookInstance;
        }
        else if (_bookPrefab != null)
        {
            _book = Instantiate(_bookPrefab);
            _book.name = _bookPrefab.name + " (Book)";
        }
        else
        {
            return;
        }

        _stream = _book.GetComponentInChildren<AlembicStreamPlayer>(true);

        if (_stream == null)
        {
            Debug.LogWarning($"[BookOpenController] '{_book.name}' に AlembicStreamPlayer が見つかりません。" +
                             "移動だけ行い、開くアニメーションは再生されません。", this);
        }

        _book.SetActive(false);
    }

    private bool EnsureCamera()
    {
        if (_camera != null && _camera.isActiveAndEnabled) return true;

        _camera = Camera.main;
        return _camera != null;
    }
}
