# Nuget Sign Checker

Scrapes the nuget.org [package stats page](https://www.nuget.org/stats/packages)
to get a list of the top 20 community packages and then checks each one to see
if it has an author signature or not.

## Caveats

The code doesn't just take the top 20 listed packages. It tries to take twenty
unique packages. For example, there are around eight different xunit.* packages
in the top 100 packages. That's stacking the game a bit, no?

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