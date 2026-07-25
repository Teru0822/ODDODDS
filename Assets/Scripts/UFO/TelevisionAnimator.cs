using System.Collections;
using UnityEngine;

/// <summary>
/// television オブジェクトに直接アタッチして使用するアニメーション制御スクリプト。
/// コイン投入時（UFOCameraController.OnCoinInserted）に連動して
/// 指定の初期座標・回転から目標（現在）座標・回転へスムーズにアニメーションします。
/// </summary>
public class TelevisionAnimator : MonoBehaviour
{
    [Header("1. 出現時（スタート）の座標・回転設定")]
    [Tooltip("出現時（スタート時）の位置")]
    [SerializeField] private Vector3 startPosition = new Vector3(6.14467525f, 6.30035591f, -15.8870001f);

    [Tooltip("出現時（スタート時）の回転角度（Inspector の Transform Rotation と同じ度数 X, Y, Z）")]
    [SerializeField] private Vector3 startEulerAngles = new Vector3(-133.76f, 11.04f, -7.36f);

    [Header("2. アニメーション完了（ゴール）の座標・回転設定")]
    [Tooltip("アニメーション完了時（目標/現在）の位置")]
    [SerializeField] private Vector3 endPosition = new Vector3(5.75500011f, 6.30035591f, -14.1708603f);

    [Tooltip("アニメーション完了時（目標/現在）の回転角度（Inspector の Transform Rotation と同じ度数 X, Y, Z）")]
    [SerializeField] private Vector3 endEulerAngles = new Vector3(-92.99f, -78.70f, -39.90f);

    [Header("3. アニメーション設定")]
    [Tooltip("アニメーション所要時間（秒）")]
    [SerializeField, Min(0.01f)] private float animationDuration = 1.0f;

    [Tooltip("アニメーションのイージングカーブ")]
    [SerializeField] private AnimationCurve easeCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Tooltip("ワールド座標を使用するか（false の場合は親基準のローカル座標）")]
    [SerializeField] private bool useWorldSpace = false;

    // クォータニオン互換プロパティ
    public Quaternion StartRotation => Quaternion.Euler(startEulerAngles);
    public Quaternion EndRotation => Quaternion.Euler(endEulerAngles);

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
    /// 現在の Transform 位置・回転を『スタート座標』に一括保存します（Inspector 右クリックから実行）。
    /// </summary>
    [ContextMenu("1. 現在の Transform を『スタート座標』として保存")]
    public void SaveCurrentTransformAsStart()
    {
        if (useWorldSpace)
        {
            startPosition = transform.position;
            startEulerAngles = transform.eulerAngles;
        }
        else
        {
            startPosition = transform.localPosition;
            startEulerAngles = transform.localEulerAngles;
        }
        Debug.Log($"[TelevisionAnimator] スタート座標を保存しました → 位置: {startPosition}, 回転(Euler): {startEulerAngles}");
    }

    /// <summary>
    /// 現在の Transform 位置・回転を『ゴール座標』に一括保存します（Inspector 右クリックから実行）。
    /// </summary>
    [ContextMenu("2. 現在の Transform を『ゴール座標』として保存")]
    public void SaveCurrentTransformAsEnd()
    {
        if (useWorldSpace)
        {
            endPosition = transform.position;
            endEulerAngles = transform.eulerAngles;
        }
        else
        {
            endPosition = transform.localPosition;
            endEulerAngles = transform.localEulerAngles;
        }
        Debug.Log($"[TelevisionAnimator] ゴール座標を保存しました → 位置: {endPosition}, 回転(Euler): {endEulerAngles}");
    }

    /// <summary>
    /// スタート位置に手動でプレビュー配置します（Inspector 右クリックから実行）。
    /// </summary>
    [ContextMenu("3. スタート位置にプレビュー配置")]
    public void SetToStartTransform()
    {
        if (useWorldSpace)
        {
            transform.position = startPosition;
            transform.rotation = StartRotation;
        }
        else
        {
            transform.localPosition = startPosition;
            transform.localRotation = StartRotation;
        }
    }

    /// <summary>
    /// ゴール位置に手動でプレビュー配置します（Inspector 右クリックから実行）。
    /// </summary>
    [ContextMenu("4. ゴール位置にプレビュー配置")]
    public void SetToEndTransform()
    {
        if (useWorldSpace)
        {
            transform.position = endPosition;
            transform.rotation = EndRotation;
        }
        else
        {
            transform.localPosition = endPosition;
            transform.localRotation = EndRotation;
        }
    }

    /// <summary>
    /// アニメーションを再生します。外部や Inspector 右クリックメニューから実行可能。
    /// </summary>
    [ContextMenu("5. アニメーションをテスト再生")]
    public void PlayAnimation()
    {
        if (this == null || gameObject == null) return;

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

    private IEnumerator AnimateRoutine()
    {
        if (this == null || gameObject == null || transform == null) yield break;

        Quaternion startRot = StartRotation;
        Quaternion endRot = EndRotation;

        if (useWorldSpace)
        {
            transform.position = startPosition;
            transform.rotation = startRot;
        }
        else
        {
            transform.localPosition = startPosition;
            transform.localRotation = startRot;
        }

        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, animationDuration);

        while (elapsed < duration)
        {
            if (this == null || gameObject == null || transform == null) yield break;

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float ease = easeCurve != null ? easeCurve.Evaluate(t) : t;

            if (useWorldSpace)
            {
                transform.position = Vector3.Lerp(startPosition, endPosition, ease);
                transform.rotation = Quaternion.Slerp(startRot, endRot, ease);
            }
            else
            {
                transform.localPosition = Vector3.Lerp(startPosition, endPosition, ease);
                transform.localRotation = Quaternion.Slerp(startRot, endRot, ease);
            }

            yield return null;
        }

        if (this == null || gameObject == null || transform == null) yield break;

        if (useWorldSpace)
        {
            transform.position = endPosition;
            transform.rotation = endRot;
        }
        else
        {
            transform.localPosition = endPosition;
            transform.localRotation = endRot;
        }

        _animCoroutine = null;
    }
}
