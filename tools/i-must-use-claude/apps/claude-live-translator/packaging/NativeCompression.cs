using System;
using System.IO;
using System.Runtime.InteropServices;

namespace DafeiyuPortable {
    // Windows 10/11 already provide Cabinet.dll; no external unpacker or download.
    // Buffer mode preserves the block size. Never trust the native size query alone.
    // https://learn.microsoft.com/windows/win32/api/compressapi/nf-compressapi-decompress
    public static class NativeCompression {
        public const long MaximumArchiveBytes=256L*1024*1024;
        public const long MaximumPayloadBytes=128L*1024*1024;
        private const int InsufficientBuffer=122;
        private const uint Lzms=5;

        public static byte[] Compress(byte[] input) {
            if(input==null||input.LongLength==0||input.LongLength>MaximumArchiveBytes)throw new InvalidDataException("Application archive exceeds the compression size limit.");
            IntPtr handle;
            Require(CreateCompressor(Lzms,IntPtr.Zero,out handle),"Create compressor");
            try {
                uint blockSize=64*1024*1024;
                Require(SetCompressorInformation(handle,1,ref blockSize,new UIntPtr(4)),"Set compression block size");
                UIntPtr required,written;
                bool sized=CompressNative(handle,input,new UIntPtr((uint)input.Length),null,UIntPtr.Zero,out required);
                int error=Marshal.GetLastWin32Error();
                if(!sized&&error!=InsufficientBuffer)throw Failure("Measure compressed archive",error);
                ulong length=required.ToUInt64();
                if(length==0||length>(ulong)MaximumArchiveBytes+1024*1024)throw new InvalidDataException("Invalid compressed buffer size.");
                byte[] buffer=new byte[checked((int)length)];
                Require(CompressNative(handle,input,new UIntPtr((uint)input.Length),buffer,new UIntPtr((uint)buffer.Length),out written),"Compress archive");
                ulong actual=written.ToUInt64();
                if(actual==0||actual>(ulong)MaximumPayloadBytes||actual>(ulong)buffer.Length)throw new InvalidDataException("Compressed application exceeds the payload size limit.");
                Array.Resize(ref buffer,checked((int)actual));return buffer;
            }finally { CloseCompressor(handle); }
        }

        public static byte[] Decompress(byte[] compressed,long expectedBytes) {
            if(compressed==null||compressed.LongLength==0||compressed.LongLength>MaximumPayloadBytes||expectedBytes<=0||expectedBytes>MaximumArchiveBytes)
                throw new InvalidDataException("Invalid application payload size.");
            IntPtr handle;
            Require(CreateDecompressor(Lzms,IntPtr.Zero,out handle),"Create decompressor");
            try {
                UIntPtr required,written;
                bool sized=DecompressNative(handle,compressed,new UIntPtr((uint)compressed.Length),null,UIntPtr.Zero,out required);
                int error=Marshal.GetLastWin32Error();
                if(!sized&&error!=InsufficientBuffer)throw Failure("Read compressed archive",error);
                if(required.ToUInt64()!=(ulong)expectedBytes)throw new InvalidDataException("Application archive size does not match its build metadata.");
                byte[] buffer=new byte[checked((int)expectedBytes)];
                Require(DecompressNative(handle,compressed,new UIntPtr((uint)compressed.Length),buffer,new UIntPtr((uint)buffer.Length),out written),"Decompress archive");
                if(written.ToUInt64()!=(ulong)expectedBytes)throw new InvalidDataException("Application archive was truncated.");
                return buffer;
            }finally { CloseDecompressor(handle); }
        }
        private static InvalidDataException Failure(string operation,int code) { return new InvalidDataException(operation+" failed (Windows error "+code+")."); }
        private static void Require(bool success,string operation) { if(!success)throw Failure(operation,Marshal.GetLastWin32Error()); }
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)][DllImport("cabinet.dll",SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)]private static extern bool CreateCompressor(uint algorithm,IntPtr allocator,out IntPtr handle);
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)][DllImport("cabinet.dll",SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)]private static extern bool SetCompressorInformation(IntPtr handle,int informationClass,ref uint value,UIntPtr valueSize);
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)][DllImport("cabinet.dll",EntryPoint="Compress",SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)]private static extern bool CompressNative(IntPtr handle,byte[] input,UIntPtr inputSize,byte[] output,UIntPtr outputSize,out UIntPtr written);
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)][DllImport("cabinet.dll")][return:MarshalAs(UnmanagedType.Bool)]private static extern bool CloseCompressor(IntPtr handle);
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)][DllImport("cabinet.dll",SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)]private static extern bool CreateDecompressor(uint algorithm,IntPtr allocator,out IntPtr handle);
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)][DllImport("cabinet.dll",EntryPoint="Decompress",SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)]private static extern bool DecompressNative(IntPtr handle,byte[] input,UIntPtr inputSize,byte[] output,UIntPtr outputSize,out UIntPtr written);
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)][DllImport("cabinet.dll")][return:MarshalAs(UnmanagedType.Bool)]private static extern bool CloseDecompressor(IntPtr handle);
    }
}
