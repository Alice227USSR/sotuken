using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Policies;

public class HanabiManager : MonoBehaviour
{
    public PlayManager playManager;
    public HandManager handManager;
    public ScoreManager scoreManager;
    public ShuffleManager shuffleManager;
    public CardDisplayManager cardDisplayManager;
    public FireworkDisplayManager fireworkDisplayManager;
    public TurnManager turnManager;
    public UIManager uiManager;

    public List<HanabiAgentHuman> agents;
    public int numPlayers = 2;
    public int handSize = 5;
    public bool autoPlay = false;
    public int selfIndex = 0;
    public TMPro.TextMeshProUGUI currentTurnText;

    [SerializeField] ExternalAIController externalAIController;

    void Start()
    {
        Debug.Log("Start called");
        InitializeGame();

        for (int i = 0; i < agents.Count; i++)
        {
            agents[i].SetManager(this);
            agents[i].SetPlayerIndex(i);
        }

        int currentPlayerIndex = turnManager.GetCurrentPlayer();
        if (agents[currentPlayerIndex].isExternalAI && externalAIController != null)
        {
            Debug.Log("Game Start: AI is first player. Forcing AI move.");
            externalAIController.SendObservationToPython(currentPlayerIndex);
        }
    }

    public void NotifyActionAppliedFromTCP()
    {
        // externalAIController が private でもここからは触れる
        if (externalAIController != null)
        {
            externalAIController.NotifyActionApplied();
        }
    }

    public void InitializeGame()
    {
        Debug.Log("InitializeGame called");

        if (shuffleManager == null) shuffleManager = FindObjectOfType<ShuffleManager>();
        if (shuffleManager == null)
        {
            Debug.LogWarning("[HanabiManager] ShuffleManager not found in scene. Creating one at runtime.");
            var go = new GameObject("ShuffleManager");
            shuffleManager = go.AddComponent<ShuffleManager>();
        }

        shuffleManager.BuildAndShuffle();

        playManager  = new PlayManager();
        handManager  = new HandManager(numPlayers, handSize, shuffleManager);
        scoreManager = new ScoreManager();
        turnManager  = new TurnManager(numPlayers);

        if (uiManager != null)
        {
            uiManager.UpdateHint(scoreManager.hints);
            uiManager.UpdateLife(scoreManager.lives);
            uiManager.UpdateScore(scoreManager.score);
            uiManager.Initialize(this);
        }
        else
        {
            Debug.LogWarning("[HanabiManager] uiManager is null (Inspector で割り当て推奨)");
        }

        if (cardDisplayManager != null)
        {
            cardDisplayManager.SetOnCardSelected(OnCardSelected);
            cardDisplayManager.DisplayHands(handManager.hands, selfIndex);
        }
        else
        {
            Debug.LogWarning("[HanabiManager] cardDisplayManager is null");
        }
    }

    void Update()
    {
        if (autoPlay)
        {
            StepAgents();
            CheckGameEnd();
        }

        if (uiManager != null && scoreManager != null)
        {
            uiManager.UpdateHint(scoreManager.hints);
            uiManager.UpdateLife(scoreManager.lives);
            uiManager.UpdateScore(scoreManager.score);
        }
    }

    public void StepAgents()
    {
        int current = turnManager.GetCurrentPlayer();
        var human = agents[current];

        if (human.isExternalAI && externalAIController != null)
            externalAIController.SendObservationToPython(current);
        else
            cardDisplayManager.DisplayHands(handManager.hands, current);
    }

    public void ResetGame() => InitializeGame();

    public void CheckGameEnd()
    {
        if (IsTerminalLikeHLE())
        {
            // 既存ログ文言はそのままでもOK
            if (scoreManager.lives <= 0)
                Debug.Log("Game Over: Out of lives");
            else if (shuffleManager.Remaining() == 0)
                Debug.Log("Game End: Deck exhausted");
            else if (scoreManager.IsAllFireworksCompleted())
                Debug.Log("Victory: All fireworks completed!");

            TryEndGameIfTerminal(); // ← 実際に止める
        }
    }

    // 終局フラグ
    private bool gameEnded = false;


