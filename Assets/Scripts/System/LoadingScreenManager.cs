using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using DG.Tweening;

namespace MiniGames.Transitions
{
    /// <summary>
    /// ローディング画面を Prefab ベースで管理するシングルトン。
    /// Assets/Resources/LoadingScreen.prefab を自動インスタンス化し DontDestroyOnLoad で保持する。
    /// URP Camera Stacking（Overlay）で全シーンに重ねて描画する。
    /// </summary>
    public class LoadingScreenManager : MonoBehaviour
    {
        private static LoadingScreenManager _instance;

        public static LoadingScreenManager Instance
        {
            get
            {
                if (_instance != null) return _instance;
                var prefab = Resources.Load<GameObject>("LoadingScreen");
                if (prefab == null)
                {
                    Debug.LogError("[LoadingScreenManager] Assets/Resources/LoadingScreen.prefab が見つかりません");
                    return null;
                }
                Instantiate(prefab); // Awake() 内で _instance がセットされる
                return _instance;
            }
        }

        [Header("カメラ（URP Overlay）")]
        [SerializeField] private Camera _loadingCamera;

        [Header("Canvas")]
        [SerializeField] private CanvasGroup _fadeCanvasGroup;
        [SerializeField] private CanvasGroup _loadingScreenCanvasGroup;
        [Tooltip("透明 Image。RaycastTarget=true でクリック貫通を防ぐ")]
        [SerializeField] private Image _rootBlocker;

        [Header("ロゴ")]
        [SerializeField] private Transform _floatingLogoParent;
        [SerializeField] private Transform _rotatingLogoPart;
        [SerializeField] private Vector3 _logoRotationAxis = Vector3.forward;
        [SerializeField] private float _logoRotationSpeed = 180f;

        [Header("浮遊アニメーション")]
        [SerializeField] private float _floatDistance = 0.5f;
        [SerializeField] private float _floatDuration = 2.0f;

        [Header("まばたき")]
        [SerializeField] private SkinnedMeshRenderer _logoSkinnedMeshRenderer;
        [SerializeField] private int _blinkBlendShapeIndex = 0;

        [Header("テキスト")]
        [SerializeField] private TMP_Text _loadingText;
        [SerializeField] private TMP_FontAsset _loadingFontAsset;

        [Header("フェード設定")]
        [SerializeField] private float _fadeDuration = 1.5f;

        private Vector3 _originalLogoLocalPosition;
        private Vector3 _originalLogoScale = Vector3.one;

        private Coroutine _loadingTextCoroutine;
        private Tween _loadingTextFadeTween;
        private Coroutine _blinkCoroutine;
        private bool _isShowing;
        private readonly List<Canvas> _hiddenCanvases = new List<Canvas>();

        /// <summary>ローディング画面が表示中か（フェード中も含む）。他システムが入力ガードに使う。</summary>
        public static bool IsShowing => _instance != null && _instance._isShowing;

        // ─── ライフサイクル ───────────────────────────────────────────────

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);

            if (_floatingLogoParent != null)
            {
                _originalLogoScale = _floatingLogoParent.localScale;
                _originalLogoLocalPosition = _floatingLogoParent.localPosition;
            }

