using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MaohaiziPet {
[DataContract] public sealed class TestResult {
    [DataMember] public string name;
    [DataMember] public bool passed;
    [DataMember] public string error;
}
[DataContract] public sealed class TestReport {
    [DataMember] public string timestamp;
    [DataMember] public int passed,failed;
    [DataMember] public List<TestResult> tests=new List<TestResult>();
}
public static class SelfTest {
    static TestReport report;
    static void Check(string name,Action action){var t=new TestResult{name=name};try{action();t.passed=true;report.passed++;}catch(Exception e){t.error=e.Message;report.failed++;}report.tests.Add(t);}
    static void Assert(bool b,string error){if(!b)throw new Exception(error);}
    static void Reject(Action a){try{a();}catch(InvalidDataException){return;}throw new Exception("未拒绝无效素材包");}
    static void SavePng(BitmapSource b,string p){var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(b));using(var f=File.Create(p))encoder.Save(f);}
    static void MakeZip(string p,string entry){using(var f=File.Create(p))using(var z=new ZipArchive(f,ZipArchiveMode.Create)){using(var writer=new StreamWriter(z.CreateEntry(entry).Open()))writer.Write("invalid");}}
    public static int Run(string folder){
        folder=Path.GetFullPath(folder);Directory.CreateDirectory(folder);report=new TestReport{timestamp=DateTimeOffset.Now.ToString("o")};
        foreach(Personality personality in Enum.GetValues(typeof(Personality))) {
            Personality copy=personality;
            Check("性格 "+copy+"：摸头有反馈",delegate{var e=new PetEngine(copy,PetMode.Companion,1);Assert(e.React(PetAction.Pet)&&e.Frame()==10&&e.StrokeCount==1&&e.Message("团团").Length>0,"摸头无反馈");});
            Check("性格 "+copy+"：逗猫棒超时恢复",delegate{var e=new PetEngine(copy,PetMode.Companion,1);e.React(PetAction.Tease);Assert(e.Frame()==8||e.Frame()==9,"缺少扑玩动作");for(int i=0;i<13;i++)e.Tick(1);Assert(e.Action==PetAction.Idle,"未恢复待机");});
            Check("性格 "+copy+"：喂食独立反馈",delegate{var e=new PetEngine(copy,PetMode.Companion,1);e.React(PetAction.Feed);Assert(e.Frame()==12&&e.Message("团团").Contains("团团"),"没有喂食反馈");});
        }
        Check("三种性格的摸头反馈不同",delegate{var strings=new HashSet<string>();foreach(Personality p in Enum.GetValues(typeof(Personality))){var e=new PetEngine(p,PetMode.Companion,1);e.React(PetAction.Pet);strings.Add(e.Message("猫猫"));}Assert(strings.Count==3,"性格反馈相同");});
        Check("勿扰时拒绝互动、没有气泡",delegate{var e=new PetEngine(Personality.Clingy,PetMode.Quiet,1);Assert(!e.React(PetAction.Tease)&&!e.React(PetAction.Feed)&&!e.React(PetAction.Pet),"勿扰触发了互动");e.Tick(1);Assert(e.Action==PetAction.Idle&&e.Message("猫")=="","勿扰没有清空状态");});
        Check("切换勿扰立即停止逗猫",delegate{var e=new PetEngine(Personality.Clingy,PetMode.Free,1);e.React(PetAction.Tease);e.SetMode(PetMode.Quiet);Assert(e.Action==PetAction.Idle,"仍在追逐");e.SetMode(PetMode.Companion);Assert(e.React(PetAction.Pet),"退出勿扰后不能互动");});
        Check("空闲后进入睡眠或伸展",delegate{var e=new PetEngine(Personality.Clingy,PetMode.Companion,1);for(int i=0;i<36;i++)e.Tick(1);Assert(e.Action==PetAction.Sleep||e.Action==PetAction.Stretch,"无自然待机变化");});
        Check("名字空白和超长输入",delegate{Assert(PetSettings.CleanName(" ")=="小橘","空名字未修正");Assert(PetSettings.CleanName(new string('猫',80)).Length==24,"名字没有限制");});
        Check("带空格和中文的启动路径正确引用",delegate{string s=PetController.StartupCommand("C:\\宠物 软件\\猫.exe");Assert(s=="\"C:\\宠物 软件\\猫.exe\" --background","启动命令不安全");});
        Check("断开显示器后位置回到可见范围",delegate{var p=PetWindow.ClampPosition(new Point(5000,-4000),new Rect(0,0,1920,1040),360,300);Assert(p.X==1560&&p.Y==0,"窗口仍然不可见");});
        Check("负坐标副屏保留有效位置",delegate{var p=PetWindow.ClampPosition(new Point(-1500,200),new Rect(-1920,0,1920,1040),360,300);Assert(p.X==-1500&&p.Y==200,"副屏坐标处理错误");});
        Check("小分辨率工作区不溢出",delegate{var p=PetWindow.ClampPosition(new Point(999,999),new Rect(0,0,640,480),360,300);Assert(p.X==280&&p.Y==180,"边缘位置错误");});
        var skin=new PetSkin();
        for(int frame=0;frame<16;frame++){int copy=frame;Check("原创样例姿态 "+copy+" 可渲染且有透明像素",delegate{var b=skin.Render(copy,.7,null);byte[] px=new byte[256*256*4];b.CopyPixels(px,1024,0);int opaque=0,clear=0;for(int i=3;i<px.Length;i+=4){if(px[i]>0)opaque++;else clear++;}Assert(opaque>1000&&clear>1000,"姿态空白或失去透明背景");});}
        string pack=Path.Combine(folder,"小橘-样例宠物包.zip");string imported=null;
        Check("导出完整独立素材包",delegate{skin.Export(pack);using(var z=ZipFile.OpenRead(pack))Assert(z.GetEntry("pet.json")!=null&&z.GetEntry("atlas.png")!=null,"缺少清单或图集");});
        Check("导入素材包并解码 16 个动作",delegate{imported=PetSkin.Import(pack,Path.Combine(folder,"test-pets"));var loaded=PetSkin.Load(imported);Assert(loaded.Frames.Length==16&&loaded.Frames.All(x=>x.PixelWidth==256),"图集解析错误");SavePng(loaded.Render(10,.7,null),Path.Combine(folder,"imported-pet.png"));});
        Check("恶意父目录路径被拒绝",delegate{string p=Path.Combine(folder,"bad-traversal.zip");MakeZip(p,"../outside.png");Reject(delegate{PetSkin.Import(p,Path.Combine(folder,"bad-pets"));});Assert(!File.Exists(Path.Combine(folder,"outside.png")),"文件逃逸");});
        Check("不执行或导入 EXE",delegate{string p=Path.Combine(folder,"bad-executable.zip");MakeZip(p,"pet.exe");Reject(delegate{PetSkin.Import(p,Path.Combine(folder,"bad-pets"));});});
        Check("素材包中绝对路径被拒绝",delegate{Reject(delegate{PetSkin.SafePart("C:/secret.png");});Reject(delegate{PetSkin.SafePart("/secret.png");});Reject(delegate{PetSkin.SafePart("..\\secret.png");});});
        Check("重复素材文件被拒绝",delegate{string p=Path.Combine(folder,"bad-duplicate.zip");using(var f=File.Create(p))using(var z=new ZipArchive(f,ZipArchiveMode.Create)){z.CreateEntry("pet.json");z.CreateEntry("pet.json");}Reject(delegate{PetSkin.Import(p,Path.Combine(folder,"bad-pets"));});});
        Check("超过19个文件的素材包被拒绝",delegate{string p=Path.Combine(folder,"bad-too-many.zip");using(var f=File.Create(p))using(var z=new ZipArchive(f,ZipArchiveMode.Create))for(int i=0;i<20;i++)z.CreateEntry(i+".png");Reject(delegate{PetSkin.Import(p,Path.Combine(folder,"bad-pets"));});});
        Check("导出 4 个循环 GIF，每个 12 帧",delegate{string gifs=Path.Combine(folder,"表情包");skin.ExportGifs(gifs);var files=Directory.GetFiles(gifs,"*.gif");Assert(files.Length==4,"缺少表情文件");foreach(string path in files){using(var f=File.OpenRead(path)){var gif=new GifBitmapDecoder(f,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnLoad);Assert(gif.Frames.Count==12,"GIF 帧数错误");}Assert(System.Text.Encoding.ASCII.GetString(File.ReadAllBytes(path)).Contains("NETSCAPE2.0"),"没有循环标记");}});
        Check("保存样品预览和图集",delegate{SavePng(skin.Render(0,.7,null),Path.Combine(folder,"sample-pet.png"));using(var z=ZipFile.OpenRead(pack))z.GetEntry("atlas.png").ExtractToFile(Path.Combine(folder,"sample-atlas.png"),true);});
        Check("导入图集后的表情仍有动态变化",delegate{var loaded=PetSkin.Load(imported);var first=loaded.Render(10,0,null);var last=loaded.Render(10,1,null);var a=new byte[256*256*4];var b=new byte[a.Length];first.CopyPixels(a,1024,0);last.CopyPixels(b,1024,0);Assert(!a.SequenceEqual(b),"图集导入后动画静止");});
        Check("单帧角色能渲染各互动状态，导出后仍保留单帧身份",delegate{
            string singleDir=Path.Combine(folder,"single-frame");Directory.CreateDirectory(singleDir);
            SavePng(skin.Render(0,0,null),Path.Combine(singleDir,"single.png"));
            var manifest=new SkinManifest{columns=1,rows=1,sheet="single.png",name="单帧测试",description="单帧角色，非完整动作图集"};
            string manifestPath=Path.Combine(singleDir,"pet.json");using(var f=File.Create(manifestPath))new DataContractJsonSerializer(typeof(SkinManifest)).WriteObject(f,manifest);
            var single=PetSkin.Load(manifestPath);Assert(single.Frames.Length==1,"单帧角色被伪造为多帧");
            for(int actionFrame=0;actionFrame<16;actionFrame++)Assert(single.Render(actionFrame,.5,null).PixelWidth==256,"互动状态越界");
            string singleZip=Path.Combine(folder,"single-frame.zip");single.Export(singleZip);
            using(var z=ZipFile.OpenRead(singleZip))using(var f=z.GetEntry("pet.json").Open()){var exported=PetSkin.ReadManifest(f);Assert(exported.columns==1&&exported.rows==1,"导出误标成完整动作图集");}
            var restored=PetSkin.Load(PetSkin.Import(singleZip,Path.Combine(folder,"single-import")));Assert(restored.Frames.Length==1,"单帧往返导入失败");
        });
        using(var f=File.Create(Path.Combine(folder,"test-report.json")))new DataContractJsonSerializer(typeof(TestReport)).WriteObject(f,report);
        return report.failed==0?0:1;
    }
}
}
