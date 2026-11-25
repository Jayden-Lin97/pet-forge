using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MaohaiziPet {
// Frame banks keep metadata, not every decoded atlas, alive for the whole app.
// The largest atlas may exceed the budget alone; it is retained only while used.
public sealed class MotionAtlasCache {
    internal static readonly MotionAtlasCache Shared=new MotionAtlasCache(384L*1024*1024);
    sealed class Identity {public long Length,Ticks;public string Hash;}
    readonly Dictionary<string,Identity> identities=new Dictionary<string,Identity>(StringComparer.OrdinalIgnoreCase);
    sealed class NativeMemory {
        long bytes;
        public NativeMemory(long size){bytes=size;GC.AddMemoryPressure(size);}
        public void Release(){long size=Interlocked.Exchange(ref bytes,0);if(size>0)GC.RemoveMemoryPressure(size);}
        ~NativeMemory(){Release();}
    }
    static readonly ConditionalWeakTable<BitmapSource,NativeMemory> lifetimes=new ConditionalWeakTable<BitmapSource,NativeMemory>();
    sealed class Entry {public string Path;public BitmapSource Pixels;public long Used,Bytes,Generation;}
    readonly Dictionary<string,Entry> entries=new Dictionary<string,Entry>(StringComparer.Ordinal);
    readonly long budget;
    long clock,generation;
    public int DecodeCount {get;private set;}
    public long ResidentBytes {get{return entries.Values.Sum(e=>e.Bytes);}}
    public MotionAtlasCache(long bytes){budget=bytes;}
    internal string ContentIdentity(string path){var file=new FileInfo(path);Identity value;if(identities.TryGetValue(path,out value)&&value.Length==file.Length&&value.Ticks==file.LastWriteTimeUtc.Ticks)return value.Hash;string hash;using(var sha=SHA256.Create())using(var stream=File.OpenRead(path))hash=Convert.ToBase64String(sha.ComputeHash(stream));if(identities.Count>256)identities.Clear();identities[path]=new Identity{Length=file.Length,Ticks=file.LastWriteTimeUtc.Ticks,Hash=hash};return hash;}
    internal BitmapSource Get(string path,int width,int height,out long version,string contentIdentity=null,bool removeGreen=false){
        string key=contentIdentity??ContentIdentity(path);
        if(removeGreen)key+="|green-v1";
        Entry entry;if(entries.TryGetValue(key,out entry)){if(entry.Pixels.PixelWidth!=width||entry.Pixels.PixelHeight!=height)throw new InvalidDataException("动作图集帧尺寸无效");entry.Used=++clock;version=entry.Generation;return entry.Pixels;}
        long size=(long)width*height*4;
        while(entries.Count>0&&ResidentBytes+size>budget){var oldest=entries.Values.OrderBy(e=>e.Used).First();entries.Remove(oldest.Path);}
        // WPF stores pixels in unmanaged memory. Account for it so evicted
        // atlases are collected rather than exhausting native image resources.
        var lifetime=new NativeMemory(size);BitmapSource pixels;
        try{
            BitmapSource source;using(var f=File.OpenRead(path)){var decoder=new PngBitmapDecoder(f,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnLoad);source=decoder.Frames[0];}
            if(source.PixelWidth!=width||source.PixelHeight!=height)throw new InvalidDataException("动作图集帧尺寸无效");
            var converted=new FormatConvertedBitmap(source,PixelFormats.Pbgra32,null,0);var decoded=new WriteableBitmap(converted);if(removeGreen)GreenSpill.Remove(decoded);decoded.Freeze();pixels=decoded;lifetimes.Add(pixels,lifetime);
        }catch{lifetime.Release();throw;}
        entry=new Entry{Path=key,Pixels=pixels,Used=++clock,Bytes=size,Generation=++generation};entries[key]=entry;DecodeCount++;version=entry.Generation;return pixels;
    }
    internal static void CheckDimensions(string path,int width,int height){
        byte[] header=new byte[24];using(var f=File.OpenRead(path))if(f.Read(header,0,header.Length)!=header.Length)throw new InvalidDataException("PNG 文件不完整");
        byte[] signature={137,80,78,71,13,10,26,10};for(int i=0;i<8;i++)if(header[i]!=signature[i])throw new InvalidDataException("动作文件不是 PNG");
        Func<int,long> number=at=>((long)header[at]<<24)|((long)header[at+1]<<16)|((long)header[at+2]<<8)|header[at+3];
        if(number(16)!=width||number(20)!=height)throw new InvalidDataException("动作图集帧尺寸无效");
    }
}
public sealed class MotionFrames:IEnumerable<BitmapSource> {
    readonly BitmapSource[] eager;
    readonly MotionSpec spec;
    readonly MotionAtlasCache cache;
    readonly string path;
    readonly string identity;
    readonly bool removeGreen;
    WeakReference last;
    int lastCell=-1;
    long lastVersion;
    public int Length {get{return eager==null?spec.frames:eager.Length;}}
    public MotionFrames(BitmapSource[] frames){eager=frames;}
    internal MotionFrames(MotionSpec motion,string file,MotionAtlasCache atlasCache,bool despill=false){spec=motion;path=file;cache=atlasCache;identity=cache.ContentIdentity(file);removeGreen=despill;}
    public BitmapSource this[int index]{get{
        if(eager!=null)return eager[index];if(index<0||index>=Length)throw new IndexOutOfRangeException();
        int size=spec.size==0?256:spec.size,cell=spec.frameMap==null?index:spec.frameMap[index];long version;
        BitmapSource atlas=cache.Get(path,spec.columns*size,spec.rows*size,out version,identity);
        var reused=last==null?null:last.Target as BitmapSource;if(reused!=null&&cell==lastCell&&version==lastVersion)return reused;
        BitmapSource frame=new CroppedBitmap(atlas,new Int32Rect(cell%spec.columns*size,cell/spec.columns*size,size,size));frame.Freeze();if(removeGreen)frame=GreenSpill.Correct(frame);last=new WeakReference(frame);lastCell=cell;lastVersion=version;return frame;
    }}
    public IEnumerator<BitmapSource> GetEnumerator(){for(int i=0;i<Length;i++)yield return this[i];}
    IEnumerator IEnumerable.GetEnumerator(){return GetEnumerator();}
}
}
