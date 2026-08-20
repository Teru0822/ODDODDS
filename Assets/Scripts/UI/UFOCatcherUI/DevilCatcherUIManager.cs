using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>ATMの買取価格を連動表示する対象のコイン種別（Noneなら連動なし、coinTextをそのまま使う）</summary>
public enum CoinPriceLinkType
{
    None,
    Gold,
    Silver,
    Bronze,
}

[System.Serializable]
public struct PriceCoinSpawnData
{
    [Tooltip("コインの画像 (未設定ならPrefabのデフォルトを使用)")]
    public Sprite coinSprite;
    [Tooltip("コインのテキスト (値段など)。coinPriceLinkTypeがNone以外の場合はATMの現在買取価格で上書きされます")]
    public string coinText;
    [Tooltip("None以外を指定すると、coinTextの代わりにATMControllerの現在買取価格を「x{価格}DC」形式で表示します")]
    public CoinPriceLinkType coinPriceLinkType;
    [Tooltip("ON: ルーレット機能（Devil_Eye）が解放されるまで、この項目を price_table に表示しません")]
    public bool requiresRouletteUnlock;

    [Tooltip("ON: coinSpriteの代わりに、ブラックダイヤの磨き段階(0〜3)に応じてdiamondStageSpritesから選んだ画像を使う")]
    public bool useDiamondStageSprite;
    [Tooltip("磨き段階0〜3に対応する画像。useDiamondStageSpriteがONの項目でのみ使用")]
    public Sprite[] diamondStageSprites;
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
/// Devilキャッチャープレイ中の専用UIの生成・破棄、およびデータの適用を管理するクラス。
/// new_devilcatcher オブジェクトにアタッチして使用します。
/// </summary>
public class DevilCatcherUIManager : MonoBehaviour
{
    [Header("Prefabs")]
    [Tooltip("price_coin プレハブ")]
    [SerializeField] private GameObject _priceCoinPrefab;

    [Tooltip("ListItem プレハブ")]
    [SerializeField] private GameObject _listItemPrefab;

    [Header("Spawn Settings")]
    [Tooltip("自動生成するCanvasのターゲットDisplay (Display 4なら4を指定)")]
    [SerializeField] private int _targetDisplay = 4;

    [Tooltip("Canvasを生成する親オブジェクト (price_tableなど)。未設定の場合は子オブジェクトから'price_table'を自動検索し、無ければ自身(new_devilcatcher)の直下に生成されます")]
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

    [Header("チュートリアル設定")]
    [Tooltip("ON: 練習用Devilキャッチャー（Practice_Cranegame）側のDevilCatcherUIManagerであることを示す。\n" +
             "実機・練習機の両方にDevilCatcherUIManagerが存在するため、OFF（実機側）はUFOCameraControllerの" +
             "static イベントを購読して自動でUIを出し入れするが、ON（練習機側）は購読しない。\n" +
             "購読してしまうと、実機のセッション開始/終了のたびに練習機側のUIまで一緒に出入りしてしまう" +
             "（TutorialCraneControllerがApplySessionActiveUIState()を直接呼ぶので、練習機側はそもそも購読不要）。\n" +
             "練習機側のDevilCatcherUIManagerでは必ずこれをONにしてください。")]
    [SerializeField] private bool isPracticeInstance = false;

    private Canvas _canvas;
    private List<GameObject> _generatedUIObjects = new List<GameObject>();

    private void Start()
    {
        // シーン上でCanvasが手動でアクティブのまま残っていると、プレイ開始/終了イベントが一度も
        // 発火する前（起動直後や、プレイ経験が一度もない状態）にもprice_tableが見えてしまうため、
        // 起動時に必ず一度非表示にしておく
        ClearGeneratedUI();

        // 練習機側はTutorialCraneControllerがApplySessionActiveUIState()を直接呼ぶため、
        // 実機のstaticイベントは購読しない（購読すると実機のセッション変化にも反応してしまう）
        if (isPracticeInstance) return;

        // Play_Canvas2 で Play を押して実際にプレイセッションが始まった/終わったタイミングを購読する
        // （tutorial_canvas / Play_canvas / Play2_canvas を閲覧しているだけの間は表示しない）
        UFOCameraController.OnPlaySessionActiveChanged += HandlePlaySessionActiveChanged;
    }

