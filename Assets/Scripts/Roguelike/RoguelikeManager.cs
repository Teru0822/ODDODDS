using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using System.Linq;
using UniRx;

public class RoguelikeManager : MonoBehaviour
{
    private Dictionary<SkillId, RoguelikeData> _roguelikeDictionary = new Dictionary<SkillId, RoguelikeData>();//SkillId: ID, RoguelikeData:ローグライク用のスキルに関するデータ
    [SerializeField] private string _jsonFilePath = "Assets/Resources/Roguelike/RoguelikeData.json";


    /// <summary>
    /// 現在アンロックされているスキルのみを集めたDictionaryを返す
    /// </summary>
    public Dictionary<SkillId, RoguelikeData> GetUnlockSkillDictionary => _roguelikeDictionary.Where(data => data.Value.isGet == true)
        .ToDictionary(data => data.Key, data => data.Value);

    /// <summary>デバッグ用：全スキルへの読み取り専用アクセス</summary>
    public IReadOnlyDictionary<SkillId, RoguelikeData> AllSkillsDebug => _roguelikeDictionary;

    /// <summary>デバッグ用：未取得スキルを全件リスト形式で返す（取得済みは除外）</summary>
    public List<RoguelikeData> GetAllSkills() =>
        _roguelikeDictionary.Values.Where(s => !s.isGet).ToList();

    /// <summary>
    /// デバッグ用：スキルをロック状態に戻す。
    /// UnlockSkill で適用した副作用（アームレベル変更・オブジェクト非表示等）は巻き戻さない。
    /// </summary>
    public void LockSkill(RoguelikeData data)
    {
        if (!_roguelikeDictionary.ContainsKey(data.id)) return;
        _roguelikeDictionary[data.id].isGet = false;
        if (RoguelikePanelManager.Instance != null)
            RoguelikePanelManager.Instance.UpdateUI();
        Debug.Log($"[RoguelikeManager] LockSkill: [{data.id}] {data.skillName}");
    }


    public RoguelikeManager MyRoguelikeManager { get; private set; }

    private Subject<RoguelikeData> _unlockSkillEvent = new Subject<RoguelikeData>();//スキルがアンロックされた際のイベント（intにはidが入る）
    public IObservable<RoguelikeData> OnUnlockSkillEvent { get { return _unlockSkillEvent; } }

    private void Awake()
    {
        LoadRoguelikeData();
    }

    private void Start()
    {
        //TODO:マルチプレイになった際には、自分のRoguelikeManagerが取得できるようにする
        MyRoguelikeManager = this;
        if (RoguelikePanelManager.Instance != null)
            RoguelikePanelManager.Instance.OnInitEvent.OnNext(MyRoguelikeManager);
        if (PinballBallManager.Instance != null)
            PinballBallManager.Instance.OnInitEvent.OnNext(MyRoguelikeManager);
    }

    private void Update()
    {
#if UNITY_EDITOR
        if (Keyboard.current.iKey.wasPressedThisFrame)
        {
            var list = GetLockSkills(2);

            if (list == null) return;

            foreach (var skill in list)
            {
                UnlockSkill(skill);
            }

            var tmpDic = GetUnlockSkillDictionary;
            foreach (var skill2 in tmpDic)
            {
                Debug.LogError(skill2.Value.skillName + "は解放済みです");
            }
        }
#endif
    }

