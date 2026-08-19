using UnityEngine;

/// <summary>
/// コイン投入口のTriggerコライダーにコインが侵入した際に、コイン投入音を再生するスクリプト。
/// 実機・練習機どちらのコイン投入口にも配置できる（isPracticeInstanceで切り替え）。
/// 再生するクリップ自体はUFOSEManager側で一元管理しており（実機:realCoinInsertSound /
/// 練習機:practiceCoinInsertSound）、このスクリプトはトリガー検知と再生先の切り替えだけを担当する。
/// </summary>
[RequireComponent(typeof(Collider))]
public class TriggerSoundPlayer : MonoBehaviour
{
    [Header("対象機体")]
    [Tooltip("練習機（Practice_Cranegame）側のコイン投入口の場合はON。実機側はOFFのままにする。" +
             "UFOSEManagerでの再生先クリップと、連動するコイン投入アニメーションの呼び出し先が" +
             "実機/練習機のどちらになるかがこれで切り替わるため、実機・練習機の演出が競合しない")]
    [SerializeField] private bool isPracticeInstance = false;

    [Tooltip("練習機側のコイン投入アニメーションを起動するコントローラー（isPracticeInstance時のみ使用）")]
    [SerializeField] private TutorialCraneController tutorialCrane;

    [Header("トリガー条件設定")]
    [Tooltip("特定のタグが付いたオブジェクトのみ音を鳴らす場合に指定します（空欄の場合は何が侵入しても再生されます）")]
    [SerializeField] private string requiredTag = "";

    [Tooltip("一度だけ再生してスクリプトを無効化するかどうか")]
    [SerializeField] private bool playOnlyOnce = false;

    [Header("連動演出設定")]
    [Tooltip("音再生時に、対応する機体（実機/練習機）のコイン投入演出を連動して開始させるか")]
    [SerializeField] private bool triggerCoinAnimation = true;

    private bool _hasPlayed = false;
    private System.Collections.Generic.HashSet<Collider> _activeColliders = new System.Collections.Generic.HashSet<Collider>();

    private void Start()
    {
        // Collider のチェック
        Collider col = GetComponent<Collider>();
        if (col != null && !col.isTrigger)
        {
            Debug.LogWarning($"[{gameObject.name}] コライダーの 'Is Trigger' が無効になっています。Triggerイベントを発生させるために有効にしてください。");
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other == null) return;
        if (playOnlyOnce && _hasPlayed) return;

        // 破棄された無効なコライダー参照をリストからクリーンアップ
        _activeColliders.RemoveWhere(c => c == null);

        // すでに検知済みの同一コライダーはスキップ
        if (_activeColliders.Contains(other)) return;

        // タグによる侵入制限があるかチェック
        if (!string.IsNullOrEmpty(requiredTag) && !other.CompareTag(requiredTag))
        {
            return; // タグが一致しない場合は処理スキップ
        }

        // コイン投入音の再生（実機/練習機どちらの機体かでUFOSEManager側の再生先が切り替わる）
        UFOSEManager.Instance?.PlayCoinInsert(isPracticeInstance);
        _hasPlayed = true;

        // 検知済みリストに登録
        _activeColliders.Add(other);

        // 対応する機体のコイン投入演出を連動トリガー（実機/練習機を取り違えないよう、
        // isPracticeInstanceに応じて呼び出し先を完全に分離している）
        if (triggerCoinAnimation)
        {
            if (isPracticeInstance)
            {
                tutorialCrane?.TriggerCoinInsertionAnimationTutorial();
            }
            else if (UFOCameraController.Instance != null)
            {
                UFOCameraController.Instance.TriggerCoinInsertionAnimation();
            }
        }

        if (playOnlyOnce)
        {
            // 一度のみ再生の場合はイベント後に本スクリプトを無効化する
            this.enabled = false;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other != null)
        {
            // エリアから出たら登録解除（再進入で再び音が鳴るようになります）
            _activeColliders.Remove(other);
        }
    }
}
