using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using DG.Tweening;
using UnityEngine.UI;

public class ResultUIManager : MonoBehaviour
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

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

    }

    public IEnumerator GameOverAnimation()
    {
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
        Debug.Log("次のページへ移動");
    }

    /// <summary>
    /// RoguelikeDatailLogの項目を前に移動
    /// </summary>
    public void PreviousPage()
    {
        if (_gameNames.TryGetValue(_roguelikeDatailLogIndex - 1, out var value))
        {
            _roguelikeDatailLogIndex--;
            _gameName.text = value;

            //TODO;前のゲームにおけるローグライク要素の結果を取得し、Text更新
        }

        Debug.Log("前のページへ移動");
    }

    /// <summary>
    /// タイトルシーンに遷移
    /// </summary>
    public void BackToTitle()
    {
        SceneManager.LoadScene(0);
    }
}
