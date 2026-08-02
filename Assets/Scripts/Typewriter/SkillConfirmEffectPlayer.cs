using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// スキル確定エフェクト Prefab のアニメーション制御。
/// Play() を呼ぶと GlowBurst の拡大フェードアウト ＋ RingPulse のリング拡散を再生し、完了後に自身を Destroy する。
/// </summary>
[DisallowMultipleComponent]
public class SkillConfirmEffectPlayer : MonoBehaviour
{
    [Header("子要素")]
    [SerializeField] private Image _glowBurst;
    [SerializeField] private Image _ringPulse;

    [Header("GlowBurst パラメータ")]
    [SerializeField] private float _burstTargetScale  = 2.5f;
    [SerializeField] private float _burstDuration     = 0.3f;
    [SerializeField] private Ease  _burstScaleEase    = Ease.OutCubic;

    [Header("RingPulse パラメータ")]
    [SerializeField] private float _ringTargetScale   = 3.5f;
    [SerializeField] private float _ringDuration      = 0.45f;
    [SerializeField] private float _ringDelay         = 0.04f;
    [SerializeField] private Ease  _ringScaleEase     = Ease.OutCubic;

    public void Play()
    {
        var seq = DOTween.Sequence();

        if (_glowBurst != null)
        {
            var burstRT = _glowBurst.rectTransform;
            burstRT.localScale = Vector3.one * 0.1f;
            _glowBurst.color = new Color(_glowBurst.color.r, _glowBurst.color.g, _glowBurst.color.b, 1f);

            seq.Join(burstRT.DOScale(_burstTargetScale, _burstDuration).SetEase(_burstScaleEase));
            seq.Join(_glowBurst.DOFade(0f, _burstDuration).SetEase(Ease.Linear));
        }

        if (_ringPulse != null)
        {
            var ringRT = _ringPulse.rectTransform;
            ringRT.localScale = Vector3.one * 0.2f;
            _ringPulse.color = new Color(_ringPulse.color.r, _ringPulse.color.g, _ringPulse.color.b, 0.85f);

            seq.Insert(_ringDelay, ringRT.DOScale(_ringTargetScale, _ringDuration).SetEase(_ringScaleEase));
            seq.Insert(_ringDelay, _ringPulse.DOFade(0f, _ringDuration).SetEase(Ease.Linear));
        }

        float totalDuration = Mathf.Max(_burstDuration, _ringDelay + _ringDuration);
        seq.OnComplete(() => Destroy(gameObject));
    }
}
