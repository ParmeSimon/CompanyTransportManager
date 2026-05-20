namespace TransportManager.Save
{
    public interface ISaveable
    {
        void CaptureState(GameSaveData save);
        void RestoreState(GameSaveData save);
    }
}
