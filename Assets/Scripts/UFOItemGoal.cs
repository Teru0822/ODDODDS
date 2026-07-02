using UnityEngine;
using TMPro; // 画面の文字（UI）を操作するために追加

/// <summary>
/// UFOキャッチャーの落とし口（透明なTriggerBox）にアタッチするクラス
/// 落とし口の拡張（強化要素）にも対応しやすい設計
/// </summary>
public class UFOItemGoal : MonoBehaviour
{
    [Header("強化要素用（外部から変更可能）")]
    [Tooltip("アイテム獲得時の金額倍率。強化で1.5倍などに変更できる")]
    public float scoreMultiplier = 1.0f;

    [Header("獲得カウント")]
    [Tooltip("獲得した時計の数")]
    public int collectedWatches = 0;

    [Tooltip("獲得した未洗浄メダルの総額")]
    public float unwashedMoney = 0f;

    /// <summary>
    /// ランプの獲得演出が実行中かどうかを示します。
    /// UFOCameraController や PatoLampController から参照されます。
    /// </summary>
    public static bool IsFlashing { get; private set; } = false;

    [Header("画面表示(UI)")]
    [Tooltip("時計の数を表示するUIテキスト")]
    public TextMeshProUGUI watchCountText;

    [Tooltip("未洗浄メダルのお金を表示するUIテキスト")]
    public TextMeshProUGUI unwashedMoneyText;



    [Header("効果音")]
    [Tooltip("再生用のAudioSource。未設定の場合は自動でGetComponentします")]
    [SerializeField] private AudioSource audioSource;

    [Tooltip("コイン獲得時の効果音")]
    [SerializeField] private AudioClip coinGetSound;

    [Tooltip("時計獲得時の効果音（未設定の場合はコインと同じ音が鳴ります）")]
    [SerializeField] private AudioClip watchGetSound;

    [Tooltip("ブラックダイヤモンド獲得時の効果音（未設定の場合はコインと同じ音が鳴ります）")]
    [SerializeField] private AudioClip blackDiamondGetSound;

    [Tooltip("効果音の音量調整 (1.0より大きい値で音量増幅可能)")]
    [Range(0f, 10f)]
    [SerializeField] private float soundVolume = 1.0f;

    [Header("時計効果")]
    [Tooltip("時計を落とし口に入れたときにUFOキャッチャーの残り時間を何秒延長するか")]
    [SerializeField] public float watchTimeExtension = 20f;

    [Header("ランプ連動設定")]
    [Tooltip("時計獲得時に緑色に光らせる対象 of ランプについたタグ名")]
    [SerializeField] private string lampTag = "InsertableItem";

    [Tooltip("ランプが光る時間（秒）")]
    [SerializeField] private float lampGreenDuration = 3.0f;

    [Tooltip("時計獲得時にランプが光る色（HDRカラー）")]
    [ColorUsage(true, true)]
    [SerializeField] private Color lampFlashColor = Color.green * 3.0f;

    [Tooltip("ジャックポット獲得時にランプが光る色（HDRカラー）")]
    [ColorUsage(true, true)]
    [SerializeField] private Color jackpotFlashColor = new Color(1.0f, 0.85f, 0.2f, 1.0f) * 3.0f;

    [Tooltip("ブラックダイヤモンド獲得時にランプが光る色（HDRカラー）")]
    [ColorUsage(true, true)]
    [SerializeField] private Color blackDiamondFlashColor = new Color(0.8f, 0.0f, 1.0f, 1.0f) * 3.0f;

    [Tooltip("ジャックポット獲得時のランプ点滅間隔（秒）")]
    [SerializeField] private float jackpotBlinkInterval = 0.15f;

    [Header("フィーバータイム設定")]
    [Tooltip("フィーバータイム有効時間（秒）")]
    [SerializeField] private float feverDuration = 10f;

    [Tooltip("フィーバータイム用の金貨プレハブ")]
    [SerializeField] private GameObject feverGoldPrefab;
    [Tooltip("フィーバータイム用の銀貨プレハブ")]
    [SerializeField] private GameObject feverSilverPrefab;
    [Tooltip("フィーバータイム用の銅貨プレハブ")]
    [SerializeField] private GameObject feverCopperPrefab;

    [Tooltip("金貨の降る割合（比率）")]
    [Range(0f, 100f)]
    [SerializeField] private float feverGoldRatio = 20f;

    [Tooltip("銀貨の降る割合（比率）")]
    [Range(0f, 100f)]
    [SerializeField] private float feverSilverRatio = 30f;

