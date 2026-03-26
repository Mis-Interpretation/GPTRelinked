[System.Serializable]
public class MoveSlot
{
    public MoveData Data { get; private set; }
    public int CurrentPP { get; private set; }

    public MoveSlot(MoveData data)
    {
        Data = data;
        CurrentPP = data.MaxPP;
    }

    public bool HasPP => CurrentPP > 0;

    public void UsePP()
    {
        if (CurrentPP > 0) CurrentPP--;
    }

    public void RestorePP(int amount)
    {
        CurrentPP = UnityEngine.Mathf.Min(CurrentPP + amount, Data.MaxPP);
    }

    public void RestoreAllPP()
    {
        CurrentPP = Data.MaxPP;
    }
}