    private void Update()
    {
        // イベント駆動の表示切り替えだけに頼らず、実際にプレイ中かどうかを毎フレーム確認して
        // 食い違いが起きないようにする（セッション終了イベントが何らかの理由で発火しなかった場合の保険）。
        // IsPlaySessionActive（引き出し演出が終わるまでtrueのまま）ではなく、クレーン操作自体が
        // 終わった瞬間にfalseになるIsPlayingUfoを見る。そうしないと引き出し演出中もprice_tableが
        // 消えず、拡大されたUnwashCoin表示と重なって見えてしまう
        if (_canvas == null || !_canvas.gameObject.activeSelf) return;

        bool isActuallyPlaying = isPracticeInstance
            ? TutorialCraneController.IsAnyTutorialPlaying
            : UFOCameraController.IsPlayingUfo;

        if (!isActuallyPlaying)
        {
            ClearGeneratedUI();
        }
    }

    private void OnDestroy()
    {
        if (!isPracticeInstance)
        {
            UFOCameraController.OnPlaySessionActiveChanged -= HandlePlaySessionActiveChanged;
        }
        ClearGeneratedUI();
    }

    private void HandlePlaySessionActiveChanged(bool isSessionActive)
    {
        ApplySessionActiveUIState(isSessionActive);
    }

    /// <summary>
    /// 実機のセッション開始/終了時と同じ price_table UI切り替えを行う。
    /// TutorialCraneController から、実機の UFOCameraController.IsPlaySessionActive には一切触れずに
    /// 同じ見た目のUI切り替え（Play2_tutorial の Play を押した時など）だけを行うために呼ばれる。
    /// </summary>
    /// <summary>
    /// 生成済みのUIの表示だけを一時的に切り替える。ApplySessionActiveUIStateと違い、UIの生成・破棄は
    /// 行わないため、fullDarkBackgroundステップの間だけ一時的に隠す、といった用途に使う
    /// </summary>
    public void SetUIVisibleExternal(bool visible)
    {
        if (_canvas != null)
        {
            _canvas.gameObject.SetActive(visible);
        }
    }

    public void ApplySessionActiveUIState(bool isSessionActive)
    {
        if (isSessionActive)
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
            GameObject canvasObj = new GameObject("DevilCatcherCanvas");
            canvasObj.transform.SetParent(parentTransform, false);

            _canvas = canvasObj.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // UnityのDisplayインデックスは0始まりのため、4を指定されたら3（Display 4）を設定
            _canvas.targetDisplay = Mathf.Max(0, _targetDisplay - 1);

            // UIに必要なコンポーネントを追加
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();
            
            Debug.Log($"[DevilCatcherUIManager] Canvas created automatically under '{parentTransform.name}'. Target Display: Display {_targetDisplay}");
        }

        // Canvasをアクティブにする
        _canvas.gameObject.SetActive(true);
        Transform canvasTransform = _canvas.transform;

        int spawnCount = 0;

        // ルーレット未解放の間は requiresRouletteUnlock な項目を除外する（レイアウトのスケール計算にも反映するため先に絞り込む）
        bool rouletteUnlocked = RouletteController.Instance != null && RouletteController.Instance.IsRouletteUnlocked;
        List<PriceCoinSpawnData> visiblePriceCoinDatas = new List<PriceCoinSpawnData>();
        if (_priceCoinDatas != null)
        {
            foreach (var coinData in _priceCoinDatas)
            {
                if (coinData.requiresRouletteUnlock && !rouletteUnlocked) continue;
                visiblePriceCoinDatas.Add(coinData);
            }
        }

