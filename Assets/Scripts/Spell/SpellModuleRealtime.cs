using UnityEngine;
using System;
using System.Collections.Generic;
using System.Text;

/// <summary>
/// Listen to Realtime ASR result and detect spells. Independent of LLM.
/// </summary>
public class SpellModuleRealtime : MonoBehaviour
{
    [SerializeField] private SpellBook spellBook;

    public static event Action<string> EventKeywordDetected;
    public event Action<string> EventInstanceKeywordDetected;

    private readonly Dictionary<string, int> triggerCounts = new Dictionary<string, int>();
    private int lastCleanedLength;

    void OnEnable()
    {
        ASRService.EventASRUpdated += HandleASRUpdated;
        ASRService.EventASRCompleted += HandleASRCompleted;
    }

    void OnDisable()
    {
        ASRService.EventASRUpdated -= HandleASRUpdated;
        ASRService.EventASRCompleted -= HandleASRCompleted;
    }

    void HandleASRCompleted(string text)
    {
        DetectSpells(text);
        ResetState();
    }

    void HandleASRUpdated(string text)
    {
        string cleaned = RemovePunctuation(text);

        if (cleaned.Length < lastCleanedLength)
            ResetState();

        lastCleanedLength = cleaned.Length;
        DetectSpells(cleaned, skipRemovePunctuation: true);
    }

    void DetectSpells(string text, bool skipRemovePunctuation = false)
    {
        string cleaned = skipRemovePunctuation ? text : RemovePunctuation(text);

        foreach (var spellEntry in spellBook.SpellEntries)
        {
            int currentCount = CountOccurrences(cleaned, spellEntry.triggerWord);

            int previousCount;
            if (!triggerCounts.TryGetValue(spellEntry.triggerWord, out previousCount))
                previousCount = 0;

            if (currentCount > previousCount)
            {
                Debug.Log($"[SpellModuleRealtime] Spell Detected: {spellEntry.triggerWord} (x{currentCount - previousCount})");
                for (int i = 0; i < currentCount - previousCount; i++)
                {
                    EventKeywordDetected?.Invoke(spellEntry.triggerWord);
                    EventInstanceKeywordDetected?.Invoke(spellEntry.triggerWord);
                }
                triggerCounts[spellEntry.triggerWord] = currentCount;
            }
        }
    }

    void ResetState()
    {
        triggerCounts.Clear();
        lastCleanedLength = 0;
    }

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

    static int CountOccurrences(string text, string word)
    {
        if (string.IsNullOrEmpty(word)) return 0;
        int count = 0;
        int index = 0;
        while ((index = text.IndexOf(word, index, StringComparison.Ordinal)) != -1)
        {
            count++;
            index += word.Length;
        }
        return count;
    }
}
