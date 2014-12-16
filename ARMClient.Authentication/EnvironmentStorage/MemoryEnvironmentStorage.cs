using ARMClient.Authentication.Contracts;

namespace ARMClient.Authentication.EnvironmentStorage
{
    class MemoryEnvironmentStorage : IEnvironmentStorage
    {
        private AzureEnvironments _azureEnvironments = AzureEnvironments.Prod;
        public void SaveEnvironment(AzureEnvironments azureEnvironment)
        {
            this._azureEnvironments = azureEnvironment;
        }

        public AzureEnvironments GetSavedEnvironment()
        {
            return this._azureEnvironments;
        }

        public bool IsCacheValid()
        {
            return true;
        }

        public void ClearSavedEnvironment()
        {
            this._azureEnvironments = AzureEnvironments.Prod;
        }
    }
}
