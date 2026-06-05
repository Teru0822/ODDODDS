using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class InterfaceFinder : MonoBehaviour
{
    // 最初のインタフェース実装を返す
    public static T FindFirstByInterface<T>() where T : class
    {
        var components = Object.FindObjectsByType<Component>(FindObjectsSortMode.InstanceID);
        return components.OfType<T>().FirstOrDefault();
    }

    // 全部返す
    public static IEnumerable<T> FindAllByInterface<T>() where T : class
    {
        var components = Object.FindObjectsByType<Component>(FindObjectsSortMode.InstanceID);
        return components.OfType<T>();
    }
}
