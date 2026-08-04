using Lattice;
using TMPro;
using UnityEngine;

/// <summary>
/// Lattice Modifier と連携して紙メッシュを動的変形させるアニメーター。
/// Start() でメッシュを細分割プレーンに差し替え、Lattice スケールを自動調整する。
///
/// 【Prefab セットアップ】
/// 1. paper_cube の子 GO「Lattice」に Lattice コンポーネントを追加（Resolution 3×3×2）
/// 2. paper_cube ルートに LatticeModifier を追加
/// 3. paper_cube ルートにこのコンポーネントを追加し _lattice に子 Lattice GO をアサイン
/// </summary>
[DisallowMultipleComponent]
public class PaperLatticeAnimator : MonoBehaviour
{
    public enum PaperState { Idle, Burning, Launching }

    [Header("Lattice 参照")]
    [Tooltip("子 GO の Lattice コンポーネントをアサイン。null なら GetComponentInChildren で自動取得")]
    [SerializeField] private Lattice.Lattice _lattice;

    [Header("メッシュ細分割（変形に必要）")]
    [Tooltip("true にすると細分割プレーンに差し替える。スケールの最も薄い軸を自動検出して向きを決定する")]
    [SerializeField] private bool _useSubdividedMesh = false;
    [SerializeField, Range(3, 12)] private int _subdivX = 6;
    [SerializeField, Range(4, 16)] private int _subdivY = 8;

    [Header("Burn（燃焼時の丸まり）")]
    [SerializeField, Range(0f, 3f)]   private float _burnCurlAmount   = 0.40f;
    [SerializeField, Range(0f, 15f)]  private float _burnShakeSpeed   = 6f;
    [SerializeField, Range(0f, 1f)]   private float _burnShakeAmount  = 0.025f;
    [Tooltip("コーナーが丸まる方向（true=Z+, false=Z-）")]
    [SerializeField] private bool _burnCurlPositiveZ = true;

    [Header("Launch（ローンチ時のねじれ）")]
    [SerializeField, Range(0f, 0.15f)] private float _launchTwistAmount = 0.07f;
    [SerializeField, Range(0f, 10f)]   private float _launchTwistSpeed  = 5f;
    [SerializeField, Range(0.1f, 2f)]  private float _launchPeakTime    = 0.5f;

    [Header("テスト用")]
    [Tooltip("true にするとタイプ中も波打ちを表示（テキストと分離します。動作確認用）")]
    [SerializeField] private bool _deformInIdle = false;
    [Tooltip("0より大きくするとBurnTをこの値で上書き。燃焼せずにカールをテストできる")]
    [SerializeField, Range(0f, 1f)] private float _debugBurnTOverride = 0f;

    // ────────────────────────────────────────────────
    // ランタイム
    // ────────────────────────────────────────────────

    private PaperState _state = PaperState.Idle;
    private float _launchElapsed;
    private float _burnStartTime;
    private Vector3 _burnBasePos;
    private bool   _burnJitterActive;
    private RealisticPaperBurn _burn;
    private TextMeshPro        _tmp;

    private void Start()
    {
        if (_lattice == null)
            _lattice = GetComponentInChildren<Lattice.Lattice>(true);

        if (_useSubdividedMesh)
            ReplaceWithSubdividedMesh();

        // LatticeModifier が TargetMesh を管理している場合 MeshFilter は空になる。
        // MeshFilter が空なら TargetMesh から補完して可視状態を維持する
        var mod    = GetComponent<LatticeModifier>();
        var filter = GetComponent<MeshFilter>();
        if (filter != null && filter.sharedMesh == null && mod?.TargetMesh != null)
            filter.sharedMesh = mod.TargetMesh;
        var mesh = filter?.sharedMesh ?? mod?.TargetMesh;
        FixLatticeScale(mesh);

        // LatticeModifier の OnEnable より後に実行するため 1 フレーム遅延
        StartCoroutine(LateAutoWire());

        _burn = GetComponent<RealisticPaperBurn>();
        _tmp  = GetComponentInChildren<TextMeshPro>(true);
    }

