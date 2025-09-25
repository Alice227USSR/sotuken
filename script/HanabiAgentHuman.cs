using UnityEngine;

public class HanabiAgentHuman : MonoBehaviour
{
    public int playerIndex;
    public HanabiManager manager;
    private int selectedCardIndex = -1;
    public bool isExternalAI = false;  // Inspectorで切り替えられるように
    public void SetPlayerIndex(int index)
    {
        playerIndex = index;
    }

    public void SetManager(HanabiManager manager)
    {
        this.manager = manager;
    }

    public void SelectCard(int index)
    {
        selectedCardIndex = index;
        Debug.Log($"カード {index} が選択されました");
    }

    public void PlaySelectedCard()
    {
        Debug.Log("PlaySelectedCard() が呼ばれました");
        int index = selectedCardIndex;
        if (index < 0 || index >= manager.handManager.hands[playerIndex].hand.Count)
        {
            Debug.LogWarning("Invalid card index.");
            return;
        }

        Card selected = manager.handManager.hands[playerIndex].hand[index];
        string color = selected.color;
        int number = selected.number;

        if (manager.scoreManager.fireworks[color] == number - 1)
        {
            manager.scoreManager.fireworks[color]++;
            manager.scoreManager.score++;
            Debug.Log("Card played successfully!");
        }
        else
        {
            manager.scoreManager.lives--;
            manager.scoreManager.discardPile.Add(selected);
            Debug.Log("Card play failed.");
        }

        manager.handManager.RemoveCard(playerIndex, index);

        if (manager.shuffleManager.Remaining() > 0)
        {
            Card newCard = manager.shuffleManager.Draw();
            manager.handManager.hands[playerIndex].hand.Insert(index, newCard);
            manager.handManager.hands[playerIndex].knowledge.Insert(index, new CardKnowledge());
            Debug.Log("New card drawn and added to hand.");
        }

        manager.cardDisplayManager.DisplayHands(manager.handManager.hands, playerIndex);
        manager.CompletePlayerTurn();
        selectedCardIndex = -1;
    }

    public void DiscardSelectedCard()
    {
        Debug.Log("DiscardSelectedCard() が呼ばれました");
        int index = selectedCardIndex;
        if (index < 0 || index >= manager.handManager.hands[playerIndex].hand.Count)
        {
            Debug.LogWarning("Invalid card index.");
            return;
        }

        manager.playManager.ExecuteAction(
            playerIndex,
            "discard",
            index,
            null,
            manager.handManager,
            manager.scoreManager,
            manager.shuffleManager,
            manager.fireworkDisplayManager
        );

        manager.cardDisplayManager.DisplayHands(manager.handManager.hands, playerIndex);
        manager.CompletePlayerTurn();
        selectedCardIndex = -1;
    }
}
