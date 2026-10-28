# Third-party notices

Corvida's own source code is licensed under the MIT license in LICENSE.
The desktop package (self-contained linux-x64 `.deb`), the browser UI (WebAssembly bundle served by the
`web` container) and the REST API container also redistribute the third-party components below. These
components keep their own licenses, and the full license texts are in `licenses/`. Listing them here
does not relicense them or imply endorsement.

"Ships in" says which artifact contains the component: **desktop** (`.deb`), **web** (WebAssembly
bundle, which every browser that opens the hosted UI downloads), **API** (`Corvida.Api` container).

## Project assets

| Asset | Origin | License |
| --- | --- | --- |
| App icons: `src/Corvida/Assets/corvida.png`, `corvida_light.png`, `src/Corvida.Web/Assets/corvida.png`, `src/Corvida.Web/wwwroot/favicon.ico`, `favicon-192.png`, `apple-touch-icon.png` | Generated for this project with an AI image generation tool; no third-party artwork was used as input. | To the extent the project contributors hold rights in these images, they are available under the repository's MIT license. This is not a claim of exclusive copyright in purely machine-generated material. |

## Redistributed libraries

| Component | Source | Ships in | License / local copy |
| --- | --- | --- | --- |
| .NET runtime 10.0 and libraries (Microsoft.NETCore.App runtime, Microsoft.Extensions.\*, Microsoft.Bcl.Cryptography, ICU data in the WebAssembly runtime) | https://github.com/dotnet/runtime | desktop, web, API | MIT: licenses/DotNet-MIT.txt; upstream component notices: licenses/DotNet-THIRD-PARTY-NOTICES.txt |
| ASP.NET Core and Entity Framework Core (Microsoft.AspNetCore.SignalR.Client, Microsoft.AspNetCore.SignalR.Client.Core, Microsoft.AspNetCore.SignalR.Common, Microsoft.AspNetCore.SignalR.Protocols.Json, Microsoft.AspNetCore.Http.Connections.Client, Microsoft.AspNetCore.Http.Connections.Common, Microsoft.AspNetCore.Connections.Abstractions, Microsoft.AspNetCore.Identity.EntityFrameworkCore, Microsoft.AspNetCore.OpenApi, Microsoft.EntityFrameworkCore, Microsoft.EntityFrameworkCore.Abstractions, Microsoft.EntityFrameworkCore.Relational, Microsoft.Extensions.Http.Polly) | https://github.com/dotnet/aspnetcore, https://github.com/dotnet/efcore | desktop, web (SignalR client); API | MIT: licenses/DotNet-MIT.txt; upstream component notices: licenses/AspNetCore-THIRD-PARTY-NOTICES.txt |
| .NET Extensions (Microsoft.Extensions.Http.Resilience, Microsoft.Extensions.Resilience, Microsoft.Extensions.ServiceDiscovery, Microsoft.Extensions.ServiceDiscovery.Abstractions, Microsoft.Extensions.Telemetry, Microsoft.Extensions.Telemetry.Abstractions, Microsoft.Extensions.Http.Diagnostics, Microsoft.Extensions.AmbientMetadata.Application, Microsoft.Extensions.Compliance.Abstractions, Microsoft.Extensions.DependencyInjection.AutoActivation, Microsoft.Extensions.Diagnostics.ExceptionSummarization) | https://github.com/dotnet/extensions | API | MIT: licenses/DotNet-Extensions-MIT.txt; upstream component notices: licenses/DotNet-Extensions-THIRD-PARTY-NOTICES.txt |
| Emscripten runtime and system libraries, 3.1.56 (compiled into `dotnet.native.wasm` / `dotnet.native.js` by the .NET WebAssembly build) | https://github.com/emscripten-core/emscripten | web | MIT OR NCSA, used under MIT: licenses/Emscripten-MIT-OR-NCSA.txt (the file also lists the licenses of the bundled system libraries) |
| Avalonia 12.1.3 (Avalonia, Avalonia.Desktop, Avalonia.Browser, Avalonia.Themes.Fluent, Avalonia.Fonts.Inter, Avalonia.FreeDesktop, Avalonia.FreeDesktop.AtSpi, Avalonia.HarfBuzz, Avalonia.Native, Avalonia.Remote.Protocol, Avalonia.Skia, Avalonia.Win32, Avalonia.X11) | https://github.com/AvaloniaUI/Avalonia | desktop, web | MIT: licenses/Avalonia-MIT.txt; upstream component notices: licenses/Avalonia-NOTICE.txt |
| Inter font, through Avalonia.Fonts.Inter | https://github.com/rsms/inter | desktop, web | OFL-1.1: licenses/Inter-OFL-1.1.txt (applies to the font, separately from Avalonia's MIT wrapper) |
| MicroCom.Runtime 0.11.6 | https://github.com/kekekeks/MicroCom | desktop | MIT: licenses/MicroCom-MIT.txt |
| Tmds.DBus.Protocol 0.94.1 | https://github.com/tmds/Tmds.DBus | desktop | MIT: licenses/Tmds.DBus-MIT.txt |
| SkiaSharp 3.119.4 and HarfBuzzSharp 8.3.1.3, with native assets (SkiaSharp, SkiaSharp.NativeAssets.Linux, SkiaSharp.NativeAssets.WebAssembly, HarfBuzzSharp, HarfBuzzSharp.NativeAssets.Linux, HarfBuzzSharp.NativeAssets.WebAssembly; `libSkiaSharp.so`, `libHarfBuzzSharp.so`, and statically linked into `dotnet.native.wasm`) | https://github.com/mono/SkiaSharp | desktop, web | MIT: licenses/SkiaSharp-MIT.txt; upstream component notices (Skia, HarfBuzz, libpng, FreeType, …): licenses/SkiaSharp-THIRD-PARTY-NOTICES.txt |
| KD.Avalonia.Rice 26.10.19 | https://www.nuget.org/packages/KD.Avalonia.Rice | desktop, web | MIT: licenses/KD.Avalonia.Rice-MIT.txt |
| Theme palettes adapted in KD.Avalonia.Rice: Nord, Solarized, Dracula, Gruvbox, Catppuccin | https://github.com/nordtheme/nord, https://github.com/altercation/solarized, https://github.com/dracula/dracula-theme, https://github.com/morhetz/gruvbox, https://github.com/catppuccin/palette | desktop, web | MIT: licenses/Nord-MIT.txt, licenses/Solarized-MIT.txt, licenses/Dracula-MIT.txt, licenses/Gruvbox-MIT.txt, licenses/Catppuccin-MIT.txt |
| Material.Icons and Material.Icons.Avalonia 3.0.2 | https://github.com/SKProCH/Material.Icons, https://github.com/AvaloniaUtils/Material.Icons.Avalonia | desktop, web | Wrapper code, MIT: licenses/Material.Icons-MIT.txt; icon collection (Pictogrammers Material Design Icons): licenses/Pictogrammers-LICENSE.txt and licenses/Apache-2.0.txt |
| CommunityToolkit.Mvvm 8.4.2 | https://github.com/CommunityToolkit/dotnet | desktop, web | MIT: licenses/CommunityToolkit-MIT.txt; upstream component notices: licenses/CommunityToolkit-THIRD-PARTY-NOTICES.txt |
| Markdown.Avalonia 12.0.0-a3 (Markdown.Avalonia, Markdown.Avalonia.Tight, Markdown.Avalonia.Html, Markdown.Avalonia.Svg, Markdown.Avalonia.SyntaxHigh, ColorDocument.Avalonia, ColorTextBlock.Avalonia) | https://github.com/whistyun/Markdown.Avalonia | desktop, web (core only) | MIT: licenses/Markdown.Avalonia-MIT.txt |
| AvaloniaEdit 12.0.0 (Avalonia.AvaloniaEdit) | https://github.com/AvaloniaUI/AvaloniaEdit | desktop | MIT: licenses/AvaloniaEdit-MIT.txt |
| Svg.Skia 4.1.0 (Svg.Controls.Avalonia, Svg.Model, Svg.SceneGraph, ShimSkiaSharp) | https://github.com/wieslawsoltes/Svg.Skia | desktop | MIT: licenses/Svg.Skia-MIT.txt |
| Svg.Custom 4.1.0 (fork of the SVG rendering library, shipped by Svg.Skia) | https://github.com/svg-net/SVG | desktop | MS-PL: licenses/SVG-MS-PL.txt |
| ExCSS 4.3.1 | https://github.com/TylerBrinks/ExCSS | desktop | MIT: licenses/ExCSS-MIT.txt |
| HtmlAgilityPack 1.11.42 | https://github.com/zzzprojects/html-agility-pack | desktop | MIT: licenses/HtmlAgilityPack-MIT.txt |
| YamlDotNet 18.1.0 and 16.3.0 | https://github.com/aaubry/YamlDotNet | desktop | MIT: licenses/YamlDotNet-MIT.txt |
| Npgsql 10.0.3 | https://github.com/npgsql/npgsql | API | PostgreSQL: licenses/Npgsql-PostgreSQL.txt |
| Npgsql.EntityFrameworkCore.PostgreSQL 10.0.2 | https://github.com/npgsql/efcore.pg | API | PostgreSQL: licenses/Npgsql.EntityFrameworkCore.PostgreSQL-PostgreSQL.txt |
| OpenIddict 7.6.0 (OpenIddict.Abstractions, OpenIddict.Core, OpenIddict.AspNetCore, OpenIddict.EntityFrameworkCore, OpenIddict.EntityFrameworkCore.Models, OpenIddict.Server, OpenIddict.Server.AspNetCore, OpenIddict.Server.DataProtection, OpenIddict.Validation, OpenIddict.Validation.AspNetCore, OpenIddict.Validation.DataProtection, OpenIddict.Validation.ServerIntegration, OpenIddict.Validation.SystemNetHttp, OpenIddict.Client, OpenIddict.Client.AspNetCore, OpenIddict.Client.DataProtection, OpenIddict.Client.SystemIntegration, OpenIddict.Client.SystemNetHttp, OpenIddict.Client.WebIntegration) | https://github.com/openiddict/openiddict-core | API | Apache-2.0: licenses/Apache-2.0.txt |
| Microsoft.IdentityModel 8.19.2 (Microsoft.IdentityModel.Abstractions, Microsoft.IdentityModel.JsonWebTokens, Microsoft.IdentityModel.Logging, Microsoft.IdentityModel.Protocols, Microsoft.IdentityModel.Tokens) | https://github.com/AzureAD/azure-activedirectory-identitymodel-extensions-for-dotnet | API | MIT: licenses/IdentityModel-MIT.txt; upstream notices: licenses/IdentityModel-NOTICE.txt, licenses/IdentityModel-THIRD-PARTY-NOTICES.txt |
| Microsoft.OpenApi 2.0.0 | https://github.com/Microsoft/OpenAPI.NET | API | MIT: licenses/OpenAPI.NET-MIT.txt |
| OpenTelemetry .NET 1.15.3 (OpenTelemetry, OpenTelemetry.Api, OpenTelemetry.Api.ProviderBuilderExtensions, OpenTelemetry.Exporter.OpenTelemetryProtocol, OpenTelemetry.Extensions.Hosting) | https://github.com/open-telemetry/opentelemetry-dotnet | API | Apache-2.0: licenses/Apache-2.0.txt; upstream component notices: licenses/OpenTelemetry-THIRD-PARTY-NOTICES.txt |
| OpenTelemetry .NET Contrib (OpenTelemetry.Instrumentation.AspNetCore 1.15.2, OpenTelemetry.Instrumentation.Http 1.15.1, OpenTelemetry.Instrumentation.Runtime 1.15.1) | https://github.com/open-telemetry/opentelemetry-dotnet-contrib | API | Apache-2.0: licenses/Apache-2.0.txt |
| Polly 7.2.4 and 8.x (Polly, Polly.Core, Polly.Extensions, Polly.RateLimiting) | https://github.com/App-vNext/Polly | API | BSD-3-Clause: licenses/Polly-BSD-3-Clause.txt |
| Polly.Extensions.Http 3.0.0 | https://github.com/App-vNext/Polly.Extensions.Http | API | BSD-3-Clause: licenses/Polly.Extensions.Http-BSD-3-Clause.txt |

