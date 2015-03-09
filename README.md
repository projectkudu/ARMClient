ARMClient
=========

ARMClient is a simple command line tool to invoke the Azure Resource Manager API. You can install it from [Chocolatey](https://chocolatey.org/) by running:

    choco install armclient

This [blog post](http://blog.davidebbo.com/2015/01/azure-resource-manager-client.html) introduces the tool and is a good place to start.

Check out [wiki](https://github.com/projectkudu/ARMClient/wiki) for more details.

    Login and get tokens
        ARMClient.exe login [environment name]
    
    Call ARM api
        ARMClient.exe [get|post|put|delete] [url] [@file|json] (-verbose)
    
    Copy token to clipboard
        ARMClient.exe token [tenant|subscription]
    
    List token cache
        ARMClient.exe listcache
    
    Clear token cache
        ARMClient.exe clearcache

Note: The tokens are cached at `%USERPROFILE%\.arm` folder.  All files are encrypted with CurrentUser ProtectData .NET api.  

ARMClient.Library.ARMClient
============================

ARMClient.Library.ARMClient is a library that facilitates getting tokens and doing ARM operations. The client is used through [Dynamic Typing in .NET](http://msdn.microsoft.com/en-us/library/dd264736.aspx)

Example
=========

```C#
// This example prints the names of all resrouceGroups that don't
// have sites under a certain subscription

private static async Task Run()
{
    var armClient = ARMClient.GetDynamicClient(apiVersion: "2014-04-01",
                                authHelper: new AuthHelper(AzureEnvironments.Prod));

    var resrouceGroups = await armClient.Subscriptions["{subscriptionId}"]
                                        .ResourceGroups
                                        .GetAsync<JObject>();

    foreach (var resrouceGroup in resrouceGroups.value)
    {
        var sites = (Site[]) await armClient.Subscriptions["{subscriptionId}"]
                                            .ResourceGroups[resrouceGroup.name]
                                            .Providers["Microsoft.Web"]
                                            .Sites
                                            .GetAsync<Site[]>();

        if (sites.Length == 0)
        {
            Console.WriteLine("ResrouceGroup: {0} Doesn't contain any websites!",
                              resrouceGroup.name);
        }
    }
}


public class Site
{
    public string location { get; set; }
    public string name { get; set; }
}

```

The make up of the call is similar to the way ARM Urls are constructed. For example if the Url looks like this
`https://management.azure.com/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Web/sites/{webSiteName}/slots/{slotName}/config/web`

Note that you can omit the hostname, and simply have:
`/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Web/sites/{webSiteName}/slots/{slotName}/config/web`

then the `ARMClient` call will be `.Subscriptions["{subscriptionId}"].ResourceGroups["{resourceGroupName}"].Providers["Microsoft.Web"].Sites["{webSiteName}"].Slots["{slotName}"].Config["web"]`

Note: Capitalization is optional `.Subscriptions[""]` == `.subscription[""]` also the distinction between `[]` and `.` is also optional  `.Config["web"]` == `.Config.Web`.
However, some names like subscription Ids which are usually GUIDs are not valid C# identifiers so you will have to use the indexer notation.

ArmGuiClient
=========
There is also a WPF GUI tool we create for ease of use. [See ArmGuiClient help page for more details](https://github.com/projectkudu/ARMClient/wiki/ArmGuiClient)

![ArmGuiClient.exe](https://github.com/shrimpy/screenshots/blob/master/ArmGuiClient.png)
