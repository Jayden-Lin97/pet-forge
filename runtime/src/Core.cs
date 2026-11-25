using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml.Serialization;

namespace MaohaiziPet {
public enum Personality { Clingy, Shy, Aloof }
public enum PetMode { Companion, Free, Quiet }
public enum PetAction { Idle, Pet, Feed, Tease, Drag, Sleep, Stretch, Greet, Roll }
public sealed class PetSettings {
    private static string previewRoot;
    private static bool customerProfile;
    public static bool IsPreviewProfile {get{return previewRoot!=null&&!customerProfile;}}
    public static void UsePreviewProfile(string path){previewRoot=path==null?null:Path.GetFullPath(path);customerProfile=false;}
    public static void UseCustomerProfile(string identity){
        if(string.IsNullOrEmpty(identity)||identity.Length>64||identity.Any(c=>!char.IsLetterOrDigit(c)&&c!='-'))throw new InvalidDataException("宠物信息不完整");
        previewRoot=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"PetForge",identity);customerProfile=true;
    }
    public string Name = "小橘";
    public Personality Personality = Personality.Clingy;
    public PetMode Mode = PetMode.Companion;
    public double Size = 150;
    public bool Topmost = true;
    public bool WarmWords = true;
    public double Familiarity = 0;
    public PetLifeRecord Life=new PetLifeRecord();
    public PetNeedsState Needs=new PetNeedsState();
    public string SkinPath = "";
    public double Left = double.NaN;
    public double Top = double.NaN;
    public static string Root { get { return previewRoot??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MaohaiziPet"); } }
    public static PetSettings Load() {
        PetSettings s=null;string path=Path.Combine(Root,"settings.xml");
        try { using (var f = File.OpenRead(path)) s=(PetSettings)new XmlSerializer(typeof(PetSettings)).Deserialize(f); }
        catch {
            s=ProfileBackups.Recover(Root);
            if(File.Exists(path))try{string damaged=Path.Combine(Root,"settings-unreadable-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".xml");if(!File.Exists(damaged))File.Copy(path,damaged);}catch{}
        }
        if(s==null)s=new PetSettings();
            s.Name = CleanName(s.Name); s.Size = Math.Max(90,Math.Min(240,s.Size));
            s.Familiarity=double.IsNaN(s.Familiarity)||double.IsInfinity(s.Familiarity)?0:Math.Max(0,Math.Min(100,s.Familiarity));
            if (!Enum.IsDefined(typeof(Personality),s.Personality)) s.Personality=Personality.Clingy;
            if (!Enum.IsDefined(typeof(PetMode),s.Mode)) s.Mode=PetMode.Companion;
            if(s.Life==null)s.Life=new PetLifeRecord();s.Life.Normalize();
            if(s.Needs==null)s.Needs=new PetNeedsState();s.Needs.Normalize();
            return s;
    }
    public static string CleanName(string s) { s=(s??"").Trim(); return s.Length==0?"小橘":s.Substring(0,Math.Min(24,s.Length)); }
    public void Save() {
        Directory.CreateDirectory(Root); string p=Path.Combine(Root,"settings.xml"); string temp=p+".tmp";
        try{ProfileBackups.BeforeSave(p,DateTime.Now);}catch{} // A failed backup must not discard a valid live save.
        using(var f=File.Create(temp)) new XmlSerializer(typeof(PetSettings)).Serialize(f,this);
        if(File.Exists(p)) File.Replace(temp,p,null); else File.Move(temp,p);
    }
}
public sealed class PetEngine {
    public Personality Personality;
    public PetMode Mode;
    public PetAction Action=PetAction.Idle;
    public double Clock, Until, LastInteraction;
    public int StrokeCount;
    public bool AutonomousIdle=true;
    private readonly Random random;
    public PetEngine(Personality p,PetMode m,int seed) {Personality=p; Mode=m; random=new Random(seed);}
    public void SetMode(PetMode m) {Mode=m; Action=PetAction.Idle; Until=0; LastInteraction=Clock;}
    public bool React(PetAction a) {
        if(Mode==PetMode.Quiet) return false;
        if(a==PetAction.Pet) StrokeCount++;
        Action=a;
        Until=Clock+(a==PetAction.Feed?4.5:a==PetAction.Tease?12:a==PetAction.Drag?1:a==PetAction.Pet?3:3.5);
        LastInteraction=Clock; return true;
    }
    public void Tick(double seconds) {
        Clock+=Math.Max(0,Math.Min(1,seconds));
        if(Mode==PetMode.Quiet) {Action=PetAction.Idle; return;}
        if(Action!=PetAction.Idle && Clock>=Until) {Action=PetAction.Idle; Until=0;}
        if(AutonomousIdle&&Action==PetAction.Idle && Clock-LastInteraction>35) {
            Action=random.Next(4)==0?PetAction.Stretch:PetAction.Sleep;
            Until=Clock+(Action==PetAction.Sleep?18:3); LastInteraction=Clock;
        }
    }
    public string Message(string name) {
        if(Mode==PetMode.Quiet) return "";
        if(Action==PetAction.Feed) return name+"：嚼嚼，好香呀";
        if(Action==PetAction.Tease) return Personality==Personality.Shy?"先观察一下……扑！":"盯住了，扑过去！";
        if(Action==PetAction.Pet) {
            if(Personality==Personality.Aloof) return StrokeCount%3==0?"好吧，再摸一下。":"哼，只让你摸一会儿。";
            if(Personality==Personality.Shy) return StrokeCount<3?"慢慢来，我有一点害羞。":"是熟悉的手，可以再摸摸。";
            return "呼噜呼噜……再摸摸我";
        }
        if(Action==PetAction.Greet) return "我在这里，陪你一会儿。";
        return "";
    }
    public int Frame() {
        double t=Clock;
        switch(Action) {
            case PetAction.Pet:return 10;
            case PetAction.Feed:return 12;
            case PetAction.Sleep:return 13;
            case PetAction.Stretch:return 14;
            case PetAction.Greet:return 15;
            case PetAction.Roll:return 11;
            case PetAction.Drag:return 9;
            case PetAction.Tease:return ((int)(t*3)%4<2)?8:9;
            default:return ((int)(t*2)%13==0)?1:0;
        }
    }
}
[DataContract] public sealed class SkinManifest {
    [DataMember] public int version=1;
    [DataMember] public string name="小橘";
    [DataMember] public string sheet="atlas.png";
    [DataMember] public int columns=4;
    [DataMember] public int rows=4;
    [DataMember] public string description="陪在桌面的小伙伴";
    [DataMember(EmitDefaultValue=false)] public string animation;
    [DataMember(EmitDefaultValue=false)] public bool customerEdition,disableGreenSpill;
    [DataMember(EmitDefaultValue=false)] public string profileId;
}
public sealed class PetSkin {
    public string Name="小橘",Description="陪在桌面的小伙伴";
    public bool CustomerEdition,DisableGreenSpill;
    public BitmapSource[] Frames;
    public LivingPet Living;
    public MotionLibrary Motions;
    public readonly MotionPlayer Motion=new MotionPlayer();
    public bool IsSample {get{return Frames==null;}}
    public static SkinManifest ReadManifest(Stream s) {return (SkinManifest)new DataContractJsonSerializer(typeof(SkinManifest)).ReadObject(s);}
    public static string SafePart(string s) {
        if(string.IsNullOrWhiteSpace(s)||s.Contains("..")||s.Contains(":")||s.StartsWith("/")||s.StartsWith("\\")||s.Contains("\\")) throw new InvalidDataException("宠物包包含不安全路径");
        return s;
    }
    public static string Import(string zipPath,string destinationRoot) {
        string temp=Path.Combine(destinationRoot,"pet-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        try {
            using(var zip=ZipFile.OpenRead(zipPath)) {
                if(zip.Entries.Count>19) throw new InvalidDataException("素材包文件过多");
                long total=0;
                var names=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach(var entry in zip.Entries) {
                    string part=SafePart(entry.FullName);
                    if(part.Contains("/")||!names.Add(part)) throw new InvalidDataException("请将素材放在 ZIP 根目录，文件名不能重复");
                    if(part!="pet.json"&&part!="motions.json"&&!part.EndsWith(".png",StringComparison.OrdinalIgnoreCase)&&part!="README.txt") throw new InvalidDataException("只支持 JSON、PNG 和说明文件");
                    // Bounded allowance for two reviewed native toy reactions.
                    total+=entry.Length; if(entry.Length>32*1024*1024 || total>112*1024*1024) throw new InvalidDataException("素材包过大");
                    entry.ExtractToFile(Path.Combine(temp,part));
                }
            }
            string manifest=Path.Combine(temp,"pet.json"); Load(manifest); return manifest;
        } catch { if(Directory.Exists(temp)) Directory.Delete(temp,true); throw; }
    }
    public static PetSkin Load(string path) {
        SkinManifest m;
        using(var f=File.OpenRead(path)) m=ReadManifest(f);
        if(m==null||m.version!=1||!((m.columns==4&&m.rows==4)||(m.columns==1&&m.rows==1))) throw new InvalidDataException("需要版本 1 的 4×4 动作图集或 1×1 单帧角色");
        string part=SafePart(m.sheet); if(part.Contains("/")||!part.EndsWith(".png",StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("图集必须是同目录 PNG");
        BitmapSource atlas;
        using(var f=File.OpenRead(Path.Combine(Path.GetDirectoryName(path),part))) {
            var decoder=new PngBitmapDecoder(f,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnLoad); atlas=decoder.Frames[0]; atlas.Freeze();
        }
        if(atlas.PixelWidth<128||atlas.PixelHeight<128||atlas.PixelWidth>4096||atlas.PixelHeight>4096||atlas.PixelWidth%m.columns!=0||atlas.PixelHeight%m.rows!=0) throw new InvalidDataException("图集尺寸无效");
        bool ragdoll=m.animation=="ragdoll-live-v1"||m.animation=="ragdoll-live-v2";if(ragdoll&&!m.disableGreenSpill)atlas=GreenSpill.Correct(atlas);
        var skin=new PetSkin{CustomerEdition=m.customerEdition,DisableGreenSpill=m.disableGreenSpill}; skin.Name=PetSettings.CleanName(m.name); skin.Description=(m.description??"定制素材包").Substring(0,Math.Min(80,(m.description??"定制素材包").Length));
        skin.Frames=new BitmapSource[m.columns*m.rows];
        for(int i=0;i<skin.Frames.Length;i++) {var b=new CroppedBitmap(atlas,new Int32Rect((i%m.columns)*atlas.PixelWidth/m.columns,(i/m.columns)*atlas.PixelHeight/m.rows,atlas.PixelWidth/m.columns,atlas.PixelHeight/m.rows)); b.Freeze();skin.Frames[i]=b;}
        if(m.animation=="ragdoll-live-v1"||m.animation=="ragdoll-live-v2"){
            if(skin.Frames.Length!=1)throw new InvalidDataException("连续角色驱动需要单张校准纹理");
            skin.Living=new LivingPet(skin.Frames[0],113,m.animation=="ragdoll-live-v2");
            skin.Motions=MotionLibrary.Load(Path.GetDirectoryName(path),!m.disableGreenSpill);
        }else if(!string.IsNullOrEmpty(m.animation))throw new InvalidDataException("不支持该角色动画驱动");
        return skin;
    }
    public void Draw(DrawingContext dc,int frame,double phase,Rect rect) {
        if(Living!=null){
            dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(21,0,0,0)),null,new Point(rect.X+rect.Width*.51,rect.Y+rect.Height*.933),rect.Width*.29,rect.Height*.018);
            if(Motion.Active){double weight=Motion.PoseWeight;var spec=Motion.VisualSpec;Rect motionRect=new Rect(rect.X+spec.originX*rect.Width/256,rect.Y+spec.originY*rect.Height/256,Motion.VisualSize*rect.Width/256,Motion.VisualSize*rect.Height/256);if(weight<1){dc.PushOpacity(1-weight);dc.DrawImage(Living.Render(),rect);dc.Pop();}if(weight>0){dc.PushOpacity(weight);dc.DrawImage(Motion.Image,motionRect);dc.Pop();}}else dc.DrawImage(Living.Render(),rect);return;
        }
        if(Frames!=null) {
            double angle=frame==10?Math.Sin(phase*2)*2.4:frame==15?Math.Sin(phase*4)*2:0;
            double lift=frame==13?Math.Sin(phase*2)*.6:Math.Sin(phase*2)*1.1;
            dc.PushTransform(new RotateTransform(angle,rect.X+rect.Width/2,rect.Bottom));dc.PushTransform(new TranslateTransform(0,lift));
            dc.DrawImage(Frames[Math.Max(0,Math.Min(Frames.Length-1,frame))],rect);dc.Pop();dc.Pop();return;
        }
        dc.PushTransform(new TranslateTransform(rect.X,rect.Y)); dc.PushTransform(new ScaleTransform(rect.Width/256,rect.Height/256));
        CatArt.Draw(dc,frame,phase); dc.Pop();dc.Pop();
    }
    public BitmapSource Render(int frame,double phase,string caption) {
        if(Living!=null){var preview=new DrawingVisual();using(var d=preview.RenderOpen())Draw(d,frame,phase,new Rect(32,32,256,256));var picture=new RenderTargetBitmap(320,320,96,96,PixelFormats.Pbgra32);picture.Render(preview);picture.Freeze();return picture;}
        var visual=new DrawingVisual();using(var dc=visual.RenderOpen()) {
            Draw(dc,frame,phase,new Rect(0,0,256,256));
            if(!string.IsNullOrEmpty(caption)) {
                dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(238,255,252,245)),null,new Rect(10,225,236,30),12,12);
                var text=new FormattedText(caption,System.Globalization.CultureInfo.GetCultureInfo("zh-CN"),FlowDirection.LeftToRight,new Typeface("Microsoft YaHei"),16,new SolidColorBrush(Color.FromRgb(79,56,45)));
                dc.DrawText(text,new Point((256-text.Width)/2,228));
            }
        }
        var bitmap=new RenderTargetBitmap(256,256,96,96,PixelFormats.Pbgra32);bitmap.Render(visual);bitmap.Freeze();return bitmap;
    }
    public void Export(string path) {
        using(var file=File.Create(path)) using(var zip=new ZipArchive(file,ZipArchiveMode.Create)) {
            var m=new SkinManifest();m.customerEdition=CustomerEdition;m.disableGreenSpill=DisableGreenSpill;m.name=Name;m.description=Description;if(Frames!=null&&Frames.Length==1){m.columns=1;m.rows=1;}if(Living!=null)m.animation=Living.AnimationId;
            using(var s=zip.CreateEntry("pet.json").Open()) new DataContractJsonSerializer(typeof(SkinManifest)).WriteObject(s,m);
            var visual=new DrawingVisual();using(var dc=visual.RenderOpen()) for(int i=0;i<m.columns*m.rows;i++){var r=new Rect((i%m.columns)*256,(i/m.columns)*256,256,256);if(Living!=null)dc.DrawImage(Frames[0],r);else Draw(dc,i,0,r);}
            var atlas=new RenderTargetBitmap(m.columns*256,m.rows*256,96,96,PixelFormats.Pbgra32);atlas.Render(visual);
            var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(atlas));using(var memory=new MemoryStream()){encoder.Save(memory);memory.Position=0;using(var s=zip.CreateEntry("atlas.png").Open())memory.CopyTo(s);}
            using(var s=new StreamWriter(zip.CreateEntry("README.txt").Open(),Encoding.UTF8)) s.Write("毛孩子桌宠素材包。导入程序即可使用。仅含素材，无可执行代码。");
            if(Motions!=null)Motions.Export(zip);
        }
    }
    public void ExportGifs(string folder) {
        Directory.CreateDirectory(folder);
        var captions=new[]{"摸摸我","下班啦","困了","收到"};var frames=new[]{10,15,13,15};
        for(int j=0;j<4;j++) {
            var encoder=new GifBitmapEncoder();
            for(int i=0;i<12;i++) {
                var metadata=new BitmapMetadata("gif");metadata.SetQuery("/grctlext/Delay",(ushort)12);metadata.SetQuery("/grctlext/Disposal",(byte)2);
                encoder.Frames.Add(BitmapFrame.Create(Render(frames[j],i*.22,captions[j]),null,metadata,null));
            }
            using(var memory=new MemoryStream()) {
                encoder.Save(memory);byte[] bytes=memory.ToArray();int insertion=13+(((bytes[10]&128)!=0)?3*(1<<((bytes[10]&7)+1)):0);
                byte[] loop={0x21,0xff,0x0b,0x4e,0x45,0x54,0x53,0x43,0x41,0x50,0x45,0x32,0x2e,0x30,0x03,0x01,0x00,0x00,0x00};
                using(var f=File.Create(Path.Combine(folder,captions[j]+".gif"))) {f.Write(bytes,0,insertion);f.Write(loop,0,loop.Length);f.Write(bytes,insertion,bytes.Length-insertion);}
            }
        }
    }
}
public static class CatArt {
    static Brush B(string hex){return (Brush)new BrushConverter().ConvertFromString(hex);}
    static readonly Brush Cream=B("#FFF3D9"),Orange=B("#EAA767"),Ink=B("#614A3C"),Pink=B("#E8A3A0"),White=B("#FFFDF8"),Green=B("#74836A");
    static readonly Pen Outline=new Pen(Ink,3.8){StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round,LineJoin=PenLineJoin.Round};
    static void Path(DrawingContext d,string geometry,Brush fill,Pen pen){d.DrawGeometry(fill,pen,Geometry.Parse(geometry));}
    static void Line(DrawingContext d,double x,double y,double xx,double yy,Pen p){d.DrawLine(p,new Point(x,y),new Point(xx,yy));}
    public static void Draw(DrawingContext d,int frame,double phase) {
        double breath=Math.Sin(phase*2)*1.5;
        if(frame==13) {
            d.DrawEllipse(Orange,Outline,new Point(128,182),78,38); d.DrawEllipse(Cream,Outline,new Point(103,174),42,35);
            Path(d,"M 67,155 L 64,125 L 88,143 M 124,145 L 147,127 L 143,161",Cream,Outline);
            Path(d,"M 76,171 Q 85,180 94,171 M 107,171 Q 116,180 125,171",null,Outline);
            d.DrawEllipse(Pink,null,new Point(101,183),5,3);Path(d,"M 175,180 C 217,137 215,221 162,202",null,new Pen(Orange,19));
            d.DrawText(new FormattedText("z",System.Globalization.CultureInfo.InvariantCulture,FlowDirection.LeftToRight,new Typeface("Segoe UI"),18+Math.Sin(phase*2)*2,Green),new Point(167,125-Math.Sin(phase*2)*8));
            return;
        }
        d.PushTransform(new TranslateTransform(0,breath));
        double tail=Math.Sin(phase*3)*12;
        Path(d,"M 166,195 C 230,218 230,"+(129+tail).ToString(System.Globalization.CultureInfo.InvariantCulture)+" 203,135",null,new Pen(Ink,20){StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round});
        Path(d,"M 166,195 C 230,218 230,"+(129+tail).ToString(System.Globalization.CultureInfo.InvariantCulture)+" 203,135",null,new Pen(Orange,13){StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round});
        bool walking=frame>=4&&frame<=7; double step=walking?((frame%2==0)?-7:7):0;
        if(frame==8||frame==9) d.PushTransform(new ScaleTransform(1.05,.83,128,218));
        else if(frame==14) d.PushTransform(new ScaleTransform(.93,1.08,128,218));
        else d.PushTransform(new RotateTransform(frame==11?13:0,128,165));
        d.DrawEllipse(Orange,Outline,new Point(128,168),49,57);d.DrawEllipse(Cream,null,new Point(128,178),33,42);
        d.DrawRoundedRectangle(Cream,Outline,new Rect(89+step,186,32,34),15,15);
        d.DrawRoundedRectangle(Cream,Outline,new Rect(135-step,186,32,34),15,15);
        if(frame==15) {d.PushTransform(new RotateTransform(-22+Math.Sin(phase*5)*10,166,162));d.DrawRoundedRectangle(Cream,Outline,new Rect(159,124,22,50),11,11);d.DrawEllipse(Pink,null,new Point(171,137),5,6);d.Pop();}
        else {d.DrawEllipse(Cream,Outline,new Point(94,181),13,25);d.DrawEllipse(Cream,Outline,new Point(160,181),13,25);}
        double headAngle=frame==10?Math.Sin(phase*2)*5:frame==2?-8:frame==3?8:frame==12?12:0;
        d.PushTransform(new RotateTransform(headAngle,128,115));
        Path(d,"M 77,89 L 74,39 Q 76,33 82,40 L 113,67 M 143,67 L 177,40 Q 182,34 183,42 L 181,92",Orange,Outline);
        Path(d,"M 84,72 L 82,49 L 103,69 M 155,70 L 175,49 L 174,76",Pink,null);
        d.DrawEllipse(Cream,Outline,new Point(128,112),63,50);
        Path(d,"M 92,70 Q 128,48 161,71 L 151,96 L 142,78 L 130,99 L 117,79 L 106,97 Z",Orange,null);
        d.DrawEllipse(White,null,new Point(110,132),19,14); d.DrawEllipse(White,null,new Point(143,132),19,14);
        bool close=frame==1||frame==10||frame==12;
        if(close) {Path(d,"M 89,113 Q 101,125 113,113 M 141,113 Q 153,125 165,113",null,Outline);}
        else {
            d.DrawEllipse(Green,null,new Point(102,113),10,12);d.DrawEllipse(Green,null,new Point(153,113),10,12);
            d.DrawEllipse(Ink,null,new Point(103+(frame==2?-3:frame==3?3:0),114),4,10);d.DrawEllipse(Ink,null,new Point(154+(frame==2?-3:frame==3?3:0),114),4,10);
            d.DrawEllipse(White,null,new Point(100,109),3,3);d.DrawEllipse(White,null,new Point(151,109),3,3);
        }
        Path(d,"M 120,131 Q 128,125 136,131 L 128,138 Z",Pink,null);
        Path(d,"M 128,138 Q 122,148 116,139 M 128,138 Q 134,148 140,139",null,new Pen(Ink,2.1));
        d.DrawEllipse(new SolidColorBrush(Color.FromArgb(80,232,163,160)),null,new Point(88,132),10,5);
        d.DrawEllipse(new SolidColorBrush(Color.FromArgb(80,232,163,160)),null,new Point(167,132),10,5);
        var whisker=new Pen(Ink,1.6);Line(d,88,131,57,127,whisker);Line(d,88,137,57,141,whisker);Line(d,167,131,197,127,whisker);Line(d,167,137,197,141,whisker);
        d.Pop();d.Pop();d.Pop();
    }
}
}
