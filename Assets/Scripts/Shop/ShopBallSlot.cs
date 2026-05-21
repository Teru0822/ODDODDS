using System.Collections.Generic;
using UnityEngine;
using TMPro;

/// <summary>
/// shop_ball の子オブジェクト（ball_1〜ball_9 など）に1つずつアタッチする販売スロット。
/// インスペクターで「販売するボール prefab」「価格」を設定する。
/// 起動時に display 用としてボール prefab を子インスタンス化し、その上に値札 (World Space TMP) を表示する。
/// ハイライト（緑=購入可、赤=購入不可）と raycast 当たり判定を提供する。
/// </summary>
public class ShopBallSlot : MonoBehaviour
{
    [Header("販売内容")]
    [Tooltip("購入時にスポーンするボール prefab (PinballBallController 付きを想定)")]
    public GameObject ballPrefab;

    [Tooltip("価格 (未洗浄金から差し引かれる)")]
    public float price = 100f;

    [Header("陳列（見た目）")]
    [Tooltip("起動時に ballPrefab を子としてインスタンス化し陳列する")]
    public bool spawnDisplayBall = true;

    [Tooltip("陳列ボールに掛けるスケール (independentScale=true ならワールドスケール、false ならローカルスケール)")]
    public Vector3 displayScale = Vector3.one;

    [Tooltip("陳列ボールのローカル位置オフセット (スロットのローカル空間)")]
    public Vector3 displayLocalOffset = Vector3.zero;

    [Tooltip("陳列ボールの回転をスロットから継承しない (true: ワールド identity 固定)")]
    public bool independentRotation = true;

    [Tooltip("陳列ボールのスケールをスロットから継承しない (true: displayScale が実ワールドスケール)")]
    public bool independentScale = true;

    [Header("値札 (World Space TextMeshPro)")]
    [Tooltip("値札の TextMeshPro (3D)。null なら子から自動検索 → 見つからなければ自動生成")]
    public TextMeshPro priceTagText;

    [Tooltip("値札表示フォーマット ({0} に金額)")]
    public string priceTagFormat = "¥{0:N0}";

    [Tooltip("値札のワールド位置オフセット (スロット原点から)。スロットの回転/スケールには影響されない")]
    public Vector3 priceTagWorldOffset = new Vector3(0f, 0.15f, 0f);

    [Tooltip("値札を常にカメラへ向ける")]
    public bool priceTagBillboard = true;

    [Tooltip("値札の前後を反転（裏向きに見える場合に ON）")]
    public bool priceTagFlip180 = false;

    [Tooltip("カメラのロール（傾き）を無視してワールド上向きを維持する")]
    public bool priceTagUseWorldUp = true;

    [Tooltip("自動生成する値札のサイズ (m)")]
    public float autoTagFontSize = 0.05f;

    [Tooltip("自動生成する値札の色")]
    public Color autoTagColor = Color.white;

    [Header("ハイライト")]
    [Tooltip("購入可能時のティント色")]
    public Color affordableColor = new Color(0.25f, 1f, 0.4f, 1f);

    [Tooltip("購入不可時のティント色")]
    public Color notAffordableColor = new Color(1f, 0.3f, 0.3f, 1f);

    [Range(0f, 1f)]
    [Tooltip("ハイライトの強さ (0=オリジナル, 1=完全置換)")]
    public float highlightStrength = 0.65f;

    [Tooltip("値札テキスト自体もハイライト色に追従させる")]
    public bool tintPriceTag = true;

    // 内部状態
    private GameObject _displayInstance;
    private Renderer[] _highlightTargets;
    private Color[] _originalBaseColors;
    private MaterialPropertyBlock _mpb;
    private Color _originalTagColor;
    private bool _purchased = false;
    private Camera _billboardCam;

    public bool IsPurchased => _purchased;
    public float Price => price;
    public GameObject BallPrefab => ballPrefab;

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int LegacyColorId = Shader.PropertyToID("_Color");

    private ShopBallController _ownerController;

    private void Start()
    {
        // 親ヒエラルキーから ShopBallController を探す (billboard カメラ参照用)
        _ownerController = GetComponentInParent<ShopBallController>();
        SetupDisplayBall();
        CollectHighlightTargets();
        CacheOriginalColors();
        SetupPriceTag();
        ApplyHighlight(false, false);
    }

