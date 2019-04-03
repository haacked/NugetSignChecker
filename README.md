# Nuget Sign Checker

Scrapes the nuget.org [package stats page](https://www.nuget.org/stats/packages)
to get a list of the top 100 community packages and then downloads each one to
check if the package has an author signature or not.

This tool is featured in a [blog post about the NuGet Package Signing experience](https://haacked.com/archive/2019/04/03/nuget-package-signing/).

## Caveats

The code doesn't test all one hundred of the top packages. Instead, it tries to
group packages by prefix and then picks the first one from each group. The reason
for this behavior is that there are groups of packages that are related to each
other and often installed together. For example, there are around eight different
xunit.* packages. If one is signed, the rest are likely assigned. And most of
these packages are installed as a group. I wanted to look at unique groups of 
packages.

So when there are multiple packages with the same prefix, this code just grabs
the first one of that group and ignores the rest. It's not perfect, but it's
good enough.

## Assumptions

Code assumes that you have `nuget.exe` in the `PATH`.

## Usage

I just load the solution in Visual Studio 2017 and hit the play button. But if
you like going through more hoops, you can build the console dll and run the
following from the root of the solution:

```cmd
dotnet .\src\bin\debug\netcoreapp2.1\NugetSignChecker.dll
```
