bepu came from:
https://github.com/bepu/bepuphysics2

it was compiled using ReleaseStripNoProfiling and copied from:
bepuphysics2-master\BepuPhysics\bin\ReleaseStripNoProfiling\netstandard2.0

----------------------------------------

It took a couple hours to figure out how to reference System.Xaml.dll (there was no nuget)

The first attempt was to just copy the dll into this folder from C:\Program Files\dotnet\packs\Microsoft.WindowsDesktop.App.Ref\3.1.0\ref\netcoreapp3.1
(which is where the wpf exe project is pointing)

Then after adding a wpf user control library and comparing csproj files, it's these two entries that add a reference to wpf dlls:


<Project Sdk="Microsoft.NET.Sdk.WindowsDesktop">

    <UseWPF>true</UseWPF>
  </PropertyGroup>
