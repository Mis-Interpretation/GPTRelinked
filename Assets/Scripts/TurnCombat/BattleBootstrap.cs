using UnityEngine;

public class BattleBootstrap : MonoBehaviour
{
    #region Editor (Serialized)
    [Header("Player Party")]
    [SerializeField] private MonsterEntry[] playerParty;

    [Header("Enemy")]
    [SerializeField] private MonsterEntry enemyEntry;

    [Header("References")]
    [SerializeField] private BattleSystem battleSystem;
    #endregion

    #region Private Functions
    void Start()
    {
        if (battleSystem == null)
        {
            Debug.LogError("[BattleBootstrap] BattleSystem reference is missing!");
            return;
        }

        if (playerParty == null || playerParty.Length == 0)
        {
            Debug.LogError("[BattleBootstrap] Player party is empty!");
            return;
        }

        Monster[] party = new Monster[playerParty.Length];
        for (int i = 0; i < playerParty.Length; i++)
        {
            if (playerParty[i].data == null)
            {
                Debug.LogError($"[BattleBootstrap] Player party slot {i} has no MonsterData!");
                return;
            }
            party[i] = new Monster(playerParty[i].data, playerParty[i].level);
        }

        if (enemyEntry.data == null)
        {
            Debug.LogError("[BattleBootstrap] Enemy MonsterData is missing!");
            return;
        }
        Monster enemy = new Monster(enemyEntry.data, enemyEntry.level);

        battleSystem.StartBattle(party, enemy);
    }
    #endregion
}

[System.Serializable]
public struct MonsterEntry
{
    public MonsterData data;
    [Range(1, 100)] public int level;
}
