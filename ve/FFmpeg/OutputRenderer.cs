using FFmpeg.AutoGen;
using Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ve.FFmpeg.Support;

namespace ve.FFmpeg
{
    public static class OutputRenderer
    {
        public static unsafe void Start(MainWindowViewModel vm, string outputFileName, ulong bitrate)
        {
            // init output stream
            var ofmt = ffmpeg.av_guess_format(Path.GetExtension(outputFileName).Substring(1), null, null);
            AVFormatContext* octx;
            ffmpeg.avformat_alloc_output_context2(&octx, ofmt, null, null).ThrowExceptionIfFFmpegError();

            var ovcodecid = FindCodecIDFromFileName(outputFileName);
            var ovcodec = ffmpeg.avcodec_find_encoder(ovcodecid);
            var ovstream = ffmpeg.avformat_new_stream(octx, ovcodec);

            ffmpeg.avcodec_get_context_defaults3(ovstream->codec, ovcodec).ThrowExceptionIfFFmpegError();
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

            var extramfdata = new Dictionary<MediaFileModel, MediaFileData>();

            // setup decoding the input frames
            foreach (var mf in vm.Sections.Select(s => s.MediaFile).Distinct())
            {
                var ivstreamw = mf.Decoder.VideoStream;
                var data = new MediaFileData();

                // image 
                byte_ptrArray4 iSrcData;
                int_array4 iSrcLines;
                ffmpeg.av_image_alloc(ref iSrcData, ref iSrcLines, framesize.Width, framesize.Height, ovstream->codec->pix_fmt, 16).ThrowExceptionIfFFmpegError();

                // init the graph
                var buffersrc = ffmpeg.avfilter_get_by_name("buffer");
                var buffersink = ffmpeg.avfilter_get_by_name("buffersink");
                var filteroutputs = ffmpeg.avfilter_inout_alloc();
                var filterinputs = ffmpeg.avfilter_inout_alloc();
                var timebase = ivstreamw.Stream->time_base;

                try
                {
                    data.Graph = ffmpeg.avfilter_graph_alloc();

                    // source
                    var srcfilterargs = $"video_size={ivstreamw.Stream->codec->width}x{ivstreamw.Stream->codec->height}:pix_fmt={(int)ivstreamw.Stream->codec->pix_fmt}:" +
                        $"time_base={timebase.num}:{timebase.den}:pixel_aspect={ivstreamw.Stream->codec->sample_aspect_ratio.num}:{ivstreamw.Stream->codec->sample_aspect_ratio.den}";
                    ffmpeg.avfilter_graph_create_filter(&data.BufferSourceContext, buffersrc, "in", srcfilterargs, null, data.Graph).ThrowExceptionIfFFmpegError();

                    // sink
                    ffmpeg.avfilter_graph_create_filter(&data.BufferSinkContext, buffersink, "out", null, null, data.Graph).ThrowExceptionIfFFmpegError();

                    var pixelFormat = AVPixelFormat.AV_PIX_FMT_YUV420P;
                    ffmpeg.av_opt_set_bin(data.BufferSinkContext, "pix_fmts", (byte*)&pixelFormat, sizeof(AVPixelFormat), ffmpeg.AV_OPT_SEARCH_CHILDREN).ThrowExceptionIfFFmpegError();

                    // set the graph endpoints
                    filteroutputs->name = ffmpeg.av_strdup("in");
                    filteroutputs->filter_ctx = data.BufferSourceContext;
                    filteroutputs->pad_idx = 0;
                    filteroutputs->next = null;

                    filterinputs->name = ffmpeg.av_strdup("out");
                    filterinputs->filter_ctx = data.BufferSinkContext;
                    filterinputs->pad_idx = 0;
                    filterinputs->next = null;

                    ffmpeg.avfilter_graph_parse_ptr(data.Graph, "copy", &filterinputs, &filteroutputs, null).ThrowExceptionIfFFmpegError();
                    ffmpeg.avfilter_graph_config(data.Graph, null).ThrowExceptionIfFFmpegError();
                }
                finally
                {
                    ffmpeg.avfilter_inout_free(&filterinputs);
                    ffmpeg.avfilter_inout_free(&filteroutputs);
                }

                extramfdata.Add(mf, data);
            }

            // start actually decoding the sections
            var filteredFrame = ffmpeg.av_frame_alloc();
            foreach (var section in vm.Sections)
            {
                var mf = section.MediaFile;
                var data = extramfdata[mf];
                var decoder = mf.Decoder;

                while (true)
                {
                    ffmpeg.av_read_frame(decoder.FormatContextPointer, decoder.VideoPacketPointer).ThrowExceptionIfFFmpegError();
                    if (decoder.VideoPacketPointer->stream_index == decoder.VideoStream.Stream->index)
                    {
                        ffmpeg.avcodec_send_packet(decoder.VideoStream.Stream->codec, decoder.VideoPacketPointer).ThrowExceptionIfFFmpegError();

                        int ret;
                        do
                        {
                            try
                            {
                                ret = ffmpeg.avcodec_receive_frame(decoder.VideoStream.Stream->codec, decoder.FramePointer).ThrowExceptionIfFFmpegError();
                                if (ret == ffmpeg.AVERROR_EOF || ret == ffmpeg.AVERROR(ffmpeg.EAGAIN))
                                    break;

                                decoder.FramePointer->pts = decoder.FramePointer->best_effort_timestamp;

                                // push the decoded frame in the graph
                                ffmpeg.av_buffersrc_write_frame(data.BufferSourceContext, decoder.FramePointer).ThrowExceptionIfFFmpegError();

                                // pull the filtered frames from the graph
                                while (true)
                                {
                                    try
                                    {
                                        ret = ffmpeg.av_buffersink_get_frame(data.BufferSinkContext, filteredFrame).ThrowExceptionIfFFmpegError();
                                        if (ret == ffmpeg.AVERROR_EOF || ret == ffmpeg.AVERROR(ffmpeg.EAGAIN))
                                            break;

                                        // use the frame
                                    }
                                    finally { ffmpeg.av_frame_unref(filteredFrame); }
                                }
                            }
                            finally { ffmpeg.av_frame_unref(decoder.FramePointer); }

                        } while (ret >= 0);
                    }

                    ffmpeg.av_packet_unref(decoder.VideoPacketPointer);
                }
            }
        }

        static AVCodecID FindCodecIDFromFileName(string outputFileName) =>
            Path.GetExtension(outputFileName) switch
            {
                ".webm" => AVCodecID.AV_CODEC_ID_WEBP,
                ".mp4" => AVCodecID.AV_CODEC_ID_MPEG4,
                _ => throw new ArgumentException($"Could not figure out codec ID for {outputFileName}")
            };

        unsafe struct MediaFileData
        {
            public AVFilterContext* BufferSourceContext, BufferSinkContext;
            public AVFilterGraph* Graph;
        }
    }
}
