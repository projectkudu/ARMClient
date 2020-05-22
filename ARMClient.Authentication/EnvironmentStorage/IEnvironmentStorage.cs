using ARMClient.Authentication.Contracts;

namespace ARMClient.Authentication.EnvironmentStorage
{
    public interface IEnvironmentStorage
    {
        void SaveEnvironment(string env);
        string GetSavedEnvironment();
        bool IsCacheValid();
        void ClearSavedEnvironment();
    }
}