    private System.Collections.IEnumerator LateAutoWire()
    {
        yield return null;
        AutoWireModifier();
    }

    // ────────────────────────────────────────────────
    // 細分割メッシュ差し替え
    // ────────────────────────────────────────────────

    private void ReplaceWithSubdividedMesh()
    {
        var modifier = GetComponent<LatticeModifier>();
        if (modifier == null) return;

        // ルートのスケールから最も薄い軸を自動検出（そこが紙の奥行き方向）
        Vector3 ws = transform.lossyScale;
        float ax = Mathf.Abs(ws.x), ay = Mathf.Abs(ws.y), az = Mathf.Abs(ws.z);
        int thickAxis; // 0=Z薄(XY面), 1=Y薄(XZ面), 2=X薄(YZ面)
        if (az <= ax && az <= ay)      thickAxis = 0;
        else if (ay <= ax && ay <= az) thickAxis = 1;
        else                           thickAxis = 2;

        var newMesh = CreatePaperMesh(_subdivX, _subdivY, thickAxis);
        modifier.TargetMesh = newMesh;
    }

    private void FixLatticeScale(Mesh mesh)
    {
        if (_lattice == null || mesh == null) return;
        var bounds = mesh.bounds;
        var ls = _lattice.transform.localScale;
        // XY はメッシュ境界+10%余裕。Z は既存値を維持（変形量に直結するためユーザーが調整）
        _lattice.transform.localScale = new Vector3(
            bounds.size.x > 0f ? bounds.size.x * 1.1f : ls.x,
            bounds.size.y > 0f ? bounds.size.y * 1.1f : ls.y,
            ls.z
        );
    }

    /// <summary>
    /// 細分割プレーンメッシュを生成する。thickAxis で紙の奥行き軸を指定。
    /// thickAxis: 0=Z薄(XY平面), 1=Y薄(XZ平面), 2=X薄(YZ平面)
    /// </summary>
    private static Mesh CreatePaperMesh(int subdivX, int subdivY, int thickAxis)
    {
        int vx = subdivX + 1;
        int vy = subdivY + 1;

        var vertices  = new Vector3[vx * vy];
        var uvs       = new Vector2[vx * vy];
        var normals   = new Vector3[vx * vy];
        var triangles = new int[subdivX * subdivY * 6];

        for (int j = 0; j < vy; j++)
        {
            for (int i = 0; i < vx; i++)
            {
                float u   = (float)i / subdivX;
                float v   = (float)j / subdivY;
                float s   = u - 0.5f;
                float t_  = v - 0.5f;
                int   idx = j * vx + i;

                Vector3 pos, nrm;
                switch (thickAxis)
                {
                    case 1:  pos = new Vector3(s, 0f, t_); nrm = Vector3.up;      break;
                    case 2:  pos = new Vector3(0f, t_, s); nrm = Vector3.right;   break;
                    default: pos = new Vector3(s, t_, 0f); nrm = Vector3.forward; break;
                }

                vertices[idx] = pos;
                uvs[idx]      = new Vector2(u, v);
                normals[idx]  = nrm;
            }
        }

        int ti = 0;
        for (int j = 0; j < subdivY; j++)
        {
            for (int i = 0; i < subdivX; i++)
            {
                int bl = j * vx + i;
                int br = bl + 1;
                int tl = bl + vx;
                int tr = tl + 1;
                triangles[ti++] = bl; triangles[ti++] = tl; triangles[ti++] = tr;
                triangles[ti++] = bl; triangles[ti++] = tr; triangles[ti++] = br;
            }
        }

        var mesh = new Mesh { name = "PaperSubdivided" };
        mesh.vertices  = vertices;
        mesh.uv        = uvs;
        mesh.normals   = normals;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
        return mesh;
    }

    // ────────────────────────────────────────────────
    // LatticeModifier 配線
    // ────────────────────────────────────────────────

