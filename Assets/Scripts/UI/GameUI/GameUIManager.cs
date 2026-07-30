using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UniRx;
using UnityEngine.UI;


public class GameUIManager : MonoBehaviour
{
    [Header("ゲーム内のオブジェクト")]
    [SerializeField] private TMP_Text _turnText;
    [SerializeField] private TMP_Text _nextDebtTurnText;
    [SerializeField] private TMP_Text _moneyText;
    [SerializeField] private TMP_Text _unwashedMoneyText;

    [Header("playerInfoPanel内のオブジェクト")]
    [SerializeField] private GameObject _playerInfoPanel;
    [SerializeField] private TMP_Text _moneyText_info;
    [SerializeField] private TMP_Text _unwashedMoneyText_info;
    [SerializeField] private TMP_Text _bronzeCoinText_info;
    [SerializeField] private TMP_Text _silverCoinText_info;
    [SerializeField] private TMP_Text _goldCoinText_info;
    [SerializeField] private TMP_Text _blackDiamondText_info;
    [SerializeField] private TMP_Text _virtuePointText;
    [SerializeField] private TMP_Text _playerNameText;
    [SerializeField] private TMP_Text _leftDebtMoneyText;
    [SerializeField] private TMP_Text _nextQuotaText;

    [Header("itemPanel内のオブジェクト")]
    [SerializeField] private GameObject _itemPanel;

    [Header("roguelikePanel内のオブジェクト")]
    [SerializeField] private GameObject _roguelikePanel;

    [Header("UnwashCoinの個別UI")]
    [SerializeField] private TMP_Text _bronzeCoinText;
    [SerializeField] private TMP_Text _silverCoinText;
    [SerializeField] private TMP_Text _goldCoinText;
    [SerializeField] private TMP_Text _blackDiamondText;


    [Header("メニュー用のSettings")]
    [SerializeField] private InputActionReference _openMenuReference;//Tabキーを押したらメニュー表示
    [SerializeField] private SerializeDictionary<int, GameObject> _menuTitle = new SerializeDictionary<int, GameObject>();
    private bool _isOpenMenu = false;
    private int _index = 0;

    [Header("アイテム取得・消失用UI")]
    [SerializeField] private GameObject _getOrLostPanel;
    [SerializeField] private TMP_Text _titleText;
    [SerializeField] private List<GameObject> _popUpListIndex;

    private  Queue<ScriptableObject> _getQueue = new();
    private  Queue<ScriptableObject> _lostQueue = new();

    private Coroutine _getOrLostAnimationCoroutine;

    //その他プライベート変数
    private float _previousMoneyValue = 0;
    private float _previousUnwashedMoneyValue = 0;
    private int _previousVirtuePointValue = 0;
    private int _previousBronzeCoinValue = 0;
    private int _previousSilverCoinValue = 0;
    private int _previousGoldCoinValue = 0;
    private int _previousBlackDiamondValue = 0;

