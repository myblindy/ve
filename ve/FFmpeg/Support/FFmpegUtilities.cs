using FFmpeg.AutoGen;
using System;
using System.Collections.Generic;
using System.Text;

namespace ve.FFmpeg.Support
{
    public static class FFmpegUtilities
    {
        public static unsafe long GetTimestamp(TimeSpan position, AVStream* stream) =>
            ffmpeg.av_rescale((long)(position.TotalSeconds * ffmpeg.AV_TIME_BASE), stream->time_base.den, stream->time_base.num * ffmpeg.AV_TIME_BASE);

        public static unsafe long PreciseSeek(TimeSpan position, AVFormatContext* decoderFormatContext, AVCodecContext* decoderCodecContext, AVStream* stream)
        {
            var tsdelta = GetTimestamp(position, stream);
            var tsdeltaMinus1 = tsdelta - ffmpeg.av_rescale(ffmpeg.av_rescale(decoderCodecContext->framerate.den, ffmpeg.AV_TIME_BASE, decoderCodecContext->framerate.num),
                stream->time_base.den, stream->time_base.num * ffmpeg.AV_TIME_BASE);

            // seek for the last keyframe before my target timestamp
            ffmpeg.avformat_seek_file(decoderFormatContext, -1, long.MinValue, tsdeltaMinus1, tsdeltaMinus1, ffmpeg.AVSEEK_FLAG_BACKWARD).ThrowExceptionIfFFmpegError();
            ffmpeg.avcodec_flush_buffers(decoderCodecContext);

            // fast forward until the expected timestamp
            using var packet = new SafeAVPacket();
            using var frame = new SafeAVFrame();
            do
            {
                ffmpeg.av_read_frame(decoderFormatContext, packet.Pointer).ThrowExceptionIfFFmpegError();
                if (packet.Pointer->stream_index != stream->index)
                    continue;

                ffmpeg.avcodec_send_packet(decoderCodecContext, packet.Pointer).ThrowExceptionIfFFmpegError();
                ffmpeg.av_packet_unref(packet.Pointer);
                if (ffmpeg.avcodec_receive_frame(decoderCodecContext, frame.Pointer).ThrowExceptionIfFFmpegErrorOtherThanAgainEof() == FFmpegSetup.AVERROR_EAGAIN)
                    continue;
            } while (frame.Pointer->pkt_dts < tsdeltaMinus1);

            return tsdelta;
        }
    }
}
