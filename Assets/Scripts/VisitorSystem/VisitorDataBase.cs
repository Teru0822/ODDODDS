using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
[CreateAssetMenu(menuName = "DataBase/VisitorDataBase")]
public class VisitorDataBase : ScriptableObject
{
    public List<VisitorData> visitorDataBase;
}
