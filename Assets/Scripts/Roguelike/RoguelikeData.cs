using System;
using System.Collections.Generic;

public enum SkillType
{
    None = 0,
    PinBall = 1,
    FallBall = 2,
    DevilCatcher = 3,
    Typewriter = 4,
}
public enum SkillId
{
    None = 0,
    
    // PinBall
    PinBall_ExpandGoal = 0,
    PinBall_DoubleOrNothingBonus = 1,
    PinBall_AddPin = 2,
    PinBall_SplitPinUpgrade = 3,
    
    // FallBall
    FallBall_Strengthen = 4,
    FallBall_ExpandHole = 5,
    FallBall_ReduceEnemySpeed = 6,
    FallBall_Exhausted = 7,
    
    // DevilCatcher
    UFO_ArmLevel2 = 8,
    UFO_ArmLevel3 = 9,
    UFO_ArmLevelUltimate = 10,
    UFO_RetryOnce = 11,
    UFO_DoubleReward = 12,
    UFO_ExpandDropHole1 = 13,
    UFO_ExpandDropHole2 = 14,
    UFO_ExpandDropHole3 = 15,
    UFO_ItemDropCoin = 16,
    UFO_PolishDiamond1 = 17,
    UFO_PolishDiamond2 = 18,
    UFO_PolishDiamond3 = 19,
    UFO_ReduceSway1 = 20,
    UFO_ReduceSway2 = 21,
    UFO_ArmSpeedUp1 = 22,
    UFO_ArmSpeedUp2 = 23,
    UFO_FeverTime = 24,
    UFO_ReduceSway3 = 25,
    UFO_ExpandItems1 = 26,
    UFO_ExpandItems2 = 27,
    UFO_LeverPrecision = 28,

    // Typewriter
    Typewriter_ExpandChoice1 = 29,  // 2択 → 3択
    Typewriter_ExpandChoice2 = 30,  // 3択 → 4択

    // DevilCatcher (追加分)
    UFO_Unlock90Sec = 31,  // プレイ時間90秒をアンロック
    UFO_ShowDescentLaser = 32,  // アームの降下地点をわかるようにする
}

public class RoguelikeData
{
    public SkillId id;//[OCNIDローグライクのID
    public string skillName;//スキルの名前
    public SkillType skillType;//スキルの名前
    public string skillDescription;//スキルの説明
    public bool isActive = true;//スキルを有効化するか否か
    public bool isGet = false;//スキルが取得されているか否か
}

[Serializable]
public class RoguelikeDataContainer
{
    public List<RoguelikeData> roguelikeDatas;
}
