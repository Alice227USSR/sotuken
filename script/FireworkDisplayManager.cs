using System.Collections.Generic;
using UnityEngine;

public class FireworkDisplayManager : MonoBehaviour
{
    public GameObject cardPrefab;
    public Transform fireworkAreaRoot; // UI上の中央表示エリア
    private Dictionary<string, Transform> colorRoots = new(); // 色ごとの親

    private char[] colors = HanabiSpec.ColorOrder; // {'R','Y','G','W','B'}

    void Start()
    {
        // 色ごとに表示位置を作る（簡易的に横並びなど）
        for (int i = 0; i < colors.Length; i++)
        {
            string key = colors[i].ToString();
            GameObject root = new GameObject(key);
            root.transform.SetParent(fireworkAreaRoot, false);
            root.transform.localPosition = new Vector3(i * 80f, 0f, 0f);
            colorRoots[key] = root.transform; // Dictionary<string, Transform> のままでOK
        }
    }

    public void UpdateFireworks(Dictionary<string, int> fireworks)
    {
        // 表示を一度リセット
        foreach (var root in colorRoots.Values)
        {
            foreach (Transform child in root)
            {
                Destroy(child.gameObject);
            }
        }

        foreach (var kv in fireworks)
        {
            Debug.Log($"UpdateFireworks: {kv.Key} = {kv.Value}");
        }

        // 各色の最新カードを表示
        foreach (var c in colors)
        {
            string key = c.ToString();
            int num = fireworks[key];
            if (num > 0)
            {
                GameObject card = Instantiate(cardPrefab, colorRoots[key]);
                card.transform.localPosition = Vector3.zero;

                var view = card.GetComponent<CardView>();
                view?.Set(new Card(key, num), null, true);
            }
        }
    }
}
