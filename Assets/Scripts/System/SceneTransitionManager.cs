using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using DG.Tweening;

namespace MiniGames.Transitions
{
    public class SceneTransitionManager : MonoBehaviour
    {
        public static SceneTransitionManager Instance { get; private set; }

        [Header("トランジションUI")]
        [Tooltip("フェード用のCanvasGroup（黒いモヤの画像などを含める）")]
        [SerializeField] private CanvasGroup _fadeCanvasGroup;
        
        [Tooltip("ロード画面専用のCanvasGroup（Loadingテキストや回転ロゴを含める）")]
        [SerializeField] private CanvasGroup _loadingScreenCanvasGroup;
        
        [Tooltip("ロード中に回転させるロゴのTransform (3Dオブジェクト可)")]
        [SerializeField] private Transform _loadingLogo;
        [Tooltip("ロゴの回転軸（3Dなら Vector3.up (0,1,0) など）")]
        [SerializeField] private Vector3 _logoRotationAxis = Vector3.forward;
        [Tooltip("ロゴの回転速度 (度/秒)")]
        [SerializeField] private float _logoRotationSpeed = 180f;

        [Tooltip("フォント反映用のLoadingテキスト")]
        [SerializeField] private TMP_Text _loadingText;
        [Tooltip("外部指定されたフォントアセット（Owrekynge等）")]
        [SerializeField] private TMP_FontAsset _loadingFontAsset;

        [Header("フェード設定")]
        [SerializeField] private float _fadeDuration = 1.5f;

        private Coroutine _loadingTextCoroutine;
        private Tween _loadingTextFadeTween;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                InitializeUI();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void InitializeUI()
        {
            if (_fadeCanvasGroup != null)
            {
                _fadeCanvasGroup.alpha = 0f;
                _fadeCanvasGroup.gameObject.SetActive(false);
            }

            if (_loadingScreenCanvasGroup != null)
            {
                _loadingScreenCanvasGroup.alpha = 0f;
                _loadingScreenCanvasGroup.gameObject.SetActive(false);
            }

            if (_loadingLogo != null)
            {
                _loadingLogo.gameObject.SetActive(false);
            }

            if (_loadingText != null && _loadingFontAsset != null)
            {
                _loadingText.font = _loadingFontAsset;
                _loadingText.text = "Loading";
            }
        }

        private void Update()
        {
            if (_loadingLogo != null && _loadingLogo.gameObject.activeSelf)
            {
                _loadingLogo.Rotate(_logoRotationAxis, _logoRotationSpeed * Time.deltaTime, Space.Self);
            }
        }

        /// <summary>
        /// 指定したシーンへモヤフェードと専用ロード画面を挟んで非同期遷移します。
        /// </summary>
        public void TransitionToScene(string sceneName, Action onTransitionComplete = null)
        {
            StartCoroutine(TransitionRoutine(sceneName, onTransitionComplete));
        }

        private IEnumerator TransitionRoutine(string sceneName, Action onTransitionComplete)
        {
            // 1. フェードアウト（暗転・モヤ）
            if (_fadeCanvasGroup != null)
            {
                _fadeCanvasGroup.gameObject.SetActive(true);
                yield return _fadeCanvasGroup.DOFade(1f, _fadeDuration).WaitForCompletion();
            }

            // 2. ロード画面の表示
            if (_loadingScreenCanvasGroup != null)
            {
                _loadingScreenCanvasGroup.gameObject.SetActive(true);
            }
            if (_loadingLogo != null)
            {
                _loadingLogo.gameObject.SetActive(true);
            }
            
            StartLoadingAnimation();

            if (_loadingScreenCanvasGroup != null)
            {
                yield return _loadingScreenCanvasGroup.DOFade(1f, 0.5f).WaitForCompletion();
            }
            else
            {
                yield return new WaitForSeconds(0.5f);
            }

            // 3. 非同期シーンロード
            AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName);
            asyncLoad.allowSceneActivation = false;

            // ロード中の最低待機時間を設ける（演出のため）
            float minimumLoadingTime = 2.0f;
            float timer = 0f;

            while (!asyncLoad.isDone)
            {
                timer += Time.deltaTime;
                // ロードが完了し、最低待機時間も過ぎたら遷移許可
                if (asyncLoad.progress >= 0.9f && timer >= minimumLoadingTime)
                {
                    asyncLoad.allowSceneActivation = true;
                }
                yield return null;
            }

            // 新しいシーンでの開始処理を少し待つ
            yield return new WaitForSeconds(0.5f);

            // 4. ロード画面の非表示
            if (_loadingScreenCanvasGroup != null)
            {
                yield return _loadingScreenCanvasGroup.DOFade(0f, 0.5f).WaitForCompletion();
                _loadingScreenCanvasGroup.gameObject.SetActive(false);
            }
            if (_loadingLogo != null)
            {
                _loadingLogo.gameObject.SetActive(false);
            }
            
            StopLoadingAnimation();

            // 5. フェードイン（モヤが晴れる）
            if (_fadeCanvasGroup != null)
            {
                yield return _fadeCanvasGroup.DOFade(0f, _fadeDuration).WaitForCompletion();
                _fadeCanvasGroup.gameObject.SetActive(false);
            }

            onTransitionComplete?.Invoke();
        }

        private void StartLoadingAnimation()
        {
            if (_loadingText == null) return;
            
            // ピリオドアニメーション開始
            _loadingTextCoroutine = StartCoroutine(LoadingTextRoutine());
            
            // 呼吸（明滅）アニメーション開始
            _loadingText.color = new Color(_loadingText.color.r, _loadingText.color.g, _loadingText.color.b, 1f);
            _loadingTextFadeTween = _loadingText.DOFade(0.3f, 1.2f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine);
        }

        private void StopLoadingAnimation()
        {
            if (_loadingTextCoroutine != null)
            {
                StopCoroutine(_loadingTextCoroutine);
                _loadingTextCoroutine = null;
            }
            if (_loadingTextFadeTween != null)
            {
                _loadingTextFadeTween.Kill();
                _loadingTextFadeTween = null;
                if (_loadingText != null)
                {
                    _loadingText.color = new Color(_loadingText.color.r, _loadingText.color.g, _loadingText.color.b, 1f);
                }
            }
        }

        private IEnumerator LoadingTextRoutine()
        {
            int dotCount = 0;
            while (true)
            {
                string dots = new string('.', dotCount);
                _loadingText.text = $"Loading{dots}";
                dotCount = (dotCount + 1) % 4; // 0, 1, 2, 3 の繰り返し
                yield return new WaitForSeconds(0.5f);
            }
        }
    }
}
