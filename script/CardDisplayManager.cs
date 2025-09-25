using System.Collections.Generic;
using UnityEngine;

public class CardDisplayManager : MonoBehaviour
{
    public GameObject cardPrefab;
    public Transform[] playerHandRoots;

    private System.Action<int> onCardSelected; // カードが選ばれたときのコールバック

    public void SetOnCardSelected(System.Action<int> callback)
    {
        onCardSelected = callback;
    }

    public void DisplayHands(List<Player> players, int selfIndex)
{
    Debug.Log("selfIndex: " + selfIndex);
    Debug.Log("players.Count: " + players.Count);
    Debug.Log("players[selfIndex].hand.Count: " + players[selfIndex].hand.Count);
    Debug.Log("players[selfIndex].knowledge.Count: " + players[selfIndex].knowledge.Count);

    for (int i = 0; i < players.Count; i++)
    {
        bool showFace = true; // 常に手札表示（開発者向けUI）
        DisplayHand(players[i].hand, players[i].knowledge, playerHandRoots[i], showFace);
    }
}
    public void DisplayHand(List<Card> cards, List<CardKnowledge> knowledge, Transform root, bool showFace)
    {
        if (root == null)
        {
            Debug.LogError("DisplayHand: root is null!");
            return;
        }

        foreach (Transform child in root)
        {
            Destroy(child.gameObject);
        }

        for (int i = 0; i < cards.Count; i++)
        {
            var obj = Instantiate(cardPrefab, root);
            obj.transform.localPosition = new Vector3(i * 0.6f, 0f, 0f);
            obj.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // Z軸を手前に倒す

            var view = obj.GetComponent<CardView>();
            if (view == null)
            {
                Debug.LogError("CardView component not found on card prefab!");
                continue;
            }

            if (i >= knowledge.Count)
            {
                Debug.LogError($"Index {i} out of range in knowledge (count: {knowledge.Count})");
                continue;
            }

            view.Set(cards[i], knowledge[i], showFace);
            
            if (showFace && onCardSelected != null)
            {
                view.SetIndexAndCallback(i, onCardSelected);
            }
        }
    }
}
