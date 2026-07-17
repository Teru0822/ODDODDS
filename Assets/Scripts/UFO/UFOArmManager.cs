using UnityEngine;

/// <summary>
/// アームの強化（プレハブの切り替え）を管理するマネージャークラス。
/// UFOキャッチャー本体のオブジェクトや、空のオブジェクトにアタッチして使います。
/// </summary>
public class UFOArmManager : MonoBehaviour
{
    public static UFOArmManager Instance { get; private set; }

    [Tooltip("操作するUFOキャッチャー本体のController")]
    public UFOArmController ufoController;

    [Tooltip("レベル1（初期状態）〜最大レベルまでのアームの親オブジェクト（シーン上のもの）を順に登録します")]
    public GameObject[] armSetsByLevel;

    [Header("シリンダー表示設定 (揺れ抑制スキル ID: 20, 21, 25 に連動)")]
    [Tooltip("揺れ抑制初期のシリンダーオブジェクト")]
    public GameObject cylinder;
    [Tooltip("揺れ抑制1段階目（ID 20）のシリンダーオブジェクト")]
    public GameObject cylinder1;
    [Tooltip("揺れ抑制2段階目（ID 21）のシリンダーオブジェクト")]
    public GameObject cylinder2;
    [Tooltip("揺れ抑制3段階目（ID 25）のシリンダーオブジェクト")]
    public GameObject cylinder3;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        UpdateCylinderBySkills();
    }

    /// <summary>
    /// スキル取得状況（ID: 20, 21, 25）をチェックして、シリンダーの表示状態を更新する
    /// </summary>
    public void UpdateCylinderBySkills()
    {
        var manager = FindFirstObjectByType<RoguelikeManager>();
        if (manager == null)
        {
            ApplyCylinderLevel(0);
            return;
        }

        var skills = manager.GetUnlockSkillDictionary;
        if (skills == null)
        {
            ApplyCylinderLevel(0);
            return;
        }

        int level = 0;
        if (skills.ContainsKey(SkillId.UFO_ReduceSway1)) level = 1;
        if (skills.ContainsKey(SkillId.UFO_ReduceSway2)) level = 2;
        if (skills.ContainsKey(SkillId.UFO_ReduceSway3)) level = 3;

        ApplyCylinderLevel(level);
    }

    private void ApplyCylinderLevel(int level)
    {
        // まず全非表示
        if (cylinder != null) cylinder.SetActive(false);
        if (cylinder1 != null) cylinder1.SetActive(false);
        if (cylinder2 != null) cylinder2.SetActive(false);
        if (cylinder3 != null) cylinder3.SetActive(false);

        // レベルに応じて必要なシリンダーを表示
        switch (level)
        {
            case 1:
                if (cylinder1 != null) cylinder1.SetActive(true);
                Debug.Log("[UFOArmManager] Cylinder1 を表示しました (Sway Level 1)");
                break;
            case 2:
                if (cylinder2 != null) cylinder2.SetActive(true);
                Debug.Log("[UFOArmManager] Cylinder2 を表示しました (Sway Level 2)");
                break;
            case 3:
                if (cylinder3 != null) cylinder3.SetActive(true);
                Debug.Log("[UFOArmManager] Cylinder3 を表示しました (Sway Level 3)");
                break;
            default:
                if (cylinder != null) cylinder.SetActive(true);
                Debug.Log("[UFOArmManager] 初期 Cylinder を表示しました (Sway Level 0)");
                break;
        }
    }

    /// <summary>
    /// 指定されたレベルのアームに交換する
    /// </summary>
    /// <param name="level">現在のレベル（1〜）</param>
    public void SetArmLevel(int level)
    {
        if (ufoController == null)
        {
            Debug.LogWarning("UFOArmManager: ufoControllerが設定されていません！");
            return;
        }

        if (armSetsByLevel == null || armSetsByLevel.Length == 0)
        {
            Debug.LogWarning("UFOArmManager: アームのオブジェクトが登録されていません！");
            return;
        }

        // レベルは1始まりと想定し、配列インデックス（0〜）に変換。配列の最大数を超えないようにクリップする。
        int index = Mathf.Clamp(level - 1, 0, armSetsByLevel.Length - 1);

        // 【修正】現在稼働している古いアーム（Lv1など配列に入っていないもの含む）を確実に非表示にする
        if (ufoController.fingerParts != null && ufoController.fingerParts.Length > 0 && ufoController.fingerParts[0] != null)
        {
            // fingerParts[0] から親を上に辿って、armRoot（大元の吊り下げ部）の直下にあるルートオブジェクトを探す
            Transform t = ufoController.fingerParts[0];
            while (t != null && t.parent != null && t.parent != ufoController.armRoot)
            {
                t = t.parent;
            }
            
            // 見つかった大元のオブジェクト（structureなど）を非表示にする
            if (t != null)
            {
                t.gameObject.SetActive(false);
            }
        }

        // 一旦配列内のすべてのアームおよびその大元（親構造）を非表示にする
        for (int i = 0; i < armSetsByLevel.Length; i++)
        {
            if (armSetsByLevel[i] != null)
            {
                // 登録されたオブジェクト（フィンガー等）自体を非表示にする
                armSetsByLevel[i].SetActive(false);

                // 大元の structure オブジェクトを親方向に辿って非表示にする
                Transform t = armSetsByLevel[i].transform;
                while (t != null && t.parent != null && t.parent != ufoController.armRoot)
                {
                    t = t.parent;
                }
                if (t != null)
                {
                    t.gameObject.SetActive(false);
                }
            }
        }

        // 目標のアームだけを表示して適用する
        GameObject targetArm = armSetsByLevel[index];
        if (targetArm != null)
        {
            // 登録されたオブジェクト（フィンガー等）自体を表示する
            targetArm.SetActive(true);

            // 大元の structure オブジェクトを親方向に辿って表示する
            Transform t = targetArm.transform;
            while (t != null && t.parent != null && t.parent != ufoController.armRoot)
            {
                t = t.parent;
            }
            if (t != null)
            {
                t.gameObject.SetActive(true);
            }

            ufoController.ChangeClaw_InScene(targetArm);
            Debug.Log($"UFOArmManager: アームを Lv{level} (インデックス:{index}) に切り替えました！");
        }
    }
}
