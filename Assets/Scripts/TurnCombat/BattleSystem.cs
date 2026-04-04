using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Thin MonoBehaviour orchestrator.
/// Owns the Unity lifecycle, coroutine execution, and UI event routing.
/// All data lives in BattleSessionData; rules in BattleRules;
/// move logic in BattleMoveExecutor; AI in BattleAIController.
/// </summary>
public class BattleSystem : MonoBehaviour
{
    #region Editor (Serialized)
    [SerializeField] private BattleUnit playerUnit;
    [SerializeField] private BattleUnit enemyUnit;
    [SerializeField] private BattleUI ui;
    [SerializeField] private BattleLLMBrain llmBrain;
    [SerializeField] private TypeChart typeChart;
    #endregion

    private BattleSessionData session;
    private BattleMoveExecutor moveExecutor;
    private BattleAIController aiController;

    public event Action<bool> OnBattleOver;

    // ── Public API ──

    public void StartBattle(Monster[] party, Monster enemy)
    {
        session = new BattleSessionData
        {
            PlayerParty = party,
            EnemyMonster = enemy
        };
        session.CurrentMonsterIndex = session.FindFirstAliveIndex();

        moveExecutor = new BattleMoveExecutor(ui, typeChart);
        aiController = new BattleAIController(llmBrain, session, ui);

        if (llmBrain != null)
        {
            llmBrain.Init();
            llmBrain.SetEnemyPersonality(enemy.Data.MonsterName, enemy.Data.PrimaryType, enemy.Data.Personality);
            llmBrain.EventChatResponseReceived += OnChatResponseReceived;
        }

        aiController.Bind();
        BindUI();
        StartCoroutine(SetupBattle());
    }

    private void UpdateEnemyCatchRateUI()
    {
        float probability = CatchCalculator.GetCatchProbability(session.EnemyMonster, session.AccumulatedCatchBonus);
        ui.EnemyHUD.UpdateCatchRate(probability);
    }

    // ── Setup & Action Phase ──

    private IEnumerator SetupBattle()
    {
        session.State = BattleState.Start;

        playerUnit.Setup(session.PlayerMonster);
        enemyUnit.Setup(session.EnemyMonster);

        ui.PlayerHUD.SetData(session.PlayerMonster);
        ui.EnemyHUD.SetData(session.EnemyMonster);
        UpdateEnemyCatchRateUI();

        yield return ui.DialogBox.TypeDialog($"A wild {session.EnemyMonster.Data.MonsterName} appeared!");
        yield return new WaitForSeconds(1f);
        yield return ui.DialogBox.TypeDialog($"Go, {session.PlayerMonster.Data.MonsterName}!");
        yield return new WaitForSeconds(1f);

        PlayerActionPhase();
    }

    private void PlayerActionPhase()
    {
        session.TurnNumber++;
        session.State = BattleState.PlayerAction;
        session.ResetTurnChat();
        UpdateEnemyCatchRateUI();
        ui.DialogBox.SetDialog("Choose an action:");
        ui.ShowActionPanel();
        aiController.FetchEnemyIntent();
    }

    private void ResumeActionPhase()
    {
        session.State = BattleState.PlayerAction;
        UpdateEnemyCatchRateUI();
        ui.DialogBox.SetDialog("Choose an action:");
        ui.ShowActionPanel();
        aiController.FetchEnemyIntent();
    }

    // ── UI Event Handlers ──

    private void HandleFightSelected()
    {
        if (session.State != BattleState.PlayerAction) return;
        ui.ShowMovePanel(session.PlayerMonster.Moves);
    }

    private void HandleTalkSelected()
    {
        if (session.State != BattleState.PlayerAction) return;
        if (session.HasChatThisTurn)
        {
            ui.DialogBox.SetDialog("Already talked this turn!");
            return;
        }
        ui.ShowChatPanel();
    }

    private void HandleSwitchSelected()
    {
        if (session.State != BattleState.PlayerAction) return;
        ui.ShowPartyPanel(session.PlayerParty, session.CurrentMonsterIndex);
    }

    private void HandleCatchSelected()
    {
        if (session.State != BattleState.PlayerAction) return;
        session.State = BattleState.PlayerCatch;
        ui.ClearEnemyIntent();
        StartCoroutine(PlayerCatchCoroutine());
    }