    /// <summary>
    /// 初期化を行う処理
    /// </summary>
    /// <param name="wallet"></param>
    private void Init(PlayerWallet wallet)
    {
        _previousMoneyValue = wallet.WashedAmount;
        _previousUnwashedMoneyValue = wallet.UnwashedAmount;
        _previousVirtuePointValue = wallet.VirtuePoints;
        _previousBronzeCoinValue = wallet.BronzeCoins;
        _previousSilverCoinValue = wallet.SilverCoins;
        _previousGoldCoinValue = wallet.GoldCoins;
        _previousBlackDiamondValue = wallet.BlackDiamonds;


        //以前の数値を記録する変数の初期化
        wallet.OnWashedMoneyAmountChange
            .Subscribe(x =>
            {
                //お金の更新処理
                DOTween.To(() => _previousMoneyValue,//ターゲットとなる変数
                        num => _previousMoneyValue = num,    //値の更新を行う
                        x,                                     //最終的な値
                        1.0f                                   //時間
                        ).OnUpdate(() =>
                        {
                            _moneyText.text = _previousMoneyValue.ToString("N0");
                            _moneyText_info.text = _previousMoneyValue.ToString("N0");
                        }); 
            }).AddTo(this);

        wallet.OnUnwashedMoneyAmountChange
            .Subscribe(x =>
            {
                //未洗浄のお金の更新処理
                DOTween.To(() => _previousUnwashedMoneyValue,//ターゲットとなる変数
                        num => _previousUnwashedMoneyValue = num,    //値の更新を行う
                        x,                                     //最終的な値
                        1.0f                                   //時間
                        ).OnUpdate(() =>
                        {
                            //_unwashedMoneyText.text = _previousUnwashedMoneyValue.ToString("N0");
                            //_unwashedMoneyText_info.text = _previousUnwashedMoneyValue.ToString("N0");
                        });
            }).AddTo(this);

        wallet.OnvirtuePointAmountChange
            .Subscribe(x =>
            {
                //恒常ポイントの更新処理
                DOTween.To(() => _previousVirtuePointValue,//ターゲットとなる変数
                        num => _previousVirtuePointValue = num,    //値の更新を行う
                        x,                                     //最終的な値
                        1.0f                                   //時間
                        ).OnUpdate(() =>
                        {
                            _virtuePointText.text = _previousVirtuePointValue.ToString("N0");
                        });
            }).AddTo(this);

        bool isBronzeInitialized = false;
        wallet.OnBronzeCoinsChange
            .Subscribe(x =>
            {
                if (!isBronzeInitialized)
                {
                    _previousBronzeCoinValue = x;
                    isBronzeInitialized = true;
                    if (_bronzeCoinText != null)
                    {
                        _bronzeCoinText.text = "x" + x.ToString("N0");
                        _bronzeCoinText_info.text = "x" + x.ToString("N0");
                    }
                    
                    return;
                }

                if (_bronzeCoinText != null)
                {
                    DOTween.To(() => _previousBronzeCoinValue,
                        num => _previousBronzeCoinValue = num,
                        x,
                        1.0f
                    ).OnUpdate(() =>
                    {
                        _bronzeCoinText.text = "x" + _previousBronzeCoinValue.ToString("N0");
                        _bronzeCoinText_info.text = "x" + _previousBronzeCoinValue.ToString("N0");
                    });
                }
            }).AddTo(this);

        bool isSilverInitialized = false;
        wallet.OnSilverCoinsChange
            .Subscribe(x =>
            {
                if (!isSilverInitialized)
                {
                    _previousSilverCoinValue = x;
                    isSilverInitialized = true;
                    if (_silverCoinText != null)
                    {
                        _silverCoinText.text = "x" + x.ToString("N0");
                        _silverCoinText_info.text = "x" + x.ToString("N0");
                    } 
                    return;
                }

                if (_silverCoinText != null)
                {
                    DOTween.To(() => _previousSilverCoinValue,
                        num => _previousSilverCoinValue = num,
                        x,
                        1.0f
                    ).OnUpdate(() =>
                    {
                        _silverCoinText.text = "x" + _previousSilverCoinValue.ToString("N0");
                        _silverCoinText_info.text = "x" + _previousSilverCoinValue.ToString("N0");
                    });
                }
            }).AddTo(this);

        bool isGoldInitialized = false;
        wallet.OnGoldCoinsChange
            .Subscribe(x =>
            {
                if (!isGoldInitialized)
                {
                    _previousGoldCoinValue = x;
                    isGoldInitialized = true;
                    if (_goldCoinText != null)
                    {
                        _goldCoinText.text = "x" + x.ToString("N0");
                        _goldCoinText_info.text = "x" + x.ToString("N0");
                    } 
                    return;
                }

                if (_goldCoinText != null)
                {
                    DOTween.To(() => _previousGoldCoinValue,
                        num => _previousGoldCoinValue = num,
                        x,
                        1.0f
                    ).OnUpdate(() =>
                    {
                        _goldCoinText.text = "x" + _previousGoldCoinValue.ToString("N0");
                        _goldCoinText_info.text = "x" + _previousGoldCoinValue.ToString("N0");
                    });
                }
            }).AddTo(this);

        bool isBlackDiamondInitialized = false;
        wallet.OnBlackDiamondsChange
            .Subscribe(x =>
            {
                if (!isBlackDiamondInitialized)
                {
                    _previousBlackDiamondValue = x;
                    isBlackDiamondInitialized = true;
                    if (_blackDiamondText != null)
                    {
                        _blackDiamondText.text = "x" + x.ToString("N0");
                        _blackDiamondText_info.text = "x" + x.ToString("N0");
                    }                   
                    return;
                }

                if (_blackDiamondText != null)
                {
                    DOTween.To(() => _previousBlackDiamondValue,
                        num => _previousBlackDiamondValue = num,
                        x,
                        1.0f
                    ).OnUpdate(() =>
                    {
                        _blackDiamondText.text = "x" + _previousBlackDiamondValue.ToString("N0");
                        _blackDiamondText_info.text = "x" + _previousBlackDiamondValue.ToString("N0");
                    });
                }
            }).AddTo(this);

        MoneyManager.Instance.OnCurrentTurnChange.Subscribe(turnNum => _turnText.text = turnNum.ToString("000")).AddTo(this);
        MoneyManager.Instance.OnNextDebtCollectionTurnChange.Subscribe(turnNum => _nextDebtTurnText.text = turnNum.ToString("00")).AddTo(this);
        MoneyManager.Instance.OnQuotaAmount.Subscribe(quotaNum => _nextQuotaText.text = quotaNum.ToString("00")).AddTo(this);
        MoneyManager.Instance.OnLeftDebtAmount.Subscribe(leftDebtNum => _leftDebtMoneyText.text = leftDebtNum.ToString("00")).AddTo(this);
        Debug.Log(this.name + "初期化完了");
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        _openMenuReference.action.Enable();

        //初期化処理
        Observable.EveryUpdate()
            .Select(_ => PlayerWallet.Local)
            .Where(target => target != null)
            .First()
            .Subscribe(target =>
            {
                Init(target);
            })
            .AddTo(this);

        // UFOキャッチャーのモード切り替えイベントを購読
        UFOCameraController.OnUfoModeChanged += HandleUfoModeChanged;
        // タイプライター報酬選択UIの表示切り替えイベントを購読
        RewardSelectionUI.OnTypewriterUIChanged += HandleTypewriterUIChanged;
    }