    [Tooltip("銅貨の降る割合（比率）")]
    [Range(0f, 100f)]
    [SerializeField] private float feverCopperRatio = 50f;

    [Tooltip("フィーバータイム中に降らせるコインの総枚数")]
    [SerializeField] private int feverRainCoinCount = 100;

    [Tooltip("フィーバータイム中に降らせる範囲のスケール")]
    [SerializeField] private Vector2 feverRainAreaScale = new Vector2(0.5f, 0.5f);

    [Tooltip("フィーバータイム中に降らせる範囲 of オフセット")]
    [SerializeField] private Vector2 feverRainAreaOffset = Vector2.zero;

    [Header("ジャックポット設定")]
    [Tooltip("ジャックポット発生時に降らせるオブジェクトのプレハブリスト（空の場合はゴールドコインになります）")]
    public System.Collections.Generic.List<GameObject> jackpotRainPrefabs = new System.Collections.Generic.List<GameObject>();
    [Tooltip("ジャックポット発生時に降らせるコインの枚数")]
    public int jackpotRainCoinCount = 100;
    [Tooltip("ジャックポット発生時に降らせる時間（秒）")]
    public float jackpotRainDuration = 5.0f;
    [Tooltip("ジャックポット発生時に降らせる範囲のスケール（1でエリア0の全域、小さいほど中心部に狭まります）")]
    public Vector2 jackpotRainAreaScale = new Vector2(0.5f, 0.5f);
    [Tooltip("ジャックポット発生時に降らせる範囲のオフセット（エリア0の中心からの位置のズレ）")]
    public Vector2 jackpotRainAreaOffset = Vector2.zero;

    private void Start()
    {
        // AudioSourceの自動取得
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }



