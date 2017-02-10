@echo off

nuget restore -PackagesDirectory packages
msbuild

md artifacts
nuget pack -NoPackageAnalysis -OutputDirectory artifacts ARMClient.nuspec