    /// <summary>ShopBallController から billboard 用カメラを直接渡したい場合のセッタ</summary>
    public void SetBillboardCamera(Camera cam)
    {
        _billboardCam = cam;
    }

    private void LateUpdate()
    {
        if (!priceTagBillboard || priceTagText == null) return;

        // カメラ参照の優先順位: 明示セット > 親 Controller の lookCamera > Camera.main
        Camera cam = _billboardCam;
        if (cam == null && _ownerController != null) cam = _ownerController.lookCamera;
        if (cam == null) cam = Camera.main;
        if (cam == null) return;

        // カメラの forward に値札の forward を合わせる view-aligned billboard
        Vector3 fwd = cam.transform.forward;
        Vector3 up = priceTagUseWorldUp ? Vector3.up : cam.transform.up;
        if (fwd.sqrMagnitude < 1e-6f) return;

        Quaternion rot = Quaternion.LookRotation(fwd, up);
        if (priceTagFlip180) rot *= Quaternion.Euler(0f, 180f, 0f);
        priceTagText.transform.rotation = rot;
    }

    private void SetupDisplayBall()
    {
        if (!spawnDisplayBall || ballPrefab == null) return;
        _displayInstance = Instantiate(ballPrefab, transform);
        _displayInstance.name = ballPrefab.name + " (Display)";
        _displayInstance.transform.localPosition = displayLocalOffset;

        if (independentRotation)
        {
            _displayInstance.transform.rotation = Quaternion.identity;
        }
        else
        {
            _displayInstance.transform.localRotation = Quaternion.identity;
        }

        if (independentScale)
        {
            Vector3 ps = transform.lossyScale;
            _displayInstance.transform.localScale = new Vector3(
                displayScale.x / Mathf.Max(1e-4f, ps.x),
                displayScale.y / Mathf.Max(1e-4f, ps.y),
                displayScale.z / Mathf.Max(1e-4f, ps.z));
        }
        else
        {
            _displayInstance.transform.localScale = displayScale;
        }

        // 陳列用なので物理を止め、ピンボール制御スクリプトも無効化する
        foreach (var rb in _displayInstance.GetComponentsInChildren<Rigidbody>(true))
        {
            rb.isKinematic = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        foreach (var ctrl in _displayInstance.GetComponentsInChildren<PinballBallController>(true))
        {
            ctrl.enabled = false;
        }
    }

    private void CollectHighlightTargets()
    {
        var list = new List<Renderer>();
        if (_displayInstance != null)
        {
            _displayInstance.GetComponentsInChildren<Renderer>(true, list);
        }
        else
        {
            GetComponentsInChildren<Renderer>(true, list);
        }
        // TextMeshPro 3D が持つ MeshRenderer は除外（値札は tintPriceTag 側で扱う）
        list.RemoveAll(r => r == null || r.GetComponent<TextMeshPro>() != null);
        _highlightTargets = list.ToArray();
        _mpb = new MaterialPropertyBlock();
    }

    private void CacheOriginalColors()
    {
        if (_highlightTargets == null) return;
        _originalBaseColors = new Color[_highlightTargets.Length];
        for (int i = 0; i < _highlightTargets.Length; i++)
        {
            Color c = Color.white;
            var mat = _highlightTargets[i].sharedMaterial;
            if (mat != null)
            {
                if (mat.HasProperty(BaseColorId)) c = mat.GetColor(BaseColorId);
                else if (mat.HasProperty(LegacyColorId)) c = mat.GetColor(LegacyColorId);
            }
            _originalBaseColors[i] = c;
        }
    }

    private void SetupPriceTag()
    {
        if (priceTagText == null)
        {
            // 自身の階層を検索（display ball の中のテキストは除外）
            foreach (var tmp in GetComponentsInChildren<TextMeshPro>(true))
            {
                if (_displayInstance != null && tmp.transform.IsChildOf(_displayInstance.transform)) continue;
                priceTagText = tmp;
                break;
            }
        }
        if (priceTagText == null)
        {
            // 自動生成: スロット配下に作り、スロットの回転/スケールに依存しないよう
            // ワールド位置を直接設定 + 親スケールを打ち消す。
            var tagGo = new GameObject("PriceTag");
            tagGo.transform.SetParent(transform, false);
            tagGo.transform.position = transform.position + priceTagWorldOffset;
            tagGo.transform.rotation = Quaternion.identity;
            Vector3 ps = transform.lossyScale;
            tagGo.transform.localScale = new Vector3(
                1f / Mathf.Max(1e-4f, ps.x),
                1f / Mathf.Max(1e-4f, ps.y),
                1f / Mathf.Max(1e-4f, ps.z));
            priceTagText = tagGo.AddComponent<TextMeshPro>();
            priceTagText.alignment = TextAlignmentOptions.Center;
            priceTagText.fontSize = autoTagFontSize;
            priceTagText.color = autoTagColor;
            var rt = priceTagText.rectTransform;
            rt.sizeDelta = new Vector2(1f, 0.3f);
        }
        // 既存の TMP が指定されている場合はユーザー配置を尊重して位置に触らない
        _originalTagColor = priceTagText.color;
        RefreshPriceTagText();
    }

    /// <summary>価格表示を更新（外部から価格を変えた時にも呼べる）</summary>
    public void RefreshPriceTagText()
    {
        if (priceTagText == null) return;
        priceTagText.text = string.Format(priceTagFormat, Mathf.FloorToInt(price));
    }

    /// <summary>ハイライト適用。show=false で解除。</summary>
    public void ApplyHighlight(bool show, bool affordable)
    {
        if (_highlightTargets == null) return;

        if (!show)
        {
            for (int i = 0; i < _highlightTargets.Length; i++)
            {
                if (_highlightTargets[i] == null) continue;
                _highlightTargets[i].SetPropertyBlock(null);
            }
            if (tintPriceTag && priceTagText != null) priceTagText.color = _originalTagColor;
            return;
        }

        Color target = affordable ? affordableColor : notAffordableColor;
        for (int i = 0; i < _highlightTargets.Length; i++)
        {
            var r = _highlightTargets[i];
            if (r == null) continue;
            Color baseColor = _originalBaseColors != null && i < _originalBaseColors.Length ? _originalBaseColors[i] : Color.white;
            Color tinted = Color.Lerp(baseColor, target, highlightStrength);
            r.GetPropertyBlock(_mpb);
            _mpb.SetColor(BaseColorId, tinted);
            _mpb.SetColor(LegacyColorId, tinted);
            r.SetPropertyBlock(_mpb);
        }
        if (tintPriceTag && priceTagText != null) priceTagText.color = target;
    }

    /// <summary>このスロットの陳列ボールが ray と交差するか判定。当たれば最近接 hit を返す。</summary>
    public bool Raycast(Ray ray, out RaycastHit hit, float maxDistance)
    {
        hit = default;
        if (_purchased) return false;
        var colliders = _displayInstance != null
            ? _displayInstance.GetComponentsInChildren<Collider>(false)
            : GetComponentsInChildren<Collider>(false);

        bool any = false;
        float best = maxDistance;
        for (int i = 0; i < colliders.Length; i++)
        {
            var c = colliders[i];
            if (c == null || !c.enabled) continue;
            if (c.Raycast(ray, out RaycastHit h, best))
            {
                hit = h;
                best = h.distance;
                any = true;
            }
        }
        return any;
    }

    /// <summary>購入完了時の処理。陳列ボールと値札を非表示にし、再購入を防ぐ。</summary>
    public void MarkPurchased()
    {
        _purchased = true;
        ApplyHighlight(false, false);
        if (_displayInstance != null) _displayInstance.SetActive(false);
        if (priceTagText != null) priceTagText.gameObject.SetActive(false);
    }

    /// <summary>購入状態を解除して再陳列する（在庫リセット用）</summary>
    public void ResetSlot()
    {
        _purchased = false;
        if (_displayInstance != null) _displayInstance.SetActive(true);
        if (priceTagText != null) priceTagText.gameObject.SetActive(true);
        ApplyHighlight(false, false);
    }
}