    private void HandleMoveSelected(int moveIndex)
    {
        if (session.State != BattleState.PlayerAction) return;
        MoveSlot slot = session.PlayerMonster.Moves[moveIndex];
        if (slot == null || !slot.HasPP) return;

        session.State = BattleState.PlayerMove;
        ui.HideAllPanels();
        ui.ClearEnemyIntent();
        StartCoroutine(ExecuteTurn(moveIndex));
    }

    private void HandlePartyMemberSelected(int partyIndex)
    {
        if (session.State != BattleState.PlayerAction) return;
        if (partyIndex == session.CurrentMonsterIndex || session.PlayerParty[partyIndex].IsFainted) return;

        session.State = BattleState.PlayerSwitch;
        ui.HideAllPanels();
        ui.ClearEnemyIntent();
        StartCoroutine(SwitchMonsterCoroutine(partyIndex, true));
    }

    private void HandleChatSubmitted(string parsedMessage, string rawMessage)
    {
        if (session.State != BattleState.PlayerAction) return;
        session.State = BattleState.PlayerChat;
        session.HasChatThisTurn = true;
        session.ChatMessageThisTurn = parsedMessage; // Logic uses parsed message
        ui.HideAllPanels();
        ui.ClearEnemyIntent();
        StartCoroutine(ChatCoroutine(parsedMessage, rawMessage));
    }

    private void HandleBackToActions()
    {
        if (session.State != BattleState.PlayerAction) return;
        PlayerActionPhase();
    }

    private void HandleRestartSelected()
    {
        if (session.State != BattleState.BattleOver) return;
        UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
    }

    // ── Turn Execution ──

    private IEnumerator ExecuteTurn(int playerMoveIndex)
    {
        MoveSlot playerMove = session.PlayerMonster.Moves[playerMoveIndex];

        while (!session.IntentReady) yield return null;

        int enemyMoveIdx = BattleRules.ClampMoveIndex(session.EnemyMonster, session.IntentMoveIndex);
        MoveSlot enemyMove = session.EnemyMonster.Moves[enemyMoveIdx];

        bool playerFirst = BattleRules.ShouldPlayerGoFirst(
            playerMove.Data, enemyMove?.Data, session.PlayerMonster, session.EnemyMonster);

        if (playerFirst)
        {
            int prevHp = session.EnemyMonster.CurrentHp;
            yield return moveExecutor.PerformMove(session.PlayerMonster, session.EnemyMonster, playerMove, ui.EnemyHUD, true);
            LogMoveUsed(session.PlayerMonster, session.EnemyMonster, playerMove, prevHp);
            yield return ApplyMoveCatchBonus();
            if (CheckBattleEnd()) yield break;

            prevHp = session.PlayerMonster.CurrentHp;
            yield return moveExecutor.PerformMove(session.EnemyMonster, session.PlayerMonster, enemyMove, ui.PlayerHUD, false);
            LogMoveUsed(session.EnemyMonster, session.PlayerMonster, enemyMove, prevHp);
            yield return ApplyMoveCatchBonus();
            if (CheckBattleEnd()) yield break;
        }
        else
        {
            int prevHp = session.PlayerMonster.CurrentHp;
            yield return moveExecutor.PerformMove(session.EnemyMonster, session.PlayerMonster, enemyMove, ui.PlayerHUD, false);
            LogMoveUsed(session.EnemyMonster, session.PlayerMonster, enemyMove, prevHp);
            yield return ApplyMoveCatchBonus();
            if (CheckBattleEnd()) yield break;

            prevHp = session.EnemyMonster.CurrentHp;
            yield return moveExecutor.PerformMove(session.PlayerMonster, session.EnemyMonster, playerMove, ui.EnemyHUD, true);
            LogMoveUsed(session.PlayerMonster, session.EnemyMonster, playerMove, prevHp);
            yield return ApplyMoveCatchBonus();
            if (CheckBattleEnd()) yield break;
        }

        yield return moveExecutor.ProcessStatusEffects(session.PlayerMonster, ui.PlayerHUD);
        if (CheckBattleEnd()) yield break;
        yield return moveExecutor.ProcessStatusEffects(session.EnemyMonster, ui.EnemyHUD);
        if (CheckBattleEnd()) yield break;

        PlayerActionPhase();
    }

    // ── Chat ──

    private IEnumerator ChatCoroutine(string parsedMessage, string rawMessage)
    {
        // Display uses the raw message
        ui.DialogBox.SetDialog($"You said to {session.EnemyMonster.Data.MonsterName}: '{rawMessage}'");
        yield return new WaitForSeconds(1.5f);

        session.WaitingForLLM = true;
        llmBrain.RequestChatResponse(parsedMessage, session.EnemyMonster, session.PlayerMonster,
                                      session.ChatHistory, session.BattleLog);
        yield return ui.DialogBox.TypeDialog($"{session.EnemyMonster.Data.MonsterName} is thinking...");
        while (session.WaitingForLLM) yield return null;
    }

