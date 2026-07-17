using UnityEngine;

// ダイヤのPrefabにアタッチするスクリプト
[RequireComponent(typeof(Renderer))]
public class DiamondPolisher : MonoBehaviour
{
    [Header("磨きの段階設定 (0=ダークマター, 1=完全なダイヤ)")]
    [SerializeField] private float[] polishLevels = { 0.0f, 0.3f, 0.7f, 1.0f };

    // 現在の磨き段階 (0 ～ 3)
    private int currentStage = 0;

    // 個別マテリアルのキャッシュ
    private Material myMaterial;

    // シェーダープロパティのIDをキャッシュ（文字列で毎回検索するより高速です）
    private readonly int polishLevelId = Shader.PropertyToID("_PolishLevel");

    private void Start()
    {
        // 獲得済みのスキル数（ID: 17〜19）に応じて初期段階を設定する
        var manager = FindFirstObjectByType<RoguelikeManager>();
        if (manager != null)
        {
            int unlockedPolishSkills = 0;
            var skills = manager.GetUnlockSkillDictionary;
            if (skills != null)
            {
                if (skills.ContainsKey(SkillId.UFO_PolishDiamond1)) unlockedPolishSkills++;
                if (skills.ContainsKey(SkillId.UFO_PolishDiamond2)) unlockedPolishSkills++;
                if (skills.ContainsKey(SkillId.UFO_PolishDiamond3)) unlockedPolishSkills++;
            }
            currentStage = Mathf.Clamp(unlockedPolishSkills, 0, polishLevels.Length - 1);
        }

        // ゲーム開始時に、このダイヤ専用のマテリアルインスタンスを生成してキャッシュ
        // ※ .material を一度呼ぶことで、他のダイヤに影響を与えなくなります
        Renderer rend = GetComponent<Renderer>();
        if (rend != null)
        {
            myMaterial = rend.material;
            
            // 初期状態をマテリアルに反映
            UpdateShader();
        }
    }

    /// <summary>
    /// 外部（プレイヤーのクリックやアクション）から呼ばれるメソッド
    /// 呼ばれるたびに1段階ずつ磨かれていく
    /// </summary>
    public void PolishDiamond()
    {
        // 既に最大段階（完全なダイヤ）なら何もしない
        if (currentStage >= polishLevels.Length - 1)
        {
            Debug.Log("これ以上磨けません！すでに完璧なダイヤモンドです。");
            return;
        }

        // 段階を1つ進める
        currentStage++;
        Debug.Log($"ダイヤを磨きました！ 現在の段階: {currentStage}");

        // シェーダーに新しい数値を送る
        UpdateShader();
    }

    /// <summary>
    /// 現在の段階に応じた数値をシェーダーに適用する
    /// </summary>
    private void UpdateShader()
    {
        if (myMaterial != null)
        {
            // 配列から現在の段階のFloat値を取得し、マテリアルにセットする
            float targetValue = polishLevels[currentStage];
            myMaterial.SetFloat(polishLevelId, targetValue);
        }
    }

    private void OnDestroy()
    {
        // .materialで複製されたマテリアルは、オブジェクト破棄時に手動でメモリ解放するのが安全です
        if (myMaterial != null)
        {
            Destroy(myMaterial);
        }
    }
}
