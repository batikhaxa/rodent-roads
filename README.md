uses LD's monopatcher

---

`data/decompiled`
contains relevant decompiled code.
DO NOT COMMIT DECOMPILED CODE obviously...

incompatible:
any other mod which replaces `MinorPet.SimulateBehavior`
(original hooks are still present; but if additional behaviour is added with MonoPatcher). 

uses lispy <3

# build
build + package + install in one go:
```ps
powershell -ExecutionPolicy Bypass -File tools/build.ps1
```
outputs:
* `src/RodentRoads/bin/Release/RodentRoads.dll`
* `<Documents>/Electronic Arts/The Sims 3/Mods/Packages/RodentRoads.package`

`-NoPackage` builds only the dll, `-Configuration Debug` for a debug build.

or the dll directly:
```ps
dotnet build src/RodentRoads/RodentRoads.csproj -c Release -p:TargetFrameworkRootPath="C:\vscode\rodent-roads\.refasm\"
```

# package
[docs/packaging.md](docs/packaging.md) — `tools/build.ps1` drives
[ts3buildtool](https://github.com/Paprotk/ts3buildtool), which embeds the dll as an
S3SA resource using `src/RodentRoads/resources/nameMap.xml` and writes the package
straight into the game's Mods folder (replacing the previous build).

`-p:TargetFrameworkRootPath`:
-# there may be a better solution?

Modern machines only have v3.0/v3.5/v4.x reference assemblies, so normal MSBuild dies with
`MSB3644: reference assemblies for .NETFramework,Version=v2.0 were not found`.
The 2.0 reference assemblies are just the runtime assemblies in
`C:\Windows\Microsoft.NET\Framework\v2.0.50727`. `tools/build.ps1` creates junctions
under `.refasm/` (v2.0 and .NETFramework\v2.0, both -> v2.0.50727) and points MSBuild
at them.
