CSMClient
=========

CSMClient supports getting token and simple Http CSM resources.
Source codes are available at https://github.com/suwatch/CSMClient.

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
