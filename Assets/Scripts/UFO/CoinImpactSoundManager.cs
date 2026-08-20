using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// コイン（DevilItem）が床や他のコインに衝突した際の効果音を、1箇所に集約して管理するマネージャー。
/// 各DevilItemはOnCollisionEnterで検知した衝突情報をNotifyImpact()に投げてくるだけで、
/// 「床かコインか」「音を鳴らすほどの速度か」「鳴らしすぎていないか」の判定・音量計算は
/// 全てここで一元的に行う。実機・練習機どちらのコインにも共通して働く。
///
/// 時計・ルーレットアイテム（プレゼントボックス）も、鳴らす音が違うだけで判定の仕組みは
/// 通常コインと完全に同じにしている（別枠のクールダウンや「1回だけ」フラグ等は持たない）。
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
    [Tooltip("同じアイテム1個につき、これより短い間隔では連続して音を鳴らさない（秒）。" +
             "コイン・時計・ルーレットアイテム共通で使う")]
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

        bool isSpecialItem = source.itemType == UFOItemType.Watch || source.itemType == UFOItemType.RouletteItem;

        // アーム（爪）のClawCarrierZone内にいる間は、DevilClawCarrierが毎FixedUpdateで運搬中の
        // アイテム同士を押し合わせるため、意図しない接触が頻発する。運搬中は音を鳴らさず、
        // 実際に落とされてゾーンの外に出てから初めて鳴るようにする（コイン・特別アイテム共通）
        if (DevilClawCarrier.IsItemInsideAnyCarrierZone(source)) return;

        float speed = collision.relativeVelocity.magnitude;
        if (speed < minVelocityThreshold) return;

        float now = Time.time;

        // 連打防止（コイン・特別アイテム共通の仕組み）
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
        bool isCoinToCoin = collision.gameObject.GetComponent<UFOItem>() != null;

        if (isSpecialItem)
        {
            if (isCoinToCoin)
            {
                DevilSEManager.Instance?.PlaySpecialItemImpact(volumeFactor);
            }
            else
            {
                DevilSEManager.Instance?.PlaySpecialItemFloorImpact(volumeFactor);
            }
        }
        else if (isCoinToCoin)
        {
            DevilSEManager.Instance?.PlayCoinImpact(volumeFactor);
        }
        else
        {
            DevilSEManager.Instance?.PlayFloorImpact(volumeFactor);
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
