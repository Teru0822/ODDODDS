using System.Collections;
using UnityEngine;

/// <summary>
/// television オブジェクトにアタッチして使用するアニメーション制御スクリプト。
/// 1. UFOキャッチャーアクセス（カメラ遷移）時: 出現座標(spawn) -> スタート座標(start) へ移動
/// 2. コイン投入時: スタート座標(start) -> ゴール座標(end) へ移動
/// </summary>
public class TelevisionAnimator : MonoBehaviour
{
    [Header("1. 出現（初期）座標設定")]
    [Tooltip("UFOキャッチャーにアクセスした瞬間（モニター出現時）の位置")]
    [SerializeField] private Vector3 spawnPosition = new Vector3(6.14467525f, 6.30035591f, -15.8870001f);

    [Tooltip("UFOキャッチャーにアクセスした瞬間（モニター出現時）の回転角度 (Transform Rotation 度数 X, Y, Z)")]
    [SerializeField] private Vector3 spawnEulerAngles = new Vector3(-133.76f, 11.04f, -7.36f);

    [Header("2. スタート座標設定（コイン投入前）")]
    [Tooltip("遷移アニメーション完了後・コイン投入前の位置")]
    [SerializeField] private Vector3 startPosition = new Vector3(5.75500011f, 6.30035591f, -14.1708603f);

    [Tooltip("遷移アニメーション完了後・コイン投入前の回転角度 (Transform Rotation 度数 X, Y, Z)")]
    [SerializeField] private Vector3 startEulerAngles = new Vector3(-92.99f, -78.70f, -39.90f);

    [Header("3. ゴール（着地）座標設定（コイン投入後）")]
    [Tooltip("コイン投入アニメーション完了後の最終位置")]
    [SerializeField] private Vector3 endPosition = new Vector3(5.75500011f, 6.30035591f, -14.1708603f);

    [Tooltip("コイン投入アニメーション完了後の最終回転角度 (Transform Rotation 度数 X, Y, Z)")]
    [SerializeField] private Vector3 endEulerAngles = new Vector3(-92.99f, -78.70f, -39.90f);

    [Header("4. アニメーション設定")]
    [Tooltip("UFOキャッチャー遷移時（出現 -> スタート）のアニメーション所要時間（秒）")]
    [SerializeField, Min(0.01f)] private float enterAnimationDuration = 1.0f;

    [Tooltip("コイン投入時（スタート -> ゴール）のアニメーション所要時間（秒）")]
    [SerializeField, Min(0.01f)] private float coinAnimationDuration = 1.0f;

