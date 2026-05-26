using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using TMPro;

/// <summary>
/// Bin を受け取って中身の cup を tube 上部に出現させる「exchange」コンポーネント。
/// レティクル照準 + プレイヤーが Bin を持っている時のみハイライト。
/// クリックで Bin を破棄し、cupPrefab を tube 上部に生成、即座に Z 軸 120° 回転を加える。
/// </summary>
[DisallowMultipleComponent]
public class ExchangeStation : InteractableHighlight
{
    [Header("Exchange 設定")]
    [Tooltip("Bin を受け取った時に出現させる cup プレハブ (BallCup 付き)")]
    public GameObject cupPrefab;

    [Tooltip("cup を出現させる位置 Transform (通常: tube 子オブジェクトの上端)")]
    public Transform tubeSpawnPoint;

    [Tooltip("出現直後に cup に加える回転 (deg)。デフォルト Z 軸 120°")]
    public Vector3 spawnRotationEuler = new Vector3(0f, 0f, 120f);

    [Tooltip("回転をワールド空間で適用 (false ならローカル空間)")]
    public bool rotateInWorldSpace = false;

    [Tooltip("回転にかける秒数。0 で瞬時、1.0 で 1 秒かけてゆっくり回転")]
    public float rotationDuration = 1.0f;

