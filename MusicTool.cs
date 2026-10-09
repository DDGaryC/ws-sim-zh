// WS 模擬器 音樂設定工具（獨立程式，不需要瀏覽器或額外元件）
// 解碼：Windows Media Foundation；播放：waveOut；介面：WinForms 自繪。
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;

[assembly: AssemblyTitle("WS 模擬器 音樂設定工具")]
[assembly: AssemblyProduct("WS 模擬器 中文化套件")]
[assembly: AssemblyVersion("1.0.0.0")]

// ======================================================================
// 音訊解碼（Media Foundation）
// ======================================================================
class AudioData
{
    public short[] Pcm;      // 交錯 16-bit
    public int Channels, Rate;
    public long Frames { get { return Pcm.Length / Channels; } }
    public double Duration { get { return (double)Frames / Rate; } }
}

static class MF
{
    [ComImport, Guid("44ae0fa8-ea31-4109-8d2e-4cae4997c555"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IMFMediaType
    {
        void GetItem(); void GetItemType(); void CompareItem(); void Compare();
        [PreserveSig] int GetUINT32([In, MarshalAs(UnmanagedType.LPStruct)] Guid key, out int value);
        void GetUINT64(); void GetDouble(); void GetGUID(); void GetStringLength(); void GetString();
        void GetAllocatedString(); void GetBlobSize(); void GetBlob(); void GetAllocatedBlob(); void GetUnknown();
        void SetItem(); void DeleteItem(); void DeleteAllItems();
        [PreserveSig] int SetUINT32([In, MarshalAs(UnmanagedType.LPStruct)] Guid key, int value);
        void SetUINT64(); void SetDouble();
        [PreserveSig] int SetGUID([In, MarshalAs(UnmanagedType.LPStruct)] Guid key, [In, MarshalAs(UnmanagedType.LPStruct)] Guid value);
    }

    [ComImport, Guid("c40a00f2-b93a-4d80-ae8c-5a1c634f58e4"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IMFSample
    {
        void GetItem(); void GetItemType(); void CompareItem(); void Compare(); void GetUINT32(); void GetUINT64();
        void GetDouble(); void GetGUID(); void GetStringLength(); void GetString(); void GetAllocatedString();
        void GetBlobSize(); void GetBlob(); void GetAllocatedBlob(); void GetUnknown(); void SetItem(); void DeleteItem();
        void DeleteAllItems(); void SetUINT32(); void SetUINT64(); void SetDouble(); void SetGUID(); void SetString();
        void SetBlob(); void SetUnknown(); void LockStore(); void UnlockStore(); void GetCount(); void GetItemByIndex();
        void CopyAllItems();
        void GetSampleFlags(); void SetSampleFlags(); void GetSampleTime();
        [PreserveSig] int SetSampleTime(long time);
        void GetSampleDuration();
        [PreserveSig] int SetSampleDuration(long duration);
        void GetBufferCount(); void GetBufferByIndex();
        [PreserveSig] int ConvertToContiguousBuffer(out IMFMediaBuffer buffer);
        [PreserveSig] int AddBuffer(IMFMediaBuffer buffer);
    }

    [ComImport, Guid("045FA593-8799-42b8-BC8D-8968C6453507"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IMFMediaBuffer
    {
        [PreserveSig] int Lock(out IntPtr buffer, out int maxLength, out int currentLength);
        [PreserveSig] int Unlock();
        [PreserveSig] int GetCurrentLength(out int length);
        [PreserveSig] int SetCurrentLength(int length);
    }

    [ComImport, Guid("3137f1cd-fe5e-4805-a5d8-fb477448cb3d"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IMFSinkWriter
    {
        [PreserveSig] int AddStream(IMFMediaType targetMediaType, out int streamIndex);
        [PreserveSig] int SetInputMediaType(int streamIndex, IMFMediaType inputMediaType, IntPtr encodingParameters);
        [PreserveSig] int BeginWriting();
        [PreserveSig] int WriteSample(int streamIndex, IMFSample sample);
        void SendStreamTick(); void PlaceMarker(); void NotifyEndOfSegment(); void Flush();
        [PreserveSig] int DoFinalize();
    }

    [ComImport, Guid("70ae66f2-c809-4e4f-8915-bdcb406b7993"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IMFSourceReader
    {
        void GetStreamSelection();
        [PreserveSig] int SetStreamSelection(int streamIndex, int selected);
        void GetNativeMediaType();
        [PreserveSig] int GetCurrentMediaType(int streamIndex, out IMFMediaType mediaType);
        [PreserveSig] int SetCurrentMediaType(int streamIndex, IntPtr reserved, IMFMediaType mediaType);
        void SetCurrentPosition();
        [PreserveSig] int ReadSample(int streamIndex, int controlFlags, out int actualStreamIndex, out int streamFlags, out long timestamp, out IMFSample sample);
    }

    [DllImport("mfplat.dll")] static extern int MFStartup(int version, int flags);
    [DllImport("mfplat.dll")] static extern int MFCreateMediaType(out IMFMediaType mediaType);
    [DllImport("mfreadwrite.dll")] static extern int MFCreateSinkWriterFromURL([MarshalAs(UnmanagedType.LPWStr)] string url, IntPtr byteStream, IntPtr attributes, out IMFSinkWriter writer);
    [DllImport("mfplat.dll")] static extern int MFCreateSample(out IMFSample sample);
    [DllImport("mfplat.dll")] static extern int MFCreateMemoryBuffer(int maxLength, out IMFMediaBuffer buffer);
    [DllImport("mfreadwrite.dll")] static extern int MFCreateSourceReaderFromURL([MarshalAs(UnmanagedType.LPWStr)] string url, IntPtr attributes, out IMFSourceReader reader);

    static readonly Guid MT_MAJOR = new Guid("48eba18e-f8c9-4687-bf11-0a74c9f96a8f");
    static readonly Guid MT_SUBTYPE = new Guid("f7e34c9a-42e8-4714-b74b-cb29d72c35e5");
    static readonly Guid MT_CHANNELS = new Guid("37e48bf5-645e-4c5b-89de-ada9e29b696a");
    static readonly Guid MT_RATE = new Guid("5faeeae7-0290-4c31-9e8a-c534f68d9dba");
    static readonly Guid MT_BITS = new Guid("f2deb57f-40fa-4764-aa33-ed4f2d1ff669");
    static readonly Guid TYPE_AUDIO = new Guid("73647561-0000-0010-8000-00AA00389B71");
    static readonly Guid FORMAT_PCM = new Guid("00000001-0000-0010-8000-00AA00389B71");
    static readonly Guid FORMAT_MP3 = new Guid("00000055-0000-0010-8000-00AA00389B71");
    static readonly Guid MT_AVG_BYTES = new Guid("1aab75c8-cfef-451c-ab95-ac034b8e1731");
    static readonly Guid MT_BLOCK_ALIGN = new Guid("322de230-9eeb-43bd-ab7a-ff412251541d");
    const int FIRST_AUDIO = -3, ALL_STREAMS = -2, EOS = 2;
    static bool started;
    static readonly object startLock = new object();

    static void Check(int hr, string what) { if (hr < 0) throw new Exception(what + " 失敗 (0x" + hr.ToString("X8") + ")"); }

    public static AudioData Decode(string path)
    {
        lock (startLock) { if (!started) { Check(MFStartup(0x00020070, 0), "MFStartup"); started = true; } }
        IMFSourceReader reader;
        Check(MFCreateSourceReaderFromURL(path, IntPtr.Zero, out reader), "開啟音訊檔");
        try
        {
            reader.SetStreamSelection(ALL_STREAMS, 0);
            reader.SetStreamSelection(FIRST_AUDIO, 1);
            IMFMediaType want;
            Check(MFCreateMediaType(out want), "MFCreateMediaType");
            want.SetGUID(MT_MAJOR, TYPE_AUDIO);
            want.SetGUID(MT_SUBTYPE, FORMAT_PCM);
            want.SetUINT32(MT_BITS, 16);
            Check(reader.SetCurrentMediaType(FIRST_AUDIO, IntPtr.Zero, want), "設定解碼格式（可能是不支援的格式）");
            Marshal.ReleaseComObject(want);
            IMFMediaType cur;
            Check(reader.GetCurrentMediaType(FIRST_AUDIO, out cur), "取得音訊格式");
            int ch, rate;
            cur.GetUINT32(MT_CHANNELS, out ch); cur.GetUINT32(MT_RATE, out rate);
            Marshal.ReleaseComObject(cur);
            var ms = new MemoryStream();
            byte[] tmp = new byte[0];
            while (true)
            {
                int idx, flags; long ts; IMFSample sample;
                Check(reader.ReadSample(FIRST_AUDIO, 0, out idx, out flags, out ts, out sample), "解碼");
                if (sample != null)
                {
                    IMFMediaBuffer buf;
                    sample.ConvertToContiguousBuffer(out buf);
                    IntPtr p; int max, len;
                    buf.Lock(out p, out max, out len);
                    if (tmp.Length < len) tmp = new byte[len];
                    Marshal.Copy(p, tmp, 0, len);
                    buf.Unlock();
                    ms.Write(tmp, 0, len);
                    Marshal.ReleaseComObject(buf);
                    Marshal.ReleaseComObject(sample);
                }
                if ((flags & EOS) != 0) break;
            }
            byte[] bytes = ms.ToArray();
            var pcm = new short[bytes.Length / 2];
            Buffer.BlockCopy(bytes, 0, pcm, 0, pcm.Length * 2);
            if (ch < 1) ch = 2;
            return new AudioData { Pcm = pcm, Channels = ch, Rate = rate };
        }
        finally { Marshal.ReleaseComObject(reader); }
    }

    // 把 pcm 的 [f0, f1) 編碼成 mp3（Windows 內建的 Media Foundation mp3 編碼器）
    public static void EncodeMp3(string path, short[] pcm, int ch, int rate, long f0, long f1, int kbps)
    {
        lock (startLock) { if (!started) { Check(MFStartup(0x00020070, 0), "MFStartup"); started = true; } }
        IMFSinkWriter w;
        Check(MFCreateSinkWriterFromURL(path, IntPtr.Zero, IntPtr.Zero, out w), "建立 mp3 檔");
        try
        {
            IMFMediaType o;
            Check(MFCreateMediaType(out o), "MFCreateMediaType");
            o.SetGUID(MT_MAJOR, TYPE_AUDIO); o.SetGUID(MT_SUBTYPE, FORMAT_MP3);
            o.SetUINT32(MT_CHANNELS, ch); o.SetUINT32(MT_RATE, rate); o.SetUINT32(MT_AVG_BYTES, kbps * 1000 / 8);
            int si;
            Check(w.AddStream(o, out si), "設定 mp3 格式（這台電腦可能沒有 mp3 編碼器）");
            IMFMediaType i;
            Check(MFCreateMediaType(out i), "MFCreateMediaType");
            i.SetGUID(MT_MAJOR, TYPE_AUDIO); i.SetGUID(MT_SUBTYPE, FORMAT_PCM);
            i.SetUINT32(MT_CHANNELS, ch); i.SetUINT32(MT_RATE, rate); i.SetUINT32(MT_BITS, 16);
            i.SetUINT32(MT_BLOCK_ALIGN, ch * 2); i.SetUINT32(MT_AVG_BYTES, rate * ch * 2);
            Check(w.SetInputMediaType(si, i, IntPtr.Zero), "設定輸入格式");
            Check(w.BeginWriting(), "開始寫入 mp3");
            long t = 0;
            for (long f = f0; f < f1; f += rate)
            {
                int n = (int)Math.Min(rate, f1 - f), bytes = n * ch * 2;
                IMFMediaBuffer buf; IMFSample smp;
                Check(MFCreateMemoryBuffer(bytes, out buf), "MFCreateMemoryBuffer");
                IntPtr ptr; int max, cur;
                buf.Lock(out ptr, out max, out cur);
                Marshal.Copy(pcm, checked((int)(f * ch)), ptr, n * ch);
                buf.Unlock();
                buf.SetCurrentLength(bytes);
                Check(MFCreateSample(out smp), "MFCreateSample");
                smp.AddBuffer(buf);
                long dur = (long)n * 10000000L / rate;
                smp.SetSampleTime(t); smp.SetSampleDuration(dur); t += dur;
                Check(w.WriteSample(si, smp), "寫入 mp3");
                Marshal.ReleaseComObject(smp); Marshal.ReleaseComObject(buf);
            }
            Check(w.DoFinalize(), "完成 mp3");
        }
        finally { Marshal.ReleaseComObject(w); }
    }

    // mp3 編碼／解碼會在開頭多出一小段靜音。找出原音的第 refFrame 個取樣，在解碼後的 enc 裡是第幾個
    public static long FindOffset(AudioData orig, long refFrame, AudioData enc, long from, long to)
    {
        int win = 4096;
        long best = from; double bestErr = double.MaxValue;
        int oc = orig.Channels, ec = enc.Channels;
        for (long lag = Math.Max(0, from); lag <= to; lag++)
        {
            if (lag + win >= enc.Frames || refFrame + win >= orig.Frames) break;
            double err = 0;
            for (int k = 0; k < win; k += 2)
            {
                double a = orig.Pcm[(refFrame + k) * oc] + (oc > 1 ? orig.Pcm[(refFrame + k) * oc + 1] : 0);
                double b = enc.Pcm[(lag + k) * ec] + (ec > 1 ? enc.Pcm[(lag + k) * ec + 1] : 0);
                err += (a - b) * (a - b);
                if (err >= bestErr) break;
            }
            if (err < bestErr) { bestErr = err; best = lag; }
        }
        return best;
    }
}

// ======================================================================
// 播放（waveOut，支援精準循環）
// ======================================================================
class Player
{
    [StructLayout(LayoutKind.Sequential)]
    struct WAVEFORMATEX { public ushort wFormatTag, nChannels; public uint nSamplesPerSec, nAvgBytesPerSec; public ushort nBlockAlign, wBitsPerSample, cbSize; }
    [StructLayout(LayoutKind.Sequential)]
    struct WAVEHDR { public IntPtr lpData; public uint dwBufferLength, dwBytesRecorded; public IntPtr dwUser; public uint dwFlags, dwLoops; public IntPtr lpNext, reserved; }
    [StructLayout(LayoutKind.Explicit)]
    struct MMTIME { [FieldOffset(0)] public uint wType; [FieldOffset(4)] public uint u; [FieldOffset(8)] public uint pad; }

    [DllImport("winmm.dll")] static extern int waveOutOpen(out IntPtr h, uint dev, ref WAVEFORMATEX fmt, IntPtr cb, IntPtr inst, uint flags);
    [DllImport("winmm.dll")] static extern int waveOutPrepareHeader(IntPtr h, IntPtr hdr, int size);
    [DllImport("winmm.dll")] static extern int waveOutUnprepareHeader(IntPtr h, IntPtr hdr, int size);
    [DllImport("winmm.dll")] static extern int waveOutWrite(IntPtr h, IntPtr hdr, int size);
    [DllImport("winmm.dll")] static extern int waveOutReset(IntPtr h);
    [DllImport("winmm.dll")] static extern int waveOutClose(IntPtr h);
    [DllImport("winmm.dll")] static extern int waveOutGetPosition(IntPtr h, ref MMTIME t, int size);

    const int NBUF = 4;
    static readonly int HDR = Marshal.SizeOf(typeof(WAVEHDR));
    static readonly int FLAGS_OFS = (int)Marshal.OffsetOf(typeof(WAVEHDR), "dwFlags");

    AudioData data;
    IntPtr hwo = IntPtr.Zero;
    Thread thread;
    volatile bool running;
    readonly object sync = new object();
    long cursor;                 // 下一個要送出的歌曲 frame
    long loopS = -1, loopE = -1; // frame
    long outTotal;               // 已送出的輸出 frame 數
    readonly List<long[]> segs = new List<long[]>(); // {輸出起點, 歌曲起點, 長度}
    public volatile float Gain = 1f;
    public string Key;           // 目前播放的檔案
    public event Action Ended;

    public bool Playing { get { return running; } }

    public void SetLoop(double s, double e)
    {
        lock (sync)
        {
            if (data == null || e <= s || s < 0) { loopS = loopE = -1; return; }
            loopS = (long)(s * data.Rate); loopE = Math.Min(data.Frames, (long)(e * data.Rate));
        }
    }

    public double Position
    {
        get
        {
            lock (sync)
            {
                if (!running || data == null) return 0;
                var t = new MMTIME { wType = 2 };
                waveOutGetPosition(hwo, ref t, Marshal.SizeOf(typeof(MMTIME)));
                long p = t.u;
                if (t.wType == 4) p = t.u / (data.Channels * 2);          // TIME_BYTES
                else if (t.wType == 1) p = (long)t.u * data.Rate / 1000;  // TIME_MS
                for (int i = segs.Count - 1; i >= 0; i--)
                {
                    var s = segs[i];
                    if (p >= s[0]) return (s[1] + Math.Min(p - s[0], s[2])) / (double)data.Rate;
                }
                return segs.Count > 0 ? segs[0][1] / (double)data.Rate : 0;
            }
        }
    }

    public void Play(AudioData d, string key, double from, double loopStart, double loopEnd)
    {
        Stop();
        data = d; Key = key;
        cursor = Math.Max(0, Math.Min(d.Frames - 1, (long)(from * d.Rate)));
        outTotal = 0; segs.Clear();
        SetLoop(loopStart, loopEnd);
        var fmt = new WAVEFORMATEX
        {
            wFormatTag = 1, nChannels = (ushort)d.Channels, nSamplesPerSec = (uint)d.Rate,
            wBitsPerSample = 16, nBlockAlign = (ushort)(d.Channels * 2), nAvgBytesPerSec = (uint)(d.Rate * d.Channels * 2)
        };
        if (waveOutOpen(out hwo, 0xFFFFFFFF, ref fmt, IntPtr.Zero, IntPtr.Zero, 0) != 0) { hwo = IntPtr.Zero; throw new Exception("無法開啟音效裝置"); }
        running = true;
        thread = new Thread(Feed) { IsBackground = true, Priority = ThreadPriority.AboveNormal };
        thread.Start();
    }

    void Feed()
    {
        int bufFrames = Math.Max(1024, data.Rate / 20);
        int bytes = bufFrames * data.Channels * 2;
        var hdrs = new IntPtr[NBUF];
        var short_ = new short[bufFrames * data.Channels];
        bool finished = false;
        try
        {
            for (int i = 0; i < NBUF; i++)
            {
                hdrs[i] = Marshal.AllocHGlobal(HDR);
                var h = new WAVEHDR { lpData = Marshal.AllocHGlobal(bytes), dwBufferLength = (uint)bytes };
                Marshal.StructureToPtr(h, hdrs[i], false);
                waveOutPrepareHeader(hwo, hdrs[i], HDR);
                if (!Fill(hdrs[i], short_, bufFrames)) { finished = true; break; }
            }
            while (running)
            {
                bool allDone = true;
                for (int i = 0; i < NBUF; i++)
                {
                    if (hdrs[i] == IntPtr.Zero) continue;
                    int f = Marshal.ReadInt32(hdrs[i], FLAGS_OFS);
                    if ((f & 1) == 0) { allDone = false; continue; }   // 還在播
                    if (!finished && !Fill(hdrs[i], short_, bufFrames)) finished = true;
                    else if (!finished) allDone = false;
                }
                if (finished && allDone) break;
                Thread.Sleep(5);
            }
        }
        finally
        {
            bool natural = running;
            lock (sync) { running = false; }
            waveOutReset(hwo);
            foreach (var h in hdrs)
            {
                if (h == IntPtr.Zero) continue;
                waveOutUnprepareHeader(hwo, h, HDR);
                var w = (WAVEHDR)Marshal.PtrToStructure(h, typeof(WAVEHDR));
                Marshal.FreeHGlobal(w.lpData); Marshal.FreeHGlobal(h);
            }
            waveOutClose(hwo); hwo = IntPtr.Zero;
            if (natural && Ended != null) Ended();
        }
    }

    // 填一個緩衝區；回傳 false 表示歌曲已結束、沒有資料
    bool Fill(IntPtr hdr, short[] buf, int bufFrames)
    {
        int ch = data.Channels, n = 0;
        float g = Gain;
        lock (sync)
        {
            while (n < bufFrames)
            {
                bool looping = loopE > 0 && cursor < loopE;
                long end = looping ? loopE : data.Frames;
                if (cursor >= end) break;
                int take = (int)Math.Min(bufFrames - n, end - cursor);
                long src = cursor * ch;
                for (int i = 0; i < take * ch; i++)
                {
                    float v = data.Pcm[src + i] * g;
                    buf[n * ch + i] = (short)(v > 32767 ? 32767 : v < -32768 ? -32768 : v);
                }
                segs.Add(new long[] { outTotal + n, cursor, take });
                n += take; cursor += take;
                if (looping && cursor >= loopE) cursor = loopS;   // 到循環終點，跳回起點
            }
            outTotal += n;
            if (segs.Count > 64) segs.RemoveRange(0, segs.Count - 64);
        }
        if (n == 0) return false;
        var w = (WAVEHDR)Marshal.PtrToStructure(hdr, typeof(WAVEHDR));
        Marshal.Copy(buf, 0, w.lpData, n * ch);
        w.dwBufferLength = (uint)(n * ch * 2);
        w.dwFlags &= ~1u;
        Marshal.StructureToPtr(w, hdr, false);
        waveOutWrite(hwo, hdr, HDR);
        return true;
    }

    public void Stop()
    {
        running = false;
        if (thread != null) { thread.Join(2000); thread = null; }
    }
}

// ======================================================================
// 樣式與自繪元件
// ======================================================================
static class UI
{
    public static readonly Color Bg = Color.FromArgb(244, 245, 249), Panel = Color.White, Line = Color.FromArgb(227, 230, 239);
    public static readonly Color Text = Color.FromArgb(31, 35, 48), Muted = Color.FromArgb(107, 113, 131);
    public static readonly Color Accent = Color.FromArgb(79, 91, 213), AccentSoft = Color.FromArgb(236, 238, 253), AccentLine = Color.FromArgb(201, 206, 246);
    public static readonly Color Hover = Color.FromArgb(246, 247, 251), Danger = Color.FromArgb(214, 69, 69), Ok = Color.FromArgb(31, 157, 85);
    public static readonly Color Play = Color.FromArgb(229, 72, 77), Wave = Color.FromArgb(154, 163, 199);
    public const string FontName = "Microsoft JhengHei UI";
    public static Font F(float size, FontStyle st = FontStyle.Regular) { return new Font(FontName, size, st); }

    public static GraphicsPath Round(RectangleF r, float rad)
    {
        var p = new GraphicsPath();
        float d = rad * 2;
        if (rad <= 0) { p.AddRectangle(r); return p; }
        p.AddArc(r.X, r.Y, d, d, 180, 90); p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }
    public static void Fill(Graphics g, RectangleF r, float rad, Color c) { using (var p = Round(r, rad)) using (var b = new SolidBrush(c)) g.FillPath(b, p); }
    public static void Stroke(Graphics g, RectangleF r, float rad, Color c, float w = 1, DashStyle ds = DashStyle.Solid)
    { using (var p = Round(r, rad)) using (var pen = new Pen(c, w) { DashStyle = ds }) g.DrawPath(pen, p); }
    public static void Smooth(Graphics g) { g.SmoothingMode = SmoothingMode.AntiAlias; g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit; }
    public static void Str(Graphics g, string s, Font f, Color c, RectangleF r, StringAlignment h = StringAlignment.Near, bool ellipsis = true)
    {
        using (var sf = new StringFormat { Alignment = h, LineAlignment = StringAlignment.Center, Trimming = ellipsis ? StringTrimming.EllipsisCharacter : StringTrimming.None, FormatFlags = StringFormatFlags.NoWrap })
        using (var b = new SolidBrush(c)) g.DrawString(s, f, b, r, sf);
    }
    public static float W(Graphics g, string s, Font f) { return g.MeasureString(s, f, 1000, StringFormat.GenericTypographic).Width; }
}

class FlatButton : Control
{
    public bool Primary, Ghost, Round;
    bool hover, down;
    public FlatButton(string text, int w = 0)
    {
        Text = text; Font = UI.F(10f); Cursor = Cursors.Hand;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        using (var g = CreateGraphics()) Size = new Size(Math.Max(w, (int)UI.W(g, text, Font) + 28), 34);
    }
    protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover = down = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { down = true; Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { down = false; Invalidate(); base.OnMouseUp(e); }
    protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
    protected override void OnTextChanged(EventArgs e) { Invalidate(); base.OnTextChanged(e); }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; UI.Smooth(g);
        var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
        float rad = Round ? Math.Min(Width, Height) / 2f - 1 : 8;
        Color bg, fg = UI.Text, border = UI.Line;
        if (Primary) { bg = hover ? Color.FromArgb(98, 109, 225) : UI.Accent; fg = Color.White; border = bg; if (down) bg = Color.FromArgb(66, 77, 196); }
        else if (Ghost) { bg = hover ? UI.AccentSoft : Color.Transparent; border = Color.Transparent; }
        else { bg = hover ? Color.FromArgb(248, 249, 252) : Color.White; if (hover) border = Color.FromArgb(197, 202, 219); }
        if (!Enabled) { fg = Color.FromArgb(170, 174, 188); if (Primary) { bg = Color.FromArgb(188, 193, 236); border = bg; fg = Color.White; } }
        if (bg.A > 0) UI.Fill(g, r, rad, bg);
        if (border.A > 0) UI.Stroke(g, r, rad, border);
        UI.Str(g, Text, Font, fg, new RectangleF(0, 0, Width, Height), StringAlignment.Center);
    }
}

class Slider : Control
{
    double val = 1;
    bool drag;
    public event Action ValueChanged;
    public double Value { get { return val; } set { val = Math.Max(0, Math.Min(1, value)); Invalidate(); } }
    public Slider()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        Cursor = Cursors.Hand; Size = new Size(140, 24);
    }
    void SetFrom(int x) { Value = (x - 10) / (double)Math.Max(1, Width - 20); if (ValueChanged != null) ValueChanged(); }
    protected override void OnMouseDown(MouseEventArgs e) { drag = true; SetFrom(e.X); }
    protected override void OnMouseMove(MouseEventArgs e) { if (drag) SetFrom(e.X); }
    protected override void OnMouseUp(MouseEventArgs e) { drag = false; }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; UI.Smooth(g); g.Clear(Parent.BackColor);
        float y = Height / 2f, x0 = 10, x1 = Width - 10, xv = x0 + (float)val * (x1 - x0);
        UI.Fill(g, new RectangleF(x0, y - 2.5f, x1 - x0, 5), 2.5f, UI.Line);
        UI.Fill(g, new RectangleF(x0, y - 2.5f, xv - x0, 5), 2.5f, UI.Accent);
        UI.Fill(g, new RectangleF(xv - 8, y - 8, 16, 16), 8, Color.White);
        UI.Stroke(g, new RectangleF(xv - 8, y - 8, 16, 16), 8, UI.Accent, 2);
    }
}

// 可點擊的自繪區塊：卡片與音效表格共用
abstract class HitView : Control
{
    protected class Hit { public RectangleF R; public string Act; public object Arg; public bool Dbl; }
    protected readonly List<Hit> hits = new List<Hit>();
    protected Hit hover;
    public event Action<string, object> Act;
    public event Action<string, object> DoubleAct;
    protected HitView()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        Font = UI.F(10f);
    }
    protected void AddHit(RectangleF r, string act, object arg) { hits.Add(new Hit { R = r, Act = act, Arg = arg }); }
    Hit Find(Point p) { for (int i = hits.Count - 1; i >= 0; i--) if (hits[i].R.Contains(p)) return hits[i]; return null; }
    protected bool IsHover(string act, object arg) { return hover != null && hover.Act == act && Equals(hover.Arg, arg); }
    protected override void OnMouseMove(MouseEventArgs e)
    {
        var h = Find(e.Location);
        if (h != hover) { hover = h; Cursor = h != null ? Cursors.Hand : Cursors.Default; Invalidate(); }
    }
    protected override void OnMouseLeave(EventArgs e) { hover = null; Invalidate(); }
    protected override void OnMouseClick(MouseEventArgs e)
    {
        var h = Find(e.Location);
        if (h != null && e.Button == MouseButtons.Left && Act != null) Act(h.Act, h.Arg);
    }
    protected override void OnMouseDoubleClick(MouseEventArgs e)
    {
        var h = Find(e.Location);
        if (h != null && DoubleAct != null) DoubleAct(h.Act, h.Arg);
    }
}

class SlotItem { public string Badge, Text, Tip; public bool Loop, Selected, CanDelete; public Sel Sel; }

class CardView : HitView
{
    public string Title;
    public List<string[]> HeadActs = new List<string[]>();   // {文字, act}
    public List<SlotItem> Slots = new List<SlotItem>();
    public string FooterText, FooterAct;
    public bool IsAdd; public string AddSub;
    public object Tag2;
    readonly ToolTip tip = new ToolTip();

    public void Fit(int w)
    {
        Width = w;
        Height = IsAdd ? 178 : 50 + Slots.Count * 40 + (FooterText != null ? 46 : 6);
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; UI.Smooth(g); g.Clear(UI.Bg);
        hits.Clear();
        var card = new RectangleF(1, 1, Width - 3, Height - 3);
        if (IsAdd)
        {
            bool hv = hover != null;
            UI.Fill(g, card, 10, hv ? UI.AccentSoft : UI.Bg);
            UI.Stroke(g, card, 10, hv ? UI.Accent : Color.FromArgb(207, 212, 228), 2, DashStyle.Dash);
            using (var f = UI.F(11f, FontStyle.Bold)) UI.Str(g, Title, f, hv ? UI.Accent : UI.Muted, new RectangleF(0, Height / 2f - 26, Width, 26), StringAlignment.Center);
            using (var f = UI.F(9f)) UI.Str(g, AddSub, f, UI.Muted, new RectangleF(0, Height / 2f + 2, Width, 22), StringAlignment.Center);
            AddHit(card, "add", Tag2);
            return;
        }
        using (var sh = new SolidBrush(Color.FromArgb(10, 20, 24, 40))) g.FillPath(sh, UI.Round(new RectangleF(1, 3, Width - 3, Height - 3), 10));
        UI.Fill(g, card, 10, UI.Panel);
        UI.Stroke(g, card, 10, UI.Line);
        using (var f = UI.F(11f, FontStyle.Bold)) UI.Str(g, Title, f, UI.Text, new RectangleF(14, 10, Width - 28, 28));
        float x = Width - 12;
        using (var f = UI.F(9.5f))
            for (int i = HeadActs.Count - 1; i >= 0; i--)
            {
                float w = UI.W(g, HeadActs[i][0], f) + 18;
                var r = new RectangleF(x - w, 10, w, 28);
                if (IsHover(HeadActs[i][1], Tag2)) UI.Fill(g, r, 7, HeadActs[i][1] == "delset" ? Color.FromArgb(253, 232, 232) : UI.AccentSoft);
                UI.Str(g, HeadActs[i][0], f, HeadActs[i][1] == "delset" && IsHover("delset", Tag2) ? UI.Danger : UI.Muted, r, StringAlignment.Center);
                AddHit(r, HeadActs[i][1], Tag2);
                x -= w + 4;
            }
        float y = 46;
        using (var fb = UI.F(9f, FontStyle.Bold))
        using (var fn = UI.F(10f))
            foreach (var s in Slots)
            {
                var row = new RectangleF(10, y, Width - 22, 36);
                AddHit(row, "slot", s.Sel);
                bool hv = hover != null && Equals(hover.Arg, s.Sel);
                if (s.Selected) { UI.Fill(g, row, 8, UI.AccentSoft); UI.Stroke(g, row, 8, UI.AccentLine); }
                else if (hv) UI.Fill(g, row, 8, UI.Hover);
                var badge = new RectangleF(row.X + 8, row.Y + 7, 84, 22);
                UI.Fill(g, badge, 6, s.Selected ? Color.White : UI.AccentSoft);
                UI.Str(g, s.Badge, fb, UI.Accent, badge, StringAlignment.Center);
                float right = row.Right - 6;
                var btns = new List<string[]> { new[] { "▶", "play" }, new[] { "換歌", "replace" } };
                if (s.CanDelete) btns.Add(new[] { "✕", "delete" });
                for (int i = btns.Count - 1; i >= 0; i--)
                {
                    float w = btns[i][1] == "replace" ? 46 : 30;
                    var r = new RectangleF(right - w, row.Y + 4, w, 28);
                    bool bh = IsHover(btns[i][1], s.Sel);
                    if (bh) UI.Fill(g, r, 6, btns[i][1] == "delete" ? Color.FromArgb(253, 232, 232) : (s.Selected ? Color.White : UI.AccentSoft));
                    UI.Str(g, btns[i][0], fn, btns[i][1] == "delete" && bh ? UI.Danger : (hv || s.Selected ? UI.Text : UI.Muted), r, StringAlignment.Center);
                    AddHit(r, btns[i][1], s.Sel);
                    right -= w + 2;
                }
                if (s.Loop) { UI.Str(g, "↻", fn, UI.Accent, new RectangleF(right - 20, row.Y, 20, row.Height), StringAlignment.Center); right -= 22; }
                UI.Str(g, s.Text, fn, UI.Text, new RectangleF(badge.Right + 10, row.Y, right - badge.Right - 12, row.Height));
                y += 40;
            }
        if (FooterText != null)
        {
            using (var fn = UI.F(10f))
            {
                float w = UI.W(g, FooterText, fn) + 26;
                var r = new RectangleF(14, y + 6, w, 32);
                UI.Fill(g, r, 8, IsHover(FooterAct, Tag2) ? UI.Hover : Color.White);
                UI.Stroke(g, r, 8, UI.Line);
                UI.Str(g, FooterText, fn, UI.Text, r, StringAlignment.Center);
                AddHit(r, FooterAct, Tag2);
            }
        }
    }
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        string t = null;
        if (hover != null && hover.Act == "slot") foreach (var s in Slots) if (Equals(s.Sel, hover.Arg)) t = s.Tip;
        if (tip.GetToolTip(this) != (t ?? "")) tip.SetToolTip(this, t ?? "");
    }
}

class SfxRow { public string Name, Key; public List<string> Files; public int SelIdx = -1; }

class SfxTable : HitView
{
    public List<SfxRow> Rows = new List<SfxRow>();
    const int HeadH = 36, RowH = 46;
    public void Fit(int w) { Width = w; Height = HeadH + Rows.Count * RowH + 3; }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; UI.Smooth(g); g.Clear(UI.Bg);
        hits.Clear();
        var box = new RectangleF(1, 1, Width - 3, Height - 3);
        UI.Fill(g, box, 10, UI.Panel); UI.Stroke(g, box, 10, UI.Line);
        float cName = 16, cKey = Width * 0.2f, cFiles = Width * 0.36f, cBtn = Width - 236;
        using (var fh = UI.F(9f, FontStyle.Bold))
        {
            UI.Str(g, "項目", fh, UI.Muted, new RectangleF(cName, 1, 100, HeadH));
            UI.Str(g, "代號", fh, UI.Muted, new RectangleF(cKey, 1, 100, HeadH));
            UI.Str(g, "音效檔（點一下試聽）", fh, UI.Muted, new RectangleF(cFiles, 1, 200, HeadH));
        }
        using (var pen = new Pen(UI.Line)) g.DrawLine(pen, 1, HeadH, Width - 2, HeadH);
        float y = HeadH;
        using (var fb = UI.F(10f, FontStyle.Bold))
        using (var fk = new Font("Consolas", 9f))
        using (var fn = UI.F(9.5f))
        using (var pen = new Pen(UI.Line))
            for (int r = 0; r < Rows.Count; r++)
            {
                var row = Rows[r];
                UI.Str(g, row.Name, fb, UI.Text, new RectangleF(cName, y, cKey - cName - 8, RowH));
                UI.Str(g, row.Key, fk, UI.Muted, new RectangleF(cKey, y, cFiles - cKey - 8, RowH));
                float x = cFiles;
                for (int i = 0; i < row.Files.Count; i++)
                {
                    string label = "▶ " + Path.GetFileName(row.Files[i]);
                    float w = Math.Min(UI.W(g, label, fn) + 22 + (row.Files.Count > 1 ? 20 : 0), cBtn - x - 8);
                    if (w < 60) break;
                    var chip = new RectangleF(x, y + 9, w, 28);
                    var arg = new object[] { row.Key, i };
                    bool sel = row.SelIdx == i;
                    UI.Fill(g, chip, 14, sel ? UI.AccentSoft : (IsHoverChip(row.Key, i) ? UI.Hover : Color.White));
                    UI.Stroke(g, chip, 14, sel ? UI.AccentLine : UI.Line);
                    AddHit(chip, "chip", arg);
                    UI.Str(g, label, fn, UI.Text, new RectangleF(chip.X + 10, chip.Y, chip.Width - 14 - (row.Files.Count > 1 ? 20 : 0), chip.Height));
                    if (row.Files.Count > 1)
                    {
                        var xr = new RectangleF(chip.Right - 24, chip.Y + 3, 20, 22);
                        bool xh = hover != null && hover.Act == "delchip" && SameArg(hover.Arg, arg);
                        if (xh) UI.Fill(g, xr, 10, Color.FromArgb(253, 232, 232));
                        UI.Str(g, "×", fn, xh ? UI.Danger : UI.Muted, xr, StringAlignment.Center);
                        AddHit(xr, "delchip", arg);
                    }
                    x += w + 6;
                }
                var b1 = new RectangleF(cBtn, y + 8, 88, 30);
                var b2 = new RectangleF(cBtn + 94, y + 8, 120, 30);
                UI.Fill(g, b1, 8, IsHover("replsfx", row.Key) ? UI.Hover : Color.White); UI.Stroke(g, b1, 8, UI.Line);
                UI.Str(g, "換音效", fn, UI.Text, b1, StringAlignment.Center);
                if (IsHover("addsfx", row.Key)) UI.Fill(g, b2, 8, UI.AccentSoft);
                UI.Str(g, "＋ 再加一個", fn, UI.Muted, b2, StringAlignment.Center);
                AddHit(b1, "replsfx", row.Key); AddHit(b2, "addsfx", row.Key);
                y += RowH;
                if (r < Rows.Count - 1) g.DrawLine(pen, 1, y, Width - 2, y);
            }
    }
    static bool SameArg(object a, object b)
    {
        var x = a as object[]; var y = b as object[];
        return x != null && y != null && Equals(x[0], y[0]) && Equals(x[1], y[1]);
    }
    bool IsHoverChip(string key, int i) { return hover != null && hover.Act == "chip" && SameArg(hover.Arg, new object[] { key, i }); }
}

// ======================================================================
// 波形
// ======================================================================
class WaveView : Control
{
    public AudioData Data;
    public string Message = "載入中…";
    public bool LoopEditable;
    public double ViewA, ViewB;
    public Func<double> GetPos;             // 播放頭（秒），沒有則回傳 -1
    public Func<double[]> GetLoop;          // {開始, 結束} 或 null
    public Action<double, double, bool> SetLoop;   // (開始, 結束, 是否拖曳結束)
    public Action<double> Seek;
    Bitmap cache; string cacheKey;
    string dragMode; char dragEdge; double dragT0; int dragX0; bool dragMoved;
    double hoverT = -1;

    public WaveView()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
        Font = UI.F(9f);
    }
    float peak = 32768f;
    public void SetData(AudioData d)
    {
        Data = d; ViewA = 0; ViewB = d != null ? d.Duration : 0; cacheKey = null;
        if (d != null)
        {
            int pk = 1; long step = Math.Max(1, d.Pcm.Length / 2000000);
            for (long i = 0; i < d.Pcm.Length; i += step) { int v = Math.Abs((int)d.Pcm[i]); if (v > pk) pk = v; }
            peak = Math.Max(pk, 32768f / 12f);   // 小聲的音效放大顯示（最多 12 倍）
        }
        Invalidate();
    }
    float T2X(double t) { return (float)((t - ViewA) / (ViewB - ViewA) * Width); }
    double X2T(int x) { return ViewA + (double)x / Math.Max(1, Width) * (ViewB - ViewA); }
    char EdgeAt(int x)
    {
        var L = LoopEditable && GetLoop != null ? GetLoop() : null;
        if (L == null) return '\0';
        if (Math.Abs(T2X(L[0]) - x) < 7) return 's';
        if (Math.Abs(T2X(L[1]) - x) < 7) return 'e';
        return '\0';
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (Data == null || e.Button != MouseButtons.Left) return;
        Focus();
        char edge = EdgeAt(e.X);
        if (edge != '\0') { dragMode = "edge"; dragEdge = edge; }
        else { dragMode = "sel"; dragT0 = Clamp(X2T(e.X)); dragX0 = e.X; dragMoved = false; }
        Capture = true;
    }
    double Clamp(double t) { return Math.Max(0, Math.Min(Data.Duration, t)); }
    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (Data == null) return;
        hoverT = X2T(e.X);
        if (dragMode == null) Cursor = EdgeAt(e.X) != '\0' ? Cursors.SizeWE : (LoopEditable ? Cursors.Cross : Cursors.Hand);
        double t = Clamp(X2T(e.X));
        if (dragMode == "edge")
        {
            var L = GetLoop();
            if (L != null)
            {
                double s = L[0], en = L[1];
                if (dragEdge == 's') s = Math.Min(t, en - 0.05); else en = Math.Max(t, s + 0.05);
                SetLoop(s, en, false);
            }
        }
        else if (dragMode == "sel" && LoopEditable && Math.Abs(e.X - dragX0) > 4)
        {
            dragMoved = true;
            double s = Math.Min(dragT0, t), en = Math.Max(dragT0, t);
            if (en - s >= 0.05) SetLoop(s, en, false);
        }
        Invalidate();
    }
    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (dragMode == null) return;
        Capture = false;
        if (dragMode == "sel" && !dragMoved) { if (Seek != null) Seek(dragT0); }
        else { var L = GetLoop(); if (L != null) SetLoop(L[0], L[1], true); }
        dragMode = null;
        Invalidate();
    }
    protected override void OnMouseLeave(EventArgs e) { hoverT = -1; Invalidate(); }
    protected override void OnMouseWheel(MouseEventArgs e)
    {
        if (Data == null) return;
        double span = ViewB - ViewA, t = X2T(e.X);
        double ns = Math.Min(Data.Duration, Math.Max(0.5, span * (e.Delta < 0 ? 1.25 : 0.8)));
        ViewA = t - (t - ViewA) * ns / span; ViewB = ViewA + ns;
        if (ViewA < 0) { ViewB -= ViewA; ViewA = 0; }
        if (ViewB > Data.Duration) { ViewA -= ViewB - Data.Duration; ViewB = Data.Duration; ViewA = Math.Max(0, ViewA); }
        Invalidate();
    }
    protected override void OnMouseDoubleClick(MouseEventArgs e) { if (Data != null) { ViewA = 0; ViewB = Data.Duration; Invalidate(); } }

    Bitmap Build(int W, int H)
    {
        var bmp = new Bitmap(W, H);
        using (var g = Graphics.FromImage(bmp))
        using (var b = new SolidBrush(UI.Wave))
        {
            int ch = Data.Channels; var pcm = Data.Pcm;
            long a = (long)(ViewA * Data.Rate), z = (long)(ViewB * Data.Rate);
            double spp = (z - a) / (double)W;
            long stride = Math.Max(1, (long)(spp / 200));
            float mid = H / 2f, amp = H / 2f - 6;
            for (int x = 0; x < W; x++)
            {
                long s0 = a + (long)(x * spp), s1 = Math.Max(s0 + 1, a + (long)((x + 1) * spp));
                int mn = 0, mx = 0;
                for (long i = s0; i < s1 && i < Data.Frames; i += stride)
                    for (int c = 0; c < ch; c++) { int v = pcm[i * ch + c]; if (v < mn) mn = v; if (v > mx) mx = v; }
                float y0 = mid - Math.Min(1f, mx / peak) * amp, y1 = mid - Math.Max(-1f, mn / peak) * amp;
                g.FillRectangle(b, x, y0, 1, Math.Max(1, y1 - y0));
            }
        }
        return bmp;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Color.FromArgb(247, 248, 252));
        if (Data == null) { UI.Str(g, Message, UI.F(10f), UI.Muted, ClientRectangle, StringAlignment.Center); return; }
        string key = Width + "x" + Height + ":" + ViewA + "-" + ViewB;
        if (cacheKey != key) { if (cache != null) cache.Dispose(); cache = Build(Width, Height); cacheKey = key; }
        var L = LoopEditable && GetLoop != null ? GetLoop() : null;
        if (L != null)
            using (var b = new SolidBrush(Color.FromArgb(34, 79, 91, 213))) g.FillRectangle(b, T2X(L[0]), 0, T2X(L[1]) - T2X(L[0]), Height);
        g.DrawImageUnscaled(cache, 0, 0);
        // 時間刻度
        double span = ViewB - ViewA;
        double[] steps = { 0.5, 1, 2, 5, 10, 15, 30, 60, 120 };
        double step = 300;
        foreach (var s in steps) if (span / s < Width / 90.0) { step = s; break; }
        using (var b = new SolidBrush(Color.FromArgb(154, 160, 180)))
        using (var f = new Font("Consolas", 8.5f))
            for (double t = Math.Ceiling(ViewA / step) * step; t < ViewB; t += step)
            {
                float x = T2X(t);
                g.FillRectangle(b, x, Height - 6, 1, 6);
                g.DrawString(Fmt(t, step < 1), f, b, x + 3, Height - 18);
            }
        UI.Smooth(g);
        if (L != null)
            using (var bAcc = new SolidBrush(UI.Accent))
            using (var fb = UI.F(8.5f, FontStyle.Bold))
                foreach (var pair in new[] { new object[] { L[0], "開始" }, new object[] { L[1], "結束" } })
                {
                    float x = T2X((double)pair[0]); string name = (string)pair[1];
                    g.FillRectangle(bAcc, x - 1, 0, 2, Height);
                    float tw = UI.W(g, name, fb) + 12;
                    var r = new RectangleF(name == "開始" ? x : x - tw, 0, tw, 18);
                    g.FillRectangle(bAcc, r);
                    UI.Str(g, name, fb, Color.White, r, StringAlignment.Center, false);
                }
        if (hoverT >= 0 && dragMode == null)
            using (var b = new SolidBrush(Color.FromArgb(60, 31, 35, 48))) g.FillRectangle(b, T2X(hoverT), 0, 1, Height);
        double pos = GetPos != null ? GetPos() : -1;
        if (pos >= 0) using (var b = new SolidBrush(UI.Play)) g.FillRectangle(b, T2X(pos) - 1, 0, 2, Height);
        using (var pen = new Pen(UI.Line)) g.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
    }
    static string Fmt(double t, bool frac)
    {
        int m = (int)(t / 60); double s = t - m * 60;
        return m + ":" + s.ToString(frac ? "00.0" : "00", CultureInfo.InvariantCulture);
    }
}

// ======================================================================
// 主視窗
// ======================================================================
class Sel
{
    public string Type, Track, Key; public int Set, Idx;
    public override bool Equals(object o)
    {
        var s = o as Sel;
        return s != null && s.Type == Type && s.Set == Set && s.Track == Track && s.Idx == Idx && s.Key == Key;
    }
    public override int GetHashCode() { return (Type + Set + Track + Idx + Key).GetHashCode(); }
}

class MainForm : Form
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    static readonly string[][] Levels = { new[] { "track1", "等級 0" }, new[] { "track2", "等級 1–2" }, new[] { "track3", "等級 3 以上" } };
    static readonly Dictionary<string, string> SfxNames = new Dictionary<string, string> {
        {"DrawCard","抽牌"},{"DamageTick","受到傷害（每點）"},{"DamageCancel","傷害取消"},{"DamageResolved","傷害結算"},
        {"EffectActivate","效果發動"},{"DeckShuffle","洗牌"},{"AutoEffectTrigger","自動效果觸發"},{"EffectOrderPrompt","選擇效果順序"},
        {"CardPlayedToBoard","角色登場"},{"DirectAttack","直接攻擊"},{"SideAttack","側面攻擊"},{"FrontalAttack","正面攻擊"},
        {"LevelUp","升級"},{"TurnChange","回合交替"},{"DeckTopFlip","翻開牌庫頂"},{"ClimaxPlayed","打出名場面"},
        {"TriggerClimax","觸發名場面"},{"YouWin","獲勝"},{"YouLose","落敗"},{"OpponentChatMessage","對手訊息"} };

