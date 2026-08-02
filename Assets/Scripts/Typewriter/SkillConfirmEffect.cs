using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// スキル確定時の演出コンポーネント。
/// RewardSelectionUI と同じ GameObject か任意の場所に追加し、
/// RewardSelectionUI._confirmEffect にアサインして使う。
/// </summary>
[DisallowMultipleComponent]
public class SkillConfirmEffect : MonoBehaviour
{
    [Header("スプライト（推奨設定）")]
    [Tooltip("グロー・フラッシュに使うスプライト。UI_steampunk_button_active 等を指定するとUIに馴染む。null なら手続き生成テクスチャを使用")]
    [SerializeField] private Sprite _flashSprite;
    [Tooltip("リングに使うスプライト。null なら手続き生成テクスチャを使用")]
    [SerializeField] private Sprite _ringSprite;

    [Header("フラッシュ")]
    [SerializeField] private Color _flashColor = new Color(1f, 0.80f, 0.40f, 0.85f);
    [SerializeField, Range(0.3f, 1.5f)] private float _flashStartScale = 0.88f;
    [SerializeField, Range(1f, 5f)] private float _flashEndScale = 1.65f;
    [SerializeField, Range(0.05f, 1f)] private float _flashDuration = 0.28f;

    [Header("バースト（外側グロー）")]
    [SerializeField] private Color _burstColor = new Color(0.90f, 0.50f, 0.12f, 0.80f);
    [SerializeField, Range(0.3f, 1.5f)] private float _burstStartScale = 0.80f;
    [SerializeField, Range(1f, 5f)] private float _burstEndScale = 2.0f;
    [SerializeField, Range(0.05f, 2f)] private float _burstDuration = 0.55f;

    [Header("リング（1〜3波）")]
    [SerializeField] private Color _ringColor = new Color(1f, 0.72f, 0.22f, 0.75f);
    [SerializeField, Range(0.3f, 1.5f)] private float _ringStartScale = 0.85f;
    [SerializeField, Range(1f, 6f)] private float _ringEndScale = 2.3f;
    [SerializeField, Range(0.05f, 2f)] private float _ringDuration = 0.50f;
    [SerializeField, Range(1, 3)] private int _ringWaveCount = 2;
    [SerializeField, Range(0f, 0.3f)] private float _ringStagger = 0.12f;

    [Header("放射スパーク（0 で無効）")]
    [SerializeField] private Color _sparkColor = new Color(1f, 0.88f, 0.35f, 1f);
    [SerializeField, Range(4f, 36f)] private float _sparkSize = 12f;
    [SerializeField, Range(0f, 1f)] private float _sparkStartRadius = 0.38f;
    [SerializeField, Range(1f, 5f)] private float _sparkEndRadius = 1.70f;
    [SerializeField, Range(0.05f, 1f)] private float _sparkDuration = 0.32f;
    [Tooltip("0 にするとスパークを無効化")]
    [SerializeField, Range(0, 16)] private int _sparkCount = 8;

    [Header("SELECTED テキスト（空で無効）")]
    [SerializeField] private string _selectedText = "";
    [SerializeField] private TMP_FontAsset _font;
    [SerializeField] private Color _selectedTextColor = new Color(1f, 0.90f, 0.55f, 1f);
    [SerializeField, Range(16f, 60f)] private float _selectedTextSize = 30f;
    [SerializeField, Range(0.2f, 2f)] private float _selectedTextDuration = 0.65f;
    [SerializeField, Range(20f, 120f)] private float _selectedTextRise = 45f;

    [Header("角丸設定（スプライト未設定時）")]
    [SerializeField, Range(0.05f, 0.5f)] private float _glowCornerRatio = 0.28f;

