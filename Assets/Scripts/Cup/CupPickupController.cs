using UnityEngine;
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

    private void Update()
    {
        if (lookCamera == null) lookCamera = Camera.main;
        if (lookCamera == null) return;

        Vector2 screenPos = (useMousePointer && Mouse.current != null)
            ? Mouse.current.position.ReadValue()
            : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        Ray ray = lookCamera.ScreenPointToRay(screenPos);
        _lastRay = ray;

        // 視線対象を選択: 状況に応じて BallCup または ExchangeStation を探索
        InteractableHighlight best = FindBestTarget(ray);

        // ハイライト切替
        if (best != _currentLooked)
        {
            if (_currentLooked != null) _currentLooked.ApplyHighlight(false);
            _currentLooked = best;
        }
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
        int cupsConsidered = 0, cupsHit = 0, cupsInteractable = 0;
        int exchangesConsidered = 0, exchangesHit = 0, exchangesInteractable = 0;

        if (IsHoldingBin)
        {
            // Bin を持っている時は exchange だけ探索
            var stations = FindObjectsByType<ExchangeStation>(FindObjectsSortMode.None);
            foreach (var ex in stations)
            {
                if (ex == null) continue;
                exchangesConsidered++;
                bool interactable = ex.IsInteractable(this);
                if (interactable) exchangesInteractable++;
                if (!interactable) continue;
                if (ex.Raycast(ray, out RaycastHit hit, lookMaxDistance))
                {
                    exchangesHit++;
                    if (hit.distance < bestDist)
                    {
                        bestDist = hit.distance;
                        best = ex;
                    }
                }
            }
            _lastDiagnostic = $"Mode=HoldingBin | Exchange考慮={exchangesConsidered} 内Interactable={exchangesInteractable} 内Hit={exchangesHit} | best={(best != null ? best.name : "NONE")}";
        }
        else
        {
            // 何も持っていない時は cup だけ探索
            var cups = FindObjectsByType<BallCup>(FindObjectsSortMode.None);
            foreach (var cup in cups)
            {
                if (cup == null) continue;
                cupsConsidered++;
                bool interactable = cup.IsInteractable(this);
                if (interactable) cupsInteractable++;
                if (!interactable) continue;
                if (cup.Raycast(ray, out RaycastHit hit, lookMaxDistance))
                {
                    cupsHit++;
                    if (hit.distance < bestDist)
                    {
                        bestDist = hit.distance;
                        best = cup;
                    }
                }
            }
            _lastDiagnostic = $"Mode=Idle | Cup考慮={cupsConsidered} 内Interactable={cupsInteractable} 内Hit={cupsHit} | best={(best != null ? best.name : "NONE")}";
        }

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
        return mouse.leftButton.wasPressedThisFrame;
    }

    private void HandleClick(InteractableHighlight target)
    {
        if (target is BallCup cup) PickupCup(cup);
        else if (target is ExchangeStation ex) ExchangeAtStation(ex);
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
        _currentLooked = null; // 次フレームで再評価
    }

    private void ExchangeAtStation(ExchangeStation station)
    {
        if (!IsHoldingBin || station == null) return;

        var newCup = station.AcceptBin(_heldBin);
        if (logEvents) Debug.Log($"[CupPickup] Bin → cup at exchange: newCup={(newCup != null ? newCup.name : "NULL")}");
        _heldBin = null;
        _currentLooked = null;
    }
}
