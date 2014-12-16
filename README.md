ARMClient
=========

ARMClient facilitates getting token and ARM resource access.  The simplest use case is to acquire token `ARMClient.exe login` and http GET CSM resource such as `ARMClient.exe get https://management.azure.com/subscriptions?api-version=2014-04-01`

Check out [wiki](https://github.com/projectkudu/ARMClient/wiki) for more details.

    Login and get tokens
        ARMClient.exe login ([Prod|Current|Dogfood|Next])
    
    Call ARM api
        ARMClient.exe [get|post|put|delete] [url] ([user])
    
    Copy token to clipboard
        ARMClient.exe token [tenant] ([user])
    
    List token cache
        ARMClient.exe listcache ([Prod|Current|Dogfood|Next])
    
    Clear token cache
        ARMClient.exe clearcache ([Prod|Current|Dogfood|Next])

Note: The tokens are cached at `%USERPROFILE%\.csm` folder.  

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

The make up of the call is similar to the way CSM Urls are constructed. For example if the Url looks like this
`https://management.azure.com/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Web/sites/{webSiteName}/slots/{slotName}/config/web`

then the `ARMClient` call will be `.Subscriptions["{subscriptionId}"].ResourceGroups["{resourceGroupName}"].Providers["Microsoft.Web"].Sites["{webSiteName}"].Slots["{slotName}"].Config["web"]`

Note: Capitalization is optional `.Subscriptions[""]` == `.subscription[""]` also the distinction between `[]` and `.` is also optional  `.Config["web"]` == `.Config.Web`.
However, some names like subscription Ids which are usually GUIDs are not valid C# identifiers so you will have to use the indexer notation.
