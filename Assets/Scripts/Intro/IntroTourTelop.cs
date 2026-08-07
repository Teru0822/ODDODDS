using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace App.Intro
{
    /// <summary>
    /// 導入ツアーのテロップ表示。タイトルの悪魔会話と同じく1文字ずつ表示し、
    /// スペースキーで「タイプ中なら全文表示 → 表示済みなら次のテロップへ」と送る。
    /// テキスト自体は <see cref="IntroTourDirector"/> のカットごとに Inspector で編集する。
    /// </summary>
    [DisallowMultipleComponent]
    public class IntroTourTelop : MonoBehaviour
    {
        [Header("UI")]
        [Tooltip("テロップ全体。フェードイン/アウトに使用します")]
        [SerializeField] private CanvasGroup _canvasGroup;

        [Tooltip("本文を流し込む TextMeshPro")]
        [SerializeField] private TMP_Text _text;

        [Tooltip("使用するフォント。未指定なら _text の設定をそのまま使います")]
        [SerializeField] private TMP_FontAsset _fontAsset;

        [Tooltip("「スペースキーで送る」等の操作ガイド。テロップ表示中だけ出したい場合に指定")]
        [SerializeField] private GameObject _advanceHint;

        [Tooltip("操作ガイドを出すまでの待ち時間(秒)。全文表示から少し遅れて出すと親切です")]
        [SerializeField] private float _advanceHintDelay = 0.6f;

        [Header("表示速度")]
        [Tooltip("1文字あたりの表示間隔(秒)")]
        [SerializeField] private float _characterInterval = 0.06f;

        [Tooltip("テロップの出現にかける時間(秒)")]
        [SerializeField] private float _fadeInDuration = 0.25f;

        [Tooltip("テロップが消えるまでの時間(秒)")]
        [SerializeField] private float _fadeOutDuration = 0.25f;

        [Tooltip("全文表示後、送り入力を受け付けるまでの最短待ち時間(秒)。誤爆防止")]
        [SerializeField] private float _minHoldAfterLine = 0.15f;

        [Header("文字送り音")]
        [Tooltip("再生用 AudioSource。未指定なら自身から取得/追加します")]
        [SerializeField] private AudioSource _audioSource;

        [Tooltip("1文字ごとに鳴らす効果音")]
        [SerializeField] private AudioClip _typingSound;

        [Tooltip("ピッチの最小値。低くすると悪魔の唸り声のようになります")]
        [SerializeField] private float _typingPitchMin = 0.6f;

        [Tooltip("ピッチの最大値")]
        [SerializeField] private float _typingPitchMax = 0.8f;

        [Tooltip("音が連続しすぎないための最低間隔(秒)")]
        [SerializeField] private float _minTypingInterval = 0.05f;

        [Range(0f, 1f)]
        [Tooltip("文字送り音の音量")]
        [SerializeField] private float _typingVolume = 0.7f;

        private float _lastTypingTime;
        private bool _visible;

        /// <summary>テロップ送りに使うキーが押されたか。</summary>
        public static bool AdvancePressed =>
            Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;

        private void Awake()
        {
            if (_audioSource == null) _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null) _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.playOnAwake = false;
            _audioSource.spatialBlend = 0f;
            if (_text != null && _fontAsset != null) _text.font = _fontAsset;
            HideImmediate();
        }

        /// <summary>
        /// 1カット分のテロップを順に表示する。全て送り終えるまで完了しない。
        /// </summary>
        public IEnumerator PlayLines(IReadOnlyList<string> lines)
        {
            if (lines == null || lines.Count == 0) yield break;

            yield return FadeIn();

            for (int i = 0; i < lines.Count; i++)
            {
                if (string.IsNullOrEmpty(lines[i])) continue;

                yield return TypeLine(ConvertArrows(lines[i]));
                yield return WaitForAdvance();
            }

            yield return FadeOut();
        }

        /// <summary>フェードを挟まず即座に隠す。スキップ時や後片付けに使う。</summary>
        public void HideImmediate()
        {
            _visible = false;
            SetHintActive(false);

            if (_canvasGroup != null)
            {
                _canvasGroup.DOKill();
                _canvasGroup.alpha = 0f;
                _canvasGroup.gameObject.SetActive(false);
            }

            if (_audioSource != null) _audioSource.pitch = 1f;
        }

        private IEnumerator FadeIn()
        {
            if (_visible) yield break;
            _visible = true;

            if (_canvasGroup == null) yield break;

            _canvasGroup.DOKill();
            _canvasGroup.gameObject.SetActive(true);
            yield return _canvasGroup.DOFade(1f, _fadeInDuration).WaitForCompletion();
        }

        private IEnumerator FadeOut()
        {
            SetHintActive(false);

            if (_canvasGroup == null)
            {
                _visible = false;
                yield break;
            }

            _canvasGroup.DOKill();
            yield return _canvasGroup.DOFade(0f, _fadeOutDuration).WaitForCompletion();
            if (_text != null) _text.text = "";
            _canvasGroup.gameObject.SetActive(false);
            _visible = false;
        }

        /// <summary>1行を1文字ずつ表示する。途中でスペースを押されたら即座に全文表示して抜ける。</summary>
        private IEnumerator TypeLine(string line)
        {
            SetHintActive(false);
            if (_text != null) _text.text = "";

            for (int i = 0; i < line.Length; i++)
            {
                // タイプ中のスペースは「早送り」。ここで送り自体は行わない
                if (AdvancePressed)
                {
                    if (_text != null) _text.text = line;
                    break;
                }

                if (_text != null) _text.text += line[i];
                PlayTypingSound(line[i]);

                yield return new WaitForSeconds(_characterInterval);
            }

            if (_text != null) _text.text = line;
            if (_audioSource != null) _audioSource.pitch = 1f;
        }

        /// <summary>全文表示後、次のテロップへ送る入力を待つ。</summary>
        private IEnumerator WaitForAdvance()
        {
            // 直前の早送り入力をそのまま送りとして拾わないよう、最低限の間を置く
            if (_minHoldAfterLine > 0f) yield return new WaitForSeconds(_minHoldAfterLine);

            float hintTimer = 0f;
            while (!AdvancePressed)
            {
                hintTimer += Time.deltaTime;
                if (hintTimer >= _advanceHintDelay) SetHintActive(true);
                yield return null;
            }

            SetHintActive(false);
        }

        private void PlayTypingSound(char c)
        {
            if (_typingSound == null || _audioSource == null) return;
            if (c == ' ' || c == '\n' || c == '　') return;
            if (Time.time - _lastTypingTime < _minTypingInterval) return;

            _audioSource.pitch = Random.Range(_typingPitchMin, _typingPitchMax);
            _audioSource.PlayOneShot(_typingSound, _typingVolume);
            _lastTypingTime = Time.time;
        }

        private void SetHintActive(bool active)
        {
            if (_advanceHint != null && _advanceHint.activeSelf != active)
            {
                _advanceHint.SetActive(active);
            }
        }

        /// <summary>タイトルの会話と同じ記法。「↓」を改行として扱う。</summary>
        private static string ConvertArrows(string source)
        {
            return string.IsNullOrEmpty(source) ? source : source.Replace('↓', '\n');
        }
    }
}