    private void AutoWireModifier()
    {
        if (_lattice == null) return;

        var modifier = GetComponent<LatticeModifier>();
        if (modifier == null)
        {
            Debug.LogWarning("[PaperLatticeAnimator] LatticeModifier が見つかりません。ルートGOに追加してください。", this);
            return;
        }
        WireLatticeItem(modifier);
    }

    private void WireLatticeItem(LatticeModifier modifier)
    {
        // Clear して Add し直すことで Global 等の設定を確実に上書きする
        modifier.Lattices.Clear();
        modifier.Lattices.Add(new LatticeItem
        {
            Lattice       = _lattice,
            Interpolation = InterpolationMethod.LinearSmooth,
            Global        = true,
            Mask          = new LatticeMask { Vertex = new LatticeMask.VertexSettings { Multiplier = 1f } }
        });

        if (modifier.Lattices.Count == 0)
            Debug.LogWarning("[PaperLatticeAnimator] LatticeModifier.Lattices への追加が反映されませんでした。Inspector で Global=true を手動設定してください。", this);
    }

    // ────────────────────────────────────────────────
    // ステートマシン
    // ────────────────────────────────────────────────

    private void Update()
    {
        if (_lattice == null) return;

        // Launch だけ状態で管理。Idle/Burning は burnT の連続関数として扱いスナップを排除
        if (_state == PaperState.Launching)
        {
            UpdateLaunching();
            return;
        }

        float burnT = (_burn != null && _burn.IsBurning)
                    ? Mathf.Clamp01(_burn.BurnProgress)
                    : 0f;

        // テスト用オーバーライド（実機テストで burnT が 0 か確認するため）
        if (_debugBurnTOverride > 0f)
            burnT = _debugBurnTOverride;

        // 燃焼状態のデバッグ（Console に毎秒1回出力）
        if (Time.frameCount % 60 == 0)
            Debug.Log($"[PaperLatticeAnimator] burnT={burnT:F3}  IsBurning={_burn?.IsBurning}  BurnProgress={_burn?.BurnProgress:F3}  _burn={(object)_burn ?? "NULL"}", this);

        // 着火を初めて検出したときだけジッター開始位置を記録
        if (burnT > 0f && !_burnJitterActive)
        {
            _burnStartTime    = Time.time;
            _burnBasePos      = transform.position;
            _burnJitterActive = true;
        }

        UpdateIdleAndBurn(burnT);
    }

    /// <summary>ローンチ演出開始。TypewriterPaperOutput から呼ばれる。</summary>
    public void StartLaunch()
    {
        _state = PaperState.Launching;
        _launchElapsed = 0f;
        ResetOffsets();
    }

    // ────────────────────────────────────────────────
    // 変形ロジック
    // ────────────────────────────────────────────────

    private void UpdateIdleDebug()
    {
        float t    = Time.time;
        int   resX = _lattice.Resolution.x;
        int   resY = _lattice.Resolution.y;
        foreach (Vector3Int h in _lattice.GetHandles())
        {
            float xNorm = resX > 1 ? (float)h.x / (resX - 1) : 0.5f;
            float wave  = Mathf.Sin(t * 1.5f + xNorm * Mathf.PI * 2f) * 0.08f;
            _lattice.SetHandleOffset(h, new Vector3(0f, 0f, wave));
        }
    }