    [Header("パーティクル VFX（フェーズ2）")]
    [Tooltip("確定時に再生するパーティクルプレハブ（Loot_Poof, LootFlash_Cash など複数可）")]
    [SerializeField] private GameObject[] _vfxPrefabs;
    [Tooltip("VFX を映すための専用カメラ（Depth Only、UIEffect レイヤーのみ描画）。null ならレイヤー変更なし")]
    [SerializeField] private Camera _effectCamera;
    [Tooltip("VFX に割り当てるレイヤー名。Project Settings → Tags and Layers で追加した名前と一致させること")]
    [SerializeField] private string _vfxLayerName = "UIEffect";
    [Tooltip("VFX のワールドスケール（0.01〜0.02 程度が目安）")]
    [SerializeField, Range(0.001f, 0.1f)] private float _vfxWorldScale = 0.015f;
    [SerializeField, Range(0f, 0.5f)] private float _vfxDelay = 0f;
    [SerializeField, Range(1f, 10f)] private float _vfxLifetime = 3f;

    private static Sprite _circleSprite;

    // ────────────────────────────────────────────────
    // 公開 API
    // ────────────────────────────────────────────────

    public void PlayAt(Vector2 canvasLocalPos, Vector2 buttonSize, Transform canvasTransform)
    {
        if (canvasTransform == null) return;
        if (_circleSprite == null) _circleSprite = BuildCircleSprite(64);

        // スプライトが未設定なら手続き生成テクスチャを使う
        int th = 64;
        int tw = Mathf.Max(4, Mathf.RoundToInt(th * buttonSize.x / Mathf.Max(buttonSize.y, 1f)));
        Texture2D glowTex = null;
        Texture2D ringTex = null;
        Sprite glowSpr = _flashSprite;
        Sprite ringSpr = _ringSprite;

        if (glowSpr == null)
        {
            glowTex = BuildRoundedRectGlowTex(tw, th, _glowCornerRatio);
            glowSpr = Sprite.Create(glowTex, new Rect(0, 0, tw, th), new Vector2(0.5f, 0.5f));
        }
        if (ringSpr == null)
        {
            ringTex = BuildRoundedRectRingTex(tw, th, _glowCornerRatio);
            ringSpr = Sprite.Create(ringTex, new Rect(0, 0, tw, th), new Vector2(0.5f, 0.5f));
        }

        var root = new GameObject("SkillConfirmFX", typeof(RectTransform));
        root.transform.SetParent(canvasTransform, false);
        var rootRT = root.GetComponent<RectTransform>();
        rootRT.anchoredPosition = canvasLocalPos;
        rootRT.sizeDelta = buttonSize;
        rootRT.SetAsLastSibling();

        var seq = DOTween.Sequence();

        // ── Flash ────────────────────────────────────────────
        var flash = MakeRectLayer("Flash", root.transform, glowSpr, _flashColor, _flashStartScale);
        seq.Join(flash.rectTransform.DOScale(_flashEndScale, _flashDuration).SetEase(Ease.OutCubic));
        seq.Join(flash.DOFade(0f, _flashDuration).SetEase(Ease.OutCubic));

        // ── Burst ─────────────────────────────────────────────
        var burst = MakeRectLayer("Burst", root.transform, glowSpr, _burstColor, _burstStartScale);
        seq.Insert(0.02f, burst.rectTransform.DOScale(_burstEndScale, _burstDuration).SetEase(Ease.OutCubic));
        seq.Insert(0.02f, burst.DOFade(0f, _burstDuration).SetEase(Ease.Linear));

        // ── Ring（複数波）────────────────────────────────────
        for (int w = 0; w < _ringWaveCount; w++)
        {
            float d = 0.03f + w * _ringStagger;
            var ring = MakeRectLayer($"Ring{w}", root.transform, ringSpr, _ringColor, _ringStartScale);
            seq.Insert(d, ring.rectTransform.DOScale(_ringEndScale, _ringDuration).SetEase(Ease.OutCubic));
            seq.Insert(d, ring.DOFade(0f, _ringDuration).SetEase(Ease.Linear));
        }

        // ── 放射スパーク ─────────────────────────────────────
        for (int i = 0; i < _sparkCount; i++)
        {
            float angle = (i / (float)_sparkCount) * Mathf.PI * 2f + Random.Range(-0.12f, 0.12f);
            float speed = Random.Range(0.80f, 1.25f);
            float halfW = buttonSize.x * 0.5f;
            Vector2 dir  = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            Vector2 start = dir * halfW * _sparkStartRadius;
            Vector2 end   = dir * halfW * _sparkEndRadius * speed;

            var sp = MakeSpark($"Spark{i}", root.transform, start, _sparkSize * Random.Range(0.8f, 1.3f));
            sp.color = _sparkColor;
            seq.Insert(0.04f, sp.rectTransform.DOAnchorPos(end, _sparkDuration * speed).SetEase(Ease.OutCubic));
            seq.Insert(0.04f, sp.DOFade(0f, _sparkDuration * speed).SetEase(Ease.InCubic));
        }

        // ── SELECTED テキスト ─────────────────────────────────
        if (!string.IsNullOrEmpty(_selectedText))
        {
            var txtGo = new GameObject("SelectedText", typeof(RectTransform));
            txtGo.transform.SetParent(root.transform, false);
            var txtRT = txtGo.GetComponent<RectTransform>();
            txtRT.anchorMin = txtRT.anchorMax = new Vector2(0.5f, 0.5f);
            txtRT.sizeDelta = new Vector2(buttonSize.x * 2.2f, _selectedTextSize * 1.8f);
            txtRT.anchoredPosition = Vector2.zero;
            txtRT.localScale = Vector3.zero;
            var tmp = txtGo.AddComponent<TextMeshProUGUI>();
            if (_font != null) tmp.font = _font;
            tmp.fontSize = _selectedTextSize;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = new Color(_selectedTextColor.r, _selectedTextColor.g, _selectedTextColor.b, 0f);
            tmp.text = _selectedText;
            tmp.enableWordWrapping = false;
            tmp.fontStyle = FontStyles.Bold;
            seq.Insert(0.08f, txtRT.DOScale(1.2f, 0.18f).SetEase(Ease.OutBack));
            seq.Insert(0.26f, txtRT.DOScale(1.0f, 0.10f));
            seq.Insert(0.08f, tmp.DOFade(1f, 0.14f));
            seq.Insert(0.30f, tmp.DOFade(0f, _selectedTextDuration - 0.22f).SetEase(Ease.InQuad));
            seq.Insert(0.08f, txtRT.DOAnchorPosY(_selectedTextRise, _selectedTextDuration).SetEase(Ease.OutCubic));
        }

        // ── クリーンアップ ────────────────────────────────────
        seq.OnComplete(() =>
        {
            if (root != null) Destroy(root);
            if (glowTex != null) { Destroy(glowSpr); Destroy(glowTex); }
            if (ringTex != null) { Destroy(ringSpr); Destroy(ringTex); }
        });

        // ── パーティクル VFX（フェーズ2）─────────────────────
        SpawnVFX(canvasLocalPos, canvasTransform);
    }

