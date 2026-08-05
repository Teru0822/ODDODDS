using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// プレイヤーにアタッチするピックアップコントローラ。
/// 毎フレームカメラから raycast し、BallCup または ExchangeStation を照準した時に水色ハイライト。
/// 左クリックで pickup (cup → Bin) または exchange (Bin → cup) を実行する。
/// </summary>
public class CupPickupController : MonoBehaviour
{
    [Header("視点 (カメラ)")]
    [Tooltip("レイの発信元カメラ。null なら Camera.main")]
    public Camera lookCamera;

    [Tooltip("マウスポインタ座標で raycast (false なら画面中央 = レティクル)")]
    public bool useMousePointer = false;

    [Tooltip("インタラクション最大距離 (m)")]
    public float lookMaxDistance = 5f;

    [Header("Bin 保持位置")]
    [Tooltip("Bin を持つ手の位置 Transform (カメラ配下の空 GameObject 推奨)")]
    public Transform handHolder;

    [Tooltip("Bin の手元保持位置オフセット (handHolder のローカル空間)")]
    public Vector3 binLocalPositionOffset = Vector3.zero;

    [Tooltip("Bin の手元保持回転 (Euler / handHolder のローカル空間)")]
    public Vector3 binLocalEulerOffset = Vector3.zero;

    [Tooltip("Bin の手元保持時のローカルスケール (Vector3.one でプレハブ通り)")]
    public Vector3 binLocalScale = Vector3.one;

    [Header("デバッグ")]
    [Tooltip("ハイライト/操作のログを Console に出力")]
    public bool logEvents = false;

    [Tooltip("毎フレームのレイ判定状況を Scene ビューで赤線として表示")]
    public bool drawRayGizmo = false;

    [Tooltip("見つけた対象の名前を画面 OnGUI に表示")]
    public bool showOnGUIDiagnostics = false;

    public bool IsHoldingBin => _heldBin != null;
    public CupBin HeldBin => _heldBin;

    private CupBin _heldBin;
    private InteractableHighlight _currentLooked;
    private Ray _lastRay;
    private string _lastDiagnostic = "";

    private void Start()
    {
        if (lookCamera == null) lookCamera = Camera.main;
    }

    private void OnDisable()
    {
        SetCurrentLooked(null);
    }

    /// <summary>照準対象を切り替え、ハイライトと OnLookEnter/OnLookExit 通知を一元管理する。</summary>
    private void SetCurrentLooked(InteractableHighlight target)
    {
        if (_currentLooked == target) return;
        if (_currentLooked != null)
        {
            _currentLooked.ApplyHighlight(false);
            _currentLooked.OnLookExit();
        }
        _currentLooked = target;
        if (_currentLooked != null)
        {
            _currentLooked.OnLookEnter();
        }
    }

    private void Update()
    {
        if (App.Input.GameInputGate.IsBlocked || UFOCameraController.IsPlayingUfo)
        {
            SetCurrentLooked(null);
            return;
        }

        if (lookCamera == null) lookCamera = Camera.main;
        if (lookCamera == null) return;

        Vector2 screenPos = (useMousePointer && Mouse.current != null)
            ? Mouse.current.position.ReadValue()
            : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        Ray ray = lookCamera.ScreenPointToRay(screenPos);
        _lastRay = ray;

        // 視線対象を選択: 状況に応じて BallCup または ExchangeStation を探索
        InteractableHighlight best = FindBestTarget(ray);

        // ハイライト切替 (enter/exit 通知込み)
        SetCurrentLooked(best);
        if (_currentLooked != null)
        {
            _currentLooked.ApplyHighlight(true);
        }

        // 左クリック処理
        if (_currentLooked != null && IsLeftClickPressed())
        {
            HandleClick(_currentLooked);
        }
    }

