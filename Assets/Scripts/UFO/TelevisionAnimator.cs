using System.Collections;
using UnityEngine;

/// <summary>
/// television オブジェクトに直接アタッチして使用するアニメーション制御スクリプト。
/// コイン投入時（UFOCameraController.OnCoinInserted）に連動して
/// 指定の初期座標・回転から目標（現在）座標・回転へスムーズにアニメーションします。
/// </summary>
public class TelevisionAnimator : MonoBehaviour
{
    [Header("アニメーション座標設定")]
    [Tooltip("出現時（スタート時）の位置")]
    [SerializeField] private Vector3 startPosition = new Vector3(6.14467525f, 6.30035591f, -15.8870001f);

    [Tooltip("出現時（スタート時）の回転 (Quaternion(x, y, z, w))")]
    [SerializeField] private Quaternion startRotation = new Quaternion(-0.911107421f, 0.0964306742f, 0.0631602257f, 0.395721138f);

    [Tooltip("アニメーション完了時（目標/現在）の位置")]
    [SerializeField] private Vector3 endPosition = new Vector3(5.75500011f, 6.30035591f, -14.1708603f);

    [Tooltip("アニメーション完了時（目標/現在）の回転 (Quaternion(x, y, z, w))")]
    [SerializeField] private Quaternion endRotation = new Quaternion(-0.676164687f, -0.218893334f, -0.613928378f, 0.343480766f);

    [Header("アニメーションパラメータ")]
    [Tooltip("アニメーション所要時間（秒）")]
    [SerializeField, Min(0.01f)] private float animationDuration = 1.0f;

    [Tooltip("アニメーションのイージングカーブ")]
    [SerializeField] private AnimationCurve easeCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Tooltip("ワールド座標を使用するか（false の場合は親基準のローカル座標）")]
    [SerializeField] private bool useWorldSpace = false;

    private Coroutine _animCoroutine;

    private void OnEnable()
    {
        UFOCameraController.OnCoinInserted += PlayAnimation;
    }

    private void OnDisable()
    {
        UFOCameraController.OnCoinInserted -= PlayAnimation;
    }

    /// <summary>
    /// アニメーションを再生します。外部や Inspector Context Menu からも実行可能。
    /// </summary>
    [ContextMenu("Test Television Animation")]
    public void PlayAnimation()
    {
        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }

        if (_animCoroutine != null)
        {
            StopCoroutine(_animCoroutine);
        }
        _animCoroutine = StartCoroutine(AnimateRoutine());
    }

    /// <summary>
    /// 現在の Transform 位置・回転を endPosition / endRotation として保存します（Inspector用便利機能）。
    /// </summary>
    [ContextMenu("Save Current Transform as End Position")]
    public void SaveCurrentTransformAsEnd()
    {
        if (useWorldSpace)
        {
            endPosition = transform.position;
            endRotation = transform.rotation;
        }
        else
        {
            endPosition = transform.localPosition;
            endRotation = transform.localRotation;
        }
        Debug.Log($"[TelevisionAnimator] endPosition/endRotation を現在のTransform ({endPosition}) に更新しました。");
    }

    private IEnumerator AnimateRoutine()
    {
        if (useWorldSpace)
        {
            transform.position = startPosition;
            transform.rotation = startRotation;
        }
        else
        {
            transform.localPosition = startPosition;
            transform.localRotation = startRotation;
        }

        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, animationDuration);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float ease = easeCurve != null ? easeCurve.Evaluate(t) : t;

            if (useWorldSpace)
            {
                transform.position = Vector3.Lerp(startPosition, endPosition, ease);
                transform.rotation = Quaternion.Slerp(startRotation, endRotation, ease);
            }
            else
            {
                transform.localPosition = Vector3.Lerp(startPosition, endPosition, ease);
                transform.localRotation = Quaternion.Slerp(startRotation, endRotation, ease);
            }

            yield return null;
        }

        if (useWorldSpace)
        {
            transform.position = endPosition;
            transform.rotation = endRotation;
        }
        else
        {
            transform.localPosition = endPosition;
            transform.localRotation = endRotation;
        }

        _animCoroutine = null;
    }
}