    // ────────────────────────────────────────────────
    // VFX スポーン
    // ────────────────────────────────────────────────

    private void SpawnVFX(Vector2 canvasLocalPos, Transform canvasTransform)
    {
        if (_vfxPrefabs == null || _vfxPrefabs.Length == 0) return;
        Vector3 worldPos = canvasTransform.TransformPoint(new Vector3(canvasLocalPos.x, canvasLocalPos.y, 0f));
        int layer = string.IsNullOrEmpty(_vfxLayerName) ? -1 : LayerMask.NameToLayer(_vfxLayerName);
        foreach (var prefab in _vfxPrefabs)
        {
            if (prefab == null) continue;
            if (_vfxDelay > 0f) StartCoroutine(SpawnVFXDelayed(prefab, worldPos, layer));
            else InstantiateVFX(prefab, worldPos, layer);
        }
    }

    private IEnumerator SpawnVFXDelayed(GameObject prefab, Vector3 worldPos, int layer)
    {
        yield return new WaitForSeconds(_vfxDelay);
        InstantiateVFX(prefab, worldPos, layer);
    }

    private void InstantiateVFX(GameObject prefab, Vector3 worldPos, int layer)
    {
        var vfx = Instantiate(prefab, worldPos, Quaternion.identity);
        vfx.transform.localScale = Vector3.one * _vfxWorldScale;
        if (layer >= 0) SetLayerRecursive(vfx, layer);
        foreach (var r in vfx.GetComponentsInChildren<Renderer>())
        {
            r.sortingLayerName = "UI";
            r.sortingOrder = 100;
        }
        foreach (var ps in vfx.GetComponentsInChildren<ParticleSystem>())
        {
            var main = ps.main;
            main.stopAction = ParticleSystemStopAction.Destroy;
        }
        Destroy(vfx, _vfxLifetime);
    }

