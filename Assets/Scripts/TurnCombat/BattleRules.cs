using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Stateless utility methods for battle rule calculations.
/// All methods are pure functions with no side effects on external state.
/// </summary>
public static class BattleRules
{
    public static bool ShouldPlayerGoFirst(MoveData playerMove, MoveData enemyMove,
                                           Monster player, Monster enemy)
    {
        if (playerMove == null || enemyMove == null) return true;
        if (playerMove.Priority != enemyMove.Priority)
            return playerMove.Priority > enemyMove.Priority;

        int playerSpeed = player.GetStat(StatType.Speed);
        int enemySpeed  = enemy.GetStat(StatType.Speed);

        if (player.Status == StatusCondition.Paralysis) playerSpeed /= 2;
        if (enemy.Status  == StatusCondition.Paralysis) enemySpeed  /= 2;

        if (playerSpeed == enemySpeed) return Random.value > 0.5f;
        return playerSpeed > enemySpeed;
    }

    public static int FallbackAIMove(Monster monster)
    {
        var moves = monster.Moves;
        var valid = new List<int>();
        for (int i = 0; i < moves.Length; i++)
        {
            if (moves[i] != null && moves[i].HasPP)
                valid.Add(i);
        }
        return valid.Count == 0 ? 0 : valid[Random.Range(0, valid.Count)];
    }

    public static int ClampMoveIndex(Monster m, int index)
    {
        if (m.Moves == null) return 0;
        if (index < 0 || index >= m.Moves.Length || m.Moves[index] == null || !m.Moves[index].HasPP)
            return FallbackAIMove(m);
        return index;
    }

    public static BattleContext BuildBattleContext(BattleSessionData session, int maxChatHistory = -1)
    {
        var player = session.PlayerMonster;
        var enemy  = session.EnemyMonster;

        var ctx = new BattleContext
        {
            playerName      = player.Data.MonsterName,
            playerLevel     = player.Level,
            playerCurrentHp = player.CurrentHp,
            playerMaxHp     = player.MaxHp,
            playerType      = player.Data.PrimaryType.ToString(),
            enemyName       = enemy.Data.MonsterName,
            enemyLevel      = enemy.Level,
            enemyCurrentHp  = enemy.CurrentHp,
            enemyMaxHp      = enemy.MaxHp,
            enemyType       = enemy.Data.PrimaryType.ToString(),
            enemyPersonality = enemy.Data.Personality,
            playerChatMessage = session.ChatMessageThisTurn,
            battleHistory   = session.BattleLog.Count > 0 ? string.Join("\n", session.BattleLog) : null,
            chatHistory     = BuildChatHistoryString(session.ChatHistory, enemy.Data.MonsterName, maxChatHistory)
        };

        var moves = enemy.Moves;
        int count = 0;
        foreach (var m in moves) { if (m != null) count++; }

        ctx.moveNames  = new string[count];
        ctx.movePowers = new int[count];
        ctx.moveTypes  = new string[count];
        ctx.movePPs    = new int[count];

        int idx = 0;
        for (int i = 0; i < moves.Length; i++)
        {
            if (moves[i] == null) continue;
            ctx.moveNames[idx]  = moves[i].Data.MoveName;
            ctx.movePowers[idx] = moves[i].Data.Power;
            ctx.moveTypes[idx]  = moves[i].Data.MoveType.ToString();
            ctx.movePPs[idx]    = moves[i].CurrentPP;
            idx++;
        }

        return ctx;
    }

    public static string GetStatusName(StatusCondition condition)
    {
        return condition switch
        {
            StatusCondition.Poison     => "was poisoned",
            StatusCondition.Burn       => "was burned",
            StatusCondition.Paralysis  => "was paralyzed",
            StatusCondition.Sleep      => "fell asleep",
            StatusCondition.Freeze     => "was frozen",
            StatusCondition.Vulnerable => "became vulnerable",
            _ => ""
        };
    }

    public static string GetStatName(StatType stat)
    {
        return stat switch
        {
            StatType.Attack    => "Attack",
            StatType.Defense   => "Defense",
            StatType.SpAttack  => "Sp. Atk",
            StatType.SpDefense => "Sp. Def",
            StatType.Speed     => "Speed",
            _ => ""
        };
    }

    private static string BuildChatHistoryString(List<ChatExchange> history, string monsterName, int maxRecords)
    {
        if (history == null || history.Count == 0) return null;
        var lines = new List<string>();
        int startIdx = maxRecords > 0 ? Mathf.Max(0, history.Count - maxRecords) : 0;
        for (int i = startIdx; i < history.Count; i++)
        {
            var ex = history[i];
            lines.Add($"训练师: \"{ex.playerMessage}\"");
            lines.Add($"{monsterName}: \"{ex.monsterResponse}\"");
        }
        return string.Join("\n", lines);
    }
}
