using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UFOキャッチャーで獲得したアイテムを、画面左下などで3D回転表示するポップアップUI。
/// 塊魂の「巻き込んだアイテム」演出のように、取得した順に表示を切り替える。
///
/// 使い方:
///   1. displayStage（表示専用カメラだけが映す、他のカメラからは見えない位置）にアイテムのモデルを生成する
///   2. displayCamera がそのステージを撮影し、RenderTexture 経由で displayImage (RawImage) に映す
///   3. UFOItemGoal.HandleItemDrop() 等から ShowPickedItem() / ShowItemPrefabDirect() を呼ぶと表示が切り替わる
/// </summary>
public class UFOItemPickupDisplay : MonoBehaviour
{
    public static UFOItemPickupDisplay Instance { get; private set; }

    [Header("アイテム定義データベース（拾ったオブジェクトの名前でItemSpawnDataを検索します）")]
    [Tooltip("ItemSpawnData アセットを登録してください。prefab名で一致判定します。")]
    [SerializeField] private List<ItemSpawnData> itemDatabase = new List<ItemSpawnData>();

    [Header("表示先")]
    [Tooltip("3DモデルをRenderTexture経由で映すRawImage。表示/非表示はこのコンポーネント単体で切り替えます" +
             "（親のCanvas等を丸ごとSetActiveすると他のUIまで消えてしまうため）")]
    [SerializeField] private RawImage displayImage;
    [Tooltip("モデルを実際に置く、表示専用カメラだけが映すステージのTransform")]
    [SerializeField] private Transform displayStage;

    [Tooltip("アイテム名を表示するテキスト（未設定でも動作します）")]
    [SerializeField] private TextMeshProUGUI itemNameText;

    [Tooltip("生成したモデルを強制的にこのレイヤーへ変更します（表示専用カメラのCulling Maskと合わせてください）。" +
             "子オブジェクトはレイヤーを自動継承しないため、これを設定しないとカメラに映りません。")]
    [SerializeField] private string displayLayerName = "ItemShowcase";

    [Header("サイズ調整")]
    [Tooltip("表示するモデルの最大辺の長さを、この値に自動でスケール調整します（アイテムごとに元の大きさがバラバラでも揃います）")]
    [SerializeField] private float targetDisplaySize = 1f;

    [Header("回転設定")]
    [Tooltip("回転させる軸（例: 縦回転なら(0,1,0)、横向きに回したいなら(1,0,0)など）")]
    [SerializeField] private Vector3 rotationAxis = Vector3.up;
    [SerializeField] private float rotationSpeed = 60f;

    [Header("表示継続時間")]
    [Tooltip("次のアイテムが来なくても表示し続ける秒数。0以下ならずっと表示したまま（次のアイテムが来るまで）")]
    [SerializeField] private float displayHoldSeconds = 0f;