        // UnwashedMoneyManagerの初期値との同期、および変更イベントの購読
        if (UnwashedMoneyManager.Instance != null)
        {
            unwashedMoney = UnwashedMoneyManager.Instance.CurrentAmount;
            UpdateUnwashedMoneyText();
            UnwashedMoneyManager.Instance.OnAmountChanged += HandleUnwashedMoneyChanged;
        }
    }

    private Coroutine _lampCoroutine;
    private System.Collections.Generic.Dictionary<Light, Color> _originalColors = new System.Collections.Generic.Dictionary<Light, Color>();
    private System.Collections.Generic.Dictionary<Light, bool> _originalEnabled = new System.Collections.Generic.Dictionary<Light, bool>();
    private System.Collections.Generic.Dictionary<Renderer, Color> _originalEmissionColors = new System.Collections.Generic.Dictionary<Renderer, Color>();

    private void OnDisable()
    {
        ResetLampsToOriginal();
    }

    private void OnDestroy()
    {
        if (UnwashedMoneyManager.Instance != null)
        {
            UnwashedMoneyManager.Instance.OnAmountChanged -= HandleUnwashedMoneyChanged;
        }
        ResetLampsToOriginal();
    }

    private void HandleUnwashedMoneyChanged(float newAmount)
    {
        unwashedMoney = newAmount;
        UpdateUnwashedMoneyText();
    }

    private void UpdateUnwashedMoneyText()
    {
        if (unwashedMoneyText != null)
        {
            unwashedMoneyText.text = $"Unwashed: ¥{Mathf.FloorToInt(unwashedMoney):N0}";
        }
    }

    // 自分の直接のコライダーに入った場合
    private void OnTriggerEnter(Collider other)
    {
        HandleItemDrop(other);
    }

    /// <summary>
    /// アイテムが入ったときの実際の処理（子オブジェクトからも呼ばれる）
    /// </summary>
    public void HandleItemDrop(Collider other)
    {
        // ぶつかった相手が UFOItem コンポーネントを持っているか確認
        UFOItem item = other.GetComponent<UFOItem>();
        
        if (item != null)
        {
            // 獲得金額の計算（基本額 × 強化倍率）
            float finalValue = item.baseValue * scoreMultiplier;
            
            // 種類ごとにカウントや特別な処理をする
            switch (item.itemType)
            {
                case UFOItemType.CopperCoin:
                case UFOItemType.SilverCoin:
                case UFOItemType.GoldCoin:
                    // メインのお金（MoneyManager）ではなく、未洗浄メダルとして別に貯める
                    // 一元管理シングルトンがあればそちらに加算（ピンボールショップ等から参照される）
                    if (UnwashedMoneyManager.Instance != null)
                    {
                        UnwashedMoneyManager.Instance.Add(finalValue);
                    }
                    else
                    {
                        // 旧 API 互換: ローカルの累計とゴール表示も維持
                        unwashedMoney += finalValue;
                        UpdateUnwashedMoneyText();
                    }
                    Debug.Log($"[獲得] {item.itemType}！ (未洗浄メダル総額: {unwashedMoney}円)");
                    
                    // コイン獲得音の再生
                    PlaySound(coinGetSound);
                    break;
                case UFOItemType.Jackpot:
                    // メインのお金（MoneyManager）ではなく、未洗浄メダルとして別に貯める
                    if (UnwashedMoneyManager.Instance != null)
                    {
                        UnwashedMoneyManager.Instance.Add(finalValue);
                    }
                    else
                    {
                        unwashedMoney += finalValue;
                        UpdateUnwashedMoneyText();
                    }
                    Debug.Log($"[獲得] Jackpot！ (未洗浄メダル総額: {unwashedMoney}円)");
                    
                    // コイン獲得音の再生
                    PlaySound(coinGetSound);

                    // ランプを金（ゴールド）に点滅させる
                    TriggerLampFlash(jackpotFlashColor, true);

                    // ジャックポット発生時にコイン雨を降らせる
                    if (ItemSpawner.Instance != null)
                    {
                        System.Collections.Generic.List<GameObject> prefabsToSpawn = new System.Collections.Generic.List<GameObject>();
                        if (jackpotRainPrefabs != null && jackpotRainPrefabs.Count > 0)
                        {
                            prefabsToSpawn.AddRange(jackpotRainPrefabs);
                        }
                        else
                        {
                            // フォールバック
                            prefabsToSpawn.Add(ItemSpawner.Instance.goldCoinPrefab);
                        }
                        ItemSpawner.Instance.StartJackpotRain(prefabsToSpawn, jackpotRainCoinCount, jackpotRainDuration, jackpotRainAreaScale, jackpotRainAreaOffset);
                    }
                    break;
                case UFOItemType.Watch:
                    collectedWatches++;
                    Debug.Log($"[獲得] 時計！ (累計: {collectedWatches}個)");

                    // UFOキャッチャーの残り時間を延長
                    if (UFOCameraController.Instance != null)
                    {
                        UFOCameraController.Instance.AddPlayTime(watchTimeExtension);
                    }

                    // 画面のUIテキストが設定されていれば表示を更新する
                    if (watchCountText != null)
                    {
                        watchCountText.text = $"Watch: {collectedWatches}";
                    }

                    // 時計獲得音の再生（未設定ならコイン音で代用）
                    PlaySound(watchGetSound != null ? watchGetSound : coinGetSound);

                    // ランプを時計用の色に光らせる（常灯）
                    TriggerLampFlash(lampFlashColor, false);
                    break;
                case UFOItemType.BlackDiamond:
                    if (MoneyManager.Instance != null)
                    {
                        // baseValueが負の値（または正の値であっても絶対値）を減算として扱う設計
                        float valToReduce = Mathf.Abs(finalValue);
                        MoneyManager.Instance.ReduceMoney(valToReduce);
                        Debug.Log($"[獲得] BlackDiamond！ 洗浄されたお金を {valToReduce}円減らしました。");
                    }
                    PlaySound(blackDiamondGetSound != null ? blackDiamondGetSound : coinGetSound);

                    // ランプを紫に光らせる（常灯）
                    TriggerLampFlash(blackDiamondFlashColor, false);
                    break;
            }

            // アイテムを消去する
            Destroy(other.gameObject);
        }
    }

    /// <summary>
    /// 効果音を再生するヘルパー関数
    /// </summary>
    private void PlaySound(AudioClip clip)
    {
        if (clip != null)
        {
            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
                if (audioSource == null)
                {
                    audioSource = gameObject.AddComponent<AudioSource>();
                    audioSource.playOnAwake = false;
                    audioSource.spatialBlend = 0f; // 2D音響にして距離減衰を無視する
                }
            }
            audioSource.PlayOneShot(clip, soundVolume);
        }
    }

    private void TriggerLampFlash(Color color, bool isBlink)
    {
        IsFlashing = true; // 演出開始
        if (_lampCoroutine != null)
        {
            StopCoroutine(_lampCoroutine);
        }
        _lampCoroutine = StartCoroutine(FlashLampsCoroutine(color, isBlink));
    }

    private System.Collections.IEnumerator FlashLampsCoroutine(Color color, bool isBlink)
    {
        GameObject[] lampObjects = GameObject.FindGameObjectsWithTag(lampTag);

        // フォールバック: 大文字小文字の揺らぎ対策
        if ((lampObjects == null || lampObjects.Length == 0) && (lampTag == "InsertableItem" || lampTag == "insertableItem"))
        {
            string alternativeTag = (lampTag == "InsertableItem") ? "insertableItem" : "InsertableItem";
            try
            {
                lampObjects = GameObject.FindGameObjectsWithTag(alternativeTag);
                if (lampObjects != null && lampObjects.Length > 0)
                {
                    Debug.LogWarning($"[ランプ連動] タグ '{lampTag}' ではオブジェクトが見つかりませんでしたが、代替タグ '{alternativeTag}' で {lampObjects.Length} 個検出しました。こちらを使用します。");
                }
            }
            catch (System.Exception) { }
        }

        int totalObjects = lampObjects != null ? lampObjects.Length : 0;
        System.Collections.Generic.List<Light> targetLights = new System.Collections.Generic.List<Light>();
        System.Collections.Generic.List<Renderer> targetRenderers = new System.Collections.Generic.List<Renderer>();

        if (lampObjects != null)
        {
            foreach (var obj in lampObjects)
            {
                if (obj != null)
                {
                    targetLights.AddRange(obj.GetComponentsInChildren<Light>(true));
                    targetRenderers.AddRange(obj.GetComponentsInChildren<Renderer>(true));
                }
            }
        }

        Debug.Log($"[ランプ連動] 対象オブジェクトを {totalObjects} 個検出。制御対象: Light={targetLights.Count}個, Renderer={targetRenderers.Count}個");

        // 元の状態をあらかじめ記録（Blink時の復元用にも使用）
        foreach (var light in targetLights)
        {
            if (light != null && !_originalColors.ContainsKey(light))
            {
                _originalColors[light] = light.color;
                _originalEnabled[light] = light.enabled;
            }
        }

        foreach (var r in targetRenderers)
        {
            if (r != null && !_originalEmissionColors.ContainsKey(r))
            {
                Color origEmission = Color.black;
                if (r.sharedMaterial != null && r.sharedMaterial.HasProperty("_EmissionColor"))
                {
                    origEmission = r.sharedMaterial.GetColor("_EmissionColor");
                }
                _originalEmissionColors[r] = origEmission;
            }
        }

        if (isBlink)
        {
            // 点滅処理の実行（周期的にオン・オフ）
            float elapsed = 0f;
            bool isOn = true;

            while (elapsed < lampGreenDuration)
            {
                if (isOn)
                {
                    // 点灯（設定された色へ）
                    foreach (var light in targetLights)
                    {
                        if (light != null)
                        {
                            light.color = color;
                            light.enabled = true;
                        }
                    }
                    foreach (var r in targetRenderers)
                    {
                        if (r != null)
                        {
                            MaterialPropertyBlock mpb = new MaterialPropertyBlock();
                            r.GetPropertyBlock(mpb);
                            mpb.SetColor("_EmissionColor", color);
                            r.SetPropertyBlock(mpb);
                        }
                    }
                }
                else
                {
                    // 消灯・復元（元の色・状態へ）
                    foreach (var light in targetLights)
                    {
                        if (light != null)
                        {
                            light.color = _originalColors[light];
                            light.enabled = _originalEnabled[light];
                        }
                    }
                    foreach (var r in targetRenderers)
                    {
                        if (r != null)
                        {
                            MaterialPropertyBlock mpb = new MaterialPropertyBlock();
                            r.GetPropertyBlock(mpb);
                            mpb.SetColor("_EmissionColor", _originalEmissionColors[r]);
                            r.SetPropertyBlock(mpb);
                        }
                    }
                }

                isOn = !isOn;
                yield return new WaitForSeconds(jackpotBlinkInterval);
                elapsed += jackpotBlinkInterval;
            }
        }
        else
        {
            // 通常の常灯処理
            foreach (var light in targetLights)
            {
                if (light != null)
                {
                    light.color = color;
                    light.enabled = true;
                }
            }
            foreach (var r in targetRenderers)
            {
                if (r != null)
                {
                    MaterialPropertyBlock mpb = new MaterialPropertyBlock();
                    r.GetPropertyBlock(mpb);
                    mpb.SetColor("_EmissionColor", color);
                    r.SetPropertyBlock(mpb);
                }
            }

            yield return new WaitForSeconds(lampGreenDuration);
        }

        // 最後に確実に元の状態に復元
        ResetLampsToOriginal();
    }

    private void ResetLampsToOriginal()
    {
        if (_lampCoroutine != null)
        {
            StopCoroutine(_lampCoroutine);
            _lampCoroutine = null;
        }

        // Light の復元
        foreach (var kvp in _originalColors)
        {
            Light light = kvp.Key;
            if (light != null)
            {
                light.color = kvp.Value;
                if (_originalEnabled.TryGetValue(light, out bool wasEnabled))
                {
                    light.enabled = wasEnabled;
                }
            }
        }
        _originalColors.Clear();
        _originalEnabled.Clear();

        // Renderer の復元
        foreach (var kvp in _originalEmissionColors)
        {
            Renderer r = kvp.Key;
            if (r != null)
            {
                MaterialPropertyBlock mpb = new MaterialPropertyBlock();
                r.GetPropertyBlock(mpb);
                mpb.SetColor("_EmissionColor", kvp.Value);
                r.SetPropertyBlock(mpb);
            }
        }
        _originalEmissionColors.Clear();

        IsFlashing = false; // 演出終了
    }

    /// <summary>
    /// フィーバータイムを起動します。制限時間がストップし、設定された比率でコインが降ります。
    /// </summary>
    public void StartFeverTime()
    {
        // 1. 制限時間のストップ（UFOCameraControllerに通知）
        if (UFOCameraController.Instance != null)
        {
            UFOCameraController.Instance.StartFeverTime(feverDuration);
        }

        // 2. コイン雨の発生
        if (ItemSpawner.Instance != null)
        {
            System.Collections.Generic.List<GameObject> prefabsToSpawn = new System.Collections.Generic.List<GameObject>();
            
            float total = feverGoldRatio + feverSilverRatio + feverCopperRatio;
            if (total <= 0f) total = 1f;

            // 比率をプール内の個数として再現
            int poolSize = 100;
            int goldCount = Mathf.RoundToInt((feverGoldRatio / total) * poolSize);
            int silverCount = Mathf.RoundToInt((feverSilverRatio / total) * poolSize);
            int copperCount = poolSize - (goldCount + silverCount);

            for (int i = 0; i < goldCount; i++) if (feverGoldPrefab != null) prefabsToSpawn.Add(feverGoldPrefab);
            for (int i = 0; i < silverCount; i++) if (feverSilverPrefab != null) prefabsToSpawn.Add(feverSilverPrefab);
            for (int i = 0; i < copperCount; i++) if (feverCopperPrefab != null) prefabsToSpawn.Add(feverCopperPrefab);

            if (prefabsToSpawn.Count > 0)
            {
                ItemSpawner.Instance.StartJackpotRain(
                    prefabsToSpawn, 
                    feverRainCoinCount, 
                    feverDuration, 
                    feverRainAreaScale, 
                    feverRainAreaOffset
                );
            }
            else
            {
                Debug.LogWarning("[UFOItemGoal] フィーバータイム用のプレハブが設定されていません。");
            }
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        // 開発中にジャックポットのコイン雨の発生範囲を可視化します
        ItemSpawner spawner = FindObjectOfType<ItemSpawner>();
        if (spawner == null) return;

        // エリア0（左上）の基本バウンズを取得
        var bounds = spawner.ComputeCellBounds(0, spawner.gridType);

        Vector3 spawnerCenter = (spawner.armRoot != null) ? spawner.armRoot.position : spawner.transform.position;
        spawnerCenter.y += spawner.spawnYOffset;

        // エリア0の中心
        float cellCenterX = (bounds.minX + bounds.maxX) * 0.5f;
        float cellCenterZ = (bounds.minZ + bounds.maxZ) * 0.5f;

        // オフセットとスケールを適用
        float rainCenterX = cellCenterX + jackpotRainAreaOffset.x;
        float rainCenterZ = cellCenterZ + jackpotRainAreaOffset.y;
        float halfW = (bounds.maxX - bounds.minX) * 0.5f * jackpotRainAreaScale.x;
        float halfH = (bounds.maxZ - bounds.minZ) * 0.5f * jackpotRainAreaScale.y;

        Vector3 rainCenter = spawnerCenter + new Vector3(rainCenterX, 0f, rainCenterZ);
        Vector3 rainSize = new Vector3(halfW * 2f, 0.1f, halfH * 2f);

        // シーンビューに水色のボックスを表示
        Gizmos.color = new Color(0f, 0.8f, 1f, 0.18f);
        Gizmos.DrawCube(rainCenter, rainSize);
        Gizmos.color = new Color(0f, 0.8f, 1f, 0.85f);
        Gizmos.DrawWireCube(rainCenter, rainSize);

        // ラベルの描画
        UnityEditor.Handles.color = new Color(0f, 0.8f, 1f, 0.95f);
        UnityEditor.Handles.Label(rainCenter + Vector3.up * 0.1f, "Jackpot Rain Area (Cell 0)");
    }
#endif
}
