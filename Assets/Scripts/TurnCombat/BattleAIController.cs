/// <summary>
/// Wraps AI / LLM interactions for enemy move selection and intent preview.
/// Pure C# class — no MonoBehaviour dependency.
/// </summary>
public class BattleAIController
{
    private readonly BattleLLMBrain llmBrain;
    private readonly BattleSessionData session;
    private readonly BattleUI ui;

    public BattleAIController(BattleLLMBrain llmBrain, BattleSessionData session, BattleUI ui)
    {
        this.llmBrain = llmBrain;
        this.session  = session;
        this.ui       = ui;
    }

    public void Bind()
    {
        if (llmBrain == null) return;
        llmBrain.EventAIActionDecided += HandleAIActionDecided;
        llmBrain.EventIntentDecided   += HandleIntentDecided;
    }

    public void Unbind()
    {
        if (llmBrain == null) return;
        llmBrain.EventAIActionDecided -= HandleAIActionDecided;
        llmBrain.EventIntentDecided   -= HandleIntentDecided;
    }

    public void FetchEnemyIntent()
    {
        session.IntentReady = false;
        session.IntentMoveIndex = -1;
        ui.UpdateEnemyIntent(null);

        if (llmBrain == null)
        {
            HandleIntentDecided(BattleRules.FallbackAIMove(session.EnemyMonster));
            return;
        }

        BattleContext ctx = BattleRules.BuildBattleContext(session, llmBrain.MaxChatHistoryForAIAction);
        llmBrain.RequestIntent(ctx);
    }

    public void RequestAIMove()
    {
        if (llmBrain == null)
        {
            HandleAIActionDecided(BattleRules.FallbackAIMove(session.EnemyMonster));
            return;
        }

        BattleContext ctx = BattleRules.BuildBattleContext(session, llmBrain.MaxChatHistoryForAIAction);
        llmBrain.RequestAIAction(ctx);
    }

    private void HandleAIActionDecided(int moveIndex)
    {
        session.PendingEnemyMoveIndex = moveIndex;
        session.WaitingForLLM = false;
    }

    private void HandleIntentDecided(int moveIndex)
    {
        session.IntentMoveIndex = moveIndex;
        session.IntentReady = true;

        if (session.State != BattleState.PlayerAction) return;

        int idx = BattleRules.ClampMoveIndex(session.EnemyMonster, moveIndex);
        MoveSlot move = session.EnemyMonster.Moves[idx];
        if (move != null)
            ui.UpdateEnemyIntent(move.Data.MoveName);
    }
}