    private void OnChatResponseReceived(ChatBuffResult result)
    {
        session.WaitingForLLM = false;
        StartCoroutine(ProcessChatResult(result));
    }

    private IEnumerator ProcessChatResult(ChatBuffResult result)
    {
        session.ChatHistory.Add(new ChatExchange
        {
            playerMessage = session.ChatMessageThisTurn,
            monsterResponse = result.response
        });
        session.LogEvent($"训练师对{session.EnemyMonster.Data.MonsterName}说: \"{session.ChatMessageThisTurn}\"");
        session.LogEvent($"{session.EnemyMonster.Data.MonsterName}回应: \"{result.response}\"");

        yield return ui.DialogBox.TypeDialog($"{session.EnemyMonster.Data.MonsterName}: \"{result.response}\"");
        yield return new WaitForSeconds(1f);

        if (result.stages != 0 && Enum.TryParse<StatType>(result.statType, out StatType stat))
        {
            Monster target = result.buffTarget == "enemy" ? session.EnemyMonster : session.PlayerMonster;
            int actual = target.ApplyStatStageChange(stat, result.stages);
            if (actual != 0)
            {
                string direction = actual > 0 ? "rose" : "fell";
                string amount = Mathf.Abs(actual) > 1 ? "sharply " : "";
                session.LogEvent($"{target.Data.MonsterName}的{BattleRules.GetStatName(stat)}{(actual > 0 ? "上升" : "下降")}了");
                yield return ui.DialogBox.TypeDialog(
                    $"{target.Data.MonsterName}'s {BattleRules.GetStatName(stat)} {amount}{direction}!");
                yield return new WaitForSeconds(0.5f);
            }
        }

        if (result.catchRateModifier != 0f)
        {
            session.AccumulatedCatchBonus += result.catchRateModifier;
            UpdateEnemyCatchRateUI();
            session.LogEvent(result.catchRateModifier > 0f
                ? $"{session.EnemyMonster.Data.MonsterName}变得更友好了"
                : $"{session.EnemyMonster.Data.MonsterName}变得更警惕了");
            string msg = result.catchRateModifier > 0f
                ? $"{session.EnemyMonster.Data.MonsterName} seems more friendly!"
                : $"{session.EnemyMonster.Data.MonsterName} became more wary!";
            yield return ui.DialogBox.TypeDialog(msg);
            yield return new WaitForSeconds(0.5f);
        }

        ResumeActionPhase();
    }

    private IEnumerator ApplyMoveCatchBonus()
    {
        float mod = moveExecutor.LastCatchRateModifier;
        Debug.LogWarning($"[CatchBonus] move modifier: {mod}, accumulated: {session.AccumulatedCatchBonus}");
        if (mod == 0f) yield break;

        session.AccumulatedCatchBonus += mod;
        UpdateEnemyCatchRateUI();
        string msg = mod > 0f
            ? $"{session.EnemyMonster.Data.MonsterName} seems more friendly!"
            : $"{session.EnemyMonster.Data.MonsterName} became more wary!";
        yield return ui.DialogBox.TypeDialog(msg);
        yield return new WaitForSeconds(0.5f);
    }

    // ── Switch ──

    private IEnumerator SwitchMonsterCoroutine(int newIndex, bool enemyGetsFreeTurn)
    {
        session.LogEvent($"训练师收回了{session.PlayerMonster.Data.MonsterName}，派出了{session.PlayerParty[newIndex].Data.MonsterName}");
        session.PlayerMonster.ResetStatStages();
        session.CurrentMonsterIndex = newIndex;
        playerUnit.Setup(session.PlayerMonster);
        ui.PlayerHUD.SetData(session.PlayerMonster);

        yield return ui.DialogBox.TypeDialog($"Go, {session.PlayerMonster.Data.MonsterName}!");
        yield return new WaitForSeconds(1f);

        if (enemyGetsFreeTurn)
        {
            session.WaitingForLLM = true;
            session.PendingEnemyMoveIndex = 0;
            aiController.RequestAIMove();
            while (session.WaitingForLLM) yield return null;

            int idx = BattleRules.ClampMoveIndex(session.EnemyMonster, session.PendingEnemyMoveIndex);
            MoveSlot enemyMove = session.EnemyMonster.Moves[idx];
            int prevHp = session.PlayerMonster.CurrentHp;
            yield return moveExecutor.PerformMove(session.EnemyMonster, session.PlayerMonster, enemyMove, ui.PlayerHUD, false);
            LogMoveUsed(session.EnemyMonster, session.PlayerMonster, enemyMove, prevHp);
            yield return ApplyMoveCatchBonus();
            if (CheckBattleEnd()) yield break;

            yield return moveExecutor.ProcessStatusEffects(session.PlayerMonster, ui.PlayerHUD);
            if (CheckBattleEnd()) yield break;
            yield return moveExecutor.ProcessStatusEffects(session.EnemyMonster, ui.EnemyHUD);
            if (CheckBattleEnd()) yield break;
        }

        PlayerActionPhase();
    }

