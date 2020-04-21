@echo off

nuget restore -PackagesDirectory packages
msbuild

md artifacts
nuget pack -NoPackageAnalysis -OutputDirectory artifacts ARMClient.nuspec

REM choco apikey --key [api-key] --source https://push.chocolatey.org/
REM choco push artifacts\ARMClient.1.6.0.nupkg --source https://push.chocolatey.org/