    private void Update()
    {
        //Tabキーを押したときにメニュー切り替え
        if (_openMenuReference.action.WasPressedThisFrame())
        {
            _isOpenMenu = !_isOpenMenu;
            if(_menuTitle.TryGetValue(_index, out var value))
                value.SetActive(_isOpenMenu);

            if (_isOpenMenu)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                _index = 0;
            }
        }
    }

    /// <summary>
    /// メニュー内のページを切り替えるボタンを押したときの処理
    /// </summary>
    public void ChangePageButton(string type)
    {
        if (type == "Previous")
            _index -= 1;
        else if (type == "Next")
            _index += 1;

        switch (_index)
        { 
            case 0://プレイヤー情報ページ
                _playerInfoPanel.SetActive(true);
                _itemPanel.SetActive(false);
                _roguelikePanel.SetActive(false);
                break;
            case 1://恒常アイテムの説明ページ
                _playerInfoPanel.SetActive(false);
                _itemPanel.SetActive(true);
                _roguelikePanel.SetActive(false);
                break;
            case 2://これ以降は各ミニゲームのローグライク要素を記述
                _playerInfoPanel.SetActive(false);
                _itemPanel.SetActive(false);
                _roguelikePanel.SetActive(true);
                break;
            default:
                Debug.LogError("想定外の数値");
                break;
        }
    }

    private void OnDestroy()
    {
        UFOCameraController.OnUfoModeChanged -= HandleUfoModeChanged;
        RewardSelectionUI.OnTypewriterUIChanged -= HandleTypewriterUIChanged;
    }

    private void HandleUfoModeChanged(bool isPlayingUfo)
    {
        gameObject.SetActive(!isPlayingUfo);
    }

    private void HandleTypewriterUIChanged(bool isShowing)
    {
        gameObject.SetActive(!isShowing);
    }
    /// <summary>
    /// アイテム、エフェクト、お金（訪問者イベントのみ）を取得・消失した際にそれぞれキューに追加する。
    /// </summary>
    /// <param name="isGet"></param>
    /// <param name="masterData"></param>
    public void AddPopupQueue(bool isGet,ScriptableObject masterData)
    {
        if (isGet)
        {
            _getQueue.Enqueue(masterData);

            if (_getOrLostAnimationCoroutine == null)
                _getOrLostAnimationCoroutine = StartCoroutine(GetOrLostAnimation(true));
        }
        else
        {
            _lostQueue.Enqueue(masterData);

            if (_getOrLostAnimationCoroutine == null)
                _getOrLostAnimationCoroutine = StartCoroutine(GetOrLostAnimation(false));
        }
    }

    /// <summary>
    /// 取得・消失した時のUIアニメーションを再生させるコルーチン
    /// </summary>
    /// <param name="isGet"></param>
    /// <returns></returns>
    private IEnumerator GetOrLostAnimation(bool isGet)
    {
        //最初のセットアップ
        Queue<ScriptableObject> queue = isGet ? _getQueue : _lostQueue;
        _titleText.text = isGet ? "Get" : "Lost";
        _titleText.color = isGet ? Color.skyBlue : Color.red;
        _getOrLostPanel.SetActive(true);


        if (!_getOrLostPanel.TryGetComponent<CanvasGroup>(out var canvasGroup))
        {
            canvasGroup = _getOrLostPanel.AddComponent<CanvasGroup>();
        }

        //_getOrLostPanelが0.5秒かけてフェードアウト（出現）してくる
        canvasGroup.alpha = 0f;
        yield return canvasGroup.DOFade(1f, 0.5f).WaitForCompletion();

        //手に入れたアイテム・エフェクトを順にスケールアップ（0.5秒かけてスケールを1に変化）させる        
        int index = 0;
        while (true)
        {
            // キューが空なら終了(３秒だけ猶予は与える)
            if (queue.Count == 0)
            {
                // 現在表示中の内容を3秒見せる
                yield return new WaitForSeconds(3f);

                // その間に追加されなかったら一旦終了
                if (queue.Count == 0)
                {
                    Sequence disappear = DOTween.Sequence();
                    for (int i = 0; i < index; i++)
                    {
                        disappear.Insert(i * 0.1f, _popUpListIndex[i].transform.DOScale(Vector3.zero, 0.3f));
                    }
                    yield return disappear.WaitForCompletion();
                    break;
                }
            }

            // 最大件数を表示したら一旦待機して消す
            if(index == _popUpListIndex.Count)
            {
                Sequence disappear = DOTween.Sequence();
                for (int i = 0; i < _popUpListIndex.Count; i++)
                {
                    disappear.Insert(i * 0.1f, _popUpListIndex[i].transform.DOScale(Vector3.zero, 0.3f));
                }
                
                yield return disappear.WaitForCompletion();
                index = 0;
            }

            //データをもとにUI表示アニメーションを再生
            ScriptableObject data = queue.Dequeue();
            Image image = _popUpListIndex[index].transform.Find("Image").GetComponentInChildren<Image>();
            TMP_Text text = _popUpListIndex[index].GetComponentInChildren<TMP_Text>();

            if (data is ItemData itemData)
            {
                image.sprite = itemData.iconImage;
                text.text = itemData.itemName;
            }
            else if (data is EffectData effectData)
            {
                image.sprite = effectData.effectIcon;
                text.text = effectData.effectName;
            }
            else if(data is MoneyData moneyData)
            {
                image.sprite = moneyData.iconImage;
                text.text = moneyData.price.ToString("N0") + "DC";
            }

            _popUpListIndex[index].transform.localScale = Vector3.zero;
            yield return _popUpListIndex[index].transform.DOScale(Vector3.one, 0.5f).WaitForCompletion();

            index++;
        }

        //getOrLostPanelが0.5秒かけてフェードイン（消失）する
        yield return canvasGroup.DOFade(0f, 0.5f).WaitForCompletion();
        _getOrLostPanel.SetActive(false);

        //コルーチンの情報をnullにする.そしてもう片方のキューに情報があったら、そっちの情報を記載するコルーチンを発動
        if(isGet)
        {
            _getOrLostAnimationCoroutine = null;
            if(_lostQueue.Count != 0 && _getOrLostAnimationCoroutine == null)
                _getOrLostAnimationCoroutine = StartCoroutine(GetOrLostAnimation(false));
        }
        else
        {
            _getOrLostAnimationCoroutine = null;
            if(_getQueue.Count != 0 && _getOrLostAnimationCoroutine == null)
                _getOrLostAnimationCoroutine = StartCoroutine(GetOrLostAnimation(true));
        }
    }
}
