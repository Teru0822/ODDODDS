using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using DG.Tweening;

namespace MiniGames.Title
{
    public class TitleConversationManager : MonoBehaviour
    {
        [Header("会話用データパス")]
        [SerializeField] private string _jsonFilePath = "Assets/Resources/Conversations/DevilConversations_JP.json";
        [Tooltip("タイトルで再生する会話のキー（テスト用など）")]
        [SerializeField] private string _targetConversationKey = "TitleTest";

        private Dictionary<string, DevilConversationData> _conversations = new Dictionary<string, DevilConversationData>();

        [Header("会話用UI")]
        [Tooltip("テキストボックス全体（フェードイン・アウト用）")]
        [SerializeField] private CanvasGroup _textBoxCanvasGroup;
        [SerializeField] private TMP_Text _nameText;
        [SerializeField] private TMP_Text _mainSentenceText;
        
        [Header("会話用コンポーネント")]
        [SerializeField] private AudioSource _audioSource;
        [SerializeField] private SerializeDictionary<string, AudioClip> _clipSerializeDictionary = new SerializeDictionary<string, AudioClip>();
        private Dictionary<string, AudioClip> _clipDictionary = new Dictionary<string, AudioClip>();

        [Header("設定")]
        [SerializeField] private TMP_FontAsset _fontAsset;
        private float _characterSpeed = 0.1f;

        private void Start()
        {
            if (_textBoxCanvasGroup != null)
            {
                _textBoxCanvasGroup.alpha = 0f;
                _textBoxCanvasGroup.gameObject.SetActive(false);
            }
            
            if (_clipSerializeDictionary != null)
            {
                _clipDictionary = _clipSerializeDictionary.GetDictionary;
            }

            if (_fontAsset != null && _nameText != null && _mainSentenceText != null)
            {
                _nameText.font = _fontAsset;
                _mainSentenceText.font = _fontAsset;
            }
            
            LoadConversationData();
        }

        private void LoadConversationData()
        {
            if (string.IsNullOrEmpty(_jsonFilePath)) return;

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
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"[TitleConversationManager] JSON読み込みエラー: {e.Message}");
                }
            }
        }

        private DevilConversationData GetConversation(string key)
        {
            if (_conversations != null && _conversations.TryGetValue(key, out var data))
            {
                return data;
            }
            return null;
        }

        private string AjustDownArrow(string str)
        {
            string result = "";
            foreach (var c in str)
            {
                if (c != '↓') result += c;
                else result += '\n';
            }
            return result;
        }

        /// <summary>
        /// 外部（TitlePlayButton等）から呼ばれる会話開始エントリーポイント
        /// </summary>
        public void StartConversation(Action onComplete)
        {
            StartCoroutine(RunConversationSequence(onComplete));
        }

        private IEnumerator RunConversationSequence(Action onComplete)
        {
            DevilConversationData data = GetConversation(_targetConversationKey);
            if (data == null)
            {
                Debug.LogWarning($"[TitleConversationManager] キー '{_targetConversationKey}' が見つかりません。テストデータを生成します。");
                data = new DevilConversationData
                {
                    key = _targetConversationKey,
                    lines = new string[] { "よく来たな、哀れな人間よ。↓ここがお前の最後の場所だ。", "準備はいいか？↓フフフ……。" },
                    bgmKey = ""
                };
            }

            // テキストボックスのフェードイン
            if (_textBoxCanvasGroup != null)
            {
                _textBoxCanvasGroup.gameObject.SetActive(true);
                yield return _textBoxCanvasGroup.DOFade(1f, 0.3f).WaitForCompletion();
            }

            // BGMの再生
            if (_audioSource != null && !string.IsNullOrEmpty(data.bgmKey) && _clipDictionary.ContainsKey(data.bgmKey))
            {
                _audioSource.clip = _clipDictionary[data.bgmKey];
                _audioSource.Play();
            }

            if (_nameText != null) _nameText.text = "悪魔";

            // テキストの1文字ずつの表示
            for (int i = 0; i < data.lines.Length; i++)
            {
                if (_mainSentenceText != null) _mainSentenceText.text = "";
                string sentence = AjustDownArrow(data.lines[i]);

                foreach (char c in sentence)
                {
                    if (Mouse.current != null && Mouse.current.leftButton.isPressed)
                    {
                        if (_mainSentenceText != null) _mainSentenceText.text = sentence;
                        break;
                    }
                    else
                    {
                        if (_mainSentenceText != null) _mainSentenceText.text += c;
                    }
                    yield return new WaitForSeconds(_characterSpeed);
                }

                yield return new WaitUntil(() => Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame);
                
                yield return new WaitForSeconds(0.2f);
            }

            // テキストボックスのフェードアウト
            if (_textBoxCanvasGroup != null)
            {
                yield return _textBoxCanvasGroup.DOFade(0f, 1f).WaitForCompletion();
                _textBoxCanvasGroup.gameObject.SetActive(false);
            }

            onComplete?.Invoke();
        }
    }
}
