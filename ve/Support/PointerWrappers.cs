


using FFmpeg.AutoGen;
using System;
using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices;

#pragma warning disable CA1801
#pragma warning disable IDE0060

internal partial class SafeAVPacket : SafeHandle
{
	[ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
	public unsafe SafeAVPacket(): base(IntPtr.Zero, true) => SetHandle(new IntPtr(ffmpeg.av_packet_alloc()));

    public override bool IsInvalid => handle == IntPtr.Zero;

	[ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
	unsafe protected override bool ReleaseHandle()
	{
		var ptr = (AVPacket *)handle.ToPointer();
		ffmpeg.av_packet_free(&ptr);
		SetHandle(IntPtr.Zero);
		return true;
	}

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1720:Identifier contains type name", Justification = "Descriptive name, leave alone")]
	unsafe public AVPacket *Pointer 
	{
		[ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
		get { return (AVPacket *)handle; }
	}
}

internal partial class SafeAVFrame : SafeHandle
{
	[ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
	public unsafe SafeAVFrame(): base(IntPtr.Zero, true) => SetHandle(new IntPtr(ffmpeg.av_frame_alloc()));

    public override bool IsInvalid => handle == IntPtr.Zero;

	[ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
	unsafe protected override bool ReleaseHandle()
	{
		var ptr = (AVFrame *)handle.ToPointer();
		ffmpeg.av_frame_free(&ptr);
		SetHandle(IntPtr.Zero);
		return true;
	}

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1720:Identifier contains type name", Justification = "Descriptive name, leave alone")]
	unsafe public AVFrame *Pointer 
	{
		[ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
		get { return (AVFrame *)handle; }
	}
}

internal partial class SafeAVFormatContext : SafeHandle
{
	public SafeAVFormatContext(IntPtr handle): base(IntPtr.Zero, true) =>
		SetHandle(handle);

    public unsafe delegate void InitDelegate(AVFormatContext **ptr);

	[ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
	public unsafe SafeAVFormatContext(InitDelegate init): base(IntPtr.Zero, true)
	{
		AVFormatContext *ptr = default;
		init(&ptr);
		SetHandle(new IntPtr(ptr));
	}

    public override bool IsInvalid => handle == IntPtr.Zero;

	[ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
	unsafe protected override bool ReleaseHandle()
	{
		var ptr = (AVFormatContext *)handle.ToPointer();
		ffmpeg.avformat_free_context(ptr);
		SetHandle(IntPtr.Zero);
		return true;
	}

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1720:Identifier contains type name", Justification = "Descriptive name, leave alone")]
	unsafe public AVFormatContext *Pointer 
	{
		[ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
		get { return (AVFormatContext *)handle; }
	}
}

internal partial class SafeAVBufferSinkParams : SafeHandle
{
	[ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
	public unsafe SafeAVBufferSinkParams(): base(IntPtr.Zero, true) => SetHandle(new IntPtr(ffmpeg.av_buffersink_params_alloc()));

    public override bool IsInvalid => handle == IntPtr.Zero;

	[ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
	unsafe protected override bool ReleaseHandle()
	{
		var ptr = (AVBufferSinkParams *)handle.ToPointer();
		ExtraReleaseHandleLogic(ptr);
		ffmpeg.av_free(ptr);
		SetHandle(IntPtr.Zero);
		return true;
	}

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1720:Identifier contains type name", Justification = "Descriptive name, leave alone")]
	unsafe public AVBufferSinkParams *Pointer 
	{
		[ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
		get { return (AVBufferSinkParams *)handle; }
	}
}


// AVBufferSinkParams pixel format helper
internal partial class SafeAVBufferSinkParams
{
	private bool MyPixelFormatChange = false;

	[ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
	public unsafe void ClearPixelFormats()
	{
		if(MyPixelFormatChange && Pointer->pixel_fmts != null)
		{
			ffmpeg.av_free(Pointer->pixel_fmts);
			Pointer->pixel_fmts = null;
		}
	}

	[ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
	public unsafe void SetPixelFormats(params AVPixelFormat[] formats)
	{
		ClearPixelFormats();

		var inbuffersize = formats.Length * sizeof(AVPixelFormat);
		var outbuffersize = inbuffersize + sizeof(AVPixelFormat);
		var sinkPixelFormats = (byte*)ffmpeg.av_malloc((ulong)outbuffersize);
		fixed (void* pformats = formats)
		    Buffer.MemoryCopy(pformats, sinkPixelFormats, outbuffersize, inbuffersize);
		Marshal.WriteInt32(new IntPtr(sinkPixelFormats + inbuffersize), (int)AVPixelFormat.AV_PIX_FMT_NONE);

		Pointer->pixel_fmts = (AVPixelFormat *)sinkPixelFormats;

		MyPixelFormatChange = true;
	}

	[ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
	protected unsafe void ExtraReleaseHandleLogic(AVBufferSinkParams *ptr) => ClearPixelFormats();
}

// avdict safe wrapper
internal class SafeAVDictionary : SafeHandle
{
	[ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
    unsafe public SafeAVDictionary() : base(IntPtr.Zero, true)
    {
		AVDictionary *dict;

		// work around to allocate this thing
		ffmpeg.av_dict_set(&dict, "key", "val", 0);
		ffmpeg.av_dict_get(dict, "", null, ffmpeg.AV_DICT_IGNORE_SUFFIX);

		SetHandle(new IntPtr(dict));
    }

    public override bool IsInvalid => handle == IntPtr.Zero;

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1720:Identifier contains type name", Justification = "Descriptive name, leave alone")]
	unsafe public AVDictionary *Pointer 
	{
		[ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
		get { return (AVDictionary *)handle; }
	}

	[ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
	unsafe protected override bool ReleaseHandle()
	{
		var dict = (AVDictionary *)handle.ToPointer();

		// remove any values still in
		AVDictionaryEntry *t = default;
		while((t = ffmpeg.av_dict_get(dict, "", t, ffmpeg.AV_DICT_IGNORE_SUFFIX)) != null)
		{
		}

		ffmpeg.av_dict_free(&dict);
		SetHandle(IntPtr.Zero);

		return true;
	}

    unsafe public delegate void UpdateDelegate(AVDictionary** dict);

	[ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
    unsafe public void Update(UpdateDelegate upd)
    {
        var dict = (AVDictionary*)handle;
        upd(&dict);
        SetHandle(new IntPtr(dict));
    }

	[ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
    unsafe public void Set(string key, string val, int flags = 0)
    {
        var dict = (AVDictionary*)handle;
        ffmpeg.av_dict_set(&dict, key, val, flags);
        SetHandle(new IntPtr(dict));
    }
}