    // ── Catch ──

    private IEnumerator PlayerCatchCoroutine()
    {
        session.LogEvent("训练师尝试捕获");
        yield return ui.DialogBox.TypeDialog("You threw a catch ball!");
        yield return new WaitForSeconds(0.5f);

        CatchResult result = CatchCalculator.TryCatch(session.EnemyMonster, session.AccumulatedCatchBonus);

        for (int i = 0; i < result.shakeCount; i++)
        {
            yield return ui.DialogBox.TypeDialog("..." + new string('●', i + 1));
            yield return new WaitForSeconds(0.6f);
        }

        if (result.success)
        {
            session.LogEvent($"成功捕获了{session.EnemyMonster.Data.MonsterName}！");
            yield return ui.DialogBox.TypeDialog($"Successfully caught {session.EnemyMonster.Data.MonsterName}!");
            yield return new WaitForSeconds(1.5f);
            
            float probability = CatchCalculator.GetCatchProbability(session.EnemyMonster, session.AccumulatedCatchBonus);
            string endMsg = $"Successfully caught {session.EnemyMonster.Data.MonsterName}!";
            if (probability < 0.5f)
            {
                endMsg += $"\nYou manage to do it at {Mathf.RoundToInt(probability * 100)}% catch rate. Lucky you!";
            }
            BattleEnd(true, endMsg);
        }
        else
        {
            session.LogEvent($"{session.EnemyMonster.Data.MonsterName}挣脱了！");
            yield return ui.DialogBox.TypeDialog($"{session.EnemyMonster.Data.MonsterName} broke free!");
            yield return new WaitForSeconds(1f);

            while (!session.IntentReady) yield return null;

            int idx = BattleRules.ClampMoveIndex(session.EnemyMonster, session.IntentMoveIndex);
            MoveSlot enemyMove = session.EnemyMonster.Moves[idx];
            int prevHp = session.PlayerMonster.CurrentHp;
            yield return moveExecutor.PerformMove(session.EnemyMonster, session.PlayerMonster, enemyMove, ui.PlayerHUD, false);
            LogMoveUsed(session.EnemyMonster, session.PlayerMonster, enemyMove, prevHp);
            yield return ApplyMoveCatchBonus();
            if (CheckBattleEnd()) yield break;

            yield return moveExecutor.ProcessStatusEffects(session.PlayerMonster, ui.PlayerHUD);
            if (CheckBattleEnd()) yield break;
            yield return moveExecutor.ProcessStatusEffects(session.EnemyMonster, ui.EnemyHUD);
            if (CheckBattleEnd()) yield break;

            PlayerActionPhase();
        }
    }

    // ── Battle End ──

    private bool CheckBattleEnd()
    {
        if (session.EnemyMonster.IsFainted)
        {
            StartCoroutine(HandleEnemyFainted());
            return true;
        }

        if (session.PlayerMonster.IsFainted)
        {
            int nextAlive = session.FindFirstAliveIndex();
            if (nextAlive < 0)
            {
                StartCoroutine(HandleAllPlayerFainted());
                return true;
            }
            StartCoroutine(HandlePlayerMonsterFainted(nextAlive));
            return true;
        }

        return false;
    }

