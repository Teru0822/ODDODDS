using System.Collections;
using UnityEngine;

/// <summary>
/// タイプライターのキー 1 個にアタッチする押し込みアニメーション。
/// 押し込み方向 / 深さ / 速度は TypewriterController 側で一元管理し、
/// 各キーは world delta を受け取って localPosition を補間するだけ。
/// </summary>
[DisallowMultipleComponent]
public class TypewriterKey : MonoBehaviour
{
    private Vector3 _restLocalPos;
    private bool _initialized;
    private bool _held;

    private void Awake()
    {
        EnsureInitialized();
    }

    public void EnsureInitialized()
    {
        if (_initialized) return;
        _restLocalPos = transform.localPosition;
        _initialized = true;
    }

    /// <summary>キーを 1 回押し込んで戻す。worldDelta は親空間に変換して加算する。</summary>
    public IEnumerator PressOnce(Vector3 worldDelta, float downDuration, float upDuration)
    {
        yield return PressDown(worldDelta, downDuration);
        yield return PressUp(upDuration);
    }

    /// <summary>キーを底まで押し込む (戻さない)。サウンド/文字追加を底着き瞬間に挟むため分離。</summary>
    public IEnumerator PressDown(Vector3 worldDelta, float downDuration)
    {
        EnsureInitialized();
        Vector3 localDelta = WorldToLocalDelta(worldDelta);
        yield return AnimateTo(_restLocalPos + localDelta, downDuration);
    }

    /// <summary>PressDown 後にキーを rest 位置に戻す。</summary>
    public IEnumerator PressUp(float upDuration)
    {
        EnsureInitialized();
        yield return AnimateTo(_restLocalPos, upDuration);
    }

    /// <summary>キーを下げて保持。Release() を呼ぶまで戻らない。left shift 用。</summary>
    public IEnumerator HoldDown(Vector3 worldDelta, float downDuration)
    {
        EnsureInitialized();
        if (_held) yield break;
        Vector3 localDelta = WorldToLocalDelta(worldDelta);
        yield return AnimateTo(_restLocalPos + localDelta, downDuration);
        _held = true;
    }

    public IEnumerator Release(float upDuration)
    {
        if (!_held) yield break;
        yield return AnimateTo(_restLocalPos, upDuration);
        _held = false;
    }

    private Vector3 WorldToLocalDelta(Vector3 worldDelta)
    {
        if (transform.parent == null) return worldDelta;
        return transform.parent.InverseTransformVector(worldDelta);
    }

    private IEnumerator AnimateTo(Vector3 targetLocal, float duration)
    {
        if (duration <= 0f)
        {
            transform.localPosition = targetLocal;
            yield break;
        }
        Vector3 from = transform.localPosition;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float u = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / duration));
            transform.localPosition = Vector3.LerpUnclamped(from, targetLocal, u);
            yield return null;
        }
        transform.localPosition = targetLocal;
    }
}