    private GameObject _currentModelInstance;
    private Coroutine _hideCoroutine;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        SetVisible(false);
    }

    private void Update()
    {
        if (_currentModelInstance != null)
        {
            _currentModelInstance.transform.Rotate(rotationAxis, rotationSpeed * Time.deltaTime, Space.World);
        }
    }

    /// <summary>
    /// 拾ったアイテムのGameObject（プレハブのClone）を渡すと、名前で ItemSpawnData を検索して表示を切り替える。
    /// </summary>
    public void ShowPickedItem(GameObject pickedObject)
    {
        if (pickedObject == null) return;

        string itemName = pickedObject.name.Replace("(Clone)", "").Trim();
        ItemSpawnData data = itemDatabase.Find(d => d != null && d.prefab != null && d.prefab.name == itemName);

        if (data == null || data.prefab == null)
        {
            Debug.LogWarning($"[UFOItemPickupDisplay] '{itemName}' に対応するItemSpawnDataがitemDatabaseに見つからないため、ポップアップ表示をスキップします。");
            return;
        }

        ShowModel(data.prefab, string.IsNullOrEmpty(data.itemName) ? data.prefab.name : data.itemName);
    }

    /// <summary>
    /// ItemSpawnData を経由せず、表示するプレハブを直接指定する場合に使う
    /// （例: candy/pinballの特別演出など、ItemSpawnData化していないアイテム用）。
    /// displayName を省略した場合はプレハブ名がそのまま表示されます。
    /// </summary>
    public void ShowItemPrefabDirect(GameObject prefab, string displayName = null)
    {
        ShowModel(prefab, string.IsNullOrEmpty(displayName) ? (prefab != null ? prefab.name : "") : displayName);
    }

    private void ShowModel(GameObject prefab, string displayName)
    {
        if (prefab == null) return;

        if (displayStage == null)
        {
            Debug.LogWarning("[UFOItemPickupDisplay] displayStage がインスペクターに設定されていません。");
            return;
        }

        if (_hideCoroutine != null)
        {
            StopCoroutine(_hideCoroutine);
            _hideCoroutine = null;
        }

        if (_currentModelInstance != null)
        {
            Destroy(_currentModelInstance);
        }

        _currentModelInstance = Instantiate(prefab, displayStage);
        _currentModelInstance.transform.localPosition = Vector3.zero;
        _currentModelInstance.transform.localRotation = Quaternion.identity;

        // 子オブジェクトはレイヤーを自動継承しないため、表示専用カメラに映るよう強制的に変更する
        int layer = LayerMask.NameToLayer(displayLayerName);
        if (layer < 0)
        {
            Debug.LogWarning($"[UFOItemPickupDisplay] レイヤー '{displayLayerName}' が存在しません。Tags and Layers で作成してください。");
        }
        else
        {
            SetLayerRecursively(_currentModelInstance.transform, layer);
        }

        // 表示専用ステージなので、物理演算・当たり判定は不要
        foreach (var col in _currentModelInstance.GetComponentsInChildren<Collider>())
        {
            col.enabled = false;
        }
        foreach (var rb in _currentModelInstance.GetComponentsInChildren<Rigidbody>())
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        // 影を落とす・受け取る設定を両方オフにして、暗い部分（影）が出ないようにする
        var renderers = _currentModelInstance.GetComponentsInChildren<Renderer>();
        foreach (var renderer in renderers)
        {
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        // アイテムごとに元の大きさがバラバラでも、最大辺が targetDisplaySize になるよう自動調整する
        if (renderers.Length > 0)
        {
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            float maxDimension = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            if (maxDimension > 0.0001f)
            {
                float scale = targetDisplaySize / maxDimension;
                _currentModelInstance.transform.localScale *= scale;
            }

            // Pivot（原点）ではなく見た目の中心（Bounds）がステージ位置に来るよう補正する。
            // Pivotが見た目の中心からズレているモデル（例: 時計）だと、Pivot基準のままでは
            // 表示が上下左右にズレて見えるため。
            Bounds scaledBounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                scaledBounds.Encapsulate(renderers[i].bounds);
            }
            Vector3 centerOffset = displayStage.position - scaledBounds.center;
            _currentModelInstance.transform.position += centerOffset;
        }

        if (itemNameText != null) itemNameText.text = displayName;

        SetVisible(true);

        if (displayHoldSeconds > 0f)
        {
            _hideCoroutine = StartCoroutine(HideAfterDelay());
        }
    }

    private IEnumerator HideAfterDelay()
    {
        yield return new WaitForSeconds(displayHoldSeconds);

        SetVisible(false);
        if (_currentModelInstance != null)
        {
            Destroy(_currentModelInstance);
            _currentModelInstance = null;
        }
        _hideCoroutine = null;
    }

    /// <summary>
    /// RawImage・アイテム名テキストだけを個別にON/OFFする（共有Canvasを巻き込まないため）。
    /// </summary>
    private void SetVisible(bool visible)
    {
        if (displayImage != null) displayImage.enabled = visible;
        if (itemNameText != null) itemNameText.enabled = visible;
    }

    private static void SetLayerRecursively(Transform root, int layer)
    {
        root.gameObject.layer = layer;
        foreach (Transform child in root)
        {
            SetLayerRecursively(child, layer);
        }
    }
}