    /// <summary>
    /// 現在アンロックされていないスキルに関する情報を任意の数だけList形式で与える
    /// </summary>
    /// <param name="num">欲しいスキル情報の数</param>
    /// <param name="type">何に関するスキルを優先的に得るか</param>
    /// <returns></returns>
    public List<RoguelikeData> GetLockSkills(int num, SkillType type = SkillType.None)
    {
        List<RoguelikeData> tmpList = new List<RoguelikeData>();

        //既にゲットしてるスキルは除外
        if (type == SkillType.None)
        {
            foreach (var skill in _roguelikeDictionary)
            {
                if (skill.Value.isGet)
                    continue;
                else
                    tmpList.Add(skill.Value);
            }
        }
        else //特定のタイプのスキルを優先して獲得させたい場合
        {
            foreach (var skill in _roguelikeDictionary)
            {
                if (skill.Value.isGet || skill.Value.skillType != type)
                    continue;
                else
                    tmpList.Add(skill.Value);
            }
        }

        if (tmpList.Count == 0)
        {
            Debug.LogError("もうすべて取得済みです");
            return null;
        }

        // 残りスキル数が要求数より少ない場合、無限ループを防ぐために上限を制限
        num = Mathf.Min(num, tmpList.Count);

        //アンロックのスキルの中から抽選する
        List<RoguelikeData> result = new List<RoguelikeData>();
        int random = UnityEngine.Random.Range(0, tmpList.Count);
        for (int i = 0; i < num; i++)
        { 
            if (result.Count != 0)
            {
                while(true)//重複しない結果になるまでランダムで抽選
                {
                    random = UnityEngine.Random.Range(0, tmpList.Count);
                    bool isCheck = true;//同じスキルが抽選されているか否か（trueなら抽選されていない）
                    foreach (var data in result)
                    {
                        if (data.id != tmpList[random].id)
                            continue;
                        else
                            isCheck = false;
                    }

                    if (isCheck) break;
                }
            }

            result.Add(tmpList[random]);
            Debug.Log("抽選結果[" + i + "] = " + tmpList[random].skillName);
        }
        return result;
    }

    /// <summary>
    /// スキルのアンロックを行う関数
    /// </summary>
    /// <param name="data"></param>
    public void UnlockSkill(RoguelikeData data)
    {
        if (_roguelikeDictionary.ContainsKey(data.id))
        {
            _roguelikeDictionary[data.id].isGet = true;

            // 【変更点】数字の if 文を enum の switch 文に置き換え
            switch (data.id)
            {
                case SkillId.UFO_ArmLevel2:
                    ApplySkill8Effects(); // ※後々 ApplyArmLevel2Effects() などにリネームするとさらに綺麗です
                    break;
                case SkillId.UFO_ArmLevel3:
                    ApplySkill9Effects();
                    break;
                case SkillId.UFO_ExpandDropHole1:
                    ApplySkill13Effects();
                    break;
                case SkillId.UFO_ExpandDropHole2:
                    ApplySkill14Effects();
                    break;
                case SkillId.UFO_ExpandDropHole3:
                    ApplySkill15Effects();
                    break;
                case SkillId.UFO_PolishDiamond1:
                case SkillId.UFO_PolishDiamond2:
                case SkillId.UFO_PolishDiamond3:
                    ApplyPolishDiamondEffects();
                    break;
                case SkillId.UFO_ArmSpeedUp1:
                case SkillId.UFO_ArmSpeedUp2:
                    ApplyArmSpeedEffects();
                    break;
                case SkillId.UFO_ReduceSway1:
                case SkillId.UFO_ReduceSway2:
                case SkillId.UFO_ReduceSway3:
                    ApplySwayEffects();
                    break;
                case SkillId.UFO_ExpandItems1:
                    ApplySkill26Effects();
                    break;
                case SkillId.UFO_ExpandItems2:
                    ApplySkill27Effects();
                    break;
                case SkillId.UFO_ItemDropCoin:
                    ApplySkill16Effects();
                    break;
                case SkillId.UFO_Unlock90Sec:
                    ApplySkill31Effects();
                    break;
            }

            // UIの更新を行っておく
            if (RoguelikePanelManager.Instance != null)
                RoguelikePanelManager.Instance.UpdateUI();
            _unlockSkillEvent.OnNext(data);
        }
        else
        {
            Debug.LogError($"指定されたキーのスキル({data.id})は存在しません。");
        }
    }

    /// <summary>
    /// 任意のルートオブジェクト配下の子孫を SetActive する汎用ヘルパー
    /// </summary>
    private void SetChildActive(string rootName, string childName, bool active, string callerLabel)
    {
        GameObject root = GameObject.Find(rootName);
        if (root == null)
        {
            Debug.LogWarning($"[RoguelikeManager] {callerLabel}: {rootName} が見つかりません。");
            return;
        }

        Transform hit = FindDeep(root.transform, childName);
        if (hit != null)
            hit.gameObject.SetActive(active);
        else
            Debug.LogWarning($"[RoguelikeManager] {callerLabel}: {childName} が {rootName} 配下に見つかりません。");
    }

