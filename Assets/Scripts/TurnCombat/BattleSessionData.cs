using System.Collections.Generic;

/// <summary>
/// Pure data container for all mutable battle runtime state.
/// No MonoBehaviour — instantiated at battle start, discarded at battle end.
/// </summary>
public class BattleSessionData
{
    public Monster[] PlayerParty;
    public int CurrentMonsterIndex;
    public Monster EnemyMonster;
    public BattleState State;

    public bool HasChatThisTurn;
    public string ChatMessageThisTurn;

    public int PendingEnemyMoveIndex;
    public bool WaitingForLLM;
    public int IntentMoveIndex = -1;
    public bool IntentReady;

    public float AccumulatedCatchBonus;

    public int TurnNumber;
    public List<string> BattleLog = new List<string>();
    public List<ChatExchange> ChatHistory = new List<ChatExchange>();

    public void LogEvent(string eventText)
    {
        BattleLog.Add($"[回合{TurnNumber}] {eventText}");
    }

    public Monster PlayerMonster => PlayerParty[CurrentMonsterIndex];

    public int FindFirstAliveIndex()
    {
        for (int i = 0; i < PlayerParty.Length; i++)
        {
            if (PlayerParty[i] != null && !PlayerParty[i].IsFainted)
                return i;
        }
        return -1;
    }

    public void ResetTurnChat()
    {
        HasChatThisTurn = false;
        ChatMessageThisTurn = null;
    }
}

[System.Serializable]
public struct ChatExchange
{
    public string playerMessage;
    public string monsterResponse;
}
