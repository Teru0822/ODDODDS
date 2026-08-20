using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
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
        private Vector3 _originalLogoLocalPosition = Vector3.zero;

        // タイトルシーンのアンビエント設定（ターン遷移ローディング画面のロゴ照明に再利用）
        private bool _hasTitleAmbientCached = false;
        private AmbientMode _titleAmbientMode;
        private Color _titleAmbientLight;
        private float _titleAmbientIntensity;
        private Color _titleAmbientSkyColor;
        private Color _titleAmbientEquatorColor;
        private Color _titleAmbientGroundColor;

        private GameObject _transitionCanvas;//ロード画面として使うCanvasのゲームオブジェクト

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);

                if (_floatingLogoParent != null)
                {
                    _originalLogoScale         = _floatingLogoParent.localScale;
                    _originalLogoLocalPosition = _floatingLogoParent.localPosition;
                }

                InitializeUI();
            }
            else
            {
                //インスタンス化できないオブジェクトが破壊される前に、インスタンス化されている方の初期化を行う
                //目的としては、ゲームシーンからタイトルシーンに遷移する際に、SerializeFieldで設定しているオブジェクトの情報を再設定するため
                var canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include,FindObjectsSortMode.None);
                Canvas deleteCanvas = null;
                foreach (var canvas in canvases)
                {
                    if(canvas.gameObject.name != "TransitionCanvas")
                    {
                        continue;
                    }
                    if (canvas.gameObject.scene.name == "DontDestroyOnLoad")
                    {
                        Instance.ReinitializeSceneDependentRefs(
                            this._lightsToTurnOff,
                            this._lightsToTurnOn);
                        continue;
                    }

                    //名前がTransitionCanvasかつ3D_Title_Sampleシーンにあるオブジェクトを指定
                    deleteCanvas = canvas;
                }

                Destroy(deleteCanvas.gameObject);//同じシーンにTransitionCanvasは2つもいらない
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

        /// <summary>
        /// タイトルシーンへ戻った際に、シーン依存の参照を新しいインスタンスの値で上書きする。
        /// Awake の else ブランチから呼び出す。
        /// </summary>
        public void ReinitializeSceneDependentRefs(Light[] toTurnOff, Light[] toTurnOn)
        {
            _lightsToTurnOff = toTurnOff;
            _lightsToTurnOn  = toTurnOn;
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
        /// シーン遷移やターン遷移の演出が進行中か。
        /// 導入ツアーなど「ロードが明けてから始めたい処理」が完了待ちに使います。
        /// </summary>
        public bool IsTransitioning => _isTransitioning;

        /// <summary>
        /// モヤだけを閉じる（画面が隠れる）。ロード画面は挟みません。
        /// </summary>
        /// <param name="duration">秒数。0以下ならインスペクタの _fadeDuration を使用</param>
        public IEnumerator FadeOutRoutine(float duration = -1f)
        {
            if (_fadeCanvasGroup == null) yield break;

            float d = duration > 0f ? duration : _fadeDuration;
            _fadeCanvasGroup.DOKill();
            _fadeCanvasGroup.gameObject.SetActive(true);
            yield return _fadeCanvasGroup.DOFade(1f, d).WaitForCompletion();
        }

        /// <summary>
        /// モヤだけを晴らす（画面が現れる）。ロード画面は挟みません。
        /// </summary>
        /// <param name="duration">秒数。0以下ならインスペクタの _fadeDuration を使用</param>
        public IEnumerator FadeInRoutine(float duration = -1f)
        {
            if (_fadeCanvasGroup == null) yield break;

            float d = duration > 0f ? duration : _fadeDuration;
            _fadeCanvasGroup.DOKill();
            yield return _fadeCanvasGroup.DOFade(0f, d).WaitForCompletion();
            _fadeCanvasGroup.alpha = 0f;
            _fadeCanvasGroup.gameObject.SetActive(false);
        }

        /// <summary>
        /// 指定したシーンへモヤフェードと専用ロード画面を挟んで非同期遷移します。
        /// </summary>
        public void TransitionToScene(string sceneName, Action onTransitionComplete = null)
        {
            StartCoroutine(TransitionRoutine(sceneName, onTransitionComplete));
        }

        /// <summary>シーン遷移なし。ローディング画面を挟んでターン処理を実行しフェードインで戻る。</summary>
        public void ShowTurnTransition(float minimumDuration, Action onDuringLoading, Action onComplete = null)
        {
            if (_isTransitioning)
            {
                Debug.LogWarning("[SceneTransitionManager] ShowTurnTransition: 既にトランジション中のためスキップしました。_isTransitioning が true のままスタックしている可能性があります。");
                return;
            }
            StartCoroutine(TurnTransitionRoutine(minimumDuration, onDuringLoading, onComplete));
        }

        private IEnumerator TurnTransitionRoutine(float minimumDuration, Action onDuringLoading, Action onComplete)
        {
            _isTransitioning = true;

            if (_loadingCamera != null) _loadingCamera.enabled = true;

            // 1. フェードアウト（黒モヤ）
            if (_fadeCanvasGroup != null)
            {
                _fadeCanvasGroup.gameObject.SetActive(true);
                yield return _fadeCanvasGroup.DOFade(1f, _fadeDuration).WaitForCompletion();
            }

            // タイトルカメラ破棄後にOverlayへフォールバックしているcanvasを
            // ScreenSpaceCameraモードに戻してCamera.mainで3Dロゴを描画できるようにする
            Canvas transitionCanvas = null;
            RenderMode originalRenderMode = RenderMode.ScreenSpaceOverlay;
            if (_fadeCanvasGroup != null && Camera.main != null)
            {
                transitionCanvas = _fadeCanvasGroup.transform.root.GetComponent<Canvas>();
                if (transitionCanvas != null)
                {
                    originalRenderMode = transitionCanvas.renderMode;
                    transitionCanvas.renderMode = RenderMode.ScreenSpaceCamera;
                    transitionCanvas.worldCamera = Camera.main;
                    yield return null; // 1フレーム待ちcanvasをCamera.mainの前に再配置
                }
            }

            // 2. ローディング画面 + アニメーション表示（モヤの上に重ねる）
            if (_loadingScreenCanvasGroup != null)
            {
                _loadingScreenCanvasGroup.alpha = 0f;
                _loadingScreenCanvasGroup.gameObject.SetActive(true);
            }
            bool showLogo = _floatingLogoParent != null;

            // ロゴ照明: タイトルシーンと同じアンビエント＋専用ライトを再利用
            var savedAmbientMode         = RenderSettings.ambientMode;
            var savedAmbientLight        = RenderSettings.ambientLight;
            var savedAmbientIntensity    = RenderSettings.ambientIntensity;
            var savedAmbientSkyColor     = RenderSettings.ambientSkyColor;
            var savedAmbientEquatorColor = RenderSettings.ambientEquatorColor;
            var savedAmbientGroundColor  = RenderSettings.ambientGroundColor;
            if (showLogo)
            {
                if (_hasTitleAmbientCached)
                {
                    RenderSettings.ambientMode         = _titleAmbientMode;
                    RenderSettings.ambientLight        = _titleAmbientLight;
                    RenderSettings.ambientIntensity    = _titleAmbientIntensity;
                    RenderSettings.ambientSkyColor     = _titleAmbientSkyColor;
                    RenderSettings.ambientEquatorColor = _titleAmbientEquatorColor;
                    RenderSettings.ambientGroundColor  = _titleAmbientGroundColor;
                }
                else
                {
                    // デバッグ用（タイトルシーンを経由しない場合）
                    RenderSettings.ambientMode      = AmbientMode.Flat;
                    RenderSettings.ambientLight     = Color.white;
                    RenderSettings.ambientIntensity = 1f;
                }
                if (_lightsToTurnOn != null)
                    foreach (var l in _lightsToTurnOn)
                        if (l != null) { l.gameObject.SetActive(true); l.enabled = true; }
            }

            if (showLogo)
            {
                _floatingLogoParent.localScale = _originalLogoScale;
                _floatingLogoParent.gameObject.SetActive(true);
            }

            StartLoadingAnimation();
            if (_loadingScreenCanvasGroup != null)
                yield return _loadingScreenCanvasGroup.DOFade(1f, 0.5f).WaitForCompletion();

            // 3. ターン処理
            try { onDuringLoading?.Invoke(); }
            catch (Exception e) { Debug.LogError($"[TurnTransition] onDuringLoading 例外: {e}"); }

            // 4. 最低表示時間待機
            yield return new WaitForSeconds(minimumDuration);

            // 5. ローディング画面消去（ロゴ縮小 + フェードアウト並行）
            if (showLogo)
            {
                _floatingLogoParent.DOScale(Vector3.zero, 0.5f).SetEase(Ease.InBack)
                    .OnComplete(() => _floatingLogoParent.gameObject.SetActive(false));
            }
            if (_loadingScreenCanvasGroup != null)
            {
                yield return _loadingScreenCanvasGroup.DOFade(0f, 0.5f).WaitForCompletion();
                _loadingScreenCanvasGroup.gameObject.SetActive(false);
            }
            StopLoadingAnimation();

            // アンビエントライトをフェードイン前に元に戻す（ゲーム画面が見える前）
            if (showLogo)
            {
                if (_lightsToTurnOn != null)
                    foreach (var l in _lightsToTurnOn)
                        if (l != null) l.gameObject.SetActive(false);
                RenderSettings.ambientMode         = savedAmbientMode;
                RenderSettings.ambientLight        = savedAmbientLight;
                RenderSettings.ambientIntensity    = savedAmbientIntensity;
                RenderSettings.ambientSkyColor     = savedAmbientSkyColor;
                RenderSettings.ambientEquatorColor = savedAmbientEquatorColor;
                RenderSettings.ambientGroundColor  = savedAmbientGroundColor;
            }

            // 6. フェードイン（モヤが晴れてゲーム画面が現れる）
            if (_fadeCanvasGroup != null)
            {
                yield return _fadeCanvasGroup.DOFade(0f, _fadeDuration).WaitForCompletion();
                _fadeCanvasGroup.gameObject.SetActive(false);
            }

            if (_loadingCamera != null) _loadingCamera.enabled = false;

            // CanvasをScreenSpaceCameraに切り替えた場合は元のモードに戻す
            if (transitionCanvas != null)
            {
                transitionCanvas.renderMode = originalRenderMode;
                transitionCanvas.worldCamera = null;
            }

            RestoreOtherCanvases();
            _isTransitioning = false;
            onComplete?.Invoke();
        }

        private IEnumerator TransitionRoutine(string sceneName, Action onTransitionComplete)
        {
            _isTransitioning = true;
            AudioListener.pause = true; // ロード中（裏）のBGM等が鳴らないように全体ミュート

            Debug.Log("[STM-DEBUG] === TransitionRoutine 開始 ===");

            if (_loadingCamera != null) _loadingCamera.enabled = true;

            // 1. フェードアウト（暗転・モヤ）
            Debug.Log($"[STM-DEBUG] Step1: _fadeCanvasGroup={(_fadeCanvasGroup != null ? "あり" : "null")}");
            if (_fadeCanvasGroup != null)
            {
                _fadeCanvasGroup.gameObject.SetActive(true);
                yield return _fadeCanvasGroup.DOFade(1f, _fadeDuration).WaitForCompletion();
            }
            Debug.Log("[STM-DEBUG] Step1: フェードアウト完了");


            // --- カメラとTransitionCanvasをシーン遷移から保護する ---
            Debug.Log($"[STM-DEBUG] Step2: Camera.main={Camera.main}");
            if (Camera.main != null)
            {
                _preservedTitleCamera = Camera.main;
                _preservedTitleCamera.transform.SetParent(null);
                DontDestroyOnLoad(_preservedTitleCamera.gameObject);
                
                // MainSceneのカメラに上書きされないよう、描画順を強制的に最前面にする
                _preservedTitleCamera.depth = 1000;

                // TransitionCanvasもシーン遷移で破棄されないように保護する
                if (_fadeCanvasGroup != null)
                {
                    Transform canvasRoot = _fadeCanvasGroup.transform.root;
                    _transitionCanvas = canvasRoot.gameObject;
                    DontDestroyOnLoad(canvasRoot.gameObject);
                    Debug.Log($"[STM-DEBUG] TransitionCanvas '{canvasRoot.name}' をDontDestroyOnLoadに追加");
                }

                // --- ライトの保護と切り替えを先に行う ---
                // カメラをワープさせる前にライトを子にしておかないと、ワープ後にSetParent(true)した場合に
                // ライトが地上の元の世界座標に取り残されてしまう。
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
                            
                            // シーンが破棄されてもライトが消えないように、保存カメラの子にして一緒に連れて行く
                            l.transform.SetParent(_preservedTitleCamera.transform, true);
                            
                            Debug.Log($"[STM-DEBUG] ライトON(ワープ前): {l.name}, pos={l.transform.position}");
                        }
                    }
                }

                // --- カメラワープの復活 ---
                // Mainシーンの建物や影にロゴが干渉して暗くなるのを防ぐため、
                // カメラ（とそれに追従するCanvas・ライト）を地下へ避難させる
                // (5000だと浮動小数点精度の低下で影がチラつくため、-500に設定)
                _hideOffset = new Vector3(0, -500f, 0); // 地下へ移動
                Debug.Log($"[STM-DEBUG] カメラワープ前: cam={_preservedTitleCamera.transform.position}");
                _preservedTitleCamera.transform.position += _hideOffset;
                Debug.Log($"[STM-DEBUG] カメラワープ後: cam={_preservedTitleCamera.transform.position}");
            }

            // （ライト処理はワープ前に移動済み）

            // TransitionCanvasをScreenSpaceCameraに切り替えてロゴを描画できるようにする。
            // LogoObjectはTransitionCanvasの子3Dオブジェクトであり、ScreenSpaceOverlayでは描画されない。
            // TurnTransitionRoutineと同じ方式で、_preservedTitleCamera（DDOLカメラ）を worldCamera に設定する。
            Canvas transitionCanvas = _fadeCanvasGroup != null
                ? _fadeCanvasGroup.transform.root.GetComponent<Canvas>()
                : null;
            RenderMode originalCanvasRenderMode = RenderMode.ScreenSpaceOverlay;
            if (transitionCanvas != null && _preservedTitleCamera != null)
            {
                originalCanvasRenderMode = transitionCanvas.renderMode;
                transitionCanvas.renderMode  = RenderMode.ScreenSpaceCamera;
                transitionCanvas.worldCamera = _preservedTitleCamera;
                yield return null; // 1フレーム待ってCanvasがカメラ前に再配置されるのを待つ
            }

            // 2. ロード画面の表示
            Debug.Log($"[STM-DEBUG] Step3: _loadingScreenCanvasGroup={(_loadingScreenCanvasGroup != null ? "あり" : "null")}, _floatingLogoParent={(_floatingLogoParent != null ? "あり" : "null")}");
            if (_loadingScreenCanvasGroup != null)
            {
                _loadingScreenCanvasGroup.gameObject.SetActive(true);
            }
            if (_floatingLogoParent != null)
            {
                _floatingLogoParent.gameObject.SetActive(true);
                _floatingLogoParent.localScale = _originalLogoScale; // DOScaleで0にされている場合に備えて復元
                Debug.Log($"[STM-DEBUG] ロゴ位置: world={_floatingLogoParent.position}, local={_floatingLogoParent.localPosition}, scale={_floatingLogoParent.localScale}");
                Debug.Log($"[STM-DEBUG] ロゴ親: {(_floatingLogoParent.parent != null ? _floatingLogoParent.parent.name : "null")}");
            }
            
            Debug.Log($"[STM-DEBUG] StartLoadingAnimation呼び出し: _logoSkinnedMeshRenderer={(_logoSkinnedMeshRenderer != null ? "あり" : "null")}, _floatDistance={_floatDistance}");
            StartLoadingAnimation();

            if (_loadingScreenCanvasGroup != null)
            {
                yield return _loadingScreenCanvasGroup.DOFade(1f, 0.5f).WaitForCompletion();
            }
            else
            {
                yield return new WaitForSeconds(0.5f);
            }
            Debug.Log("[STM-DEBUG] Step3: ロード画面フェードイン完了");

            // 3. 非同期シーンロード
            // --- ロード中のロゴ照明を維持するため、現在の環境光設定を保存する ---
            var savedAmbientMode = RenderSettings.ambientMode;
            var savedAmbientLight = RenderSettings.ambientLight;
            var savedAmbientIntensity = RenderSettings.ambientIntensity;
            var savedAmbientSkyColor = RenderSettings.ambientSkyColor;
            var savedAmbientEquatorColor = RenderSettings.ambientEquatorColor;
            var savedAmbientGroundColor = RenderSettings.ambientGroundColor;

            // ターン遷移のロゴ照明に再利用するためキャッシュ
            _titleAmbientMode        = savedAmbientMode;
            _titleAmbientLight       = savedAmbientLight;
            _titleAmbientIntensity   = savedAmbientIntensity;
            _titleAmbientSkyColor    = savedAmbientSkyColor;
            _titleAmbientEquatorColor = savedAmbientEquatorColor;
            _titleAmbientGroundColor = savedAmbientGroundColor;
            _hasTitleAmbientCached   = true;

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

            // --- 遷移先シーン本来の環境光を控えておく ---
            // この時点でアクティブシーンは遷移先なので、RenderSettings は遷移先の値になっている。
            // 直後にタイトルの値で上書きするが、SetActiveScene では戻せない
            // （既にアクティブなシーンを指定しても再適用されない）ため、ここで保持しておく。
            var targetAmbientMode         = RenderSettings.ambientMode;
            var targetAmbientLight        = RenderSettings.ambientLight;
            var targetAmbientIntensity    = RenderSettings.ambientIntensity;
            var targetAmbientSkyColor     = RenderSettings.ambientSkyColor;
            var targetAmbientEquatorColor = RenderSettings.ambientEquatorColor;
            var targetAmbientGroundColor  = RenderSettings.ambientGroundColor;

            // --- Mainシーンの環境光がロゴを暗くするのを防ぐため、タイトルシーンの環境光を一時適用 ---
            RenderSettings.ambientMode = savedAmbientMode;
            RenderSettings.ambientLight = savedAmbientLight;
            RenderSettings.ambientIntensity = savedAmbientIntensity;
            RenderSettings.ambientSkyColor = savedAmbientSkyColor;
            RenderSettings.ambientEquatorColor = savedAmbientEquatorColor;
            RenderSettings.ambientGroundColor = savedAmbientGroundColor;

            // --- 待機処理の復活 ---
            // MultiSceneLoader がサブシーンを読み込んでいる場合は終わるまで待機
            while (MultiSceneLoader.IsLoadingSubScenes)
            {
                yield return null;
            }

            //すべてのサブシーンの読み込みが終わったため、プレイヤー情報を読み込む
            RoguelikeSaveManager.Load();
            try
            {
                if(SettingUIManager.Instance != null) SettingUIManager.Instance.Init();
            }
            catch (System.Exception e)
            {
                Debug.LogError(e);
                Debug.LogError("SettingUIManagerの初期化に失敗しました");
            }
            

            Debug.Log("[SceneTransitionManager] MultiSceneLoaderのロードが完了しました。ItemSpawnerの待機へ移行します。");
            // サブシーンロード完了後、Devilキャッチャーの ItemSpawner 等が Start() を呼ぶための猶予
            yield return new WaitForSeconds(0.5f);

            Debug.Log($"[SceneTransitionManager] ItemSpawner待機チェック。IsSpawning: {ItemSpawner.IsSpawning}");

            // --- 追加: Devilキャッチャー等でのコイン生成待機 ---
            // ItemSpawnerがコインを生成中の場合は、それが終わるまでロード画面の裏で待機する
            if (ItemSpawner.IsSpawning)
            {
                Debug.Log("[SceneTransitionManager] ItemSpawnerがスポーン中のため、完了を待機します。");
                float spawnWaitTimeout = 0f;
                while (ItemSpawner.IsSpawning && spawnWaitTimeout < 60f)
                {
                    yield return null;
                    spawnWaitTimeout += Time.deltaTime;
                }
                if (spawnWaitTimeout >= 60f)
                    Debug.LogWarning("[SceneTransitionManager] ItemSpawner待機がタイムアウトしました。強制続行します。");

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

            // --- Mainシーンのカメラ映像を保持するため、ClearFlagsをDepthに変更 ---
            // SolidColor(黒)のままだと、フェードアウト時にMainシーンの描画が黒で上書きされてしまう
            if (_preservedTitleCamera != null)
            {
                _preservedTitleCamera.clearFlags = CameraClearFlags.Depth;
            }
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

            // --- 遷移先シーン本来の環境光へ戻す ---
            // モヤが晴れた後に戻すと、正しい明るさへ切り替わる瞬間が見えてしまう。
            // 画面がまだ隠れているこのタイミングで適用しておく
            RenderSettings.ambientMode         = targetAmbientMode;
            RenderSettings.ambientLight        = targetAmbientLight;
            RenderSettings.ambientIntensity    = targetAmbientIntensity;
            RenderSettings.ambientSkyColor     = targetAmbientSkyColor;
            RenderSettings.ambientEquatorColor = targetAmbientEquatorColor;
            RenderSettings.ambientGroundColor  = targetAmbientGroundColor;

            // 5. フェードイン（モヤが晴れる）
            // モヤが晴れ始めるタイミングでBGMの再生を解禁する
            AudioListener.pause = false;

            if (_fadeCanvasGroup != null)
            {
                yield return _fadeCanvasGroup.DOFade(0f, _fadeDuration).WaitForCompletion();
                _fadeCanvasGroup.gameObject.SetActive(false);
            }

            // --- ライトの復元 ---
            // ロード用に消していたシーンのライトを元に戻す
            if (_lightsToTurnOff != null)
            {
                foreach (var l in _lightsToTurnOff)
                {
                    if (l != null) l.enabled = true;
                }
            }
            // ロード用に点けていたライトをロゴ親に移して無効化（ターン遷移で再利用するため）
            // _preservedTitleCamera を破棄する前に行わないとライトも一緒に消える
            if (_lightsToTurnOn != null && _floatingLogoParent != null)
            {
                // DOScale で scale=0 になっている状態で SetParent(true) すると座標が壊れるため
                // 再ペアレント前にスケールを戻す
                _floatingLogoParent.localScale = _originalLogoScale;
                foreach (var l in _lightsToTurnOn)
                {
                    if (l != null)
                    {
                        // ワールド座標を維持してロゴ親の子に移す（ロゴとの相対位置が保たれる）
                        l.transform.SetParent(_floatingLogoParent, true);
                        l.gameObject.SetActive(false);
                    }
                }
            }
            else if (_lightsToTurnOn != null)
            {
                foreach (var l in _lightsToTurnOn)
                    if (l != null) l.gameObject.SetActive(false);
            }

            // --- 連れてきたタイトルカメラを破棄し、隠していたUIを元に戻す ---
            if (_preservedTitleCamera != null)
            {
                // ロードが完全に終わってモヤが晴れたら、不要になったタイトルカメラを破棄して
                // MainSceneのカメラに描画を完全に引き継ぐ
                Destroy(_preservedTitleCamera.gameObject);
                _preservedTitleCamera = null;
            }

            // TransitionCanvasをScreenSpaceCamera切り替え前の状態に戻す。
            // OverlayのままだとTurnTransitionRoutineがoriginalRenderModeにOverlayを保存し
            // 正常に動くが、ScreenSpaceCameraのままにするとTurnTransition終了時に
            // worldCamera=nullのScreenSpaceCameraになりロゴが映らなくなるため必ず戻す。
            if (transitionCanvas != null)
            {
                transitionCanvas.renderMode  = originalCanvasRenderMode;
                transitionCanvas.worldCamera = null;
            }

            // アクティブシーンを遷移先に確定させる。
            // 環境光の復元自体はフェードイン前に済ませてある（SetActiveScene は
            // 既にアクティブなシーンを指定しても Lighting を再適用しないため頼れない）
            SceneManager.SetActiveScene(SceneManager.GetSceneByName(sceneName));

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
                
                // TransitionCanvas自体を保護する（_fadeCanvasGroupや_loadingScreenCanvasGroupは
                // TransitionCanvasの「子」であり、Canvas.gameObjectチェックでは「親」のCanvasを保護できない）
                if (_fadeCanvasGroup != null && c.gameObject == _fadeCanvasGroup.transform.root.gameObject) continue;
                
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
                // 前回アニメーションの途中終了によるズレをリセット（SetRelativeの累積を防ぐ）
                _floatingLogoParent.DOKill();
                _floatingLogoParent.localPosition = _originalLogoLocalPosition;

                // ScreenSpace-Camera Canvasの子オブジェクトは、Canvasのスケール（非常に小さい）が適用されるため、
                // _floatDistanceをそのまま使うと動きが極めて微小になる。
                // Canvas座標系で意味のある移動量に変換する（ロゴのlocalScaleで割って正規化）。
                float adjustedFloatDistance = _floatDistance;
                if (_floatingLogoParent.parent != null)
                {
                    float parentScale = _floatingLogoParent.localScale.y;
                    if (parentScale > 1f)
                    {
                        adjustedFloatDistance = _floatDistance * parentScale;
                        Debug.Log($"[STM-DEBUG] 浮遊距離を補正: {_floatDistance} -> {adjustedFloatDistance} (scale={parentScale})");
                    }
                }

                _floatingLogoParent.DOLocalMoveY(adjustedFloatDistance, _floatDuration)
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

        private void OnApplicationQuit()
        {
            if(SceneManager.GetActiveScene().buildIndex != 0)//タイトルシーン以外の時にセーブ処理を行う
                RoguelikeSaveManager.Save();
        }
    }
}
