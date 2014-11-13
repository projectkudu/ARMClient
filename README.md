CSMClient
=========

CSMClient facilitates getting token and CSM resource access.  The simplest use case is to acquire token `CSMClient.exe login` and http GET CSM resource such as `CSMClient.exe get https://management.azure.com/subscriptions?api-version=2014-04-01`

Check out [wiki](https://github.com/suwatch/CSMClient/wiki) for more details.

    Login and get tokens
        CSMClient.exe login ([Prod|Current|Dogfood|Next])
    
    Call CSM api
        CSMClient.exe [get|post|put|delete] [url] ([user])
    
    Copy token to clipboard
        CSMClient.exe token [tenant] ([user])
    
    List token cache
        CSMClient.exe listcache ([Prod|Current|Dogfood|Next])
    
    Clear token cache
        CSMClient.exe clearcache ([Prod|Current|Dogfood|Next])

Note: The tokens are cached at `%USERPROFILE%\.csm` folder.  
