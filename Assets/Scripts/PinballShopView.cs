using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// PinballSessionController.PinBallState == 1（AtP1）の間だけ有効になる「ショップ／エイミング画面」管理。
///
///   State==1 に入った瞬間:
///     - 各 shopBalls[i].positionAnchor の位置に shopBalls[i].prefab をインスタンス化して陳列。
///     - マウスカーソルを表示（ロック解除）。
///     - Scene_Environment の「Spot Light」を起動（GameObject を Active 化）。
///   State==1 の間:
///     - マウスカーソルが乗ったショップボールの輪郭をハイライト（hoverColor）。
///     - 左クリックで選択。選択中のボールは常に赤（selectedColor）で輪郭表示。
///     - 選択された prefab を PinballSessionController に渡す（playbase2 ボタン発射時に使われる）。
///   State==1 を出た瞬間（Escape で 0、または playbase2 で発射して 3 など）:
///     - 陳列ボールを破棄、カーソル非表示（ロック）、Spot Light を消灯。
///
/// 輪郭は既存のポストプロセス式 OutlineHighlightUtil / OutlineRendererFeature を流用。
/// 参照は Inspector で割り当てる（Scene_PinBall 内）。Spot Light は別シーン（Scene_Environment）に
/// あるため Inspector では割り当てられない（保存時 null 化）。名前で実行時検索する。
/// </summary>
[DisallowMultipleComponent]
public class PinballShopView : MonoBehaviour
{
    [System.Serializable]
    public class ShopBallEntry
    {
        [Tooltip("陳列位置の空オブジェクト（例: shop_ball1Pos）")]
        public Transform positionAnchor;

        [Tooltip("陳列・選択する見た目用 prefab（例: shop_ball1）。ショップに並ぶオブジェクト")]
        public GameObject prefab;

        [Tooltip("これを選択して play した際に実際にスポーン／発射されるボール prefab。" +
                 "陳列用 prefab とは別物を指定できる。未設定なら陳列用 prefab をそのまま使う")]
        public GameObject ballPrefab;
    }

    [Header("セッション参照")]
    [Tooltip("State を読む PinballSessionController。null なら実行時に自動取得")]
    public PinballSessionController session;

    [Tooltip("マウスレイ発信元のカメラ。null なら Camera.main")]
    public Camera lookCamera;

    [Header("ショップボール（位置アンカー ↔ prefab）")]
    [Tooltip("shop_ball1Pos→shop_ball1, shop_ball2Pos→shop_ball2, ... を要素ごとに設定")]
    public ShopBallEntry[] shopBalls;

    [Header("ハイライト（輪郭）")]
    [Tooltip("マウスを乗せた（ホバー中）ボールの輪郭色")]
    public Color hoverColor = new Color(0.2f, 0.6f, 1f, 1f);

    [Tooltip("選択中ボールの輪郭色（常時表示）")]
    public Color selectedColor = new Color(1f, 0.15f, 0.15f, 1f);

    [Tooltip("輪郭の太さ（ピクセル）")]
    [Min(1f)]
    public float outlineWidth = 4f;

    [Tooltip("Raycast の最大距離（m）")]
    public float maxDistance = 100f;

    [Header("スポットライト（Scene_Environment）")]
    [Tooltip("起動／消灯する Spot Light の名前。別シーンにあるため実行時に名前検索する")]
    public string spotLightName = "Spot Light";

    [Tooltip("同一シーンに置く場合はここへ直接割り当て可（指定時は名前検索より優先）")]
    public GameObject spotLightObject;

    [Header("カーソル")]
    [Tooltip("State==1 のときだけカーソルを表示し、それ以外で非表示（ロック）にする")]
    public bool manageCursor = true;

    // 陳列インスタンスごとの情報
    private readonly List<GameObject> _instances = new List<GameObject>();
    private readonly List<List<Renderer>> _instanceRenderers = new List<List<Renderer>>();
    private readonly List<Collider[]> _instanceColliders = new List<Collider[]>();
    private readonly List<GameObject> _launchPrefabs = new List<GameObject>(); // 選択時に発射するボール prefab

    private MaterialPropertyBlock _mpb;
    private int _selectedIndex = -1;
    private int _prevState = -1;
    private GameObject _spotLightGo;
    private bool _inShop;
    private bool _lightInitialized;

    private void Awake()
    {
        _mpb = new MaterialPropertyBlock();
        if (session == null) session = FindAnyObjectByType<PinballSessionController>();
    }

