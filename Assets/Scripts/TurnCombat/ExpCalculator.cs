using UnityEngine;

public static class ExpCalculator
{
    public static int GetExpGain(MonsterData defeatedMonster, int defeatedLevel)
    {
        return Mathf.FloorToInt((defeatedMonster.BaseExpYield * defeatedLevel) / 7f);
    }
}
