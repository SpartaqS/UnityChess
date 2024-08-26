namespace UnityChess.SaveSystem
{
    public interface ISaveable
    {
        object CaptureState(); //save method

        void RestoreState(object state); //load method
    }
}
