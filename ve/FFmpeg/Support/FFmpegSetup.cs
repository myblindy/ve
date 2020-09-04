using FFmpeg.AutoGen;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace ve.FFmpeg.Support
{
    public static class FFmpegSetup
    {
        public static void Initialize()
        {
            var current = Environment.CurrentDirectory;
            var probe = Path.Combine("FFmpeg", "bin", Environment.Is64BitProcess ? "x64" : "x86");
            while (current != null)
            {
                var ffmpegBinaryPath = Path.Combine(current, probe);
                if (Directory.Exists(ffmpegBinaryPath))
                {
                    Console.WriteLine($"FFmpeg binaries found in: {ffmpegBinaryPath}");
                    ffmpeg.RootPath = ffmpegBinaryPath;

                    return;
                }

                current = Directory.GetParent(current)?.FullName;
            }

            throw new InvalidProgramException("Could not find ffmpeg binaries.");
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "name matches ffmpeg library")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "name matches ffmpeg library")]
        public static unsafe string av_strerror(int error)
        {
            var bufferSize = 1024;
            var buffer = stackalloc byte[bufferSize];
            ffmpeg.av_strerror(error, buffer, (ulong)bufferSize);
            var message = Marshal.PtrToStringAnsi((IntPtr)buffer);
            return message;
        }

        public static int ThrowExceptionIfFFmpegError(this int error)
        {
            if (error < 0) throw new ApplicationException(av_strerror(error));
            return error;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "name matches ffmpeg library")]
        public static readonly int AVERROR_EAGAIN = ffmpeg.AVERROR(ffmpeg.EAGAIN);
        public static int ThrowExceptionIfFFmpegErrorOtherThanAgainEof(this int error)
        {
            if (error < 0 && error != ffmpeg.AVERROR_EOF && error != AVERROR_EAGAIN) throw new ApplicationException(av_strerror(error));
            return error;
        }
    }
}
