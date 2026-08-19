using App.Player;
using DG.Tweening;
using Newtonsoft.Json;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using MiniGames.Transitions;

public enum Language
{
    JP = 0,
    EN = 1,
}

public class DebtCollectionManager : MonoBehaviour,ILanguage
{
    public static DebtCollectionManager Instance;
    private bool _isStartDebtCollection = false;//現在、悪魔の取り立てが行われているか
    public bool IsStartDebtCollection => _isStartDebtCollection;

    /// <summary>
    /// 取り立てイベントが進行中か。Instance が存在しないシーンからでも安全に参照できる
    /// </summary>
    public static bool IsCollecting => Instance != null && Instance._isStartDebtCollection;
    [Header("デモ版専用")]
    [SerializeField] private bool _demoGamePlay = false;
    [SerializeField] private int _demoGamePlayEndTurn = 12;
    [SerializeField] private AudioClip _demoFaustClip;
    [SerializeField] private Sprite _thxPlayDemoImage;
    [SerializeField] private TMP_Text _thxPlayDemoText;
    [SerializeField] private TMP_Text _quitMessage;
    private List<Transform> _demoPinballCameraFoots = new List<Transform>();
    private GameObject _faust;
    private GameObject _imp;
    private GameObject _pinball;
    private GameObject _demoCameraPosition;
    private Transform _pinballCenter;

    [Header("会話用データパス")] 
    [SerializeField] private Language _language = Language.JP;
    [SerializeField] private string _jsonFilePathJP = "Assets/Resources/Conversations/DevilConversations_JP.json";
    [SerializeField] private string _jsonFilePathEN = "Assets/Resources/Conversations/DevilConversations_EN.json";

    private Dictionary<string, DevilConversationData> _conversations = new Dictionary<string, DevilConversationData>();//データ格納辞書

    [Header("会話用オブジェクト")]
    [SerializeField] private Image _background;
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

    [Header("テロップ表示設定")]
    [Tooltip("悪魔のセリフテロップ（会話パネル・名前・本文）を表示するかどうか。" +
             "オフの間はテロップの描画と文字送りのクリック待ちを飛ばし、" +
             "カメラ移動・BGM・請求金額の演出・成否判定はそのまま進みます。" +
             "再びセリフを出したくなったらここにチェックを入れるだけで元に戻ります。")]
    [SerializeField] private bool _showConversationTelop = false;

    [Header("会話用の設定")]
    [SerializeField] private InputActionReference _clickReference;
    [SerializeField] private TMP_FontAsset _japaneseFontAsset;
    [SerializeField] private TMP_FontAsset _englishFontAsset;
    private float _characterSpeed = 0.1f;//文字を書くスピード

    //数字カウント用アニメーション
    Sequence _countAnimSequence;
    int _previousDecreaseValue;
    int _previousMoneyValue;

    [Header("ゲームオーバー用")]
    [SerializeField] private ResultUIManager _resultUIManager;

    [Header("取り立て場面への遷移用")]
    [SerializeField] private GameObject _cameraPosition;
    [SerializeField] private float _turnTransitionDuration = 2f;
    [SerializeField] private GameObject _gameUI;
    private Vector3 _originPosition;
    private Vector3 _originRotation;
    private FirstPersonController _fpController;
    private Camera _camera;

    private IEnumerator _debtCoroutine;
    bool _isStopCoroutine = false;

