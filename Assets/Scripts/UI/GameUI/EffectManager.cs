using UnityEngine;
using System;
using System.Collections.Generic;
using UnityEngine.UI;
using TMPro;
using UniRx;
using UnityEngine.InputSystem;

public enum EffectType
{
    Buff,
    Debuff,
}

/// <summary>
/// エフェクトのランタイム用データを格納するクラス
/// </summary>
[System.Serializable]
public class EffectSaveData
{
    public int id;
    public int leftTurn{get;set;}
    public int level{get;set;}
}

public class EffectInstance
{
    public EffectData master;
    public int LeftTurn { get; set; }
    public int Level{get;set;} = 1;
    public int Id => master.id;
    public int InitTurn => master.turn;
    public bool IsInfinity => master.isInfinity;    
    public string EffectName => master.effectName;
    public Sprite EffectIcon => master.effectIcon;
    public string EffectDescription => master.description;
    public EffectType EffectType => master.effectType;

    public EffectSaveData CreateEffectSaveData()
    {
        EffectSaveData savedata = new EffectSaveData();
        savedata.leftTurn = LeftTurn;
        savedata.level = Level;

        return savedata;
    }
}

public class EffectManager : MonoBehaviour, IsaveDataProvider
{
    public static EffectManager Instance;
    [SerializeField] private GameUIManager _gameUIManager;
    [SerializeField] private EffectDataBase _effectDataBase;
    [SerializeField] private List<Button> _buttons;

    [Header("説明用オブジェクト")]
    [SerializeField] private TMP_Text _effectNameText;
    [SerializeField] private TMP_Text _effectTurnText;
    [SerializeField] private TMP_Text _effectExplainText;
    ReactiveCollection<EffectInstance> _ownedEffects = new ReactiveCollection<EffectInstance>();//エフェクトの情報を保持したものを集めたリスト
    void Awake()
    {
        if(Instance == null)
        {
            Instance = this;
        }
        else
            Destroy(this.gameObject);

        _ownedEffects
            .ObserveAdd()
            .Subscribe(index =>
            {
                if (index.Value == null)
                {
                    Debug.LogError("付与されたEffectInstanceがnull");
                    return;
                }
                Debug.LogWarning(index.Value.EffectName + "が付与されました");
                UpdateUI();
            }).AddTo(this);

        _ownedEffects
            .ObserveRemove()
            .Subscribe(index =>
            {
                if (index.Value == null)
                {
                    Debug.LogError("削除されたEffectInstanceがnull");
                    return;
                }
                Debug.LogWarning(index.Value.EffectName + "が解除されました");
                UpdateUI();
            }).AddTo(this);

        _effectNameText.gameObject.SetActive(false);
        _effectTurnText.gameObject.SetActive(false);
        _effectExplainText.gameObject.SetActive(false);
        UpdateUI();
    }

    void Start()
    {
        //初期化処理
        // このコンポーネントを含む GameUI プレハブはルートシーン(MainScene)側に配置されているが、
        // MoneyManager は加法ロードされるサブシーン(Scene_Environment)側に居る。
        // MultiSceneLoader の非同期ロードが終わるまで MoneyManager.Instance は null のため、
        // Start() で直接購読すると NullReferenceException になる。生成されるまで待ってから購読する。
        // 待つ対象は、この直後に参照する MoneyManager そのものにしている。PlayerWallet を待つ形でも
        // 現状は動くが、それは PlayerWallet と MoneyManager がたまたま同じ Scene_Environment に
        // 居るからで、マネージャをルートシーンへ移すなどの構成変更で壊れてしまうため。
        Observable.EveryUpdate()
            .Select(_ => MoneyManager.Instance)
            .Where(moneyManager => moneyManager != null)
            .First()
            .Subscribe(moneyManager =>
            {
                moneyManager.OnCurrentTurnChange.Skip(1).Subscribe(_ => ReduceEffectLeftTurn()).AddTo(this);
            })
            .AddTo(this);
    }

    void Update()
    {
        #if UNITY_EDITOR
        if (Keyboard.current.pKey.wasPressedThisFrame)
        {
            int random = UnityEngine.Random.Range(0,_effectDataBase.effectDataBase.Count - 1);
            AddEffect(random);
        }
#endif
    }

    public void WriteSaveData(RoguelikeSaveData saveData)
    {
        //EffectInstanceからEffectSaveDataを作成し、セーブさせる
        List<EffectSaveData> tmpEffectSaveData = new List<EffectSaveData>();

        foreach(var effect in _ownedEffects)
        {
            tmpEffectSaveData.Add(effect.CreateEffectSaveData());
        }

        saveData.ownedEffects = tmpEffectSaveData;
    }

    public void ReadSaveData(RoguelikeSaveData saveData)
    {
        _ownedEffects.Clear();
        if (saveData.ownedEffects != null)
        {
            foreach (var effectSaveData in saveData.ownedEffects)
            {
                EffectInstance instance = new EffectInstance();
                instance.master = GetEffectDataById(effectSaveData.id);
                instance.LeftTurn = effectSaveData.leftTurn;
                instance.Level = effectSaveData.level;
                _ownedEffects.Add(instance);
            }
        }
    }

