using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
[CreateAssetMenu(menuName = "DataBase/EffectDataBase")]
public class EffectDataBase : ScriptableObject
{
    public List<EffectData> effectDataBase;
}