The container images are built from `mcr.microsoft.com/dotnet/aspnetcore` (API) and `nginx:alpine` (web)
base images, which carry their own license information; the `postgres` image is pulled unchanged.

## Not redistributed

The following are used only to build, test, orchestrate or run from source, and are not included in the
`.deb`, the WebAssembly bundle or the API image: the MCP server (`Corvida.Mcp`, run with `dotnet run`, and
its ModelContextProtocol and Microsoft.Extensions.Hosting packages), the Aspire AppHost
(Aspire.Hosting.\*, KubernetesClient, Grpc.\*, Google.Protobuf and their dependencies),
Avalonia.Diagnostics (Debug builds only), Microsoft.EntityFrameworkCore.Design, xunit, xunit.runner.visualstudio,
Microsoft.NET.Test.Sdk, coverlet.collector, Microsoft.AspNetCore.Mvc.Testing, Testcontainers.PostgreSql, and
the Windows/macOS-only native assets (Avalonia.Angle.Windows.Natives, SkiaSharp.NativeAssets.Win32/macOS,
HarfBuzzSharp.NativeAssets.Win32/macOS).

## Maintenance

When you add copied code or assets, or change the dependencies, record the source, version,
author and license here, and update the license copies in `licenses/`. Before a release, check
the publish outputs (`dotnet publish src/Corvida/Corvida.csproj -c Release -r linux-x64 --self-contained`,
`dotnet publish src/Corvida.Web/Corvida.Web.csproj -c Release`, `dotnet publish src/Corvida.Api/Corvida.Api.csproj -c Release`)
for new packages and native libraries.