    private static void SetLayerRecursive(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform child in go.transform)
            SetLayerRecursive(child.gameObject, layer);
    }

    // ────────────────────────────────────────────────
    // 生成ヘルパー
    // ────────────────────────────────────────────────

    private static Image MakeRectLayer(string name, Transform parent, Sprite spr, Color color, float startScale)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;
        rt.localScale = Vector3.one * startScale;
        var img = go.AddComponent<Image>();
        img.sprite = spr;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    private static Image MakeSpark(string name, Transform parent, Vector2 anchoredPos, float size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(size, size);
        rt.anchoredPosition = anchoredPos;
        var img = go.AddComponent<Image>();
        img.sprite = _circleSprite;
        img.raycastTarget = false;
        return img;
    }

    // ────────────────────────────────────────────────
    // テクスチャ生成（スプライト未設定時のフォールバック）
    // ────────────────────────────────────────────────

    private static Sprite BuildCircleSprite(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        var pix = new Color32[size * size];
        float c = size * 0.5f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                float a = Mathf.Clamp01(1f - d);
                a = a * a;
                pix[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
        tex.SetPixels32(pix);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    private static float SdRoundedRect(float px, float py, float bx, float by, float r)
    {
        float qx = Mathf.Abs(px) - bx + r;
        float qy = Mathf.Abs(py) - by + r;
        float outer = Mathf.Sqrt(Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f) + Mathf.Max(qy, 0f) * Mathf.Max(qy, 0f));
        return outer + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
    }

    private static Texture2D BuildRoundedRectGlowTex(int tw, int th, float cornerRatio)
    {
        var tex = new Texture2D(tw, th, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        var pix = new Color32[tw * th];
        float cx = tw * 0.5f, cy = th * 0.5f;
        float r  = th * cornerRatio;
        float bx = tw * 0.5f - r, by = th * 0.5f - r;
        float glowRange = th * 0.28f;
        for (int y = 0; y < th; y++)
            for (int x = 0; x < tw; x++)
            {
                float sdf = SdRoundedRect(x - cx, y - cy, bx, by, r);
                float a   = Mathf.Clamp01(1f - sdf / glowRange);
                a = Mathf.Sqrt(a);
                pix[y * tw + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
        tex.SetPixels32(pix);
        tex.Apply();
        return tex;
    }

    private static Texture2D BuildRoundedRectRingTex(int tw, int th, float cornerRatio)
    {
        var tex = new Texture2D(tw, th, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        var pix = new Color32[tw * th];
        float cx = tw * 0.5f, cy = th * 0.5f;
        float r  = th * cornerRatio;
        float bx = tw * 0.5f - r, by = th * 0.5f - r;
        float ringWidth = th * 0.12f;
        for (int y = 0; y < th; y++)
            for (int x = 0; x < tw; x++)
            {
                float sdf = SdRoundedRect(x - cx, y - cy, bx, by, r);
                float a   = 1f - Mathf.Abs(sdf) / ringWidth;
                a = Mathf.Clamp01(a);
                a = Mathf.Sqrt(a);
                pix[y * tw + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
        tex.SetPixels32(pix);
        tex.Apply();
        return tex;
    }
}