    private void Update()
    {
        if (session == null) return;

        int state = session.PinBallState;
        bool shop = state == 1;

        if (state != _prevState)
        {
            // 状態が変わった瞬間だけカーソル／ライト／陳列を切り替える（毎フレームの強制はしない）
            if (shop) EnterShop();
            else if (_inShop) LeaveShop();
            _prevState = state;
        }

        // ライトの初期消灯のみ、解決でき次第 1 度だけ適用（以降は遷移時に切替）
        EnsureLightInitialized();

        if (_inShop) UpdateShopSelection();
    }

    /// <summary>
    /// カーソルを操作する。State==1 に入った／出た「遷移時のみ」呼ぶこと。
    /// 毎フレーム呼ぶと他シーンのカーソル表示を奪うので絶対にしない。
    /// </summary>
    private void SetCursor(bool show)
    {
        if (!manageCursor) return;
        Cursor.lockState = show ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = show;
    }

    /// <summary>「Spot Light」の点灯/消灯。解決済みかつ状態が異なるときだけ切り替える。</summary>
    private void SetSpotLight(bool on)
    {
        ResolveSpotLight();
        if (_spotLightGo == null) return;
        if (_spotLightGo.activeSelf != on) _spotLightGo.SetActive(on);
    }

    /// <summary>別シーン未ロードで Start 時に解決できないライトを、解決でき次第 1 度だけ現状態へ合わせる。</summary>
    private void EnsureLightInitialized()
    {
        if (_lightInitialized) return;
        ResolveSpotLight();
        if (_spotLightGo == null) return;
        if (_spotLightGo.activeSelf != _inShop) _spotLightGo.SetActive(_inShop);
        _lightInitialized = true;
    }

    private void OnDisable()
    {
        // 自分がショップ表示中だった場合のみ後始末（カーソルロック／ライト消灯）。
        // ショップ外なら他シーンが管理しているカーソルに触れない。
        if (_inShop) LeaveShop();
        _prevState = -1;
    }

    // ---- 状態遷移 -------------------------------------------------------

    private void EnterShop()
    {
        _inShop = true;
        SpawnShopBalls();
        SetCursor(true);    // 遷移時のみ: カーソル表示＋ロック解除
        SetSpotLight(true); // 遷移時のみ: 点灯
        _selectedIndex = -1;
        session.SetSelectedBallPrefab(null);
    }

    private void LeaveShop()
    {
        _inShop = false;
        ClearOutlines();
        DespawnShopBalls();
        SetCursor(false);    // 遷移時のみ: カーソル非表示＋中央ロック
        SetSpotLight(false); // 遷移時のみ: 消灯
        _selectedIndex = -1;
        session.SetSelectedBallPrefab(null);
    }

    // ---- 陳列ボールの生成／破棄 ----------------------------------------

