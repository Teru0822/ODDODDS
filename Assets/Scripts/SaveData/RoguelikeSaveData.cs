using System;
using System.Collections.Generic;

[Serializable]
public class RoguelikeSaveData
{
    public int virtuePoints;
    //恒久ポイントを活用したローグライク要素
    public bool isUnlockPinball    = false; // ピンボール機能を解放
    public bool isUnlockTypewriter = false; // タイプライター機能を解放
    public bool isUnlockMinigame   = false; // ミニゲーム機能を解放
    public bool isUnlockVisitor    = false; // 訪問者機能を解放

    //アイテム関連
    public List<ItemSaveData> ownedPermanentItems;//恒常アイテムのリスト
    public List<ItemSaveData> ownedConsumeItems;//消費アイテムのリスト

    //エフェクト関連
    public List<EffectSaveData> ownedEffects;//消費アイテムのリスト

    //訪問者関連
    public List<VisitorSaveData> visitorSaveDatas;

    // 今後ここに追加のメタプログレッション要素（スキル解放状態など）を記載

    //ーーー各遊技のローグライク要素をここに記述していくーーーー
    //UFOキャッチャー
    public bool isUfoCatcherUnlocked = false; // 例: UFOキャッチャーが解放されたかどうか

    //鉄球落とし
    public bool isFallBallUnlocked = false;   // 例: 鉄球落としが解放されたかどうか

    //ピンボール
    public bool isPinballUnlocked = false;    // 例: ピンボールが解放されたかどうか
}
