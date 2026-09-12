using System.Collections.ObjectModel;

using Ico.Reader;
using Ico.Reader.Data;
using Ico.Reader.Data.Source;
using Ico.Reader.Export;

namespace Ani.Reader.Models;
/// <summary>
/// Represents an ANI animation, containing frames, metadata, and extraction utilities.
/// </summary>
public class AniData
{
    /// <summary>
    /// The name of the ANI file, typically derived from its identifier.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// The origin of the ANI file (Executable, DLL, or ANI file).
    /// </summary>
    public AniOriginFileType Origin { get; }

    /// <summary>
    /// The total duration of the animation.
    /// </summary>
    public TimeSpan TotalAnimationDuration { get; private set; }

    /// <summary>
    /// The total number of frames in the animation.
    /// </summary>
    public int TotalFrames => Frames?.Count ?? 0;

    /// <summary>
    /// The frame rate of the animation, calculated as:
    /// <para> FrameRate (fps) = 60 / DisplayRate </para>
    /// <para> If the display rate is 0, a default of 1 fps is used. </para>
    /// </summary>
    public float FrameRate => _aniEntry.Header.DisplayRate > 0 ? 60f / _aniEntry.Header.DisplayRate : 1f;

    /// <summary>
    /// A read-only collection of frames in the animation.
    /// </summary>
    public ReadOnlyCollection<FrameInformation> Frames { get; private set; } = null!;

    /// <summary>
    /// A read-only collection of animation sequences.
    /// </summary>
    public ReadOnlyCollection<AnimationInformation> Animations { get; private set; } = null!;

    /// <summary>
    /// An ICO reader instance used to process and extract image frames.
    /// </summary>
    public IcoReader Reader { get; }

    /// <summary>
    /// The data source from which the ANI file is being read.
    /// </summary>
    public IDataSource DataSource { get; private set; }

    private IcoData[] _icos = null!;
    private readonly AniEntry _aniEntry;
    private readonly IIcoExporter _icoExporter;

    internal AniData(AniEntry aniEntry, AniOriginFileType origin, IDataSource dataSource, IcoReader icoReader, IIcoExporter icoExporter)
    {
        Name = aniEntry.Id.ToString();
        Origin = origin;
        DataSource = dataSource;
        Reader = icoReader;
        _icoExporter = icoExporter ?? throw new ArgumentNullException(nameof(icoExporter));
        _aniEntry = aniEntry ?? throw new ArgumentNullException(nameof(aniEntry));
        CalculateFrames();
        InitializeIcoDatas();
    }

    /// <summary>
    /// Finds an image reference within an ICO file that matches the given animation properties.
    /// <para>
    /// Size takes priority over bit depth: a frame may store the requested size at a different depth than the
    /// variant reports, and returning that image is always better than returning a different size.
    /// </para>
    /// </summary>
    /// <param name="icoData">The ICO data containing multiple image references.</param>
    /// <param name="aniInfo">The animation frame information, specifying dimensions and bit depth.</param>
    /// <returns>The best matching <see cref="ImageReference"/> from the ICO data.</returns>
    public ImageReference FindByAnimationInformation(IcoData icoData, AnimationInformation aniInfo)
    {
        var exactMatch = icoData.ImageReferences.FirstOrDefault(x =>
            x.BitCount == aniInfo.BitCount && x.Width == aniInfo.Width && x.Height == aniInfo.Height);
        if (exactMatch is not null)
            return exactMatch;

        var sizeMatch = icoData.ImageReferences
            .Where(x => x.Width == aniInfo.Width && x.Height == aniInfo.Height)
            .OrderByDescending(EffectiveBitCount)
            .FirstOrDefault();

        return sizeMatch ?? icoData.ImageReferences.First();
    }

