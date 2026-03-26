using UnityEngine;
using TMPro;
using System;
using System.Text;
using System.Collections.Generic;

/// <summary>
/// Progressive incantation matcher: listens to real-time ASR input
/// and checks character-by-character against a preset spell text.
/// Visual feedback colors matched vs unmatched portions.
/// Design reference: Helldivers directional-key stratagem input.
/// </summary>
public class SpellChecker : MonoBehaviour
{
    #region Editor (Serialized)
    [SerializeField, TextArea(2,3)] private string incantation;
    [SerializeField] private string spellName;
    [SerializeField] private TextMeshProUGUI displayText;
    [SerializeField] private Color matchedColor = new Color(0.2f, 0.9f, 0.3f);
    [SerializeField] private Color unmatchedColor = Color.white;
    [SerializeField] private KeyCode listenKey = KeyCode.LeftControl;
    #endregion

    #region Private Variables
    private string cleanIncantation;
    private int[] cleanToOriginalIndex;
    private int matchedCount;
    private bool isListening;
    private string confirmedBuffer;
    #endregion

    #region Events
    public static event Action<string> EventSpellCompleted;
    public event Action<string> EventInstanceSpellCompleted;
    #endregion

    #region Public Functions
    public void StartListen()
    {
        if (isListening) return;
        isListening = true;
        matchedCount = 0;
        confirmedBuffer = "";
        PrepareIncantation();
        UpdateDisplay();
        ASRService.EventASRUpdated += HandleASRUpdated;
        ASRService.EventASRCompleted += HandleASRCompleted;
    }

    public void StopListen()
    {
        if (!isListening) return;
        isListening = false;
        ASRService.EventASRUpdated -= HandleASRUpdated;
        ASRService.EventASRCompleted -= HandleASRCompleted;
    }

    public void SetIncantation(string newIncantation, string newSpellName)
    {
        incantation = newIncantation;
        spellName = newSpellName;
        PrepareIncantation();
        matchedCount = 0;
        UpdateDisplay();
    }

    public float MatchProgress =>
        cleanIncantation != null && cleanIncantation.Length > 0
            ? (float)matchedCount / cleanIncantation.Length
            : 0f;

    public bool IsListening => isListening;
    #endregion

    #region Private Functions
    void Awake()
    {
        PrepareIncantation();
    }

    void OnEnable()
    {
        UpdateDisplay();
    }

    void OnDisable()
    {
        if (isListening) StopListen();
    }

    void Update()
    {
        if (Input.GetKeyDown(listenKey))
        {
            StartListen();
        }

        if (Input.GetKeyUp(listenKey))
        {
            StopListen();
        }
    }

    void PrepareIncantation()
    {
        if (string.IsNullOrEmpty(incantation))
        {
            cleanIncantation = "";
            cleanToOriginalIndex = Array.Empty<int>();
            return;
        }

        var sb = new StringBuilder();
        var mapping = new List<int>();
        for (int i = 0; i < incantation.Length; i++)
        {
            char c = incantation[i];
            if (!char.IsPunctuation(c) && !char.IsWhiteSpace(c))
            {
                sb.Append(c);
                mapping.Add(i);
            }
        }
        cleanIncantation = sb.ToString();
        cleanToOriginalIndex = mapping.ToArray();
    }

    void HandleASRUpdated(string text)
    {
        if (!isListening) return;
        string fullInput = confirmedBuffer + RemovePunctuation(text);
        ProcessInput(fullInput);
    }

    void HandleASRCompleted(string text)
    {
        if (!isListening) return;
        confirmedBuffer += RemovePunctuation(text);
        ProcessInput(confirmedBuffer);
    }

    void ProcessInput(string cleanInput)
    {
        int newCount = GetSubsequenceMatchCount(cleanInput, cleanIncantation, matchedCount);
        matchedCount = Mathf.Max(matchedCount, newCount);
        UpdateDisplay();

        if (matchedCount >= cleanIncantation.Length && cleanIncantation.Length > 0)
        {
            Debug.Log($"[SpellChecker] Incantation complete — spell: {spellName}");
            EventSpellCompleted?.Invoke(spellName);
            EventInstanceSpellCompleted?.Invoke(spellName);
            StopListen();
        }
    }

    void UpdateDisplay()
    {
        if (displayText == null) return;

        if (string.IsNullOrEmpty(incantation))
        {
            displayText.text = "";
            return;
        }

        string matchedHex = ColorUtility.ToHtmlStringRGB(matchedColor);
        string unmatchedHex = ColorUtility.ToHtmlStringRGB(unmatchedColor);

        if (matchedCount <= 0)
        {
            displayText.text = $"<color=#{unmatchedHex}>{incantation}</color>";
            return;
        }

        int splitIndex = matchedCount >= cleanIncantation.Length
            ? incantation.Length
            : cleanToOriginalIndex[matchedCount - 1] + 1;

        string matched = incantation.Substring(0, splitIndex);
        string unmatched = incantation.Substring(splitIndex);

        displayText.text = $"<color=#{matchedHex}>{matched}</color><color=#{unmatchedHex}>{unmatched}</color>";
    }
    #endregion

    #region Helper Functions
    static string RemovePunctuation(string text)
    {
        var sb = new StringBuilder(text.Length);
        foreach (char c in text)
        {
            if (!char.IsPunctuation(c) && !char.IsWhiteSpace(c))
                sb.Append(c);
        }
        return sb.ToString();
    }

    /// <summary>
    /// Subsequence match starting from a locked-in progress point.
    /// Scans input for remaining target chars from startFromTarget onward;
    /// already-matched characters are never re-evaluated, so ASR
    /// corrections cannot revert confirmed progress.
    /// </summary>
    static int GetSubsequenceMatchCount(string input, string target, int startFromTarget = 0)
    {
        int targetIndex = startFromTarget;
        for (int i = 0; i < input.Length && targetIndex < target.Length; i++)
        {
            if (char.ToLowerInvariant(input[i]) == char.ToLowerInvariant(target[targetIndex]))
                targetIndex++;
        }
        return targetIndex;
    }
    #endregion
}
