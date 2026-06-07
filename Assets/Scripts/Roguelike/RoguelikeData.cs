using System;
using System.Collections.Generic;

public enum SkillType
{ 
    None = 0,
    PinBall = 1,
    FallBall = 2,
    UFOcatcher = 3,
}
public class RoguelikeData
{
    public int id;//ローグライクのID
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