    void Awake()
    {
        if(Instance == null)
            Instance = this;
        else
            Destroy(this.gameObject);
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        LoadConversationData();

        _clickReference.action.Enable();

        //シリアライズで設定したSerializeDictionaryをDictionaryに変換
        _clipDictionary = _clipSerializeDictionary.GetDictionary;

        AdjustLanguageSetting();

        /*
        this.UpdateAsObservable()
            .Subscribe(_ =>
            {
                
                if(SettingUIManager.IsMenuOpen && !_isStopCoroutine)
                {
                    if(_debtCoroutine != null)
                    {
                        StopCoroutine(_debtCoroutine);
                        if(_countAnimSequence != null) _countAnimSequence.Pause();
                        _isStopCoroutine = true;
                    }
                }
                else if(!SettingUIManager.IsMenuOpen && _isStopCoroutine)
                {
                    if(_debtCoroutine != null)
                    {
                        StartCoroutine(_debtCoroutine);
                        if(_countAnimSequence != null) _countAnimSequence.Play();
                        _isStopCoroutine = false;
                    }
                }
            }).AddTo(this);
            */
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

    public void SettingLanguage(Language language)
    {
        if (language == Language.JP)
        {
            _mainSentence.font = _japaneseFontAsset;
            _reduceMoneyCounter.font = _japaneseFontAsset;
            _myMoneyCounter.font = _japaneseFontAsset;

            _name.text = "アクマ";
            _reduceMoneyMessage = "<size=50>請求金額</size>\n";
            _myMoneyMessage = "<size=50>所持金</size>\n";
            _characterSpeed = 0.1f;
        }
        else if(language == Language.EN)
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

    public void StartConversationCoroutine(string key = "")
    {
        FindObjectDemo();
        _faust.SetActive(false);
        _debtCoroutine = ShowConversation(key);
        StartCoroutine(_debtCoroutine);
    }

    /// <summary>
    /// 悪魔の取り立てに関するアニメーションを再生
    /// </summary>
    /// <param name="key">会話のキー</param>
    /// <returns>会話コルーチン</returns>
    private IEnumerator ShowConversation(string key = "")
    {
        bool isSuccess = true;//取り立てに耐えたか否か
        bool isFinishFade = false;

        _isStartDebtCollection = true;
        if(_fpController == null)//必要なコンポーネントが無かった場合は取得
        {
            _fpController = FindFirstObjectByType<FirstPersonController>();
        }
        if(_camera == null)
        {
            _camera = Camera.main;
        }
        if(_cameraPosition == null)
        {
            _cameraPosition = GameObject.Find("DebtCameraPosition");
        }

        _fpController.enabled = false;//イベント終了まで動けないようにする
        _originPosition = _camera.transform.position;
        _originRotation = _camera.transform.eulerAngles;

        SceneTransitionManager.Instance.ShowTurnTransition(
            _turnTransitionDuration,
            onDuringLoading: () => Debug.Log("Now Loading"),
            onComplete:      () => {isFinishFade = true;SetTelopPanelActive(true);});

        yield return new WaitForSeconds(1.0f);//大体Loading画面になったであろう時間まで待機
        _camera.transform.position = _cameraPosition.transform.position;
        _camera.transform.eulerAngles = new Vector3(0,180,0);
        _gameUI.GetComponent<CanvasGroup>().alpha = 0;//見えないようにする

        yield return new WaitUntil(() => isFinishFade == true);

        //特にKeyが指定されていない場合はランダムなキーを指定
        if(key == "")
        {
            int random = UnityEngine.Random.RandomRange(1, GetConversationTypeNum(ConversationType.Conversation));
            Debug.LogError("シナリオ数：" + GetConversationTypeNum(ConversationType.Conversation) + "\n抽選されたシナリオ：" + random);
            key = "Conversation_" + random.ToString("00");
        }

        //シナリオに設定された文字を表示させていく
        yield return StartCoroutine(TextSystem(key));

        //取り立て開始
        {
            //今回の取り立て金額に関する情報を取得
            _previousDecreaseValue = MoneyManager.Instance.GetQuotaThisTime();
            _previousMoneyValue = (int)MoneyManager.Instance.CurrentMoney;

            //ゲームオブジェクトの表示・非表示
            SetTelopPanelActive(false);
            _reduceMoneyCounter.text = _reduceMoneyMessage + _previousDecreaseValue.ToString();
            _myMoneyCounter.text = _myMoneyMessage + _previousMoneyValue.ToString();

            //減らす金額を表示する用のアニメーション開始
            _countAnimSequence = DOTween.Sequence();
            _countAnimSequence.Append(_reduceMoneyCounter.DOFade(endValue: 1f, duration: 1f))//請求金額テキストの表示
                  .Append(_myMoneyCounter.DOFade(endValue: 1f, duration: 1f))//所持金テキストの表示
                  .Append(_reduceMoneyCounter.rectTransform.DOAnchorPos(new Vector2(0, 180), 1.0f).SetEase(Ease.OutQuart))
                  .Join(_myMoneyCounter.rectTransform.DOAnchorPos(new Vector2(0, -180), 1.0f).SetEase(Ease.OutQuart))
                  .Append(DOTween.To(() => _previousDecreaseValue,//ターゲットとなる変数
                         num => _previousDecreaseValue = num,    //値の更新を行う
                         0,                                     //最終的な値
                         1.0f                                   //時間
                         ).OnStart(() => _audioSource.PlayOneShot(_drumRoll)).OnUpdate(() => _reduceMoneyCounter.text = _reduceMoneyMessage + _previousDecreaseValue.ToString()))
                  .Join(DOTween.To(() => _previousMoneyValue,
                         num => _previousMoneyValue = num,
                         _previousMoneyValue - _previousDecreaseValue,
                         1.0f
                         ).OnUpdate(() => _myMoneyCounter.text = _myMoneyMessage + _previousMoneyValue.ToString()))
                  .AppendInterval(1.0f)
                  .Append(_reduceMoneyCounter.DOFade(endValue: 0f, duration: 1f))//請求金額テキストの表示
                  .Join(_myMoneyCounter.DOFade(endValue: 0f, duration: 1f));//所持金テキストの表示
            _countAnimSequence.Play();
            MoneyManager.Instance.ApplyTurnDecrease();
            yield return _countAnimSequence.WaitForCompletion();//アニメーションが終わるまで待つ

            //取り立て成功か否かで処理を分ける
            if (MoneyManager.Instance.CheckGameOver())//失敗用
            {
                //ランダムで失敗演出を設定
                isSuccess = false;
                int random = UnityEngine.Random.RandomRange(0, GetConversationTypeNum(ConversationType.Fail) + 1);
                Debug.LogError("シナリオ数：" + GetConversationTypeNum(ConversationType.Fail) + "\n抽選されたシナリオ：" + random);
                string failKey = "fail_" + random.ToString("00");

                //必要なオブジェクトをアクティブ
                SetTelopPanelActive(true);
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
                    _isStartDebtCollection = false;
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
                SetTelopPanelActive(true);
                yield return StartCoroutine(TextSystem(successKey));
            }

            yield return new WaitUntil(() => _clickReference.action.WasPressedThisFrame());
        }



        //これで会話が終了の場合
        if(_demoGamePlay && MoneyManager.Instance.CurrentTurnCount != _demoGamePlayEndTurn)
        {
            //暗転を解除させる
            SetTelopPanelActive(false);
            _isStartDebtCollection = false;
            
            SceneTransitionManager.Instance.ShowTurnTransition(
            _turnTransitionDuration,
            onDuringLoading: () => Debug.Log("Now Loading"),
            onComplete:      () => {_fpController.enabled = true; MoneyManager.Instance?.AdvanceTurn();});

            yield return new WaitForSeconds(1.0f);//大体Loading画面になったであろう時間まで待機
            _gameUI.GetComponent<CanvasGroup>().alpha = 1;//見えるようにする
            _camera.transform.position = _originPosition;
            _camera.transform.eulerAngles = _originRotation;
            

            if (isSuccess)//取り立てに耐えられたらBGMは消す
            {
                _audioSource.DOFade(endValue: 0f, duration: 1f).OnComplete(() =>
                {
                    _audioSource.Stop();
                    _audioSource.volume = 0.2f;
                });

                
            }

            _reduceMoneyCounter.DOFade(endValue: 0f, duration: 1f);
            _reduceMoneyCounter.rectTransform.DOAnchorPos(new Vector2(-540, 180), 1.0f).SetEase(Ease.OutQuart);

            _myMoneyCounter.DOFade(endValue: 0f, duration: 1f);
            _myMoneyCounter.rectTransform.DOAnchorPos(new Vector2(540, 180), 1.0f).SetEase(Ease.OutQuart);
            _countAnimSequence = null;
        }
        else if(_demoGamePlay && isSuccess && MoneyManager.Instance.CurrentTurnCount == _demoGamePlayEndTurn)//デモ版専用シーンを再生する（体験版リリース後に消す）
        {
            isFinishFade = false;
            _faust.SetActive(true);
            SceneTransitionManager.Instance.ShowTurnTransition(
            2.0f,
            onDuringLoading: () => Debug.Log("Now Loading"),
            onComplete:      () => {isFinishFade = true;});

            yield return new WaitForSeconds(1.0f);

            _camera.transform.position = _demoCameraPosition.transform.position;
            _camera.transform.eulerAngles = _demoCameraPosition.transform.eulerAngles;

            _imp.transform.DORotate(new Vector3(0,-20,0), 0.5f);
            _faust.transform.DORotate(new Vector3(0,120,0), 0.5f);
            _mainSentence.text = "";
            _name.text = "???";

            yield return new WaitUntil(() => isFinishFade);
            yield return StartCoroutine(TextSystemForFaust());

            //最後まで遊んでくれてありがとう的な
            _background.sprite = _thxPlayDemoImage;
            _background.color = new Color(255,255,255,0);
            SetTelopPanelActive(false);
            
            yield return _background.DOFade(1, 3.0f).WaitForCompletion();
            yield return _thxPlayDemoText.DOFade(1, 2.0f).WaitForCompletion();
            yield return _quitMessage.DOFade(1, 2.0f).WaitForCompletion();

            yield return new WaitUntil(() => _clickReference.action.WasPressedThisFrame());
            RoguelikeSaveManager.DeleteSaveData();
            #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;//シーン再生終了
            #else
                Application.Quit();//ゲームプレイ終了
            #endif
        }

        Debug.Log("イベント無事終了");
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
    /// 会話テロップ用パネルの表示切り替え。_showConversationTelop がオフの間は常に非表示のままにする
    /// </summary>
    /// <param name="active">テロップを出したいか</param>
    private void SetTelopPanelActive(bool active)
    {
        if (_panel == null) return;

        _panel.SetActive(_showConversationTelop && active);
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

        //テロップ非表示中はBGMの切り替えだけ行い、セリフの描画と文字送り待ちは飛ばす
        if (!_showConversationTelop)
        {
            _mainSentence.text = "";
            yield break;
        }

        for (int i = 0; i < _conversations[key].lines.Length; i++)
        {
            _mainSentence.text = "";//テキストを消去
            string sentence = AjustDownArrow(_conversations[key].lines[i]);

            //アクマのアニメーションを遷移させる（任意）
            //_devil.sprite = _devilExpressions[(int)_conversations[key].devilExpressions[i]];

            //テキストを一文字ずつ描画
            int charCount = 0;
            foreach (char c in sentence)
            {
                //途中でトリガーボタンを押すとテキストを全て出力(設定を開いているときは反応しない)
                if (charCount >= 3 && _clickReference.action.IsPressed() && !SettingUIManager.IsMenuOpen)
                {
                    _mainSentence.text = sentence;
                    break;
                }
                else
                {
                    yield return new WaitUntil(() => SettingUIManager.IsMenuOpen == false);//設定画面が閉じられるまで待つ
                    _mainSentence.text += c;
                }

                charCount++;
                yield return new WaitForSeconds(_characterSpeed);
            }

            //次に進むためのクリック入力
            yield return new WaitUntil(() => _clickReference.action.WasPressedThisFrame());
            yield return new WaitForSeconds(0.5f);
        }
    }

    /// <summary>
    /// ファウスト用。後で消す
    /// </summary>
    /// <returns></returns>
    private IEnumerator TextSystemForFaust()
    {
        //指定されたBGMに設定・再生(同じBGMの場合は無視)
        _audioSource.clip = _demoFaustClip;
        _audioSource.Play();        

        if(_language == Language.JP)
        {
            yield return TextSystemPlot("アクマ、お待たせしました。↓以前頼まれていたピンボール台の調整が終わりましたよ。", "???");
            //ToDo：ここでピンボールの出現演出を追加
            SetTelopPanelActive(false);
            yield return demoSequence().WaitForCompletion();
            SetTelopPanelActive(true);
            yield return TextSystemPlot("おぉ！よくできてんじゃねえカ！↓...前の台と外装が変わってねえカ？相変わらずいい趣味してるゼ。","アクマ");
            _faust.transform.DORotate(new Vector3(0,75,0), 0.5f);
            yield return TextSystemPlot("誉め言葉として受け取っておきます。して、そちらにいる御仁はどなたですか？","???");
            _imp.transform.DORotate(new Vector3(0,25,0), 0.5f);
            yield return TextSystemPlot("新しいおもちゃサ。↓そして、新しくなったこいつの初の犠牲者ダ。","アクマ");
            yield return TextSystemPlot("そうですか。私の名はファウスト、ただの数学者です。↓君がこのゲームにどれほど抗えるのか、楽しみにしていますね。","ファウスト");
            yield return TextSystemPlot("くはははははははははははははっ！！","ファウスト");
        }
        else
        {
            yield return TextSystemPlot("Demon, sorry to keep you waiting. ↓ I’ve finished the adjustments to the pinball machine you asked me to make earlier", "???");
            //ToDo：ここでピンボールの出現演出を追加
            SetTelopPanelActive(false);
            yield return demoSequence().WaitForCompletion();
            SetTelopPanelActive(true);
            yield return TextSystemPlot("Wow! You did a damn good job! ↓ ...Hold on, didn’t you change the exterior from the old machine? Still got some pretty good taste, I see.","Demon");
            _faust.transform.DORotate(new Vector3(0,75,0), 0.5f);
            yield return TextSystemPlot("I’ll take that as a compliment. Now then, who’s the gentleman standing over there?","???");
            _imp.transform.DORotate(new Vector3(0,25,0), 0.5f);
            yield return TextSystemPlot("He's my new toy. ↓ And he's gonna be the first victim of this newly upgraded pinball machine.","Demon");
            yield return TextSystemPlot("I see. My name is Faust. I’m just a mathematician. ↓ I’m looking forward to seeing how long you can withstand this game.","Faust");
            yield return TextSystemPlot("Ku-hahahahahahahahahaha!","Faust");
        }
    }

    private IEnumerator TextSystemPlot(string text = "", string name = "")
    {
        //テロップ非表示中はセリフを描画せず、そのまま次の演出へ進める
        if (!_showConversationTelop)
        {
            _mainSentence.text = "";
            yield break;
        }

        //カメラ遷移を実施
        _name.text = name;

        _mainSentence.text = "";//テキストを消去
        string sentence = AjustDownArrow(text);

        //アクマのアニメーションを遷移させる（任意）
        //_devil.sprite = _devilExpressions[(int)_conversations[key].devilExpressions[i]];

        //テキストを一文字ずつ描画
        int charCount = 0;
        foreach (char c in sentence)
        {
            //途中でトリガーボタンを押すとテキストを全て出力(設定を開いているときは反応しない)
            if (charCount >= 5 && _clickReference.action.IsPressed() && !SettingUIManager.IsMenuOpen)
            {
                _mainSentence.text = sentence;
                break;
            }
            else
            {
                yield return new WaitUntil(() => SettingUIManager.IsMenuOpen == false);//設定画面が閉じられるまで待つ
                _mainSentence.text += c;
            }

            charCount++;
            yield return new WaitForSeconds(_characterSpeed);
        }

        //次に進むためのクリック入力
        yield return new WaitUntil(() => _clickReference.action.WasPressedThisFrame());
        yield return new WaitForSeconds(0.5f);
    }

    private Sequence demoSequence()
    {
        
        Sequence pinballsep = DOTween.Sequence();
        pinballsep.Append(_background.DOFade(1, 0.5f).OnComplete(() => {_camera.transform.position = _demoPinballCameraFoots[0].position; _camera.transform.LookAt(_pinballCenter);_camera.cullingMask &= ~(1 << LayerMask.NameToLayer("DemoLayer"));}))
        .AppendInterval(0.2f)
        .Append(_background.DOFade(0, 0.5f))
        .Append(_camera.transform.DOPath(
        new[]
        {
            _demoPinballCameraFoots[1].position,
            _demoPinballCameraFoots[2].position,
        },
        5f, PathType.CatmullRom).SetLookAt(_pinballCenter))
        .Append(_background.DOFade(1, 0.5f).OnComplete(() => {_camera.transform.position = _demoPinballCameraFoots[3].position;_camera.transform.LookAt(_pinballCenter);}))
        .AppendInterval(0.2f)
        .Append(_background.DOFade(0, 0.5f))
        .Append(_camera.transform.DOPath(
        new[]
        {
            _demoPinballCameraFoots[4].position,
            _demoPinballCameraFoots[5].position,
        },
        5f, PathType.CatmullRom).SetLookAt(_pinballCenter))
        .Append(_background.DOFade(1, 0.5f).OnComplete(() => {_camera.transform.position = _demoCameraPosition.transform.position; _camera.transform.eulerAngles = _demoCameraPosition.transform.eulerAngles;_camera.cullingMask |= 1 << LayerMask.NameToLayer("DemoLayer");}))
        .AppendInterval(0.2f)
        .Append(_background.DOFade(0, 0.5f));

        return pinballsep;
    }

    private void FindObjectDemo()
    {
        _pinball = GameObject.Find("Demo_pinball");
        _faust = GameObject.Find("Demo_Faust");
        _imp = GameObject.Find("imp");
        _pinballCenter = GameObject.Find("newpinball_eye_inner").transform;
        _demoCameraPosition = GameObject.Find("Demo_CameraPosition");
        
        
        _demoPinballCameraFoots.Add(GameObject.Find("CinemachineCameraPoint1").transform);
        _demoPinballCameraFoots.Add(GameObject.Find("CinemachineCameraPoint2").transform);
        _demoPinballCameraFoots.Add(GameObject.Find("CinemachineCameraPoint3").transform);
        _demoPinballCameraFoots.Add(GameObject.Find("CinemachineCameraPoint4").transform);
        _demoPinballCameraFoots.Add(GameObject.Find("CinemachineCameraPoint5").transform);
        _demoPinballCameraFoots.Add(GameObject.Find("CinemachineCameraPoint6").transform);
    }
}