    readonly string root, audioRoot, builtInRoot, catalogPath;
    Dictionary<string, object> cat;
    bool dirty, backedUp;
    string tab = "battle";
    Sel sel;

    readonly Player player = new Player();
    readonly Dictionary<string, AudioData> audioCache = new Dictionary<string, AudioData>();
    // 正在讀取的檔案 → 等著結果的動作（同一個檔案正在讀時，後來的請求排隊，不會被丟掉）
    readonly Dictionary<string, List<Action<AudioData>>> loading = new Dictionary<string, List<Action<AudioData>>>();
    readonly List<string> cacheOrder = new List<string>();   // 只保留最近 4 首解碼結果（長歌一首約 80 MB）
    double pausedPos;
    string pausedKey;

    // 介面元件
    FlatButton btnSave, btnFolder;
    readonly List<FlatButton> tabButtons = new List<FlatButton>();
    FlowLayoutPanel content;
    Panel editor, editorEmpty, detail, loopRow;
    Label lblWhat, lblFile, lblTime, lblVol, lblTip, lblLoopState, toast;
    FlatButton btnPlay, btnSeam, btnCut, btnStop, btnLsNow, btnLeNow, btnClear, btnLsM, btnLsP, btnLeM, btnLeP;
    TextBox txtLs, txtLe;
    CheckBox chkCf; NumericUpDown numCf;
    Slider volSlider;
    WaveView wave;
    System.Windows.Forms.Timer tick, toastTimer;
    bool syncing;