    private void UpdateUI()
    {
        if (_buttons == null || _buttons.Count == 0)
        {
            Debug.LogWarning("Scroll View内にbuttonがないです");
            return;
        }

        if(_ownedEffects.Count != 0)
        {
            for (int i = 0; i < _buttons.Count; i++)
            {
                if (i < _ownedEffects.Count)
                {
                    var effect = _ownedEffects[i];
                    _buttons[i].gameObject.SetActive(true);
                    SetButtonUI(_buttons[i].gameObject, effect);

                    _buttons[i].onClick.RemoveAllListeners();
                    _buttons[i].onClick.AddListener(() => OnSelectEffect(effect));
                }
                else
                {
                    _buttons[i].gameObject.SetActive(false);
                }
            }
        }
        else
        {
            for (int i = 0; i < _buttons.Count; i++)
                _buttons[i].gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// ボタンのUIをアイテムに応じて変更させる
    /// </summary>
    private void SetButtonUI(GameObject btnObj, EffectInstance effect)
    {
        Image ground = btnObj.GetComponent<Image>();
        Image image = btnObj.transform.Find("EffectImage").GetComponent<Image>();

        if(ground != null)
        {
            if(effect.EffectType == EffectType.Buff)    ground.color = Color.green;
            else ground.color = Color.red;
        }

        if(image != null)
            image.sprite = effect.EffectIcon;
    }

    /// <summary>
    /// アイテムが選択された際の処理
    /// </summary>
    private void OnSelectEffect(EffectInstance effect)
    {
        if (effect == null) return;

        if (_effectNameText != null)
        {
            _effectNameText.text = "Effect Name: " + effect.EffectName;
            _effectNameText.gameObject.SetActive(true);
        }

        if (_effectTurnText != null)
        {
            if(effect.IsInfinity)
                _effectTurnText.text = "Left Turn: Eternal";
            else
                _effectTurnText.text = "Left Turn: " + effect.LeftTurn.ToString();

            _effectTurnText.gameObject.SetActive(true);
        }

        if (_effectExplainText != null)
        {
            _effectExplainText.text = effect.EffectDescription.ToString();
            _effectExplainText.gameObject.SetActive(true);
        }
    }

    /// <summary>
    /// IDからEffectDataを取得する
    /// </summary>
    private EffectData GetEffectDataById(int id)
    {
        if (_effectDataBase == null || _effectDataBase.effectDataBase == null)
        {
            return null;
        }
        return _effectDataBase.effectDataBase.Find(effect => effect.id == id);
    }

    public bool IsHasEffect(int id)
    {
        if(_ownedEffects.Count == 0 || id == -1) return false;
        
        foreach(var effect in _ownedEffects)
        {
            if(effect.Id == id)
                return true;
        }
        return false;
    }

    /// <summary>
    /// アイテムの名前から対応するエフェクトのidを返す関数。アイテム使用時に相手に対応するidを返すためだけに使用すること
    /// </summary>
    /// <param name="itemName"></param>
    /// <returns></returns>
    public int GetIdByItemName(string itemName)
    {
        var id = _effectDataBase.effectDataBase.Find(effect => effect.effectName == itemName+" Effect").id;
        if(id == 0)
            return -1;
        else
            return id;
    }

    public void AddEffect(int id)
    {
        Debug.LogError("付与しようとしているエフェクト：" + GetEffectDataById(id).effectName);
        bool exists = false;

        if(_ownedEffects.Count != 0)
        {
            foreach (var effect in _ownedEffects)
            {
                if (effect.Id == id)
                {
                    exists = true;
                    break;
                }
            }
        }

        if (!exists)
        {
            EffectData original = GetEffectDataById(id);
            if (original != null)
            {
                EffectInstance instance = new EffectInstance();
                instance.master = original;
                instance.LeftTurn = original.turn;
                instance.Level = 1;
                _ownedEffects.Add(instance);
                _gameUIManager.AddPopupQueue(true,instance.master);
            }
            else
            {
                Debug.LogError("指定されたIDは存在しません");
            }
        }
        
        UpdateUI();
    }

    /// <summary>
    /// 指定されたIDのアイテムを所持リストから削除し、UI更新を行う
    /// </summary>
    public void RemoveEffect(int id)
    {
        foreach (var effect in _ownedEffects)
        {
            if (effect != null && effect.Id == id)
            {
                _ownedEffects.Remove(effect);
                _gameUIManager.AddPopupQueue(true,effect.master);
                UpdateUI();
                break;
            }
        }
    }

    /// <summary>
    /// ターン経過後に、呼び出してターン数を減少させる
    /// </summary>
    public void ReduceEffectLeftTurn()
    {
        Debug.LogWarning("エフェクトのターンを減少する");
        for (int i = _ownedEffects.Count - 1; i >= 0; i--)
        {
            if(_ownedEffects[i].IsInfinity)
                continue;

            _ownedEffects[i].LeftTurn --;
            if(_ownedEffects[i].LeftTurn <= 0)//残りターンがゼロになった時の処理
            {
                _ownedEffects.Remove(_ownedEffects[i]);
            }
        }

        UpdateUI();
        _effectNameText.gameObject.SetActive(false);
        _effectTurnText.gameObject.SetActive(false);
        _effectExplainText.gameObject.SetActive(false);
    }
}
