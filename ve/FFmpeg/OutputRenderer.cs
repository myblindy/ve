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
            AVFormatContext* ofctx;
            ffmpeg.avformat_alloc_output_context2(&ofctx, ofmt, null, null).ThrowExceptionIfFFmpegError();

            var ovcodecid = FindCodecIDFromFileName(outputFileName);
            var ovencoder = ffmpeg.avcodec_find_encoder(ovcodecid);

            var framesize = vm.MediaFiles[0].Decoder.VideoStream.FrameSize;                                                             // frame size should be an output
            var ovencoderContext = ffmpeg.avcodec_alloc_context3(ovencoder);

            ffmpeg.avcodec_get_context_defaults3(ovencoderContext, ovencoder).ThrowExceptionIfFFmpegError();
            ovencoderContext->codec_id = ovcodecid;
            ovencoderContext->bit_rate = (long)bitrate;
            ovencoderContext->width = framesize.Width;
            ovencoderContext->height = framesize.Height;
            ovencoderContext->time_base =ffmpeg.av_inv_q(vm.MediaFiles[0].Decoder.VideoStream.DecoderCodecContext->framerate);          // todo framerate should be an output
            ovencoderContext->gop_size = 12;  // one intra-frame every this many frames at most
            ovencoderContext->pix_fmt = AVPixelFormat.AV_PIX_FMT_YUV420P;

            // mpeg workaround
            if (ovencoderContext->codec_id == AVCodecID.AV_CODEC_ID_MPEG1VIDEO)
                ovencoderContext->mb_decision = 2;

            // generic workaround
            if ((ofmt->flags & ffmpeg.AVFMT_GLOBALHEADER) != 0)
                ovencoderContext->flags |= ffmpeg.AV_CODEC_FLAG_GLOBAL_HEADER;

            // open the codec 
            ffmpeg.avcodec_open2(ovencoderContext, ovencoder, null).ThrowExceptionIfFFmpegError();

            // create the output stream from the codec definition
            var ovstream = ffmpeg.avformat_new_stream(ofctx, null);
            ffmpeg.avcodec_parameters_from_context(ovstream->codecpar, ovencoderContext).ThrowExceptionIfFFmpegError();
            ovstream->time_base = ovencoderContext->time_base;

            // open the file and write the header
            ffmpeg.avio_open(&ofctx->pb, outputFileName, ffmpeg.AVIO_FLAG_WRITE).ThrowExceptionIfFFmpegError();
            ffmpeg.avformat_write_header(ofctx, null).ThrowExceptionIfFFmpegError();

            var extramfdata = new Dictionary<MediaFileModel, MediaFileData>();

            // setup decoding the input frames
            var mfs = vm.Sections.Select(s => s.MediaFile).Distinct().ToArray();
            foreach (var mf in mfs)
            {
                var ivstreamw = mf.Decoder.VideoStream;
                var data = new MediaFileData();

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
                        $"time_base={timebase.num}/{timebase.den}:pixel_aspect={ivstreamw.Stream->codec->sample_aspect_ratio.num}/{ivstreamw.Stream->codec->sample_aspect_ratio.den}";
                    ffmpeg.avfilter_graph_create_filter(&data.BufferSourceContext, buffersrc, "in", srcfilterargs, null, data.Graph).ThrowExceptionIfFFmpegError();

                    // sink
                    ffmpeg.avfilter_graph_create_filter(&data.BufferSinkContext, buffersink, "out", null, null, data.Graph).ThrowExceptionIfFFmpegError();

                    ffmpeg.av_opt_set_bin(data.BufferSinkContext, "pix_fmts", (byte*)&ovstream->codec->pix_fmt, sizeof(AVPixelFormat), ffmpeg.AV_OPT_SEARCH_CHILDREN).ThrowExceptionIfFFmpegError();

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

            var filteredFrame = ffmpeg.av_frame_alloc();
            var encPacket = ffmpeg.av_packet_alloc();

            // start actually decoding the sections
            try
            {
                foreach (var section in vm.Sections)
                {
                    var mf = section.MediaFile;
                    var data = extramfdata[mf];
                    var decoder = mf.Decoder;

                    while (true)
                    {
                        void sink(int ret, AVPacket* encPacket, AVStream* ovstream, AVFormatContext* ofctx)
                        {
                            while (ret >= 0)
                            {
                                ret = ffmpeg.avcodec_receive_packet(ovencoderContext, encPacket);
                                if (ret >= 0)
                                {
                                    encPacket->stream_index = 0;
                                    ffmpeg.av_packet_rescale_ts(encPacket, decoder.VideoStream.DecoderCodecContext->time_base, ovstream->time_base);
                                    ffmpeg.av_interleaved_write_frame(ofctx, encPacket).ThrowExceptionIfFFmpegError();
                                }
                            }
                        }

                        if (ffmpeg.av_read_frame(decoder.FormatContextPointer, decoder.VideoPacketPointer).ThrowExceptionIfFFmpegErrorOtherThanAgainEof()
                            == ffmpeg.AVERROR_EOF)
                        {
                            // enter drain mode
                            ffmpeg.avcodec_send_frame(ovencoderContext, null).ThrowExceptionIfFFmpegErrorOtherThanAgainEof();
                            sink(1, encPacket, ovstream, ofctx);
                            break;
                        }

                        if (decoder.VideoPacketPointer->stream_index == decoder.VideoStream.Stream->index)
                        {
                            ffmpeg.avcodec_send_packet(decoder.VideoStream.DecoderCodecContext, decoder.VideoPacketPointer).ThrowExceptionIfFFmpegError();

                            int ret;
                            do
                            {
                                try
                                {
                                    ret = ffmpeg.avcodec_receive_frame(decoder.VideoStream.DecoderCodecContext, decoder.FramePointer).ThrowExceptionIfFFmpegErrorOtherThanAgainEof();
                                    if (ret == ffmpeg.AVERROR_EOF || ret == FFmpegSetup.AVERROR_EAGAIN)
                                        break;

                                    decoder.FramePointer->pts = decoder.FramePointer->best_effort_timestamp;

                                    // push the decoded frame in the graph
                                    ffmpeg.av_buffersrc_write_frame(data.BufferSourceContext, decoder.FramePointer).ThrowExceptionIfFFmpegError();

                                    // pull the filtered frames from the graph
                                    while (true)
                                    {
                                        try
                                        {
                                            ret = ffmpeg.av_buffersink_get_frame(data.BufferSinkContext, filteredFrame).ThrowExceptionIfFFmpegErrorOtherThanAgainEof();
                                            if (ret == ffmpeg.AVERROR_EOF || ret == FFmpegSetup.AVERROR_EAGAIN)
                                                break;

                                            // send the frame for encoding, and get the encoded packets
                                            ret = ffmpeg.avcodec_send_frame(ovencoderContext, filteredFrame).ThrowExceptionIfFFmpegErrorOtherThanAgainEof();
                                            sink(ret, encPacket, ovstream, ofctx);
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
            finally
            {
                ffmpeg.av_packet_free(&encPacket);
                ffmpeg.av_frame_free(&filteredFrame);
            }

            //cleanup
            ffmpeg.av_write_trailer(ofctx);
            ffmpeg.avio_closep(&ofctx->pb);
            ffmpeg.avformat_free_context(ofctx);
        }

        static AVCodecID FindCodecIDFromFileName(string outputFileName) =>
            Path.GetExtension(outputFileName) switch
            {
                ".webm" => AVCodecID.AV_CODEC_ID_WEBP,
                ".mp4" => AVCodecID.AV_CODEC_ID_H264,
                _ => throw new ArgumentException($"Could not figure out codec ID for {outputFileName}")
            };

        unsafe struct MediaFileData
        {
            public AVFilterContext* BufferSourceContext, BufferSinkContext;
            public AVFilterGraph* Graph;
        }
    }
}