    /// <summary>
    /// Idle と Burning を burnT の連続関数として一本化。
    /// burnT=0（着火前）から burnT=1（全焼）まで状態遷移なしで滑らかに変化する。
    /// </summary>
    private void UpdateIdleAndBurn(float burnT)
    {
        float t    = Time.time;
        int   resX = _lattice.Resolution.x;
        int   resY = _lattice.Resolution.y;
        int   resZ = _lattice.Resolution.z;

        // ワールド上方向を（スケール無視・回転のみ）ラティスローカルに変換して高さ軸を判定
        Vector3 upInLocal = _lattice.transform.InverseTransformDirection(Vector3.up).normalized;
        float   aX = Mathf.Abs(upInLocal.x), aY = Mathf.Abs(upInLocal.y), aZ = Mathf.Abs(upInLocal.z);
        int     upAxis = (aX >= aY && aX >= aZ) ? 0 : (aY >= aZ ? 1 : 2);
        float   upSign = upAxis == 0 ? upInLocal.x : (upAxis == 1 ? upInLocal.y : upInLocal.z);

        // カールの向きはワールド空間で定義 → InverseTransformVector でスケール込みローカル変換
        Vector3 bendWorld = _burnCurlPositiveZ ? Vector3.forward : Vector3.back;

        foreach (Vector3Int h in _lattice.GetHandles())
        {
            float xNorm = resX > 1 ? (float)h.x / (resX - 1) : 0.5f;
            float yNorm = resY > 1 ? (float)h.y / (resY - 1) : 0.5f;
            float zNorm = resZ > 1 ? (float)h.z / (resZ - 1) : 0.5f;

            // ハンドルの高さ（0=底辺, 1=上辺）をワールドY軸基準で求める
            float rawNorm    = upAxis == 0 ? xNorm : (upAxis == 1 ? yNorm : zNorm);
            float heightNorm = upSign >= 0f ? rawNorm : 1f - rawNorm;

            _lattice.SetHandleOffset(h, _lattice.transform.InverseTransformVector(
                ComputeDeformOffset(heightNorm, xNorm, yNorm, burnT, t, bendWorld)));
        }

        // 着火直後の位置ジッター（約 1 秒でフェード）
        if (_burnJitterActive)
        {
            float sinceIgnite = t - _burnStartTime;
            float env = Mathf.Exp(-sinceIgnite * 2.5f);
            if (env > 0.01f)
            {
                transform.position = _burnBasePos + new Vector3(
                    Mathf.Sin(t * 8.3f + 0.7f) * 0.008f * env,
                    Mathf.Sin(t * 6.1f + 2.1f) * 0.005f * env,
                    0f
                );
            }
        }
    }

    // TMP テキストを紙メッシュと同じ変形式で動かす（LateUpdate で実行）
    private void LateUpdate()
    {
        if (_tmp == null || _lattice == null) return;
        if (_state == PaperState.Launching) return;

        float burnT = (_burn != null && _burn.IsBurning)
                    ? Mathf.Clamp01(_burn.BurnProgress)
                    : 0f;
        if (_debugBurnTOverride > 0f)
            burnT = _debugBurnTOverride;

        if (burnT <= 0f && !_deformInIdle) return;

        ApplyDeformToTMP(burnT);
    }

