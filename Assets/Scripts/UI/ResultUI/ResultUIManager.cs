using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using DG.Tweening;
using UnityEngine.UI;

public class ResultUIManager : MonoBehaviour, IsaveDataProvider
{
    [Header("ゲームオーバー用")]
    [SerializeField] private Image _gameOverPanel;
    [SerializeField] private TMP_Text _gameOverMessage;
    [SerializeField] private GameObject _titleButton;

    [Header("ResultLogPanel用")]
    [SerializeField] private GameObject _resultLogPanel;
    [SerializeField] private TMP_Text _resultDatailLog;

    [Header("RoguelikeLogPanel用")]
    [SerializeField] private GameObject _roguelikeLogPanel;
    [SerializeField] private TMP_Text _roguelikeDatailLog;
    [SerializeField] private TMP_Text _gameName;
    [SerializeField] private SerializeDictionary<int, string> _gameNames = new SerializeDictionary<int, string>();
    private int _roguelikeDatailLogIndex = 0;

    Sequence gameOverAnimSequence;

    //ゲームオーバー時に出す情報を格納する情報を格納する
    [Header("情報を表示させる用のText")]
    [SerializeField] private Language _language;//言語
    [SerializeField] private TMP_Text _playTimeText;//プレイ時間
    [SerializeField] private TMP_Text _surviveTurnText;//生きたターン数
    [SerializeField] private TMP_Text _earnMoneyCountText;//稼いだ金額
    [SerializeField] private TMP_Text _getItemCountText;//手に入れたアイテム数
    [SerializeField] private TMP_Text _useItemCountText;//手に入れたアイテム数
    [SerializeField] private TMP_Text _tradeVisitorCountText;//トレードを行った回数

    public void WriteSaveData(RoguelikeSaveData saveData)
    {
        //何もしない
    }

    public void ReadSaveData(RoguelikeSaveData saveData)
    {
        //データ表示に必要なものを取得する
        if(_language == Language.JP)
        {
            _playTimeText.text = "プレイ時間：" + saveData.playTime.ToString();
            _surviveTurnText.text = "生きたターン：" + saveData.nowTurn.ToString();
            _earnMoneyCountText.text = "稼いだ金額：" + saveData.earnMoney.ToString();
            _getItemCountText.text = "手に入れたアイテム数：" + saveData.getItemCount.ToString();
            _useItemCountText.text = "使用したアイテム数：" + saveData.useItemCount.ToString();
            _tradeVisitorCountText.text = "取引回数：" + saveData.tradeTimes.ToString();
        }
        else if(_language == Language.EN)
        {
            _playTimeText.text = "Play time：" + saveData.playTime.ToString();
            _surviveTurnText.text = "Survived turn：" + saveData.nowTurn.ToString();
            _earnMoneyCountText.text = "Earned money：" + saveData.earnMoney.ToString();
            _getItemCountText.text = "Obtained item count：" + saveData.getItemCount.ToString();
            _useItemCountText.text = "used item count：" + saveData.useItemCount.ToString();
            _tradeVisitorCountText.text = "Trade times：" + saveData.tradeTimes.ToString();
        }
    }

    public IEnumerator GameOverAnimation()
    {
        RoguelikeSaveManager.Save();
        RoguelikeSaveManager.Load();
        yield return new WaitUntil(() => _tradeVisitorCountText.text != "");//ロードが終わるまで待機

        gameOverAnimSequence = DOTween.Sequence();
        gameOverAnimSequence.Append(_gameOverPanel.DOFade(endValue: 1f, duration: 2f))
            .Append(_gameOverMessage.DOFade(endValue: 1f, duration: 1f));


        yield return gameOverAnimSequence.WaitForCompletion();//アニメーションが終わるまで待つ
        _resultLogPanel.SetActive(true);
        yield return new WaitForSeconds(1.0f);
        _roguelikeLogPanel.SetActive(true);
        yield return new WaitForSeconds(1.0f);
        _titleButton.SetActive(true);
        yield return null;
    }

    /// <summary>
    /// RoguelikeDatailLogの項目を次に移動
    /// </summary>
    public void NextPage()
    {
        if (_gameNames.TryGetValue(_roguelikeDatailLogIndex + 1, out var value))
        {
            _roguelikeDatailLogIndex++;
            _gameName.text = value;

            //TODO;次のゲームにおけるローグライク要素の結果を取得し、Text更新
        }
    }

    /// <summary>
    /// RoguelikeDatailLogの項目を次に移動
    /// </summary>
    public void PreviousPage()
    {
        if (_gameNames.TryGetValue(_roguelikeDatailLogIndex - 1, out var value))
        {
            _roguelikeDatailLogIndex--;
            _gameName.text = value;

            //TODO;前のゲームにおけるローグライク要素の結果を取得し、Text更新
        }

    }

    /// <summary>
    /// �^�C�g���V�[���ɑJ��
    /// </summary>
    public void BackToTitle()
    {
        SceneManager.LoadScene(0);
    }
}
