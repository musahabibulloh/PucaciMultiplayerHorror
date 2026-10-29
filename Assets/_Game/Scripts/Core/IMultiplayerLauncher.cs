namespace Pucaci.Core
{
    public interface IMultiplayerLauncher
    {
        void Host();
        void Join(string roomCode);
        bool IsAvailable { get; }
    }
}
