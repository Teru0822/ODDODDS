using System;
using System.Collections;
using System.Collections.Generic;
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
        [SerializeField] private Transform _floatingLogoParent;
        [Tooltip("回転させるロゴの特定パーツ")]
        [SerializeField] private Transform _rotatingLogoPart;
        [Tooltip("ロゴの回転軸（3Dなら Vector3.up (0,1,0) など）")]
        [SerializeField] private Vector3 _logoRotationAxis = Vector3.forward;
        [Tooltip("ロゴの回転速度 (度/秒)")]
        [SerializeField] private float _logoRotationSpeed = 180f;

        [Header("浮遊アニメーション設定")]
        [SerializeField] private float _floatDistance = 0.5f;
        [SerializeField] private float _floatDuration = 2.0f;

        [Header("ロード中ライト制御")]
        [SerializeField] private Light[] _lightsToTurnOff;
        [SerializeField] private Light[] _lightsToTurnOn;

        [Header("ロード中ロゴまばたき設定")]
        [SerializeField] private SkinnedMeshRenderer _logoSkinnedMeshRenderer;
        [Tooltip("目の開閉を制御するBlendShapeのインデックス")]
        [SerializeField] private int _blinkBlendShapeIndex = 0;

        [Tooltip("フォント反映用のLoadingテキスト")]
        [SerializeField] private TMP_Text _loadingText;
        [Tooltip("外部指定されたフォントアセット（Owrekynge等）")]
        [SerializeField] private TMP_FontAsset _loadingFontAsset;

        [Header("フェード設定")]
        [SerializeField] private float _fadeDuration = 1.5f;

        [Header("特殊シーン連携")]
        [Tooltip("コイン等が生成・落下し終わるまで追加で待機する時間（秒）")]
        [SerializeField] private float _postSpawnWaitTime = 2.5f;

        [Header("ローディング専用カメラ")]
        [Tooltip("ロード中だけオンにする専用のカメラ（Unity上で作成してアタッチしてください）")]
        [SerializeField] private Camera _loadingCamera;

        private Coroutine _loadingTextCoroutine;
        private Tween _loadingTextFadeTween;
        private Coroutine _blinkCoroutine;

        // --- 追加: タイトルシーンのカメラを一緒に連れて行く用 ---
        private Camera _preservedTitleCamera;

        // 他のシーンのUIを一時的に隠すためのリスト
        private List<Canvas> _hiddenCanvases = new List<Canvas>();
        
        // 隔離した距離を覚えておく（-10000だと浮動小数点精度の低下で影がチラつくため、-500に変更）
        private Vector3 _hideOffset = new Vector3(0, -500f, 0);

        private bool _isTransitioning = false;
        private Vector3 _originalLogoScale = Vector3.one;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);

                if (_floatingLogoParent != null)
                {
                    _originalLogoScale = _floatingLogoParent.localScale;
                }

                InitializeUI();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // (LateUpdateで毎フレーム監視するため、ここは不要になりました)
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

            if (_floatingLogoParent != null)
            {
                _floatingLogoParent.localScale = _originalLogoScale;
                _floatingLogoParent.gameObject.SetActive(false);
            }

            if (_lightsToTurnOn != null)
            {
                foreach (var l in _lightsToTurnOn)
                {
                    if (l != null) l.gameObject.SetActive(false);
                }
            }

            if (_loadingText != null && _loadingFontAsset != null)
            {
                _loadingText.font = _loadingFontAsset;
                _loadingText.text = "Loading";
            }
        }

        private void Update()
        {
            if (_rotatingLogoPart != null && _floatingLogoParent != null && _floatingLogoParent.gameObject.activeSelf)
            {
                _rotatingLogoPart.Rotate(_logoRotationAxis, _logoRotationSpeed * Time.deltaTime, Space.Self);
            }
        }

        private void LateUpdate()
        {
            // トランジション中は、他スクリプトが生成したUI（RoundTextなど）が
            // 画面に描画される直前（LateUpdate）で強制的に隠蔽し続ける
            if (_isTransitioning)
            {
                HideOtherCanvases();
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
            _isTransitioning = true;
            AudioListener.pause = true; // ロード中（裏）のBGM等が鳴らないように全体ミュート

            // --- 追加: 専用カメラをオンにする ---
            if (_loadingCamera != null) _loadingCamera.enabled = true;

            // 1. フェードアウト（暗転・モヤ）
            if (_fadeCanvasGroup != null)
            {
                _fadeCanvasGroup.gameObject.SetActive(true);
                yield return _fadeCanvasGroup.DOFade(1f, _fadeDuration).WaitForCompletion();
            }

            // --- 追加: 画面が完全にモヤで隠れた後に、カメラを保護して地下へワープさせる ---
            if (Camera.main != null)
            {
                _preservedTitleCamera = Camera.main;
                _preservedTitleCamera.transform.SetParent(null);
                DontDestroyOnLoad(_preservedTitleCamera.gameObject);
                
                // MainSceneのカメラに上書きされないよう、描画順を強制的に最前面にする
                _preservedTitleCamera.depth = 1000;

                // 他シーンの光を避けるため、ロード画面一式を遥か地下にワープさせる
                _preservedTitleCamera.transform.position += _hideOffset;
                transform.position += _hideOffset;
            }

            // ライトの切り替え
            if (_lightsToTurnOff != null)
            {
                foreach (var l in _lightsToTurnOff) if (l != null) l.enabled = false;
            }
            if (_lightsToTurnOn != null)
            {
                foreach (var l in _lightsToTurnOn) 
                {
                    if (l != null) 
                    {
                        l.gameObject.SetActive(true);
                        l.enabled = true;
                    }
                }
            }

            // 2. ロード画面の表示
            if (_loadingScreenCanvasGroup != null)
            {
                _loadingScreenCanvasGroup.gameObject.SetActive(true);
            }
            if (_floatingLogoParent != null)
            {
                _floatingLogoParent.gameObject.SetActive(true);
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

            // 新しいシーンでの開始処理を少し待つ（AwakeやStartが呼ばれる猶予）
            yield return new WaitForSeconds(0.5f);

            // --- 待機処理の復活 ---
            // MultiSceneLoader がサブシーンを読み込んでいる場合は終わるまで待機
            while (MultiSceneLoader.IsLoadingSubScenes)
            {
                yield return null;
            }

            Debug.Log("[SceneTransitionManager] MultiSceneLoaderのロードが完了しました。ItemSpawnerの待機へ移行します。");

            // サブシーンロード完了後、UFOキャッチャーの ItemSpawner 等が Start() を呼ぶための猶予
            yield return new WaitForSeconds(0.5f);

            Debug.Log($"[SceneTransitionManager] ItemSpawner待機チェック。IsSpawning: {ItemSpawner.IsSpawning}");

            // --- 追加: UFOキャッチャー等でのコイン生成待機 ---
            // ItemSpawnerがコインを生成中の場合は、それが終わるまでロード画面の裏で待機する
            if (ItemSpawner.IsSpawning)
            {
                Debug.Log("[SceneTransitionManager] ItemSpawnerがスポーン中のため、完了を待機します。");
                while (ItemSpawner.IsSpawning)
                {
                    yield return null;
                }
                
                Debug.Log($"[SceneTransitionManager] スポーンが完了しました。追加待機({_postSpawnWaitTime}秒)を開始します。");
                // コインがすべて生成された後、床に落ちて物理演算が落ち着くまでの追加待機
                if (_postSpawnWaitTime > 0f)
                {
                    yield return new WaitForSeconds(_postSpawnWaitTime);
                }
            }
            else
            {
                Debug.Log("[SceneTransitionManager] ItemSpawnerは動作していませんでした。追加待機をスキップします。");
            }

            Debug.Log("[SceneTransitionManager] 全ての待機処理が完了。ロード画面を消去しフェードインを開始します。");
            // 4. ロード画面の非表示
            if (_floatingLogoParent != null)
            {
                // ロゴをシュッと縮小させて消す（キャンバスのフェードアウトと並行して実行）
                _floatingLogoParent.DOScale(Vector3.zero, 0.5f).SetEase(Ease.InBack).OnComplete(() => 
                {
                    _floatingLogoParent.gameObject.SetActive(false);
                });
            }

            if (_loadingScreenCanvasGroup != null)
            {
                yield return _loadingScreenCanvasGroup.DOFade(0f, 0.5f).WaitForCompletion();
                _loadingScreenCanvasGroup.gameObject.SetActive(false);
            }
            else
            {
                yield return new WaitForSeconds(0.5f);
            }
            
            StopLoadingAnimation();

            // 5. フェードイン（モヤが晴れる）
            // モヤが晴れ始めるタイミングでBGMの再生を解禁する
            AudioListener.pause = false;

            if (_fadeCanvasGroup != null)
            {
                yield return _fadeCanvasGroup.DOFade(0f, _fadeDuration).WaitForCompletion();
                _fadeCanvasGroup.gameObject.SetActive(false);
            }

            // --- 追加: 連れてきたタイトルカメラを破棄し、隠していたUIを元に戻す ---
            if (_preservedTitleCamera != null)
            {
                // ロードが完全に終わってモヤが晴れたら、不要になったタイトルカメラを破棄して
                // MainSceneのカメラに描画を完全に引き継ぐ
                Destroy(_preservedTitleCamera.gameObject);
                _preservedTitleCamera = null;

                // 管理マネージャー自身の位置も元の正常な位置に戻しておく
                transform.position -= _hideOffset;
            }

            RestoreOtherCanvases();
            _isTransitioning = false;

            onTransitionComplete?.Invoke();
        }

        private void HideOtherCanvases()
        {
            Canvas[] allCanvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            foreach (var c in allCanvases)
            {
                // 自分が管理しているキャンバス以外で、現在表示されているものを隠す
                if (_fadeCanvasGroup != null && c.gameObject == _fadeCanvasGroup.gameObject) continue;
                if (_loadingScreenCanvasGroup != null && c.gameObject == _loadingScreenCanvasGroup.gameObject) continue;
                
                // 親階層も含めてチェック（自分の子キャンバスを誤爆しないため）
                if (c.transform.IsChildOf(this.transform)) continue;

                if (c.enabled)
                {
                    c.enabled = false;
                    if (!_hiddenCanvases.Contains(c))
                    {
                        _hiddenCanvases.Add(c);
                    }
                }
            }
        }

        private void RestoreOtherCanvases()
        {
            foreach (var c in _hiddenCanvases)
            {
                if (c != null) c.enabled = true;
            }
            _hiddenCanvases.Clear();
        }

        private void StartLoadingAnimation()
        {
            if (_loadingText != null)
            {
                // ピリオドアニメーション開始
                _loadingTextCoroutine = StartCoroutine(LoadingTextRoutine());
                
                // 呼吸（明滅）アニメーション開始
                _loadingText.color = new Color(_loadingText.color.r, _loadingText.color.g, _loadingText.color.b, 1f);
                _loadingTextFadeTween = _loadingText.DOFade(0.3f, 1.2f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine);
            }

            if (_floatingLogoParent != null)
            {
                // 現在の位置から相対的に _floatDistance 分上に移動し、戻るのを繰り返す
                _floatingLogoParent.DOLocalMoveY(_floatDistance, _floatDuration)
                    .SetRelative(true)
                    .SetLoops(-1, LoopType.Yoyo)
                    .SetEase(Ease.InOutSine);
            }

            if (_logoSkinnedMeshRenderer != null)
            {
                _blinkCoroutine = StartCoroutine(BlinkRoutine());
            }
        }

        private void StopLoadingAnimation()
        {
            if (_floatingLogoParent != null)
            {
                _floatingLogoParent.DOKill();
            }

            if (_blinkCoroutine != null)
            {
                StopCoroutine(_blinkCoroutine);
                _blinkCoroutine = null;
            }
            if (_logoSkinnedMeshRenderer != null)
            {
                _logoSkinnedMeshRenderer.SetBlendShapeWeight(_blinkBlendShapeIndex, 0f);
            }

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

        private IEnumerator BlinkRoutine()
        {
            // ロード開始直後にまず一度瞬きさせる
            yield return new WaitForSeconds(0.5f);

            while (true)
            {
                if (_logoSkinnedMeshRenderer == null) break;

                // 閉じる (0 -> 100)
                float t = 0f;
                float blinkDuration = 0.05f;
                while (t < blinkDuration)
                {
                    t += Time.deltaTime;
                    float weight = Mathf.Lerp(0f, 100f, t / blinkDuration);
                    if (_logoSkinnedMeshRenderer != null) _logoSkinnedMeshRenderer.SetBlendShapeWeight(_blinkBlendShapeIndex, weight);
                    yield return null;
                }
                if (_logoSkinnedMeshRenderer != null) _logoSkinnedMeshRenderer.SetBlendShapeWeight(_blinkBlendShapeIndex, 100f);

                // 少しだけ閉じた状態を維持
                yield return new WaitForSeconds(0.02f);

                // 開ける (100 -> 0)
                t = 0f;
                while (t < blinkDuration)
                {
                    t += Time.deltaTime;
                    float weight = Mathf.Lerp(100f, 0f, t / blinkDuration);
                    if (_logoSkinnedMeshRenderer != null) _logoSkinnedMeshRenderer.SetBlendShapeWeight(_blinkBlendShapeIndex, weight);
                    yield return null;
                }
                if (_logoSkinnedMeshRenderer != null) _logoSkinnedMeshRenderer.SetBlendShapeWeight(_blinkBlendShapeIndex, 0f);

                // 次の瞬きまでの待機（1〜2.5秒）
                float waitTime = UnityEngine.Random.Range(1.0f, 2.5f);
                yield return new WaitForSeconds(waitTime);
            }
        }
    }
}
