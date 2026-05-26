using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// 「魔法のタイプライター」用の紙ギミック。
/// BeginNewPaper() で paperPrefab を spawnPoint に生成し、AppendChar() で打鍵に同期して TMP_Text に文字追加。
/// 紙はタイプライター横で上下左右にふわふわ浮遊する。
/// EndPaper() で「ぐるぐる回って加速 → 下にためる → ワールド +Y へぶっ飛ぶ」3 段ローンチ演出を実行。
/// </summary>
[DisallowMultipleComponent]
public class TypewriterPaperOutput : MonoBehaviour
{
    [Header("Prefab / Spawn")]
    [Tooltip("生成する紙 prefab。子に TMP_Text (3D or UGUI) を必ず 1 個含めること")]
    public GameObject paperPrefab;

    [Tooltip("紙の出現位置 Transform (タイプライター横の浮遊位置)。null なら自身の transform")]
    public Transform spawnPoint;

    [Header("浮遊 (vertical)")]
    [Tooltip("浮遊運動を有効化")]
    public bool floatMotion = true;

    [Tooltip("上下浮遊の振幅 (m)")]
    public float bobAmplitudeV = 0.015f;

    [Tooltip("上下浮遊の周期 (秒)")]
    public float bobPeriodV = 2.5f;

    [Tooltip("上下浮遊の軸 (spawnPoint ローカル空間)")]
    public Vector3 bobAxisV = Vector3.up;

    [Header("浮遊 (horizontal)")]
    [Tooltip("左右浮遊の振幅 (m)")]
    public float bobAmplitudeH = 0.012f;

    [Tooltip("左右浮遊の周期 (秒)。上下と異なる値にすると Lissajous 風になる")]
    public float bobPeriodH = 3.2f;

    [Tooltip("左右浮遊の軸 (spawnPoint ローカル空間)")]
    public Vector3 bobAxisH = Vector3.right;

    [Header("回転揺れ")]
    [Tooltip("回転揺れの振幅 (度)。0 で回転無し")]
    public float rotAmplitude = 3.0f;

    [Tooltip("回転揺れの周期 (秒)")]
    public float rotPeriod = 3.5f;

    [Tooltip("回転揺れの軸 (spawnPoint ローカル空間)")]
    public Vector3 rotAxis = Vector3.forward;

    [Header("打鍵完了時のローンチ演出")]
    [Tooltip("EndPaper でローンチ演出を実行")]
    public bool enableLaunch = true;

    [Tooltip("回転加速の軸 (ワールド空間)。デフォルトはワールド +Y")]
    public Vector3 launchSpinAxis = Vector3.up;

    [Tooltip("回転加速 開始時の角速度 (度/秒)")]
    public float launchSpinStartSpeed = 180f;

    [Tooltip("回転加速 終了時の角速度 (度/秒)")]
    public float launchSpinEndSpeed = 2400f;

    [Tooltip("回転加速フェーズの長さ (秒)")]
    public float launchSpinDuration = 1.2f;

    [Tooltip("ためフェーズで下に沈む距離 (m)。ワールド -Y 方向")]
    public float launchDipDistance = 0.08f;

    [Tooltip("ためフェーズの長さ (秒)")]
    public float launchDipDuration = 0.25f;

    [Tooltip("発射時の初速 (m/s, ワールド +Y)")]
    public float launchUpInitialSpeed = 2f;

    [Tooltip("発射中の加速度 (m/s², ワールド +Y)")]
    public float launchUpAccel = 500f;

    [Tooltip("発射フェーズの長さ (秒)。終了後に紙を破棄")]
    public float launchUpDuration = 1.2f;

    private GameObject _currentPaper;
    private TMP_Text _currentText;
    private Vector3 _baseLocalPos;
    private Quaternion _baseLocalRot;
    private float _bobPhase;

    public bool HasActivePaper => _currentPaper != null;

    public void BeginNewPaper()
    {
        if (paperPrefab == null)
        {
            Debug.LogWarning("[TypewriterPaperOutput] paperPrefab が未設定。スキップ", this);
            return;
        }
        if (_currentPaper != null) Destroy(_currentPaper);

        var sp = spawnPoint != null ? spawnPoint : transform;
        _currentPaper = Instantiate(paperPrefab, sp.position, sp.rotation, sp);
        _currentPaper.transform.localPosition = Vector3.zero;
        _currentPaper.transform.localRotation = Quaternion.identity;
        _baseLocalPos = _currentPaper.transform.localPosition;
        _baseLocalRot = _currentPaper.transform.localRotation;
        _bobPhase = Random.Range(0f, Mathf.PI * 2f);

        _currentText = _currentPaper.GetComponentInChildren<TMP_Text>(true);
        if (_currentText == null)
        {
            Debug.LogWarning($"[TypewriterPaperOutput] paperPrefab '{paperPrefab.name}' に TMP_Text が見つかりません", _currentPaper);
        }
        else
        {
            _currentText.text = "";
            Debug.Log($"[TypewriterPaperOutput] BeginNewPaper: TMP_Text='{_currentText.name}' ({_currentText.GetType().Name})", _currentPaper);
        }
    }