    /// <summary>
    /// Saves the extracted frames of the animation as PNG images in the specified directory.
    /// </summary>
    /// <param name="directoryPath">The directory in which to save the frames.</param>
    /// <param name="aniInfo">The animation information used for frame extraction.</param>
    public async Task SaveImages(string directoryPath, AnimationInformation aniInfo)
    {
        var aniDir = Path.Combine(directoryPath, Name);
        if (!Directory.Exists(aniDir))
            Directory.CreateDirectory(aniDir);

        foreach (var frame in Frames)
        {
            var subDir = $"{aniInfo.Width}x{aniInfo.Height}_{aniInfo.BitCount}_bit";
            var subPath = Path.Combine(aniDir, subDir);
            if (!Directory.Exists(subPath))
                Directory.CreateDirectory(subPath);

            var frameChunk = frame.FrameReference.GetFrameStream(DataSource.GetStream());
            var icoData = Reader.Read(frameChunk);
            if (icoData is null)
                continue;

            if (icoData.ImageReferences.Count == 0)
                continue;

            var imageReference = FindByAnimationInformation(icoData, aniInfo);
            var fileName = Path.Combine(subPath, $"frame_{frame.Position} ({imageReference.Width}x{imageReference.Height} {imageReference.BitCount} bit).png");
            await _icoExporter.SaveImageAsync(icoData, imageReference, fileName);
        }
    }

    /// <summary>
    /// Returns the index of the highest quality <see cref="AnimationInformation"/>, scoring pixel area and color
    /// bit depth relative to the best value present and combining them using the supplied weights.
    /// <para>
    /// The weights are a ratio and are normalized internally, so 2 and 1 rank identically to 0.667 and 0.333.
    /// The default favors size over color depth. Because both terms are relative to the variants of this
    /// animation, a score is only meaningful within that set.
    /// </para>
    /// </summary>
    /// <param name="areaWeight">The relative importance of the pixel area.</param>
    /// <param name="colorBitWeight">The relative importance of the color bit depth.</param>
    /// <returns>The index of the preferred variant, or -1 if this animation has no variants.</returns>
    public int PreferredAnimationIndex(double areaWeight = 2, double colorBitWeight = 1)
        => BestByQuality(Animations, areaWeight, colorBitWeight);

