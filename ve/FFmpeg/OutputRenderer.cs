using FFmpeg.AutoGen;
using Microsoft.Win32.SafeHandles;
using Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ve.FFmpeg.Support;

namespace ve.FFmpeg
{
    public static class OutputRenderer
    {
        public static unsafe void Start(MainWindowViewModel vm, string outputFileName, int crf)
        {
            // init output stream
            var ofmt = ffmpeg.av_guess_format(Path.GetExtension(outputFileName)[1..], null, null);
            using var ofctx = new SafeAVFormatContext(pp =>
                ffmpeg.avformat_alloc_output_context2(pp, ofmt, null, null).ThrowExceptionIfFFmpegError());

            var ovcodecid = FindCodecIDFromFileName(outputFileName);
            var ovencoder = ffmpeg.avcodec_find_encoder(ovcodecid);

            var framesize = vm.MediaFiles[0].Decoder.VideoStream.FrameSize;                                                             // frame size should be an output
            var framerate = vm.MediaFiles[0].Decoder.VideoStream.DecoderCodecContext->framerate;
            var ovencoderContext = ffmpeg.avcodec_alloc_context3(ovencoder);

            ffmpeg.avcodec_get_context_defaults3(ovencoderContext, ovencoder).ThrowExceptionIfFFmpegError();
            ovencoderContext->codec_id = ovcodecid;
            //ovencoderContext->bit_rate = (long)bitrate * 1000;
            ovencoderContext->width = framesize.Width;
            ovencoderContext->height = framesize.Height;
            ovencoderContext->time_base = ffmpeg.av_inv_q(framerate);           // todo framerate should be an output
            ovencoderContext->gop_size = 12;                                    // one intra-frame every this many frames at most
            ovencoderContext->pix_fmt = AVPixelFormat.AV_PIX_FMT_YUV420P;

            // mpeg workaround
            if (ovencoderContext->codec_id == AVCodecID.AV_CODEC_ID_MPEG1VIDEO)
                ovencoderContext->mb_decision = 2;

            // generic workaround
            if ((ofmt->flags & ffmpeg.AVFMT_GLOBALHEADER) != 0)
                ovencoderContext->flags |= ffmpeg.AV_CODEC_FLAG_GLOBAL_HEADER;

            // encoder settings
            using (var avOptions = new SafeAVDictionary())
            {
                avOptions.Set("deadline", "best");
                avOptions.Set("cpu-used", "0");

                if (ovcodecid == AVCodecID.AV_CODEC_ID_VP8)
                {
                    avOptions.Set("crf", $"{crf:0.00}");

                    //This value was chosen to make the bitrate high enough
                    //for libvpx to "turn off" the maximum bitrate feature
                    //that is normally applied to constant quality.
                    ovencoderContext->bit_rate = (long)framesize.Width * framesize.Height * framerate.num / framerate.den;
                }
                else if (ovcodecid == AVCodecID.AV_CODEC_ID_H264)
                {
                    avOptions.Set("rc", "cqp");
                    avOptions.Set("qp_i", $"{crf:0.00}");
                    avOptions.Set("qp_p", $"{crf:0.00}");
                    avOptions.Set("qp_b", $"{crf + 2:0.00}");
                }

                // open the codec 
                avOptions.Update(pp =>
                    ffmpeg.avcodec_open2(ovencoderContext, ovencoder, pp).ThrowExceptionIfFFmpegError());
            }

            // create the output stream from the codec definition
            var ovstream = ffmpeg.avformat_new_stream(ofctx.Pointer, null);
            ffmpeg.avcodec_parameters_from_context(ovstream->codecpar, ovencoderContext).ThrowExceptionIfFFmpegError();
            ovstream->time_base = ovencoderContext->time_base;

            // open the file and write the header
            ffmpeg.avio_open(&ofctx.Pointer->pb, outputFileName, ffmpeg.AVIO_FLAG_WRITE).ThrowExceptionIfFFmpegError();
            ffmpeg.avformat_write_header(ofctx.Pointer, null).ThrowExceptionIfFFmpegError();

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
                var timebase = ovencoderContext->time_base;

                try
                {
                    data.Graph = ffmpeg.avfilter_graph_alloc();

                    // source
                    var srcfilterargs = $"video_size={framesize.Width}x{framesize.Height}:pix_fmt={(int)ivstreamw.Stream->codec->pix_fmt}:" +
                        $"time_base={timebase.num}/{timebase.den}:pixel_aspect={ivstreamw.Stream->codec->sample_aspect_ratio.num}/{ivstreamw.Stream->codec->sample_aspect_ratio.den}";
                    ffmpeg.avfilter_graph_create_filter(&data.BufferSourceContext, buffersrc, "in", srcfilterargs, null, data.Graph).ThrowExceptionIfFFmpegError();

                    {
                        // sink
                        AVBufferSinkParams* sinkparams = null;
                        try
                        {
                            sinkparams = ffmpeg.av_buffersink_params_alloc();
                            var sinkPixelFormats = new[] { AVPixelFormat.AV_PIX_FMT_RGB24, AVPixelFormat.AV_PIX_FMT_YUV420P, AVPixelFormat.AV_PIX_FMT_NONE };
                            fixed (AVPixelFormat* sinkPixelFormatsPointer = sinkPixelFormats)
                            {
                                sinkparams->pixel_fmts = sinkPixelFormatsPointer;
                                ffmpeg.avfilter_graph_create_filter(&data.BufferSinkContext, buffersink, "out", null, sinkparams, data.Graph).ThrowExceptionIfFFmpegError();
                            }
                        }
                        finally { ffmpeg.av_free(sinkparams); }
                    }

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

            using var frame = new SafeAVFrame();
            using var packet = new SafeAVPacket();

            int frames = 0;

            // start actually decoding the sections
            foreach (var section in vm.Sections)
            {
                var mf = section.MediaFile;
                var data = extramfdata[mf];
                var decoder = mf.Decoder;

                var encoderThread = new Thread(() =>
                {
                    using var encPacket = new SafeAVPacket();
                    while (true)
                    {
                        var ret = ffmpeg.avcodec_receive_packet(ovencoderContext, encPacket.Pointer).ThrowExceptionIfFFmpegErrorOtherThanAgainEof();
                        if (ret >= 0)
                        {
                            encPacket.Pointer->stream_index = 0;
                            ffmpeg.av_packet_rescale_ts(encPacket.Pointer, decoder.VideoStream.Stream->time_base, ovstream->time_base);
                            ffmpeg.av_interleaved_write_frame(ofctx.Pointer, encPacket.Pointer).ThrowExceptionIfFFmpegError();

                            if (++frames % 50 == 0)
                                Console.Write('.');
                        }
                        else if (ret == ffmpeg.AVERROR_EOF)
                            break;
                    }
                })
                { Name = "Encoder Thread" };
                encoderThread.Start();

                var decodingDone = false;
                var decoderSync = new object();
                var decoderThread = new Thread(() =>
                {
                    using var filteredFrame = new SafeAVFrame();
                    while (true)
                    {
                        try
                        {
                            int ret;
                            lock (decoderSync)
                                ret = ffmpeg.avcodec_receive_frame(decoder.VideoStream.DecoderCodecContext, frame.Pointer).ThrowExceptionIfFFmpegErrorOtherThanAgainEof();

                            if (ret == FFmpegSetup.AVERROR_EAGAIN)
                                if (decodingDone)
                                    break;
                                else
                                    continue;

                            frame.Pointer->pts = frame.Pointer->best_effort_timestamp;

                            // push the decoded frame in the graph
                            ffmpeg.av_buffersrc_write_frame(data.BufferSourceContext, frame.Pointer).ThrowExceptionIfFFmpegError();

                            // pull the filtered frames from the graph
                            while (true)
                            {
                                try
                                {
                                    ret = ffmpeg.av_buffersink_get_frame(data.BufferSinkContext, filteredFrame.Pointer).ThrowExceptionIfFFmpegErrorOtherThanAgainEof();
                                    if (ret == ffmpeg.AVERROR_EOF || ret == FFmpegSetup.AVERROR_EAGAIN)
                                        break;

                                    // send the frame for encoding, and get the encoded packets
                                    ret = ffmpeg.avcodec_send_frame(ovencoderContext, filteredFrame.Pointer).ThrowExceptionIfFFmpegErrorOtherThanAgainEof();
                                }
                                finally { ffmpeg.av_frame_unref(filteredFrame.Pointer); }
                            }
                        }
                        finally { ffmpeg.av_frame_unref(frame.Pointer); }
                    }
                })
                { Name = "Decoder Thread" };
                decoderThread.Start();

                // seek to beginning
                if (section.Start != TimeSpan.Zero)
                    ffmpeg.av_seek_frame(decoder.FormatContextPointer, decoder.VideoStream.Stream->index,
                        ffmpeg.av_rescale_q((long)(section.Start.TotalSeconds * ffmpeg.AV_TIME_BASE), ffmpeg.av_get_time_base_q(), decoder.VideoStream.Stream->time_base),
                        ffmpeg.AVSEEK_FLAG_ANY).ThrowExceptionIfFFmpegError();

                while (true)
                {
                    // eof?
                    if (ffmpeg.av_read_frame(decoder.FormatContextPointer, packet.Pointer).ThrowExceptionIfFFmpegErrorOtherThanAgainEof()
                        == ffmpeg.AVERROR_EOF)
                    {
                        // enter drain mode
                        ffmpeg.avcodec_send_frame(ovencoderContext, null).ThrowExceptionIfFFmpegErrorOtherThanAgainEof();
                        break;
                    }

                    // my stream?
                    if (packet.Pointer->stream_index == decoder.VideoStream.Stream->index)
                        while (true)
                        {
                            // handle multi-threading-only EAGAIN 
                            int ret;
                            lock (decoderSync)
                                ret = ffmpeg.avcodec_send_packet(decoder.VideoStream.DecoderCodecContext, packet.Pointer).ThrowExceptionIfFFmpegErrorOtherThanAgainEof();

                            if (ret >= 0)
                                break;
                        }

                    // free packet and done
                    ffmpeg.av_packet_unref(packet.Pointer);
                }

                decodingDone = true;
                decoderThread.Join();
                encoderThread.Join();
            }

            //cleanup
            ffmpeg.av_write_trailer(ofctx.Pointer);
            ffmpeg.avio_closep(&ofctx.Pointer->pb);
        }

        static AVCodecID FindCodecIDFromFileName(string outputFileName) =>
            Path.GetExtension(outputFileName) switch
            {
                ".webm" => AVCodecID.AV_CODEC_ID_VP8,
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
