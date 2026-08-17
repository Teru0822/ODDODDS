using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

public class RoguelikePanelManager : MonoBehaviour,ILanguage
{
    public static RoguelikePanelManager Instance;
    [Header("スキル表示用オブジェクト")]
    [SerializeField] private Button _skillTypeOptionButton;
    [SerializeField] private TMP_Text _selectedTypeText;
    [SerializeField] private GameObject _skillTypeScrollView;
    [SerializeField] private List<Button> _skillTypeButtons = new List<Button>();

    [SerializeField] private List<Button> _skillButtons = new List<Button>();

    [Header("説明用オブジェクト")]
    [SerializeField] private TMP_Text _detailDescriptionText;
    [SerializeField] private Image _detailIconImage;
    private RoguelikeManager _roguelikeManager;

    private Subject<RoguelikeManager> _initEvent = new Subject<RoguelikeManager>();
    public IObserver<RoguelikeManager> OnInitEvent { get { return _initEvent; } }
    private Language _language;

    private void Awake()
    {
        if(Instance == null)
            Instance = this;
        else
            Destroy(this.gameObject);

        _initEvent.Subscribe(manager => _roguelikeManager = manager);

        // GameUIが非アクティブ状態でシーンが開始した場合（introツアー等）、
        // RoguelikeManager.Start()からのOnInitEvent.OnNextが来ないため自前で取得する
        if (_roguelikeManager == null)
            _roguelikeManager = FindFirstObjectByType<RoguelikeManager>();
    }

    private void Start()
    {
        //初期化処理
        Observable.EveryUpdate()
            .Select(_ => _roguelikeManager)
            .Where(target => target != null)
            .First()
            .Subscribe(target =>
            {
                UpdateUI();//_roguelikeManagerの中がNullじゃなくなったら一回UIを更新しておく
            })
            .AddTo(this);

        //_skillTypeOptionButtonなどの挙動をセットする
        _skillTypeOptionButton.onClick.AddListener(() =>
        {
            if (!_skillTypeScrollView.activeSelf)
                _skillTypeScrollView.SetActive(true);
            else
                _skillTypeScrollView.SetActive(false);
        });

        for (int i = 0; i < _skillTypeButtons.Count; i++)
        {
            _skillTypeButtons[i].onClick.RemoveAllListeners();
            int idx = i;
            _skillTypeButtons[i].onClick.AddListener(() => SetSkillTypeFilter(idx));
        }
    }

    public void SettingLanguage(Language language)
    {
        _language = language;
    }

    private void SetSkillTypeFilter(int type)
    {
        switch (type)
        { 
            case 0: _selectedTypeText.text = "All";  break;
            case 1: _selectedTypeText.text = "PinBall"; break;
            case 2: _selectedTypeText.text = "FallBall"; break;
            case 3: _selectedTypeText.text = "UFOcatcher"; break;
            default: break;
        }
        UpdateUI((SkillType)type);
        _skillTypeScrollView.SetActive(false);
    }

    /// <summary>
    /// スキルのアンロック状況に合わせてScroll Viewのボタンを更新する
    /// </summary>
    /// <param name="type">表示させたいスキルの種類</param>
    public void UpdateUI(SkillType type = SkillType.None)
    {
        if (_roguelikeManager == null)
            _roguelikeManager = FindFirstObjectByType<RoguelikeManager>();
        if (_roguelikeManager == null)
        {
            Debug.LogWarning("[RoguelikePanelManager] RoguelikeManager 未設定のため UpdateUI をスキップします", this);
            return;
        }
        var roguelikeDic = _roguelikeManager.GetUnlockSkillDictionary;
        if (type != SkillType.None)
        {
            roguelikeDic = roguelikeDic.Where(data => data.Value.skillType == type)
                .ToDictionary(data => data.Key, data => data.Value);
        }

        if (_skillButtons == null || _skillButtons.Count == 0)
        {
            Debug.LogWarning("Scroll View内にbuttonがないです");
            return;
        }

        for (int i = 0; i < _skillButtons.Count; i++)
        {
            if (i < roguelikeDic.Count)
            {
                var skill = roguelikeDic.ElementAt(i);
                _skillButtons[i].gameObject.SetActive(true);
                SetButtonText(_skillButtons[i].gameObject, skill.Value.skillName);

                _skillButtons[i].onClick.RemoveAllListeners();
                _skillButtons[i].onClick.AddListener(() => OnSelectSkill(skill.Value));
            }
            else
            {
                _skillButtons[i].gameObject.SetActive(false);
            }
        }

        //説明用のオブジェクトは一旦非表示
        _detailDescriptionText.gameObject.SetActive(false);
        _detailIconImage.gameObject.SetActive(false);
    }

    /// <summary>
    /// ボタンオブジェクト配下からTextまたはTMP_Textを探してテキストを設定する
    /// </summary>
    private void SetButtonText(GameObject btnObj, string text)
    {
        TMP_Text tmpText = btnObj.GetComponentInChildren<TMP_Text>();
        if (tmpText != null)
        {
            tmpText.text = text;
            return;
        }
    }

    /// <summary>
    /// スキルが選択された際の処理
    /// </summary>
    private void OnSelectSkill(RoguelikeData skill)
    {
        if (skill == null) return;

        if (_detailDescriptionText != null)
        {
            _detailDescriptionText.text = skill.skillDescription;
            _detailDescriptionText.gameObject.SetActive(true);
        }

        //if (_detailIconImage != null)
        //{
        //    _detailIconImage.sprite = item.iconImage;
        //    _detailIconImage.gameObject.SetActive(item.iconImage != null);
        //}
    }


}
