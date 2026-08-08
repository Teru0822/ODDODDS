using System.Collections.Generic;
using UnityEngine;

namespace App.Input
{
    /// <summary>
    /// ゲーム全体のマウス・インタラクション入力をまとめてブロックするゲート。
    /// IntroTourDirector 等の演出中に Lock() し、終了後に Unlock() する。
    ///
    /// あわせて「Escape キーを専有している対象」も管理する。ATM のように
    /// カメラをフォーカスしている間は Escape がフォーカス解除に使われるため、
    /// 設定画面など同じキーを見ている側は IsEscapeCaptured を見て入力を無視する。
    /// </summary>
    public static class GameInputGate
    {
        public static bool IsBlocked { get; private set; }

        public static void Lock()   => IsBlocked = true;
        public static void Unlock() => IsBlocked = false;

        // --- Escape の専有 ---

        private static readonly HashSet<Object> _escapeOwners = new HashSet<Object>();

        // 解放した瞬間のフレーム。MonoBehaviour の Update 順は不定なので、
        // 「解放したフレーム」も伏せておかないと同フレームで設定画面が開いてしまう
        private static int _lastEscapeReleaseFrame = -1;

        /// <summary>Escape を専有している対象がいるか（解放した当該フレームも true）。</summary>
        public static bool IsEscapeCaptured =>
            _escapeOwners.Count > 0 || Time.frameCount == _lastEscapeReleaseFrame;

        /// <summary>Escape の専有を開始する。同じ owner を二重に登録しても1件として扱う。</summary>
        public static void CaptureEscape(Object owner)
        {
            if (owner == null) return;
            _escapeOwners.Add(owner);
        }

        /// <summary>Escape の専有を終了する。</summary>
        public static void ReleaseEscape(Object owner)
        {
            if (owner == null) return;
            if (_escapeOwners.Remove(owner) && _escapeOwners.Count == 0)
            {
                _lastEscapeReleaseFrame = Time.frameCount;
            }
        }

        /// <summary>
        /// 破棄済みオブジェクトが登録に残っている場合の保険。
        /// シーン遷移で専有主が消えても Escape が効かなくなるのを防ぐ。
        /// </summary>
        public static void PurgeDestroyedEscapeOwners()
        {
            int before = _escapeOwners.Count;
            _escapeOwners.RemoveWhere(o => o == null);
            if (before > 0 && _escapeOwners.Count == 0)
            {
                _lastEscapeReleaseFrame = Time.frameCount;
            }
        }
    }
}
