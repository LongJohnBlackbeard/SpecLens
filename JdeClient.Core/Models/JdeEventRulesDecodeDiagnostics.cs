using JdeClient.Core.Interop;

namespace JdeClient.Core.Models;

/// <summary>
/// Diagnostics captured while decoding event rules specs.
/// </summary>
public sealed class JdeEventRulesDecodeDiagnostics
{
    public int Sequence { get; set; }
    public int BlobSize { get; set; }
    public string HeadHex { get; set; } = string.Empty;
    public bool RawLooksLikeGbrSpec { get; set; }
    public UnpackAttempt RawLittleEndian { get; set; } = UnpackAttempt.Empty;
    public UnpackAttempt RawBigEndian { get; set; } = UnpackAttempt.Empty;
    public B733UnpackAttempt RawB733LittleEndian { get; set; } = B733UnpackAttempt.Empty;
    public B733UnpackAttempt RawB733BigEndian { get; set; } = B733UnpackAttempt.Empty;
    public bool Uncompressed { get; set; }
    public int UncompressedSize { get; set; }
    public bool UncompressedLooksLikeGbrSpec { get; set; }
    public UnpackAttempt UncompressedLittleEndian { get; set; } = UnpackAttempt.Empty;
    public UnpackAttempt UncompressedBigEndian { get; set; } = UnpackAttempt.Empty;
    public B733UnpackAttempt UncompressedB733LittleEndian { get; set; } = B733UnpackAttempt.Empty;
    public B733UnpackAttempt UncompressedB733BigEndian { get; set; } = B733UnpackAttempt.Empty;

    public sealed class UnpackAttempt
    {
        public JdeStructures.JdeUnpackSpecStatus Status { get; set; }
        public int UnpackedLength { get; set; }
        public bool LooksLikeGbrSpec { get; set; }
        public string? Error { get; set; }

        public static readonly UnpackAttempt Empty = new();
    }

    public sealed class B733UnpackAttempt
    {
        public JdeStructures.JdeB733UnpackSpecStatus Status { get; set; }
        public int UnpackedLength { get; set; }
        public bool LooksLikeGbrSpec { get; set; }
        public uint CodePage { get; set; }
        public int OsType { get; set; }
        public string? Error { get; set; }

        public static readonly B733UnpackAttempt Empty = new();
    }
}
