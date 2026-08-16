using UnityEngine;

/// <summary>
/// Triggerコライダーにオブジェクトが侵入した際に、指定された効果音を再生するスクリプト。
/// </summary>
[RequireComponent(typeof(Collider))]
public class TriggerSoundPlayer : MonoBehaviour
{
    [Header("オーディオ設定")]
    [Tooltip("再生する効果音（AudioClip）")]
    [SerializeField] private AudioClip soundClip;

    [Tooltip("再生に使用するAudioSource。未設定の場合は自動取得/追加されます")]
    [SerializeField] private AudioSource audioSource;

    [Tooltip("効果音の音量 (1.0より大きい値で音量増幅可能)")]
    [Range(0f, 10f)]
    [SerializeField] private float volume = 1.0f;

    [Tooltip("3Dサウンドのブレンド率 (0 = 完全な2Dで距離減衰なし、1 = 完全な3D立体音響)")]
    [Range(0f, 1f)]
    [SerializeField] private float spatialBlend = 0.0f;

    [Header("トリガー条件設定")]
    [Tooltip("特定のタグが付いたオブジェクトのみ音を鳴らす場合に指定します（空欄の場合は何が侵入しても再生されます）")]
    [SerializeField] private string requiredTag = "";

    [Tooltip("一度だけ再生してスクリプトを無効化するかどうか")]
    [SerializeField] private bool playOnlyOnce = false;

    [Header("連動演出設定")]
    [Tooltip("音再生時にUFOCameraControllerのコイン投入演出を連動して開始させるか")]
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

        // AudioSource がない場合は自動的に取得/追加する
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
            }
        }

        // 音量減衰に影響する 2D/3D 設定を適用
        if (audioSource != null)
        {
            audioSource.spatialBlend = spatialBlend;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other == null) return;
        Debug.Log($"[TriggerSoundPlayer][DIAG] {gameObject.name}: OnTriggerEnter検知 other={other.name}, tag={other.tag}, requiredTag={requiredTag}, playOnlyOnce={playOnlyOnce}, hasPlayed={_hasPlayed}");
        if (playOnlyOnce && _hasPlayed) return;

        // 破棄された無効なコライダー参照をリストからクリーンアップ
        _activeColliders.RemoveWhere(c => c == null);

        // すでに検知済みの同一コライダーはスキップ
        if (_activeColliders.Contains(other))
        {
            Debug.Log($"[TriggerSoundPlayer][DIAG] {gameObject.name}: 既に_activeColliders登録済みのためスキップ (other={other.name}, 現在の登録数={_activeColliders.Count})");
            return;
        }

        // タグによる侵入制限があるかチェック
        if (!string.IsNullOrEmpty(requiredTag))
        {
            if (!other.CompareTag(requiredTag))
            {
                Debug.Log($"[TriggerSoundPlayer][DIAG] {gameObject.name}: タグ不一致のためスキップ (other.tag={other.tag})");
                return; // タグが一致しない場合は処理スキップ
            }
        }

        // 音声の再生
        if (soundClip != null && audioSource != null)
        {
            audioSource.PlayOneShot(soundClip, volume);
            _hasPlayed = true;

            // 検知済みリストに登録
            _activeColliders.Add(other);

            // UFOキャッチャーのコイン投入演出を連動トリガー
            Debug.Log($"[TriggerSoundPlayer][DIAG] {gameObject.name}: 音声再生・検知済み登録が完了。triggerCoinAnimation={triggerCoinAnimation}, UFOCameraController.Instance={(UFOCameraController.Instance != null ? UFOCameraController.Instance.gameObject.name + " (id=" + UFOCameraController.Instance.GetInstanceID() + ")" : "null")}");
            if (triggerCoinAnimation && UFOCameraController.Instance != null)
            {
                UFOCameraController.Instance.TriggerCoinInsertionAnimation();
            }

            if (playOnlyOnce)
            {
                // 一度のみ再生の場合はイベント後に本スクリプトを無効化する
                this.enabled = false;
            }
        }
        else
        {
            if (soundClip == null)
            {
                Debug.LogWarning($"[{gameObject.name}] 再生用の 'Sound Clip' がアタッチされていません。");
            }
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
