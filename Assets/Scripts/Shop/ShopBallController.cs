using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

/// <summary>
/// shop_ball ルートにアタッチするコントローラ。
/// 子スロット (ShopBallSlot) を集約し、毎フレームカメラから raycast して、
/// 見つめているスロットを緑/赤にハイライトする。
/// 緑の状態で左クリックされたら未洗浄金を引き、指定 spawn target にボールを生成する。
/// </summary>
public class ShopBallController : MonoBehaviour
{
    [Header("視点（カメラ）")]
    [Tooltip("視線判定に使うカメラ。null なら Camera.main")]
    public Camera lookCamera;

    [Tooltip("マウスポインタ座標で raycast する (false なら画面中央)")]
    public bool useMousePointer = false;

    [Tooltip("レイの最大距離")]
    public float lookMaxDistance = 50f;

    [Header("ボール生成")]
    [Tooltip("購入したボールを生成する位置 (例: ピンボールのプランジャー位置)")]
    public Transform ballSpawnPoint;

    [Tooltip("生成ボールに与える初速 (spawn target のローカル空間)")]
    public Vector3 initialVelocityLocal = Vector3.zero;

    [Tooltip("生成ボールの transform.localScale に掛ける倍率")]
    public float spawnScaleMultiplier = 1f;

    [Tooltip("生成ボールの親 (null なら spawn target を親)")]
    public Transform spawnParent;

    [Header("イベント")]
    [Tooltip("購入が成立したときに発火 (ShopBallSlot を引数に)")]
    public PurchaseEvent onPurchase;

    [System.Serializable] public class PurchaseEvent : UnityEvent<ShopBallSlot> { }

    private ShopBallSlot[] _slots;
    private ShopBallSlot _currentLooked;

    private void Start()
    {
        _slots = GetComponentsInChildren<ShopBallSlot>(true);
        if (lookCamera == null) lookCamera = Camera.main;
        PropagateCameraToSlots();
    }

    private void PropagateCameraToSlots()
    {
        if (_slots == null) return;
        for (int i = 0; i < _slots.Length; i++)
        {
            if (_slots[i] != null) _slots[i].SetBillboardCamera(lookCamera);
        }
    }

    /// <summary>外部から lookCamera を後付けする時は、これを呼んで slot に同期する</summary>
    public void SetLookCamera(Camera cam)
    {
        lookCamera = cam;
        PropagateCameraToSlots();
    }

    private void OnDisable()
    {
        if (_currentLooked != null)
        {
            _currentLooked.ApplyHighlight(false, false);
            _currentLooked = null;
        }
    }

    private void Update()
    {
        if (UFOCameraController.IsPlayingUfo)
        {
            if (_currentLooked != null)
            {
                _currentLooked.ApplyHighlight(false, false);
                _currentLooked = null;
            }
            return;
        }

        if (lookCamera == null) lookCamera = Camera.main;
        if (lookCamera == null || _slots == null || _slots.Length == 0) return;

        // 視線レイ
        Vector2 screenPos = (useMousePointer && Mouse.current != null)
            ? Mouse.current.position.ReadValue()
            : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        Ray ray = lookCamera.ScreenPointToRay(screenPos);

        // 最も近いスロットを選ぶ
        ShopBallSlot bestSlot = null;
        float bestDist = float.PositiveInfinity;
        for (int i = 0; i < _slots.Length; i++)
        {
            var slot = _slots[i];
            if (slot == null || slot.IsPurchased) continue;
            if (slot.Raycast(ray, out RaycastHit hit, lookMaxDistance) && hit.distance < bestDist)
            {
                bestDist = hit.distance;
                bestSlot = slot;
            }
        }

        // ハイライト切替
        if (bestSlot != _currentLooked)
        {
            if (_currentLooked != null) _currentLooked.ApplyHighlight(false, false);
            _currentLooked = bestSlot;
        }

        if (_currentLooked == null) return;

        bool affordable = CanAfford(_currentLooked.Price);
        _currentLooked.ApplyHighlight(true, affordable);

        // 左クリック → 緑のときのみ購入成立
        if (affordable && IsLeftClickPressed())
        {
            // ポインタが緑の対象上にあるかは現状の bestSlot が示している
            Purchase(_currentLooked);
        }
    }

    private bool IsLeftClickPressed()
    {
        var mouse = Mouse.current;
        if (mouse == null) return false;
        return mouse.leftButton.wasPressedThisFrame;
    }

    private bool CanAfford(float price)
    {
        return UnwashedMoneyManager.Instance != null
            && UnwashedMoneyManager.Instance.CanAfford(price);
    }

    private void Purchase(ShopBallSlot slot)
    {
        if (slot == null || slot.BallPrefab == null) return;
        if (UnwashedMoneyManager.Instance == null)
        {
            Debug.LogWarning("[ShopBallController] UnwashedMoneyManager がシーンに存在しません。");
            return;
        }
        if (!UnwashedMoneyManager.Instance.TrySpend(slot.Price))
        {
            Debug.LogWarning($"[ShopBallController] 残高不足で購入失敗: price={slot.Price}");
            return;
        }

        Vector3 pos = ballSpawnPoint != null ? ballSpawnPoint.position : slot.transform.position;
        Quaternion rot = ballSpawnPoint != null ? ballSpawnPoint.rotation : Quaternion.identity;
        Transform parent = spawnParent != null ? spawnParent : ballSpawnPoint;

        // 親なしで生成してからワールド変換維持で親子化 (親の lossyScale で巨大化するのを防ぐ)
        GameObject spawned = Instantiate(slot.BallPrefab, pos, rot);
        if (parent != null)
        {
            spawned.transform.SetParent(parent, true); // worldPositionStays=true でワールドスケールを保持
        }

        if (!Mathf.Approximately(spawnScaleMultiplier, 1f))
        {
            spawned.transform.localScale *= spawnScaleMultiplier;
        }

        // ボール固有の価値を slot.price で初期化 (Exchange モニターが最終的に読む値)
        var ballController = spawned.GetComponent<PinballBallController>();
        if (ballController != null)
        {
            ballController.currentValue = slot.Price;
        }

        if (initialVelocityLocal != Vector3.zero)
        {
            var rb = spawned.GetComponent<Rigidbody>();
            if (rb != null)
            {
                Vector3 worldVel = ballSpawnPoint != null
                    ? ballSpawnPoint.TransformDirection(initialVelocityLocal)
                    : initialVelocityLocal;
                rb.linearVelocity = worldVel;
            }
        }

        slot.MarkPurchased();
        onPurchase?.Invoke(slot);
        Debug.Log($"[ShopBallController] 購入成立: {slot.BallPrefab.name} (¥{slot.Price}) → spawn at {pos}");
    }
}
