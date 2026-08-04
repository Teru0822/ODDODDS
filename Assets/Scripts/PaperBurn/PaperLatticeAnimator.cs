using Lattice;
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
    [SerializeField, Range(0f, 1f)]   private float _burnCurlAmount   = 0.40f;
    [SerializeField, Range(0f, 15f)]  private float _burnShakeSpeed   = 6f;
    [SerializeField, Range(0f, 0.1f)] private float _burnShakeAmount  = 0.025f;
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

        AutoWireModifier();

        _burn = GetComponent<RealisticPaperBurn>();
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
        var list = modifier.Lattices;
        if (list.Count == 0)
        {
            list.Add(new LatticeItem
            {
                Lattice       = _lattice,
                Interpolation = InterpolationMethod.LinearSmooth,
                Global        = true,
                Mask          = new LatticeMask { Vertex = new LatticeMask.VertexSettings { Multiplier = 1f } }
            });
        }
        else
        {
            var item = list[0];
            item.Lattice = _lattice;
            item.Global  = true;
            list[0] = item;
        }
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
        float t     = Time.time;
        int   resX  = _lattice.Resolution.x;
        int   resY  = _lattice.Resolution.y;
        float zSign = _burnCurlPositiveZ ? 1f : -1f;

        foreach (Vector3Int h in _lattice.GetHandles())
        {
            float xNorm = resX > 1 ? (float)h.x / (resX - 1) : 0.5f;
            float yNorm = resY > 1 ? (float)h.y / (resY - 1) : 0.5f;

            // Z 波打ち：Idle/Burning 共通。常に同じ式なので状態切り替えで値が飛ばない
            float wave = _deformInIdle
                       ? Mathf.Sin(t * 1.5f + xNorm * Mathf.PI * 2f) * 0.08f
                       : 0f;

            // カール：Y（持ち上がり）＋ Z（手前/奥への湾曲）を同時にかける
            // → 底辺が「回転」ではなく「丸まる」ように見える
            float bottomFactor = (1f - yNorm) * (1f - yNorm);
            float curlAmount   = bottomFactor * burnT * _burnCurlAmount;
            float curlY        = curlAmount;
            float curlZ        = curlAmount * 0.8f * zSign;

            // Z シェイク：burnT が大きいほど揺れる
            float shake = Mathf.Sin(t * _burnShakeSpeed + h.x * 1.73f + h.y * 2.31f)
                        * _burnShakeAmount * burnT;

            _lattice.SetHandleOffset(h, new Vector3(0f, curlY, wave + curlZ + shake));
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
        AutoWireModifier();
        int resX = _lattice.Resolution.x;
        int resY = _lattice.Resolution.y;
        foreach (Vector3Int h in _lattice.GetHandles())
        {
            float xNorm    = resX > 1 ? (float)h.x / (resX - 1) : 0.5f;
            float yNorm    = resY > 1 ? (float)h.y / (resY - 1) : 0.5f;
            float edgeDist = Mathf.Max(Mathf.Abs(xNorm - 0.5f), Mathf.Abs(yNorm - 0.5f)) * 2f;
            _lattice.SetHandleOffset(h, new Vector3(0f, 0f, edgeDist * edgeDist * _burnCurlAmount));
        }
        Debug.Log("[PaperLatticeAnimator] DebugForceBurn 完了", this);
    }
}
