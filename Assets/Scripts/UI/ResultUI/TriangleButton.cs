using UnityEngine;
using UnityEngine.UI;

public class TriangleButton : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        Image image = GetComponent<Image>();

        // 0.5f = アルファ値が50%以上の場所だけクリックに反応させる
        image.alphaHitTestMinimumThreshold = 0.5f;
    }
}
