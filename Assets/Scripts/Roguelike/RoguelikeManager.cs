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
    private Dictionary<int,RoguelikeData> _roguelikeDictionary = new Dictionary<int, RoguelikeData>();//int: ID, RoguelikeData:ローグライク用のスキルに関するデータ
    [SerializeField] private string _jsonFilePath = "Assets/Resources/Roguelike/RoguelikeData.json";


    /// <summary>
    /// 現在アンロックされているスキルのみを集めたDictionaryを返す
    /// </summary>
    public Dictionary<int, RoguelikeData> GetUnlockSkillDictionary => _roguelikeDictionary.Where(data => data.Value.isGet == true)
        .ToDictionary(data => data.Key, data => data.Value);


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
        RoguelikePanelManager.Instance.OnInitEvent.OnNext(MyRoguelikeManager);
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
        else　//特定のタイプのスキルを優先して獲得させたい場合
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

            // スキルID専用の演出処理
            if (data.id == 13) ApplySkill13Effects();
            else if (data.id == 14) ApplySkill14Effects();
            else if (data.id == 15) ApplySkill15Effects();

            //UIの更新を行っておく
            RoguelikePanelManager.Instance.UpdateUI();
            _unlockSkillEvent.OnNext(data);
        }
        else
        {
            Debug.LogError("指定されたキーのスキルは存在しません。");
            return;
        }
    }

    /// <summary>
    /// lock_main 配下の指定名オブジェクトを非表示にする共通ヘルパー
    /// </summary>
    private void HideLockMainChild(string childName, string callerLabel)
    {
        GameObject lockMain = GameObject.Find("lock_main");
        if (lockMain == null)
        {
            Debug.LogWarning($"[RoguelikeManager] {callerLabel}: lock_main が見つかりません。");
            return;
        }

        Transform hit = FindDeep(lockMain.transform, childName);
        if (hit != null)
            hit.gameObject.SetActive(false);
        else
            Debug.LogWarning($"[RoguelikeManager] {callerLabel}: {childName} が lock_main 配下に見つかりません。");
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
