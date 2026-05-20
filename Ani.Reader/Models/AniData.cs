using System.Collections.ObjectModel;

using Ico.Reader;
using Ico.Reader.Data;
using Ico.Reader.Data.Source;

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

    internal AniData(AniEntry aniEntry, AniOriginFileType origin, IDataSource dataSource, IcoReader icoReader)
    {
        Name = aniEntry.Id.ToString();
        Origin = origin;
        DataSource = dataSource;
        Reader = icoReader;
        _aniEntry = aniEntry ?? throw new ArgumentNullException(nameof(aniEntry));
        CalculateFrames();
        InitializeIcoDatas();
    }

    /// <summary>
    /// Finds an image reference within an ICO file that matches the given animation properties.
    /// </summary>
    /// <param name="icoData">The ICO data containing multiple image references.</param>
    /// <param name="aniInfo">The animation frame information, specifying dimensions and bit depth.</param>
    /// <returns>The best matching <see cref="ImageReference"/> from the ICO data.</returns>
    public ImageReference FindByAnimationInformation(IcoData icoData, AnimationInformation aniInfo)
    {
        return icoData.ImageReferences.FirstOrDefault(x =>
            x.BitCount == aniInfo.BitCount && x.Width == aniInfo.Width && x.Height == aniInfo.Height)
                ?? icoData.ImageReferences.First();
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
            await icoData.SaveImageAsync(imageReference, fileName);
        }
    }

    /// <summary>
    /// Returns the index of the <see cref="AnimationInformation"/> with the highest pixel complexity,
    /// calculated as <c>BitCount * Width * Height</c>.
    /// </summary>
    public int PreferredAnimationIndex()
    {
        if (Animations is null || Animations.Count == 0)
            return -1;

        var bestIndex = 0;
        long bestScore = 0;
        for (var i = 0; i < Animations.Count; i++)
        {
            var a = Animations[i];
            long score = (long)a.BitCount * a.Width * a.Height;
            if (score > bestScore)
            {
                bestScore = score;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

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
            var pos = 0;
            foreach (var ico in _icos)
            {
                try
                {
                    var imageReference = FindByAnimationInformation(ico, animationInfo);

                    hotspotInfo.Add(new FrameHotspot()
                    {
                        FramePosition = pos,
                        HotspotX = imageReference.HotspotX,
                        HotspotY = imageReference.HotspotY
                    });
                }
                catch
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

    private List<AnimationInformation> GetAnimations(IcoData icoData)
    {
        var currentAnimations = icoData.ImageReferences
            .Select(x => new AnimationInformation
            {
                BitCount = x.BitCount,
                Height = x.Height,
                Width = x.Width
                //HotspotsX = x.HotspotX,
                //HotspotsY = x.HotspotY
            });

        return [.. currentAnimations];
    }

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
