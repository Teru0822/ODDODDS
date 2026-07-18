using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

[System.Serializable]
public struct PriceCoinSpawnData
{
    [Tooltip("price_coin UIを生成するかどうか")]
    public bool isEnabled;
    [Tooltip("コインの画像 (未設定ならPrefabのデフォルトを使用)")]
    public Sprite coinSprite;
    [Tooltip("コインのテキスト")]
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

    [Header("UI Spawn Datas")]
    [Tooltip("price_coin UIの生成設定")]
    [SerializeField] private PriceCoinSpawnData _priceCoinData = new PriceCoinSpawnData { isEnabled = true };

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

        // 自身（または子オブジェクト）にCanvasが存在するか確認
        _canvas = GetComponentInChildren<Canvas>(true);
        if (_canvas == null)
        {
            // なければCanvasを自動生成
            GameObject canvasObj = new GameObject("UFOCatcherCanvas");
            canvasObj.transform.SetParent(transform, false);

            _canvas = canvasObj.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // UnityのDisplayインデックスは0始まりのため、4を指定されたら3（Display 4）を設定
            _canvas.targetDisplay = Mathf.Max(0, _targetDisplay - 1);

            // UIに必要なコンポーネントを追加
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();
            
            Debug.Log($"[UFOCatcherUIManager] Canvas created automatically. Target Display: Display {_targetDisplay}");
        }

        // Canvasをアクティブにする
        _canvas.gameObject.SetActive(true);
        Transform canvasTransform = _canvas.transform;

        int spawnCount = 0;

        // 1. price_coin の生成と配置
        if (_priceCoinData.isEnabled && _priceCoinPrefab != null)
        {
            GameObject coinObj = Instantiate(_priceCoinPrefab, canvasTransform);
            RectTransform coinRect = coinObj.GetComponent<RectTransform>();
            if (coinRect != null)
            {
                // 1つ生成するごとにPosYを-25ずつずらす
                Vector3 localPos = coinRect.localPosition;
                localPos.y -= 25f * spawnCount;
                coinRect.localPosition = localPos;
            }

            // データの適用
            PriceCoinUI coinUI = coinObj.GetComponent<PriceCoinUI>();
            if (coinUI != null)
            {
                if (_priceCoinData.coinSprite != null)
                {
                    coinUI.SetImage(_priceCoinData.coinSprite);
                }
                coinUI.SetText(_priceCoinData.coinText);
            }
            else
            {
                Debug.LogWarning("[UFOCatcherUIManager] price_coin プレハブに PriceCoinUI コンポーネントがアタッチされていません。");
            }

            _generatedUIObjects.Add(coinObj);
            spawnCount++;
        }

        // 2. ListItem の生成と配置
        if (_listItemPrefab != null && _listItemDatas != null)
        {
            foreach (var itemData in _listItemDatas)
            {
                GameObject itemObj = Instantiate(_listItemPrefab, canvasTransform);
                RectTransform itemRect = itemObj.GetComponent<RectTransform>();
                if (itemRect != null)
                {
                    // 1つ生成するごとにPosYを-25ずつずらす
                    Vector3 localPos = itemRect.localPosition;
                    localPos.y -= 25f * spawnCount;
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
