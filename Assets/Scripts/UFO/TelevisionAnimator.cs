using System.Collections;
using UnityEngine;

/// <summary>
/// Scene_UFOCatcher 内の television オブジェクトに直接アタッチして使用する演出用スクリプト。
/// コイン投入時（UFOCameraController.OnCoinInserted）に
/// 出現位置・回転から目標（現在）位置・回転へ1秒間かけてスムーズにアニメーション補間します。
/// </summary>
public class TelevisionAnimator : MonoBehaviour
{
    [Header("アニメーション座標設定 (スタート / 出現座標)")]
    [Tooltip("出現時の位置")]
    [SerializeField] private Vector3 startPosition = new Vector3(6.14467525f, 6.30035591f, -15.8870001f);

    [Tooltip("出現時の回転 (Quaternion)")]
    [SerializeField] private Quaternion startRotation = new Quaternion(-0.911107421f, 0.0964306742f, 0.0631602257f, 0.395721138f);

    [Tooltip("出現時の回転 (度数表示 X, Y, Z)")]
    [SerializeField] private Vector3 startEulerAngles = new Vector3(-133.76f, 11.04f, -7.36f);

    [Header("アニメーション座標設定 (ゴール / 現在座標)")]
    [Tooltip("移動完了時（目標/現在）の位置")]
    [SerializeField] private Vector3 endPosition = new Vector3(5.75500011f, 6.30035591f, -14.1708603f);

    [Tooltip("移動完了時（目標/現在）の回転 (Quaternion)")]
    [SerializeField] private Quaternion endRotation = new Quaternion(-0.676164687f, -0.218893334f, -0.613928378f, 0.343480766f);

    [Tooltip("移動完了時（目標/現在）の回転 (度数表示 X, Y, Z)")]
    [SerializeField] private Vector3 endEulerAngles = new Vector3(-92.99f, -78.70f, -39.90f);

    [Header("アニメーション設定")]
    [Tooltip("アニメーションの所要時間（秒）")]
    [SerializeField, Min(0.01f)] private float animationDuration = 1.0f;

    [Tooltip("アニメーションのイージングカーブ")]
    [SerializeField] private AnimationCurve easeCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Tooltip("ワールド座標を使用するか（false の場合は親基準のローカル座標）")]
    [SerializeField] private bool useWorldSpace = false;

    public Quaternion StartRotation => (startRotation != Quaternion.identity && startRotation.w != 0) ? startRotation : Quaternion.Euler(startEulerAngles);
    public Quaternion EndRotation => (endRotation != Quaternion.identity && endRotation.w != 0) ? endRotation : Quaternion.Euler(endEulerAngles);

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (startRotation != Quaternion.identity && startRotation.w != 0 && startEulerAngles == Vector3.zero)
        {
            startEulerAngles = startRotation.eulerAngles;
        }
        if (endRotation != Quaternion.identity && endRotation.w != 0 && endEulerAngles == Vector3.zero)
        {
            endEulerAngles = endRotation.eulerAngles;
        }
    }
#endif

    private Coroutine _animCoroutine;

    private void OnEnable()
    {
        UFOCameraController.OnCoinInserted += PlayCoinAnimation;
    }

    private void OnDisable()
    {
        UFOCameraController.OnCoinInserted -= PlayCoinAnimation;
    }

    /// <summary>
    /// コイン投入時アニメーションを再生します。
    /// </summary>
    [ContextMenu("テスト再生: コイン投入時アニメーション")]
    public void PlayCoinAnimation()
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

    public void PlayAnimation()
    {
        PlayCoinAnimation();
    }

    /// <summary>
    /// 現在の Transform 位置・回転を『スタート（出現）座標』に保存します。
    /// </summary>
    [ContextMenu("現在の Transform を『スタート(出現)座標』として保存")]
    public void SaveCurrentTransformAsStart()
    {
        if (useWorldSpace)
        {
            startPosition = transform.position;
            startRotation = transform.rotation;
            startEulerAngles = transform.eulerAngles;
        }
        else
        {
            startPosition = transform.localPosition;
            startRotation = transform.localRotation;
            startEulerAngles = transform.localEulerAngles;
        }
        Debug.Log($"[TelevisionAnimator] スタート(出現)座標を保存しました → 位置: {startPosition}, 回転: {startEulerAngles}");
    }

    /// <summary>
    /// 現在の Transform 位置・回転を『ゴール（目標）座標』に保存します。
    /// </summary>
    [ContextMenu("現在の Transform を『ゴール(目標)座標』として保存")]
    public void SaveCurrentTransformAsEnd()
    {
        if (useWorldSpace)
        {
            endPosition = transform.position;
            endRotation = transform.rotation;
            endEulerAngles = transform.eulerAngles;
        }
        else
        {
            endPosition = transform.localPosition;
            endRotation = transform.localRotation;
            endEulerAngles = transform.localEulerAngles;
        }
        Debug.Log($"[TelevisionAnimator] ゴール(目標)座標を保存しました → 位置: {endPosition}, 回転: {endEulerAngles}");
    }

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
        float dur = Mathf.Max(0.01f, animationDuration);

        while (elapsed < dur)
        {
            if (this == null || gameObject == null || transform == null) yield break;

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / dur);
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