    /// <summary>lock_main 配下の子を非表示にする便利ラッパー</summary>
    private void HideLockMainChild(string childName, string callerLabel)
        => SetChildActive("lock_main", childName, false, callerLabel);

    /// <summary>スキルID8取得時：アームを Lv2 に切り替える</summary>
    private void ApplySkill8Effects()
    {
        if (UFOArmManager.Instance != null)
            UFOArmManager.Instance.SetArmLevel(2);
        else
            Debug.LogWarning("[RoguelikeManager] ApplySkill8Effects: UFOArmManager が見つかりません。");
    }

    /// <summary>スキルID9取得時：アームを Lv3 に切り替える</summary>
    private void ApplySkill9Effects()
    {
        if (UFOArmManager.Instance != null)
            UFOArmManager.Instance.SetArmLevel(3);
        else
            Debug.LogWarning("[RoguelikeManager] ApplySkill9Effects: UFOArmManager が見つかりません。");
    }

    /// <summary>スキルID16取得時：クレーンゲームにルーレット機能（Devil_Eye）を解放する</summary>
    private void ApplySkill16Effects()
    {
        // RouletteController は Devil_Eye と一緒に非アクティブな状態からスタートする構成のため、
        // Awake() が未実行で Instance が null のことがある。その場合は非アクティブ含みで検索する。
        var roulette = RouletteController.Instance;
        if (roulette == null)
            roulette = FindFirstObjectByType<RouletteController>(FindObjectsInactive.Include);

        if (roulette != null)
            roulette.Unlock();
        else
            Debug.LogWarning("[RoguelikeManager] ApplySkill16Effects: RouletteController が見つかりません。");
    }

    /// <summary>スキルID26取得時：UFOキャッチャーの中身（降らせる枚数）を1000枚に増やす</summary>
    private void ApplySkill26Effects()
    {
        if (ItemSpawner.Instance != null)
            ItemSpawner.Instance.SetSpawnCount(SpawnCountType.Count1000);
        else
            Debug.LogWarning("[RoguelikeManager] ApplySkill26Effects: ItemSpawner が見つかりません。");
    }

    /// <summary>スキルID27取得時：UFOキャッチャーの中身（降らせる枚数）を1500枚に増やす</summary>
    private void ApplySkill27Effects()
    {
        if (ItemSpawner.Instance != null)
            ItemSpawner.Instance.SetSpawnCount(SpawnCountType.Count1500);
        else
            Debug.LogWarning("[RoguelikeManager] ApplySkill27Effects: ItemSpawner が見つかりません。");
    }

    /// <summary>スキルID31取得時：クレーンゲームのプレイ時間90秒を解禁する（Rock_90非表示・TouchPanelの90選択可能に）</summary>
    private void ApplySkill31Effects()
    {
        var touchPanel = FindFirstObjectByType<TouchPanelOutlineController>();
        if (touchPanel != null)
            touchPanel.UnlockDuration(90f);
        else
            Debug.LogWarning("[RoguelikeManager] ApplySkill31Effects: TouchPanelOutlineController が見つかりません。");
    }

