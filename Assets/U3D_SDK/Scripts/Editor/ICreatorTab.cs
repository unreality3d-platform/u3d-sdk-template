namespace U3D.Editor
{
    public interface ICreatorTab
    {
        string TabName { get; }
        bool IsComplete { get; }
        void Initialize();
        void DrawTab();
    }
}