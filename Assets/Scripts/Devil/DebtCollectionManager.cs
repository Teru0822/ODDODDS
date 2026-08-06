using App.Player;
using DG.Tweening;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class DebtCollectionManager : MonoBehaviour
{
    [Header("会話用データパス")] 
    [SerializeField] private Language _language = Language.JP;
    [SerializeField] private string _jsonFilePathJP = "Assets/Resources/Conversations/DevilConversations_JP.json";
    [SerializeField] private string _jsonFilePathEN = "Assets/Resources/Conversations/DevilConversations_EN.json";

    private Dictionary<string, DevilConversationData> _conversations = new Dictionary<string, DevilConversationData>();//データ格納辞書

    [Header("会話用オブジェクト")]
    [SerializeField] private Image _background;
    [SerializeField] private Image _devil;
    [SerializeField] private Sprite[] _devilExpressions;
    [SerializeField] private GameObject _panel;
    [SerializeField] private TMP_Text _name;
    [SerializeField] private TMP_Text _mainSentence;
    [SerializeField] private TMP_Text _reduceMoneyCounter;
    [SerializeField] private TMP_Text _myMoneyCounter;
    private string _reduceMoneyMessage = "<size=50>請求金額</size>";
    private string _myMoneyMessage = "<size=50>所持金</size>";

    [Header("会話用コンポーネント")]
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private SerializeDictionary<string, AudioClip> _clipSerializeDictionary = new SerializeDictionary<string, AudioClip>();
    private Dictionary<string, AudioClip> _clipDictionary = new Dictionary<string, AudioClip>();
    [SerializeField] private AudioClip _drumRoll;

    [Header("会話用の設定")]
    [SerializeField] private InputActionReference _clickReference;
    [SerializeField] private TMP_FontAsset _japaneseFontAsset;
    [SerializeField] private TMP_FontAsset _englishFontAsset;
    private float _characterSpeed = 0.1f;//文字を書くスピード

    //数字カウント用アニメーション
    Sequence countAnimSequence;
    int previousDecreaseValue;
    int previousMoneyValue;

    [Header("ゲームオーバー用")]
    [SerializeField] private ResultUIManager _resultUIManager;


    private FirstPersonController _fpController;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        LoadConversationData();

        _clickReference.action.Enable();

        //シリアライズで設定したSerializeDictionaryをDictionaryに変換
        _clipDictionary = _clipSerializeDictionary.GetDictionary;

        AdjustLanguageSetting();
    }

    private void Update()
    {
#if UNITY_EDITOR
        if (Keyboard.current.zKey.wasPressedThisFrame)
        {
            MoneyManager.Instance.ReduceMoney(9999);
            StartCoroutine(ShowConversation("Conversation_00"));
            Debug.Log("試しにイベント機能を使います。");
        }
#endif
    }

    /// <summary>
    /// ローカライズ用の設定
    /// </summary>
    private void AdjustLanguageSetting()
    {
        //TODO:残りの多言語対応はLocalizationを用いてやっていこう
        if (_language == Language.JP)
        {
            _mainSentence.font = _japaneseFontAsset;
            _reduceMoneyCounter.font = _japaneseFontAsset;
            _myMoneyCounter.font = _japaneseFontAsset;

            _name.text = "アクマ";
            _reduceMoneyMessage = "<size=50>請求金額</size>\n";
            _myMoneyMessage = "<size=50>所持金</size>\n";
            _characterSpeed = 0.1f;
        }
        else
        {
            _mainSentence.font = _englishFontAsset;
            _reduceMoneyCounter.font = _englishFontAsset;
            _myMoneyCounter.font = _englishFontAsset;

            _name.text = "Demon";
            _reduceMoneyMessage = "<size=50>Debt Amount</size>\n";
            _myMoneyMessage = "<size=50>Money</size>\n";
            _characterSpeed = 0.05f;
        }
    }

    /// <summary>
    /// 指定されたパスのJSONファイルから会話データを読み込み、Dictionaryに格納
    /// </summary>
    private void LoadConversationData()
    {
        var _jsonFilePath = "";
        if (_language == Language.JP)
        {
            _jsonFilePath = _jsonFilePathJP;
        }
        else
        {
            _jsonFilePath = _jsonFilePathEN;
        }
        
        if (string.IsNullOrEmpty(_jsonFilePath))
        {
            Debug.LogWarning("JSONファイルパスが指定されていません。");
            return;
        }

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
                DevilConversationContainer container = JsonConvert.DeserializeObject<DevilConversationContainer>(json);

                _conversations.Clear();
                if (container != null && container.conversations != null)
                {
                    foreach (var data in container.conversations)
                    {
                        if (!string.IsNullOrEmpty(data.key))
                        {
                            _conversations.Add(data.key, data);

                            //デバッグ用データ確認
                            Debug.Log($"key: {_conversations[data.key].key}\nnextkey: {_conversations[data.key].nextKey}\nbgmKey: {_conversations[data.key].bgmKey}\n");
                            for (int i = 0; i < _conversations[data.key].lines.Length; i++)
                            {
                                Debug.Log($"セリフ: {_conversations[data.key].lines[i]}\n表情: {_conversations[data.key].devilExpressions[i]}\n");
                            }
                        }
                    }
                    Debug.Log($"会話データをロードしました。総件数: {_conversations.Count} 件 (パス: {fullPath})");
                }
                else
                {
                    Debug.LogWarning("JSONのパース結果が空です、またはフォーマットが異なります。");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"会話データのロード中にエラーが発生しました: {e.Message}");
            }
        }
        else
        {
            Debug.LogError($"会話データファイルが見つかりません。パス: {fullPath}");
        }
    }

    /// <summary>
    /// 会話キーに対応する会話データを取得します
    /// </summary>
    /// <param name="key">会話のキー</param>
    /// <returns>会話データ。見つからない場合はnull</returns>
    private DevilConversationData GetConversation(string key)
    {
        if (_conversations != null && _conversations.TryGetValue(key, out var data))
        {
            return data;
        }
        return null;
    }

    private int GetConversationTypeNum(ConversationType type)
    {
        return _conversations.Count(pair => pair.Value.conversationType == type);
    }

    /// <summary>
    /// 悪魔の取り立てに関するアニメーションを再生
    /// </summary>
    /// <param name="key">会話のキー</param>
    /// <returns>会話コルーチン</returns>
    public IEnumerator ShowConversation(string key = "")
    {
        bool isSuccess = true;//取り立てに耐えたか否か
        if(_fpController == null)//必要なコンポーネントが無かった場合は取得
        {
            _fpController = FindFirstObjectByType<FirstPersonController>();
        }
        _fpController.enabled = false;//イベント終了まで動けないようにする

        //画面を暗転させる
        _background.DOFade(endValue: 1f, duration: 1f)
            .OnComplete(() =>
            {
                _devil.DOFade(endValue: 1f, duration: 1f)
                    .OnComplete(() =>
                    {
                        _panel.SetActive(true);
                    });
            });

        //特にKeyが指定されていない場合はランダムなキーを指定
        if(key == "")
        {
            int random = UnityEngine.Random.RandomRange(1, GetConversationTypeNum(ConversationType.Conversation));
            Debug.LogError("シナリオ数：" + GetConversationTypeNum(ConversationType.Conversation) + "\n抽選されたシナリオ：" + random);
            key = "Conversation_" + random.ToString("00");
        }

        //シナリオに設定された文字を表示させていく
        yield return new WaitForSeconds(1.5f);
        yield return StartCoroutine(TextSystem(key));

        //取り立て開始
        {
            previousDecreaseValue = MoneyManager.Instance.GetQuotaThisTime();
            previousMoneyValue = (int)MoneyManager.Instance.CurrentMoney;

            //ゲームオブジェクトの表示・非表示
            _panel.SetActive(false);
            _reduceMoneyCounter.text = _reduceMoneyMessage + previousDecreaseValue.ToString();
            _myMoneyCounter.text = _myMoneyMessage + previousMoneyValue.ToString();

            //減らす金額を表示する用のアニメーション開始
            countAnimSequence = DOTween.Sequence();
            countAnimSequence.Append(_reduceMoneyCounter.DOFade(endValue: 1f, duration: 1f))//請求金額テキストの表示
                  .Append(_myMoneyCounter.DOFade(endValue: 1f, duration: 1f))//所持金テキストの表示
                  .Append(_reduceMoneyCounter.rectTransform.DOAnchorPos(new Vector2(0, 180), 1.0f).SetEase(Ease.OutQuart))
                  .Join(_myMoneyCounter.rectTransform.DOAnchorPos(new Vector2(0, -180), 1.0f).SetEase(Ease.OutQuart))
                  .Append(DOTween.To(() => previousDecreaseValue,//ターゲットとなる変数
                         num => previousDecreaseValue = num,    //値の更新を行う
                         0,                                     //最終的な値
                         1.0f                                   //時間
                         ).OnStart(() => _audioSource.PlayOneShot(_drumRoll)).OnUpdate(() => _reduceMoneyCounter.text = _reduceMoneyMessage + previousDecreaseValue.ToString()))
                  .Join(DOTween.To(() => previousMoneyValue,
                         num => previousMoneyValue = num,
                         previousMoneyValue - previousDecreaseValue,
                         1.0f
                         ).OnUpdate(() => _myMoneyCounter.text = _myMoneyMessage + previousMoneyValue.ToString()))
                  .AppendInterval(1.0f)
                  .Append(_reduceMoneyCounter.DOFade(endValue: 0f, duration: 1f))//請求金額テキストの表示
                  .Join(_myMoneyCounter.DOFade(endValue: 0f, duration: 1f));//所持金テキストの表示
            countAnimSequence.Play();
            MoneyManager.Instance.ApplyTurnDecrease();
            yield return countAnimSequence.WaitForCompletion();//アニメーションが終わるまで待つ

            //取り立て成功か否かで処理を分ける
            if (MoneyManager.Instance.CheckGameOver())//失敗用
            {
                //ランダムで失敗演出を設定
                isSuccess = false;
                int random = UnityEngine.Random.RandomRange(0, GetConversationTypeNum(ConversationType.Fail) + 1);
                Debug.LogError("シナリオ数：" + GetConversationTypeNum(ConversationType.Fail) + "\n抽選されたシナリオ：" + random);
                string failKey = "fail_" + random.ToString("00");

                //必要なオブジェクトをアクティブ
                _panel.SetActive(true);
                yield return StartCoroutine(TextSystem(failKey));

                //TODO:アイテムでゲームオーバーを回避する
                yield return new WaitForSeconds(1.0f);
                ItemPanelManager itemManager = FindFirstObjectByType<ItemPanelManager>();
                if(itemManager != null && itemManager.isHasItem(9,1))//聖職者のアンクを持っている場合
                {
                    isSuccess = true;
                    yield return StartCoroutine(TextSystem("Revive"));
                }
                else
                {
                    //ゲームオーバー処理
                    yield return StartCoroutine(_resultUIManager.GameOverAnimation());
                    RoguelikeSaveManager.DeleteDataInGameOver();
                }
            }
            else//成功用
            {
                //ランダムで成功演出を設定
                int random = UnityEngine.Random.RandomRange(0, GetConversationTypeNum(ConversationType.Success) + 1);
                Debug.LogError("シナリオ数：" + GetConversationTypeNum(ConversationType.Success) + "\n抽選されたシナリオ：" + random);
                string successKey = "success_" + random.ToString("00");

                //必要なオブジェクトをアクティブ
                _panel.SetActive(true);
                yield return StartCoroutine(TextSystem(successKey));
            }

            yield return new WaitUntil(() => _clickReference.action.WasPressedThisFrame());
        }



        //これで会話が終了の場合
        {
            //暗転を解除させる
            _background.DOFade(endValue: 0f, duration: 2f);
            _devil.DOFade(endValue: 0f, duration: 2f);
            _panel.SetActive(false);

            if (isSuccess)//取り立てに耐えられたらBGMは消す
            {
                _audioSource.DOFade(endValue: 0f, duration: 1f).OnComplete(() =>
                {
                    _audioSource.Stop();
                    _audioSource.volume = 0.2f;
                });

                MoneyManager.Instance.AdvanceTurn();
            }        

            _reduceMoneyCounter.DOFade(endValue: 0f, duration: 1f);
            _reduceMoneyCounter.rectTransform.DOAnchorPos(new Vector2(-540, 180), 1.0f).SetEase(Ease.OutQuart);

            _myMoneyCounter.DOFade(endValue: 0f, duration: 1f);
            _myMoneyCounter.rectTransform.DOAnchorPos(new Vector2(540, 180), 1.0f).SetEase(Ease.OutQuart);
            _fpController.enabled = true;//イベント終了まで動けないようにする
            yield return new WaitUntil(() => _background.color.a <= 0);
        }

        Debug.Log("イベント無事終了");
    }

    private enum Language
    {
        JP,
        EN
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
    private IEnumerator TextSystem(string key)
    {
        if(GetConversation(key) == null)//指定したキーが存在しない場合
            yield break;

        //指定されたBGMに設定・再生(同じBGMの場合は無視)
        if (_audioSource.clip != _clipDictionary[_conversations[key].bgmKey] || !_audioSource.isPlaying)
        {
            _audioSource.clip = _clipDictionary[_conversations[key].bgmKey];
            _audioSource.Play();
        }

        for (int i = 0; i < _conversations[key].lines.Length; i++)
        {
            _mainSentence.text = "";//テキストを消去
            string sentence = AjustDownArrow(_conversations[key].lines[i]);

            //アクマの表情を変化させる
            _devil.sprite = _devilExpressions[(int)_conversations[key].devilExpressions[i]];

            //テキストを一文字ずつ描画
            foreach (char c in sentence)
            {
                //途中でトリガーボタンを押すとテキストを全て出力
                if (_clickReference.action.IsPressed())
                {
                    _mainSentence.text = sentence;
                    break;
                }
                else
                    _mainSentence.text += c;

                yield return new WaitForSeconds(_characterSpeed);
            }

            //次に進むためのクリック入力
            yield return new WaitUntil(() => _clickReference.action.WasPressedThisFrame());
            yield return new WaitForSeconds(0.5f);
        }
    }
}

