using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MiniGames.Transitions
{
    /// <summary>
    /// シーン遷移・ターン遷移の司令塔。
    /// ローディング画面の描画は LoadingScreenManager に委譲する。
    /// ゲーム固有の待機処理（MultiSceneLoader, ItemSpawner, セーブ）はここに残す。
    /// </summary>
    public class SceneTransitionManager : MonoBehaviour
    {
        public static SceneTransitionManager Instance { get; private set; }

        [Header("特殊シーン連携")]
        [Tooltip("コイン等が生成・落下し終わるまで追加で待機する時間（秒）")]
        [SerializeField] private float _postSpawnWaitTime = 2.5f;

        private bool _isTransitioning = false;

        /// <summary>
        /// シーン遷移やターン遷移の演出が進行中か。
        /// 導入ツアーなど「ロードが明けてから始めたい処理」が完了待ちに使います。
        /// </summary>
        public bool IsTransitioning => _isTransitioning;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
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

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode) { }

        // ─── 公開 API ─────────────────────────────────────────────────────

        /// <summary>
        /// 指定したシーンへロード画面を挟んで非同期遷移します。
        /// </summary>
        public void TransitionToScene(string sceneName, Action onTransitionComplete = null)
        {
            StartCoroutine(TransitionRoutine(sceneName, onTransitionComplete));
        }

        /// <summary>
        /// シーン遷移なし。ローディング画面を挟んでターン処理を実行しフェードインで戻る。
        /// </summary>
        public void ShowTurnTransition(float minimumDuration, Action onDuringLoading, Action onComplete = null)
        {
            if (_isTransitioning)
            {
                Debug.LogWarning("[SceneTransitionManager] ShowTurnTransition: 既にトランジション中のためスキップしました。");
                return;
            }
            StartCoroutine(TurnTransitionRoutine(minimumDuration, onDuringLoading, onComplete));
        }

        /// <summary>
        /// モヤだけを閉じる（画面が隠れる）。ロード画面は挟みません。
        /// ロゴやローディングテキストは出さず黒フェードのみ。
        /// </summary>
        public IEnumerator FadeOutRoutine(float duration = -1f)
        {
            var lsm = LoadingScreenManager.Instance;
            if (lsm == null) yield break;
            yield return lsm.SimpleFadeOut(duration);
        }

        /// <summary>
        /// モヤだけを晴らす（画面が現れる）。ロード画面は挟みません。
        /// ロゴやローディングテキストは出さず黒フェードのみ。
        /// </summary>
        public IEnumerator FadeInRoutine(float duration = -1f)
        {
            var lsm = LoadingScreenManager.Instance;
            if (lsm == null) yield break;
            yield return lsm.SimpleFadeIn(duration);
        }

        // ─── 内部ルーティン ───────────────────────────────────────────────

        private IEnumerator TransitionRoutine(string sceneName, Action onTransitionComplete)
        {
            _isTransitioning = true;
            AudioListener.pause = true;

            var lsm = LoadingScreenManager.Instance;
            if (lsm != null) yield return lsm.Show();

            // 非同期シーンロード（最低 2 秒待機）
            var asyncLoad = SceneManager.LoadSceneAsync(sceneName);
            asyncLoad.allowSceneActivation = false;
            float timer = 0f;
            while (!asyncLoad.isDone)
            {
                timer += Time.deltaTime;
                if (asyncLoad.progress >= 0.9f && timer >= 2.0f)
                    asyncLoad.allowSceneActivation = true;
                yield return null;
            }

            // 遷移先の Awake / Start が呼ばれる猶予
            yield return new WaitForSeconds(0.5f);

            // ゲーム固有: サブシーンのロード完了を待機
            while (MultiSceneLoader.IsLoadingSubScenes)
                yield return null;

            // プレイヤーデータのロード
            RoguelikeSaveManager.Load();
            try
            {
                if (SettingUIManager.Instance != null) SettingUIManager.Instance.Init();
            }
            catch (Exception e)
            {
                Debug.LogError($"[SceneTransitionManager] SettingUIManager 初期化失敗: {e}");
            }

            // サブシーンロード完了後、ItemSpawner が Start() を呼ぶ猶予
            yield return new WaitForSeconds(0.5f);

            // ゲーム固有: Devilキャッチャー等でのコイン生成待機
            if (ItemSpawner.IsSpawning)
            {
                float timeout = 0f;
                while (ItemSpawner.IsSpawning && timeout < 60f)
                {
                    yield return null;
                    timeout += Time.deltaTime;
                }
                if (timeout >= 60f)
                    Debug.LogWarning("[SceneTransitionManager] ItemSpawner 待機がタイムアウトしました。強制続行します。");

                if (_postSpawnWaitTime > 0f)
                    yield return new WaitForSeconds(_postSpawnWaitTime);
            }

            if (lsm != null) yield return lsm.Hide();
            AudioListener.pause = false;

            SceneManager.SetActiveScene(SceneManager.GetSceneByName(sceneName));

            _isTransitioning = false;
            onTransitionComplete?.Invoke();
        }

        private IEnumerator TurnTransitionRoutine(float minimumDuration, Action onDuringLoading, Action onComplete)
        {
            _isTransitioning = true;

            var lsm = LoadingScreenManager.Instance;
            if (lsm != null)
            {
                yield return lsm.ShowWhile(onDuringLoading, minimumDuration);
            }
            else
            {
                try { onDuringLoading?.Invoke(); }
                catch (Exception e) { Debug.LogError($"[SceneTransitionManager] TurnTransition 例外: {e}"); }
                yield return new WaitForSeconds(minimumDuration);
            }

            _isTransitioning = false;
            onComplete?.Invoke();
        }

        private void OnApplicationQuit()
        {
            if (SceneManager.GetActiveScene().buildIndex != 0)
                RoguelikeSaveManager.Save();
        }
    }
}