    public MainForm(string root)
    {
        this.root = root;
        audioRoot = Path.Combine(root, "Audio");
        builtInRoot = Path.Combine(root, @"Weiss Schwarz_Data\StreamingAssets\Audio");
        catalogPath = Path.Combine(audioRoot, "audio_catalog.json");

        Text = "WS 模擬器 音樂設定工具";
        Font = UI.F(10f);
        BackColor = UI.Bg;
        ClientSize = new Size(1280, 860);
        MinimumSize = new Size(1000, 680);
        StartPosition = FormStartPosition.CenterScreen;
        KeyPreview = true;
        DoubleBuffered = true;
        try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

        BuildHeader();
        BuildEditor();
        content = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(20, 14, 8, 20), BackColor = UI.Bg, WrapContents = true };
        content.Resize += delegate { RenderList(); };
        Controls.Add(content);
        content.BringToFront();

        toast = new Label { AutoSize = false, Height = 38, BackColor = Color.FromArgb(34, 38, 52), ForeColor = Color.White, TextAlign = ContentAlignment.MiddleCenter, Visible = false, Font = UI.F(10f) };
        Controls.Add(toast); toast.BringToFront();
        toastTimer = new System.Windows.Forms.Timer { Interval = 3000 };
        toastTimer.Tick += delegate { toast.Visible = false; toastTimer.Stop(); };

