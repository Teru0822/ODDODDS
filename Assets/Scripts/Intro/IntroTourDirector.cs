using System;
using System.Collections;
using System.Collections.Generic;
using MiniGames.Transitions;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace App.Intro
{
    /// <summary>カットのカメラの動き方。</summary>
    public enum IntroTourMotion
    {
        [Tooltip("動かない。寄りのカットに")]
        Static,

        [Tooltip("Orbit Pivot を中心に回りながら、常にそこを見続ける")]
        Orbit,

        [Tooltip("Start Pose から End Pose へ滑らかに移動する。寄り/引きに")]
        PoseToPose
    }

    /// <summary>
    /// 導入ツアー1カット分の定義。カメラの姿勢は「シーンに置いた空オブジェクト」で指定するため、
    /// サブシーン側のオブジェクトを直接参照しない（Multi-Scene のシーン跨ぎ参照は保存時に消えるため）。
    /// </summary>
    [Serializable]
    public class IntroTourShot
    {
        [Tooltip("Inspector 上の見出し。動作には影響しません")]
        public string label = "Shot";

        [Header("カメラ")]
        [Tooltip("カット開始時のカメラ位置・向き。Sceneビューで空オブジェクトを置いて指定します")]
        public Transform startPose;

        [Tooltip("動き方。Static=静止 / Orbit=周回 / PoseToPose=2点間移動")]
        public IntroTourMotion motion = IntroTourMotion.Orbit;

        [Tooltip("Orbit: 周回の中心。カメラはこの位置を見続けます（Start Pose の向きは無視されます）")]
        public Transform orbitPivot;

        [Tooltip("Orbit: このカットで回る角度(度)。正で時計回り、負で反時計回り")]
        public float orbitDegrees = 40f;

        [Tooltip("PoseToPose: 移動先のカメラ位置・向き")]
        public Transform endPose;

        [Tooltip("カメラが動き切るまでの時間(秒)。動き終わってもテロップが残っていればその姿勢で待機します")]
        public float motionDuration = 10f;

        [Header("画角")]
        [Tooltip("カット開始時の Field of View。小さいほど望遠寄りで映画的になります")]
        public float startFov = 45f;

        [Tooltip("カット終了時の Field of View。start と変えるとゆっくり寄る/引く効果になります")]
        public float endFov = 42f;

        [Header("テロップ")]
        [Tooltip("このカットで順に表示するテロップ。スペースキーで送ります。文中の「↓」は改行になります")]
        [TextArea(2, 5)]
        public string[] lines = new string[0];

        [Tooltip("テロップが無い場合に、このカットを表示し続ける時間(秒)")]
        public float durationWithoutTelop = 4f;

        [Header("カットイベント")]
        [Tooltip("このカットが始まった瞬間に呼ばれる。ドアの開閉など、カット固有の演出に使う")]
        public UnityEvent onShotEnter;

        [Tooltip("このカットが終わった瞬間に呼ばれる")]
        public UnityEvent onShotExit;
    }

    /// <summary>
    /// ゲームシーンに入った直後に流す導入ツアー。
    /// 部屋 → UFOキャッチャー → 操作部 → ATM → 操作部 → インターホン → ドア → 取引口 → 悪魔 の順で
    /// カメラを切り替えながらテロップで説明し、最後にモヤ（霧）でフェードアウトしてから
    /// 通常のプレイヤーカメラへ戻し、操作を解禁する。
    ///
    /// 配置: ルートシーン(MainScene)の空オブジェクトに付ける。カメラ姿勢用の空オブジェクトは
    /// このオブジェクトの子として作る（コンテキストメニューの「9カットの雛形を作成」で一括生成可）。
    /// </summary>
    [DisallowMultipleComponent]
    public class IntroTourDirector : MonoBehaviour, IsaveDataProvider
    {
        [Header("カメラ")]
        [Tooltip("ツアー専用カメラ。AudioListener は付けないでください（付いていれば自動で無効化します）")]
        [SerializeField] private Camera _tourCamera;

        [Tooltip("ツアー中のカメラ描画順。プレイヤーカメラより大きい値にすると手前に描画されます")]
        [SerializeField] private float _tourCameraDepth = 100f;

        [Header("テロップ")]
        [SerializeField] private IntroTourTelop _telop;

        [Header("カット一覧")]
        [Tooltip("上から順に再生されます。テキストはここで自由に編集できます")]
        [SerializeField] private List<IntroTourShot> _shots = new List<IntroTourShot>();

        [Header("開始条件")]
        [Tooltip("シーン開始時に自動で再生する。オフにすると StartTour() を呼ぶまで始まりません")]
        [SerializeField] private bool _playOnStart = true;

        [Tooltip("タイトルからのロード演出が終わるのを待つ最大秒数。保険のタイムアウトです")]
        [SerializeField] private float _transitionWaitTimeout = 30f;

        [Tooltip("最初のカットに移ってから再生を始めるまでの待ち時間(秒)。モヤが晴れる間を取ります")]
        [SerializeField] private float _startDelay = 0.5f;

        [Header("UFOキャッチャー演出")]
        [Tooltip("UFOキャッチャーのライトを点灯させるカット番号（0始まり。Shot 02 なら 1）")]
        [SerializeField] private int _ufoSpotlightOnShotIndex = 1;

        [Tooltip("UFOキャッチャーのライトを消灯させるカット番号（0始まり。Shot 03 が終わったら消す場合は 2）")]
        [SerializeField] private int _ufoSpotlightOffShotIndex = 2;

        [Header("取引口")]
        [Tooltip("取引口を開くカットの番号（0始まり。Shot 08 なら 7）")]
        [SerializeField] private int _doorHatchShotIndex = 7;

        [Header("終了時の霧")]
        [Tooltip("モヤでフェードアウト/フェードインする時間(秒)")]
        [SerializeField] private float _fogFadeDuration = 1.5f;

        [Tooltip("SceneTransitionManager が居ない時(MainSceneを直接再生した時など)に使う代替のモヤ")]
        [SerializeField] private CanvasGroup _fallbackFogCanvasGroup;

        [Header("ツアー中の表示制御")]
        [Tooltip("ツアー中はゲーム側の Canvas(HUD等)を隠す。テロップとモヤは対象外です")]
        [SerializeField] private bool _hideOtherCanvasesDuringTour = true;

        [Header("ツアー終了後に有効化する GameObject")]
        [Tooltip("ツアー終了時に SetActive(true) する GO 名のリスト。非アクティブでも検索できます")]
        [SerializeField] private string[] _activateOnTourEnd = { "GameUI" };

        [Header("スキップ")]
        [Tooltip("Escキーでツアー全体を飛ばせるようにする。開発中の確認用")]
        [SerializeField] private bool _allowSkipAll = true;

        [Header("デバッグ")]
        [SerializeField] private bool _logEvents = true;

        private bool _isWatchTour = false;//既に一度このセーブデータでツアーを見ているか否か
        private bool _isLoadComplete = false;//ロードが終わったか否か

        /// <summary>ツアーが終わってプレイヤー操作が解禁された時に呼ばれる。</summary>
        public event Action OnTourFinished;

        /// <summary>ツアー再生中か。</summary>
        public bool IsRunning { get; private set; }

        private Coroutine _sequenceRoutine;
        private Coroutine _motionRoutine;
        private bool _isFinishing;

        private App.Player.FirstPersonController _fpController;
        private bool _playerWasEnabled = true;
        private readonly List<Canvas> _hiddenCanvases = new List<Canvas>();
        private DoorHatchController _doorHatch;

        private void Awake()
        {
            if (_tourCamera == null)
            {
                Debug.LogError("[IntroTourDirector] ツアー用カメラが未設定です。ツアーを再生できません", this);
                enabled = false;
                return;
            }

            if (_playOnStart) App.Input.GameInputGate.Lock();

            // AudioListener が2つあると Unity が警告を出し続けるため、ツアー用カメラ側を無効化する
            var listener = _tourCamera.GetComponent<AudioListener>();
            if (listener != null && listener.enabled)
            {
                listener.enabled = false;
                if (_logEvents) Debug.Log("[IntroTourDirector] ツアー用カメラの AudioListener を無効化しました", this);
            }

            // ロード画面のモヤが晴れた瞬間に1カット目が映っているよう、この時点で構えておく
            _tourCamera.depth = _tourCameraDepth;
            _tourCamera.enabled = true;
            ApplyShotStart(GetShot(0));
        }

        private IEnumerator Start()
        {
            if (!_playOnStart) yield break;

            yield return WaitUntilSceneReady();
            yield return new WaitUntil(() => _isLoadComplete == true);

            if(!_isWatchTour)//一度も見たことが無い時のみツアー開始
                StartTour();
            else
            {
                _isFinishing = true;

                StopMotion();
                if (_telop != null) _telop.HideImmediate();

                // 1. 霧でフェードアウト
                yield return FadeFog(true);

                // 2. モヤに隠れている間にプレイヤーカメラへ戻す
                if (_tourCamera != null) _tourCamera.enabled = false;
                RestoreCanvases();
                UnlockPlayer();

                // 3. 霧が晴れてプレイヤー操作へ
                yield return FadeFog(false);

                // 4. ツアー中は非アクティブにしておいた UI を有効化する
                if (_activateOnTourEnd != null)
                {
                    foreach (var goName in _activateOnTourEnd)
                    {
                        var go = FindInAllScenes(goName);
                        if (go != null) go.SetActive(true);
                        else if (_logEvents) Debug.LogWarning($"[IntroTourDirector] ActivateOnTourEnd: '{goName}' が見つかりません", this);
                    }
                }

                IsRunning = false;
                _isFinishing = false;
                _sequenceRoutine = null;

                if (_logEvents) Debug.Log("[IntroTourDirector] ツアー終了。プレイヤー操作を解禁しました", this);
                OnTourFinished?.Invoke();
            }
        }

        private void Update()
        {
            if (!IsRunning || _isFinishing || !_allowSkipAll) return;

            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                if (_logEvents) Debug.Log("[IntroTourDirector] Escでツアー全体をスキップします", this);
                SkipAll();
            }
        }

        public void WriteSaveData(RoguelikeSaveData data)
        {
            data.isWatchTour = true;
        }
        
        public void ReadSaveData(RoguelikeSaveData data)
        {
            _isWatchTour = data.isWatchTour;
            _isLoadComplete = true;
        }

        /// <summary>ツアーを開始する。_playOnStart をオフにして任意のタイミングで呼ぶこともできる。</summary>
        public void StartTour()
        {
            if (IsRunning) return;
            if (_shots == null || _shots.Count == 0)
            {
                Debug.LogWarning("[IntroTourDirector] カットが1つも登録されていません。ツアーをスキップします", this);
                StartCoroutine(FinishRoutine());
                return;
            }

            // コルーチン開始（1フレーム後）を待たず、同一フレームで即座に隠す
            IsRunning = true;
            _isFinishing = false;
            var hatchGo = GameObject.Find("door_hatch");
            _doorHatch = hatchGo != null ? hatchGo.GetComponent<DoorHatchController>() : null;
            LockPlayer();
            HideGameplayCanvases();

            _sequenceRoutine = StartCoroutine(RunTour());
        }

        /// <summary>残りのカットを全て飛ばして終了処理へ移る。</summary>
        public void SkipAll()
        {
            if (!IsRunning || _isFinishing) return;

            if (_sequenceRoutine != null)
            {
                StopCoroutine(_sequenceRoutine);
                _sequenceRoutine = null;
            }
            StartCoroutine(FinishRoutine());
        }

        /// <summary>
        /// サブシーン(プレイヤーやATM等)のロードと、タイトルからのモヤ演出の完了を待つ。
        /// </summary>
        private IEnumerator WaitUntilSceneReady()
        {
            while (MultiSceneLoader.IsLoadingSubScenes) yield return null;

            // タイトル経由でない(MainSceneを直接再生した)場合は Instance が無いのでそのまま進む
            var transition = SceneTransitionManager.Instance;
            if (transition != null)
            {
                float limit = Time.realtimeSinceStartup + Mathf.Max(0f, _transitionWaitTimeout);
                while (transition.IsTransitioning && Time.realtimeSinceStartup < limit)
                {
                    yield return null;
                }
            }

            if (_startDelay > 0f) yield return new WaitForSeconds(_startDelay);
        }

        private IEnumerator RunTour()
        {
            if (_logEvents) Debug.Log($"[IntroTourDirector] ツアー開始 (全{_shots.Count}カット)", this);
            yield return new WaitUntil(() => GameUIManager.Instance.IsGameUIVisible == false);
            for (int i = 0; i < _shots.Count; i++)
            {
                var shot = _shots[i];
                if (shot == null) continue;

                if (_logEvents) Debug.Log($"[IntroTourDirector] カット{i + 1}: {shot.label}", this);
                yield return PlayShot(shot, i);
            }

            _sequenceRoutine = null;
            yield return FinishRoutine();
        }

        /// <summary>
        /// 1カットを再生する。カメラの動きは並行して走らせ、テロップを送り終えた時点でカットを終える
        /// （テロップが無いカットは durationWithoutTelop だけ表示する）。
        /// </summary>
        private IEnumerator PlayShot(IntroTourShot shot, int shotIndex)
        {
            ApplyShotStart(shot);
            shot.onShotEnter?.Invoke();

            if (shotIndex == _ufoSpotlightOnShotIndex)
                UFOCameraController.Instance?.SetPlaySpotlight(true, playSound: true);

            if (_doorHatch != null && shotIndex == _doorHatchShotIndex)
                _doorHatch.Open();

            StopMotion();
            _motionRoutine = StartCoroutine(AnimateCamera(shot));

            bool hasTelop = shot.lines != null && shot.lines.Length > 0 && _telop != null;
            if (hasTelop)
            {
                yield return _telop.PlayLines(shot.lines);
            }
            else
            {
                yield return new WaitForSeconds(Mathf.Max(0f, shot.durationWithoutTelop));
            }

            StopMotion();
            shot.onShotExit?.Invoke();

            if (shotIndex == _ufoSpotlightOffShotIndex)
                UFOCameraController.Instance?.SetPlaySpotlight(false, playSound: true);

            if (_doorHatch != null && shotIndex == _doorHatchShotIndex)
                _doorHatch.Close();
        }

        /// <summary>カットのカメラを時間に沿って動かす。動き切ったらその姿勢のまま終了する。</summary>
        private IEnumerator AnimateCamera(IntroTourShot shot)
        {
            Transform cam = _tourCamera.transform;

            Vector3 startPos = shot.startPose != null ? shot.startPose.position : cam.position;
            Quaternion startRot = shot.startPose != null ? shot.startPose.rotation : cam.rotation;

            float duration = Mathf.Max(0.01f, shot.motionDuration);
            float elapsed = 0f;

            while (true)
            {
                float u = Mathf.Clamp01(elapsed / duration);
                // 等速のまま始めると回り出しが機械的に見えるため、加減速を付ける
                float eased = Mathf.SmoothStep(0f, 1f, u);

                switch (shot.motion)
                {
                    case IntroTourMotion.Orbit when shot.orbitPivot != null:
                    {
                        Vector3 pivot = shot.orbitPivot.position;
                        Quaternion spin = Quaternion.AngleAxis(shot.orbitDegrees * eased, Vector3.up);
                        cam.position = pivot + spin * (startPos - pivot);

                        Vector3 toPivot = pivot - cam.position;
                        if (toPivot.sqrMagnitude > 0.0001f)
                        {
                            cam.rotation = Quaternion.LookRotation(toPivot, Vector3.up);
                        }
                        break;
                    }

                    case IntroTourMotion.PoseToPose when shot.endPose != null:
                    {
                        cam.position = Vector3.Lerp(startPos, shot.endPose.position, eased);
                        cam.rotation = Quaternion.Slerp(startRot, shot.endPose.rotation, eased);
                        break;
                    }

                    default:
                    {
                        cam.SetPositionAndRotation(startPos, startRot);
                        break;
                    }
                }

                _tourCamera.fieldOfView = Mathf.Lerp(shot.startFov, shot.endFov, eased);

                if (u >= 1f) yield break;

                elapsed += Time.deltaTime;
                yield return null;
            }
        }

        /// <summary>カットの初期姿勢と画角をカメラへ即時反映する。</summary>
        private void ApplyShotStart(IntroTourShot shot)
        {
            if (shot == null || _tourCamera == null) return;

            if (shot.startPose != null)
            {
                _tourCamera.transform.SetPositionAndRotation(shot.startPose.position, shot.startPose.rotation);
            }

            // Orbit は常に中心を向くので、初期姿勢もそこへ合わせておく
            if (shot.motion == IntroTourMotion.Orbit && shot.orbitPivot != null)
            {
                Vector3 toPivot = shot.orbitPivot.position - _tourCamera.transform.position;
                if (toPivot.sqrMagnitude > 0.0001f)
                {
                    _tourCamera.transform.rotation = Quaternion.LookRotation(toPivot, Vector3.up);
                }
            }

            _tourCamera.fieldOfView = shot.startFov;
        }

        private IEnumerator FinishRoutine()
        {
            _isFinishing = true;

            StopMotion();
            if (_telop != null) _telop.HideImmediate();

            // 1. 霧でフェードアウト
            yield return FadeFog(true);

            // 2. モヤに隠れている間にプレイヤーカメラへ戻す
            if (_tourCamera != null) _tourCamera.enabled = false;
            RestoreCanvases();
            UnlockPlayer();

            // 3. 霧が晴れてプレイヤー操作へ
            yield return FadeFog(false);

            // 4. ツアー中は非アクティブにしておいた UI を有効化する
            if (_activateOnTourEnd != null)
            {
                foreach (var goName in _activateOnTourEnd)
                {
                    var go = FindInAllScenes(goName);
                    if (go != null) go.SetActive(true);
                    else if (_logEvents) Debug.LogWarning($"[IntroTourDirector] ActivateOnTourEnd: '{goName}' が見つかりません", this);
                }
            }

            IsRunning = false;
            _isFinishing = false;
            _sequenceRoutine = null;

            if (_logEvents) Debug.Log("[IntroTourDirector] ツアー終了。プレイヤー操作を解禁しました", this);
            OnTourFinished?.Invoke();
        }

        /// <summary>モヤ(霧)の開閉。SceneTransitionManager があればその演出を再利用する。</summary>
        private IEnumerator FadeFog(bool fadeOut)
        {
            var transition = SceneTransitionManager.Instance;
            if (transition != null)
            {
                yield return fadeOut
                    ? transition.FadeOutRoutine(_fogFadeDuration)
                    : transition.FadeInRoutine(_fogFadeDuration);
                yield break;
            }

            if (_fallbackFogCanvasGroup == null) yield break;

            float duration = Mathf.Max(0.01f, _fogFadeDuration);
            float from = fadeOut ? 0f : 1f;
            float to = fadeOut ? 1f : 0f;

            _fallbackFogCanvasGroup.gameObject.SetActive(true);
            _fallbackFogCanvasGroup.alpha = from;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                _fallbackFogCanvasGroup.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }

            _fallbackFogCanvasGroup.alpha = to;
            if (!fadeOut) _fallbackFogCanvasGroup.gameObject.SetActive(false);
        }

        private void StopMotion()
        {
            if (_motionRoutine == null) return;
            StopCoroutine(_motionRoutine);
            _motionRoutine = null;
        }

        /// <summary>
        /// プレイヤー操作を止める。プレイヤーは Scene_Environment 側に居るため、
        /// Inspector 参照ではなくサブシーンのロード完了後に実行時検索する。
        /// </summary>
        private void LockPlayer()
        {
            App.Input.GameInputGate.Lock();

            _fpController = FindAnyObjectByType<App.Player.FirstPersonController>();
            if (_fpController != null)
            {
                _playerWasEnabled = _fpController.enabled;
                _fpController.enabled = false;
            }
            else if (_logEvents)
            {
                Debug.LogWarning("[IntroTourDirector] FirstPersonController が見つかりません。操作ロックをスキップします", this);
            }

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void UnlockPlayer()
        {
            App.Input.GameInputGate.Unlock();

            if (_fpController != null) _fpController.enabled = _playerWasEnabled;

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        /// <summary>非アクティブな GO を含め全ロードシーンを走査して名前で検索する。</summary>
        private static GameObject FindInAllScenes(string goName)
        {
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            {
                var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;
                foreach (var root in scene.GetRootGameObjects())
                {
                    var found = FindInHierarchy(root.transform, goName);
                    if (found != null) return found.gameObject;
                }
            }
            return null;
        }

        private static Transform FindInHierarchy(Transform parent, string targetName)
        {
            if (parent.name == targetName) return parent;
            foreach (Transform child in parent)
            {
                var found = FindInHierarchy(child, targetName);
                if (found != null) return found;
            }
            return null;
        }

        /// <summary>ツアー中だけゲーム側の Canvas を伏せる。テロップとモヤは対象外。</summary>
        private void HideGameplayCanvases()
        {
            if (!_hideOtherCanvasesDuringTour) return;

            _hiddenCanvases.Clear();
            foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (canvas == null || !canvas.enabled) continue;

                // モヤ・ロード画面は DontDestroyOnLoad 側に居るので触らない
                if (canvas.gameObject.scene.name == "DontDestroyOnLoad") continue;

                // 自分の配下とテロップは残す
                if (canvas.transform.IsChildOf(transform)) continue;
                if (_telop != null && canvas.transform.IsChildOf(_telop.transform.root)) continue;

                canvas.enabled = false;
                _hiddenCanvases.Add(canvas);
            }
        }

        private void RestoreCanvases()
        {
            foreach (var canvas in _hiddenCanvases)
            {
                if (canvas != null) canvas.enabled = true;
            }
            _hiddenCanvases.Clear();
        }

        private IntroTourShot GetShot(int index)
        {
            if (_shots == null || index < 0 || index >= _shots.Count) return null;
            return _shots[index];
        }

#if UNITY_EDITOR
        /// <summary>
        /// 仕様どおりの9カットと、その姿勢用の空オブジェクトをまとめて作る。
        /// 生成後は Sceneビューで各 Pose/Pivot をドラッグして構図を決める。
        /// </summary>
        [ContextMenu("9カットの雛形を作成 (姿勢用の空オブジェクトも生成)")]
        private void CreateDefaultShots()
        {
            if (_shots != null && _shots.Count > 0
                && !UnityEditor.EditorUtility.DisplayDialog(
                    "カットの雛形を作成",
                    $"既に {_shots.Count} 件のカットが登録されています。破棄して作り直しますか？",
                    "作り直す", "やめる"))
            {
                return;
            }

            Transform root = transform.Find("TourPoses");
            if (root == null)
            {
                var rootGo = new GameObject("TourPoses");
                UnityEditor.Undo.RegisterCreatedObjectUndo(rootGo, "Create TourPoses");
                rootGo.transform.SetParent(transform, false);
                root = rootGo.transform;
            }

            UnityEditor.Undo.RecordObject(this, "Create Intro Tour Shots");
            _shots = new List<IntroTourShot>
            {
                BuildShot(root, 1, "部屋全体", IntroTourMotion.Orbit, 60f, 16f, 55f, 50f, new[]
                {
                    "ようこそ、我が城へ。↓ここが今日からお前の職場だ。",
                    "借金を返すまで、外には出られん。↓せいぜい稼ぐことだな。"
                }),
                BuildShot(root, 2, "UFOキャッチャー", IntroTourMotion.Orbit, 45f, 12f, 45f, 40f, new[]
                {
                    "まずはこれだ。↓景品を掴み上げて金に換える。"
                }),
                BuildShot(root, 3, "UFOキャッチャーの操作", IntroTourMotion.PoseToPose, 0f, 6f, 40f, 32f, new[]
                {
                    "レバーでアームを狙った位置へ。↓ボタンを押せば掴みにいく。"
                }),
                BuildShot(root, 4, "ATM全体", IntroTourMotion.Orbit, 35f, 10f, 45f, 40f, new[]
                {
                    "手に入れたコインは、この機械で現金に換えろ。"
                }),
                BuildShot(root, 5, "ATMの操作", IntroTourMotion.PoseToPose, 0f, 6f, 40f, 30f, new[]
                {
                    "枚数をテンキーで入力し、決定キーだ。↓札は下の排出口から出てくる。"
                }),
                BuildShot(root, 6, "インターホン", IntroTourMotion.Static, 0f, 5f, 38f, 34f, new[]
                {
                    "客が訪ねてくると、これが鳴る。"
                }),
                BuildShot(root, 7, "ドア", IntroTourMotion.PoseToPose, 0f, 7f, 45f, 38f, new[]
                {
                    "だが扉は開けるな。↓何が立っているか分かったものではない。"
                }),
                BuildShot(root, 8, "ドアの取引口", IntroTourMotion.Static, 0f, 5f, 36f, 30f, new[]
                {
                    "品物のやり取りは、この取引口を使え。"
                }),
                BuildShot(root, 9, "悪魔", IntroTourMotion.Orbit, 25f, 10f, 40f, 36f, new[]
                {
                    "分かったな。↓期日までに耳を揃えて返せ。",
                    "……ああ、それと。↓夜は、あまり物音を立てるなよ。"
                }),
            };

            UnityEditor.EditorUtility.SetDirty(this);
            if (!Application.isPlaying && gameObject.scene.IsValid())
            {
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
            }

            Debug.Log("[IntroTourDirector] 9カットの雛形を作成しました。TourPoses 配下の Pose / Pivot を配置してください", this);
        }

        private IntroTourShot BuildShot(Transform root, int index, string label, IntroTourMotion motion,
            float orbitDegrees, float motionDuration, float startFov, float endFov, string[] lines)
        {
            var shot = new IntroTourShot
            {
                label = $"{index:00}_{label}",
                motion = motion,
                orbitDegrees = orbitDegrees,
                motionDuration = motionDuration,
                startFov = startFov,
                endFov = endFov,
                lines = lines,
                startPose = FindOrCreateChild(root, $"Pose_{index:00}_{label}")
            };

            if (motion == IntroTourMotion.Orbit)
            {
                shot.orbitPivot = FindOrCreateChild(root, $"Pivot_{index:00}_{label}");
            }
            else if (motion == IntroTourMotion.PoseToPose)
            {
                shot.endPose = FindOrCreateChild(root, $"PoseEnd_{index:00}_{label}");
            }

            return shot;
        }

        private static Transform FindOrCreateChild(Transform parent, string childName)
        {
            Transform existing = parent.Find(childName);
            if (existing != null) return existing;

            var go = new GameObject(childName);
            UnityEditor.Undo.RegisterCreatedObjectUndo(go, "Create Tour Pose");
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        /// <summary>選択中に、各カットのカメラ視錐台・位置・注視先をSceneビューで確認できるようにする。</summary>
        private void OnDrawGizmosSelected()
        {
            if (_shots == null) return;

            Matrix4x4 origMatrix = Gizmos.matrix;
            const float frustumRange = 6f;
            const float aspect = 16f / 9f;

            foreach (var shot in _shots)
            {
                if (shot?.startPose == null) continue;

                // startPose の視錐台（シアン半透明）
                Gizmos.color = new Color(0f, 1f, 1f, 0.25f);
                Gizmos.matrix = Matrix4x4.TRS(shot.startPose.position, shot.startPose.rotation, Vector3.one);
                Gizmos.DrawFrustum(Vector3.zero, shot.startFov, frustumRange, 0.1f, aspect);
                Gizmos.matrix = origMatrix;

                // startPose マーカー
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(shot.startPose.position, 0.15f);
                Gizmos.DrawRay(shot.startPose.position, shot.startPose.forward * 0.6f);

                // ラベル
                UnityEditor.Handles.Label(
                    shot.startPose.position + Vector3.up * 0.3f,
                    shot.label ?? "");

                if (shot.motion == IntroTourMotion.Orbit && shot.orbitPivot != null)
                {
                    Gizmos.color = Color.yellow;
                    Gizmos.DrawWireSphere(shot.orbitPivot.position, 0.2f);
                    Gizmos.DrawLine(shot.startPose.position, shot.orbitPivot.position);
                }
                else if (shot.motion == IntroTourMotion.PoseToPose && shot.endPose != null)
                {
                    // endPose の視錐台（緑半透明）
                    Gizmos.color = new Color(0f, 1f, 0f, 0.15f);
                    Gizmos.matrix = Matrix4x4.TRS(shot.endPose.position, shot.endPose.rotation, Vector3.one);
                    Gizmos.DrawFrustum(Vector3.zero, shot.endFov, frustumRange, 0.1f, aspect);
                    Gizmos.matrix = origMatrix;

                    Gizmos.color = Color.green;
                    Gizmos.DrawWireSphere(shot.endPose.position, 0.15f);
                    Gizmos.DrawLine(shot.startPose.position, shot.endPose.position);
                }
            }

            Gizmos.matrix = origMatrix;
        }
#endif
    }
}
