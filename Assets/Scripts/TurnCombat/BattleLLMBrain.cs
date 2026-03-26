using System;
using System.Text;
using UnityEngine;
using LLMUnity;

public class BattleLLMBrain : MonoBehaviour
{
    #region Editor (Serialized)
    [SerializeField] private LLMAgent aiAgent;
    [SerializeField] private LLMAgent chatAgent;
    #endregion

    #region Events
    public event Action<int> EventAIActionDecided;
    public event Action<int> EventIntentDecided;
    public event Action<ChatBuffResult> EventChatResponseReceived;
    #endregion

    #region Private Variables
    private string pendingResponse;
    private string pendingIntentResponse;
    #endregion

    #region Public Functions
    public void Init()
    {
        if (aiAgent != null)
        {
            aiAgent.llm = LLMManager.LLMInstance;
            aiAgent.systemPrompt = BuildAISystemPrompt(aiAgent.systemPrompt);
            aiAgent.Warmup();
        }
        if (chatAgent != null)
        {
            chatAgent.llm = LLMManager.LLMInstance;
            chatAgent.Warmup();
        }
    }

    public void RequestAIAction(BattleContext context)
    {
        if (aiAgent == null)
        {
            EventAIActionDecided?.Invoke(0);
            return;
        }

        string prompt = BuildAIPrompt(context);
        aiAgent.Chat(prompt, OnAICallback, OnAICompleted);
    }

    public void RequestIntent(BattleContext context)
    {
        if (aiAgent == null)
        {
            EventIntentDecided?.Invoke(0);
            return;
        }

        string prompt = BuildAIPrompt(context);
        aiAgent.Chat(prompt, OnIntentCallback, OnIntentCompleted);
    }

    public void RequestChatResponse(string playerMessage, Monster enemyMonster, Monster playerMonster)
    {
        if (chatAgent == null)
        {
            var fallback = new ChatBuffResult { response = "...", buffTarget = "player", statType = "Attack", stages = 0 };
            EventChatResponseReceived?.Invoke(fallback);
            return;
        }

        string prompt = BuildChatPrompt(playerMessage, enemyMonster, playerMonster);
        chatAgent.Chat(prompt, OnChatCallback, OnChatCompleted);
    }

    public void SetEnemyPersonality(string monsterName, ElementType type, string personality)
    {
        if (chatAgent == null) return;
        string personalityClause = string.IsNullOrWhiteSpace(personality)
            ? ""
            : $"你的性格是：{personality}。请始终以这种性格来回应。";
        chatAgent.systemPrompt =
            $"你是一只名为 {monsterName} 的 {type} 属性怪兽，正在和训练师的怪兽战斗。" +
            personalityClause +
            "训练师会尝试和你对话。你需要以怪兽的身份用简短的话回应，并根据对话内容决定给予buff或debuff。" +
            "你的回复必须是严格的JSON格式，不要包含其他文字：\n" +
            "{\"response\": \"你的回应内容\", \"buffTarget\": \"player或enemy\", \"statType\": \"Attack/Defense/SpAttack/SpDefense/Speed\", \"stages\": 数字(-2到2)}\n" +
            "如果训练师说了讨好你或有趣的话，你可以给player加buff(stages为正数)。" +
            "如果训练师说了冒犯的话，给player加debuff(stages为负数)或给enemy加buff。" +
            "如果对话平淡，stages设为0。";
    }
    #endregion

    #region Private Functions
    private string BuildAISystemPrompt(string personality)
    {
        var sb = new StringBuilder();
        sb.AppendLine("你是战斗AI。根据当前战斗状态选择最佳招式。只返回一个JSON，不要其他文字。");
        sb.AppendLine("返回格式: {\"moveIndex\": 数字}");
        if (!string.IsNullOrWhiteSpace(personality))
        {
            sb.AppendLine($"额外指导: {personality}");
        }
        return sb.ToString();
    }

