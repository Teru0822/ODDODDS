using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// タイプライターでのブラックダイヤ強制売却を演出するUI。
/// 普段は非表示（_panel）で、売却が発生した時だけ表示し、
/// ダイヤの個数を保有数→0へ、所持金を売却前→売却後へカウントアニメーションさせる。
/// ダイヤを保有していない(count=0)場合は呼ばれない想定。
/// </summary>
public class BlackDiamondSellDisplay : MonoBehaviour
{
    [Header("表示パネル")]
    [Tooltip("普段は非表示。売却演出の間だけ表示する")]
    [SerializeField] private GameObject _panel;

    [Header("ダイヤ")]
    [Tooltip("磨き段階(0〜3)ごとのダイヤ画像。RoguelikeManager.GetDiamondPolishStage() の値に対応")]
    [SerializeField] private Sprite[] _diamondSprites = new Sprite[4];
    [SerializeField] private Image _diamondImage;
    [Tooltip("現所持金に対する増減率の表示。例: -15%")]
    [SerializeField] private TMP_Text _diamondPercentText;
    [Tooltip("保有数→0へカウントダウンする個数表示")]
    [SerializeField] private TMP_Text _diamondCountText;

    [Header("デビルコイン")]
    [Tooltip("売却前→売却後の金額へカウントする所持金表示")]
    [SerializeField] private TMP_Text _coinAmountText;

    [Header("アニメーション")]
    [Tooltip("カウントアニメーションの所要時間(秒)")]
    [SerializeField] private float _duration = 1.0f;
    [Tooltip("アニメーション終了後、パネルを閉じるまでの表示保持時間(秒)")]
    [SerializeField] private float _holdAfterFinish = 1.0f;

    [Header("SE")]
    [Tooltip("数字が流れている間に鳴らすタイプ音")]
    [SerializeField] private AudioClip _tickClip;
    [Tooltip("タイプ音の間隔(秒)")]
    [SerializeField] private float _tickInterval = 0.06f;
    [Range(0f, 1f)]
    [SerializeField] private float _tickVolume = 0.3f;
    [Tooltip("再生用 AudioSource。null なら自身に AddComponent して使う")]
    [SerializeField] private AudioSource _audioSource;

    private void Awake()
    {
        if (_panel != null) _panel.SetActive(false);
    }

    /// <summary>
    /// 売却演出を再生する。diamondCount は演出開始時点の保有数（0はTypewriterInteractable側で呼ばれない想定）。
    /// rate は現所持金に対する増減率（正で増加、負で減少）。moneyBefore/After は売却前後の所持金（表示用）。
    /// </summary>
    public IEnumerator PlaySellAnimation(int diamondCount, int stage, float rate, float moneyBefore, float moneyAfter)
    {
        if (diamondCount <= 0) yield break;

        if (_panel != null) _panel.SetActive(true);

        if (_diamondImage != null && _diamondSprites != null && _diamondSprites.Length > 0)
        {
            int spriteIndex = Mathf.Clamp(stage, 0, _diamondSprites.Length - 1);
            var sprite = _diamondSprites[spriteIndex];
            if (sprite != null) _diamondImage.sprite = sprite;
        }

        if (_diamondPercentText != null)
        {
            int percent = Mathf.RoundToInt(rate * 100f);
            _diamondPercentText.text = (percent >= 0 ? "+" : "") + percent + "%";
        }

        EnsureAudioSource();

        float elapsed = 0f;
        float soundTimer = 0f;
        PlayTick();

        while (elapsed < _duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / _duration);
            float ease = 1f - Mathf.Pow(1f - t, 3); // EaseOutCubic

            int currentDiamondCount = Mathf.RoundToInt(Mathf.Lerp(diamondCount, 0, ease));
            float currentMoney = Mathf.Lerp(moneyBefore, moneyAfter, ease);

            if (_diamondCountText != null) _diamondCountText.text = currentDiamondCount.ToString();
            if (_coinAmountText != null) _coinAmountText.text = Mathf.RoundToInt(currentMoney).ToString("N0");

            soundTimer += Time.deltaTime;
            if (soundTimer >= _tickInterval)
            {
                soundTimer -= _tickInterval;
                PlayTick();
            }

            yield return null;
        }

        if (_diamondCountText != null) _diamondCountText.text = "0";
        if (_coinAmountText != null) _coinAmountText.text = Mathf.RoundToInt(moneyAfter).ToString("N0");

        yield return new WaitForSeconds(_holdAfterFinish);

        if (_panel != null) _panel.SetActive(false);
    }

    private void EnsureAudioSource()
    {
        if (_audioSource == null) _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.playOnAwake = false;
        }
    }

    private void PlayTick()
    {
        if (_tickClip == null || _audioSource == null) return;
        _audioSource.PlayOneShot(_tickClip, _tickVolume);
    }
}
