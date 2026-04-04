using UnityEngine;

public struct CatchResult
{
    public bool success;
    public int shakeCount;
}

public static class CatchCalculator
{
    public static float GetCatchProbability(Monster target, float chatBonus = 0f)
    {
        MonsterData data = target.Data;
        float catchRate = data.CatchRate;

        float statusMultiplier = target.Status switch
        {
            StatusCondition.Sleep    => data.SleepCatchMultiplier,
            StatusCondition.Freeze   => data.FreezeCatchMultiplier,
            StatusCondition.Paralysis => data.ParalysisCatchMultiplier,
            StatusCondition.Poison   => data.PoisonCatchMultiplier,
            StatusCondition.Burn     => data.BurnCatchMultiplier,
            _ => 1f
        };

        float clampedBonus = Mathf.Clamp(chatBonus, data.ChatCatchBonusMin, data.ChatCatchBonusMax);
        float modifiedRate = (catchRate + clampedBonus) * statusMultiplier;

        // Using modifiedRate as a direct percentage (out of 100)
        return Mathf.Clamp01(modifiedRate / 100f);
    }

    public static CatchResult TryCatch(Monster target, float chatBonus = 0f)
    {
        MonsterData data = target.Data;
        float catchRate = data.CatchRate;

        float statusMultiplier = target.Status switch
        {
            StatusCondition.Sleep    => data.SleepCatchMultiplier,
            StatusCondition.Freeze   => data.FreezeCatchMultiplier,
            StatusCondition.Paralysis => data.ParalysisCatchMultiplier,
            StatusCondition.Poison   => data.PoisonCatchMultiplier,
            StatusCondition.Burn     => data.BurnCatchMultiplier,
            _ => 1f
        };

        float clampedBonus = Mathf.Clamp(chatBonus, data.ChatCatchBonusMin, data.ChatCatchBonusMax);
        float modifiedRate = (catchRate + clampedBonus) * statusMultiplier;

        int requiredShakes = data.RequiredShakes;
        float finalProbability = Mathf.Clamp01(modifiedRate / 100f);
        
        Debug.LogWarning($"[Catch] modifiedRate: {modifiedRate} (base: {catchRate}, chatBonus: {chatBonus}, clamped: {clampedBonus}, statusMul: {statusMultiplier}, shakes: {requiredShakes}) -> Final Prob: {finalProbability:P}");

        var result = new CatchResult();

        if (finalProbability >= 1f)
        {
            result.success = true;
            result.shakeCount = requiredShakes;
            return result;
        }

        // To make the overall success exactly `finalProbability`, 
        // each independent shake must have probability `finalProbability^(1/shakes)`
        float shakeProbability = finalProbability > 0f ? Mathf.Pow(finalProbability, 1f / requiredShakes) : 0f;

        result.shakeCount = 0;
        for (int i = 0; i < requiredShakes; i++)
        {
            if (Random.value <= shakeProbability)
                result.shakeCount++;
            else
                break;
        }

        result.success = result.shakeCount >= requiredShakes;
        return result;
    }
}