        int totalCount = 0;
        totalCount += visiblePriceCoinDatas.Count;
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

        // 1. price_coin の生成と配置（ルーレット未解放の項目は除外済みの visiblePriceCoinDatas を使う）
        if (_priceCoinPrefab != null)
        {
            foreach (var coinData in visiblePriceCoinDatas)
            {
                GameObject coinObj = Instantiate(_priceCoinPrefab, canvasTransform);
                
                // スケールを生成数に合わせて調整
                Vector3 origScale = coinObj.transform.localScale;
                coinObj.transform.localScale = origScale * scale;

                RectTransform coinRect = coinObj.GetComponent<RectTransform>();
                if (coinRect != null)
                {
                    // 1つ生成するごとにPosYを-25ずつずらす (スケールに応じて間隔も拡大)
                    Vector3 localPos = coinRect.localPosition;
                    localPos.y -= (25f * scale) * spawnCount;
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
                    Sprite spriteToUse = coinData.coinSprite;

                    // ブラックダイヤ用の項目は、磨き段階に応じて画像を差し替える
                    if (coinData.useDiamondStageSprite && coinData.diamondStageSprites != null && coinData.diamondStageSprites.Length > 0)
                    {
                        var roguelikeManager = FindFirstObjectByType<RoguelikeManager>();
                        if (roguelikeManager != null)
                        {
                            int stage = Mathf.Clamp(roguelikeManager.GetDiamondPolishStage(), 0, coinData.diamondStageSprites.Length - 1);
                            if (coinData.diamondStageSprites[stage] != null)
                                spriteToUse = coinData.diamondStageSprites[stage];
                        }
                    }

                    if (spriteToUse != null)
                    {
                        coinUI.SetImage(spriteToUse);
                    }

                    // 金貨・銀貨・銅貨はATMの現在買取価格（毎回変動する）を「xNNNDC」形式で表示する。
                    // 対象外（None）の項目はcoinTextをそのまま使う
                    string textToUse = coinData.coinText;
                    if (coinData.coinPriceLinkType != CoinPriceLinkType.None && App.ATM.ATMController.Instance != null)
                    {
                        float price = 0f;
                        if (coinData.coinPriceLinkType == CoinPriceLinkType.Gold)
                            price = App.ATM.ATMController.Instance.GoldPrice;
                        else if (coinData.coinPriceLinkType == CoinPriceLinkType.Silver)
                            price = App.ATM.ATMController.Instance.SilverPrice;
                        else if (coinData.coinPriceLinkType == CoinPriceLinkType.Bronze)
                            price = App.ATM.ATMController.Instance.BronzePrice;
                        textToUse = $"x{price:N0}DC";
                    }
                    coinUI.SetText(textToUse);
                }
                else
                {
                    Debug.LogWarning("[DevilCatcherUIManager] price_coin プレハブに PriceCoinUI コンポーネントがアタッチされていません。");
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
                    // 1つ生成するごとにPosYを-25ずつずらす (スケールに応じて間隔も拡大)
                    Vector3 localPos = itemRect.localPosition;
                    localPos.y -= (25f * scale) * spawnCount;
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
                    Debug.LogWarning("[DevilCatcherUIManager] ListItem プレハブに ListItemUI コンポーネントがアタッチされていません。");
                }

                _generatedUIObjects.Add(itemObj);
                spawnCount++;
            }
        }

        Debug.Log($"[DevilCatcherUIManager] UI items generated. Total spawned: {spawnCount}");
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
            // 自動生成したCanvas（名前がDevilCatcherCanvas）はオブジェクトごと破棄
            if (_canvas.name == "DevilCatcherCanvas")
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
                Debug.Log("[DevilCatcherUIManager] Automatically generated Canvas destroyed.");
            }
            else
            {
                // 手動配置されたCanvasは非表示にするだけ
                _canvas.gameObject.SetActive(false);
            }
        }
    }
}