    private void ApplyDeformToTMP(float burnT)
    {
        // ForceMeshUpdate でこのフレームの TMP 頂点をリセットしてから変形を重ねる
        _tmp.ForceMeshUpdate();
        var textInfo = _tmp.textInfo;
        if (textInfo.characterCount == 0) return;

        float t = Time.time;

        // UpdateIdleAndBurn と同じ基準で高さ軸・変形方向を算出
        Vector3 upInLocal = _lattice.transform.InverseTransformDirection(Vector3.up).normalized;
        float   aX = Mathf.Abs(upInLocal.x), aY = Mathf.Abs(upInLocal.y), aZ = Mathf.Abs(upInLocal.z);
        int     upAxis = (aX >= aY && aX >= aZ) ? 0 : (aY >= aZ ? 1 : 2);
        float   upSign = upAxis == 0 ? upInLocal.x : (upAxis == 1 ? upInLocal.y : upInLocal.z);
        Vector3 bendWorld = _burnCurlPositiveZ ? Vector3.forward : Vector3.back;

        bool modified = false;

        for (int ci = 0; ci < textInfo.characterCount; ci++)
        {
            var charInfo = textInfo.characterInfo[ci];
            if (!charInfo.isVisible) continue;

            int       matIdx = charInfo.materialReferenceIndex;
            int       vtxIdx = charInfo.vertexIndex;
            Vector3[] verts  = textInfo.meshInfo[matIdx].vertices;

            for (int vi = 0; vi < 4; vi++)
            {
                // TMP ローカル → ワールドへ変換
                Vector3 worldVert = _tmp.transform.TransformPoint(verts[vtxIdx + vi]);

                // ラティスローカル座標で高さを計算（UpdateIdleAndBurn と同じ基準）
                Vector3 lp        = _lattice.transform.InverseTransformPoint(worldVert);
                float   raw       = upAxis == 0 ? lp.x + 0.5f : (upAxis == 2 ? lp.z + 0.5f : lp.y + 0.5f);
                float   heightNorm = Mathf.Clamp01(upSign >= 0f ? raw : 1f - raw);

                Vector3 worldOffset = ComputeDeformOffset(
                    heightNorm,
                    Mathf.Clamp01(lp.x + 0.5f), Mathf.Clamp01(lp.y + 0.5f),
                    burnT, t, bendWorld);
                verts[vtxIdx + vi] = _tmp.transform.InverseTransformPoint(worldVert + worldOffset);
            }
            modified = true;
        }

        if (modified)
            _tmp.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices);
    }

    /// <summary>
    /// ハンドル／TMP 頂点共通の変形オフセット（ワールド単位）を計算する。
    /// Perlin ノイズの 2 オクターブ合成（fBm）で有機的な動きを生成する。
    /// </summary>
    /// <param name="heightNorm">高さ正規化（0=底辺, 1=上辺）</param>
    /// <param name="xNorm">ラティスローカル X 位置 0-1</param>
    /// <param name="yNorm">ラティスローカル Y 位置 0-1</param>
    private Vector3 ComputeDeformOffset(
        float heightNorm, float xNorm, float yNorm,
        float burnT, float t, Vector3 bendWorld)
    {
        float nx = xNorm * 1.8f;
        float ny = yNorm * 1.4f;

        // ── Perlin 2 オクターブ fBm（波の基本ノイズ）──
        // 全ての波打ち・揺れに共通して使う有機的なベースノイズ
        float slow = Mathf.PerlinNoise(nx + t * 0.28f, ny + t * 0.19f) - 0.5f;
        float fast = (Mathf.PerlinNoise(nx * 2.2f + t * 0.62f + 4.1f, ny * 1.9f + t * 0.45f + 8.7f) - 0.5f) * 0.5f;
        float fbm  = (slow + fast) * (2f / 3f); // 実効値 ≈ [-0.33, 0.33]

        // ── 波打ち量の決定 ──
        // 燃焼中は常に揺れる。_deformInIdle で浮遊時にも揺れを追加
        float waveStrength = burnT * 0.30f + (_deformInIdle ? 0.15f : 0f);
        float waveBend = fbm * waveStrength;       // bend 方向への揺れ
        float waveUp   = fbm * waveStrength * 0.4f; // 上下方向の揺れ（立体感）

        // ── 燃焼カール（下端ほど強い）──
        float bottomFactor = (1f - heightNorm) * (1f - heightNorm);
        float curlAmount   = bottomFactor * burnT * _burnCurlAmount;

        // ── 燃焼下端のさらに激しいフラッター（火炎端のばたつき）──
        float shakeBend = 0f, shakeUp = 0f;
        if (burnT > 0f)
        {
            float sp = _burnShakeSpeed;
            float b1 = Mathf.PerlinNoise(nx * 5f + t * sp * 0.45f + 23.9f,
                                          ny * 4f + t * sp * 0.38f + 17.2f) - 0.5f;
            // 下端ほど激しく（0.3 + 0.7 * bottomEdge にすると上端も少し揺れる）
            float edgeFactor = 0.3f + 0.7f * (1f - heightNorm);
            shakeBend = b1 * edgeFactor * burnT * _burnShakeAmount;
            shakeUp   = shakeBend * 0.4f;
        }

        return Vector3.up * (curlAmount + waveUp  + shakeUp)
             + bendWorld  * (curlAmount * 0.8f + waveBend + shakeBend);
    }

    private void UpdateLaunching()
    {
        _launchElapsed += Time.deltaTime;
        float t        = Time.time;
        float progress = Mathf.Clamp01(_launchElapsed / _launchPeakTime);
        float envelope = Mathf.Sin(progress * Mathf.PI);

        int resX = _lattice.Resolution.x;
        int resY = _lattice.Resolution.y;

        foreach (Vector3Int h in _lattice.GetHandles())
        {
            float xNorm = resX > 1 ? (float)h.x / (resX - 1) - 0.5f : 0f;
            float yNorm = resY > 1 ? (float)h.y / (resY - 1) - 0.5f : 0f;

            float twist = Mathf.Sin(t * _launchTwistSpeed) * _launchTwistAmount * envelope;
            _lattice.SetHandleOffset(h, new Vector3(
                0f,
                twist * Mathf.Abs(xNorm) * 2f,
                twist * Mathf.Abs(yNorm) * 2f
            ));
        }
    }

    private void ResetOffsets()
    {
        foreach (Vector3Int h in _lattice.GetHandles())
            _lattice.SetHandleOffset(h, Vector3.zero);
    }


    // ────────────────────────────────────────────────
    // デバッグ
    // ────────────────────────────────────────────────

    [ContextMenu("Debug: Check Setup")]
    private void DebugCheckSetup()
    {
        Debug.Log($"[PaperLatticeAnimator] _lattice = {(_lattice != null ? _lattice.name : "NULL")}", this);

        var modifier = GetComponent<LatticeModifier>();
        if (modifier == null) { Debug.LogError("[PaperLatticeAnimator] LatticeModifier が見つかりません", this); return; }

        Debug.Log($"[PaperLatticeAnimator] LatticeModifier.Lattices.Count = {modifier.Lattices.Count}", this);
        for (int i = 0; i < modifier.Lattices.Count; i++)
            Debug.Log($"  [{i}] Lattice={modifier.Lattices[i].Lattice?.name ?? "NULL"}  Global={modifier.Lattices[i].Global}", this);

        var filter = GetComponent<MeshFilter>();
        if (filter != null)
        {
            var m = filter.sharedMesh;
            Debug.Log($"[PaperLatticeAnimator] mesh={m?.name}  isReadable={m?.isReadable}  vertexCount={m?.vertexCount}", this);
            if (m != null && !m.isReadable)
                Debug.LogError("[PaperLatticeAnimator] ★メッシュの Read/Write が無効です！", this);
        }

        if (_lattice != null)
        {
            int hc = 0;
            foreach (var _ in _lattice.GetHandles()) hc++;
            Debug.Log($"[PaperLatticeAnimator] Lattice handles={hc} resolution={_lattice.Resolution} localScale={_lattice.transform.localScale}", this);
        }

        Debug.Log($"[PaperLatticeAnimator] State={_state} DeformInIdle={_deformInIdle} BurnProgress={_burn?.BurnProgress ?? 0f}", this);
    }

    [ContextMenu("Debug: Force Burn Curl (test)")]
    private void DebugForceBurn()
    {
        if (_lattice == null) { Debug.LogError("_lattice が null", this); return; }

        var modifier = GetComponent<LatticeModifier>();
        int cnt = modifier?.Lattices.Count ?? -1;
        bool glob = cnt > 0 && modifier.Lattices[0].Global;
        Debug.Log($"[DebugForceBurn] Lattices.Count={cnt}  Global={glob}  lossyScale={transform.lossyScale}  LatticeLocalScale={_lattice.transform.localScale}", this);

        // 全ハンドルを lossyScale の 30% 分ずらして確実に変形が見えるか確認する
        float big = transform.lossyScale.y * 0.3f;
        foreach (Vector3Int h in _lattice.GetHandles())
            _lattice.SetHandleOffset(h, new Vector3(0f, big, big));

        Debug.Log($"[DebugForceBurn] 全ハンドルに offset Y={big:F3} Z={big:F3} を設定しました", this);
    }
}
