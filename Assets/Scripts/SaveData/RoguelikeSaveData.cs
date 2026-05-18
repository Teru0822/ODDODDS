using System;

[Serializable]
public class RoguelikeSaveData
{
    public int virtuePoints;
    // 今後ここに追加のメタプログレッション要素（スキル解放状態など）を記載できます

    //ーーー各遊技のローグライク要素をここに記述していくーーーー
    //UFOキャッチャー
    public bool isUfoCatcherUnlocked = false; // 例: UFOキャッチャーが解放されたかどうか

    //鉄球落とし
    public bool isFallBallUnlocked = false;   // 例: 鉄球落としが解放されたかどうか

    //ピンボール
    public bool isPinballUnlocked = false;    // 例: ピンボールが解放されたかどうか
}
