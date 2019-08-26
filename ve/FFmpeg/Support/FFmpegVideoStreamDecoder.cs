using FFmpeg.AutoGen;
using ReactiveUI;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;

namespace ve.FFmpeg.Support
{
    public unsafe class FFmpegVideoStreamDecoder : ReactiveObject, IDisposable
    {
        public string FilePath { get; }

        public AVFormatContext* FormatContextPointer { get; private set; } = ffmpeg.avformat_alloc_context();
        public AVFrame* FramePointer { get; private set; } = ffmpeg.av_frame_alloc();
        public AVPacket* VideoPacketPointer { get; private set; } = ffmpeg.av_packet_alloc();

        private readonly Thread VideoThread;

        public FFmpegVideoStream VideoStream { get; }

        public FFmpegAudioStream AudioStream { get; }

        public double LengthSeconds => FormatContextPointer->duration / (double)ffmpeg.AV_TIME_BASE;

        public FFmpegVideoStreamDecoder(string filePath)
        {
            FilePath = filePath;

            // open the file
            {
                var fc = FormatContextPointer;
                ffmpeg.avformat_open_input(&fc, filePath, null, null).ThrowExceptionIfFFmpegError();
                FormatContextPointer = fc;
            }
            ffmpeg.avformat_find_stream_info(FormatContextPointer, null).ThrowExceptionIfFFmpegError();

            // find useful streams
            for (var idx = 0; idx < FormatContextPointer->nb_streams; ++idx)
                switch (FormatContextPointer->streams[idx]->codec->codec_type)
                {
                    case AVMediaType.AVMEDIA_TYPE_VIDEO:
                        if (VideoStream.Stream is null)
                            VideoStream = new FFmpegVideoStream
                            {
                                Stream = FormatContextPointer->streams[idx],
                                Codec = ffmpeg.avcodec_find_decoder(FormatContextPointer->streams[idx]->codec->codec_id),
                                FrameSize = new Size(FormatContextPointer->streams[idx]->codecpar->width, FormatContextPointer->streams[idx]->codecpar->height),
                            };
                        break;
                    case AVMediaType.AVMEDIA_TYPE_AUDIO:
                        if (AudioStream.Stream is null)
                            AudioStream = new FFmpegAudioStream
                            {
                                Stream = FormatContextPointer->streams[idx]
                            };
                        break;
                }

            VideoThread = new Thread(VideoThreadProc) { Name = $"Video Player Thread for {FilePath}", IsBackground = true };
            //VideoThread.Start();
        }

        private readonly ConcurrentQueue<FFmpegPacketWrapper> VideoPacketQueue = new ConcurrentQueue<FFmpegPacketWrapper>();

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
                    VideoPacketQueue.Enqueue(new FFmpegPacketWrapper { Packet = VideoPacketPointer });
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

                if (VideoStream.Stream != null)
                    ffmpeg.avcodec_close(VideoStream.Stream->codec);
                if (AudioStream.Stream != null)
                    ffmpeg.avcodec_close(AudioStream.Stream->codec);

                {
                    var fc = FormatContextPointer;
                    ffmpeg.avformat_close_input(&fc);
                    FormatContextPointer = fc;
                }

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

    public unsafe struct FFmpegVideoStream
    {
        internal AVStream* Stream;
        internal AVCodec* Codec;
        internal Size FrameSize;
    }

    public unsafe struct FFmpegAudioStream
    {
        internal AVStream* Stream;
    }

    public unsafe struct FFmpegPacketWrapper
    {
        internal AVPacket* Packet;
    }
}
