using ARMClient.Authentication.Contracts;

namespace ARMClient.Authentication.EnvironmentStorage
{
    public interface IEnvironmentStorage
    {
        void SaveEnvironment(AzureEnvironments azureEnvironment);
        AzureEnvironments GetSavedEnvironment();
        bool IsCacheValid();
        void ClearSavedEnvironment();
    }
}