namespace App.Input
{
    /// <summary>
    /// ゲーム全体のマウス・インタラクション入力をまとめてブロックするゲート。
    /// IntroTourDirector 等の演出中に Lock() し、終了後に Unlock() する。
    /// </summary>
    public static class GameInputGate
    {
        public static bool IsBlocked { get; private set; }

        public static void Lock()   => IsBlocked = true;
        public static void Unlock() => IsBlocked = false;
    }
}
