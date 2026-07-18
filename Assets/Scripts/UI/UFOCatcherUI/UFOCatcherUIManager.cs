using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

[System.Serializable]
public struct PriceCoinSpawnData
{
    [Tooltip("コインの画像 (未設定ならPrefabのデフォルトを使用)")]
    public Sprite coinSprite;
    [Tooltip("コインのテキスト (値段など)")]
    public string coinText;
}

[System.Serializable]
public struct ListItemSpawnData
{
    [Tooltip("ListItemの画像 (未設定ならPrefabのデフォルトを使用)")]
    public Sprite itemSprite;
    [Tooltip("Row 1 のテキスト")]
    public string row1Text;
    [Tooltip("Row 2 のテキスト")]
    public string row2Text;
    [Tooltip("Row 3 のテキスト")]
    public string row3Text;
}

/// <summary>
/// UFOキャッチャープレイ中の専用UIの生成・破棄、およびデータの適用を管理するクラス。
/// new_ufocatcher オブジェクトにアタッチして使用します。
/// </summary>
public class UFOCatcherUIManager : MonoBehaviour
{
    [Header("Prefabs")]
    [Tooltip("price_coin プレハブ")]
    [SerializeField] private GameObject _priceCoinPrefab;

    [Tooltip("ListItem プレハブ")]
    [SerializeField] private GameObject _listItemPrefab;

    [Header("Spawn Settings")]
    [Tooltip("自動生成するCanvasのターゲットDisplay (Display 4なら4を指定)")]
    [SerializeField] private int _targetDisplay = 4;

    [Tooltip("Canvasを生成する親オブジェクト (price_tableなど)。未設定の場合は子オブジェクトから'price_table'を自動検索し、無ければ自身(new_ufocatcher)の直下に生成されます")]
    [SerializeField] private Transform _priceTable;

    [Tooltip("スケールが大きくなった際に、右方向に寄せるためのオフセット倍率。1.0倍を超えた分に対してこの値が掛けられて右にずれます")]
    [SerializeField] private float _xOffsetPerScale = 30f;

    [Tooltip("スケールが大きくなった際に、下方向に下げるためのオフセット倍率。1.0倍を超えた分に対してこの値が掛けられて下にずれます")]
    [SerializeField] private float _yOffsetPerScale = 50f;

    [Header("UI Spawn Datas")]
    [Tooltip("price_coin UIの生成設定リスト。ここの数だけprice_coinが生成されます")]
    [SerializeField] private List<PriceCoinSpawnData> _priceCoinDatas = new List<PriceCoinSpawnData>();

    [Tooltip("ListItem UIの生成設定リスト。ここの数だけListItemが生成されます")]
    [SerializeField] private List<ListItemSpawnData> _listItemDatas = new List<ListItemSpawnData>();

    private Canvas _canvas;
    private List<GameObject> _generatedUIObjects = new List<GameObject>();

    private void Start()
    {
        // UFOカメラコントローラーのプレイ開始・終了イベントを購読
        UFOCameraController.OnUfoModeChanged += HandleUfoModeChanged;
    }

    private void OnDestroy()
    {
        UFOCameraController.OnUfoModeChanged -= HandleUfoModeChanged;
        ClearGeneratedUI();
    }

    private void HandleUfoModeChanged(bool isPlayingUfo)
    {
        if (isPlayingUfo)
        {
            GenerateUI();
        }
        else
        {
            ClearGeneratedUI();
        }
    }

