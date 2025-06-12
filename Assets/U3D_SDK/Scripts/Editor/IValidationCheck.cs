namespace U3D.Editor
{
    public interface IValidationCheck
    {
        string CheckName { get; }
        string Description { get; }
        ValidationSeverity Severity { get; }
        bool CanAutoFix { get; }
        ValidationResult RunCheck();
        void AutoFix();
    }
}