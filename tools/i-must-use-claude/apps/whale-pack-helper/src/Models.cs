using System;
using System.Collections.Generic;
using System.Threading;

namespace WhalePack {
    public enum ProcessingMode { CompressVideo, ResizeImage, SourceArchive, Original, Exclude }
    public sealed class SourceEntry {
        public string FullPath = "";
        public string RelativePath = "";
        public long Length;
        public string Reason = "";
        public bool Excluded;
        public bool Sensitive;
        public bool UserIncluded;
        public bool IsDirectory;
        public bool CannotInclude;
    }
    public sealed class InputItem {
        public string Path = "";
        public string DisplayName = "";
        public bool IsDirectory;
        public long OriginalBytes;
        public long EstimatedBytes;
        public ProcessingMode Mode;
        public List<SourceEntry> Entries = new List<SourceEntry>();
        public List<string> Warnings = new List<string>();
    }
    public sealed class HandoffData {
        public string Project = "";
        public string Goal = "";
        public string Progress = "";
        public string Verified = "";
        public string Unverified = "";
        public string Next = "";
    }
    public sealed class AppSettings {
        public string Language = "zh";
        public string OutputDirectory = "";
        public string FfmpegPath = "";
        public bool WriteHandoff = true;
        public HandoffData Handoff = new HandoffData();
    }
    public sealed class PackProgress {
        public string Message = "";
        public int Percent;
    }
    public sealed class OutputFile {
        public string Name = "";
        public string FullPath = "";
        public long Length;
    }
    public sealed class PackageResult {
        public string OutputDirectory = "";
        public List<OutputFile> Files = new List<OutputFile>();
        public List<string> Warnings = new List<string>();
        public List<string> MergeInstructions = new List<string>();
        public string CopyText = "";
    }
    public sealed class PreparedFile {
        public string FullPath = "";
        public string ArchivePath = "";
    }
    public interface IMediaProcessor {
        List<PreparedFile> Prepare(InputItem item, string workDirectory, AppSettings settings, Action<PackProgress> progress, CancellationToken cancellation);
    }
    public static class Limits {
        public const long MaxOutputBytes = 29000000;
        public const long MaxSourceFileBytes = 20000000;
        public const long ZipPayloadBytes = 28000000;
    }
}
