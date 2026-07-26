using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using System;
using System.Collections.Generic;

public class VisitorUIViewer : MonoBehaviour
{
    // [SerializeField] private TMP_Text _nameText;
    // [SerializeField] private TMP_Text _conversationText;

    // [Header("会話用の設定")]
    // [SerializeField] private InputActionReference _clickReference;
    // private float _characterSpeed = 0.1f;//文字を書くスピード
    // // Start is called once before the first execution of Update after the MonoBehaviour is created
    // void Start()
    // {
        
    // }

    // /// <summary>
    // /// 下矢印を改行に変換する
    // /// </summary>
    // /// <param name="str"></param>
    // /// <returns></returns>
    // private string AjustDownArrow(string str)
    // {
    //     string result = null;
    //     foreach (var c in str)
    //     {
    //         if (c != '↓')
    //             result += c;
    //         else
    //             result += '\n';
    //     }

    //     return result;
    // }

    // /// <summary>
    // /// イベントキーの内容を基にテキストメッセージを書き換えていく
    // /// </summary>
    // /// <param name="key">イベントキー</param>
    // /// <returns></returns>
    // private IEnumerator TextSystem(VisitorInstance visitor)
    // {
    //     if(GetConversation(key) == null)//指定したキーが存在しない場合
    //         yield break;

    //     //指定されたBGMに設定・再生(同じBGMの場合は無視)
    //     if (_audioSource.clip != _clipDictionary[_conversations[key].bgmKey] || !_audioSource.isPlaying)
    //     {
    //         _audioSource.clip = _clipDictionary[_conversations[key].bgmKey];
    //         _audioSource.Play();
    //     }

    //     for (int i = 0; i < _conversations[key].lines.Length; i++)
    //     {
    //         _conversationText.text = "";//テキストを消去
    //         string sentence = AjustDownArrow(_conversations[key].lines[i]);

    //         //アクマの表情を変化させる


    //         //テキストを一文字ずつ描画
    //         foreach (char c in sentence)
    //         {
    //             //途中でトリガーボタンを押すとテキストを全て出力
    //             if (_clickReference.action.IsPressed())
    //             {
    //                 _conversationText.text = sentence;
    //                 break;
    //             }
    //             else
    //                 _conversationText.text += c;

    //             yield return new WaitForSeconds(_characterSpeed);
    //         }

    //         //次に進むためのクリック入力
    //         yield return new WaitUntil(() => _clickReference.action.WasPressedThisFrame());
    //         yield return new WaitForSeconds(0.5f);
    //     }
    // }
}
