using System.Collections;
using UnityEngine;

/// <summary>
/// ルーレット「目」用スワッパー。
///
/// UFO キャッチャーのゲーム開始（IsPlayingUfo が true になった瞬間）に
/// BlendShape で目を閉じ、ピーク（完全閉目）で blackSphere と rouletteReel の
/// アクティブを入れ替え、再び目を開ける。
///
/// 設定方法:
///   1. ルーレット Prefab の任意の GameObject にアタッチ
///   2. Inspector で eyeSkinnedMesh / blackSphere / rouletteReel を設定
///   3. shapeKeyName または blendShapeIndex で対象 BlendShape を指定
/// </summary>
public class RouletteEyeSwapper : MonoBehaviour
{
    [Header("目")]
    [Tooltip("瞼の BlendShape を持つ SkinnedMeshRenderer。null なら子から自動取得")]
    [SerializeField] private SkinnedMeshRenderer eyeSkinnedMesh;

    [Tooltip("使用する Blend Shape 名。空の場合は blendShapeIndex を使う")]
    [SerializeField] private string shapeKeyName = "";

    [Tooltip("shapeKeyName が空 / 見つからない時のインデックス")]
    [SerializeField] private int blendShapeIndex = 0;

    [Tooltip("BlendShape の最大値（Unity の SkinnedMesh は 0〜100）")]
    [SerializeField] private float shapeKeyMaxValue = 100f;

    [Header("ライト")]
    [Tooltip("目の光として使うライト。瞬き中に明るさがフェードアウト/インする")]
    [SerializeField] private Light eyeLight;

    [Header("スワップ対象")]
    [Tooltip("初期状態でアクティブになっている黒い球体")]
    [SerializeField] private GameObject blackSphere;

    [Tooltip("初期状態で非アクティブになっているルーレットのリール")]
    [SerializeField] private GameObject rouletteReel;

    [Header("アニメーション時間")]
    [Tooltip("目を閉じるまでの秒数")]
    [SerializeField, Min(0.05f)] private float closeDuration = 0.3f;

    [Tooltip("目を開くまでの秒数")]
    [SerializeField, Min(0.05f)] private float openDuration = 0.4f;

    // -----------------------------------------------------------------------

    private int   _resolvedIndex = -1;
    private bool  _lastIsPlayingUfo;
    private bool  _isSwapping;
    private float _initialLightIntensity;

    // -----------------------------------------------------------------------

    private void Awake()
    {
        if (eyeSkinnedMesh == null)
            eyeSkinnedMesh = GetComponentInChildren<SkinnedMeshRenderer>(true);

        if (eyeLight != null)
            _initialLightIntensity = eyeLight.intensity;

        ResolveShapeIndex();
    }

    private void Start()
    {
        _lastIsPlayingUfo = UFOCameraController.IsPlayingUfo;
    }

    private void Update()
    {
        bool isPlayingUfo = UFOCameraController.IsPlayingUfo;

        // UFO 開始・終了どちらのエッジでもスワップを発動
        if (_lastIsPlayingUfo != isPlayingUfo && !_isSwapping)
            StartCoroutine(BlinkAndSwap());

        _lastIsPlayingUfo = isPlayingUfo;
    }

    // -----------------------------------------------------------------------
    // 外部から直接呼び出し可能
    // -----------------------------------------------------------------------

    /// <summary>即座に瞬きスワップを実行する（Button イベント等からも使用可）。</summary>
    public void TriggerSwap()
    {
        if (!_isSwapping)
            StartCoroutine(BlinkAndSwap());
    }

    // -----------------------------------------------------------------------

    private IEnumerator BlinkAndSwap()
    {
        _isSwapping = true;

        // 目を閉じる + ライトフェードアウト（同一コルーチンで同期）
        float currentLightIntensity = eyeLight != null ? eyeLight.intensity : _initialLightIntensity;
        yield return AnimateWeightAndLight(GetCurrentWeight(), shapeKeyMaxValue, currentLightIntensity, 0f, closeDuration);

        // ピーク：オブジェクトをスワップ
        if (blackSphere  != null) blackSphere.SetActive(!blackSphere.activeSelf);
        if (rouletteReel != null) rouletteReel.SetActive(!rouletteReel.activeSelf);

        // ルーレット表示中はライトを消灯したまま、黒球表示中は点灯
        bool isRouletteActive = rouletteReel != null && rouletteReel.activeSelf;
        float lightTarget = isRouletteActive ? 0f : _initialLightIntensity;

        // 目を開ける + ライト制御（同一コルーチンで同期）
        yield return AnimateWeightAndLight(shapeKeyMaxValue, 0f, 0f, lightTarget, openDuration);

        SetWeight(0f);
        if (eyeLight != null) eyeLight.intensity = lightTarget;
        _isSwapping = false;
    }

    private IEnumerator AnimateWeightAndLight(float weightFrom, float weightTo, float lightFrom, float lightTo, float duration)
    {
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(0.0001f, duration);
            float clamped = Mathf.Clamp01(t);
            SetWeight(Mathf.Lerp(weightFrom, weightTo, clamped));
            if (eyeLight != null)
                eyeLight.intensity = Mathf.Lerp(lightFrom, lightTo, clamped);
            yield return null;
        }
    }

    private IEnumerator AnimateWeight(float from, float to, float duration)
    {
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(0.0001f, duration);
            SetWeight(Mathf.Lerp(from, to, Mathf.Clamp01(t)));
            yield return null;
        }
    }

    private void SetWeight(float value)
    {
        if (eyeSkinnedMesh != null && _resolvedIndex >= 0)
            eyeSkinnedMesh.SetBlendShapeWeight(_resolvedIndex, value);
    }

    private float GetCurrentWeight()
    {
        if (eyeSkinnedMesh != null && _resolvedIndex >= 0)
            return eyeSkinnedMesh.GetBlendShapeWeight(_resolvedIndex);
        return 0f;
    }

    private void ResolveShapeIndex()
    {
        _resolvedIndex = blendShapeIndex;
        if (eyeSkinnedMesh == null || eyeSkinnedMesh.sharedMesh == null) return;
        if (string.IsNullOrEmpty(shapeKeyName)) return;

        int idx = eyeSkinnedMesh.sharedMesh.GetBlendShapeIndex(shapeKeyName);
        if (idx >= 0)
            _resolvedIndex = idx;
        else
            Debug.LogWarning($"[RouletteEyeSwapper] Shape key '{shapeKeyName}' が見つかりません。blendShapeIndex={blendShapeIndex} を使用", this);
    }
}
