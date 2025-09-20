using System;
using System.Linq;

namespace ARMClient.Authentication.Contracts
{
    public class AzCloudEnvironment
    {
        public AzEndpoints endpoints { get; set; }
        public string profile { get; set; }
    }

    public class AzEndpoints
    {
        public string activeDirectory { get; set; }
        public string activeDirectoryGraphResourceId { get; set; }
        public string activeDirectoryResourceId { get; set; }
    }
}
