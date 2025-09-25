using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;

public class HanabiAgent : Agent
{
    private HanabiManager manager;
    private int playerIndex;
    private int selectedCardIndex = -1;

    public void SetPlayerIndex(int index)
    {
        playerIndex = index;
    }

    public void SetManager(HanabiManager manager)
    {
        this.manager = manager;
    }

    public void Initialize(HanabiManager manager)
    {
        this.manager = manager;
    }

    public override void OnEpisodeBegin()
    {
        // 特に何もせず、HanabiManager 側で管理
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        var obs = manager.playManager.GetObservation(playerIndex, manager.handManager, manager.scoreManager, manager.shuffleManager);
        foreach (var val in obs)
        {
            sensor.AddObservation(val);
        }
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        if (manager.turnManager.GetCurrentPlayer() != playerIndex)
            return;

        int action = actions.DiscreteActions[0];
        var legalActions = manager.playManager.GetLegalActions(playerIndex, manager.handManager, manager.scoreManager);

        if (action < legalActions.Count)
        {
            var chosen = legalActions[action];
            manager.playManager.ExecuteAction(
                playerIndex,
                chosen.actionType,
                chosen.targetIndex,
                chosen.hintInfo,
                manager.handManager,
                manager.scoreManager,
                manager.shuffleManager,
                manager.fireworkDisplayManager
            );
        }

        manager.CompletePlayerTurn(); // ← ターン進行を統一管理
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        // 手動操作が必要であれば実装
    }

    public void SelectCard(int index)
    {
        selectedCardIndex = index;
        Debug.Log($"カード {index} が選択されました");
    }

    public void PlaySelectedCard()
    {
        Debug.Log("PlaySelectedCard() が呼ばれました");
        Debug.Log("selectedCardIndex = " + selectedCardIndex);

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

        manager.cardDisplayManager.DisplayHands(manager.handManager.hands, manager.selfIndex);
        manager.CompletePlayerTurn();  // ← ここも統一！
        selectedCardIndex = -1;
    }

    public void DiscardSelectedCard()
    {
        Debug.Log("DiscardSelectedCard() が呼ばれました");
        Debug.Log("selectedCardIndex = " + selectedCardIndex);

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

        manager.cardDisplayManager.DisplayHands(manager.handManager.hands, manager.selfIndex);
        manager.CompletePlayerTurn(); // ← 統一！
        selectedCardIndex = -1;
    }
}
