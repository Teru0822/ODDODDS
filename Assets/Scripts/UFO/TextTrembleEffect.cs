using TMPro;
using UnityEngine;

/// <summary>
/// TMP_Textの文字を1文字ずつ、その文字自身の中心を軸にブルブルと震わせる（パーリンノイズによる
/// 不規則な回転＋位置ジッター）。TMP_TextがついているGameObjectに直接アタッチして使う。
/// TMP_TextのメッシュをForceMeshUpdate()で毎フレーム素の状態に戻してから頂点をずらして書き戻す、
/// TextMeshProの公式サンプル（VertexJitter等）と同じ手法。
/// </summary>
[RequireComponent(typeof(TMP_Text))]
public class TextTrembleEffect : MonoBehaviour
{
    [Tooltip("1文字ごとの震えの最大角度（度）")]
    [SerializeField] private float angle = 3f;

    [Tooltip("1文字ごとの震えの最大位置ズレ（TextMeshProのローカル単位）")]
    [SerializeField] private float positionAmount = 2f;

    [Tooltip("震える速さ。大きいほど小刻みにブルブル震える")]
    [SerializeField] private float speed = 18f;

    [Tooltip("隣の文字とどれだけ震えのタイミングをずらすか（バラバラ感を出す）")]
    [SerializeField] private float characterOffset = 4f;

    [Header("親の揺れの影響を受けない設定")]
    [Tooltip("揺れている親（Panel等、ImageTrembleEffectで揺れている場合）を指定すると、その親が" +
             "今のフレームでどれだけ動いたかをローカル座標のまま差し引いて、テキストブロック自体は" +
             "親の動きに引きずられず本来の位置に留まる（文字ごとの震えは従来通りそのまま効く）")]
    [SerializeField] private ImageTrembleEffect parentTremble;

    private TMP_Text _text;
    private RectTransform _rect;
    private Vector2 _originalAnchoredPosition;
    private Quaternion _originalRotation;

    private void Awake()
    {
        _text = GetComponent<TMP_Text>();
        _rect = GetComponent<RectTransform>();
        if (_rect != null)
        {
            _originalAnchoredPosition = _rect.anchoredPosition;
            _originalRotation = _rect.localRotation;
        }
    }

    private void Update()
    {
        AnimateTremble();
    }

    private void LateUpdate()
    {
        if (parentTremble == null || _rect == null) return;

        // 親（Panel等）のUpdate()はこのLateUpdate()より必ず先に終わっている（Unityの実行順の保証）。
        // 親の「このフレームの揺れ量」をローカル座標のまま差し引くことで、テキストブロック自体は
        // 親の動きに引きずられず本来の位置に留まる。文字ごとの震え（メッシュ頂点）はテキストの
        // ローカル空間で完結しているため影響を受けない。ワールド座標には一切触れない。
        _rect.anchoredPosition = _originalAnchoredPosition - parentTremble.CurrentPositionDelta;
        _rect.localRotation = _originalRotation * Quaternion.Euler(0f, 0f, -parentTremble.CurrentRotationDeltaDegrees);
    }

    private void AnimateTremble()
    {
        if (_text == null || string.IsNullOrEmpty(_text.text)) return;

        _text.ForceMeshUpdate();
        TMP_TextInfo textInfo = _text.textInfo;
        int characterCount = textInfo.characterCount;
        if (characterCount == 0) return;

        float noiseTime = Time.time * speed;

        for (int i = 0; i < characterCount; i++)
        {
            TMP_CharacterInfo charInfo = textInfo.characterInfo[i];
            if (!charInfo.isVisible) continue;

            int materialIndex = charInfo.materialReferenceIndex;
            int vertexIndex = charInfo.vertexIndex;
            Vector3[] vertices = textInfo.meshInfo[materialIndex].vertices;

            Vector3 charMidBaseline = (vertices[vertexIndex + 0] + vertices[vertexIndex + 2]) * 0.5f;

            // 文字ごとにノイズのサンプル位置をずらすことで、全文字がバラバラに震えているように見せる
            float charSeed = i * characterOffset;
            float a = (Mathf.PerlinNoise(noiseTime + charSeed, 0.37f) - 0.5f) * 2f * angle;
            float offsetX = (Mathf.PerlinNoise(noiseTime + charSeed, 5.21f) - 0.5f) * 2f * positionAmount;
            float offsetY = (Mathf.PerlinNoise(noiseTime + charSeed, 9.73f) - 0.5f) * 2f * positionAmount;

            Quaternion rotation = Quaternion.Euler(0f, 0f, a);
            Vector3 jitter = new Vector3(offsetX, offsetY, 0f);

            for (int v = 0; v < 4; v++)
            {
                vertices[vertexIndex + v] = rotation * (vertices[vertexIndex + v] - charMidBaseline) + charMidBaseline + jitter;
            }
        }

        for (int m = 0; m < textInfo.meshInfo.Length; m++)
        {
            textInfo.meshInfo[m].mesh.vertices = textInfo.meshInfo[m].vertices;
            _text.UpdateGeometry(textInfo.meshInfo[m].mesh, m);
        }
    }
}
