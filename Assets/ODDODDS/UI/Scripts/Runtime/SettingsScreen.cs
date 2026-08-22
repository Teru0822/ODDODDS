using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace OddOdds.UI.Settings
{
    /// <summary>
    /// 設定画面のルート。Escape での開閉と、タブによるページ切り替えだけを持つ。
    ///
    /// 各設定項目の中身（解像度・音量など）はここでは扱わない。
    /// 値の読み書きは既存の SettingUIManager 側か、ページごとの専用スクリプトに任せ、
    /// この枠組みは「見た目」と「開閉」に責務を絞っている。
    /// </summary>
    [DisallowMultipleComponent]
    public class SettingsScreen : MonoBehaviour
    {
        /// <summary>タブとページの対応 1 組。Inspector で増減できる。</summary>
        [Serializable]
        public class Page
        {
            [Tooltip("このページを開くタブ")]
            public SettingsTabButton tab;

            [Tooltip("表示/非表示を切り替える中身")]
            public GameObject panel;

            [Tooltip("Inspector 上の見分け用。動作には影響しない")]
            public string note;
        }

        // ------------------------------------------------------------------
        [Header("テーマ")]
        [Tooltip("色・サイズ・フォントの定義。変更後は右クリック →「テーマを適用」")]
        [SerializeField] private SettingsTheme _theme;

        [Header("構成")]
        [Tooltip("Escape で開閉する対象。Canvas 直下のルートを入れる")]
        [SerializeField] private GameObject _root;

        [Tooltip("タブとページの組。上から順に並ぶ")]
        [SerializeField] private List<Page> _pages = new List<Page>();

        [Tooltip("起動時に開いておくページの番号")]
        [SerializeField] private int _defaultPageIndex;

        [Header("閉じる操作")]
        [SerializeField] private Button _closeButton;

        // ------------------------------------------------------------------
        [Header("入力")]
        [Tooltip("設定画面を開閉するアクション。InputSystem_Actions の OpenSetting を割り当てる")]
        [SerializeField] private InputActionReference _openAction;

        [Tooltip("オフにすると Escape を見ない。既存の SettingUIManager と併用して検証したい時に使う")]
        [SerializeField] private bool _handleOpenInput = true;

        [Tooltip("ビルド番号 0 のシーン（タイトル）では開かない")]
        [SerializeField] private bool _blockOnFirstScene = true;

        [Header("開いている間の挙動")]
        [Tooltip("開いている間カーソルを表示する")]
        [SerializeField] private bool _releaseCursor = true;

        [Tooltip("開いている間 Time.timeScale を 0 にする")]
        [SerializeField] private bool _pauseGame;

        // ------------------------------------------------------------------
        [Header("イベント")]
        [Tooltip("開いた瞬間。プレイヤー操作の無効化などをここに繋ぐ")]
        public UnityEvent onOpened;

        [Tooltip("閉じた瞬間。開く前の状態に戻す処理をここに繋ぐ")]
        public UnityEvent onClosed;

        /// <summary>UnityEvent&lt;int&gt; はそのままでは Inspector に出ないので、具象クラスにしておく。</summary>
        [Serializable] public class PageChangedEvent : UnityEvent<int> { }

        [Tooltip("ページが切り替わった時。引数はページ番号")]
        public PageChangedEvent onPageChanged;

        // ------------------------------------------------------------------
        private bool _isOpen;
        private int _currentPage = -1;
        private bool _blocked;

        private CursorLockMode _cursorLockBeforeOpen;
        private bool _cursorVisibleBeforeOpen;
        private float _timeScaleBeforeOpen = 1f;

        /// <summary>現在開いているか。</summary>
        public bool IsOpen => _isOpen;

        /// <summary>現在のページ番号。</summary>
        public int CurrentPage => _currentPage;

        public SettingsTheme Theme => _theme;

        /// <summary>他システムから一時的に開けなくする（ムービー中など）。</summary>
        public void SetBlocked(bool blocked) => _blocked = blocked;

        // ------------------------------------------------------------------

        private void Awake()
        {
            if (_root == null) _root = gameObject;

            for (int i = 0; i < _pages.Count; i++)
            {
                var page = _pages[i];
                if (page?.tab == null) continue;

                page.tab.Initialize(_theme);

                int index = i; // クロージャ用にコピー
                page.tab.Button.onClick.AddListener(() => ShowPage(index));
            }

            if (_closeButton != null) _closeButton.onClick.AddListener(Close);

            SetOpen(false, immediate: true);
            ShowPage(_defaultPageIndex);
        }

        private void OnEnable()
        {
            if (_openAction != null && _openAction.action != null) _openAction.action.Enable();
        }

        private void Update()
        {
            if (!_handleOpenInput || _openAction == null || _openAction.action == null) return;

            // 開いていない間だけ、他システムが Escape を専有していないか確認する。
            // （開いている間は閉じる操作を受け付けたいので素通りさせる）
            if (!_isOpen)
            {
                if (_blocked) return;
                if (_blockOnFirstScene && SceneManager.GetActiveScene().buildIndex == 0) return;

                App.Input.GameInputGate.PurgeDestroyedEscapeOwners();
                if (App.Input.GameInputGate.IsEscapeCaptured) return;
            }

            if (_openAction.action.WasPressedThisFrame()) Toggle();
        }

        // ------------------------------------------------------------------
        // 開閉
        // ------------------------------------------------------------------

        public void Toggle() => SetOpen(!_isOpen);
        public void Open()   => SetOpen(true);
        public void Close()  => SetOpen(false);

        private void SetOpen(bool open, bool immediate = false)
        {
            if (_isOpen == open && !immediate) return;
            _isOpen = open;

            if (_root != null) _root.SetActive(open);

            if (open)
            {
                _cursorLockBeforeOpen = Cursor.lockState;
                _cursorVisibleBeforeOpen = Cursor.visible;
                _timeScaleBeforeOpen = Time.timeScale;

                if (_releaseCursor)
                {
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                }
                if (_pauseGame) Time.timeScale = 0f;

                if (!immediate) onOpened?.Invoke();
            }
            else
            {
                if (_releaseCursor)
                {
                    Cursor.lockState = _cursorLockBeforeOpen;
                    Cursor.visible = _cursorVisibleBeforeOpen;
                }
                if (_pauseGame) Time.timeScale = _timeScaleBeforeOpen;

                // 選択状態が残ると、閉じたあとキー入力がボタンに吸われることがある
                if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);

                if (!immediate) onClosed?.Invoke();
            }
        }

        // ------------------------------------------------------------------
        // ページ切り替え
        // ------------------------------------------------------------------

        /// <summary>指定番号のページを表示する。Button の OnClick から直接呼んでもよい。</summary>
        public void ShowPage(int index)
        {
            if (_pages.Count == 0) return;
            index = Mathf.Clamp(index, 0, _pages.Count - 1);
            if (_currentPage == index) return;

            _currentPage = index;

            for (int i = 0; i < _pages.Count; i++)
            {
                var page = _pages[i];
                if (page == null) continue;

                bool selected = i == index;
                if (page.panel != null) page.panel.SetActive(selected);
                if (page.tab != null) page.tab.SetSelected(selected);
            }

            onPageChanged?.Invoke(index);
        }

        // ------------------------------------------------------------------
        // テーマ適用
        // ------------------------------------------------------------------

        /// <summary>
        /// 配下の ThemedElement と Selectable にテーマを流し込む。
        /// テーマアセットを編集したあと、Inspector の右クリックメニューから実行する。
        /// </summary>
        [ContextMenu("テーマを適用")]
        public void ApplyTheme()
        {
            if (_theme == null)
            {
                Debug.LogWarning("[SettingsScreen] Theme が未設定です。", this);
                return;
            }

            foreach (var element in GetComponentsInChildren<ThemedElement>(true))
                element.Apply(_theme);

            foreach (var selectable in GetComponentsInChildren<Selectable>(true))
                _theme.ApplyTint(selectable, SelectableTint.Resolve(selectable));

            foreach (var page in _pages)
                page?.tab?.Initialize(_theme);

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEditor.EditorUtility.SetDirty(this);
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
            }
#endif
            Debug.Log("[SettingsScreen] テーマを適用しました。", this);
        }
    }
}
