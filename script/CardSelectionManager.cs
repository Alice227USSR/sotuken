using UnityEngine;

public class CardSelectionManager : MonoBehaviour
{
    public static CardSelectionManager Instance;

    private CardView currentSelected;
    private int selectedCardIndex = -1;
    public HanabiManager hanabiManager;
    public int CurrentSelectedIndex => selectedCardIndex; // 追記（getter）

    void Awake()
    {
        Instance = this;
    }

    public void Select(CardView newCard)
    {
        if (currentSelected != null && currentSelected != newCard)
        {
            currentSelected.Deselect();
        }

        if (currentSelected == newCard)
        {
            // 同じカードならトグル解除
            currentSelected.Deselect();
            currentSelected = null;
            selectedCardIndex = -1;
        }
        else
        {
            newCard.Select();
            currentSelected = newCard;
            selectedCardIndex = newCard.IndexInHand;
        }

        if (hanabiManager != null && selectedCardIndex >= 0)
        {
            hanabiManager.agents[hanabiManager.selfIndex].SelectCard(selectedCardIndex);
        }
    }
}
