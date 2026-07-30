using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using UniRx;
using TMPro;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using System.Linq;
using DG.Tweening;

/// <summary>
/// Jsonファイルにセーブする、ランタイム時に必要なデータをまとめたクラス
/// </summary>
public class VisitorSaveData
{
    public int id;
    public int eventProgress;
}

/// <summary>
/// 訪問者に関する情報をまとめたクラス
/// </summary>
public class VisitorInstance
{
    public VisitorData master;
    [Range(1,5)]public int eventProgress = 1;//イベントの進行状況
    public bool isCheck = false;//抽選時に確かめたか否かを判断するBool.抽選処理終了後に必ずFalseにすること
    public string key = "";//現在選択中のキー
    public int Id => master.id;
    public string VisitorName => master.visitorName;
    public List<Request> Requests => master.requests;
    public List<Reward> Rewards => master.rewards;
    public Dictionary<string, VisitorConversationContainer[]> Conversations => master.conversations.GetDictionary;

    public VisitorSaveData CreateVisitorSaveData()
    {
        VisitorSaveData savedata = new VisitorSaveData();
        savedata.id = Id;
        savedata.eventProgress = eventProgress;

        return savedata;
    }
}

public class VisitorSystem : MonoBehaviour,IsaveDataProvider
{
    [SerializeField] private VisitorDataBase _visitorDataBase;
    [SerializeField] private List<GameObject> _visitorPrefabs;
    [SerializeField] private GameObject _visitorSpawnPoint;
    private GameObject _visitor;
    private List<VisitorInstance> _visitorInstances = new List<VisitorInstance>();
    private ReactiveProperty<VisitorInstance> _nowSelectedVisitorInstance = new ReactiveProperty<VisitorInstance>(null);
    public IObservable<VisitorInstance> OnSelectVisitorInstance => _nowSelectedVisitorInstance;
    private int _nowSelectIndexInTradeContent = -1;//ガルガンチュアなどの複数の選択肢が用意されてるやつ用

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        //ターン進行時に訪問者の抽選を行い、対象者がいた時に呼び鈴を鳴らす
        MoneyManager.Instance.OnCurrentTurnChange
        .Skip(1)//最初のターンはスキップ
        .Subscribe(_ =>
        {
            VisitorLottery();
            if(_nowSelectedVisitorInstance.Value != null)
            {
                //TODO:抽選されたアバタを_visitorSpawnPointまでテレポートさせる
                _icController.UpdateState(IntercomController.IntercomState.Calling);
            }
        })
        .AddTo(this);

        //プレイヤーがインターホンのTalkボタンを押したら会話イベントを開始する
        _icController.OnChangeCurrentState
            .Where(value =>  value == IntercomController.IntercomState.Talking)
            .Subscribe(_ =>
            {
                if(_nowSelectedVisitorInstance.Value != null)
                    StartCoroutine(VisitorTradeConversation(_nowSelectedVisitorInstance.Value));
            })
            .AddTo(this);

        _select1.onClick.AddListener(() => _nowSelectIndexInTradeContent = 0);
        _select2.onClick.AddListener(() => _nowSelectIndexInTradeContent = 1);

