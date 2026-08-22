using UnityEngine;
using UnityEngine.UI;

namespace OddOdds.UI.Settings
{
    /// <summary>
    /// この UI 部品がテーマ上どういう役割かを宣言するマーカー。
    /// SettingsScreen の「テーマを適用」を実行すると、配下のこれらが一括で塗り直される。
    ///
    /// 手で追加した Image / Text にもこれを付けておけば、テーマ変更に追従する。
    /// 逆に、テーマから外して個別に色を決めたい部品には付けなければよい。
    /// </summary>
    [DisallowMultipleComponent]
    [ExecuteAlways]
    public class ThemedElement : MonoBehaviour
    {
        [Tooltip("テーマ上の役割。色とフォントがここから決まる")]
        public ThemeRole role = ThemeRole.None;

        [Tooltip("チェックすると、この部品だけテーマ適用の対象外になる（手で色を決めたい時に使う）")]
        public bool ignore;

        private Graphic _graphic;

        /// <summary>この部品にテーマを適用する。</summary>
        public void Apply(SettingsTheme theme)
        {
            if (theme == null || ignore) return;

            if (_graphic == null) _graphic = GetComponent<Graphic>();
            if (_graphic == null) return;

            theme.Apply(role, _graphic);
        }
    }
}