    [Tooltip("アニメーションのイージングカーブ")]
    [SerializeField] private AnimationCurve easeCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Tooltip("ワールド座標を使用するか（false の場合は親基準のローカル座標）")]
    [SerializeField] private bool useWorldSpace = false;

    // クォータニオン互換プロパティ
    public Quaternion SpawnRotation => Quaternion.Euler(spawnEulerAngles);
    public Quaternion StartRotation => Quaternion.Euler(startEulerAngles);
    public Quaternion EndRotation => Quaternion.Euler(endEulerAngles);

    private Coroutine _animCoroutine;

    private void OnEnable()
    {
        UFOCameraController.OnUfoModeChanged += HandleUfoModeChanged;
        UFOCameraController.OnCoinInserted += PlayCoinAnimation;
    }

    private void OnDisable()
    {
        UFOCameraController.OnUfoModeChanged -= HandleUfoModeChanged;
        UFOCameraController.OnCoinInserted -= PlayCoinAnimation;
    }

    private void HandleUfoModeChanged(bool isUfoMode)
    {
        if (isUfoMode)
        {
            PlayEnterAnimation();
        }
        else
        {
            ResetToSpawnTransform();
        }
    }

    /// <summary>
    /// 現在の Transform 位置・回転を『1. 出現(初期)座標』に保存します。
    /// </summary>
    [ContextMenu("1. 現在の Transform を『出現(初期)座標』として保存")]
    public void SaveCurrentTransformAsSpawn()
    {
        if (useWorldSpace)
        {
            spawnPosition = transform.position;
            spawnEulerAngles = transform.eulerAngles;
        }
        else
        {
            spawnPosition = transform.localPosition;
            spawnEulerAngles = transform.localEulerAngles;
        }
        Debug.Log($"[TelevisionAnimator] 出現座標を保存しました → 位置: {spawnPosition}, 回転: {spawnEulerAngles}");
    }

    /// <summary>
    /// 現在の Transform 位置・回転を『2. スタート座標』に保存します。
    /// </summary>
    [ContextMenu("2. 現在の Transform を『スタート座標』として保存")]
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
        Debug.Log($"[TelevisionAnimator] スタート座標を保存しました → 位置: {startPosition}, 回転: {startEulerAngles}");
    }

    /// <summary>
    /// 現在の Transform 位置・回転を『3. ゴール座標』に保存します。
    /// </summary>
    [ContextMenu("3. 現在の Transform を『ゴール座標』として保存")]
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
        Debug.Log($"[TelevisionAnimator] ゴール座標を保存しました → 位置: {endPosition}, 回転: {endEulerAngles}");
    }

    /// <summary>
    /// 出現(初期)位置にプレビュー配置します。
    /// </summary>
    public void SetToSpawnTransform()
    {
        if (useWorldSpace)
        {
            transform.position = spawnPosition;
            transform.rotation = SpawnRotation;
        }
        else
        {
            transform.localPosition = spawnPosition;
            transform.localRotation = SpawnRotation;
        }
    }

    /// <summary>
    /// スタート位置にプレビュー配置します。
    /// </summary>
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
    /// ゴール位置にプレビュー配置します。
    /// </summary>
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
    /// UFOキャッチャーアクセス時（出現座標 -> スタート座標）のアニメーションを再生します。
    /// </summary>
    [ContextMenu("4. テスト再生: 進入時 (出現 -> スタート)")]
    public void PlayEnterAnimation()
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
        _animCoroutine = StartCoroutine(AnimateRoutine(spawnPosition, SpawnRotation, startPosition, StartRotation, enterAnimationDuration));
    }

    /// <summary>
    /// 後方互換用エイリアス：コイン投入時アニメーションを再生します。
    /// </summary>
    public void PlayAnimation()
    {
        PlayCoinAnimation();
    }

    /// <summary>
    /// コイン投入時（スタート座標 -> ゴール座標）のアニメーションを再生します。
    /// </summary>
    [ContextMenu("5. テスト再生: コイン時 (スタート -> ゴール)")]
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
        _animCoroutine = StartCoroutine(AnimateRoutine(startPosition, StartRotation, endPosition, EndRotation, coinAnimationDuration));
    }

    private void ResetToSpawnTransform()
    {
        if (this == null || gameObject == null || transform == null) return;
        if (_animCoroutine != null)
        {
            StopCoroutine(_animCoroutine);
            _animCoroutine = null;
        }
        SetToSpawnTransform();
    }

    private IEnumerator AnimateRoutine(Vector3 fromPos, Quaternion fromRot, Vector3 toPos, Quaternion toRot, float duration)
    {
        if (this == null || gameObject == null || transform == null) yield break;

        if (useWorldSpace)
        {
            transform.position = fromPos;
            transform.rotation = fromRot;
        }
        else
        {
            transform.localPosition = fromPos;
            transform.localRotation = fromRot;
        }

        float elapsed = 0f;
        float dur = Mathf.Max(0.01f, duration);

        while (elapsed < dur)
        {
            if (this == null || gameObject == null || transform == null) yield break;

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / dur);
            float ease = easeCurve != null ? easeCurve.Evaluate(t) : t;

            if (useWorldSpace)
            {
                transform.position = Vector3.Lerp(fromPos, toPos, ease);
                transform.rotation = Quaternion.Slerp(fromRot, toRot, ease);
            }
            else
            {
                transform.localPosition = Vector3.Lerp(fromPos, toPos, ease);
                transform.localRotation = Quaternion.Slerp(fromRot, toRot, ease);
            }

            yield return null;
        }

        if (this == null || gameObject == null || transform == null) yield break;

        if (useWorldSpace)
        {
            transform.position = toPos;
            transform.rotation = toRot;
        }
        else
        {
            transform.localPosition = toPos;
            transform.localRotation = toRot;
        }

        _animCoroutine = null;
    }
}