            SetupCamera();
            SetupLights();
            ExcludeLoadingLayerFromAllCameras();
            SceneManager.sceneLoaded += OnSceneLoaded;
            SetBlocker(false);
            HideImmediate();
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ExcludeLoadingLayerFromAllCameras();
        }

        private void Update()
        {
            if (_rotatingLogoPart != null && _floatingLogoParent != null && _floatingLogoParent.gameObject.activeSelf)
            {
                _rotatingLogoPart.Rotate(_logoRotationAxis, _logoRotationSpeed * Time.deltaTime, Space.Self);
            }
        }

        // ─── カメラ・レイヤー ─────────────────────────────────────────────

        private void SetupCamera()
        {
            if (_loadingCamera == null) return;
            var pos = _loadingCamera.transform.position;
            _loadingCamera.transform.position = new Vector3(pos.x, -500f, pos.z);
            _loadingCamera.depth = 200; // IntroTourDirector のツアーカメラ(depth=100)より前面
            _loadingCamera.clearFlags = CameraClearFlags.SolidColor;
            _loadingCamera.backgroundColor = Color.black;
            _loadingCamera.enabled = false;
        }

        private void SetupLights()
        {
            int layer = LayerMask.NameToLayer("LoadingScreen");
            if (layer < 0) return;
            int loadingOnlyMask = 1 << layer;
            foreach (var lt in GetComponentsInChildren<Light>(true))
            {
                lt.cullingMask = loadingOnlyMask;
                lt.enabled = false; // カメラが無効な間は消灯しておく
            }
        }

        private void SetLightsEnabled(bool enable)
        {
            foreach (var lt in GetComponentsInChildren<Light>(true))
                lt.enabled = enable;
        }

        private void ExcludeLoadingLayerFromAllCameras()
        {
            int layer = LayerMask.NameToLayer("LoadingScreen");
            if (layer < 0) return;
            int mask = ~(1 << layer);
            foreach (var cam in Camera.allCameras)
            {
                if (cam == _loadingCamera) continue;
                cam.cullingMask &= mask;
            }
        }

        private void SetBlocker(bool active)
        {
            if (_rootBlocker != null) _rootBlocker.raycastTarget = active;
        }

        /// <summary>ゲーム側の Canvas を全て無効化する。Screen Space Overlay も含め上書きを防ぐ。</summary>
        private void HideOtherCanvases()
        {
            _hiddenCanvases.Clear();
            foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (canvas == null || !canvas.enabled) continue;
                // DontDestroyOnLoad 側（LoadingScreen 自身等）は触らない
                if (canvas.gameObject.scene.name == "DontDestroyOnLoad") continue;
                canvas.enabled = false;
                _hiddenCanvases.Add(canvas);
            }
        }

        private void RestoreCanvases()
        {
            foreach (var canvas in _hiddenCanvases)
                if (canvas != null) canvas.enabled = true;
            _hiddenCanvases.Clear();
        }

        private void HideImmediate()
        {
            if (_loadingCamera != null) _loadingCamera.enabled = false;
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
            if (_floatingLogoParent != null)
            {
                _floatingLogoParent.localScale = _originalLogoScale;
                _floatingLogoParent.gameObject.SetActive(false);
            }
            if (_loadingText != null)
            {
                if (_loadingFontAsset != null) _loadingText.font = _loadingFontAsset;
                _loadingText.text = "Loading";
            }
        }

        // ─── Public API ───────────────────────────────────────────────────

        /// <summary>ローディング画面をフェードインして表示する。</summary>
        public Coroutine Show(float fadeDuration = -1f)
        {
            float d = fadeDuration > 0f ? fadeDuration : _fadeDuration;
            return StartCoroutine(ShowRoutine(d));
        }

        /// <summary>ローディング画面をフェードアウトして非表示にする。</summary>
        public Coroutine Hide(float fadeDuration = -1f)
        {
            float d = fadeDuration > 0f ? fadeDuration : _fadeDuration;
            return StartCoroutine(HideRoutine(d));
        }

        /// <summary>ローディング画面を表示してシーンを非同期ロードし、完了後に非表示にする。</summary>
        public Coroutine TransitionToScene(string sceneName, float minimumDuration = 2f, float fadeDuration = -1f, Action onComplete = null)
        {
            float d = fadeDuration > 0f ? fadeDuration : _fadeDuration;
            return StartCoroutine(TransitionToSceneRoutine(sceneName, minimumDuration, d, onComplete));
        }

        /// <summary>
        /// condition が true になるまで（かつ minimumDuration 秒以上経過するまで）表示を続け、
        /// 条件クリア後さらに postCompletionDelay 秒待ってから非表示にする。
        /// </summary>
        public Coroutine ShowUntil(Func<bool> condition, float minimumDuration = 0f, float postCompletionDelay = 0f, float fadeDuration = -1f, Action onComplete = null)
        {
            float d = fadeDuration > 0f ? fadeDuration : _fadeDuration;
            return StartCoroutine(ShowUntilRoutine(condition, minimumDuration, postCompletionDelay, d, onComplete));
        }

        /// <summary>コルーチンが完了するまで（かつ minimumDuration 秒以上）表示を続ける。</summary>
        public Coroutine ShowWhile(IEnumerator task, float minimumDuration = 1f, float postCompletionDelay = 0f, float fadeDuration = -1f, Action onComplete = null)
        {
            float d = fadeDuration > 0f ? fadeDuration : _fadeDuration;
            return StartCoroutine(ShowWhileRoutine(task, minimumDuration, postCompletionDelay, d, onComplete));
        }

        /// <summary>
        /// ロゴ・ローディングテキストを出さず黒オーバーレイだけでフェードアウトする。
        /// ツアー内ショット間の霧演出など、ロード画面を挟まない単純な遮幕に使う。
        /// </summary>
        public Coroutine SimpleFadeOut(float fadeDuration = -1f)
        {
            float d = fadeDuration > 0f ? fadeDuration : _fadeDuration;
            return StartCoroutine(SimpleFadeOutRoutine(d));
        }

        /// <summary>
        /// 黒オーバーレイだけをフェードインして画面を開く。SimpleFadeOut とペアで使う。
        /// </summary>
        public Coroutine SimpleFadeIn(float fadeDuration = -1f)
        {
            float d = fadeDuration > 0f ? fadeDuration : _fadeDuration;
            return StartCoroutine(SimpleFadeInRoutine(d));
        }

        /// <summary>
        /// Action を即時実行し minimumDuration 秒間表示を続ける。
        /// 現行 SceneTransitionManager.ShowTurnTransition の置き換え用。
        /// </summary>
        public Coroutine ShowWhile(Action onDuringLoading, float minimumDuration = 1f, float postCompletionDelay = 0f, float fadeDuration = -1f, Action onComplete = null)
        {
            float d = fadeDuration > 0f ? fadeDuration : _fadeDuration;
            return StartCoroutine(ShowWhileActionRoutine(onDuringLoading, minimumDuration, postCompletionDelay, d, onComplete));
        }

        // ─── 内部ルーティン ───────────────────────────────────────────────

        private IEnumerator ShowRoutine(float fadeDuration)
        {
            _isShowing = true;
            SetBlocker(true);

            // FadeGroup を即時黒にしてからカメラを有効化することで、
            // カメラ背景色が1フレーム見えてしまう黄色フラッシュを防ぐ。
            if (_fadeCanvasGroup != null)
            {
                _fadeCanvasGroup.DOKill();
                _fadeCanvasGroup.alpha = 1f;
                _fadeCanvasGroup.gameObject.SetActive(true);
            }

            if (_loadingCamera != null) _loadingCamera.enabled = true;
            HideOtherCanvases(); // Screen Space Overlay 含む全ゲームCanvasを隠す
            SetLightsEnabled(true);

            if (_loadingScreenCanvasGroup != null)
            {
                _loadingScreenCanvasGroup.DOKill();
                _loadingScreenCanvasGroup.alpha = 0f;
                _loadingScreenCanvasGroup.gameObject.SetActive(true);
            }
            if (_floatingLogoParent != null)
            {
                _floatingLogoParent.localScale = _originalLogoScale;
                _floatingLogoParent.gameObject.SetActive(true);
            }

            StartLoadingAnimation();

            if (_loadingScreenCanvasGroup != null)
                yield return _loadingScreenCanvasGroup.DOFade(1f, 0.5f).WaitForCompletion();
        }

        private IEnumerator HideRoutine(float fadeDuration)
        {
            if (_floatingLogoParent != null)
            {
                _floatingLogoParent.DOKill();
                _floatingLogoParent.DOScale(Vector3.zero, 0.5f).SetEase(Ease.InBack)
                    .OnComplete(() =>
                    {
                        if (_floatingLogoParent != null)
                        {
                            _floatingLogoParent.gameObject.SetActive(false);
                            _floatingLogoParent.localScale = _originalLogoScale;
                        }
                    });
            }

            if (_loadingScreenCanvasGroup != null)
            {
                yield return _loadingScreenCanvasGroup.DOFade(0f, 0.5f).WaitForCompletion();
                _loadingScreenCanvasGroup.gameObject.SetActive(false);
            }

            StopLoadingAnimation();
            SetLightsEnabled(false); // ゲームが透け始める前にロゴライトを消す

            if (_fadeCanvasGroup != null)
            {
                // FadeGroup を透かす際に黒ではなくゲーム映像が見えるよう Depth に切り替える
                if (_loadingCamera != null) _loadingCamera.clearFlags = CameraClearFlags.Depth;
                yield return _fadeCanvasGroup.DOFade(0f, fadeDuration).WaitForCompletion();
                _fadeCanvasGroup.gameObject.SetActive(false);
            }

            if (_loadingCamera != null)
            {
                _loadingCamera.enabled = false;
                _loadingCamera.clearFlags = CameraClearFlags.SolidColor; // 次の Show に備えてリセット
            }
            RestoreCanvases();
            SetBlocker(false);
            _isShowing = false;
        }

        private IEnumerator TransitionToSceneRoutine(string sceneName, float minimumDuration, float fadeDuration, Action onComplete)
        {
            yield return ShowRoutine(fadeDuration);

            var op = SceneManager.LoadSceneAsync(sceneName);
            op.allowSceneActivation = false;
            float timer = 0f;
            while (!op.isDone)
            {
                timer += Time.deltaTime;
                if (op.progress >= 0.9f && timer >= minimumDuration)
                    op.allowSceneActivation = true;
                yield return null;
            }

            yield return new WaitForSeconds(0.5f);
            yield return HideRoutine(fadeDuration);
            onComplete?.Invoke();
        }

        private IEnumerator ShowUntilRoutine(Func<bool> condition, float minimumDuration, float postCompletionDelay, float fadeDuration, Action onComplete)
        {
            yield return ShowRoutine(fadeDuration);

            float timer = 0f;
            while (!condition() || timer < minimumDuration)
            {
                timer += Time.deltaTime;
                yield return null;
            }

            if (postCompletionDelay > 0f)
                yield return new WaitForSeconds(postCompletionDelay);

            yield return HideRoutine(fadeDuration);
            onComplete?.Invoke();
        }

        private IEnumerator ShowWhileRoutine(IEnumerator task, float minimumDuration, float postCompletionDelay, float fadeDuration, Action onComplete)
        {
            yield return ShowRoutine(fadeDuration);

            bool taskDone = false;
            StartCoroutine(RunAndSignal(task, () => taskDone = true));

            float timer = 0f;
            while (!taskDone || timer < minimumDuration)
            {
                timer += Time.deltaTime;
                yield return null;
            }

            if (postCompletionDelay > 0f)
                yield return new WaitForSeconds(postCompletionDelay);

            yield return HideRoutine(fadeDuration);
            onComplete?.Invoke();
        }

        private IEnumerator RunAndSignal(IEnumerator task, Action signal)
        {
            yield return task;
            signal?.Invoke();
        }

        private IEnumerator ShowWhileActionRoutine(Action onDuringLoading, float minimumDuration, float postCompletionDelay, float fadeDuration, Action onComplete)
        {
            yield return ShowRoutine(fadeDuration);

            try { onDuringLoading?.Invoke(); }
            catch (Exception e) { Debug.LogError($"[LoadingScreenManager] ShowWhile Action 例外: {e}"); }

            yield return new WaitForSeconds(minimumDuration);

            if (postCompletionDelay > 0f)
                yield return new WaitForSeconds(postCompletionDelay);

            yield return HideRoutine(fadeDuration);
            onComplete?.Invoke();
        }

        private IEnumerator SimpleFadeOutRoutine(float fadeDuration)
        {
            SetBlocker(true);
            SetLightsEnabled(false); // 霧フェード中はロゴライトを消したまま

            // ゲームが透けて見える状態からフェードアウトするため Depth を使う
            if (_loadingCamera != null)
            {
                _loadingCamera.clearFlags = CameraClearFlags.Depth;
                _loadingCamera.enabled = true;
            }
            if (_fadeCanvasGroup != null)
            {
                _fadeCanvasGroup.DOKill();
                _fadeCanvasGroup.alpha = 0f;
                _fadeCanvasGroup.gameObject.SetActive(true);
                yield return _fadeCanvasGroup.DOFade(1f, fadeDuration).WaitForCompletion();
            }
        }

        private IEnumerator SimpleFadeInRoutine(float fadeDuration)
        {
            // clearFlags を Depth に切り替えてゲームが透けて見えるようにしてから1フレーム待ち、
            // ゲームカメラが色バッファを描画した後でフェードを開始することで黒フレームを防ぐ
            if (_loadingCamera != null) _loadingCamera.clearFlags = CameraClearFlags.Depth;
            yield return null;

            if (_fadeCanvasGroup != null)
            {
                _fadeCanvasGroup.DOKill();
                yield return _fadeCanvasGroup.DOFade(0f, fadeDuration).WaitForCompletion();
                _fadeCanvasGroup.gameObject.SetActive(false);
            }
            if (_loadingCamera != null)
            {
                _loadingCamera.enabled = false;
                _loadingCamera.clearFlags = CameraClearFlags.SolidColor;
            }
            SetBlocker(false);
        }

        // ─── アニメーション ───────────────────────────────────────────────

        private void StartLoadingAnimation()
        {
            if (_loadingText != null)
            {
                _loadingTextCoroutine = StartCoroutine(LoadingTextRoutine());
                _loadingText.color = new Color(_loadingText.color.r, _loadingText.color.g, _loadingText.color.b, 1f);
                _loadingTextFadeTween = _loadingText.DOFade(0.3f, 1.2f)
                    .SetLoops(-1, LoopType.Yoyo)
                    .SetEase(Ease.InOutSine);
            }

            if (_floatingLogoParent != null)
            {
                _floatingLogoParent.DOKill();
                _floatingLogoParent.localPosition = _originalLogoLocalPosition;

                _floatingLogoParent.DOLocalMoveY(_floatDistance, _floatDuration)
                    .SetRelative(true)
                    .SetLoops(-1, LoopType.Yoyo)
                    .SetEase(Ease.InOutSine);
            }

            if (_logoSkinnedMeshRenderer != null)
                _blinkCoroutine = StartCoroutine(BlinkRoutine());
        }

        private void StopLoadingAnimation()
        {
            if (_floatingLogoParent != null)
                _floatingLogoParent.DOKill();

            if (_blinkCoroutine != null)
            {
                StopCoroutine(_blinkCoroutine);
                _blinkCoroutine = null;
            }
            if (_logoSkinnedMeshRenderer != null)
                _logoSkinnedMeshRenderer.SetBlendShapeWeight(_blinkBlendShapeIndex, 0f);

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
                    _loadingText.color = new Color(_loadingText.color.r, _loadingText.color.g, _loadingText.color.b, 1f);
            }
        }

        private IEnumerator LoadingTextRoutine()
        {
            int dotCount = 0;
            while (true)
            {
                if (_loadingText != null)
                    _loadingText.text = $"Loading{new string('.', dotCount)}";
                dotCount = (dotCount + 1) % 4;
                yield return new WaitForSeconds(0.5f);
            }
        }

        private IEnumerator BlinkRoutine()
        {
            yield return new WaitForSeconds(0.5f);

            while (true)
            {
                if (_logoSkinnedMeshRenderer == null) break;

                const float blinkDuration = 0.05f;

                // 閉じる
                float t = 0f;
                while (t < blinkDuration)
                {
                    t += Time.deltaTime;
                    if (_logoSkinnedMeshRenderer != null)
                        _logoSkinnedMeshRenderer.SetBlendShapeWeight(_blinkBlendShapeIndex, Mathf.Lerp(0f, 100f, t / blinkDuration));
                    yield return null;
                }
                if (_logoSkinnedMeshRenderer != null)
                    _logoSkinnedMeshRenderer.SetBlendShapeWeight(_blinkBlendShapeIndex, 100f);

                yield return new WaitForSeconds(0.02f);

                // 開ける
                t = 0f;
                while (t < blinkDuration)
                {
                    t += Time.deltaTime;
                    if (_logoSkinnedMeshRenderer != null)
                        _logoSkinnedMeshRenderer.SetBlendShapeWeight(_blinkBlendShapeIndex, Mathf.Lerp(100f, 0f, t / blinkDuration));
                    yield return null;
                }
                if (_logoSkinnedMeshRenderer != null)
                    _logoSkinnedMeshRenderer.SetBlendShapeWeight(_blinkBlendShapeIndex, 0f);

                yield return new WaitForSeconds(UnityEngine.Random.Range(1.0f, 2.5f));
            }
        }
    }
}
