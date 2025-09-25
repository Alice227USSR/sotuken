using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;

public class UIManager : MonoBehaviour
{
    public TextMeshProUGUI hintText;
    public TextMeshProUGUI lifeText;
    public TextMeshProUGUI scoreText;
    public TMP_Dropdown targetPlayerDropdown;
    public TMP_Dropdown hintInfoDropdown;
    public Button playButton;   // ← 追加
    public Button giveHintButton;
    public Button discardButton;

    private HanabiManager hanabiManager;

    void Start()
    {
        if (discardButton) discardButton.onClick.AddListener(OnDiscardButtonPressed);
        if (playButton) playButton.onClick.AddListener(OnPlayButtonPressed); // ← 追加
    }

    public void Initialize(HanabiManager manager)
    {
        hanabiManager = manager;
        if (giveHintButton) giveHintButton.onClick.AddListener(OnGiveHintPressed);
        if (playButton) playButton.onClick.AddListener(OnPlayButtonPressed); // ← 追加
        PopulateHintDropdowns();
    }

    void OnGiveHintPressed()
    {
        int targetPlayer = targetPlayerDropdown.value;
        string hint = hintInfoDropdown.options[hintInfoDropdown.value].text;
        hanabiManager.ExecuteHintFromUI(targetPlayer, hint);
    }

    public void UpdateHint(int hints) { hintText.text = $"Hints: {hints}"; }
    public void UpdateLife(int lives) { lifeText.text = $"Lives: {lives}"; }
    public void UpdateScore(int score) { scoreText.text = $"Score: {score}"; }

    public void PopulateHintDropdowns()
    {
        targetPlayerDropdown.ClearOptions();
        hintInfoDropdown.ClearOptions();

        var playerOptions = new List<string>();
        for (int i = 0; i < hanabiManager.numPlayers; i++)
            if (i != hanabiManager.selfIndex) playerOptions.Add($"Player {i}");
        targetPlayerDropdown.AddOptions(playerOptions);

        var hintOptions = new List<string>();
        foreach (var c in HanabiSpec.ColorOrder) hintOptions.Add(c.ToString()); // R,Y,G,W,B
        hintOptions.AddRange(new[] { "1", "2", "3", "4", "5" });
        hintInfoDropdown.AddOptions(hintOptions);

        // 文字が消える問題の回避（テーマ上書き防止）
        if (hintInfoDropdown.captionText) hintInfoDropdown.captionText.color = Color.black;
        if (targetPlayerDropdown.captionText) targetPlayerDropdown.captionText.color = Color.black;
        foreach (var txt in hintInfoDropdown.GetComponentsInChildren<TextMeshProUGUI>(true)) txt.color = Color.black;
        foreach (var txt in targetPlayerDropdown.GetComponentsInChildren<TextMeshProUGUI>(true)) txt.color = Color.black;

        hintInfoDropdown.RefreshShownValue();
        targetPlayerDropdown.RefreshShownValue();
    }

    void OnDiscardButtonPressed()
    {
        hanabiManager.agents[hanabiManager.selfIndex].DiscardSelectedCard();
    }
    // UIManager.cs に追加
    void OnPlayButtonPressed()
    {
        if (hanabiManager != null)
            hanabiManager.PlaySelectedCardFromUI();
    }

}