    public void AppendChar(char c)
    {
        if (_currentText == null)
        {
            Debug.LogWarning($"[TypewriterPaperOutput] AppendChar('{c}') 無視: TMP_Text 未取得", this);
            return;
        }
        _currentText.text += c;
    }

    /// <summary>打鍵完了。enableLaunch=true ならローンチ演出を開始する (紙はその後破棄される)。</summary>
    public void EndPaper()
    {
        if (_currentPaper == null) return;
        if (!enableLaunch) return;

        // 浮遊 Update から手放してローンチ専用に
        var paper = _currentPaper;
        _currentPaper = null;
        _currentText = null;
        // spawnPoint から切り離してワールド空間で動かす
        paper.transform.SetParent(null, true);
        StartCoroutine(LaunchSequence(paper));
    }

    private IEnumerator LaunchSequence(GameObject paper)
    {
        if (paper == null) yield break;
        Transform tr = paper.transform;

        // Phase 1: ぐるぐる回って加速
        Vector3 spinAxis = launchSpinAxis.sqrMagnitude > 0f ? launchSpinAxis.normalized : Vector3.up;
        float spinElapsed = 0f;
        while (spinElapsed < launchSpinDuration && paper != null)
        {
            spinElapsed += Time.deltaTime;
            float u = Mathf.Clamp01(spinElapsed / Mathf.Max(0.001f, launchSpinDuration));
            float speed = Mathf.Lerp(launchSpinStartSpeed, launchSpinEndSpeed, u * u); // 二次で加速感を強調
            tr.Rotate(spinAxis, speed * Time.deltaTime, Space.World);
            yield return null;
        }

        // Phase 2: 下にずらして力をためる (回転は最高速で継続)
        Vector3 dipStart = tr.position;
        Vector3 dipEnd = dipStart + Vector3.down * launchDipDistance;
        float dipElapsed = 0f;
        while (dipElapsed < launchDipDuration && paper != null)
        {
            dipElapsed += Time.deltaTime;
            float u = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(dipElapsed / Mathf.Max(0.001f, launchDipDuration)));
            tr.position = Vector3.LerpUnclamped(dipStart, dipEnd, u);
            tr.Rotate(spinAxis, launchSpinEndSpeed * Time.deltaTime, Space.World);
            yield return null;
        }

        // Phase 3: ワールド +Y へぶっ飛ぶ (加速しながら)
        float launchElapsed = 0f;
        float currentSpeed = launchUpInitialSpeed;
        while (launchElapsed < launchUpDuration && paper != null)
        {
            launchElapsed += Time.deltaTime;
            currentSpeed += launchUpAccel * Time.deltaTime;
            tr.position += Vector3.up * (currentSpeed * Time.deltaTime);
            tr.Rotate(spinAxis, launchSpinEndSpeed * Time.deltaTime, Space.World);
            yield return null;
        }

        if (paper != null) Destroy(paper);
    }

    private void Update()
    {
        if (!floatMotion || _currentPaper == null) return;

        float now = Time.time;
        float angleV = (now / Mathf.Max(0.001f, bobPeriodV)) * Mathf.PI * 2f + _bobPhase;
        float angleH = (now / Mathf.Max(0.001f, bobPeriodH)) * Mathf.PI * 2f + _bobPhase + Mathf.PI * 0.5f;

        Vector3 axisV = bobAxisV.sqrMagnitude > 0f ? bobAxisV.normalized : Vector3.up;
        Vector3 axisH = bobAxisH.sqrMagnitude > 0f ? bobAxisH.normalized : Vector3.right;
        Vector3 offset = axisV * (Mathf.Sin(angleV) * bobAmplitudeV)
                       + axisH * (Mathf.Sin(angleH) * bobAmplitudeH);
        _currentPaper.transform.localPosition = _baseLocalPos + offset;

        if (rotAmplitude > 0f)
        {
            float angleR = (now / Mathf.Max(0.001f, rotPeriod)) * Mathf.PI * 2f + _bobPhase;
            float deg = Mathf.Sin(angleR) * rotAmplitude;
            Vector3 rAxis = rotAxis.sqrMagnitude > 0f ? rotAxis.normalized : Vector3.forward;
            _currentPaper.transform.localRotation = _baseLocalRot * Quaternion.AngleAxis(deg, rAxis);
        }
    }
}
