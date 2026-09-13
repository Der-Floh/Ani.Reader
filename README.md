# ![Ani.Reader icon](https://raw.githubusercontent.com/Der-Floh/Ani.Reader/main/Assets/icon-x64.png) Ani.Reader

[![NuGet Version](https://img.shields.io/nuget/vpre/Ani.Reader)](https://www.nuget.org/packages/Ani.Reader)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Ani.Reader)](https://www.nuget.org/packages/Ani.Reader)

**`Ani.Reader`** is a cross-platform library designed for extracting animated cursors from `.ani` **files**, as well as from **embedded resources** within `.exe` **and** `.dll` files.

## Installation

```sh
dotnet add package Ani.Reader
```

**Requirements**: .NET Standard 2.0 or later (compatible with .NET Framework 4.6.1+, .NET Core 2.0+, .NET 5+).

## Key Features

- **Platform-Independent Design**: Extracts animated cursors from ANI, EXE, and DLL files without relying on Windows-specific functions, making it fully cross-platform.
- **Supports ANI Files and PE Resources**: Reads animated cursors from standalone `.ani` files as well as embedded resources within executables and DLLs.
- **Efficient Memory Usage**: Implements lazy frame loading — individual frame image data is not decoded until it is needed.
- **Multiple Animation Variants**: A single ANI source can contain variants at different sizes and bit depths, each accessible as a separate `AnimationInformation`.
- **Flexible Data Access**: Supports reading from file paths, byte arrays, and streams.
- **WebP Export**: Animated cursors can be exported as animated WebP images via the built-in `WebPCreator` extension.

## Getting Started

### Reading aniData

```cs
var aniReader = new AniReader();

// Reading from a file path (most memory-efficient)
AniData[]? aniFromPath = aniReader.Read("path/to/cursor.ani");
AniData[]? aniFromDll  = aniReader.Read("path/to/user32.dll");
AniData[]? aniFromExe  = aniReader.Read("path/to/app.exe");

// Reading from a byte array
byte[] aniBytes = File.ReadAllBytes("path/to/cursor.ani");
AniData[]? aniFromBytes = aniReader.Read(aniBytes);

// Reading from a stream (copies the stream for independent access)
using var stream = File.OpenRead("path/to/cursor.ani");
AniData[]? aniFromStream = aniReader.Read(stream: stream, copyStream: true);

// Reading from a stream without copying (as efficient as direct file reading)
using (var streamOrigin = File.OpenRead("path/to/cursor.ani"))
{
    AniData[]? aniFromStreamDirect = aniReader.Read(stream: streamOrigin, copyStream: false);
    // ✅ This is as memory-efficient as reading directly from a file.
    // 🔴 WARNING: All frames must be accessed before closing the stream,
    // otherwise an error will occur.
}
```

- `copyStream: true` → The stream is **copied**, allowing access to frames even after the original stream is closed.
- `copyStream: false` → The stream is **used directly**, making it as **memory-efficient as reading from a file**, but the stream must be seekable and must remain open while accessing frames.

> **Note:** All `Read()` overloads return `null` if the file does not exist, the format is unrecognised, or the data cannot be parsed.

### Working with AniData

`Read()` returns an `AniData[]` because a single PE file can embed multiple animated cursors. Each `AniData` represents one animation and exposes its frames and animation variants.

#### AniData Properties

| Property                 | Type                                       | Description                                      |
| ------------------------ | ------------------------------------------ | ------------------------------------------------ |
| `Name`                   | `string`                                   | Derived from the file name or PE resource ID     |
| `Origin`                 | `AniOriginFileType`                        | `Executable`, `Dll`, or `Ani`                    |
| `TotalAnimationDuration` | `TimeSpan`                                 | Sum of all frame durations                       |
| `TotalFrames`            | `int`                                      | Number of steps in `Frames`                      |
| `FrameRate`              | `float`                                    | Frames per second (`60 / DisplayRate`)           |
| `Frames`                 | `ReadOnlyCollection<FrameInformation>`     | Ordered sequence of frame steps including timing |
| `Animations`             | `ReadOnlyCollection<AnimationInformation>` | Available size and bit-depth variants            |
| `LoadsOnWindows`         | `bool`                                     | Whether Windows loads this animation             |

An animation Windows loads plays exactly the steps Windows plays. One Windows refuses, such as a file whose `seq` chunk does not match its header, is still read as far as it can be, and `LoadsOnWindows` is `false`. The XML documentation of `LoadsOnWindows` lists the rules.

#### FrameInformation Properties

| Property         | Type                | Description                                       |
| ---------------- | ------------------- | ------------------------------------------------- |
| `Position`       | `int`               | Index of this step in the animation sequence      |
| `Start`          | `TimeSpan`          | Time offset when this frame begins                |
| `Duration`       | `TimeSpan`          | How long this frame is displayed                  |
| `FrameReference` | `AniFrameReference` | Raw byte offset and size within the source stream |

#### AnimationInformation Properties

| Property        | Type                 | Description                            |
| --------------- | -------------------- | -------------------------------------- |
| `Width`         | `int`                | Frame width in pixels                  |
| `Height`        | `int`                | Frame height in pixels                 |
| `BitCount`      | `int`                | Bit depth (e.g. 1, 4, 8, 24, 32)       |
| `FrameHotspots` | `List<FrameHotspot>` | Cursor hotspot position per frame step |

### Retrieving Frame Data

Each `AnimationInformation` describes one size/depth variant. Use it to retrieve the decoded PNG bytes of a specific frame:

```cs
foreach (var aniData in aniDatas)
{
    var animation = aniData.Animations[0];

    // Get the decoded PNG bytes for a single frame
    byte[]? frameBytes = await aniData.GetFrameBytes(animation, aniData.Frames[0]);
}
```

### Selecting the Preferred Animation Variant

`AniData.PreferredAnimationIndex()` returns the index of the `AnimationInformation` with the highest quality score, calculated as `BitCount × Width × Height`.

```cs
int preferredIndex = aniData.PreferredAnimationIndex();
var animation = aniData.Animations[preferredIndex];
```

### Saving / Exporting

#### Saving Frames as PNG

```cs
// Save all frames of an animation variant as individual PNG files
await aniData.SaveImages("output/", animation);
```

#### Exporting as Animated WebP

```cs
// Save as an animated WebP file
await aniData.SaveAsWebP("output/cursor.webp", animation);

// Get the animated WebP as a byte array
byte[]? webpBytes = await aniData.GetWebpBytes(animation);
```

## Configuration

By default, `AniReader` uses built-in decoders. You can supply a custom `AniReaderConfiguration` to override any part of the decoding pipeline:

```cs
var config = new AniReaderConfiguration
{
    AniDecoder   = new MyCustomAniDecoder(),
    AniPeDecoder = new MyCustomAniPeDecoder(),
    IcoReader    = new IcoReader()
};

var aniReader = new AniReader(config);
```

`AniReaderConfiguration` properties:

| Property       | Type            | Description                                                     |
| -------------- | --------------- | --------------------------------------------------------------- |
| `AniDecoder`   | `IAniDecoder`   | Decoder for parsing ANI RIFF data                               |
| `AniPeDecoder` | `IAniPeDecoder` | Decoder for extracting ANI resources from `.exe` / `.dll` files |
| `IcoReader`    | `IcoReader`     | ICO reader used to decode individual animation frames           |
| `IcoExporter`  | `IIcoExporter`  | ICO exporter used to write decoded frames to disk               |

## Dependency Injection Support

For applications utilizing Dependency Injection, `Ani.Reader` provides an extension method to seamlessly register its services with the DI container.

```cs
public void ConfigureServices(IServiceCollection services)
{
    services.AddAniReader();
}
```

## Dependencies

**`Ani.Reader`** is designed with minimal external dependencies to ensure lightweight integration into your projects.

- [Ico.Reader](https://www.nuget.org/packages/Ico.Reader/)
- [Magick.NET-Q16-AnyCPU](https://www.nuget.org/packages/Magick.NET-Q16-AnyCPU/) *(WebP export only)*
- [Microsoft.Extensions.DependencyInjection.Abstractions](https://www.nuget.org/packages/Microsoft.Extensions.DependencyInjection.Abstractions/)
