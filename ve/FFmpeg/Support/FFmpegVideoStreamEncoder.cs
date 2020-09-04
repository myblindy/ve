using FFmpeg.AutoGen;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ve.Model;

namespace ve.FFmpeg.Support
{
    public unsafe class FFmpegVideoStreamEncoder
    {
        readonly SafeAVFormatContext outputFormatContext;
        readonly AVCodec* outputEncoderCodec;
        readonly AVCodecContext* outputEncoderCodecContext;
        readonly AVStream* outputStream;

        public FFmpegVideoStreamEncoder(MainWindowViewModel vm, string outputFileName, int crf, int width, int height)
        {
            var outputFormat = ffmpeg.av_guess_format(Path.GetExtension(outputFileName)[1..], null, null);
            outputFormatContext = new SafeAVFormatContext(pp =>
                ffmpeg.avformat_alloc_output_context2(pp, outputFormat, null, null).ThrowExceptionIfFFmpegError());

            var outputCodecId = FindCodecIdFromFileName(outputFileName);
            outputEncoderCodec = ffmpeg.avcodec_find_encoder(outputCodecId);
            outputEncoderCodecContext = ffmpeg.avcodec_alloc_context3(outputEncoderCodec);

            var frameSize = vm.MediaFiles[0].Decoder.VideoStream.FrameSize;                                                             // frame size should be an output
            var frameRate = vm.MediaFiles[0].Decoder.VideoStream.DecoderCodecContext->framerate;
            
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
            outputStream = ffmpeg.avformat_new_stream(outputFormatContext.Pointer, null);
            ffmpeg.avcodec_parameters_from_context(outputStream->codecpar, outputEncoderCodecContext).ThrowExceptionIfFFmpegError();
            outputStream->time_base = outputEncoderCodecContext->time_base;

            // open the file and write the header
            ffmpeg.avio_open(&outputFormatContext.Pointer->pb, outputFileName, ffmpeg.AVIO_FLAG_WRITE).ThrowExceptionIfFFmpegError();
            ffmpeg.avformat_write_header(outputFormatContext.Pointer, null).ThrowExceptionIfFFmpegError();

            var cropNeeded = !(vm.Camera.KeyFrames.Count == 0
                || (vm.Camera.KeyFrames.Count == 1 && vm.Camera.KeyFrames[0].InnerObject == new RectangleModel(0, 0, frameSize.Width, frameSize.Height)));

            var extraMfData = new Dictionary<MediaFileModel, MediaFileData>();
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

        }

        public void Encode(SafeAVFrame frame)
        {
            using var packet = new SafeAVPacket();

            int error;
            do
            {
                ffmpeg.avcodec_send_frame(outputEncoderCodecContext, frame.Pointer).ThrowExceptionIfFFmpegError();
                error = ffmpeg.avcodec_receive_packet(outputEncoderCodecContext, packet.Pointer).ThrowExceptionIfFFmpegErrorOtherThanAgainEof();
            } while (error == FFmpegSetup.AVERROR_EAGAIN);
        }

        static AVCodecID FindCodecIdFromFileName(string outputFileName) =>
            Path.GetExtension(outputFileName) switch
            {
                ".webm" => AVCodecID.AV_CODEC_ID_VP8,
                ".mp4" => AVCodecID.AV_CODEC_ID_H264,
                _ => throw new ArgumentException($"Could not figure out codec ID for {outputFileName}")
            };
    }
}