    private IEnumerator HandleEnemyFainted()
    {
        session.State = BattleState.BattleOver;

        int expGain = ExpCalculator.GetExpGain(session.EnemyMonster.Data, session.EnemyMonster.Level);
        yield return ui.DialogBox.TypeDialog($"{session.PlayerMonster.Data.MonsterName} gained {expGain} EXP!");
        yield return new WaitForSeconds(1f);

        int levelsGained = session.PlayerMonster.AddExp(expGain);
        ui.PlayerHUD.UpdateExpBar();

        if (levelsGained > 0)
        {
            ui.PlayerHUD.RefreshLevel();
            yield return ui.DialogBox.TypeDialog($"{session.PlayerMonster.Data.MonsterName} grew to Lv{session.PlayerMonster.Level}!");
            yield return new WaitForSeconds(1f);

            List<MoveData> newMoves = session.PlayerMonster.GetNewMovesForCurrentLevel();
            foreach (var move in newMoves)
            {
                bool learned = session.PlayerMonster.LearnMove(move);
                if (learned)
                {
                    yield return ui.DialogBox.TypeDialog($"{session.PlayerMonster.Data.MonsterName} learned {move.MoveName}!");
                    yield return new WaitForSeconds(1f);
                }
                else
                {
                    yield return ui.DialogBox.TypeDialog($"{session.PlayerMonster.Data.MonsterName} wants to learn {move.MoveName}, but moves are full!");
                    yield return new WaitForSeconds(1f);
                }
            }
        }

        yield return ui.DialogBox.TypeDialog("You won the battle!");
        yield return new WaitForSeconds(1f);
        BattleEnd(true);
    }

    private IEnumerator HandlePlayerMonsterFainted(int nextAlive)
    {
        yield return ui.DialogBox.TypeDialog($"{session.PlayerMonster.Data.MonsterName} is unable to battle!");
        yield return new WaitForSeconds(1f);
        yield return ui.DialogBox.TypeDialog("Choose the next monster!");
        ui.ShowPartyPanel(session.PlayerParty, session.CurrentMonsterIndex);

        session.State = BattleState.PlayerAction;
    }

    private IEnumerator HandleAllPlayerFainted()
    {
        session.State = BattleState.BattleOver;
        yield return ui.DialogBox.TypeDialog("All monsters have fainted...");
        yield return new WaitForSeconds(1f);
        yield return ui.DialogBox.TypeDialog("You lost the battle...");
        yield return new WaitForSeconds(1f);
        BattleEnd(false);
    }

    private void BattleEnd(bool playerWon, string endMessage = "")
    {
        session.State = BattleState.BattleOver;
        ui.HideAllPanels();
        ui.ClearEnemyIntent();

        if (string.IsNullOrEmpty(endMessage))
        {
            endMessage = playerWon ? "Battle Won!" : "Battle Lost...";
        }
        ui.ShowEndPanel(playerWon, endMessage);

        if (llmBrain != null)
            llmBrain.EventChatResponseReceived -= OnChatResponseReceived;

        aiController.Unbind();
        UnbindUI();

        OnBattleOver?.Invoke(playerWon);
    }

    // ── Logging ──

    private void LogMoveUsed(Monster attacker, Monster target, MoveSlot move, int targetPrevHp)
    {
        int damage = targetPrevHp - target.CurrentHp;
        string dmgText = damage > 0 ? $"，对{target.Data.MonsterName}造成了{damage}点伤害" : "";
        session.LogEvent($"{attacker.Data.MonsterName}使用了{move.Data.MoveName}{dmgText}");
    }

    // ── UI Binding ──

    private void BindUI()
    {
        ui.Init();
        ui.OnFightSelected       += HandleFightSelected;
        ui.OnTalkSelected        += HandleTalkSelected;
        ui.OnSwitchSelected      += HandleSwitchSelected;
        ui.OnCatchSelected       += HandleCatchSelected;
        ui.OnMoveSelected        += HandleMoveSelected;
        ui.OnPartyMemberSelected += HandlePartyMemberSelected;
        ui.OnChatSubmitted       += HandleChatSubmitted;
        ui.OnBackToActions       += HandleBackToActions;
        ui.OnRestartSelected     += HandleRestartSelected;
    }

    private void UnbindUI()
    {
        ui.OnFightSelected       -= HandleFightSelected;
        ui.OnTalkSelected        -= HandleTalkSelected;
        ui.OnSwitchSelected      -= HandleSwitchSelected;
        ui.OnCatchSelected       -= HandleCatchSelected;
        ui.OnMoveSelected        -= HandleMoveSelected;
        ui.OnPartyMemberSelected -= HandlePartyMemberSelected;
        ui.OnChatSubmitted       -= HandleChatSubmitted;
        ui.OnBackToActions       -= HandleBackToActions;
        // 注意：不在这里解绑 OnRestartSelected，因为它在 BattleEnd 之后仍需要生效
    }
}
