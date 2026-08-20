using UnityEngine;
using System;
[Serializable]
[CreateAssetMenu(menuName = "Data/EffectData")]
public class EffectData  : ScriptableObject
{
    public int id; //アイテムのID
    public int turn;//何ターン持続するか
    public bool isInfinity = false;//効果が永続するものならTrue
    [Tooltip("0:日本語, 1:英語, 2:中国語")]
    public string[] effectName;//名前
    [Tooltip("0:日本語, 1:英語, 2:中国語")]
    public string[] description;//説明
    public string description_en;//説明(英語)
    public Sprite effectIcon;//アイテムの画像
    public EffectType effectType;//エフェクトの種類：バフかデバフか
}
