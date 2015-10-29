@echo off

nuget restore
msbuild

md artifacts
nuget pack -NoPackageAnalysis -OutputDirectory artifacts ARMClient.nuspec