    // HLE相当の終局判定
    public bool IsTerminalLikeHLE()
    {
        if (gameEnded) return true;
        if (scoreManager.lives <= 0) return true;                // ライフ尽き
        if (scoreManager.IsAllFireworksCompleted()) return true; // 全色5
        bool deckEmpty = shuffleManager.Remaining() == 0;        // 山札0
        bool allHandsEmpty = true;
        for (int p = 0; p < handManager.hands.Count; p++)
            if (handManager.hands[p].hand.Count > 0) { allHandsEmpty = false; break; }
        if (deckEmpty && allHandsEmpty) return true;             // 山札0 & 全員手札0
        return false;
    }

    // 終局時の停止・UI反映
    public void TryEndGameIfTerminal()
    {
        if (gameEnded) return;
        if (!IsTerminalLikeHLE()) return;
        gameEnded = true;
        Debug.Log("[Game] Terminal reached. Ending game.");
        if (uiManager != null)
        {
            uiManager.UpdateHint(scoreManager.hints);
            uiManager.UpdateLife(scoreManager.lives);
            uiManager.UpdateScore(scoreManager.score);
            // 必要なら最終表示: uiManager.ShowFinal(scoreManager.score);
        }
    }

    public void OnCardSelected(int index)
    {
        int currentPlayer = turnManager.GetCurrentPlayer();
        Debug.Log($"Player {currentPlayer} selected card index: {index}");
    }

    public void ExecuteHintFromUI(int targetPlayer, string hint)
    {
        if (scoreManager.lives <= 0)
        {
            Debug.Log("ライフが0のため操作できません");
            return;
        }
        playManager.ExecuteAction(selfIndex, "hint", targetPlayer, hint, handManager, scoreManager, shuffleManager, fireworkDisplayManager);
        cardDisplayManager.DisplayHands(handManager.hands, selfIndex);
        CompletePlayerTurn();
    }

    public void OnDiscardButtonPressed()
    {
        if (scoreManager.lives <= 0)
        {
            Debug.Log("ライフが0のため操作できません");
            return;
        }
        agents[selfIndex].DiscardSelectedCard();
    }

    public void PlaySelectedCardFromUI()
    {
        int current = turnManager.GetCurrentPlayer();
        // 人間ターン前提（外部AIのときは何もしない）
        if (agents[current].isExternalAI) { Debug.LogWarning("AIターン中のため手動Play無視"); return; }

        int idx = CardSelectionManager.Instance != null
            ? CardSelectionManager.Instance.CurrentSelectedIndex
            : -1;

        if (idx < 0) { Debug.LogWarning("カードが選択されていません"); return; }

        // 手札枚数に合わせて範囲ガード
        int handCount = handManager.hands[current].hand.Count;
        if (idx >= handCount) idx = handCount - 1;

        // 実行
        playManager.ExecuteAction(
            current, "play", idx, null,
            handManager, scoreManager, shuffleManager, fireworkDisplayManager);

        // 画面更新とターン進行
        if (cardDisplayManager != null) cardDisplayManager.DisplayHands(handManager.hands, current);
        if (uiManager != null)
        {
            uiManager.UpdateHint(scoreManager.hints);
            uiManager.UpdateLife(scoreManager.lives);
            uiManager.UpdateScore(scoreManager.score);
        }
        CompletePlayerTurn();
    }

    public void CompletePlayerTurn()
    {
        if (gameEnded) return;                   // ★追加：終局なら何もしない
        turnManager.AdvanceTurn();
        selfIndex = turnManager.GetCurrentPlayer();
        CheckGameEnd();
        if (gameEnded) return;                   // ★追加：Check後に終局なら止める

        UpdateCurrentTurnUI();

        int current = turnManager.GetCurrentPlayer();
        var human = agents[current];
        if (human.isExternalAI && externalAIController != null)
            externalAIController.SendObservationToPython(current);
        else
            cardDisplayManager.DisplayHands(handManager.hands, current);
    }

    void UpdateCurrentTurnUI()
    {
        int currentPlayer = turnManager.GetCurrentPlayer();
        if (currentTurnText) currentTurnText.text = $"Turn: Player {currentPlayer}";
    }
}