    internal static int BestByQuality(IReadOnlyList<AnimationInformation>? animations, double areaWeight, double colorBitWeight)
    {
        if (areaWeight < 0)
            throw new ArgumentOutOfRangeException(nameof(areaWeight), areaWeight, "Weights cannot be negative.");
        if (colorBitWeight < 0)
            throw new ArgumentOutOfRangeException(nameof(colorBitWeight), colorBitWeight, "Weights cannot be negative.");

        var weightSum = areaWeight + colorBitWeight;
        if (weightSum <= 0)
            throw new ArgumentException("At least one weight must be greater than zero.", nameof(areaWeight));

        if (animations is null || animations.Count == 0)
            return -1;

        var maxArea = animations.Max(a => (long)a.Width * a.Height);
        var maxBitCount = animations.Max(a => a.BitCount);

        var bestIndex = 0;
        var bestScore = double.NegativeInfinity;
        for (var i = 0; i < animations.Count; i++)
        {
            var animation = animations[i];
            var areaRatio = maxArea > 0 ? (double)((long)animation.Width * animation.Height) / maxArea : 0;
            var bitRatio = maxBitCount > 0 ? (double)animation.BitCount / maxBitCount : 0;
            var score = ((areaWeight * areaRatio) + (colorBitWeight * bitRatio)) / weightSum;

            if (score > bestScore)
            {
                bestScore = score;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    /// <summary>
    /// Decodes one frame and returns its image at the given size, encoded as PNG.
    /// <para> The image is chosen with <see cref="FindByAnimationInformation"/>. </para>
    /// </summary>
    /// <param name="aniInfo">The size to take from the frame, one of <see cref="Animations"/>.</param>
    /// <param name="frame">The frame to decode, one of <see cref="Frames"/>.</param>
    /// <returns>
    /// The PNG encoded image, or <see langword="null"/> if the frame's data cannot be read as icon or cursor data or
    /// holds no image.
    /// </returns>
    public async Task<byte[]?> GetFrameBytes(AnimationInformation aniInfo, FrameInformation frame)
    {
        var frameChunk = frame.FrameReference.GetFrameStream(DataSource.GetStream());
        var icoData = Reader.Read(frameChunk);
        if (icoData is null)
            return null;

        if (icoData.ImageReferences.Count == 0)
            return null;

        var imageReference = FindByAnimationInformation(icoData, aniInfo);
        return await icoData.GetImageAsync(imageReference);
    }

    private void InitializeIcoDatas()
    {
        var animationInfos = new List<AnimationInformation>();
        _icos = new IcoData[TotalFrames];

        for (var i = 0; i < Frames.Count; i++)
        {
            var frame = Frames[i];
            using var frameBaseStream = DataSource.GetStream();
            using var frameChunk = frame.FrameReference.GetFrameStream(frameBaseStream);
            var icoData = Reader.Read(frameChunk) ?? throw new Exception($"The frame at position {frame.Position} could not be read.");

            if (icoData.ImageReferences.Count == 0)
            {
                // Invalid Frame
            }
            else
            {
                var variations = new List<FrameVariationInformation>();
                foreach (var imageRef in icoData.ImageReferences)
                {
                    variations.Add(new FrameVariationInformation()
                    {
                        Height = imageRef.Height,
                        Width = imageRef.Width,
                        BitCount = imageRef.BitCount,
                        HotspotX = imageRef.HotspotX,
                        HotspotY = imageRef.HotspotY
                    });
                }

                frame.VariationDetails = variations;
            }

            var currentAnimations = GetAnimations(icoData);
            if (animationInfos.Count == 0)
            {
                animationInfos.AddRange(currentAnimations);
            }
            else
            {
                for (var aniIndex = animationInfos.Count - 1; aniIndex >= 0; aniIndex--)
                {
                    var animation = animationInfos[aniIndex];
                    if (!currentAnimations.Contains(animation))
                    {
                        animationInfos.RemoveAt(aniIndex);
                    }
                }
            }

            _icos[i] = icoData;
        }

        foreach (var animationInfo in animationInfos)
        {
            var hotspotInfo = new List<FrameHotspot>();
            for (var pos = 0; pos < _icos.Length; pos++)
            {
                try
                {
                    var imageReference = FindByAnimationInformation(_icos[pos], animationInfo);

                    hotspotInfo.Add(new FrameHotspot()
                    {
                        FramePosition = pos,
                        HotspotX = imageReference.HotspotX,
                        HotspotY = imageReference.HotspotY
                    });
                }
                catch (InvalidOperationException)
                {
                    hotspotInfo.Add(new FrameHotspot()
                    {
                        FramePosition = pos,
                        HotspotX = 0,
                        HotspotY = 0,
                    });
                }
            }

            animationInfo.FrameHotspots = hotspotInfo;
        }

        Animations = new ReadOnlyCollection<AnimationInformation>(animationInfos);
    }

    private static List<AnimationInformation> GetAnimations(IcoData icoData)
    {
        var currentAnimations = icoData.ImageReferences
            .GroupBy(x => (x.Width, x.Height))
            .Select(group => group.OrderByDescending(EffectiveBitCount).First())
            .Select(x => new AnimationInformation
            {
                BitCount = EffectiveBitCount(x),
                Height = x.Height,
                Width = x.Width
            });

        return [.. currentAnimations];
    }

    /// <summary>
    /// The color depth a consumer of this image actually gets.
    /// <para>
    /// PNG-compressed entries are reported at their storage depth, which for a palettized image says nothing
    /// about the artwork it encodes: a two-color cursor stored as a 1-bit palette loses nothing. Icon and
    /// cursor formats require PNG entries to be 32-bit ARGB, so they are ranked as such.
    /// </para>
    /// </summary>
    private static int EffectiveBitCount(ImageReference imageReference)
        => imageReference.Format == IcoImageFormat.Png ? 32 : imageReference.BitCount;

    private void CalculateFrames()
    {
        var frameList = new List<FrameInformation>();
        var currentStart = TimeSpan.Zero;

        var frameSequence = _aniEntry.FrameSequence;
        if (frameSequence.Count == 0)
        {
            frameSequence = Enumerable.Range(0, _aniEntry.Frames.Count).Select(i => (uint)i).ToList();
        }

        for (var i = 0; i < frameSequence.Count; i++)
        {
            var frameIndex = (int)frameSequence[i];
            var frameDuration = (_aniEntry.FrameRates.Count > frameIndex) ? _aniEntry.FrameRates[frameIndex] : _aniEntry.Header.DisplayRate;
            var duration = TimeSpan.FromSeconds(frameDuration / 60.0);

            frameList.Add(new FrameInformation
            {
                Position = i,
                Start = currentStart,
                Duration = duration,
                FrameReference = _aniEntry.Frames[frameIndex]
            });

            currentStart += duration;
        }

        Frames = new ReadOnlyCollection<FrameInformation>(frameList);
        TotalAnimationDuration = Frames.LastOrDefault()?.Start + Frames.LastOrDefault()?.Duration ?? TimeSpan.Zero;
    }
}
