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
        public SafeAVFrame CurrentAVFrame { get; private set; } = new();
        private SafeAVPacket CurrentAVPacket { get; set; } = new();

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
                        {
                            var vs = new FFmpegVideoStream
                            {
                                Stream = FormatContextPointer->streams[idx],
                                DecoderCodec = ffmpeg.avcodec_find_decoder(FormatContextPointer->streams[idx]->codec->codec_id),
                                FrameSize = new Size(FormatContextPointer->streams[idx]->codecpar->width, FormatContextPointer->streams[idx]->codecpar->height),
                            };

                            // decoder context
                            vs.DecoderCodecContext = ffmpeg.avcodec_alloc_context3(vs.DecoderCodec);
                            ffmpeg.avcodec_parameters_to_context(vs.DecoderCodecContext, vs.Stream->codecpar).ThrowExceptionIfFFmpegError();
                            vs.DecoderCodecContext->framerate = ffmpeg.av_guess_frame_rate(FormatContextPointer, vs.Stream, null);
                            ffmpeg.avcodec_open2(vs.DecoderCodecContext, vs.DecoderCodec, null).ThrowExceptionIfFFmpegError();

                            VideoStream = vs;
                        }
                        else
                            FormatContextPointer->streams[idx]->discard = AVDiscard.AVDISCARD_ALL;
                        break;
                    case AVMediaType.AVMEDIA_TYPE_AUDIO:
                        if (AudioStream.Stream is null)
                            AudioStream = new FFmpegAudioStream
                            {
                                Stream = FormatContextPointer->streams[idx]
                            };
                        else
                            FormatContextPointer->streams[idx]->discard = AVDiscard.AVDISCARD_ALL;
                        break;
                }
        }

        public bool TryDecodeNextFrame()
        {
            CurrentAVFrame.Unref();

            int error;
            do
            {
                try
                {
                    do
                    {
                        error = ffmpeg.av_read_frame(FormatContextPointer, CurrentAVPacket.Pointer).ThrowExceptionIfFFmpegErrorOtherThanAgainEof();
                        if (error == ffmpeg.AVERROR_EOF)
                            return false;
                    } while (CurrentAVPacket.Pointer->stream_index != VideoStream.Stream->index);                                               // ignore anything but the video stream we care about

                    ffmpeg.avcodec_send_packet(VideoStream.DecoderCodecContext, CurrentAVPacket.Pointer).ThrowExceptionIfFFmpegError();         // send the packet to the frame decoder
                }
                finally { CurrentAVPacket.Unref(); }

                error = ffmpeg.avcodec_receive_frame(VideoStream.DecoderCodecContext, CurrentAVFrame.Pointer);
            } while (error == FFmpegSetup.AVERROR_EAGAIN);
            error.ThrowExceptionIfFFmpegError();

            return true;
        }

        #region IDisposable Support
        private bool disposedValue; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // managed state
                    //QuitRequest = true;
                    //VideoThreadPlayRequestEvent.Set();
                    //Stop(true);
                    //VideoThreadPlayRequestEvent.Dispose();
                }

                // unmanaged resources 
                CurrentAVFrame.Dispose();
                CurrentAVPacket.Dispose();

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
        internal AVCodec* DecoderCodec;
        internal AVCodecContext* DecoderCodecContext;
        internal Size FrameSize;
    }

    public unsafe struct FFmpegAudioStream
    {
        internal AVStream* Stream;
    }
}
