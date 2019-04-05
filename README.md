ARMClient
=========

ARMClient is a simple command line tool to invoke the Azure Resource Manager API. You can install it from [Chocolatey](https://chocolatey.org/) by running:

    choco install armclient --source=https://chocolatey.org/api/v2/

This [blog post](http://blog.davidebbo.com/2015/01/azure-resource-manager-client.html) introduces the tool and is a good place to start.

Check out [wiki](https://github.com/projectkudu/ARMClient/wiki) for more details.

    Login and get tokens
        ARMClient.exe login [environment name]
    
    Login with Azure CLI 2.0 (az -- https://github.com/Azure/azure-cli)
        ARMClient.exe azlogin
    
    Call ARM api
        ARMClient.exe [get|post|put|patch|delete] [url] (<@file|content>) (-h "header: value") (-verbose)
        Use '-h' multiple times to add more than one custom HTTP header.
    
    Copy token to clipboard
        ARMClient.exe token [tenant|subscription]
    
    List token cache
        ARMClient.exe listcache
    
    Clear token cache
        ARMClient.exe clearcache

Note: Valid values for optional `[environment name]`: (Default) `Prod` for Azure Global, `Fairfax` for Azure Government, `Blackforest` for Azure Germany, `Mooncake` for Azure China.

Note: The tokens are cached at `%USERPROFILE%\.arm` folder.  All files are encrypted with CurrentUser ProtectData .NET api. 

Note: PowerShell users will need to escape the `@` symbol with a back tick <code>`</code>.

