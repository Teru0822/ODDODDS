using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
[CreateAssetMenu(menuName = "EffectDataBase")]
public class EffectDataBase : ScriptableObject
{
    public List<EffectData> effectDataBase;
}
