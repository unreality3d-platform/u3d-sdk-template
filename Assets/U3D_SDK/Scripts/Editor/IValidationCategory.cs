using System.Collections.Generic;

namespace U3D.Editor
{
    public interface IValidationCategory
    {
        string CategoryName { get; }
        System.Threading.Tasks.Task<List<ValidationResult>> RunChecks();
    }
}