    /// <summary>子孫を再帰的に名前検索する</summary>
    private Transform FindDeep(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name) return child;
            var found = FindDeep(child, name);
            if (found != null) return found;
        }
        return null;
    }

    /// <summary>スキルID13取得時：bolt3・main3・pkn31〜pkn34 を非表示</summary>
    private void ApplySkill13Effects()
    {
        HideLockMainChild("bolt3", "ApplySkill13Effects");
        HideLockMainChild("main3", "ApplySkill13Effects");
        HideLockMainChild("pkn31", "ApplySkill13Effects");
        HideLockMainChild("pkn32", "ApplySkill13Effects");
        HideLockMainChild("pkn33", "ApplySkill13Effects");
        HideLockMainChild("pkn34", "ApplySkill13Effects");
    }

    /// <summary>スキルID14取得時：bolt2・main2・pkn21〜pkn24 を非表示</summary>
    private void ApplySkill14Effects()
    {
        HideLockMainChild("bolt2", "ApplySkill14Effects");
        HideLockMainChild("main2", "ApplySkill14Effects");
        HideLockMainChild("pkn21", "ApplySkill14Effects");
        HideLockMainChild("pkn22", "ApplySkill14Effects"); // 「okn22」は pkn22 の誤字として処理
        HideLockMainChild("pkn23", "ApplySkill14Effects");
        HideLockMainChild("pkn24", "ApplySkill14Effects");
    }

    /// <summary>スキルID15取得時：bolt1・main1・pkn11〜pkn14 を非表示</summary>
    private void ApplySkill15Effects()
    {
        HideLockMainChild("bolt1", "ApplySkill15Effects");
        HideLockMainChild("main1", "ApplySkill15Effects");
        HideLockMainChild("pkn11", "ApplySkill15Effects");
        HideLockMainChild("pkn12", "ApplySkill15Effects");
        HideLockMainChild("pkn13", "ApplySkill15Effects");
        HideLockMainChild("pkn14", "ApplySkill15Effects");
    }

    /// <summary>スキルID17〜19取得時にダイヤモンドを磨く段階を進める</summary>
    private void ApplyPolishDiamondEffects()
    {
        var polishers = FindObjectsByType<DiamondPolisher>(FindObjectsSortMode.None);
        foreach (var polisher in polishers)
        {
            if (polisher != null)
            {
                polisher.PolishDiamond();
            }
        }
    }

    /// <summary>スキルID22, 23取得時にアームの速度パラメータを変更する</summary>
    private void ApplyArmSpeedEffects()
    {
        var controller = FindFirstObjectByType<UFOArmController>();
        if (controller != null)
        {
            controller.UpdateArmSpeedBySkills();
        }
        else
        {
            Debug.LogWarning("[RoguelikeManager] ApplyArmSpeedEffects: UFOArmController が見つかりません。");
        }
    }

    /// <summary>スキルID20, 21, 25取得時にアームの揺れパラメータおよびシリンダー表示を変更する</summary>
    private void ApplySwayEffects()
    {
        var controller = FindFirstObjectByType<UFOArmController>();
        if (controller != null)
        {
            controller.UpdateSwayBySkills();
        }
        else
        {
            Debug.LogWarning("[RoguelikeManager] ApplySwayEffects: UFOArmController が見つかりません。");
        }

        var armManager = FindFirstObjectByType<UFOArmManager>();
        if (armManager != null)
        {
            armManager.UpdateCylinderBySkills();
        }
        else
        {
            Debug.LogWarning("[RoguelikeManager] ApplySwayEffects: UFOArmManager が見つかりません。");
        }
    }


    /// <summary>
    /// 指定されたパスのJSONファイルから会話データを読み込み、Dictionaryに格納
    /// </summary>
    private void LoadRoguelikeData()
    {
        // プロジェクトルート相対パスに対応するため、絶対パスを構築
        string fullPath = _jsonFilePath;
        if (!Path.IsPathRooted(fullPath))
        {
            fullPath = Path.Combine(Application.dataPath, "..", _jsonFilePath);
        }

        if (File.Exists(fullPath))
        {
            try
            {
                string json = File.ReadAllText(fullPath);
                RoguelikeDataContainer container = JsonConvert.DeserializeObject<RoguelikeDataContainer>(json);

                _roguelikeDictionary.Clear();
                if (container != null && container.roguelikeDatas != null)
                {
                    foreach (var data in container.roguelikeDatas)
                    {

                        _roguelikeDictionary.Add(data.id, data);

                        //デバッグ用データ確認
                        Debug.Log($"RoguelkeID: {_roguelikeDictionary[data.id].id}\nskillName: {_roguelikeDictionary[data.id].skillName}\nskillType: {_roguelikeDictionary[data.id].skillType}\ndescibe: {_roguelikeDictionary[data.id].skillDescription}\n");
                        
                    }
                    Debug.Log($"ローグライク用のデータをロードしました。総件数: {_roguelikeDictionary.Count} 件 (パス: {fullPath})");
                }
                else
                {
                    Debug.LogWarning("JSONのパース結果が空です、またはフォーマットが異なります。");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"ローグライク用データのロード中にエラーが発生しました: {e.Message}");
            }
        }
        else
        {
            Debug.LogError($"ローグライク用データファイルが見つかりません。パス: {fullPath}");
        }
    }
}
