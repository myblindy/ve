


using FFmpeg.AutoGen;
using System;
using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices;

public class SafeAVPacket : SafeHandle
{
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

	unsafe public AVPacket *Pointer =>
		(AVPacket *)handle;
}

public class SafeAVFrame : SafeHandle
{
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

	unsafe public AVFrame *Pointer =>
		(AVFrame *)handle;
}

public class SafeAVFormatContext : SafeHandle
{
	public SafeAVFormatContext(IntPtr handle): base(IntPtr.Zero, true) =>
		SetHandle(handle);

    public unsafe delegate void InitDelegate(AVFormatContext **ptr);
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

	unsafe public AVFormatContext *Pointer =>
		(AVFormatContext *)handle;
}


// avdict safe wrapper
public class SafeAVDictionary : SafeHandle
{
    unsafe public SafeAVDictionary() : base(IntPtr.Zero, true)
    {
		AVDictionary *dict;

		// work around to allocate this thing
		ffmpeg.av_dict_set(&dict, "key", "val", 0);
		ffmpeg.av_dict_get(dict, "", null, ffmpeg.AV_DICT_IGNORE_SUFFIX);

		SetHandle(new IntPtr(dict));
    }

    public override bool IsInvalid => handle == IntPtr.Zero;
	unsafe public AVDictionary *Pointer => (AVDictionary *)handle;

	[ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
	unsafe protected override bool ReleaseHandle()
	{
		var dict = (AVDictionary *)handle.ToPointer();

		// remove any values still in
		//AVDictionaryEntry *t = default;
		//while((t = ffmpeg.av_dict_get(dict, "", t, ffmpeg.AV_DICT_IGNORE_SUFFIX)) != null)
		//{
		//}

		ffmpeg.av_dict_free(&dict);
		SetHandle(IntPtr.Zero);

		return true;
	}

    unsafe public void Set(string key, string val, int flags = 0)
    {
        var dict = (AVDictionary*)handle;
        ffmpeg.av_dict_set(&dict, key, val, flags);
        SetHandle(new IntPtr(dict));
    }
}