        /*--- view用の処理 ---*/
        try
        {
            _patchObject = GameObject.Find("door_hatch");
            _mainCamera = Camera.main;
            _fpController = FindAnyObjectByType<App.Player.FirstPersonController>();

            _itemPanelManager = FindAnyObjectByType<ItemPanelManager>();
            _effectManager = FindAnyObjectByType<EffectManager>();
            _gameUIManager = FindFirstObjectByType<GameUIManager>();
        }
        catch (System.Exception)
        {
            
            throw;
        }
    }


    public void WriteSaveData(RoguelikeSaveData saveData)
    {
        List<VisitorSaveData> visitorSaveDatas = new List<VisitorSaveData>();
        foreach (var visitorInstance in _visitorInstances)
        {
            VisitorSaveData visitorSaveData = visitorInstance.CreateVisitorSaveData();
            visitorSaveDatas.Add(visitorSaveData);
        }

        saveData.visitorSaveDatas = visitorSaveDatas;
    }

    public void ReadSaveData(RoguelikeSaveData saveData)
    {
        _visitorInstances.Clear();
        if (saveData.visitorSaveDatas != null)
        {
            foreach (var visitorSaveData in saveData.visitorSaveDatas)
            {
                VisitorData visitorData = _visitorDataBase.visitorDataBase.Find(v => v.id == visitorSaveData.id);
                if (visitorData != null)
                {
                    VisitorInstance visitorInstance = new VisitorInstance();
                    visitorInstance.master = visitorData;
                    visitorInstance.eventProgress = visitorSaveData.eventProgress;
                    visitorInstance.key = "Conversation" + visitorSaveData.eventProgress.ToString("0");
                    visitorInstance.isCheck = false;
                    _visitorInstances.Add(visitorInstance);
                }
                else
                {
                    Debug.LogWarning($"訪問者ID {visitorSaveData.id} がデータベースに見つかりません。");
                }
            }
        }
        else //セーブデータに情報がなかった場合は新規作成
        {
            Debug.LogError("訪問者用セーブデータ新規作成");
            //_visitorDataBaseに登録されているVisitorDataを元にVisitorInstanceを生成する
            foreach (var visitorData in _visitorDataBase.visitorDataBase)
            {
                VisitorInstance visitorInstance = new VisitorInstance();
                visitorInstance.master = visitorData;
                visitorInstance.eventProgress = 1;
                visitorInstance.key = "Conversation" + visitorInstance.eventProgress.ToString("0");
                visitorInstance.isCheck = false;
                _visitorInstances.Add(visitorInstance);
            }

            Debug.LogError("訪問者用セーブデータ新規作成が完了");
        }

        //デバッグ用
        foreach(var visitorInstance in _visitorInstances)
        {
            VisitorConversationContainer[] a;
            Debug.LogWarning(visitorInstance.VisitorName + "\n" + visitorInstance.eventProgress + "\n" + visitorInstance.Conversations.TryGetValue("Conversation1" ,out a) + "\n" + a[0].lineJp);
        }
    }

    /// <summary>
    /// ターン開始時にどの訪問者が来るか抽選する関数
    /// </summary>
    public void VisitorLottery()
    {
        if (_visitorInstances == null || _visitorInstances.Count == 0)
        {
            Debug.LogWarning("[VisitorSystem] VisitorLottery: _visitorInstances が空のためスキップ");
            return;
        }
        Debug.LogError("抽選開始");
        int count = 0;
        while(_nowSelectedVisitorInstance.Value == null)
        {
            int random = UnityEngine.Random.Range(0,_visitorInstances.Count);
            random = UnityEngine.Random.Range(0,2);;//ToDo:全員分のストーリー完成させたら消そう
            if(!_visitorInstances[random].isCheck)//まだ抽選されていない人物の場合
            {
                if(CheckClearEventTrigger(_visitorInstances[random].VisitorName, _visitorInstances[random].eventProgress))
                {
                    _nowSelectedVisitorInstance.Value = _visitorInstances[random];
                    Debug.LogError($"抽選の結果{_visitorInstances[random].VisitorName}に決定しました");
                    break;
                }
                else
                {
                    Debug.LogError(_visitorInstances[random].VisitorName + "は満たしていない");
                    _visitorInstances[random].isCheck = true;
                    count++;
                }

                if(count >= _visitorInstances.Count)//全員条件を満たしていない場合
                {
                    Debug.LogError("全員条件を満たしていませんでした");
                    break;
                }
            }
            else
            {
                Debug.LogError(_visitorInstances[random].VisitorName + "は確認済み");
                continue;
            }
        }

        //抽選終了後の処理
        foreach(var visitorInstance in _visitorInstances)
            visitorInstance.isCheck = false;
    }


    /// <summary>
    /// イベントを発行できる条件がクリアされているのかを確かめる関数
    /// </summary>
    /// <param name="visitorName">訪問者の名前</param>
    /// /// <param name="nowEventProgress">イベントの進行度</param>
    /// <returns></returns>
    private bool CheckClearEventTrigger(string visitorName, int nowEventProgress)
    {
        switch(visitorName)
        {
            case "Bell":
                break;
            case "Gargantua":
                break;
            case "Eligor":
                break;
            case "Silvia":
                break;
            case "Machia":
                break;
            case "Lucas":
                break;
            case "Jin":
                break;
            case "Lilith":
                break;
            case "Faust":
                break;
            case "Neun":
                break;
        }

        //switch文を突破出来たら条件を満たしているとみなす
        Debug.LogError(visitorName + "のイベント条件を満たしています");
        return true;
    }

    /*---------- ここからはViewに記載する予定の処理を記述 ----------*/
    [Header("以下はView部分で必要なコンポーネント")]
    [SerializeField] private GameObject _visitorUI;
    [SerializeField] private IntercomController _icController;
    [SerializeField] private TMP_Text _nameText;
    [SerializeField] private TMP_Text _conversationText;
    [SerializeField] private Button _select1;//選択肢1
    [SerializeField] private Button _select2;//選択肢2

    [Header("会話用の設定")]
    [SerializeField] private InputActionReference _clickReference;
    [SerializeField] private AudioSource _audio;
    [SerializeField] private SerializeDictionary<string, AudioClip> _characterVoices;//主人公の会話データを格納
    private float _characterSpeed = 0.1f;//文字を書くスピード


    [Header("データ更新用コンポーネント")]
    [SerializeField] private ItemPanelManager _itemPanelManager;
    [SerializeField] private EffectManager _effectManager;

    [SerializeField] private SerializeDictionary<string, string[]> _otherLines;//主人公の会話データを格納

    [Header("取引アニメーション用")]
    [SerializeField] private Transform _cameraPosition;
    [SerializeField] private Transform _itemPosition;
    [SerializeField] private Camera _mainCamera;
    [SerializeField] private GameObject _patchObject;
    [SerializeField] private GameObject _itemLight;
    [SerializeField] private AudioClip _openSE;
    private App.Player.FirstPersonController _fpController;
    private Sequence _tradeAnimSequence;
    private GameUIManager _gameUIManager;

    /// <summary>
    /// 訪問者との会話フローを実行するコルーチン
    /// </summary>
    /// <param name="visitor">訪問者の情報</param>
    /// <returns></returns>
    public IEnumerator VisitorTradeConversation(VisitorInstance visitor)
    {
        //最初に会話を行う
        yield return StartCoroutine(TextSystem(visitor));

        //取引ができるか判定・主人公のセリフで伝える
        if(isClearRequest(visitor))
        {
            //セリフ用コルーチン
            yield return StartCoroutine(TextSystem("CanTradeText"));
            _icController.IsCanPushIdle = true;

            //取引に応じるか決定するまで待つ
            yield return new WaitUntil(() => _icController.CurrentState != IntercomController.IntercomState.Talking);
            if(_icController.CurrentState == IntercomController.IntercomState.Idle)//取引を拒否した場合
            {
                //UIを消して終了処理
                
            }
            else if(_icController.CurrentState == IntercomController.IntercomState.Trade)//取引に応じた場合
            {
                //要求・報酬に関する情報を取得
                Request request = visitor.Requests[visitor.eventProgress - 1].Clone();
                Reward reward = visitor.Rewards[visitor.eventProgress - 1].Clone();

                //選択肢が存在する取引相手の場合,取引内容を決定するまで待機
                if(visitor.VisitorName == "Faust" || (visitor.VisitorName == "Gargantua"/*&& visitor.eventProgress < 3*/))//ガルガンチュアはイベント3回目までは確定してるので無視
                {
                    _visitorUI.SetActive(true);
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                    _nameText.text = "You";
                    _conversationText.text = "どちらを選ぼうか...？";
                    _select1.gameObject.SetActive(true);
                    _select2.gameObject.SetActive(true);
                    _select1.GetComponentInChildren<VisitorSelectionHover>().Init(reward.RewardElements[0].content, _conversationText);
                    _select2.GetComponentInChildren<VisitorSelectionHover>().Init(reward.RewardElements[1].content, _conversationText);

                    yield return new WaitUntil(() => _nowSelectIndexInTradeContent != -1);
                    _select1.gameObject.SetActive(false);
                    _select2.gameObject.SetActive(false);
                    _visitorUI.SetActive(false);
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                }

                if(visitor.VisitorName == "Lucas")
                {
                    //TODO:コイントスの機能実装
                }

                //要求されたものを提出
                List<GameObject> reqObj = new List<GameObject>();
                List<GameObject> rewObj = new List<GameObject>();
                
                foreach(var element in request.RequestElements)
                {
                    if (element.content is ItemData itemData)
                    {
                        // ItemDataの処理
                        if(itemData.prefabData != null)
                        {
                            reqObj.Add(itemData.prefabData);
                        } 
                        _itemPanelManager.RemoveItem(itemData.id,itemData.itemType);
                    }
                    else if(element.content is MoneyData moneyData)
                    {
                        if(moneyData.prefabData != null) reqObj.Add(moneyData.prefabData);
                        MoneyManager.Instance.ReduceMoney(moneyData.price);
                        if(_gameUIManager != null) _gameUIManager.AddPopupQueue(false,moneyData);
                    }
                    else if (element.content is EffectData effectData)
                    {
                        // EffectDataの処理
                        _effectManager.RemoveEffect(effectData.id);
                    }
                }

                //報酬の獲得
                if(visitor.VisitorName == "Faust" || visitor.VisitorName == "Gargantua")
                {
                    //選択していない方の報酬を削除
                    if(_nowSelectIndexInTradeContent != -1)
                    {
                        if(_nowSelectIndexInTradeContent == 0)
                            reward.RewardElements.RemoveAt(1);
                        else if(_nowSelectIndexInTradeContent == 1)
                            reward.RewardElements.RemoveAt(0);
                    }
                }
                foreach(var element in reward.RewardElements)
                {
                    if (element.content is ItemData itemData)
                    {
                        // ItemDataの処理
                        if(itemData.prefabData != null)
                        {
                            rewObj.Add(itemData.prefabData);
                        }

                        _itemPanelManager.AddItem(itemData.id,itemData.itemType);
                    }
                    else if(element.content is MoneyData moneyData)
                    {
                        if(moneyData.prefabData != null) rewObj.Add(moneyData.prefabData);
                        MoneyManager.Instance.AddMoney(moneyData.price);
                        if(_gameUIManager != null) _gameUIManager.AddPopupQueue(true,moneyData);
                    }
                    else if (element.content is EffectData effectData)
                    {
                        // EffectDataの処理
                        _effectManager.AddEffect(effectData.id);
                    }
                }
                
                //アニメーション開始
                if (_fpController != null)
                    _fpController.enabled = false;

                GameObject itemObj = null;
                _tradeAnimSequence = DOTween.Sequence();
                _tradeAnimSequence
                .Append(_mainCamera.transform.DOMove(_cameraPosition.position, 1.0f))
                .OnStart(() =>
                {
                    _mainCamera.transform.eulerAngles = new Vector3(0,180,0);
                    _itemLight.SetActive(true);
                })
                .AppendInterval(1.0f)
                .Append(_patchObject.transform.DOLocalMoveZ(0.0016f, 1.0f))
                .AppendCallback(() =>
                {
                    //提出アイテムを出現させる
                    if(reqObj != null)
                    {
                        Debug.Log("提出アイテムをスポーンします");
                        itemObj = Instantiate(reqObj[0],_itemPosition.position,Quaternion.identity);
                    }
                    else
                        Debug.LogError("提出アイテムは存在しない");
                })
                .AppendInterval(1.0f)
                .Append(_patchObject.transform.DOLocalMoveZ(-0.0004f, 1.0f))               
                .AppendCallback(() =>
                {
                    //報酬アイテムを出現させる
                    if(itemObj != null) Destroy(itemObj);
                    if(rewObj != null)
                    {
                        Debug.Log("報酬アイテムをスポーンします");
                        itemObj = Instantiate(rewObj[0],_itemPosition.position,Quaternion.identity);
                    }
                    else
                        Debug.LogError("スポーンされてない");
                })
                .AppendInterval(1.0f)
                .Append(_patchObject.transform.DOLocalMoveZ(0.0016f, 1.0f))
                .AppendInterval(1.5f);

                //SEを追加してから再生
                _tradeAnimSequence.InsertCallback(2.0f, () =>
                {
                    _audio.PlayOneShot(_openSE);
                });
                _tradeAnimSequence.InsertCallback(4.0f, () =>
                {
                    _audio.PlayOneShot(_openSE);
                });
                _tradeAnimSequence.InsertCallback(6.0f, () =>
                {
                    _audio.PlayOneShot(_openSE);
                });
                _tradeAnimSequence.Play();
                yield return _tradeAnimSequence.WaitForCompletion();//アニメーションが終わるまで待つ
                _patchObject.transform.DOLocalMoveZ(-0.0004f, 1.0f);
                _itemLight.SetActive(false);
                if (_fpController != null)
                    _fpController.enabled = true;

                //報酬アイテムを破壊しておく
                if(itemObj != null) Destroy(itemObj);

                //アイテムやエフェクトの処理, イベント進行度更新
                visitor.key = "TradeSuccess" + visitor.eventProgress.ToString("0");
                yield return StartCoroutine(TextSystem(visitor));

                //イベント進行状況を更新
                visitor.eventProgress++;
                visitor.key = "Conversation" + visitor.eventProgress.ToString("0");
            }
        }
        else
        {
            yield return StartCoroutine(TextSystem("Can'tTradeText"));
        }
        
        

        Debug.Log("訪問者システム終了");

        //終了時の処理
        _conversationText.text = "";
        _nowSelectIndexInTradeContent = -1;
        _nowSelectedVisitorInstance.Value = null;
        _icController.IsCanPushIdle = false;
        _icController.UpdateState(IntercomController.IntercomState.Idle);
        _select1.gameObject.SetActive(false); 
        _select2.gameObject.SetActive(false);
    }

    /// <summary>
    /// 訪問者が求めるアイテム・お金・エフェクトを持っているのかを検知する関数
    /// </summary>
    /// <param name="visitor"></param>
    /// <returns></returns>
    private bool isClearRequest(VisitorInstance visitor)
    {
        var request = visitor.Requests[visitor.eventProgress];
        if(request.RequestElements.Count == 0)//要求アイテムが0のとき
        {
            return true;
        }

        foreach(var element in request.RequestElements)
        {
            if (element.content is ItemData itemData)
            {
                // ItemDataの処理
                if(_itemPanelManager.isHasItem(itemData.id, element.num))
                    return true;
            }
            else if(element.content is MoneyData moneyData)
            {
                
            }
            else if (element.content is EffectData effectData)
            {
                // EffectDataの処理
            }
        }
        
        
        return false;
    }

    /// <summary>
    /// 下矢印を改行に変換する
    /// </summary>
    /// <param name="str"></param>
    /// <returns></returns>
    private string AjustDownArrow(string str)
    {
        string result = null;
        foreach (var c in str)
        {
            if (c != '↓')
                result += c;
            else
                result += '\n';
        }

        return result;
    }

    /// <summary>
    /// イベントキーの内容を基にテキストメッセージを書き換えていく
    /// </summary>
    /// <param name="key">イベントキー</param>
    /// <returns></returns>
    private IEnumerator TextSystem(VisitorInstance visitor)
    {
        if(visitor.key == null || !visitor.Conversations.ContainsKey(visitor.key))//指定したキーが存在しない場合
        {
            Debug.LogError(visitor.key + "\n" + visitor.Conversations.ContainsKey(visitor.key));
            Debug.LogError("キーが存在しません");
            yield break;
        }

        yield return new WaitForSeconds(0.5f);
        _visitorUI.SetActive(true);
        _nameText.text = visitor.VisitorName;

        //指定されたBGMに設定・再生(同じBGMの場合は無視)
        // if (_audioSource.clip != _clipDictionary[_conversations[key].bgmKey] || !_audioSource.isPlaying)
        // {
        //     _audioSource.clip = _clipDictionary[_conversations[key].bgmKey];
        //     _audioSource.Play();
        // }

        var contentContainer = visitor.Conversations[visitor.key];
        var characterVoice = _characterVoices.GetDictionary[visitor.VisitorName];
        for (int i = 0; i < contentContainer.Length; i++)
        {
            _conversationText.text = "";//テキストを消去
            string sentence = AjustDownArrow(contentContainer[i].lineJp);

            //アニメーションを変化させる


            //テキストを一文字ずつ描画
            int charNum = 0;
            foreach (char c in sentence)
            {
                //途中でトリガーボタンを押すとテキストを全て出力
                if (_clickReference.action.IsPressed())
                {
                    _conversationText.text = sentence;
                    break;
                }
                else
                    _conversationText.text += c;

                charNum++;
                if(/*charNum % 2 == 0 &&*/ characterVoice != null) _audio.PlayOneShot(characterVoice);
                yield return new WaitForSeconds(_characterSpeed);
            }

            //次に進むためのクリック入力
            yield return new WaitUntil(() => _clickReference.action.WasPressedThisFrame());
            yield return new WaitForSeconds(0.5f);
        }

        _conversationText.text = "";
        _visitorUI.SetActive(false);
    }

    /// <summary>
    /// 主人公の話をテキスト形式で表示するときの関数
    /// </summary>
    /// <param name="lines"></param>
    /// <returns></returns>
    private IEnumerator TextSystem(string key)
    {
        if(!_otherLines.GetDictionary.ContainsKey(key))
            yield break;

        yield return new WaitForSeconds(0.5f);
        _visitorUI.SetActive(true);
        _nameText.text = "You";

        string[] lines;
        _otherLines.TryGetValue(key, out lines);

        for (int i = 0; i < lines.Length; i++)
        {
            _conversationText.text = "";//テキストを消去
            string sentence = AjustDownArrow(lines[i]);

            //アニメーションを変化させる


            //テキストを一文字ずつ描画
            foreach (char c in sentence)
            {
                //途中でトリガーボタンを押すとテキストを全て出力
                if (_clickReference.action.IsPressed())
                {
                    _conversationText.text = sentence;
                    break;
                }
                else
                    _conversationText.text += c;

                yield return new WaitForSeconds(_characterSpeed);
            }

            //次に進むためのクリック入力
            yield return new WaitUntil(() => _clickReference.action.WasPressedThisFrame());
            yield return new WaitForSeconds(0.5f);
        }

        _conversationText.text = "";
        _visitorUI.SetActive(false);
    }
}