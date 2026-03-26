using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class Monster
{
    #region Private Variables
    private MonsterData data;
    private int level;
    private int exp;
    private int currentHp;
    private StatusCondition status;
    private int statusTurns;
    private MoveSlot[] moves;
    private Dictionary<StatType, int> statStages;
    #endregion

    #region Public Functions
    public MonsterData Data => data;
    public int Level => level;
    public int Exp => exp;
    public int CurrentHp => currentHp;
    public StatusCondition Status => status;
    public int StatusTurns => statusTurns;
    public MoveSlot[] Moves => moves;
    public bool IsFainted => currentHp <= 0;

    public Monster(MonsterData data, int level)
    {
        this.data = data;
        this.level = level;
        this.exp = data.GetExpForLevel(level);
        this.status = StatusCondition.None;
        this.statusTurns = 0;

        statStages = new Dictionary<StatType, int>
        {
            { StatType.Attack, 0 },
            { StatType.Defense, 0 },
            { StatType.SpAttack, 0 },
            { StatType.SpDefense, 0 },
            { StatType.Speed, 0 }
        };

        InitMoves();
        currentHp = MaxHp;
    }

    public int MaxHp => Mathf.FloorToInt((2 * data.BaseStats.hp * level) / 100f) + level + 10;

    public int GetStat(StatType statType)
    {
        int baseStat = GetBaseStat(statType);
        int raw = Mathf.FloorToInt((2 * baseStat * level) / 100f) + 5;
        float stageMultiplier = GetStageMultiplier(statStages[statType]);
        return Mathf.Max(1, Mathf.FloorToInt(raw * stageMultiplier));
    }

    public int GetStatStage(StatType statType)
    {
        return statStages.TryGetValue(statType, out int stage) ? stage : 0;
    }

    public int ApplyStatStageChange(StatType statType, int stages)
    {
        int oldStage = statStages[statType];
        statStages[statType] = Mathf.Clamp(oldStage + stages, -6, 6);
        return statStages[statType] - oldStage;
    }

    public void ResetStatStages()
    {
        foreach (StatType stat in statStages.Keys)
            statStages[stat] = 0;
    }

    public void TakeDamage(int damage)
    {
        currentHp = Mathf.Max(0, currentHp - damage);
    }

    public void Heal(int amount)
    {
        currentHp = Mathf.Min(MaxHp, currentHp + amount);
    }

    public void ApplyStatus(StatusCondition condition)
    {
        if (status != StatusCondition.None) return;
        status = condition;
        statusTurns = 0;
    }

    public void CureStatus()
    {
        status = StatusCondition.None;
        statusTurns = 0;
    }

    public void IncrementStatusTurns()
    {
        statusTurns++;
    }

    public int AddExp(int gained)
    {
        exp += gained;
        int oldLevel = level;
        while (level < 100 && exp >= data.GetExpForLevel(level + 1))
            level++;

        if (level > oldLevel)
        {
            int oldMaxHp = Mathf.FloorToInt((2 * data.BaseStats.hp * oldLevel) / 100f) + oldLevel + 10;
            int hpGain = MaxHp - oldMaxHp;
            currentHp = Mathf.Min(currentHp + hpGain, MaxHp);
        }

        return level - oldLevel;
    }

    public List<MoveData> GetNewMovesForCurrentLevel()
    {
        var newMoves = new List<MoveData>();
        if (data.LearnableMoves == null) return newMoves;
        foreach (var lm in data.LearnableMoves)
        {
            if (lm.levelRequired == level && lm.move != null)
                newMoves.Add(lm.move);
        }
        return newMoves;
    }

    public bool LearnMove(MoveData moveData, int slotIndex = -1)
    {
        if (slotIndex < 0)
        {
            for (int i = 0; i < moves.Length; i++)
            {
                if (moves[i] == null)
                {
                    moves[i] = new MoveSlot(moveData);
                    return true;
                }
            }
            return false;
        }

        if (slotIndex >= 0 && slotIndex < moves.Length)
        {
            moves[slotIndex] = new MoveSlot(moveData);
            return true;
        }
        return false;
    }

    public void FullRestore()
    {
        currentHp = MaxHp;
        CureStatus();
        ResetStatStages();
        foreach (var move in moves)
            move?.RestoreAllPP();
    }
    #endregion

    #region Private Functions
    private void InitMoves()
    {
        moves = new MoveSlot[4];
        if (data.LearnableMoves == null) return;

        var available = new List<LearnableMove>();
        foreach (var lm in data.LearnableMoves)
        {
            if (lm.levelRequired <= level && lm.move != null)
                available.Add(lm);
        }

        // Take the last 4 (highest level) moves
        int start = Mathf.Max(0, available.Count - 4);
        for (int i = start; i < available.Count; i++)
            moves[i - start] = new MoveSlot(available[i].move);
    }

    private int GetBaseStat(StatType statType)
    {
        return statType switch
        {
            StatType.Attack => data.BaseStats.attack,
            StatType.Defense => data.BaseStats.defense,
            StatType.SpAttack => data.BaseStats.spAttack,
            StatType.SpDefense => data.BaseStats.spDefense,
            StatType.Speed => data.BaseStats.speed,
            _ => 0
        };
    }

    private float GetStageMultiplier(int stage)
    {
        if (stage >= 0)
            return (2f + stage) / 2f;
        else
            return 2f / (2f - stage);
    }
    #endregion
}
