using UnityEngine;
using System;
using System.Collections.Generic;

public class SpellCaster : MonoBehaviour
{
    #region Editor (Serialized)
    [SerializeField] private SpellDefinition spellDefinition;
    [SerializeField, SpellName] private string[] enabledSpells;
    #endregion

    #region Private Variables
    private readonly Dictionary<string, SpellRuntimeState> runtimeStates = new Dictionary<string, SpellRuntimeState>();
    #endregion

    #region Events
    public event Action<string> EventSpellCast;
    #endregion

    #region Private Functions
    void OnEnable()
    {
        SpellModule.EventKeywordDetected += HandleSpellDetected;
        SpellModuleRealtime.EventKeywordDetected += HandleSpellDetected;
    }

    void OnDisable()
    {
        SpellModule.EventKeywordDetected -= HandleSpellDetected;
        SpellModuleRealtime.EventKeywordDetected -= HandleSpellDetected;
    }

    void Start()
    {
        InitializeRuntimeStates();
    }

    void Update()
    {
        RechargeStashes();
    }

    void InitializeRuntimeStates()
    {
        runtimeStates.Clear();
        if (spellDefinition == null || enabledSpells == null) return;

        foreach (string spellName in enabledSpells)
        {
            if (string.IsNullOrEmpty(spellName)) continue;
            SpellData? data = spellDefinition.GetSpell(spellName);
            if (!data.HasValue) continue;

            runtimeStates[spellName] = new SpellRuntimeState
            {
                cooldown = data.Value.cooldown,
                maxStash = data.Value.maxStash,
                currentStash = data.Value.maxStash,
                rechargeTimer = 0f
            };
        }
    }

    void RechargeStashes()
    {
        foreach (var kvp in runtimeStates)
        {
            SpellRuntimeState state = kvp.Value;
            if (state.currentStash >= state.maxStash) continue;

            state.rechargeTimer -= Time.deltaTime;
            if (state.rechargeTimer <= 0f)
            {
                state.currentStash++;
                if (state.currentStash < state.maxStash)
                    state.rechargeTimer = state.cooldown;
            }
        }
    }

    void HandleSpellDetected(string keyword)
    {
        if (!runtimeStates.TryGetValue(keyword, out SpellRuntimeState state)) return;
        if (state.currentStash <= 0)
        {
            Debug.Log($"[SpellCaster] {keyword} on cooldown ({state.rechargeTimer:F1}s remaining)");
            return;
        }

        state.currentStash--;
        if (state.currentStash < state.maxStash && state.rechargeTimer <= 0f)
            state.rechargeTimer = state.cooldown;

        SpellData? data = spellDefinition.GetSpell(keyword);
        if (data.HasValue && data.Value.spellPrefab != null)
        {
            Instantiate(data.Value.spellPrefab, transform.position, transform.rotation);
            Debug.Log($"[SpellCaster] Cast {keyword} (stash: {state.currentStash}/{state.maxStash})");
        }

        EventSpellCast?.Invoke(keyword);
    }
    #endregion

    #region Helper Functions
    private class SpellRuntimeState
    {
        public float cooldown;
        public int maxStash;
        public int currentStash;
        public float rechargeTimer;
    }
    #endregion
}