    private void SpawnShopBalls()
    {
        DespawnShopBalls();
        if (shopBalls == null) return;

        for (int i = 0; i < shopBalls.Length; i++)
        {
            var entry = shopBalls[i];
            if (entry == null || entry.prefab == null || entry.positionAnchor == null)
            {
                Debug.LogWarning($"[PinballShopView] shopBalls[{i}] の prefab か positionAnchor が未設定です。", this);
                continue;
            }

            GameObject go = Instantiate(entry.prefab);
            go.name = entry.prefab.name + " (Shop)";
            go.transform.SetParent(entry.positionAnchor, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;

            // 陳列なので物理を止め、ピンボール制御も無効化（落下・転がりを防ぐ）
            foreach (var rb in go.GetComponentsInChildren<Rigidbody>(true))
            {
                rb.isKinematic = true;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            foreach (var ctrl in go.GetComponentsInChildren<PinballBallController>(true))
            {
                ctrl.enabled = false;
            }

            // Raycast 用 Collider を確保（無ければ Renderer の合成バウンズから BoxCollider を付与）
            var colliders = go.GetComponentsInChildren<Collider>(true);
            if (colliders == null || colliders.Length == 0)
            {
                colliders = EnsureBoxCollider(go);
            }

            // 輪郭対象 Renderer を収集
            var rlist = new List<Renderer>();
            go.GetComponentsInChildren<Renderer>(true, rlist);
            var filtered = new List<Renderer>(OutlineHighlightUtil.FilterRenderers(rlist));

            _instances.Add(go);
            _instanceColliders.Add(colliders);
            _instanceRenderers.Add(filtered);
            // 発射用 prefab（未設定なら陳列用 prefab にフォールバック）
            _launchPrefabs.Add(entry.ballPrefab != null ? entry.ballPrefab : entry.prefab);
        }
    }

    private void DespawnShopBalls()
    {
        ClearOutlines();
        for (int i = 0; i < _instances.Count; i++)
        {
            if (_instances[i] != null) Destroy(_instances[i]);
        }
        _instances.Clear();
        _instanceRenderers.Clear();
        _instanceColliders.Clear();
        _launchPrefabs.Clear();
    }

    private Collider[] EnsureBoxCollider(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return go.GetComponentsInChildren<Collider>(true);

        // ワールド空間でバウンズを合成 → ルートのローカル空間へ変換して BoxCollider に設定
        Bounds world = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) world.Encapsulate(renderers[i].bounds);

        var box = go.AddComponent<BoxCollider>();
        box.center = go.transform.InverseTransformPoint(world.center);
        Vector3 ls = go.transform.lossyScale;
        box.size = new Vector3(
            world.size.x / Mathf.Max(1e-4f, Mathf.Abs(ls.x)),
            world.size.y / Mathf.Max(1e-4f, Mathf.Abs(ls.y)),
            world.size.z / Mathf.Max(1e-4f, Mathf.Abs(ls.z)));
        return new Collider[] { box };
    }

    // ---- 選択・ハイライト ----------------------------------------------

    private void UpdateShopSelection()
    {
        Camera cam = lookCamera != null ? lookCamera : Camera.main;
        if (cam == null || Mouse.current == null) return;

        Vector2 mousePos = Mouse.current.position.ReadValue();
        Ray ray = cam.ScreenPointToRay(mousePos);

        // マウス下の最近接ボールを探す
        int hovered = -1;
        float bestDist = maxDistance;
        for (int i = 0; i < _instances.Count; i++)
        {
            if (RaycastInstance(_instanceColliders[i], ray, bestDist, out float dist))
            {
                bestDist = dist;
                hovered = i;
            }
        }

        // 左クリックでホバー中のボールを選択
        if (hovered >= 0 && Mouse.current.leftButton.wasPressedThisFrame)
        {
            _selectedIndex = hovered;
            session.SetSelectedBallPrefab(_launchPrefabs[hovered]);
        }

        // 各ボールの輪郭を更新（選択=赤 常時 / ホバー=hoverColor / それ以外=非表示）
        for (int i = 0; i < _instances.Count; i++)
        {
            if (i == _selectedIndex)
                OutlineHighlightUtil.SetActive(_instanceRenderers[i], true, selectedColor, outlineWidth, _mpb);
            else if (i == hovered)
                OutlineHighlightUtil.SetActive(_instanceRenderers[i], true, hoverColor, outlineWidth, _mpb);
            else
                OutlineHighlightUtil.SetActive(_instanceRenderers[i], false, hoverColor, outlineWidth, _mpb);
        }
    }

    private static bool RaycastInstance(Collider[] colliders, Ray ray, float maxDist, out float dist)
    {
        dist = maxDist;
        bool any = false;
        for (int i = 0; i < colliders.Length; i++)
        {
            var c = colliders[i];
            if (c == null || !c.enabled) continue;
            if (c.Raycast(ray, out RaycastHit h, dist))
            {
                dist = h.distance;
                any = true;
            }
        }
        return any;
    }

    private void ClearOutlines()
    {
        for (int i = 0; i < _instanceRenderers.Count; i++)
        {
            OutlineHighlightUtil.SetActive(_instanceRenderers[i], false, hoverColor, outlineWidth, _mpb);
        }
    }

    // ---- スポットライト解決（別シーン対応） ----------------------------

    private void ResolveSpotLight()
    {
        if (_spotLightGo != null) return;

        if (spotLightObject != null)
        {
            _spotLightGo = spotLightObject;
            return;
        }

        // 別シーン（Scene_Environment）の非アクティブも含めて名前一致で検索
        var lights = FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < lights.Length; i++)
        {
            if (lights[i] != null && lights[i].gameObject.name == spotLightName)
            {
                _spotLightGo = lights[i].gameObject;
                return;
            }
        }
        // Light 以外の親に付いている場合に備え、名前で GameObject も探す（アクティブのみ）
        var go = GameObject.Find(spotLightName);
        if (go != null) _spotLightGo = go;
    }
}
