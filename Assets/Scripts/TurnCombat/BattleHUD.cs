using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class BattleHUD : MonoBehaviour
{
    #region Editor (Serialized)
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI hpText;
    [SerializeField] private Slider hpBar;
    [SerializeField] private Slider expBar;
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private TextMeshProUGUI intentText;
    [SerializeField] private TextMeshProUGUI catchRateText; // Optional
    [SerializeField] private float hpAnimSpeed = 2f;
    #endregion

    #region Private Variables
    private Monster monster;
    #endregion

    #region Public Functions
    public void SetData(Monster monster)
    {
        this.monster = monster;
        nameText.text = monster.Data.MonsterName;
        hpBar.maxValue = monster.MaxHp;
        hpBar.value = monster.CurrentHp;
        UpdateHPText();
        UpdateStatus(monster.Status);
        UpdateExpBar();
    }

    public Coroutine AnimateHP(int targetHp)
    {
        return StartCoroutine(AnimateHPCoroutine(targetHp));
    }

    public void UpdateStatus(StatusCondition condition)
    {
        if (statusText == null) return;
        if (condition == StatusCondition.None)
        {
            statusText.text = "";
            return;
        }
        statusText.text = condition switch
        {
            StatusCondition.Poison => "PSN",
            StatusCondition.Burn => "BRN",
            StatusCondition.Paralysis => "PAR",
            StatusCondition.Sleep => "SLP",
            StatusCondition.Freeze => "FRZ",
            StatusCondition.Vulnerable => "VUL",
            _ => ""
        };
    }

    public void UpdateExpBar()
    {
        if (expBar == null || monster == null) return;
        int currentLevelExp = monster.Data.GetExpForLevel(monster.Level);
        int nextLevelExp = monster.Data.GetExpForLevel(monster.Level + 1);
        float normalized = (float)(monster.Exp - currentLevelExp) / (nextLevelExp - currentLevelExp);
        expBar.value = Mathf.Clamp01(normalized);
    }

    public void UpdateCatchRate(float probability)
    {
        if (catchRateText == null && nameText != null)
        {
            var go = new GameObject("CatchRateText");
            go.transform.SetParent(nameText.transform.parent, false);
            catchRateText = go.AddComponent<TextMeshProUGUI>();
            catchRateText.font = nameText.font;
            catchRateText.fontSize = nameText.fontSize * 0.8f;
            catchRateText.color = nameText.color;
            catchRateText.alignment = TextAlignmentOptions.Right;
            var rect = catchRateText.rectTransform;
            rect.anchorMin = new Vector2(1, 1);
            rect.anchorMax = new Vector2(1, 1);
            rect.pivot = new Vector2(1, 1);
            rect.anchoredPosition = new Vector2(0, -30);
            rect.sizeDelta = new Vector2(200, 30);
        }
        
        if (catchRateText != null)
        {
            catchRateText.text = $"Catch: {probability:P0}";
            catchRateText.gameObject.SetActive(true);
        }
    }

    public void RefreshLevel()
    {
        if (monster == null) return;
        hpBar.maxValue = monster.MaxHp;
    }

    public void SetIntent(string moveName)
    {
        if (intentText == null) return;
        intentText.text = moveName == null ? "Intent: Thinking..." : $"Intent: {moveName}";
        intentText.gameObject.SetActive(true);
    }

    public void ClearIntent()
    {
        if (intentText == null) return;
        intentText.gameObject.SetActive(false);
    }
    #endregion

    #region Private Functions
    private IEnumerator AnimateHPCoroutine(int targetHp)
    {
        float current = hpBar.value;
        float target = targetHp;
        while (!Mathf.Approximately(current, target))
        {
            current = Mathf.MoveTowards(current, target, hpAnimSpeed * Time.deltaTime * monster.MaxHp);
            hpBar.value = current;
            UpdateHPText();
            yield return null;
        }
        hpBar.value = target;
        UpdateHPText();
    }

    private void UpdateHPText()
    {
        if (hpText == null) return;
        hpText.text = $"{Mathf.CeilToInt(hpBar.value)}/{monster.MaxHp}";
    }
    #endregion
}
