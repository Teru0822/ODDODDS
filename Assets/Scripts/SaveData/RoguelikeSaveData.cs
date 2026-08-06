using System;
using System.Collections.Generic;

[Serializable]
public class RoguelikeSaveData
{
    //基本情報
    public int money;
    public int unwashedMoney;
    public int bronzeCoin;
    public int silverCoin;
    public int goldCoin;
    public int blackDiamond;
    public int virtuePoints;

    //ターン数など
    public int nowTurn;//現在のターン
    public int nextDebtTurn;//次の取り立てターン
    public int nextDebtPrice;//次の取り立てられる金額
    public int leftDebtAmount;//残りの借金の金額
    public int debtClearTimes;//借金の取り立てに何回耐えきったか
    public bool isWatchTour;//ツアーを一回みたか

    //恒久ポイントを活用したローグライク要素
    public bool isUnlockPinball    = false; // ピンボール機能を解放
    public bool isUnlockTypewriter = false; // タイプライター機能を解放
    public bool isUnlockMinigame   = false; // ミニゲーム機能を解放
    public bool isUnlockVisitor    = false; // 訪問者機能を解放

    //アイテム関連
    public List<ItemSaveData> ownedPermanentItems;//恒常アイテムのリスト
    public List<ItemSaveData> ownedConsumeItems;//消費アイテムのリスト

    //エフェクト関連
    public List<EffectSaveData> ownedEffects;//エフェクトのリスト

    //訪問者関連
    public List<VisitorSaveData> visitorSaveDatas;//訪問者の進行状況をセーブするリスト

    // 今後ここに追加のメタプログレッション要素（スキル解放状態など）を記載
    public List<RoguelikeSaveClass> roguelikeSaveDatas;

    //ーーー各遊技のローグライク要素をここに記述していくーーーー
    //UFOキャッチャー
    public bool isUfoCatcherUnlocked = false; // 例: UFOキャッチャーが解放されたかどうか

    //鉄球落とし
    public bool isFallBallUnlocked = false;   // 例: 鉄球落としが解放されたかどうか

    //ピンボール
    public bool isPinballUnlocked = false;    // 例: ピンボールが解放されたかどうか

    //UFOキャッチャー・ルーレット特別演出（Element2 / Roulette03）
    public bool isRouletteCandyObtained = false;    // candyを入手済みか
    public bool isRoulettePinballObtained = false;  // pinballを入手済みか
}