    /// <summary>
    /// 設定されたデータに基づいてUIオブジェクトを自動生成します
    /// </summary>
    private void GenerateUI()
    {
        // 既存の生成物を安全にクリア
        ClearGeneratedUI();

        // 親とするTransformの決定
        Transform parentTransform = _priceTable;
        if (parentTransform == null)
        {
            // 子オブジェクトから "price_table" という名前のオブジェクトを検索
            parentTransform = transform.Find("price_table");
            if (parentTransform == null)
            {
                // 非アクティブなものも含めて再帰的に検索
                foreach (Transform child in GetComponentsInChildren<Transform>(true))
                {
                    if (child.name == "price_table")
                    {
                        parentTransform = child;
                        break;
                    }
                }
            }
        }

        // それでも見つからなければ自身を親にする
        if (parentTransform == null)
        {
            parentTransform = transform;
        }

        // 指定した親（またはその子）にCanvasが存在するか確認
        _canvas = parentTransform.GetComponentInChildren<Canvas>(true);
        if (_canvas == null)
        {
            // なければCanvasを自動生成
            GameObject canvasObj = new GameObject("UFOCatcherCanvas");
            canvasObj.transform.SetParent(parentTransform, false);

            _canvas = canvasObj.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // UnityのDisplayインデックスは0始まりのため、4を指定されたら3（Display 4）を設定
            _canvas.targetDisplay = Mathf.Max(0, _targetDisplay - 1);

            // UIに必要なコンポーネントを追加
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();
            
            Debug.Log($"[UFOCatcherUIManager] Canvas created automatically under '{parentTransform.name}'. Target Display: Display {_targetDisplay}");
        }

        // Canvasをアクティブにする
        _canvas.gameObject.SetActive(true);
        Transform canvasTransform = _canvas.transform;

        int spawnCount = 0;

        int totalCount = 0;
        if (_priceCoinDatas != null) totalCount += _priceCoinDatas.Count;
        if (_listItemDatas != null) totalCount += _listItemDatas.Count;

        // 生成総数に応じてスケールを決定する (10個以上で最小 of 1.0f、6個以下で最大の1.8f)
        float scale = 1.0f;
        if (totalCount <= 6)
        {
            scale = 1.8f;
        }
        else if (totalCount == 7)
        {
            scale = 1.5f;
        }
        else if (totalCount == 8)
        {
            scale = 1.2f;
        }
        else if (totalCount == 9)
        {
            scale = 1.1f;
        }
        else
        {
            scale = 1.0f;
        }

        // オフセット計算用のスケール基準 (X/Yのスライド量の減少は以前の4個以下で1.8fからの下がり方を維持)
        float offsetScale = 1.0f;
        if (totalCount <= 4)
        {
            offsetScale = 1.8f;
        }
        else if (totalCount == 5)
        {
            offsetScale = 1.6f;
        }
        else if (totalCount == 6)
        {
            offsetScale = 1.4f;
        }
        else if (totalCount == 7)
        {
            offsetScale = 1.2f;
        }
        else if (totalCount == 8)
        {
            offsetScale = 1.1f;
        }
        else
        {
            offsetScale = 1.0f;
        }

        // 1. price_coin の生成と配置
        if (_priceCoinPrefab != null && _priceCoinDatas != null)
        {
            foreach (var coinData in _priceCoinDatas)
            {
                GameObject coinObj = Instantiate(_priceCoinPrefab, canvasTransform);
                
                // スケールを生成数に合わせて調整
                Vector3 origScale = coinObj.transform.localScale;
                coinObj.transform.localScale = origScale * scale;

                RectTransform coinRect = coinObj.GetComponent<RectTransform>();
                if (coinRect != null)
                {
                    // 1つ生成するごとにPosYを-25ずつずらす (オフセットスケールに応じて間隔も拡大)
                    Vector3 localPos = coinRect.localPosition;
                    localPos.y -= (25f * offsetScale) * spawnCount;
                    // オフセット計算用スケールが大きくなった分だけ右（X正方向）にずらす
                    localPos.x += _xOffsetPerScale * (offsetScale - 1.0f);
                    // オフセット計算用スケールが大きくなった分だけ下（Y負方向）にずらす
                    localPos.y -= _yOffsetPerScale * (offsetScale - 1.0f);
                    coinRect.localPosition = localPos;
                }

                // データの適用
                PriceCoinUI coinUI = coinObj.GetComponent<PriceCoinUI>();
                if (coinUI != null)
                {
                    if (coinData.coinSprite != null)
                    {
                        coinUI.SetImage(coinData.coinSprite);
                    }
                    coinUI.SetText(coinData.coinText);
                }
                else
                {
                    Debug.LogWarning("[UFOCatcherUIManager] price_coin プレハブに PriceCoinUI コンポーネントがアタッチされていません。");
                }

                _generatedUIObjects.Add(coinObj);
                spawnCount++;
            }
        }

        // 2. ListItem の生成と配置
        if (_listItemPrefab != null && _listItemDatas != null)
        {
            foreach (var itemData in _listItemDatas)
            {
                GameObject itemObj = Instantiate(_listItemPrefab, canvasTransform);

                // スケールを生成数に合わせて調整
                Vector3 origScale = itemObj.transform.localScale;
                itemObj.transform.localScale = origScale * scale;

                RectTransform itemRect = itemObj.GetComponent<RectTransform>();
                if (itemRect != null)
                {
                    // 1つ生成するごとにPosYを-25ずつずらす (オフセットスケールに応じて間隔も拡大)
                    Vector3 localPos = itemRect.localPosition;
                    localPos.y -= (25f * offsetScale) * spawnCount;
                    // オフセット計算用スケールが大きくなった分だけ右（X正方向）にずらす
                    localPos.x += _xOffsetPerScale * (offsetScale - 1.0f);
                    // オフセット計算用スケールが大きくなった分だけ下（Y負方向）にずらす
                    localPos.y -= _yOffsetPerScale * (offsetScale - 1.0f);
                    itemRect.localPosition = localPos;
                }

                // データの適用
                ListItemUI itemUI = itemObj.GetComponent<ListItemUI>();
                if (itemUI != null)
                {
                    if (itemData.itemSprite != null)
                    {
                        itemUI.SetImage(itemData.itemSprite);
                    }
                    itemUI.SetTexts(itemData.row1Text, itemData.row2Text, itemData.row3Text);
                }
                else
                {
                    Debug.LogWarning("[UFOCatcherUIManager] ListItem プレハブに ListItemUI コンポーネントがアタッチされていません。");
                }

                _generatedUIObjects.Add(itemObj);
                spawnCount++;
            }
        }

        Debug.Log($"[UFOCatcherUIManager] UI items generated. Total spawned: {spawnCount}");
    }

    /// <summary>
    /// 生成したUIと自動生成したCanvasをクリア（破棄・非表示）します
    /// </summary>
    private void ClearGeneratedUI()
    {
        // 生成したオブジェクトを破棄
        foreach (var obj in _generatedUIObjects)
        {
            if (obj != null)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    DestroyImmediate(obj);
                    continue;
                }
#endif
                Destroy(obj);
            }
        }
        _generatedUIObjects.Clear();

        // Canvasの処理
        if (_canvas != null)
        {
            // 自動生成したCanvas（名前がUFOCatcherCanvas）はオブジェクトごと破棄
            if (_canvas.name == "UFOCatcherCanvas")
            {
#if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    DestroyImmediate(_canvas.gameObject);
                    _canvas = null;
                    return;
                }
#endif
                Destroy(_canvas.gameObject);
                _canvas = null;
                Debug.Log("[UFOCatcherUIManager] Automatically generated Canvas destroyed.");
            }
            else
            {
                // 手動配置されたCanvasは非表示にするだけ
                _canvas.gameObject.SetActive(false);
            }
        }
    }
}