    private string BuildAIPrompt(BattleContext context)
    {
        var sb = new StringBuilder();
        sb.AppendLine("你是战斗AI。根据当前战斗状态选择最佳招式。只返回一个JSON，不要其他文字。");
        sb.AppendLine($"我方: {context.enemyName} Lv{context.enemyLevel} HP:{context.enemyCurrentHp}/{context.enemyMaxHp} 属性:{context.enemyType}");
        if (!string.IsNullOrEmpty(context.enemyPersonality))
            sb.AppendLine($"我方性格: {context.enemyPersonality}");
        sb.AppendLine($"对手: {context.playerName} Lv{context.playerLevel} HP:{context.playerCurrentHp}/{context.playerMaxHp} 属性:{context.playerType}");
        sb.Append("可用招式: ");
        for (int i = 0; i < context.moveNames.Length; i++)
        {
            if (!string.IsNullOrEmpty(context.moveNames[i]))
                sb.Append($"[{i}]{context.moveNames[i]}(威力:{context.movePowers[i]},属性:{context.moveTypes[i]},PP:{context.movePPs[i]}) ");
        }
        sb.AppendLine();
        if (!string.IsNullOrEmpty(context.playerChatMessage))
        {
            sb.AppendLine($"本回合训练师对你说了: \"{context.playerChatMessage}\"");
        }
        sb.AppendLine("返回格式: {\"moveIndex\": 数字}");
        return sb.ToString();
    }

    private string BuildChatPrompt(string playerMessage, Monster enemyMonster, Monster playerMonster)
    {
        var sb = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(enemyMonster.Data.Personality))
            sb.AppendLine($"你的性格: {enemyMonster.Data.Personality}");
        sb.AppendLine($"训练师对你说: \"{playerMessage}\"");
        sb.AppendLine($"你({enemyMonster.Data.MonsterName})当前状态: HP {enemyMonster.CurrentHp}/{enemyMonster.MaxHp}");
        sb.AppendLine($"对方({playerMonster.Data.MonsterName})当前状态: HP {playerMonster.CurrentHp}/{playerMonster.MaxHp}");
        sb.AppendLine("请以JSON格式回复。");
        return sb.ToString();
    }

    private void OnAICallback(string msg)
    {
        pendingResponse = msg;
    }

    private void OnAICompleted()
    {
        int moveIndex = 0;
        try
        {
            var parsed = JsonUtility.FromJson<AIActionResponse>(pendingResponse);
            moveIndex = parsed.moveIndex;
        }
        catch
        {
            Debug.LogWarning($"[BattleLLM] Failed to parse AI response: {pendingResponse}");
        }
        EventAIActionDecided?.Invoke(moveIndex);
    }

    private void OnIntentCallback(string msg)
    {
        pendingIntentResponse = msg;
    }

    private void OnIntentCompleted()
    {
        int moveIndex = 0;
        try
        {
            var parsed = JsonUtility.FromJson<AIActionResponse>(pendingIntentResponse);
            moveIndex = parsed.moveIndex;
        }
        catch
        {
            Debug.LogWarning($"[BattleLLM] Failed to parse intent response: {pendingIntentResponse}");
        }
        EventIntentDecided?.Invoke(moveIndex);
    }

    private void OnChatCallback(string msg)
    {
        pendingResponse = msg;
    }

    private void OnChatCompleted()
    {
        var result = new ChatBuffResult { response = "...", buffTarget = "player", statType = "Attack", stages = 0 };
        try
        {
            result = JsonUtility.FromJson<ChatBuffResult>(pendingResponse);
        }
        catch
        {
            Debug.LogWarning($"[BattleLLM] Failed to parse chat response: {pendingResponse}");
        }
        EventChatResponseReceived?.Invoke(result);
    }
    #endregion
}

#region Helper Types
[System.Serializable]
public struct BattleContext
{
    public string playerName;
    public int playerLevel;
    public int playerCurrentHp;
    public int playerMaxHp;
    public string playerType;
    public string enemyName;
    public int enemyLevel;
    public int enemyCurrentHp;
    public int enemyMaxHp;
    public string enemyType;
    public string enemyPersonality;
    public string[] moveNames;
    public int[] movePowers;
    public string[] moveTypes;
    public int[] movePPs;
    public string playerChatMessage;
}

[System.Serializable]
public class AIActionResponse
{
    public int moveIndex;
}

[System.Serializable]
public class ChatBuffResult
{
    public string response;
    public string buffTarget;
    public string statType;
    public int stages;
}
#endregion
