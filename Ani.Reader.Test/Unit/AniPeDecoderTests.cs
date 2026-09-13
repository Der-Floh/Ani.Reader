using System.Globalization;

using Ani.Reader.Decoder;
using Ani.Reader.Models;
using Ani.Reader.Test.Fixtures;

using Ico.Reader.PeDecoder;
using Ico.Reader.PeDecoder.Models;

using Xunit;

namespace Ani.Reader.Test.Unit;

/// <summary>
/// An executable or DLL can carry several animated cursors, and one that cannot be read must not cost the others.
/// </summary>
public sealed class AniPeDecoderTests
{
    private static readonly byte[] Fixture = TestFiles.BytesOf(TestFiles.AnimatedThreeFrame);

    [Fact]
    public void GetDecodedAniResult_SkipsAnUnreadableResourceAndKeepsTheRest()
    {
        var image = new FakePeImage(Fixture, Fixture[..40], Fixture);
        using var stream = new MemoryStream(image.Bytes);

        var result = new AniPeDecoder(image, new AniDecoder()).GetDecodedAniResult(stream);

        Assert.NotNull(result);
        Assert.Equal(AniOriginFileType.Dll, result.OriginFileType);
        Assert.Equal(new[] { 1, 3 }, result.Entries.Select(entry => entry.Id));
        Assert.Equal(new[] { image.OffsetOf(1), image.OffsetOf(3) }, result.Entries.Select(entry => entry.EntryOffset));
        Assert.All(result.Entries, entry => Assert.Equal(3, entry.Frames.Count));
    }

    [Fact]
    public void Read_ReturnsEveryReadableAnimationOfTheDll()
    {
        var image = new FakePeImage(Fixture, Fixture[..40], Fixture);
        var configuration = new AniReaderConfiguration { AniPeDecoder = new AniPeDecoder(image, new AniDecoder()) };

        var animations = new AniReader(configuration).Read(image.Bytes);

        Assert.NotNull(animations);
        Assert.Equal(2, animations.Length);
        Assert.All(animations, aniData =>
        {
            Assert.Equal(AniOriginFileType.Dll, aniData.Origin);
            Assert.Equal(3, aniData.TotalFrames);
            Assert.NotEmpty(aniData.Animations);
        });
    }

    /// <summary>
    /// A PE image reduced to what <see cref="AniPeDecoder"/> reads: an MZ signature, then RT_ANICURSOR resources and a
    /// resource tree pointing at them. The resource section's address differs from its file offset, so a resource
    /// only resolves when its address is translated.
    /// </summary>
    private sealed class FakePeImage : IPeDecoder
    {
        private const uint SectionAddress = 0x1000;
        private const int StubSize = 64;

        private readonly ResourceDirectory _root;
        private readonly List<long> _offsets = [];

        public FakePeImage(params byte[][] resources)
        {
            using var bytes = new MemoryStream();
            bytes.Write([(byte)'M', (byte)'Z']);
            bytes.SetLength(StubSize);
            bytes.Position = StubSize;

            var resourceDirectories = new List<ResourceDirectory>();
            for (var i = 0; i < resources.Length; i++)
            {
                var id = (uint)(i + 1);
                _offsets.Add(bytes.Position);
                resourceDirectories.Add(new ResourceDirectory
                {
                    Name = id.ToString(CultureInfo.InvariantCulture),
                    Level = 3,
                    DataEntries = [new ResourceDataEntry { ID = id, DataRVA = SectionAddress + (uint)bytes.Position, Size = (uint)resources[i].Length }],
                });
                bytes.Write(resources[i]);
            }

            Bytes = bytes.ToArray();
            _root = new ResourceDirectory
            {
                Name = "Root",
                Level = 1,
                Section = new SectionHeader { Name = ".rsrc", VirtualAddress = SectionAddress, SizeOfRawData = (uint)Bytes.Length },
                Subdirectories = [new ResourceDirectory { Name = ResourceType.RT_ANICURSOR.ToString(), Level = 2, Subdirectories = resourceDirectories }],
            };
        }

        public byte[] Bytes { get; }

        public long OffsetOf(int resourceId) => _offsets[resourceId - 1];

        public MzHeader DecodeMZ(Stream stream) => new PeFileDecoder().DecodeMZ(stream);

        public PeHeader DecodePE(Stream stream) => new() { Characteristics = Characteristics.ImageFileDLL };

        public ResourceDirectory? DecodeResourceDirectory(Stream stream, PeHeader peHeader) => _root;

        public bool IsPeFormat(MzHeader mzHeader) => mzHeader.HasMzSignature;

        public bool IsPeFormat(Stream stream) => IsPeFormat(DecodeMZ(stream));
    }
}
