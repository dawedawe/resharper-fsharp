## Using a custom JetBrains FSharp.Compiler.Service

ReSharper.FSharp uses a modified fork of the FSharp.Compiler.Service (FCS), distributed in the `JetBrains.FSharp.Compiler.Service` nuget package.
This fork is hosted at https://github.com/JetBrains/fsharp

To test changes made in the vanilla FCS (https://github.com/dotnet/fsharp) to see how it affects ReSharper.FSharp you can do the following steps:

First, make sure you have:
- a fork and clone of `dotnet/fsharp`
- a fork and clone of `JetBrains/ReSharper.FSharp`  

Also, make sure you know which branches to use. Currently that would be the `net232` branch from JetBrains.
```
git clone https://github.com/JetBrains/resharper-fsharp --single-branch --branch net232
```

## Getting your changes into the JetBrains fsharp branch

The FCS changes you want to test should be pushed to your personal fork of `dotnet/fsharp`.

Next, go to the clone of your dotnet/fsharp fork and add `JetBrains/fsharp` as a remote. In this example, the name `jetbrains` is chosen for the new remote:
```
git remote add jetbrains https://github.com/JetBrains/fsharp.git
```

Fetch the newly added remote:
```
git fetch jetbrains net232
```

Now you can [cherry-pick](https://git-scm.com/docs/git-cherry-pick) your changes from your `dotnet/fsharp` fork to bring them into the `jetbrains/net232` branch  
```
git cherry-pick `<commit-hash>` --no-commit
```

## Build a custom `JetBrains.FSharp.Compiler.Service` package

Open the [`src\Compiler\FSharp.Compiler.Service.fsproj`](https://github.com/JetBrains/fsharp/blob/0b71e53a653b41d2c490a6b9040bec7fe6acea97/src/Compiler/FSharp.Compiler.Service.fsproj#L46) file and edit the configured nuspec file (in the `<NuspecFile>` tag) to be `JetBrains.FSharp.Compiler.Service.nuspec`.

Open the [`src\Compiler\JetBrains.FSharp.Compiler.Service.nuspec`](https://github.com/JetBrains/fsharp/blob/0b71e53a653b41d2c490a6b9040bec7fe6acea97/src/Compiler/JetBrains.FSharp.Compiler.Service.nuspec#L4) file and edit the package version (in the `<version>` tag) to be higher than the currently used version in ReSharper.FSharp. You can find the currently used version in the [`ReSharper.FSharp\Directory.Build.props`](../Directory.Build.props) file in the `<FSharpCompilerServiceVersion>` tag. Make sure the version you choose isn't already published somewhere. That might save you some trouble.  
You also need to change the VersionPrefix in the `<VersionPrefix>` tag in [`src\Compiler\FSharp.Compiler.Service.fsproj`](https://github.com/JetBrains/fsharp/blob/0b71e53a653b41d2c490a6b9040bec7fe6acea97/src/Compiler/FSharp.Compiler.Service.fsproj#L30).  

Now you can try to build a new `JetBrains.FSharp.Compiler.Service` package with your changes included. In your `dotnet/fsharp` clone, run the command:
```
.\Build.cmd -noVisualStudio -pack -c Debug
```
Or if you're on Linux/Max:
```
./build.sh -pack -c Debug
```

You might need to fix the outfall of your changes in order to let the build succeed in the JetBrains branch of FCS.
After a successful build, several packages can be found in `\artifacts\packages\Debug\PreRelease`.

## Letting ReSharper.FSharp consume your custom built `JetBrains.FSharp.Compiler.Service` package

We will use a local package source. On Windows, that could be for example:
```
mkdir C:\packages
```

Go to your `fsharp\artifacts\packages\Debug\PreRelease` folder and push the FCS packge to your local source:
```
dotnet nuget push .\JetBrains.FSharp.Compiler.Service.2023.1.3-dev.final.nupkg --source C:\packages\
```

Go to the ReSharper.FSharp subdirectory in your JetBrains/Resharper.FSharp clone:
```
cd ReSharper.FSharp
```

Add the created directory as a package source:
```
dotnet nuget add source --name local --configfile ./nuget.config "C:\packages"
```

Open the [`ReSharper.FSharp\Directory.Build.props`](../Directory.Build.props) file and change the FSharpCompilerServiceVersion value to have the same version as in your modified `JetBrains.FSharp.Compiler.Service.nuspec` file from above.  
But make sure to keep the right postfix (here `-dev.final`), so the package is found.
After you're done, all three files (`JetBrains.FSharp.Compiler.Service.nuspec`, `FSharp.Compiler.Service.fsproj`, `Directory.Build.props`) should have the same version defined.

Now you can finally build ReSharper.FSharp with your custom FSC package:
```
dotnet build
```

## Debugging

You can now open the ReSharper.FSharp solution with your Rider IDE and start a Debug session.  
To step into the sources of FCS, set a breakpoint on a call to it and when the breakpoint is hit, step into it. That should open your local JetBrains branch FCS sources and you can continue stepping through FCS code.

## Caveats

You might need to clean your `fsharp\artifacts\` folder if you repeatedly want to build packages.

Depending on the exact state of the code bases you might need to do some more changes to let things compile.  
For example, at the time of writing it was needed to comment out  
`<file src="..\..\artifacts\bin\FSharp.Compiler.Service\Debug\netstandard2.0\System.Diagnostics.DiagnosticSource.dll" target="lib\netstandard2.0" />`  
in the `JetBrains.FSharp.Compiler.Service.nuspec` file to let it compile.  

Furthermore a bump of the FSharpCoreVersion in `ReSharper.FSharp\Directory.Build.props` to `7.0.200` was needed to let it consume the custom built FCS package.
