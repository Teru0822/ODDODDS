using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// コイン（DevilItem）が床や他のコインに衝突した際の効果音を、1箇所に集約して管理するマネージャー。
/// 各DevilItemはOnCollisionEnterで検知した衝突情報をNotifyImpact()に投げてくるだけで、
/// 「床かコインか」「音を鳴らすほどの速度か」「鳴らしすぎていないか」の判定・音量計算は
/// 全てここで一元的に行う。実機・練習機どちらのコインにも共通して働く。
///
/// シーンに1つだけ配置する。
/// </summary>
public class CoinImpactSoundManager : MonoBehaviour
{
    public static CoinImpactSoundManager Instance { get; private set; }

    [Header("音を鳴らす速度のしきい値")]
    [Tooltip("衝突の相対速度がこれ未満の場合は無音（ほとんど落差がない衝突では鳴らさない）")]
    [SerializeField] private float minVelocityThreshold = 0.5f;

    [Tooltip("衝突の相対速度がこれ以上で最大音量になる（高いところから落ちるほど大きい音、のスケーリング用）")]
    [SerializeField] private float maxVelocityForFullVolume = 5f;

    [Header("鳴らしすぎ防止")]
    [Tooltip("同じコイン1個につき、これより短い間隔では連続して音を鳴らさない（秒）")]
    [SerializeField] private float perCoinCooldown = 0.1f;

    [Tooltip("直近windowDuration秒間に鳴らせる衝突音の最大数。" +
             "大量のコインが同時に落ちる（Fever Time等）場合に、音の洪水になるのを防ぐ")]
    [SerializeField] private int maxSoundsPerWindow = 5;

    [SerializeField] private float windowDuration = 0.1f;

    private readonly Dictionary<UFOItem, float> _lastPlayTimeByCoin = new Dictionary<UFOItem, float>();
    private readonly Queue<float> _recentPlayTimestamps = new Queue<float>();

    private void Awake()
    {
        // シーンに1つだけ配置する想定だが、念のため既存インスタンスを奪わないガードを入れておく
        if (Instance == null)
        {
            Instance = this;
        }
    }

    /// <summary>
    /// UFOItem.OnCollisionEnterから呼ばれる。衝突相手が床かコインかを判定し、
    /// 速度に応じた音量で対応する効果音をDevilSEManager経由で再生する。
    /// </summary>
    public void NotifyImpact(Collision collision, UFOItem source)
    {
        if (collision.contactCount == 0 || source == null) return;

        // 毎ターンの初回コイン投下中（ItemSpawnerが大量のコインを一斉に降らせている最中〜
        // 降り終わって落ち着くまでの数秒間）は、床/コイン同士の衝突音が洪水のようになってしまうため
        // 一切鳴らさない。それ以外（クレーンで掴んで落とした時など）は通常通り鳴らす
        if (ItemSpawner.IsInitialSpawning) return;

        float speed = collision.relativeVelocity.magnitude;
        if (speed < minVelocityThreshold) return;

        float now = Time.time;

        // 同じコインの連打防止
        if (_lastPlayTimeByCoin.TryGetValue(source, out float lastTime) && now - lastTime < perCoinCooldown)
        {
            return;
        }

        // 直近windowDuration秒間の総再生数を制限（コイン雨などでの音の洪水防止）
        PruneOldTimestamps(now);
        if (_recentPlayTimestamps.Count >= maxSoundsPerWindow)
        {
            return;
        }

        float volumeFactor = Mathf.InverseLerp(minVelocityThreshold, maxVelocityForFullVolume, speed);

        // 時計・ルーレットアイテム（プレゼントボックス）は、床/コインどちらに当たった場合でも
        // 専用の衝突音を優先して鳴らす（両者は同じ音を共有する）
        bool isSpecialItem = source.itemType == UFOItemType.Watch || source.itemType == UFOItemType.RouletteItem;

        if (isSpecialItem)
        {
            DevilSEManager.Instance?.PlaySpecialItemImpact(volumeFactor);
        }
        else
        {
            bool isCoinToCoin = collision.gameObject.GetComponent<UFOItem>() != null;
            if (isCoinToCoin)
            {
                DevilSEManager.Instance?.PlayCoinImpact(volumeFactor);
            }
            else
            {
                DevilSEManager.Instance?.PlayFloorImpact(volumeFactor);
            }
        }

        _lastPlayTimeByCoin[source] = now;
        _recentPlayTimestamps.Enqueue(now);
    }

    private void PruneOldTimestamps(float now)
    {
        while (_recentPlayTimestamps.Count > 0 && now - _recentPlayTimestamps.Peek() > windowDuration)
        {
            _recentPlayTimestamps.Dequeue();
        }
    }
}
