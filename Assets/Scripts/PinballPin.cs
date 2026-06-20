using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// ピンボールのピン (Split / Enforce) を表すコンポーネント。
/// 玉が触れたら一度だけ効果を発動し、消費状態になる（以降の衝突では効果なし）。
/// Exchange 換金で再有効化できる。ヒット時の SFX / VFX はピン個別に設定可能。
///
/// 設置方法:
///   1. ピンの GameObject に本コンポーネントをアタッチ
///   2. type を Split か Enforce に設定
///   3. ピンに Collider を付ける（PinballBallController が本コンポーネントを直接見るのでタグは必須ではない）
///
/// ※ 発光・自転・pin_wing パルスなどの演出機能は廃止済み。見た目の演出はヒット VFX prefab 側で行う。
/// </summary>
public class PinballPin : MonoBehaviour
{
    public enum PinType { Split, Enforce }

    [Header("ピン種別")]
    [Tooltip("Split: 玉が分裂するピン\nEnforce: 玉の発色レベルを 1 段階上げ、価値を倍にするピン")]
    [SerializeField] private PinType type = PinType.Split;

    [Header("ヒット回数")]
    [Tooltip("効果が発動する最大ヒット回数。この回数に達するまでは毎回効果が出る。" +
             "（例: 3 なら 3 回目までは分裂/強化が起き、4 回目以降は無効）")]
    [SerializeField, Min(1)] private int maxHits = 1;

    [Tooltip("規定回数に達して効果が尽きたときに非表示 (SetActive(false)) にする GameObject。" +
             "※ Collider はここで消さない。Collider を含むオブジェクトを指定すると物理判定も消えるので、" +
             "見た目用の子オブジェクト（メッシュ等）を指定すること。null なら何も隠さない")]
    [SerializeField] private GameObject hideOnExhaust;

    [Header("ヒット時の SFX / VFX (このピン個別)")]
    [Tooltip("玉がこのピンに当たった瞬間に再生する効果音。ピンごとに別の音を割り当てられる。null なら無音")]
    [SerializeField] private AudioClip hitSfx;

    [Range(0f, 2f)]
    [Tooltip("ヒット SFX の音量")]
    [SerializeField] private float hitSfxVolume = 1f;

    [Range(0f, 0.5f)]
    [Tooltip("ヒット SFX の再生ピッチのランダム幅 (0 で固定)")]
    [SerializeField] private float hitSfxPitchVariance = 0f;

    [Tooltip("玉がこのピンに当たった瞬間にヒット位置へ生成する VFX prefab (ParticleSystem 等)。ピンごとに別の VFX を割り当てられる。null なら無し")]
    [SerializeField] private GameObject hitVfxPrefab;

    [Tooltip("生成した VFX を破棄するまでの秒数。ただし prefab に ParticleSystem があればその再生長で自動算出する")]
    [SerializeField, Min(0f)] private float hitVfxLifetime = 2f;

    [Header("Exchange 換金で再有効化")]
    [Tooltip("Exchange で換金 (DispenseMoney) が完了した時に、このピンを再有効化 (消費前の状態に戻す)")]
    [SerializeField] private bool reactivateOnExchangeDispense = true;

    private int _hits = 0;
    private ExchangeStation _exchangeStation;

    /// <summary>効果が尽きた（規定ヒット回数に達した）か。Collider は消さないので物理判定は残る。</summary>
    public bool IsConsumed => _hits >= maxHits;
    public PinType Type => type;

    void Start()
    {
        if (!reactivateOnExchangeDispense) return;
        _exchangeStation = FindAnyObjectByType<ExchangeStation>();
        if (_exchangeStation == null) return;
        if (_exchangeStation.onDispenseComplete == null)
        {
            _exchangeStation.onDispenseComplete = new UnityEvent();
        }
        _exchangeStation.onDispenseComplete.AddListener(Reactivate);
    }

    void OnDestroy()
    {
        if (_exchangeStation != null && _exchangeStation.onDispenseComplete != null)
        {
            _exchangeStation.onDispenseComplete.RemoveListener(Reactivate);
        }
    }

    /// <summary>ヒット数をリセットして再び効果を発動できるようにし、隠したオブジェクトを再表示する。</summary>
    public void Reactivate()
    {
        if (_hits == 0) return;
        _hits = 0;
        if (hideOnExhaust != null) hideOnExhaust.SetActive(true);
    }

    /// <summary>
    /// 玉が触れた時に呼ぶ。まだ規定回数に達していなければヒットを 1 加算して true（効果発動）。
    /// 規定回数に達した時点で hideOnExhaust を非表示にする。Collider は一切無効化しない。
    /// 既に効果が尽きていれば false。
    /// </summary>
    public bool TryConsume()
    {
        if (_hits >= maxHits) return false;
        _hits++;
        if (_hits >= maxHits && hideOnExhaust != null)
        {
            hideOnExhaust.SetActive(false);
        }
        return true;
    }

    /// <summary>
    /// このピン個別のヒット SFX / VFX を worldPos で発火する。
    /// SFX は PinballSplitFXManager のプール経由（フレーム上限あり）で鳴らし、無ければ簡易再生にフォールバック。
    /// VFX は prefab を生成し、ParticleSystem があれば再生長で、無ければ hitVfxLifetime で自動破棄する。
    /// </summary>
    public void PlayHitFX(Vector3 worldPos)
    {
        // SFX
        if (hitSfx != null)
        {
            var fx = PinballSplitFXManager.Instance;
            if (fx != null)
                fx.PlayPooledOneShot(worldPos, hitSfx, hitSfxVolume, hitSfxPitchVariance);
            else
                AudioSource.PlayClipAtPoint(hitSfx, worldPos, hitSfxVolume);
        }

        // VFX
        if (hitVfxPrefab != null)
        {
            var go = Instantiate(hitVfxPrefab, worldPos, hitVfxPrefab.transform.rotation);
            var ps = go.GetComponent<ParticleSystem>();
            if (ps != null)
            {
                ps.Play();
                var main = ps.main;
                Destroy(go, main.duration + main.startLifetime.constantMax + 0.5f);
            }
            else
            {
                Destroy(go, hitVfxLifetime);
            }
        }
    }
}
