using UnityEngine;
using TMPro;

public class CardView : MonoBehaviour
{
    public TextMeshProUGUI numberText;
    public SpriteRenderer background;

    private int indexInHand;
    private System.Action<int> onSelectedCallback;
    private bool isSelected = false;

    public int IndexInHand => indexInHand;

    public void Set(
        Card card,
        CardKnowledge knowledge,
        bool showFace = true,
        int index = -1,
        System.Action<int> onSelected = null
    )
    {
        indexInHand = index;
        onSelectedCallback = onSelected;
        isSelected = false;

        var canvas = GetComponentInChildren<Canvas>();
        if (canvas != null)
        {
            // 文字が上下逆に見えるケースへの対処（元プロジェクトの流儀）
            canvas.transform.localRotation = Quaternion.Euler(0f, 0f, 180f);
            canvas.transform.localPosition = new Vector3(0f, 24.4f, -0.6f);
        }

        if (showFace)
        {
            // 数字＆色（数字に色を付ける本体）
            numberText.text  = card.number.ToString();
            numberText.color = ColorFromString(card.color);
            background.color = Color.white;

            // TMP側の上書きを防止
            numberText.enableVertexGradient = false;
            numberText.alpha = 1f;
        }
        else
        {
            // 裏向き表示：知識が単一確定なら背景色で示す
            bool colorKnown  = IsSingleTrue(knowledge.possibleColors, out int colorIndex);
            bool numberKnown = IsSingleTrue(knowledge.possibleNumbers, out int numberIndex);

            numberText.text  = numberKnown ? (numberIndex + 1).ToString() : "?";
            numberText.color = Color.black;
            background.color = colorKnown ? ColorFromIndex(colorIndex) : Color.gray;
        }
    }

    // クリックで選択通知（CardSelectionManager に渡す）
    void OnMouseDown()
    {
        CardSelectionManager.Instance.Select(this);
    }

    // ===== ここから「他クラスが呼ぶ想定のAPI」 =====

    // CardDisplayManager から呼ばれる（index と callback を後付け）
    public void SetIndexAndCallback(int index, System.Action<int> callback)
    {
        indexInHand = index;
        onSelectedCallback = callback;
    }

    // CardSelectionManager から呼ばれる
    public void Select()
    {
        if (isSelected) return;
        isSelected = true;
        // 視覚的に少し持ち上げる
        transform.localPosition += new Vector3(0f, 0.3f, 0f);
        // 必要ならコールバック
        onSelectedCallback?.Invoke(indexInHand);
    }

    // CardSelectionManager から呼ばれる
    public void Deselect()
    {
        if (!isSelected) return;
        isSelected = false;
        transform.localPosition -= new Vector3(0f, 0.3f, 0f);
    }

    // ===== 補助関数 =====

    bool IsSingleTrue(bool[] array, out int index)
    {
        index = -1;
        int count = 0;
        for (int i = 0; i < array.Length; i++)
        {
            if (array[i]) { index = i; count++; }
        }
        return count == 1;
    }

    Color ColorFromString(string color)
    {
        // "R"/"r"/"red" → 'R' に正規化してから色決定
        char c = HanabiSpec.NormalizeColorString(color);
        return c switch
        {
            'R' => Color.red,
            'Y' => Color.yellow,
            'G' => Color.green,
            'W' => Color.white,
            'B' => Color.blue,
            _   => Color.gray
        };
    }

    Color ColorFromIndex(int index)
    {
        return index switch
        {
            0 => Color.red,     // R
            1 => Color.yellow,  // Y
            2 => Color.green,   // G
            3 => Color.white,   // W
            4 => Color.blue,    // B
            _ => Color.gray
        };
    }
}
