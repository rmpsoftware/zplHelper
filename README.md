# ZplHelper

ZplHelper renders Zebra ZPL labels to PNG locally using SkiaSharp. It preserves
the BinaryKits analyzer, printer storage and drawing APIs and uses SkiaSharp
for graphic decoding, conversion and encoding. The package targets .NET 10.
Native SkiaSharp and HarfBuzz assets are included through NuGet dependencies
on Windows, Linux and macOS. CI runs on Windows and Linux.

## Rendering

```shell
dotnet add package ZplHelper --version 0.2.0
```

```csharp
using BinaryKits.Zpl.Viewer;
using BinaryKits.Zpl.Viewer.ElementDrawers;

const string zpl = "^XA^CI28^FO32,32^A0N,40,40^FDHello, Zebra!^FS^XZ";
var storage = new PrinterStorage();
var analyzer = new ZplAnalyzer(storage);
var drawer = new ZplElementDrawer(storage, new DrawerOptions { OpaqueBackground = true });

foreach (var label in analyzer.Analyze(zpl).LabelInfos)
{
    byte[] png = drawer.Draw(label.ZplElements, labelWidth: 57.1, labelHeight: 76, printDensityDpmm: 12);
    // Write or embed the PNG in a document.
}
```

Dimensions are in millimeters and density is in dots per millimeter. To convert a
printer resolution in dpi, pass `(int)Math.Round(dpi / 25.4)`. The default PNG
background is transparent. Use a separate `PrinterStorage` and drawer for each
concurrent job. Use the same storage for parsing and drawing to preserve
downloaded graphics, formats and preloaded printer assets.

ZPL support follows the upstream analyzer. Common text and field blocks,
barcodes including QR, shapes, inline graphics, downloaded graphics and
printer storage are supported. Font metrics depend on the available typeface.
Set `FontManager.FontLoader` to control layout across platforms. Supply a
stream-backed typeface such as one loaded with `SKTypeface.FromFile` for
HarfBuzz shaping. On headless Linux the default font manager can fall back to
the bundled DejaVu Sans Mono.

Fonts P to V are rendered with the font 0 typeface like Zebra printers and
labelary.com do. `^LT` is ignored by default because it usually compensates a
printer specific media offset; set `ZplAnalyzer.ApplyLabelTop = true` to shift
the content like labelary.com.

## Migration from BinaryKits.Zpl.Viewer

Replace the `BinaryKits.Zpl.Viewer` PackageReference with `ZplHelper`.
Remove a direct `BinaryKits.Zpl.Label` reference if present because the label
types ship in the same assembly. The `BinaryKits.Zpl.*` namespaces and
`PrinterStorage`, `ZplAnalyzer`, `ZplElementDrawer.Draw` and
`DrawerOptions` calls used for PNG previews are retained. If your application
pins SkiaSharp native or managed packages, align their versions with the
ZplHelper dependency on SkiaSharp 4.157.0.

## Build and release

```shell
dotnet test -c Release
dotnet pack src/ZplHelper -c Release -o artifacts
```

The pack command produces a `.nupkg` and a `.snupkg`. To reference the local
package in a consumer project, add the absolute `artifacts` folder as a NuGet
source alongside nuget.org.

To publish, push the repository to GitHub and configure an environment named
`nuget` with the NuGet profile name in the `NUGET_USER` secret. On nuget.org,
register a [Trusted Publishing policy](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing)
for that repository, the `ci.yml` workflow and the `nuget` environment. Allow
publishing `ZplHelper` in the policy. Set the desired version in
`src/ZplHelper/ZplHelper.csproj` and run the **Build, test and release**
workflow on `main` with **Publish to nuget.org** enabled. CI tests the
package on Windows and Linux before the publish job requests a short-lived
NuGet credential. For a different GitHub repository, update the package links
in the project file.

## License

[MIT](LICENSE), based on [BinaryKits.Zpl](https://github.com/BinaryKits/BinaryKits.Zpl).
See [NOTICE.md](NOTICE.md) for source and font attribution.
