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
    /// The number of steps in <see cref="Frames"/>. A frame shown at several steps counts once per step.
    /// </summary>
    public int TotalFrames => Frames?.Count ?? 0;

    /// <summary>
    /// The frame rate of the animation, calculated as:
    /// <para> FrameRate (fps) = 60 / DisplayRate </para>
    /// <para> If the display rate is 0, a default of 1 fps is used. </para>
    /// </summary>
    public float FrameRate => _aniEntry.Header.DisplayRate > 0 ? 60f / _aniEntry.Header.DisplayRate : 1f;

    /// <summary>
    /// The steps of the animation in playing order, each naming the frame it shows and for how long.
    /// <para> <see cref="LoadsOnWindows"/> describes how the steps follow from the file. </para>
    /// </summary>
    public ReadOnlyCollection<FrameInformation> Frames { get; private set; } = null!;

    /// <summary>
    /// The sizes the animation can be played back at, one entry per size.
    /// <para>
    /// Lists the sizes every frame holding an image has, ignoring frames without one. When those frames share no size,
    /// it lists the sizes of the first of them instead, and <see cref="FindByAnimationInformation"/> gives each other
    /// frame the image closest in size.
    /// </para>
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

    /// <summary>
    /// Whether Windows loads this animation, in which case <see cref="Frames"/> holds exactly the steps Windows plays.
    /// <para> Windows loads an animation when all of the following hold: </para>
    /// <list type="bullet">
    /// <item><description>The chunks are ordered and sized as <see cref="AniEntry.ChunkLayoutLoadsOnWindows"/> describes.</description></item>
    /// <item><description>The <c>anih</c> header states a size of 36 bytes.</description></item>
    /// <item><description>The header declares at least one frame, and no more frames than the file stores. Stored frames past the declared count are ignored.</description></item>
    /// <item><description>Each declared frame holds at least one image whose data lies within the frame.</description></item>
    /// <item><description>The header declares at least one step.</description></item>
    /// <item><description>A <c>seq </c> chunk holds one entry per step, each naming a declared frame, whether or not the sequence flag is set. Without one, the steps show the frames in order, so there can be no more steps than declared frames.</description></item>
    /// <item><description>A <c>rate</c> chunk holds one entry per step. Without one, the header's display rate is at least 1.</description></item>
    /// </list>
    /// <para>
    /// Windows refuses any other animation, which is then read as far as it can be: there is a step for each <c>seq </c>
    /// entry, or for each stored frame; each step lasts its <c>rate</c> entry, or the display rate; a <c>seq </c> entry
    /// past the last frame shows the last frame; and a step whose frame holds no readable image stays in
    /// <see cref="Frames"/>, with <see cref="GetFrameBytes"/> returning <see langword="null"/> for it.
    /// </para>
    /// </summary>
    public bool LoadsOnWindows { get; }

    /// <summary>
    /// Whether at least one step shows a frame that holds an image.
    /// </summary>
    internal bool ShowsAnImage { get; }

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

        var frameImages = aniEntry.Frames.Select(frame => new Lazy<IcoData?>(() => ReadFrameImage(frame))).ToArray();
        bool FrameHoldsImage(int frameIndex) => HoldsImage(frameImages[frameIndex].Value, aniEntry.Frames[frameIndex]);

        LoadsOnWindows = AniPlayback.LoadsOnWindows(aniEntry, FrameHoldsImage);
        var steps = AniPlayback.Steps(aniEntry, LoadsOnWindows);
        ShowsAnImage = steps.Any(step => FrameHoldsImage(step.FrameIndex));

        CalculateFrames(steps);
        InitializeIcoDatas([.. steps.Select(step => frameImages[step.FrameIndex].Value)]);
    }

    /// <summary>
    /// Finds an image reference within an ICO file that matches the given animation properties.
    /// <para>
    /// Size takes priority over bit depth: a frame may store the requested size at a different depth than the
    /// variant reports, and returning that image is always better than returning a different size.
    /// </para>
    /// <para>
    /// A frame without an image of the requested size gives the image closest to it, counting the difference in width
    /// plus the difference in height, and the larger of two equally close sizes.
    /// </para>
    /// </summary>
    /// <param name="icoData">The ICO data containing multiple image references.</param>
    /// <param name="aniInfo">The animation frame information, specifying dimensions and bit depth.</param>
    /// <returns>The best matching <see cref="ImageReference"/> from the ICO data.</returns>
    /// <exception cref="InvalidOperationException">The ICO data holds no image.</exception>
    public ImageReference FindByAnimationInformation(IcoData icoData, AnimationInformation aniInfo)
    {
        var exactMatch = icoData.ImageReferences.FirstOrDefault(x =>
            x.BitCount == aniInfo.BitCount && x.Width == aniInfo.Width && x.Height == aniInfo.Height);
        if (exactMatch is not null)
            return exactMatch;

        return icoData.ImageReferences
            .OrderBy(x => Math.Abs(x.Width - aniInfo.Width) + Math.Abs(x.Height - aniInfo.Height))
            .ThenByDescending(x => (long)x.Width * x.Height)
            .ThenByDescending(EffectiveBitCount)
            .First();
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
            try
            {
                await _icoExporter.SaveImageAsync(icoData, imageReference, fileName);
            }
            catch (Exception exception) when (IsUndecodableImage(exception))
            {
                continue;
            }
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
    /// The PNG encoded image, or <see langword="null"/> if the frame's data cannot be read as icon or cursor data, holds
    /// no image, or holds an image that cannot be decoded.
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
        try
        {
            return await icoData.GetImageAsync(imageReference);
        }
        catch (Exception exception) when (IsUndecodableImage(exception))
        {
            return null;
        }
    }

    private static bool IsUndecodableImage(Exception exception) => exception is EndOfStreamException or InvalidDataException;

    private IcoData? ReadFrameImage(AniFrameReference frame)
    {
        using var sourceStream = DataSource.GetStream();
        using var frameStream = frame.GetFrameStream(sourceStream);
        return Reader.Read(frameStream);
    }

    private static bool HoldsImage(IcoData? icoData, AniFrameReference frame)
        => icoData is not null && icoData.ImageReferences.Any(image => (long)image.Offset + image.Size <= frame.Size);

    private void InitializeIcoDatas(IReadOnlyList<IcoData?> stepImages)
    {
        for (var i = 0; i < Frames.Count; i++)
        {
            var icoData = stepImages[i];
            if (icoData is not null && icoData.ImageReferences.Count > 0)
                Frames[i].VariationDetails = icoData.ImageReferences.Select(ToVariation).ToList();
        }

        var animationInfos = SharedSizes(stepImages);
        foreach (var animationInfo in animationInfos)
            animationInfo.FrameHotspots = stepImages.Select((icoData, position) => HotspotOf(icoData, animationInfo, position)).ToList();

        Animations = new ReadOnlyCollection<AnimationInformation>(animationInfos);
    }

    private static List<AnimationInformation> SharedSizes(IEnumerable<IcoData?> stepImages)
    {
        var framesWithImages = stepImages.OfType<IcoData>().Where(icoData => icoData.ImageReferences.Count > 0).ToList();
        if (framesWithImages.Count == 0)
            return [];

        var firstFrameSizes = GetAnimations(framesWithImages[0]);
        var sharedSizes = firstFrameSizes
            .Where(size => framesWithImages.All(icoData => icoData.ImageReferences.Any(image => image.Width == size.Width && image.Height == size.Height)))
            .ToList();

        return sharedSizes.Count > 0 ? sharedSizes : firstFrameSizes;
    }

    private static FrameVariationInformation ToVariation(ImageReference image) => new()
    {
        Width = image.Width,
        Height = image.Height,
        BitCount = image.BitCount,
        HotspotX = image.HotspotX,
        HotspotY = image.HotspotY
    };

    private FrameHotspot HotspotOf(IcoData? icoData, AnimationInformation animationInfo, int position)
    {
        var imageReference = icoData is null || icoData.ImageReferences.Count == 0
            ? null
            : FindByAnimationInformation(icoData, animationInfo);

        return new FrameHotspot
        {
            FramePosition = position,
            HotspotX = imageReference?.HotspotX ?? 0,
            HotspotY = imageReference?.HotspotY ?? 0
        };
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

    private void CalculateFrames(IReadOnlyList<(int FrameIndex, uint Rate)> steps)
    {
        var frameList = new List<FrameInformation>(steps.Count);
        var currentStart = TimeSpan.Zero;

        for (var i = 0; i < steps.Count; i++)
        {
            var duration = TimeSpan.FromSeconds(steps[i].Rate / 60.0);

            frameList.Add(new FrameInformation
            {
                Position = i,
                Start = currentStart,
                Duration = duration,
                FrameReference = _aniEntry.Frames[steps[i].FrameIndex]
            });

            currentStart += duration;
        }

        Frames = new ReadOnlyCollection<FrameInformation>(frameList);
        TotalAnimationDuration = currentStart;
    }
}