        tick = new System.Windows.Forms.Timer { Interval = 33 };
        tick.Tick += delegate { OnTick(); };
        tick.Start();
        player.Ended += delegate { BeginInvoke(new Action(delegate { pausedPos = 0; SyncPlayBtn(); })); };

        LoadCatalog();
        RenderAll();
        SetTitle();
    }

    // ---------------- 資料 ----------------
    void LoadCatalog()
    {
        string json = File.ReadAllText(catalogPath, Encoding.UTF8).TrimStart('﻿');
        var ser = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
        cat = (Dictionary<string, object>)Norm(ser.DeserializeObject(json));
        foreach (var o in Concat(L("events"), L("bgm")))
        {
            var d = (Dictionary<string, object>)o;
            if (!d.ContainsKey("files") || !(d["files"] is List<object>)) d["files"] = new List<object>();
            if (!d.ContainsKey("volume")) d["volume"] = 1.0;
        }
        L("loopPoints");
        var duel = Ev("duel_stepped");
        if (duel != null && !(duel.ContainsKey("fileSets") && duel["fileSets"] is List<object>)) duel["fileSets"] = new List<object>();
    }
    static object Norm(object o)
    {
        var d = o as Dictionary<string, object>;
        if (d != null) { var n = new Dictionary<string, object>(); foreach (var kv in d) n[kv.Key] = Norm(kv.Value); return n; }
        var arr = o as object[];
        if (arr != null) { var l = new List<object>(); foreach (var x in arr) l.Add(Norm(x)); return l; }
        var al = o as System.Collections.ArrayList;
        if (al != null) { var l = new List<object>(); foreach (var x in al) l.Add(Norm(x)); return l; }
        return o;
    }
    static IEnumerable<object> Concat(List<object> a, List<object> b) { foreach (var x in a) yield return x; foreach (var x in b) yield return x; }
    List<object> L(string k)
    {
        object v;
        if (!cat.TryGetValue(k, out v) || !(v is List<object>)) { v = new List<object>(); cat[k] = v; }
        return (List<object>)v;
    }
    Dictionary<string, object> Ev(string key)
    {
        foreach (var o in Concat(L("bgm"), L("events"))) { var d = (Dictionary<string, object>)o; if ((d["key"] as string) == key) return d; }
        return null;
    }
    static List<object> Files(Dictionary<string, object> ev) { return (List<object>)ev["files"]; }
    List<object> Sets() { return (List<object>)Ev("duel_stepped")["fileSets"]; }
    string EvKey(Sel s) { return s.Type == "set" ? "duel_stepped" : s.Type == "deck" ? (s.Key ?? "deck_editor") : s.Key; }
    // 「牌組編輯器音樂」與「主畫面音樂」兩個分頁共用同一套畫面，用這個決定目前是哪一個
    string ListKey { get { return tab == "menu" ? "main_menu" : "deck_editor"; } }
    // 主畫面音樂是中文化套件新增的項目（遊戲原本沒有）；還沒有的話先用牌組編輯器的曲目當預設
    Dictionary<string, object> EnsureBgm(string key)
    {
        var ev = Ev(key);
        if (ev != null) return ev;
        ev = new Dictionary<string, object>();
        ev["key"] = key; ev["file"] = "";
        var files = new List<object>();
        var deck = Ev("deck_editor");
        if (deck != null) files.AddRange(Files(deck));
        ev["files"] = files; ev["fileSets"] = new List<object>(); ev["volume"] = 1.0;
        L("bgm").Add(ev);
        return ev;
    }
    double Vol(Sel s) { try { return Convert.ToDouble(Ev(EvKey(s))["volume"], Inv); } catch { return 1; } }

    string PathOf(Sel s)
    {
        if (s == null) return null;
        try
        {
            if (s.Type == "set") { object v; ((Dictionary<string, object>)Sets()[s.Set]).TryGetValue(s.Track, out v); return v as string; }
            var f = Files(Ev(EvKey(s)));
            return s.Idx < f.Count ? f[s.Idx] as string : null;
        }
        catch { return null; }
    }
    void SetPath(Sel s, string rel)
    {
        if (s.Type == "set") ((Dictionary<string, object>)Sets()[s.Set])[s.Track] = rel;
        else Files(Ev(EvKey(s)))[s.Idx] = rel;
        MarkDirty();
    }
    Dictionary<string, object> FindLoop(string rel)
    {
        if (rel == null) return null;
        foreach (var o in L("loopPoints"))
        {
            var d = (Dictionary<string, object>)o;
            if (string.Equals(d.ContainsKey("track") ? d["track"] as string : null, rel, StringComparison.OrdinalIgnoreCase)) return d;
        }
        return null;
    }
    double[] LoopOf(string rel)
    {
        var lp = FindLoop(rel); if (lp == null) return null;
        double s = ParseTime(lp["loopStart"] as string), e = ParseTime(lp["loopEnd"] as string);
        return (s >= 0 && e > s) ? new[] { s, e } : null;
    }
    static double ParseTime(string t)
    {
        if (string.IsNullOrWhiteSpace(t)) return -1;
        double acc = 0;
        foreach (var part in t.Trim().Split(':'))
        {
            double v;
            if (!double.TryParse(part, NumberStyles.Float, Inv, out v)) return -1;
            acc = acc * 60 + v;
        }
        return acc;
    }
    static string FmtTime(double s)
    {
        if (s < 0) s = 0;
        int m = (int)(s / 60);
        return m + ":" + (s - m * 60).ToString("00.000", Inv);
    }
    void MarkDirty() { dirty = true; SetTitle(); btnSave.Text = "● 儲存"; }
    void SetTitle() { Text = "WS 模擬器 音樂設定工具" + (dirty ? "  （尚未儲存）" : ""); }

    void Save()
    {
        try
        {
            if (!backedUp) { File.Copy(catalogPath, catalogPath + ".bak", true); backedUp = true; }
            var sb = new StringBuilder();
            WriteJson(sb, cat, 0);
            File.WriteAllText(catalogPath, sb.ToString(), new UTF8Encoding(false));
            dirty = false; SetTitle(); btnSave.Text = "儲存";
            Toast("已儲存！重新進入遊戲的對戰／牌組編輯器／主畫面就會套用");
        }
        catch (Exception ex) { Toast("儲存失敗：" + ex.Message, true); }
    }
    static void WriteJson(StringBuilder sb, object v, int ind)
    {
        string pad = new string(' ', ind * 4), pad2 = new string(' ', (ind + 1) * 4);
        if (v == null) sb.Append("null");
        else if (v is string)
        {
            sb.Append('"');
            foreach (char c in (string)v)
            {
                if (c == '"') sb.Append("\\\""); else if (c == '\\') sb.Append("\\\\");
                else if (c == '\n') sb.Append("\\n"); else if (c == '\r') sb.Append("\\r"); else if (c == '\t') sb.Append("\\t");
                else if (c < 32) sb.Append("\\u" + ((int)c).ToString("x4")); else sb.Append(c);
            }
            sb.Append('"');
        }
        else if (v is bool) sb.Append((bool)v ? "true" : "false");
        else if (v is int || v is long) sb.Append(Convert.ToString(v, Inv));
        else if (v is decimal) sb.Append(((decimal)v).ToString(Inv));
        else if (v is double || v is float)
        {
            double d = Convert.ToDouble(v, Inv);
            sb.Append(d == Math.Floor(d) ? d.ToString("0.0", Inv) : d.ToString("R", Inv));
        }
        else if (v is Dictionary<string, object>)
        {
            var d = (Dictionary<string, object>)v;
            if (d.Count == 0) { sb.Append("{}"); return; }
            sb.Append("{\n"); int i = 0;
            foreach (var kv in d)
            {
                sb.Append(pad2); WriteJson(sb, kv.Key, 0); sb.Append(": "); WriteJson(sb, kv.Value, ind + 1);
                sb.Append(++i < d.Count ? ",\n" : "\n");
            }
            sb.Append(pad).Append('}');
        }
        else if (v is List<object>)
        {
            var l = (List<object>)v;
            if (l.Count == 0) { sb.Append("[]"); return; }
            sb.Append("[\n");
            for (int i = 0; i < l.Count; i++) { sb.Append(pad2); WriteJson(sb, l[i], ind + 1); sb.Append(i < l.Count - 1 ? ",\n" : "\n"); }
            sb.Append(pad).Append(']');
        }
        else sb.Append(Convert.ToString(v, Inv));
    }

    string ResolveAudio(string rel)
    {
        if (string.IsNullOrEmpty(rel)) return null;
        foreach (var b in new[] { audioRoot, builtInRoot })
        {
            string p = Path.Combine(b, rel.Replace('/', '\\'));
            if (File.Exists(p)) return p;
        }
        return null;
    }

    string PickAndImport(string sub)
    {
        using (var dlg = new OpenFileDialog())
        {
            dlg.Title = sub == "SFX" ? "選擇音效檔" : "選擇音樂檔";
            dlg.Filter = "音訊檔 (*.mp3;*.wav;*.ogg)|*.mp3;*.wav;*.ogg|所有檔案 (*.*)|*.*";
            if (dlg.ShowDialog(this) != DialogResult.OK) return null;
            try
            {
                string full = Path.GetFullPath(dlg.FileName);
                string ar = Path.GetFullPath(audioRoot).TrimEnd('\\') + "\\";
                if (full.StartsWith(ar, StringComparison.OrdinalIgnoreCase)) return full.Substring(ar.Length).Replace('\\', '/');
                string dir = Path.Combine(audioRoot, sub);
                Directory.CreateDirectory(dir);
                string name = Path.GetFileName(full), bas = Path.GetFileNameWithoutExtension(name), ext = Path.GetExtension(name);
                string dest = Path.Combine(dir, name); int n = 2;
                long len = new FileInfo(full).Length;
                while (File.Exists(dest) && new FileInfo(dest).Length != len) { dest = Path.Combine(dir, bas + "_" + n + ext); n++; }
                if (!File.Exists(dest)) File.Copy(full, dest);
                return sub + "/" + Path.GetFileName(dest);
            }
            catch (Exception ex) { Toast("複製檔案失敗：" + ex.Message, true); return null; }
        }
    }

    // ---------------- 音訊 ----------------
    void WithAudio(string rel, Action<AudioData> then)
    {
        string path = ResolveAudio(rel);
        if (path == null) { Toast("找不到檔案：" + rel, true); wave.Message = "找不到檔案"; wave.Invalidate(); return; }
        string key = path + "|" + File.GetLastWriteTimeUtc(path).Ticks;
        AudioData d;
        if (audioCache.TryGetValue(key, out d))
        {
            cacheOrder.Remove(key); cacheOrder.Add(key);
            then(d); return;
        }
        List<Action<AudioData>> waiting;
        if (loading.TryGetValue(key, out waiting)) { waiting.Add(then); return; }
        loading[key] = new List<Action<AudioData>> { then };
        ThreadPool.QueueUserWorkItem(delegate
        {
            AudioData r = null; string err = null;
            try { r = MF.Decode(path); } catch (Exception ex) { err = ex.Message; }
            BeginInvoke(new Action(delegate
            {
                var callbacks = loading[key];
                loading.Remove(key);
                if (r == null) { Toast("無法讀取音訊：" + err, true); if (PathOf(sel) == rel) { wave.Message = "無法讀取這個檔案（" + err + "）"; wave.Invalidate(); } return; }
                audioCache[key] = r; cacheOrder.Add(key);
                // 超過 4 首就清掉最舊、而且目前沒有在顯示或播放的
                string playingPath = player.Playing && player.Key != null ? ResolveAudio(player.Key) : null;
                for (int i = 0; i < cacheOrder.Count && cacheOrder.Count > 4; )
                {
                    string old = cacheOrder[i];
                    bool inUse = wave.Data == audioCache[old] || (playingPath != null && old.StartsWith(playingPath + "|"));
                    if (inUse) { i++; continue; }
                    audioCache.Remove(old); cacheOrder.RemoveAt(i);
                }
                foreach (var cb in callbacks) cb(r);
            }));
        });
    }

    void PlaySel(double from)
    {
        string rel = PathOf(sel); if (rel == null) return;
        var s = sel;
        WithAudio(rel, d =>
        {
            if (!Equals(sel, s)) return;
            var L2 = sel.Type != "sfx" ? LoopOf(rel) : null;
            player.Gain = (float)Vol(sel);
            try { player.Play(d, rel, from, L2 != null ? L2[0] : -1, L2 != null ? L2[1] : -1); }
            catch (Exception ex) { Toast(ex.Message, true); }
            SyncPlayBtn();
        });
    }
    void StopPlayback(bool rewind)
    {
        if (player.Playing) { pausedPos = rewind ? 0 : player.Position; pausedKey = player.Key; }
        else if (rewind) pausedPos = 0;
        player.Stop();
        SyncPlayBtn();
    }
    void TogglePlay()
    {
        string rel = PathOf(sel); if (rel == null) return;
        if (player.Playing && player.Key == rel) { StopPlayback(false); return; }
        PlaySel(pausedKey == rel ? pausedPos : 0);
    }
    void SyncPlayBtn()
    {
        if (btnPlay == null) return;
        btnPlay.Text = player.Playing && player.Key == PathOf(sel) ? "❚❚" : "▶";
    }
    double CurPos()
    {
        string rel = PathOf(sel);
        if (player.Playing && player.Key == rel) return player.Position;
        return pausedKey == rel ? pausedPos : 0;
    }
    void OnTick()
    {
        if (editor.Visible && wave.Data != null)
        {
            lblTime.Text = FmtTime(CurPos()) + "  /  " + FmtTime(wave.Data.Duration);
            if (player.Playing || wave.Focused || wave.ClientRectangle.Contains(wave.PointToClient(Cursor.Position))) wave.Invalidate();
        }
    }
    void LiveLoop()
    {
        string rel = PathOf(sel);
        if (player.Key != rel) return;
        var L2 = LoopOf(rel);
        if (L2 != null) player.SetLoop(L2[0], L2[1]); else player.SetLoop(-1, -1);
    }

    // ---------------- 介面：頂部 ----------------
    void BuildHeader()
    {
        var head = new Panel { Dock = DockStyle.Top, Height = 74, BackColor = UI.Panel };
        head.Paint += (s, e) => { using (var p = new Pen(UI.Line)) e.Graphics.DrawLine(p, 0, head.Height - 1, head.Width, head.Height - 1); };
        var title = new Label { Text = "♪ 音樂設定工具", Font = UI.F(15f, FontStyle.Bold), ForeColor = UI.Text, AutoSize = true, Location = new Point(22, 12) };
        var sub = new Label { Text = "替換遊戲的背景音樂與音效、試聽、設定循環點", Font = UI.F(9.5f), ForeColor = UI.Muted, AutoSize = true, Location = new Point(25, 44) };
        btnSave = new FlatButton("儲存", 110) { Primary = true, Font = UI.F(10.5f, FontStyle.Bold) };
        btnSave.Height = 38;
        btnFolder = new FlatButton("開啟 Audio 資料夾") { Ghost = true };
        head.Controls.AddRange(new Control[] { title, sub, btnSave, btnFolder });
        head.Resize += delegate { btnSave.Location = new Point(head.Width - btnSave.Width - 22, 18); btnFolder.Location = new Point(btnSave.Left - btnFolder.Width - 8, 20); };
        btnSave.Click += delegate { Save(); };
        btnFolder.Click += delegate { Process.Start("explorer.exe", "\"" + audioRoot + "\""); };

        var tabs = new Panel { Dock = DockStyle.Top, Height = 48, BackColor = UI.Panel };
        tabs.Paint += (s, e) =>
        {
            var g = e.Graphics;
            using (var p = new Pen(UI.Line)) g.DrawLine(p, 0, tabs.Height - 1, tabs.Width, tabs.Height - 1);
            foreach (var b in tabButtons)
                if ((string)b.Tag == tab) using (var br = new SolidBrush(UI.Accent)) g.FillRectangle(br, b.Left + 6, tabs.Height - 4, b.Width - 12, 3);
        };
        int x = 18;
        foreach (var t in new[] { new[] { "battle", "對戰音樂" }, new[] { "deck", "牌組編輯器音樂" }, new[] { "menu", "主畫面音樂" }, new[] { "sfx", "音效" } })
        {
            var b = new FlatButton(t[1]) { Ghost = true, Tag = t[0], Font = UI.F(10.5f, FontStyle.Bold), Location = new Point(x, 7) };
            b.Height = 36;
            b.Click += delegate { tab = (string)b.Tag; RenderAll(); };
            tabButtons.Add(b); tabs.Controls.Add(b);
            x += b.Width + 4;
        }
        Controls.Add(tabs);
        Controls.Add(head);
    }

    // ---------------- 介面：清單 ----------------
    void RenderAll()
    {
        foreach (var b in tabButtons) b.ForeColor = (string)b.Tag == tab ? UI.Accent : UI.Muted;
        if (tabButtons.Count > 0) tabButtons[0].Parent.Invalidate();
        RenderList();
    }
    void RenderList()
    {
        if (cat == null || content == null) return;
        var scroll = content.VerticalScroll.Value;
        content.SuspendLayout();
        foreach (Control c in content.Controls) c.Dispose();
        content.Controls.Clear();
        int avail = content.ClientSize.Width - content.Padding.Horizontal - SystemInformation.VerticalScrollBarWidth - 2;
        string hint = tab == "battle" ? "每場對戰開始時，遊戲會從下面「隨機選一組」。等級 0 播第 1 首，等級 1–2 播第 2 首，等級 3 以上播第 3 首。點一首歌就能在下方試聽與設定循環點；雙擊可以直接換歌。"
            : tab == "deck" ? "牌組編輯器畫面的背景音樂。放多首的話，每次進入會「隨機播放其中一首」。"
            : tab == "menu" ? "主畫面（主選單）的背景音樂。放多首的話，每次回到主畫面會「隨機播放其中一首」。需要用「Weiss Schwarz 中文版」捷徑開遊戲才會播放。"
            : "遊戲中的各種音效。一個項目放多個檔案時，遊戲會「隨機挑一個」播放。點檔名可以試聽並調整音量。";
        var lbl = new Label { Text = hint, ForeColor = UI.Muted, AutoSize = false, Width = avail - 14, Height = 30, Margin = new Padding(0, 0, 0, 6), AutoEllipsis = true };
        content.Controls.Add(lbl);
        content.SetFlowBreak(lbl, true);

        if (tab == "sfx")
        {
            var t = new SfxTable();
            foreach (var o in L("events"))
            {
                var d = (Dictionary<string, object>)o;
                if (d.ContainsKey("fileSets")) continue;
                string k = d["key"] as string;
                var row = new SfxRow { Key = k, Name = SfxNames.ContainsKey(k) ? SfxNames[k] : k, Files = new List<string>() };
                foreach (var f in Files(d)) row.Files.Add(f as string);
                if (sel != null && sel.Type == "sfx" && sel.Key == k) row.SelIdx = sel.Idx;
                t.Rows.Add(row);
            }
            t.Fit(avail - 14);
            t.Act += OnAct; t.DoubleAct += (a, arg) => { if (a == "chip") OnAct("replchip", arg); };
            content.Controls.Add(t);
        }
        else
        {
            int cols = Math.Max(1, (avail + 14) / (340 + 14));
            int cw = (avail - cols * 14) / cols;   // 每張卡右邊各留 14 的間距
            if (tab == "battle")
            {
                var sets = Sets();
                for (int i = 0; i < sets.Count; i++)
                {
                    var c = new CardView { Title = "第 " + (i + 1) + " 組", Tag2 = i, Margin = new Padding(0, 0, 14, 14) };
                    c.HeadActs.Add(new[] { "全換成同一首", "same" });
                    c.HeadActs.Add(new[] { "刪除", "delset" });
                    foreach (var lv in Levels) c.Slots.Add(MakeSlot(new Sel { Type = "set", Set = i, Track = lv[0] }, lv[1], false));
                    c.Fit(cw); c.Act += OnAct; c.DoubleAct += OnDouble;
                    content.Controls.Add(c);
                }
                var add = new CardView { IsAdd = true, Title = "＋ 新增一組", AddSub = "先選一首歌，三個等級都會先用它", Margin = new Padding(0, 0, 14, 14) };
                add.Fit(cw); add.Act += OnAct;
                content.Controls.Add(add);
            }
            else
            {
                string key = ListKey;
                var files = Files(EnsureBgm(key));
                var c = new CardView { Title = key == "main_menu" ? "主畫面" : "牌組編輯器", FooterText = "＋ 加入一首", FooterAct = "adddeck", Margin = new Padding(0, 0, 14, 14) };
                for (int i = 0; i < files.Count; i++) c.Slots.Add(MakeSlot(new Sel { Type = "deck", Key = key, Idx = i }, "第 " + (i + 1) + " 首", files.Count > 1));
                c.Fit(Math.Min(avail - 14, 640)); c.Act += OnAct; c.DoubleAct += OnDouble;
                content.Controls.Add(c);
            }
        }
        content.ResumeLayout();
        if (scroll > 0) { try { content.VerticalScroll.Value = Math.Min(scroll, content.VerticalScroll.Maximum); content.PerformLayout(); } catch { } }
    }
    SlotItem MakeSlot(Sel s, string badge, bool canDelete)
    {
        string p = PathOf(s);
        return new SlotItem { Badge = badge, Text = p != null ? Path.GetFileName(p) : "（未設定）", Tip = p, Loop = LoopOf(p) != null, Selected = Equals(s, sel), Sel = s, CanDelete = canDelete };
    }

    void SelectItem(Sel s, bool autoplay)
    {
        bool changed = !Equals(s, sel);
        sel = s;
        RenderList();
        if (changed) { player.Stop(); pausedPos = 0; pausedKey = null; RenderEditor(); }
        if (autoplay) PlaySel(0);
    }

    void OnDouble(string act, object arg)
    {
        if (act == "slot" || act == "play") OnAct("replace", arg);
    }

    void OnAct(string act, object arg)
    {
        var s = arg as Sel;
        switch (act)
        {
            case "slot": SelectItem(s, false); break;
            case "play":
                if (!Equals(s, sel)) SelectItem(s, false);
                if (player.Playing && player.Key == PathOf(s)) StopPlayback(true); else PlaySel(0);
                break;
            case "replace":
            {
                string rel = PickAndImport("BGM"); if (rel == null) return;
                SetPath(s, rel); sel = null; SelectItem(s, false); Toast("已替換成「" + Path.GetFileName(rel) + "」，記得按儲存");
                break;
            }
            case "delete":
            {
                var files = Files(Ev(EvKey(s)));
                if (files.Count <= 1) { Toast("至少要保留一首", true); return; }
                files.RemoveAt(s.Idx); MarkDirty(); player.Stop(); sel = null; RenderList(); RenderEditor();
                break;
            }
            case "same":
            {
                int i = (int)arg;
                string rel = PickAndImport("BGM"); if (rel == null) return;
                var set = (Dictionary<string, object>)Sets()[i];
                foreach (var lv in Levels) set[lv[0]] = rel;
                MarkDirty(); sel = null; SelectItem(new Sel { Type = "set", Set = i, Track = "track1" }, false);
                Toast("第 " + (i + 1) + " 組三個等級都改成「" + Path.GetFileName(rel) + "」");
                break;
            }
            case "delset":
            {
                int i = (int)arg; var sets = Sets();
                if (sets.Count <= 1) { Toast("至少要保留一組", true); return; }
                if (MessageBox.Show(this, "確定刪除第 " + (i + 1) + " 組？（音樂檔本身不會被刪除）", "刪除", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                sets.RemoveAt(i); MarkDirty(); player.Stop(); sel = null; RenderList(); RenderEditor();
                break;
            }
            case "add":
            {
                string rel = PickAndImport("BGM"); if (rel == null) return;
                var d = new Dictionary<string, object>(); d["track1"] = rel; d["track2"] = rel; d["track3"] = rel;
                Sets().Add(d); MarkDirty();
                SelectItem(new Sel { Type = "set", Set = Sets().Count - 1, Track = "track1" }, false);
                Toast("已新增一組，三個等級都先用「" + Path.GetFileName(rel) + "」，可以再個別換歌");
                break;
            }
            case "adddeck":
            {
                string rel = PickAndImport("BGM"); if (rel == null) return;
                var files = Files(EnsureBgm(ListKey)); files.Add(rel); MarkDirty();
                SelectItem(new Sel { Type = "deck", Key = ListKey, Idx = files.Count - 1 }, false);
                break;
            }
            case "chip":
            {
                var a = (object[])arg;
                var ns = new Sel { Type = "sfx", Key = (string)a[0], Idx = (int)a[1] };
                if (Equals(ns, sel) && player.Playing) { StopPlayback(true); return; }
                SelectItem(ns, true);
                break;
            }
            case "replchip":
            {
                var a = (object[])arg;
                string rel = PickAndImport("SFX"); if (rel == null) return;
                Files(Ev((string)a[0]))[(int)a[1]] = rel; MarkDirty(); sel = null;
                SelectItem(new Sel { Type = "sfx", Key = (string)a[0], Idx = (int)a[1] }, true);
                break;
            }
            case "delchip":
            {
                var a = (object[])arg;
                var files = Files(Ev((string)a[0])); files.RemoveAt((int)a[1]); MarkDirty(); player.Stop(); sel = null; RenderList(); RenderEditor();
                break;
            }
            case "replsfx":
            case "addsfx":
            {
                string key = (string)arg, rel = PickAndImport("SFX"); if (rel == null) return;
                var files = Files(Ev(key));
                if (act == "replsfx") { files.Clear(); }
                files.Add(rel); MarkDirty(); sel = null;
                SelectItem(new Sel { Type = "sfx", Key = key, Idx = files.Count - 1 }, true);
                break;
            }
        }
    }

    // ---------------- 介面：編輯面板 ----------------
    void BuildEditor()
    {
        editor = new Panel { Dock = DockStyle.Bottom, Height = 290, BackColor = UI.Panel };
        detail = new Panel { Dock = DockStyle.Fill, BackColor = UI.Panel };
        editor.Paint += (s, e) => { using (var p = new Pen(UI.Line)) e.Graphics.DrawLine(p, 0, 0, editor.Width, 0); };
        editorEmpty = new Panel { Dock = DockStyle.Fill, BackColor = UI.Panel };
        editorEmpty.Controls.Add(new Label { Text = "點選上面任一首歌或音效，就能在這裡試聽和設定循環點", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, ForeColor = UI.Muted });

        lblWhat = new Label { AutoSize = true, ForeColor = UI.Muted, Font = UI.F(9f), Location = new Point(22, 14) };
        lblFile = new Label { AutoSize = false, AutoEllipsis = true, Font = UI.F(12f, FontStyle.Bold), ForeColor = UI.Text, Location = new Point(20, 32), Size = new Size(420, 28) };
        btnPlay = new FlatButton("▶") { Primary = true, Round = true, Font = UI.F(13f), Size = new Size(46, 46) };
        btnSeam = new FlatButton("↻ 試聽接縫");
        btnCut = new FlatButton("✂ 只用這一段");
        btnStop = new FlatButton("■") { Ghost = true, Size = new Size(40, 34) };
        lblTime = new Label { AutoSize = false, Size = new Size(210, 24), Font = new Font("Consolas", 11f), ForeColor = UI.Text, TextAlign = ContentAlignment.MiddleCenter };
        lblVol = new Label { AutoSize = false, Size = new Size(110, 24), ForeColor = UI.Muted, TextAlign = ContentAlignment.MiddleLeft };
        volSlider = new Slider();
        wave = new WaveView { Height = 120 };
        lblTip = new Label { AutoSize = false, Height = 20, ForeColor = UI.Muted, Font = UI.F(9f) };

        loopRow = new Panel { Height = 44, BackColor = UI.Panel };
        var lblLoop = new Label { Text = "循環點", Font = UI.F(10f, FontStyle.Bold), AutoSize = true, Location = new Point(0, 12) };
        lblLoopState = new Label { AutoSize = true, Font = UI.F(9f), Location = new Point(58, 13), Padding = new Padding(6, 1, 6, 1) };
        txtLs = new TextBox { Width = 92, Font = new Font("Consolas", 10.5f), TextAlign = HorizontalAlignment.Center };
        txtLe = new TextBox { Width = 92, Font = new Font("Consolas", 10.5f), TextAlign = HorizontalAlignment.Center };
        btnLsM = new FlatButton("−", 30); btnLsP = new FlatButton("＋", 30); btnLeM = new FlatButton("−", 30); btnLeP = new FlatButton("＋", 30);
        btnLsNow = new FlatButton("⇤ 目前位置"); btnLeNow = new FlatButton("目前位置 ⇥");
        chkCf = new CheckBox { Text = "淡入淡出", AutoSize = true, ForeColor = UI.Muted };
        numCf = new NumericUpDown { Width = 56, DecimalPlaces = 1, Increment = 0.5m, Maximum = 10 };
        var lblSec = new Label { Text = "秒", AutoSize = true, ForeColor = UI.Muted };
        btnClear = new FlatButton("清除循環點") { Ghost = true };
        var lblS = new Label { Text = "開始", AutoSize = true, ForeColor = UI.Muted };
        var lblE = new Label { Text = "結束", AutoSize = true, ForeColor = UI.Muted };
        loopRow.Controls.AddRange(new Control[] { lblLoop, lblLoopState, lblS, btnLsM, txtLs, btnLsP, btnLsNow, lblE, btnLeM, txtLe, btnLeP, btnLeNow, chkCf, numCf, lblSec, btnClear });
        loopRow.Resize += delegate
        {
            int x = lblLoopState.Right + 16, y = 5;
            foreach (var c in new Control[] { lblS, btnLsM, txtLs, btnLsP, btnLsNow, null, lblE, btnLeM, txtLe, btnLeP, btnLeNow, null, chkCf, numCf, lblSec, null, btnClear })
            {
                if (c == null) { x += 10; continue; }
                c.Location = new Point(x, c is Label || c is CheckBox ? 12 : (c is TextBox ? y + 5 : (c is NumericUpDown ? y + 5 : y)));
                x += c.Width + 4;
            }
        };

        detail.Controls.AddRange(new Control[] { lblWhat, lblFile, btnPlay, btnSeam, btnCut, btnStop, lblTime, lblVol, volSlider, wave, lblTip, loopRow });
        editor.Controls.Add(detail); editor.Controls.Add(editorEmpty);
        detail.Resize += delegate { LayoutEditor(); };
        Controls.Add(editor);

        btnPlay.Click += delegate { TogglePlay(); };
        btnStop.Click += delegate { StopPlayback(true); };
        btnCut.Click += delegate { CutSegment(); };
        btnSeam.Click += delegate
        {
            var L2 = LoopOf(PathOf(sel));
            if (L2 == null) { Toast("請先在波形上拖曳選出循環範圍", true); return; }
            PlaySel(Math.Max(0, L2[1] - 4));
        };
        volSlider.ValueChanged += delegate
        {
            if (syncing || sel == null) return;
            double v = Math.Round(volSlider.Value, 2);
            Ev(EvKey(sel))["volume"] = v; lblVol.Text = "音量 " + (int)Math.Round(v * 100) + "%";
            if (player.Key == PathOf(sel)) player.Gain = (float)v;
            MarkDirty();
        };
        wave.GetPos = () => { string rel = PathOf(sel); return player.Playing && player.Key == rel ? player.Position : (pausedKey == rel ? pausedPos : -1); };
        wave.GetLoop = () => LoopOf(PathOf(sel));
        wave.SetLoop = (s, e, done) => { EnsureLoop(PathOf(sel), s, e); SyncLoopUi(); LiveLoop(); if (done) RenderList(); };
        wave.Seek = t =>
        {
            string rel = PathOf(sel);
            if (player.Playing && player.Key == rel) PlaySel(t); else { pausedKey = rel; pausedPos = t; }
        };
        txtLs.Leave += delegate { LoopFromInputs(); };
        txtLe.Leave += delegate { LoopFromInputs(); };
        txtLs.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { LoopFromInputs(); e.SuppressKeyPress = true; } };
        txtLe.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { LoopFromInputs(); e.SuppressKeyPress = true; } };
        btnLsNow.Click += delegate { SetEdge('s', CurPos()); };
        btnLeNow.Click += delegate { SetEdge('e', CurPos()); };
        btnLsM.Click += delegate { Nudge('s', -1); }; btnLsP.Click += delegate { Nudge('s', 1); };
        btnLeM.Click += delegate { Nudge('e', -1); }; btnLeP.Click += delegate { Nudge('e', 1); };
        chkCf.CheckedChanged += delegate { if (syncing) return; var lp = FindLoop(PathOf(sel)); if (lp != null) { lp["crossfade"] = chkCf.Checked; MarkDirty(); } };
        numCf.ValueChanged += delegate { if (syncing) return; var lp = FindLoop(PathOf(sel)); if (lp != null) { lp["crossfadeSeconds"] = (double)numCf.Value; MarkDirty(); } };
        btnClear.Click += delegate
        {
            var lp = FindLoop(PathOf(sel)); if (lp == null) return;
            L("loopPoints").Remove(lp); MarkDirty(); SyncLoopUi(); LiveLoop(); RenderList();
            Toast("已清除循環點，這首會整首播完再重播");
        };
        RenderEditor();
    }

    // 把目前選取的範圍（循環點）另存成 wav，套用到這個項目。
    // 用途：同一首歌在不同等級播放不同段落——遊戲的循環點是跟著檔案走的，
    // 而且同一個檔案在升級時不會切換，所以每個段落要是獨立的檔案。
    void CutSegment() { CutSegment(true); }
    void CutSegment(bool confirm)
    {
        string rel = PathOf(sel);
        var L2 = LoopOf(rel);
        if (rel == null || wave.Data == null) return;
        if (L2 == null) { Toast("請先在波形上拖曳，選出要使用的段落", true); return; }
        var d = wave.Data;
        long f0 = (long)(L2[0] * d.Rate), f1 = Math.Min(d.Frames, (long)(L2[1] * d.Rate));
        if (f1 - f0 < d.Rate / 2) { Toast("選取的段落太短（至少要 0.5 秒）", true); return; }
        string suffix = sel.Type == "set"
            ? "_第" + (sel.Set + 1) + "組_" + Array.Find(Levels, l => l[0] == sel.Track)[1].Replace(" ", "").Replace("–", "-")
            : (sel.Key == "main_menu" ? "_主畫面" : "_牌組編輯器");
        string baseName = Path.GetFileNameWithoutExtension(rel) + suffix;
        foreach (char c in Path.GetInvalidFileNameChars()) baseName = baseName.Replace(c, '_');
        string dir = Path.Combine(audioRoot, "BGM");
        Directory.CreateDirectory(dir);
        string dest = Path.Combine(dir, baseName + ".mp3"); int n = 2;
        while (File.Exists(dest) || File.Exists(Path.ChangeExtension(dest, ".wav"))) dest = Path.Combine(dir, baseName + "_" + n++ + ".mp3");
        double secs = (f1 - f0) / (double)d.Rate;
        string msg = "要把 " + FmtTime(L2[0]) + " ～ " + FmtTime(L2[1]) + "（" + secs.ToString("0.0", Inv) + " 秒）另存成\n「" + Path.GetFileName(dest) + "」\n並套用到這個項目嗎？\n\n套用後會在這一段裡循環播放；原本的音樂檔不會被修改。";
        if (confirm && MessageBox.Show(this, msg, "只用這一段", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        try
        {
            var s = sel;
            double loopA = -1, loopB = -1;
            try
            {
                // mp3：前後各多留 0.2 秒，存好後找出段落的實際位置，循環點就落在真正的音樂上（避開 mp3 前後的靜音）
                long pad = d.Rate / 5;
                long s0 = Math.Max(0, f0 - pad), s1 = Math.Min(d.Frames, f1 + pad);
                MF.EncodeMp3(dest, d.Pcm, d.Channels, d.Rate, s0, s1, 320);
                var enc = MF.Decode(dest);
                long expect = f0 - s0;
                long at = MF.FindOffset(d, f0, enc, expect, expect + 6000);
                loopA = (double)at / enc.Rate;
                loopB = loopA + (double)(f1 - f0) / d.Rate;
                if (loopB > enc.Duration) loopB = enc.Duration;
            }
            catch (Exception mp3Error)
            {
                // 沒有 mp3 編碼器之類的狀況：改存 wav（檔案較大，但不需要額外處理）
                try { if (File.Exists(dest)) File.Delete(dest); } catch { }
                dest = Path.ChangeExtension(dest, ".wav");
                WriteWav(dest, d.Pcm, d.Channels, d.Rate, f0, f1);
                Toast("無法存成 mp3（" + mp3Error.Message + "），已改存成 wav", true);
            }
            string newRel = "BGM/" + Path.GetFileName(dest);
            SetPath(s, newRel);
            if (loopA >= 0) EnsureLoop(newRel, loopA, loopB);
            sel = null; SelectItem(s, false);
            Toast("已另存成「" + Path.GetFileName(dest) + "」（" + (new FileInfo(dest).Length / 1048576.0).ToString("0.0", Inv) + " MB）並套用，記得按儲存");
        }
        catch (Exception ex) { Toast("另存失敗：" + ex.Message, true); }
    }

    static void WriteWav(string path, short[] pcm, int ch, int rate, long f0, long f1)
    {
        long frames = f1 - f0;
        int bytes = checked((int)(frames * ch * 2));
        using (var fs = new FileStream(path, FileMode.CreateNew))
        using (var w = new BinaryWriter(fs))
        {
            w.Write(Encoding.ASCII.GetBytes("RIFF")); w.Write(36 + bytes); w.Write(Encoding.ASCII.GetBytes("WAVE"));
            w.Write(Encoding.ASCII.GetBytes("fmt ")); w.Write(16); w.Write((short)1); w.Write((short)ch);
            w.Write(rate); w.Write(rate * ch * 2); w.Write((short)(ch * 2)); w.Write((short)16);
            w.Write(Encoding.ASCII.GetBytes("data")); w.Write(bytes);
            var buf = new byte[bytes];
            Buffer.BlockCopy(pcm, checked((int)(f0 * ch * 2)), buf, 0, bytes);
            w.Write(buf);
        }
    }

    void LayoutEditor()
    {
        int W = detail.ClientSize.Width, pad = 22;
        int right = W - pad;
        volSlider.Location = new Point(right - volSlider.Width, 26);
        lblVol.Location = new Point(volSlider.Left - lblVol.Width - 2, 26);
        lblTime.Location = new Point(lblVol.Left - lblTime.Width - 10, 26);
        btnStop.Location = new Point(lblTime.Left - btnStop.Width - 8, 24);
        btnSeam.Location = new Point(btnStop.Left - btnSeam.Width - 6, 24);
        btnCut.Location = new Point(btnSeam.Left - btnCut.Width - 6, 24);
        btnPlay.Location = new Point((btnCut.Visible ? btnCut.Left : btnSeam.Left) - btnPlay.Width - 10, 18);
        lblFile.Width = Math.Max(120, btnPlay.Left - 40);
        wave.Location = new Point(pad, 74); wave.Width = W - pad * 2;
        lblTip.Location = new Point(pad, wave.Bottom + 2); lblTip.Width = W - pad * 2;
        loopRow.Location = new Point(pad, lblTip.Bottom + 2); loopRow.Width = W - pad * 2;
        if (toast != null) toast.Location = new Point(ClientSize.Width - toast.Width - 24, ClientSize.Height - editor.Height - toast.Height - 16);
    }

    void RenderEditor()
    {
        string rel = PathOf(sel);
        bool has = sel != null && rel != null;
        editorEmpty.Visible = !has; detail.Visible = has;
        if (!has) { editor.Height = 90; player.Stop(); wave.SetData(null); return; }
        bool music = sel.Type != "sfx";
        editor.Height = music ? 290 : 240;
        lblWhat.Text = sel.Type == "set" ? "對戰音樂・第 " + (sel.Set + 1) + " 組・" + Array.Find(Levels, l => l[0] == sel.Track)[1]
            : sel.Type == "deck" ? (sel.Key == "main_menu" ? "主畫面音樂・第 " : "牌組編輯器音樂・第 ") + (sel.Idx + 1) + " 首" : "音效・" + (SfxNames.ContainsKey(sel.Key) ? SfxNames[sel.Key] : sel.Key);
        lblFile.Text = Path.GetFileName(rel);
        btnSeam.Visible = music; btnCut.Visible = music; loopRow.Visible = music;
        wave.LoopEditable = music;
        lblTip.Text = music ? "在波形上「拖曳」選取範圍（循環點）；拖動藍色邊線可微調；按「✂ 只用這一段」可把選取範圍另存成這個項目專用的段落。滾輪縮放，雙擊顯示全部。" : "點一下跳到該位置。滾輪縮放，雙擊顯示全部。";
        syncing = true;
        double v = Vol(sel); volSlider.Value = v; lblVol.Text = "音量 " + (int)Math.Round(v * 100) + "%";
        syncing = false;
        SyncLoopUi();
        SyncPlayBtn();
        wave.Message = "載入中…"; wave.SetData(null);
        var s = sel;
        WithAudio(rel, d => { if (Equals(sel, s)) wave.SetData(d); });
        LayoutEditor();
    }

    void SyncLoopUi()
    {
        var lp = FindLoop(PathOf(sel));
        syncing = true;
        lblLoopState.Text = lp != null ? "已設定：播到結束會跳回開始" : "未設定：整首播完後從頭重播";
        lblLoopState.ForeColor = lp != null ? UI.Ok : UI.Muted;
        lblLoopState.BackColor = lp != null ? Color.FromArgb(230, 246, 236) : Color.FromArgb(238, 240, 245);
        if (!txtLs.Focused) txtLs.Text = lp != null ? lp["loopStart"] as string : "";
        if (!txtLe.Focused) txtLe.Text = lp != null ? lp["loopEnd"] as string : "";
        chkCf.Checked = lp != null && lp.ContainsKey("crossfade") && lp["crossfade"] is bool && (bool)lp["crossfade"];
        decimal cs = 0;
        if (lp != null && lp.ContainsKey("crossfadeSeconds")) { try { cs = Convert.ToDecimal(lp["crossfadeSeconds"], Inv); } catch { } }
        numCf.Value = Math.Max(0, Math.Min(10, cs));
        chkCf.Enabled = numCf.Enabled = btnClear.Enabled = lp != null;
        syncing = false;
        loopRow.PerformLayout();
        if (loopRow.Width > 0) { var w = loopRow.Width; loopRow.Width = w + 1; loopRow.Width = w; }
    }
    void EnsureLoop(string rel, double s, double e)
    {
        var lp = FindLoop(rel);
        if (lp == null)
        {
            lp = new Dictionary<string, object>();
            lp["track"] = rel; lp["loopStart"] = ""; lp["loopEnd"] = ""; lp["crossfade"] = false; lp["crossfadeSeconds"] = 0.0;
            L("loopPoints").Add(lp);
        }
        lp["loopStart"] = FmtTime(s); lp["loopEnd"] = FmtTime(e);
        MarkDirty();
    }
    void SetEdge(char edge, double t)
    {
        string rel = PathOf(sel); if (rel == null || wave.Data == null) return;
        var L2 = LoopOf(rel);
        double s = L2 != null ? L2[0] : 0, e = L2 != null ? L2[1] : wave.Data.Duration;
        t = Math.Max(0, Math.Min(wave.Data.Duration, t));
        if (edge == 's') s = t; else e = t;
        if (e - s < 0.05) { Toast("「結束」必須比「開始」晚", true); SyncLoopUi(); return; }
        EnsureLoop(rel, s, e); SyncLoopUi(); LiveLoop(); RenderList();
    }
    void Nudge(char edge, int dir)
    {
        var L2 = LoopOf(PathOf(sel)); if (L2 == null) return;
        double step = (ModifierKeys & Keys.Shift) != 0 ? 0.1 : 0.01;
        SetEdge(edge, (edge == 's' ? L2[0] : L2[1]) + dir * step);
    }
    void LoopFromInputs()
    {
        string rel = PathOf(sel); if (rel == null || syncing) return;
        var lp = FindLoop(rel);
        if (txtLs.Text.Trim() == "" && txtLe.Text.Trim() == "" && lp == null) return;
        if (lp != null && txtLs.Text == (lp["loopStart"] as string) && txtLe.Text == (lp["loopEnd"] as string)) return;
        double s = ParseTime(txtLs.Text), e = ParseTime(txtLe.Text);
        if (s < 0 || e < 0) { Toast("時間格式請用「分:秒」，例如 0:15 或 1:23.500", true); SyncLoopUi(); return; }
        if (e - s < 0.05) { Toast("「結束」必須比「開始」晚", true); SyncLoopUi(); return; }
        EnsureLoop(rel, s, e); SyncLoopUi(); LiveLoop(); RenderList();
    }

    void Toast(string msg, bool err = false)
    {
        toast.Text = msg;
        toast.BackColor = err ? UI.Danger : Color.FromArgb(34, 38, 52);
        using (var g = CreateGraphics()) toast.Width = (int)UI.W(g, msg, toast.Font) + 40;
        LayoutEditor();
        toast.Visible = true; toast.BringToFront();
        toastTimer.Stop(); toastTimer.Interval = err ? 4200 : 3000; toastTimer.Start();
    }

    // 自我測試用
    public void TestSelect(Sel s) { SelectItem(s, false); }
    public void TestTab(string t) { tab = t; RenderAll(); }
    public void ForceClose() { dirty = false; Close(); }
    public bool TestWaveLoaded() { return wave.Data != null; }
    public string TestCut(double a, double b)
    {
        EnsureLoop(PathOf(sel), a, b);
        CutSegment(false);
        var lp = LoopOf(PathOf(sel));
        return PathOf(sel) + (lp != null ? "  loop " + lp[0].ToString("0.000") + " - " + lp[1].ToString("0.000") : "  (no loop)");
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == (Keys.Control | Keys.S)) { Save(); return true; }
        if (keyData == Keys.Space && !(ActiveControl is TextBox) && !(ActiveControl is NumericUpDown)) { TogglePlay(); return true; }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (dirty)
        {
            var r = MessageBox.Show(this, "有尚未儲存的變更，要儲存嗎？", "音樂設定工具", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
            if (r == DialogResult.Cancel) { e.Cancel = true; return; }
            if (r == DialogResult.Yes) Save();
        }
        player.Stop();
        base.OnFormClosing(e);
    }
}

static class Program
{
    [DllImport("user32.dll")] static extern bool SetProcessDPIAware();

    static string FindRoot()
    {
        string exeDir = Path.GetDirectoryName(Application.ExecutablePath);
        foreach (var d in new[] { exeDir, Path.GetDirectoryName(exeDir) })
            if (d != null && File.Exists(Path.Combine(d, @"Audio\audio_catalog.json"))) return d;
        return null;
    }


    // 開發用自我測試：解碼、播放、把視窗畫成圖片
    static void SelfTest(string root, string outDir)
    {
        var log = new StringBuilder();
        try
        {
            var sw = Stopwatch.StartNew();
            var d = MF.Decode(Path.Combine(root, @"Audio\BGM\BGM_1_2.mp3"));
            log.AppendLine("decode mp3: " + d.Duration.ToString("0.000") + "s ch=" + d.Channels + " rate=" + d.Rate + " in " + sw.ElapsedMilliseconds + "ms");
            var w = MF.Decode(Path.Combine(root, @"Audio\SFX\DrawCard.wav"));
            int pk = 0; foreach (var v in w.Pcm) pk = Math.Max(pk, Math.Abs((int)v)); log.AppendLine("decode wav: " + w.Duration.ToString("0.000") + "s ch=" + w.Channels + " rate=" + w.Rate + " peak=" + pk);
            var p = new Player(); p.Gain = 0f;
            p.Play(d, "x", 104.0, 104.75, 105.2);
            Thread.Sleep(1500);
            for (int k = 0; k < 6; k++) { log.AppendLine("loop pos (104.75-105.2): " + p.Position.ToString("0.000")); Thread.Sleep(170); }
            p.Stop();
            p.Play(w, "y", 0, -1, -1); Thread.Sleep(1500);
            log.AppendLine("short sfx ended naturally: playing=" + p.Playing);
            p.Stop();
        }
        catch (Exception ex) { log.AppendLine("ERROR " + ex); }
        var f = new MainForm(root);
        f.StartPosition = FormStartPosition.Manual; f.Location = new Point(-3000, -3000);
        f.Show(); Application.DoEvents();
        Snap(f, Path.Combine(outDir, "ui_1_battle.png"));
        // 快速連點同一首歌的兩個項目：第二個也要能載入（之前會卡在「載入中」）
        f.TestSelect(new Sel { Type = "set", Set = 0, Track = "track1" });
        f.TestSelect(new Sel { Type = "set", Set = 0, Track = "track2" });
        var swl = Stopwatch.StartNew();
        while (!f.TestWaveLoaded() && swl.ElapsedMilliseconds < 15000) { Application.DoEvents(); Thread.Sleep(20); }
        log.AppendLine("rapid switch loaded: " + f.TestWaveLoaded() + " in " + swl.ElapsedMilliseconds + " ms");
        f.ClientSize = new Size(1900, 1000); Application.DoEvents(); Snap(f, Path.Combine(outDir, "ui_1b_wide.png"));
        f.ClientSize = new Size(1280, 860); Application.DoEvents();
        f.TestSelect(new Sel { Type = "set", Set = 0, Track = "track2" });
        for (int i = 0; i < 60; i++) { Application.DoEvents(); Thread.Sleep(50); }
        Snap(f, Path.Combine(outDir, "ui_2_selected.png"));
        f.TestTab("sfx");
        f.TestSelect(new Sel { Type = "sfx", Key = "DrawCard", Idx = 0 });
        for (int i = 0; i < 30; i++) { Application.DoEvents(); Thread.Sleep(50); }
        Snap(f, Path.Combine(outDir, "ui_3_sfx.png"));
        f.TestTab("deck");
        Snap(f, Path.Combine(outDir, "ui_4_deck.png"));
        f.TestTab("menu");
        f.TestSelect(new Sel { Type = "deck", Key = "main_menu", Idx = 0 });
        for (int i = 0; i < 40; i++) { Application.DoEvents(); Thread.Sleep(50); }
        Snap(f, Path.Combine(outDir, "ui_5_menu.png"));
        f.TestTab("battle");
        f.TestSelect(new Sel { Type = "set", Set = 0, Track = "track3" });
        for (int i = 0; i < 60; i++) { Application.DoEvents(); Thread.Sleep(50); }
        string cut = f.TestCut(20.0, 35.5);
        log.AppendLine("cut -> " + cut);
        try { string cutPath = cut.Split(new[] { "  loop ", "  (no loop)" }, StringSplitOptions.None)[0]; var cd = MF.Decode(Path.Combine(root, "Audio", cutPath.Replace('/', '\\'))); double la = double.Parse(cut.Substring(cut.IndexOf("  loop ") + 7).Split(' ')[0], CultureInfo.InvariantCulture);
          var od = MF.Decode(Path.Combine(root, @"Audio\BGM\Terraria Calamity Mod Music -  Siren s Call & Forbidden Lullaby  - Theme of Leviathan_320k.mp3"));
          Func<long, double> diff = lagF => { double e = 0, n = 0; long o0 = (long)(20.0 * od.Rate); for (int k = 0; k < 8000; k++) { double x = od.Pcm[(o0 + k) * od.Channels], y = cd.Pcm[(lagF + k) * cd.Channels]; e += (x - y) * (x - y); n += x * x; } return Math.Sqrt(e / Math.Max(1, n)); };
          long at = (long)Math.Round(la * cd.Rate);
          log.AppendLine("alignment error at loop start: " + diff(at).ToString("0.000") + " (shifted 50 samples: " + diff(at + 50).ToString("0.000") + ")");
          log.AppendLine("cut duration: " + cd.Duration.ToString("0.000") + "s, size " + new FileInfo(Path.Combine(root, "Audio", cutPath.Replace('/', '\\'))).Length + " bytes"); }
        catch (Exception ex) { log.AppendLine("cut decode ERROR " + ex.Message); }
        for (int i = 0; i < 40; i++) { Application.DoEvents(); Thread.Sleep(50); }
        Snap(f, Path.Combine(outDir, "ui_6_cut.png"));
        log.AppendLine("ui ok");
        f.ForceClose();
        File.WriteAllText(Path.Combine(outDir, "selftest.txt"), log.ToString());
    }
    static void Snap(Form f, string path)
    {
        Application.DoEvents();
        using (var bmp = new Bitmap(f.Width, f.Height)) { f.DrawToBitmap(bmp, new Rectangle(0, 0, f.Width, f.Height)); bmp.Save(path); }
    }
    [STAThread]
    static void Main(string[] args)
    {
        try { SetProcessDPIAware(); } catch { }
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        if (args.Length >= 3 && args[0] == "--selftest") { SelfTest(args[1], args[2]); return; }
        string root = args.Length > 0 && Directory.Exists(args[0]) ? args[0] : FindRoot();
        if (root == null)
        {
            using (var dlg = new OpenFileDialog { Title = "請選擇模擬器資料夾裡的 Weiss Schwarz.exe", Filter = "Weiss Schwarz.exe|Weiss Schwarz.exe" })
            {
                if (dlg.ShowDialog() != DialogResult.OK) return;
                root = Path.GetDirectoryName(dlg.FileName);
            }
            if (!File.Exists(Path.Combine(root, @"Audio\audio_catalog.json")))
            {
                MessageBox.Show("這個資料夾裡找不到 Audio\\audio_catalog.json。", "音樂設定工具");
                return;
            }
        }
        Application.Run(new MainForm(root));
    }
}
