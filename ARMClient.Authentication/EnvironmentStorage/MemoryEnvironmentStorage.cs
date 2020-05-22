using ARMClient.Authentication.Contracts;

namespace ARMClient.Authentication.EnvironmentStorage
{
    class MemoryEnvironmentStorage : IEnvironmentStorage
    {
        private string _env = Constants.ARMProdEnv;

        public void SaveEnvironment(string env)
        {
            this._env = env;
        }

        public string GetSavedEnvironment()
        {
            return this._env;
        }

        public bool IsCacheValid()
        {
            return true;
        }

        public void ClearSavedEnvironment()
        {
            this._env = Constants.ARMProdEnv;
        }
    }
}
