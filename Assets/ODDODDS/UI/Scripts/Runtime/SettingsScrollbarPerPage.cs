using System;
using UnityEngine;
using UnityEngine.UI;

namespace OddOdds.UI.Settings
{
    /// <summary>
    /// 表示中のページごとに縦スクロールバーを出し分ける。
    ///
    /// 設定画面は 1 つの ScrollRect を 4 ページで使い回しているため、
    /// ScrollRect 標準の AutoHide だけでは「項目が少ないページでもバーが残る」ことがある。
    /// ページ単位で明示的に指定できるようにしてある。
    /// </summary>
    [DisallowMultipleComponent]
    public class SettingsScrollbarPerPage : MonoBehaviour
    {
        [Serializable]
        public class PageRule
        {
            [Tooltip("対象ページ（GraphicContent など）")]
            public GameObject page;

            [Tooltip("このページでスクロールバーを表示するか")]
            public bool showScrollbar = true;
        }

        [Tooltip("出し分ける対象のスクロールバー")]
        [SerializeField] private Scrollbar _scrollbar;

        [Tooltip("ページごとの表示設定。上から順に、最初に見つかったアクティブなページが採用される")]
        [SerializeField] private PageRule[] _rules = new PageRule[0];

        [Tooltip("どのページにも当てはまらないときの表示")]
        [SerializeField] private bool _fallbackShow = true;

        private GameObject _lastActivePage;

        private void OnEnable() => Refresh();

        private void Update()
        {
            // ページ切り替えは SettingUIManager 側が SetActive で行うため、
            // イベントを持たない。切り替わった時だけ反映する
            var active = FindActivePage();
            if (active == _lastActivePage) return;
            _lastActivePage = active;
            Apply(active);
        }

        /// <summary>現在の状態を即座に反映する。</summary>
        public void Refresh()
        {
            _lastActivePage = FindActivePage();
            Apply(_lastActivePage);
        }

        private GameObject FindActivePage()
        {
            if (_rules == null) return null;
            foreach (var rule in _rules)
            {
                if (rule?.page != null && rule.page.activeInHierarchy) return rule.page;
            }
            return null;
        }

        private void Apply(GameObject activePage)
        {
            if (_scrollbar == null) return;

            bool show = _fallbackShow;
            if (activePage != null && _rules != null)
            {
                foreach (var rule in _rules)
                {
                    if (rule?.page == activePage)
                    {
                        show = rule.showScrollbar;
                        break;
                    }
                }
            }

            SetVisible(show);
        }

        /// <summary>
        /// 見えなくする。
        ///
        /// SetActive では消せない。ScrollRect の Visibility が Permanent 以外だと、
        /// ScrollRect 自身がスクロール要否に応じて毎フレーム SetActive を切り替えるため、
        /// こちらで false にしてもすぐ戻されてしまう。
        /// CanvasGroup なら ScrollRect の管理と衝突しない。
        /// </summary>
        private void SetVisible(bool visible)
        {
            var group = _scrollbar.GetComponent<CanvasGroup>();
            if (group == null) group = _scrollbar.gameObject.AddComponent<CanvasGroup>();

            float alpha = visible ? 1f : 0f;
            if (!Mathf.Approximately(group.alpha, alpha)) group.alpha = alpha;
            group.interactable = visible;
            group.blocksRaycasts = visible;
        }

#if UNITY_EDITOR
        /// <summary>生成ツールからの配線用。</summary>
        public void Bind(Scrollbar scrollbar, PageRule[] rules)
        {
            _scrollbar = scrollbar;
            _rules = rules;
        }
#endif
    }
}
