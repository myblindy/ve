using FFmpeg.AutoGen;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace ve.FFmpeg
{
    public static class Renderer
    {
        public static unsafe void Start(MainWindowViewModel vm, string outputFileName, ulong bitrate)
        {
            // init output stream
            var ofmt = ffmpeg.av_guess_format(Path.GetExtension(outputFileName), null, null);
            AVFormatContext* octx;
            ffmpeg.avformat_alloc_output_context2(&octx, ofmt, null, null);

            var ovcodecid = FindCodecIDFromFileName(outputFileName);
            var ovcodec = ffmpeg.avcodec_find_encoder(ovcodecid);
            var ovstream = ffmpeg.avformat_new_stream(octx, ovcodec);

            ffmpeg.avcodec_get_context_defaults3(ovstream->codec, ovcodec);
            ovstream->codec->codec_id = ovcodecid;
            ovstream->codec->bit_rate = (long)bitrate;
            var framesize = vm.MediaFiles[0].Decoder.VideoStream.FrameSize;                                                             // frame size should be an output
            ovstream->codec->width = framesize.Width;
            ovstream->codec->height = framesize.Height;
            ovstream->codec->time_base = vm.MediaFiles[0].Decoder.VideoStream.Stream->time_base;                                         // todo framerate should be an output
            ovstream->codec->gop_size = 12;  // one intra-frame every this many frames at most
            ovstream->codec->pix_fmt = AVPixelFormat.AV_PIX_FMT_YUV420P;

            // mpeg workaround
            if (ovstream->codec->codec_id == AVCodecID.AV_CODEC_ID_MPEG1VIDEO)
                ovstream->codec->mb_decision = 2;

            // generic workaround
            if ((ofmt->flags & ffmpeg.AVFMT_GLOBALHEADER) != 0)
                ovstream->codec->flags |= ffmpeg.AV_CODEC_FLAG_GLOBAL_HEADER;

            // start decoding the input frames
            foreach(var section in vm.Sections)
            {
                var ivstreamw = section.MediaFile.Decoder.VideoStream;

                var iframe = ffmpeg.av_frame_alloc();
                AVPicture opicture;
                ffmpeg.av_image_alloc(&opicture, ovstream->codec->pix_fmt, framesize.Width, framesize.Height);
            }
        }

        static AVCodecID FindCodecIDFromFileName(string outputFileName) =>
            Path.GetExtension(outputFileName) switch
            {
                ".webm" => AVCodecID.AV_CODEC_ID_WEBP,
                ".mp4" => AVCodecID.AV_CODEC_ID_MPEG4,
                _ => throw new ArgumentException($"Could not figure out codec ID for {outputFileName}")
            };
    }
}
