using FFmpeg.AutoGen;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading;

namespace ve.FFmpeg.Support
{
    public unsafe class FFmpegVideoStreamDecoder : ReactiveObject, IDisposable
    {
        public string FilePath { get; }

        private readonly AVFormatContext* FormatContextPointer = ffmpeg.avformat_alloc_context();
        private readonly AVFrame* FramePointer = ffmpeg.av_frame_alloc();
        private readonly AVPacket* VideoPacketPointer = ffmpeg.av_packet_alloc();

        private readonly Thread VideoThread;

        public FFmpegVideoStream VideoStream { get; }

        public FFmpegAudioStream AudioStream { get; }

        public double LengthSeconds => Math.Max(
            VideoStream is null || VideoStream.Stream == null ? 0 : VideoStream.Stream->duration / (double)ffmpeg.AV_TIME_BASE,
            AudioStream is null || AudioStream.Stream == null ? 0 : AudioStream.Stream->duration / (double)ffmpeg.AV_TIME_BASE);

        public FFmpegVideoStreamDecoder(string filePath)
        {
            FilePath = filePath;

            // open the file
            fixed (AVFormatContext** FormatContextPP = &FormatContextPointer)
                ffmpeg.avformat_open_input(FormatContextPP, filePath, null, null).ThrowExceptionIfFFmpegError();
            ffmpeg.avformat_find_stream_info(FormatContextPointer, null).ThrowExceptionIfFFmpegError();

            // find useful streams
            for (var idx = 0; idx < FormatContextPointer->nb_streams; ++idx)
                switch (FormatContextPointer->streams[idx]->codec->codec_type)
                {
                    case AVMediaType.AVMEDIA_TYPE_VIDEO:
                        if (VideoStream is null)
                            VideoStream = new FFmpegVideoStream
                            {
                                Stream = FormatContextPointer->streams[idx],
                                Codec = ffmpeg.avcodec_find_decoder(FormatContextPointer->streams[idx]->codec->codec_id),
                            };
                        break;
                    case AVMediaType.AVMEDIA_TYPE_AUDIO:
                        if (AudioStream is null)
                            AudioStream = new FFmpegAudioStream
                            {
                                Stream = FormatContextPointer->streams[idx]
                            };
                        break;
                }

            VideoThread = new Thread(VideoThreadProc) { Name = $"Video Player Thread for {FilePath}" };
            VideoThread.Start();
        }

        private void VideoThreadProc(object obj)
        {
            while (true)
            {
                Playing = PlayingRequest;
                if (!PlayingRequest) VideoThreadPlayRequestEvent.WaitOne();
                if (QuitRequest) return;

                if (ffmpeg.av_read_frame(FormatContextPointer, VideoPacketPointer) == ffmpeg.AVERROR_EOF)
                {
                    // done
                    PlayingRequest = Playing = false;
                }

                if (VideoPacketPointer->stream_index == VideoStream.Stream->index)
                {

                }
            }
        }

        private volatile bool QuitRequest, PlayingRequest, Playing;
        private readonly AutoResetEvent VideoThreadPlayRequestEvent = new AutoResetEvent(false);

        public void Play()
        {
            PlayingRequest = true;
            VideoThreadPlayRequestEvent.Set();
        }

        public void Stop(bool wait = false)
        {
            PlayingRequest = false;
            if (wait) VideoThread.Join();
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // managed state
                    QuitRequest = true;
                    VideoThreadPlayRequestEvent.Set();
                    Stop(true);
                    VideoThreadPlayRequestEvent.Dispose();
                }

                // unmanaged resources 
                ffmpeg.av_frame_unref(FramePointer);
                ffmpeg.av_free(FramePointer);

                ffmpeg.av_packet_unref(VideoPacketPointer);
                ffmpeg.av_free(VideoPacketPointer);

                if (VideoStream != null)
                    ffmpeg.avcodec_close(VideoStream.Stream->codec);
                if (AudioStream != null)
                    ffmpeg.avcodec_close(AudioStream.Stream->codec);

                fixed (AVFormatContext** FormatContextPP = &FormatContextPointer)
                    ffmpeg.avformat_close_input(FormatContextPP);

                disposedValue = true;
            }
        }

        ~FFmpegVideoStreamDecoder()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }

    public unsafe class FFmpegVideoStream
    {
        internal AVStream* Stream;
        internal AVCodec* Codec;
    }

    public unsafe class FFmpegAudioStream
    {
        internal AVStream* Stream;
    }
}
