using FFmpeg.AutoGen;
using Microsoft.Win32.SafeHandles;
using ve.Model;
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
        internal static unsafe void Start(MainWindowViewModel vm, string outputFileName, int crf, int width, int height)
        {
            // init output stream
            var outputFormat = ffmpeg.av_guess_format(Path.GetExtension(outputFileName)[1..], null, null);
            using var outputFormatContext = new SafeAVFormatContext(pp =>
                ffmpeg.avformat_alloc_output_context2(pp, outputFormat, null, null).ThrowExceptionIfFFmpegError());

            var outputCodecId = FindCodecIdFromFileName(outputFileName);
            var outputEncoderCodec = ffmpeg.avcodec_find_encoder(outputCodecId);

            var frameSize = vm.MediaFiles[0].Decoder.VideoStream.FrameSize;                                                             // frame size should be an output
            var frameRate = vm.MediaFiles[0].Decoder.VideoStream.DecoderCodecContext->framerate;
            var outputEncoderCodecContext = ffmpeg.avcodec_alloc_context3(outputEncoderCodec);

            ffmpeg.avcodec_get_context_defaults3(outputEncoderCodecContext, outputEncoderCodec).ThrowExceptionIfFFmpegError();
            outputEncoderCodecContext->codec_id = outputCodecId;
            //ovencoderContext->bit_rate = (long)bitrate * 1000;
            outputEncoderCodecContext->width = width/*framesize.Width*/;
            outputEncoderCodecContext->height = height/*framesize.Height*/;
            outputEncoderCodecContext->time_base = ffmpeg.av_inv_q(frameRate);           // todo framerate should be an output
            outputEncoderCodecContext->gop_size = 12;                                    // one intra-frame every this many frames at most
            outputEncoderCodecContext->pix_fmt = AVPixelFormat.AV_PIX_FMT_YUV420P;

            // mpeg workaround
            if (outputEncoderCodecContext->codec_id == AVCodecID.AV_CODEC_ID_MPEG1VIDEO)
                outputEncoderCodecContext->mb_decision = 2;

            // generic workaround
            if ((outputFormat->flags & ffmpeg.AVFMT_GLOBALHEADER) != 0)
                outputEncoderCodecContext->flags |= ffmpeg.AV_CODEC_FLAG_GLOBAL_HEADER;

            // encoder settings
            using (var avOptions = new SafeAVDictionary())
            {
                avOptions.Set("deadline", "best");
                avOptions.Set("cpu-used", "0");

                if (outputCodecId == AVCodecID.AV_CODEC_ID_VP8)
                {
                    avOptions.Set("crf", $"{crf:0.00}");

                    //This value was chosen to make the bitrate high enough
                    //for libvpx to "turn off" the maximum bitrate feature
                    //that is normally applied to constant quality.
                    outputEncoderCodecContext->bit_rate = (long)frameSize.Width * frameSize.Height * frameRate.num / frameRate.den;
                }
                else if (outputCodecId == AVCodecID.AV_CODEC_ID_H264)
                {
                    avOptions.Set("rc", "cqp");
                    avOptions.Set("qp_i", $"{crf:0.00}");
                    avOptions.Set("qp_p", $"{crf:0.00}");
                    avOptions.Set("qp_b", $"{crf + 2:0.00}");
                }

                // open the codec 
                avOptions.Update(pp =>
                    ffmpeg.avcodec_open2(outputEncoderCodecContext, outputEncoderCodec, pp).ThrowExceptionIfFFmpegError());
            }

            // create the output stream from the codec definition
            var outputStream = ffmpeg.avformat_new_stream(outputFormatContext.Pointer, null);
            ffmpeg.avcodec_parameters_from_context(outputStream->codecpar, outputEncoderCodecContext).ThrowExceptionIfFFmpegError();
            outputStream->time_base = outputEncoderCodecContext->time_base;

            // open the file and write the header
            ffmpeg.avio_open(&outputFormatContext.Pointer->pb, outputFileName, ffmpeg.AVIO_FLAG_WRITE).ThrowExceptionIfFFmpegError();
            ffmpeg.avformat_write_header(outputFormatContext.Pointer, null).ThrowExceptionIfFFmpegError();

            var extraMfData = new Dictionary<MediaFileModel, MediaFileData>();

            var cropNeeded = !(vm.Camera.KeyFrames.Count == 0
                || (vm.Camera.KeyFrames.Count == 1 && vm.Camera.KeyFrames[0].InnerObject == new RectangleModel(0, 0, frameSize.Width, frameSize.Height)));

            // setup decoding the input frames
            var mfs = vm.Sections.Select(s => s.MediaFile).Distinct().ToArray();
            foreach (var mf in mfs)
            {
                var inputStreamWrapper = mf.Decoder.VideoStream;
                var data = new MediaFileData();

                // init the graph
                var bufferSourceFilter = ffmpeg.avfilter_get_by_name("buffer");
                var bufferSinkFilter = ffmpeg.avfilter_get_by_name("buffersink");
                var filterOutputs = ffmpeg.avfilter_inout_alloc();
                var filterInputs = ffmpeg.avfilter_inout_alloc();
                var timeBase = outputEncoderCodecContext->time_base;

                try
                {
                    data.Graph = ffmpeg.avfilter_graph_alloc();

                    // source
                    var srcfilterargs = $"video_size={frameSize.Width}x{frameSize.Height}:pix_fmt={(int)inputStreamWrapper.Stream->codec->pix_fmt}:" +
                        $"time_base={timeBase.num}/{timeBase.den}:pixel_aspect={inputStreamWrapper.Stream->codec->sample_aspect_ratio.num}/{inputStreamWrapper.Stream->codec->sample_aspect_ratio.den}";
                    ffmpeg.avfilter_graph_create_filter(&data.BufferSourceContext, bufferSourceFilter, "in", srcfilterargs, null, data.Graph).ThrowExceptionIfFFmpegError();

                    using (var sinkParams = new SafeAVBufferSinkParams())
                    {
                        sinkParams.SetPixelFormats(AVPixelFormat.AV_PIX_FMT_YUV420P, AVPixelFormat.AV_PIX_FMT_RGB24);
                        ffmpeg.avfilter_graph_create_filter(&data.BufferSinkContext, bufferSinkFilter, "out", null, sinkParams.Pointer, data.Graph).ThrowExceptionIfFFmpegError();
                    }

                    // set the graph endpoints
                    filterOutputs->name = ffmpeg.av_strdup("in");
                    filterOutputs->filter_ctx = data.BufferSourceContext;
                    filterOutputs->pad_idx = 0;
                    filterOutputs->next = null;

                    filterInputs->name = ffmpeg.av_strdup("out");
                    filterInputs->filter_ctx = data.BufferSinkContext;
                    filterInputs->pad_idx = 0;
                    filterInputs->next = null;

                    ffmpeg.avfilter_graph_parse_ptr(data.Graph, cropNeeded ? $"crop" : "copy", &filterInputs, &filterOutputs, null).ThrowExceptionIfFFmpegError();
                    ffmpeg.avfilter_graph_config(data.Graph, null).ThrowExceptionIfFFmpegError();
                }
                finally
                {
                    ffmpeg.avfilter_inout_free(&filterInputs);
                    ffmpeg.avfilter_inout_free(&filterOutputs);
                }

                extraMfData.Add(mf, data);
            }

            using var frame = new SafeAVFrame();
            using var packet = new SafeAVPacket();

            int frames = 0;

            // start actually decoding the sections
            foreach (var section in vm.Sections)
            {
                var mf = section.MediaFile;
                var data = extraMfData[mf];
                var decoder = mf.Decoder;
                var graph = data.Graph;

                // seek to start cut
                var cutStartTs = FFmpegUtilities.PreciseSeek(section.Start, decoder.FormatContextPointer, decoder.VideoStream.DecoderCodecContext, decoder.VideoStream.Stream);
                var cutEndTs = FFmpegUtilities.GetInternalTimestamp(section.End, decoder.VideoStream.Stream);
                bool reachedCutEnd = false;

                var encoderThread = new Thread(() =>
                {
                    using var encPacket = new SafeAVPacket();
                    while (true)
                    {
                        var ret = ffmpeg.avcodec_receive_packet(outputEncoderCodecContext, encPacket.Pointer).ThrowExceptionIfFFmpegErrorOtherThanAgainEof();
                        if (ret >= 0)
                        {
                            encPacket.Pointer->stream_index = 0;
                            ffmpeg.av_packet_rescale_ts(encPacket.Pointer, decoder.VideoStream.Stream->time_base, outputStream->time_base);
                            ffmpeg.av_interleaved_write_frame(outputFormatContext.Pointer, encPacket.Pointer).ThrowExceptionIfFFmpegError();

                            if (++frames % 50 == 0)
                                Console.Write('.');
                        }
                        else if (reachedCutEnd || ret == ffmpeg.AVERROR_EOF)
                            break;
                    }
                })
                { Name = "Encoder Thread" };
                encoderThread.Start();

                var decodingDone = false;
                var decoderThread = new Thread(() =>
                {
                    using var filteredFrame = new SafeAVFrame();

                    while (true)
                    {
                        try
                        {
                            int ret;
                            ret = ffmpeg.avcodec_receive_frame(decoder.VideoStream.DecoderCodecContext, frame.Pointer).ThrowExceptionIfFFmpegErrorOtherThanAgainEof();

                            if (ret == FFmpegSetup.AVERROR_EAGAIN)
                                if (decodingDone)
                                    break;
                                else
                                    continue;

                            if (frame.Pointer->best_effort_timestamp > cutEndTs)
                            {
                                reachedCutEnd = true;
                                return;
                            }

                            frame.Pointer->pts = frame.Pointer->best_effort_timestamp - cutStartTs;

                            if (cropNeeded)
                            {
                                var rect = vm.Camera[FFmpegUtilities.GetTimespanTimestamp(frame.Pointer->pts, decoder.VideoStream.Stream)];
                                if (LastRectangle is null || LastRectangle != rect)
                                {
                                    if (LastRectangle is null || rect.X != LastRectangle.X)
                                        ffmpeg.avfilter_graph_send_command(graph, "crop", "x", rect.X.ToString(), null, 0, ffmpeg.AVFILTER_CMD_FLAG_ONE)
                                            .ThrowExceptionIfFFmpegError();

                                    if (LastRectangle is null || rect.Y != LastRectangle.Y)
                                        ffmpeg.avfilter_graph_send_command(graph, "crop", "y", rect.Y.ToString(), null, 0, ffmpeg.AVFILTER_CMD_FLAG_ONE)
                                            .ThrowExceptionIfFFmpegError();

                                    if (LastRectangle is null || rect.Width != LastRectangle.Width)
                                        ffmpeg.avfilter_graph_send_command(graph, "crop", "out_w", rect.Width.ToString(), null, 0, ffmpeg.AVFILTER_CMD_FLAG_ONE)
                                            .ThrowExceptionIfFFmpegError();

                                    if (LastRectangle is null || rect.Height != LastRectangle.Height)
                                        ffmpeg.avfilter_graph_send_command(graph, "crop", "out_h", rect.Height.ToString(), null, 0, ffmpeg.AVFILTER_CMD_FLAG_ONE)
                                            .ThrowExceptionIfFFmpegError();

                                    LastRectangle = rect;
                                }
                            }

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
                                    ret = ffmpeg.avcodec_send_frame(outputEncoderCodecContext, filteredFrame.Pointer).ThrowExceptionIfFFmpegErrorOtherThanAgainEof();
                                }
                                finally { ffmpeg.av_frame_unref(filteredFrame.Pointer); }
                            }
                        }
                        finally { ffmpeg.av_frame_unref(frame.Pointer); }
                    }
                })
                { Name = "Decoder Thread" };
                decoderThread.Start();

                RectangleModel LastRectangle = null;
                while (true)
                {
                    int err;
                    do
                    {
                        err = ffmpeg.av_read_frame(decoder.FormatContextPointer, packet.Pointer).ThrowExceptionIfFFmpegErrorOtherThanAgainEof();
                    }while(err)

                    // eof?
                    if (reachedCutEnd || ffmpeg.av_read_frame(decoder.FormatContextPointer, packet.Pointer).ThrowExceptionIfFFmpegErrorOtherThanAgainEof()
                        == ffmpeg.AVERROR_EOF)
                    {
                        // enter drain mode
                        ffmpeg.avcodec_send_frame(outputEncoderCodecContext, null).ThrowExceptionIfFFmpegErrorOtherThanAgainEof();
                        break;
                    }

                    // my stream?
                    if (packet.Pointer->stream_index == decoder.VideoStream.Stream->index)
                        while (!reachedCutEnd)
                        {
                            // handle multi-threading-only EAGAIN 
                            int ret;
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
            ffmpeg.av_write_trailer(outputFormatContext.Pointer);
            ffmpeg.avio_closep(&outputFormatContext.Pointer->pb);
        }

        static AVCodecID FindCodecIdFromFileName(string outputFileName) =>
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
