using UnityEngine;

/// <summary>
/// 画像UI（Image等、RectTransformを持つUI要素）をハッキングされたような見た目で揺らす・
/// チラつかせる演出。TextTrembleEffect（文字の頂点をブルブル震わせる）と同じPerlinノイズの
/// 考え方を、RectTransform全体の位置・回転に適用したもの。
/// 加えて、不規則な間隔で表示/非表示を切り替える「ノイズドロップアウト」も任意でON/OFFできる。
/// 震え・チラつきはそれぞれ個別にON/OFFできるので、震えだけ・チラつきだけの用途にも使える。
/// </summary>
public class ImageTrembleEffect : MonoBehaviour
{
    [Header("震え（トレンブル）")]
    [Tooltip("震えを有効にするか")]
    [SerializeField] private bool enableTremble = true;

    [Tooltip("震えの最大角度（度）")]
    [SerializeField] private float angle = 3f;

    [Tooltip("震えの最大位置ズレ（RectTransformのローカル単位＝ピクセル相当）")]
    [SerializeField] private float positionAmount = 4f;

    [Tooltip("震える速さ。大きいほど小刻みにブルブル震える")]
    [SerializeField] private float speed = 18f;

    [Header("表示/非表示のチラつき（ハッキング風ドロップアウト）")]
    [Tooltip("不規則な表示/非表示の点滅を有効にするか")]
    [SerializeField] private bool enableFlicker = false;

    [Tooltip("表示されている状態が続く時間の範囲（秒）")]
    [SerializeField] private Vector2 visibleDurationRange = new Vector2(0.3f, 1.2f);

    [Tooltip("非表示になっている時間の範囲（秒）")]
    [SerializeField] private Vector2 hiddenDurationRange = new Vector2(0.03f, 0.15f);

    [Tooltip("非表示になる際のアルファ値（0で完全に消える。0より大きくすると薄く残る）")]
    [SerializeField, Range(0f, 1f)] private float hiddenAlpha = 0f;

    [Header("親の揺れの影響を受けない設定")]
    [Tooltip("揺れている親（Panel等、こちらもImageTrembleEffectで揺れている場合）を指定すると、" +
             "その親が今のフレームでどれだけ動いたかをローカル座標のまま差し引いてから自分の揺れを" +
             "適用するため、親の動きに引きずられず「自分自身の揺れだけ」を保てる。" +
             "未設定なら通常通り（親の動きもそのまま伝わる）")]
    [SerializeField] private ImageTrembleEffect parentTremble;

    /// <summary>このフレームで自分がどれだけ揺れたか（子がこの親の動きを打ち消すために参照する）</summary>
    public Vector2 CurrentPositionDelta { get; private set; }

    /// <summary>このフレームで自分がどれだけ回転したか（度）</summary>
    public float CurrentRotationDeltaDegrees { get; private set; }

    private RectTransform _rect;
    private CanvasGroup _canvasGroup;
    private Vector2 _originalAnchoredPosition;
    private Quaternion _originalRotation;
    private float _noiseSeedX;
    private float _noiseSeedY;
    private float _noiseSeedR;
    private float _flickerTimer;
    private bool _isVisible = true;

    private void Awake()
    {
        _rect = GetComponent<RectTransform>();
        _originalAnchoredPosition = _rect.anchoredPosition;
        _originalRotation = _rect.localRotation;

        // 複数の画像に同時にアタッチしても全部同じ動きにならないよう、個体ごとにノイズの
        // サンプル開始位置をランダムにずらす
        _noiseSeedX = Random.Range(0f, 1000f);
        _noiseSeedY = Random.Range(0f, 1000f);
        _noiseSeedR = Random.Range(0f, 1000f);

        if (enableFlicker)
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null) _canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
    }

    private void OnEnable()
    {
        _isVisible = true;
        _flickerTimer = Random.Range(visibleDurationRange.x, visibleDurationRange.y);
        if (_canvasGroup != null) _canvasGroup.alpha = 1f;
    }

    private void OnDisable()
    {
        // 無効化時は元の状態にきちんと戻す（揺れたまま・消えたままにしない）
        if (_rect != null)
        {
            _rect.anchoredPosition = _originalAnchoredPosition;
            _rect.localRotation = _originalRotation;
        }
        if (_canvasGroup != null) _canvasGroup.alpha = 1f;
        CurrentPositionDelta = Vector2.zero;
        CurrentRotationDeltaDegrees = 0f;
    }

    private void Update()
    {
        if (parentTremble != null) return; // 親打ち消しがある場合はLateUpdateで処理する

        if (enableTremble) AnimateTremble();
        if (enableFlicker) AnimateFlicker();
    }

    private void LateUpdate()
    {
        if (parentTremble == null) return;

        // 親（Panel等）のUpdate()はこのLateUpdate()より必ず先に終わっている（Unityの実行順の保証）。
        // 親の「このフレームの揺れ量」をローカル座標のまま差し引くことで、親の動きだけを打ち消し、
        // 自分自身の揺れ（enableTremble）だけをそのまま残す。ワールド座標には一切触れないため、
        // Canvasのスケーリングやレイアウトのタイミングに影響されない。
        AnimateTrembleCancelingParent();
        if (enableFlicker) AnimateFlicker();
    }

    private void AnimateTremble()
    {
        float noiseTime = Time.time * speed;

        float offsetX = (Mathf.PerlinNoise(noiseTime + _noiseSeedX, 0.37f) - 0.5f) * 2f * positionAmount;
        float offsetY = (Mathf.PerlinNoise(noiseTime + _noiseSeedY, 5.21f) - 0.5f) * 2f * positionAmount;
        float rot = (Mathf.PerlinNoise(noiseTime + _noiseSeedR, 9.73f) - 0.5f) * 2f * angle;

        CurrentPositionDelta = new Vector2(offsetX, offsetY);
        CurrentRotationDeltaDegrees = rot;

        _rect.anchoredPosition = _originalAnchoredPosition + CurrentPositionDelta;
        _rect.localRotation = _originalRotation * Quaternion.Euler(0f, 0f, rot);
    }

    private void AnimateTrembleCancelingParent()
    {
        Vector2 ownOffset = Vector2.zero;
        float ownRot = 0f;

        if (enableTremble)
        {
            float noiseTime = Time.time * speed;
            ownOffset = new Vector2(
                (Mathf.PerlinNoise(noiseTime + _noiseSeedX, 0.37f) - 0.5f) * 2f * positionAmount,
                (Mathf.PerlinNoise(noiseTime + _noiseSeedY, 5.21f) - 0.5f) * 2f * positionAmount);
            ownRot = (Mathf.PerlinNoise(noiseTime + _noiseSeedR, 9.73f) - 0.5f) * 2f * angle;
        }

        CurrentPositionDelta = ownOffset;
        CurrentRotationDeltaDegrees = ownRot;

        _rect.anchoredPosition = _originalAnchoredPosition - parentTremble.CurrentPositionDelta + ownOffset;
        _rect.localRotation = _originalRotation * Quaternion.Euler(0f, 0f, ownRot - parentTremble.CurrentRotationDeltaDegrees);
    }

    private void AnimateFlicker()
    {
        _flickerTimer -= Time.deltaTime;
        if (_flickerTimer > 0f) return;

        _isVisible = !_isVisible;
        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = _isVisible ? 1f : hiddenAlpha;
        }

        _flickerTimer = _isVisible
            ? Random.Range(visibleDurationRange.x, visibleDurationRange.y)
            : Random.Range(hiddenDurationRange.x, hiddenDurationRange.y);
    }
}
