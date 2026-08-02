using TMPro;
using UnityEngine;

/// <summary>
/// TMP テキストを RenderTexture に焼き込み、紙の _MainTex に適用する。
/// StartBurning() の直前に BakeAndApply() を呼ぶことで、テキストが紙の
/// テクスチャの一部となり、燃焼マスクと完全に同期して消える。
///
/// ■ 事前設定不要（専用レイヤー追加は不要）
///   paper_cube prefab のルートに AddComponent するだけ。
/// </summary>
[RequireComponent(typeof(RealisticPaperBurn))]
public class PaperTextBaker : MonoBehaviour
{
    [Tooltip("焼き込む TMP テキスト。未設定なら子から自動検索")]
    public TextMeshPro sourceText;

    [Tooltip("焼き込み解像度（2の累乗。512 推奨）")]
    public int resolution = 512;

    private RenderTexture _rt;

    void Start()
    {
        if (sourceText == null)
            sourceText = GetComponentInChildren<TextMeshPro>();
    }

    /// <summary>
    /// テキストを撮影して mat の _MainTex に焼き込む。
    /// TMP オブジェクトはこの呼び出し後に非表示になる。
    /// </summary>
    public void BakeAndApply(Material mat)
    {
        if (sourceText == null || mat == null) return;

        // ── 1. RenderTexture 生成 ─────────────────────────────────────
        _rt              = new RenderTexture(resolution, resolution, 16, RenderTextureFormat.ARGB32);
        _rt.antiAliasing = 4;
        _rt.Create();

        // ── 2. 紙のメッシュを一時的に非表示（TMP だけを撮影するため）──
        //    専用レイヤー不要。同一フレーム内で即座に復元するので1フレームも表示されない。
        var meshRend    = GetComponent<Renderer>();
        bool meshActive = meshRend != null && meshRend.enabled;
        if (meshRend != null) meshRend.enabled = false;

        // ── 3. 一時カメラ：紙の表面法線（localUp）方向から見下ろす ──────
        var camGO = new GameObject("_PaperBakeCam");
        var up    = transform.up; // 紙の表面法線（スケールY が薄い面の法線）

        camGO.transform.SetPositionAndRotation(
            transform.position + up * 0.3f,
            Quaternion.LookRotation(-up, transform.forward)
        );

        var cam = camGO.AddComponent<Camera>();
        cam.orthographic     = true;
        // 紙の XZ サイズ（lossyScale の大きい辺の半値 + 余白）
        cam.orthographicSize = Mathf.Max(transform.lossyScale.x,
                                         transform.lossyScale.z) * 0.5f + 0.1f;
        cam.clearFlags       = CameraClearFlags.SolidColor;
        cam.backgroundColor  = new Color(0f, 0f, 0f, 0f); // 透明背景
        cam.cullingMask      = 1 << sourceText.gameObject.layer; // TMP のレイヤーだけ
        cam.targetTexture    = _rt;
        cam.nearClipPlane    = 0.01f;
        cam.farClipPlane     = 0.40f; // 紙面付近だけ（余分なオブジェクトを除外）

        // ── 4. TMP メッシュを最新状態に更新してから描画 ─────────────────
        sourceText.ForceMeshUpdate();
        cam.Render();

        Destroy(camGO);

        // ── 5. 紙のメッシュを元に戻す ────────────────────────────────
        if (meshRend != null) meshRend.enabled = meshActive;

        // ── 6. マテリアルにテクスチャを適用 ─────────────────────────────
        mat.SetTexture("_MainTex", _rt);
        mat.SetFloat("_TexBlend", 1f);

        // ── 7. TMP オブジェクト非表示（テクスチャに置き換えたため不要）──
        sourceText.gameObject.SetActive(false);
    }

    void OnDestroy()
    {
        if (_rt != null)
        {
            _rt.Release();
            Destroy(_rt);
        }
    }
}