    private InteractableHighlight FindBestTarget(Ray ray)
    {
        InteractableHighlight best = null;
        float bestDist = float.PositiveInfinity;

        // すべての InteractableHighlight 派生 (BallCup / ExchangeStation / ExchangeButton 等) を対象に探索。
        // 各派生の IsInteractable(this) が現在のプレイヤー状態 (Bin 保持中など) に応じて可否を決める。
        var all = FindObjectsByType<InteractableHighlight>(FindObjectsSortMode.None);
        int considered = 0, interactable = 0, hits = 0;
        foreach (var item in all)
        {
            if (item == null) continue;
            considered++;
            if (!item.IsInteractable(this)) continue;
            interactable++;
            if (item.Raycast(ray, out RaycastHit hit, lookMaxDistance) && hit.distance < bestDist)
            {
                hits++;
                bestDist = hit.distance;
                best = item;
            }
        }

        string mode = IsHoldingBin ? "HoldingBin" : "Idle";
        string bestStr = best != null ? $"{best.name}({best.GetType().Name})" : "NONE";
        _lastDiagnostic = $"Mode={mode} | 考慮={considered} Interactable={interactable} Hit={hits} | best={bestStr}";

        if (logEvents && Time.frameCount % 30 == 0)
        {
            Debug.Log($"[CupPickup] {_lastDiagnostic}");
        }
        return best;
    }

    private void OnDrawGizmos()
    {
        if (!drawRayGizmo) return;
        Gizmos.color = Color.red;
        Gizmos.DrawRay(_lastRay.origin, _lastRay.direction * lookMaxDistance);
    }

    private void OnGUI()
    {
        if (!showOnGUIDiagnostics) return;
        GUI.Box(new Rect(10, 10, 500, 24), $"[CupPickup] {_lastDiagnostic} | held={(_heldBin != null ? _heldBin.name : "none")}");
    }

    private bool IsLeftClickPressed()
    {
        var mouse = Mouse.current;
        if (mouse == null) return false;
        if (!mouse.leftButton.wasPressedThisFrame) return false;
        // UI 要素 (報酬選択ダイアログなど) 上のクリックは EventSystem に任せ、ワールドへは流さない
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return false;
        return true;
    }

    private void HandleClick(InteractableHighlight target)
    {
        switch (target)
        {
            case BallCup cup: PickupCup(cup); break;
            case ExchangeStation ex: ExchangeAtStation(ex); break;
            case ExchangeButton btn:
                btn.OnPressed();
                if (logEvents) Debug.Log($"[CupPickup] ExchangeButton pressed: {btn.name}");
                SetCurrentLooked(null); // 押した直後は対象を再評価 (累計値 0 になればハイライトも消える)
                break;
            case TypewriterInteractable tw:
                tw.OnPressed();
                if (logEvents) Debug.Log($"[CupPickup] Typewriter clicked: {tw.name}");
                SetCurrentLooked(null); // 直後は UI モードに入るので対象を再評価
                break;
        }
    }

    private void PickupCup(BallCup cup)
    {
        if (IsHoldingBin) return;
        if (cup == null || cup.BinPrefab == null) return;

        // cup の中身を取り出す (ボールは SetActive(false) になる)
        var balls = cup.TakeContents();

        // Bin を生成して手元に保持
        Transform parent = handHolder != null ? handHolder : transform;
        Vector3 worldPos = parent.TransformPoint(binLocalPositionOffset);
        Quaternion worldRot = parent.rotation * Quaternion.Euler(binLocalEulerOffset);

        GameObject binGo = Instantiate(cup.BinPrefab, worldPos, worldRot, parent);
        // ローカル変換を再適用 (Instantiate でワールド指定したのを localScale だけ追加調整)
        binGo.transform.localPosition = binLocalPositionOffset;
        binGo.transform.localRotation = Quaternion.Euler(binLocalEulerOffset);
        binGo.transform.localScale = binLocalScale;

        var bin = binGo.GetComponent<CupBin>();
        if (bin == null) bin = binGo.AddComponent<CupBin>();
        bin.heldBalls = balls;

        // ボールを手元のラインに追随させたいわけではないので、世界座標で SetActive(false) のままにする
        // (exchange 時に物理位置を上書きするので参照だけ保持で十分)

        _heldBin = bin;

        if (logEvents) Debug.Log($"[CupPickup] cup → Bin: balls={balls.Count}, cup={cup.name}");

        Destroy(cup.gameObject);
        SetCurrentLooked(null); // 次フレームで再評価
    }

    private void ExchangeAtStation(ExchangeStation station)
    {
        if (!IsHoldingBin || station == null) return;

        var newCup = station.AcceptBin(_heldBin);
        if (logEvents) Debug.Log($"[CupPickup] Bin → cup at exchange: newCup={(newCup != null ? newCup.name : "NULL")}");
        _heldBin = null;
        SetCurrentLooked(null);
    }
}
