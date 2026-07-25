using System.Collections;
using UnityEngine;

/// <summary>
/// Scene_UFOCatcher 内のテレビ（television）オブジェクトにアタッチして使用する演出用スクリプト。
/// 1. UFOキャッチャークリック時: 出現座標(spawn)へ即座にワープし、そこからスタート座標(start)へアニメーション移動
/// 2. 全コイン投入完了時: スタート座標(start)からゴール座標(end)へアニメーション移動
/// </summary>
public class TelevisionAnimator : MonoBehaviour
{
    [Header("1. 出現（ワープ先）座標設定")]
    [Tooltip("UFOキャッチャーアクセス時（モニター出現時）に即座にワープする位置")]
    [SerializeField] private Vector3 spawnPosition = new Vector3(6.14467525f, 6.30035591f, -15.8870001f);

    [Tooltip("UFOキャッチャーアクセス時（モニター出現時）に即座にワープする回転 (Quaternion)")]
    [SerializeField] private Quaternion spawnRotation = new Quaternion(-0.911107421f, 0.0964306742f, 0.0631602257f, 0.395721138f);

    [Tooltip("UFOキャッチャーアクセス時（モニター出現時）に即座にワープする回転 (度数表示 X, Y, Z)")]
    [SerializeField] private Vector3 spawnEulerAngles = new Vector3(-133.76f, 11.04f, -7.36f);

    [Header("2. スタート座標設定（コイン投入前）")]
    [Tooltip("出現座標から移動し終えるアクセス完了位置（コイン投入前の待機位置）")]
    [SerializeField] private Vector3 startPosition = new Vector3(5.75500011f, 6.30035591f, -14.1708603f);

    [Tooltip("出現座標から移動し終えるアクセス完了回転 (Quaternion)")]
    [SerializeField] private Quaternion startRotation = new Quaternion(-0.676164687f, -0.218893334f, -0.613928378f, 0.343480766f);

    [Tooltip("出現座標から移動し終えるアクセス完了回転 (度数表示 X, Y, Z)")]
    [SerializeField] private Vector3 startEulerAngles = new Vector3(-92.99f, -78.70f, -39.90f);

    [Header("3. ゴール（目標）座標設定（全コイン投入後）")]
    [Tooltip("全コイン投入完了アニメーション後の最終位置")]
    [SerializeField] private Vector3 endPosition = new Vector3(5.75500011f, 6.30035591f, -14.1708603f);

    [Tooltip("全コイン投入完了アニメーション後の最終回転 (Quaternion)")]
    [SerializeField] private Quaternion endRotation = new Quaternion(-0.676164687f, -0.218893334f, -0.613928378f, 0.343480766f);

    [Tooltip("全コイン投入完了アニメーション後の最終回転 (度数表示 X, Y, Z)")]
    [SerializeField] private Vector3 endEulerAngles = new Vector3(-92.99f, -78.70f, -39.90f);

    [Header("4. アニメーション設定")]
    [Tooltip("UFOキャッチャーアクセス時（出現 -> スタート）の所要時間（秒）")]
    [SerializeField, Min(0.01f)] private float enterAnimationDuration = 1.0f;

    [Tooltip("全コイン投入完了時（スタート -> ゴール）の所要時間（秒）")]
    [SerializeField, Min(0.01f)] private float coinAnimationDuration = 1.0f;

    [Tooltip("アニメーションのイージングカーブ")]
    [SerializeField] private AnimationCurve easeCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Tooltip("ワールド座標を使用するか（false の場合は親基準のローカル座標）")]
    [SerializeField] private bool useWorldSpace = false;

    public Quaternion SpawnRotation => (spawnRotation != Quaternion.identity && spawnRotation.w != 0) ? spawnRotation : Quaternion.Euler(spawnEulerAngles);
    public Quaternion StartRotation => (startRotation != Quaternion.identity && startRotation.w != 0) ? startRotation : Quaternion.Euler(startEulerAngles);
    public Quaternion EndRotation => (endRotation != Quaternion.identity && endRotation.w != 0) ? endRotation : Quaternion.Euler(endEulerAngles);

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (spawnRotation != Quaternion.identity && spawnRotation.w != 0 && spawnEulerAngles == Vector3.zero)
            spawnEulerAngles = spawnRotation.eulerAngles;
        if (startRotation != Quaternion.identity && startRotation.w != 0 && startEulerAngles == Vector3.zero)
            startEulerAngles = startRotation.eulerAngles;
        if (endRotation != Quaternion.identity && endRotation.w != 0 && endEulerAngles == Vector3.zero)
            endEulerAngles = endRotation.eulerAngles;
    }
#endif

    private Coroutine _animCoroutine;

    private void OnEnable()
    {
        UFOCameraController.OnUfoModeChanged += HandleUfoModeChanged;
        UFOCameraController.OnAllCoinsInserted += PlayCoinAnimation;
    }

    private void OnDisable()
    {
        UFOCameraController.OnUfoModeChanged -= HandleUfoModeChanged;
        UFOCameraController.OnAllCoinsInserted -= PlayCoinAnimation;
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
    /// 現在の Transform 位置・回転を『1. 出現(ワープ先)座標』に保存します。
    /// </summary>
    [ContextMenu("1. 現在の Transform を『出現(ワープ先)座標』として保存")]
    public void SaveCurrentTransformAsSpawn()
    {
        if (useWorldSpace)
        {
            spawnPosition = transform.position;
            spawnRotation = transform.rotation;
            spawnEulerAngles = transform.eulerAngles;
        }
        else
        {
            spawnPosition = transform.localPosition;
            spawnRotation = transform.localRotation;
            spawnEulerAngles = transform.localEulerAngles;
        }
        Debug.Log($"[TelevisionAnimator] 出現(ワープ先)座標を保存しました → 位置: {spawnPosition}, 回転: {spawnEulerAngles}");
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
            startRotation = transform.rotation;
            startEulerAngles = transform.eulerAngles;
        }
        else
        {
            startPosition = transform.localPosition;
            startRotation = transform.localRotation;
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
            endRotation = transform.rotation;
            endEulerAngles = transform.eulerAngles;
        }
        else
        {
            endPosition = transform.localPosition;
            endRotation = transform.localRotation;
            endEulerAngles = transform.localEulerAngles;
        }
        Debug.Log($"[TelevisionAnimator] ゴール座標を保存しました → 位置: {endPosition}, 回転: {endEulerAngles}");
    }

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

    /// <summary>
    /// 1段階目：UFOキャッチャーアクセス時
    /// ① 出現座標へ即座にワープ (0秒)
    /// ② 出現座標からスタート座標へアニメーション移動 (1秒)
    /// </summary>
    [ContextMenu("4. テスト再生: アクセス時 (出現へワープ -> スタートへ移動)")]
    public void PlayEnterAnimation()
    {
        if (this == null || gameObject == null) return;

        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }

        // ① 出現座標へ即座にワープ
        SetToSpawnTransform();

        // ② 出現座標からスタート座標へスライド移動
        if (_animCoroutine != null)
        {
            StopCoroutine(_animCoroutine);
        }
        _animCoroutine = StartCoroutine(AnimateRoutine(spawnPosition, SpawnRotation, startPosition, StartRotation, enterAnimationDuration));
    }

    /// <summary>
    /// 2段階目：全コイン投入完了時（スタート座標 -> ゴール座標）のアニメーションを再生します。
    /// </summary>
    [ContextMenu("5. テスト再生: 全コイン投入後 (スタート -> ゴール)")]
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

    public void PlayAnimation()
    {
        PlayCoinAnimation();
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