    [Tooltip("回転の補間カーブ (任意)。空ならリニア。EaseInOut にすると始終なめらか")]
    public AnimationCurve rotationCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Tooltip("受け取った Bin を子に置く親 (null なら破棄前のシーンルート)")]
    public Transform spawnedCupParent;

    [Tooltip("ボール再配置の散らばり半径 (m)")]
    public float ballScatterRadius = 0.08f;

    [Header("価値モニター (ex_disp)")]
    [Tooltip("価値を表示する TMP_Text (TextMeshPro 3D / UI どちらでも可)。null なら子から自動検索")]
    public TMP_Text valueDisplayText;

    [Tooltip("値表示フォーマット ({0} に累計値が入る)")]
    public string valueDisplayFormat = "¥{0:N0}";

    [Tooltip("起動時の累計値 (テスト用シード)")]
    public float currentTotalValue = 0f;

    [Tooltip("吸い込み時にデフォルトのボール価値 (UFOItem が無いボール用)")]
    public float defaultBallValue = 100f;

    [Tooltip("吸い込んだ価値を UnwashedMoneyManager にも加算する (ゲーム通貨と連動させたい場合)")]
    public bool addToUnwashedMoney = false;

    [Header("お金払い出し (ex_button → MoneySpawner)")]
    [Tooltip("money オブジェクトの生成位置 Transform (通常: exchange の子 MoneySpawner)。null なら子から自動検索")]
    public Transform moneySpawner;

    [Tooltip("10000 の位 1 個分の money prefab (例: money_big)")]
    public GameObject moneyBigPrefab;

    [Tooltip("money_big 1 個が表す単位額 (デフォルト 10000)")]
    public int bigDenomination = 10000;

    [Tooltip("1000 の位 1 個分の money prefab (例: money_middle)")]
    public GameObject moneyMiddlePrefab;

    [Tooltip("money_middle 1 個が表す単位額 (デフォルト 1000)")]
    public int middleDenomination = 1000;

    [Tooltip("100 の位 1 個分の money prefab (例: money_small)")]
    public GameObject moneySmallPrefab;

    [Tooltip("money_small 1 個が表す単位額 (デフォルト 100)")]
    public int smallDenomination = 100;

    [Tooltip("各 money を 1 つずつ生成する間隔 (秒)。0 で一気に生成")]
    public float dispenseInterval = 0.05f;

    [Tooltip("生成位置のランダム散らばり半径 (m)")]
    public float dispenseJitterRadius = 0.05f;

    [Tooltip("生成した money に与える初速 (moneySpawner のローカル空間)")]
    public Vector3 dispenseInitialVelocity = Vector3.zero;

    [Tooltip("各 money の生成時に Y 軸でランダム回転させる")]
    public bool dispenseRandomYRotation = true;

    [Tooltip("払い出し完了後に累計値を 0 にリセットする")]
    public bool resetValueOnDispense = true;

    [Header("ドア演出 (ex_door)")]
    [Tooltip("開閉アニメーション対象のドア Transform。null なら子から 'ex_door' を自動検索")]
    public Transform exDoor;

    [Tooltip("ex_door 開く時の移動量 (ex_door 自身のローカル Z+ 方向、メートル)")]
    public float doorOpenDistance = 0.3f;

    [Tooltip("ボタンクリックから ex_door が開き始めるまでの秒数 (デフォルト t=2)")]
    public float doorOpenTime = 2.0f;

    [Tooltip("ボタンクリックから money 飛散・所持金加算が始まるまでの秒数 (デフォルト t=4)")]
    public float scatterTime = 4.0f;

    [Tooltip("ボタンクリックから ex_door が閉じ始めるまでの秒数 (デフォルト t=8)")]
    public float doorCloseTime = 8.0f;

    [Tooltip("ドアの開閉アニメーション秒数 (片方向)")]
    public float doorAnimDuration = 0.4f;

    [Header("Money 飛散演出")]
    [Tooltip("飛散時に各 money に与える外向き初速の大きさ (m/s)")]
    public float scatterSpeed = 3.5f;

    [Tooltip("飛散初速のランダム上方成分 (0=水平, 1=ほぼ真上)")]
    [Range(0f, 1f)] public float scatterUpwardBias = 0.6f;

    [Tooltip("飛散時に各 money に与える角速度の大きさ (rad/s)")]
    public float scatterAngularSpeed = 12f;

    [Tooltip("飛散を 1 個ずつずらす間隔 (秒)。0 で一斉")]
    public float scatterInterval = 0.05f;

    [Tooltip("飛散開始から各 money を Destroy するまでの寿命 (秒)")]
    public float scatterLifetime = 0.6f;

    [Header("イベント")]
    [Tooltip("交換成立時に発火 (生成された cup の BallCup を引数に)")]
    public ExchangeEvent onExchange;

    [Tooltip("ボール吸い込みで累計値が変化した時に発火 (引数: 新累計値)")]
    public UnityEvent<float> onTotalValueChanged;

    [Tooltip("払い出し開始時に発火")]
    public UnityEvent onDispenseStart;

    [Tooltip("払い出し完了時に発火")]
    public UnityEvent onDispenseComplete;

    [System.Serializable] public class ExchangeEvent : UnityEvent<BallCup> { }

    private bool _dispensing;
    /// <summary>払い出し中フラグ (ボタン側で二重起動防止に使える)</summary>
    public bool IsDispensing => _dispensing;

    /// <summary>現在の累計価値 (吸い込みごとに加算)</summary>
    public float CurrentTotalValue => currentTotalValue;

    private Vector3 _doorClosedLocalPos;
    private bool _doorInitialized;
    private readonly List<GameObject> _spawnedMoney = new List<GameObject>();

    private void Start()
    {
        // valueDisplayText が未指定なら子から自動検索 (TMP_Text 派生: TextMeshPro / TextMeshProUGUI 両対応)
        if (valueDisplayText == null)
        {
            valueDisplayText = GetComponentInChildren<TMP_Text>(true);
        }
        // moneySpawner が未指定なら子から名前で検索 (MoneySpawner or money_spawner)
        if (moneySpawner == null)
        {
            var found = transform.Find("MoneySpawner");
            if (found == null) found = transform.Find("money_spawner");
            if (found == null)
            {
                // 階層深く検索
                foreach (var t in GetComponentsInChildren<Transform>(true))
                {
                    if (t.name == "MoneySpawner" || t.name == "money_spawner") { found = t; break; }
                }
            }
            if (found != null) moneySpawner = found;
        }
        // exDoor 自動検索
        if (exDoor == null)
        {
            var d = transform.Find("ex_door");
            if (d == null)
            {
                foreach (var t in GetComponentsInChildren<Transform>(true))
                {
                    if (t.name == "ex_door") { d = t; break; }
                }
            }
            if (d != null) exDoor = d;
        }
        if (exDoor != null)
        {
            _doorClosedLocalPos = exDoor.localPosition;
            _doorInitialized = true;
        }
        UpdateValueDisplay();
    }

    public override bool IsInteractable(CupPickupController pickup)
    {
        if (pickup == null) return false;
        return pickup.IsHoldingBin && cupPrefab != null && tubeSpawnPoint != null;
    }

    /// <summary>ボール 1 個分の価値を累計に加算してモニター表示を更新する。</summary>
    public void AddValue(float amount)
    {
        if (amount == 0f) return;
        currentTotalValue += amount;
        UpdateValueDisplay();
        if (addToUnwashedMoney && UnwashedMoneyManager.Instance != null && amount > 0f)
        {
            UnwashedMoneyManager.Instance.Add(amount);
        }
        onTotalValueChanged?.Invoke(currentTotalValue);
    }

    /// <summary>累計値を 0 にリセット (デバッグ・ステージ再開用)</summary>
    public void ResetValue()
    {
        currentTotalValue = 0f;
        UpdateValueDisplay();
        onTotalValueChanged?.Invoke(currentTotalValue);
    }

    private void UpdateValueDisplay()
    {
        if (valueDisplayText == null) return;
        valueDisplayText.text = string.Format(valueDisplayFormat, Mathf.FloorToInt(currentTotalValue));
    }

    /// <summary>
    /// 現在の累計値の桁数に応じて money_big/middle/small を MoneySpawner 位置から落とす。
    /// 10000 の位 → money_big、1000 の位 → money_middle、100 の位 → money_small。
    /// 完了後 resetValueOnDispense=true なら累計値を 0 にリセット。
    /// </summary>
    public void DispenseMoney()
    {
        if (_dispensing) return;
        if (currentTotalValue <= 0f) return;
        StartCoroutine(DispenseMoneyCoroutine());
    }

    private IEnumerator DispenseMoneyCoroutine()
    {
        _dispensing = true;
        onDispenseStart?.Invoke();

        int value = Mathf.FloorToInt(currentTotalValue);
        // 各桁の数字を取り出す (10000/1000/100 の位)
        int bigCount = (value / Mathf.Max(1, bigDenomination)) % 10;
        int middleCount = (value / Mathf.Max(1, middleDenomination)) % 10;
        int smallCount = (value / Mathf.Max(1, smallDenomination)) % 10;

        Debug.Log($"[ExchangeStation] DispenseMoney: value=¥{value}, big={bigCount}, middle={middleCount}, small={smallCount}", this);

        // クリック直後に累計値表示はクリアし、ボタン側の再押下を防ぐ
        if (resetValueOnDispense) ResetValue();

        // クリック時刻 (t=0) を起点にすべてのタイミングを絶対時刻で待機する
        float clickTime = Time.time;

        _spawnedMoney.Clear();
        // money 生成は t=0 から並行して進める (バックグラウンドで)
        StartCoroutine(SpawnAllMoneyRoutine(bigCount, middleCount, smallCount));

        // t = doorOpenTime まで待つ → ex_door を開く
        yield return WaitUntilAfter(clickTime, doorOpenTime);
        yield return AnimateDoor(opening: true);

        // t = scatterTime まで待つ → money 飛散 + 所持金加算
        yield return WaitUntilAfter(clickTime, scatterTime);
        yield return ScatterSpawnedMoney();

        if (value > 0)
        {
            if (MoneyManager.Instance != null)
            {
                MoneyManager.Instance.AddMoney(value);
            }
            else
            {
                Debug.LogWarning("[ExchangeStation] MoneyManager.Instance が見つからず、所持金加算をスキップしました。", this);
            }
        }

        // t = doorCloseTime まで ex_door は開いた位置で固定 → 到達後に閉じる
        yield return WaitUntilAfter(clickTime, doorCloseTime);
        yield return AnimateDoor(opening: false);

        _dispensing = false;
        onDispenseComplete?.Invoke();
    }

    /// <summary>clickTime からの絶対経過秒数 targetElapsed まで待つ。すでに過ぎていれば即時 return。</summary>
    private IEnumerator WaitUntilAfter(float clickTime, float targetElapsed)
    {
        float remaining = targetElapsed - (Time.time - clickTime);
        if (remaining > 0f) yield return new WaitForSeconds(remaining);
    }

    private IEnumerator SpawnAllMoneyRoutine(int bigCount, int middleCount, int smallCount)
    {
        yield return SpawnMoneyOverTime(moneyBigPrefab, bigCount, "big");
        yield return SpawnMoneyOverTime(moneyMiddlePrefab, middleCount, "middle");
        yield return SpawnMoneyOverTime(moneySmallPrefab, smallCount, "small");
    }

    private IEnumerator AnimateDoor(bool opening)
    {
        if (exDoor == null) yield break;
        if (!_doorInitialized)
        {
            _doorClosedLocalPos = exDoor.localPosition;
            _doorInitialized = true;
        }
        // ex_door 自身の局所 Z+ 方向に doorOpenDistance だけずらす。
        // localPosition は親の座標系なので、ex_door 自身の回転で回して親の座標系に変換する。
        Vector3 offsetLocalSelf = new Vector3(0f, 0f, doorOpenDistance);
        Vector3 offsetInParent = exDoor.localRotation * offsetLocalSelf;
        Vector3 from = exDoor.localPosition;
        Vector3 to = opening ? (_doorClosedLocalPos + offsetInParent) : _doorClosedLocalPos;
        float dur = Mathf.Max(0.001f, doorAnimDuration);
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float u = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / dur));
            exDoor.localPosition = Vector3.LerpUnclamped(from, to, u);
            yield return null;
        }
        exDoor.localPosition = to;
    }

    private IEnumerator ScatterSpawnedMoney()
    {
        if (_spawnedMoney.Count == 0) yield break;

        Transform sp = moneySpawner != null ? moneySpawner : transform;
        foreach (var go in _spawnedMoney)
        {
            if (go == null) continue;

            // 外向き方向 (sp.position から見た radial) + 上向きバイアス
            Vector3 outward = go.transform.position - sp.position;
            outward.y = 0f;
            if (outward.sqrMagnitude < 0.0001f)
            {
                outward = new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f));
            }
            outward.Normalize();
            Vector3 dir = Vector3.Lerp(outward, Vector3.up, scatterUpwardBias).normalized;

            var rb = go.GetComponent<Rigidbody>();
            if (rb == null) rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.linearVelocity = dir * scatterSpeed;
            rb.angularVelocity = Random.onUnitSphere * scatterAngularSpeed;

            Destroy(go, scatterLifetime);

            if (scatterInterval > 0f) yield return new WaitForSeconds(scatterInterval);
        }
        _spawnedMoney.Clear();
    }

    private IEnumerator SpawnMoneyOverTime(GameObject prefab, int count, string label)
    {
        if (prefab == null)
        {
            if (count > 0) Debug.LogWarning($"[ExchangeStation] money_{label} prefab 未設定。{count} 個をスキップ", this);
            yield break;
        }
        if (count <= 0) yield break;

        Transform sp = moneySpawner != null ? moneySpawner : transform;
        for (int i = 0; i < count; i++)
        {
            Vector2 j = Random.insideUnitCircle * dispenseJitterRadius;
            // moneySpawner のローカル平面 (right/forward) 上に散らす
            Vector3 pos = sp.position + sp.right * j.x + sp.forward * j.y;
            Quaternion rot = sp.rotation;
            if (dispenseRandomYRotation) rot *= Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

            var go = Instantiate(prefab, pos, rot);
            _spawnedMoney.Add(go);

            if (dispenseInitialVelocity != Vector3.zero)
            {
                var rb = go.GetComponent<Rigidbody>();
                if (rb != null) rb.linearVelocity = sp.TransformDirection(dispenseInitialVelocity);
            }

            if (dispenseInterval > 0f) yield return new WaitForSeconds(dispenseInterval);
        }
    }

    /// <summary>Bin を受け取り、cup を出現させて中身を移す。生成された BallCup を返す。</summary>
    public BallCup AcceptBin(CupBin bin)
    {
        if (bin == null || cupPrefab == null || tubeSpawnPoint == null) return null;

        // tube 上部に cup を生成 (親なし → SetParent(true) で親 lossyScale による巨大化を防ぐ)
        Vector3 pos = tubeSpawnPoint.position;
        Quaternion rot = tubeSpawnPoint.rotation;
        Transform parent = spawnedCupParent != null ? spawnedCupParent : null;
        GameObject cupGo = Instantiate(cupPrefab, pos, rot);
        if (parent != null)
        {
            cupGo.transform.SetParent(parent, true);
        }

        // 中身のボールを cup に流し込む
        var newCup = cupGo.GetComponent<BallCup>();
        if (newCup != null)
        {
            newCup.ReceiveContents(bin.heldBalls, ballScatterRadius);
        }
        else
        {
            Debug.LogWarning("[ExchangeStation] 生成した cupPrefab に BallCup が付いていません。ボールが移されません。", cupGo);
        }

        // 出現直後に Z 軸 120° (任意の) 回転をゆっくり加える。
        // 既存シリアライズ済み Inspector で値が壊れているケース (旧バージョンに無かったフィールド) のセーフティ:
        // - rotationDuration が 0 以下なら 1.0 秒に強制
        // - rotationCurve が null/空ならデフォルト EaseInOut を使う
        float effectiveDuration = rotationDuration > 0f ? rotationDuration : 1.0f;
        AnimationCurve effectiveCurve = (rotationCurve != null && rotationCurve.length >= 2)
            ? rotationCurve
            : AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        if (newCup != null)
        {
            newCup.StartSmoothRotation(spawnRotationEuler, effectiveDuration, rotateInWorldSpace, effectiveCurve);
        }
        else
        {
            // BallCup が無くても回転だけはする
            if (rotateInWorldSpace) cupGo.transform.Rotate(spawnRotationEuler, Space.World);
            else cupGo.transform.Rotate(spawnRotationEuler, Space.Self);
        }

        // bin の参照をクリアして破棄 (heldBalls はもう新 cup 側にあるので OnDestroy で消されないよう外す)
        bin.heldBalls = null;
        Destroy(bin.gameObject);

        onExchange?.Invoke(newCup);
        return newCup;
    }
}
