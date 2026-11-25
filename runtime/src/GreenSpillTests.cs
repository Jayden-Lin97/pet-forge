using System;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MaohaiziPet {
public static class GreenSpillTests {
    static LivingReport report;
    static void Assert(bool ok,string text){if(!ok)throw new Exception(text);}
    static void Check(string name,Action run){var t=new TestResult{name=name};try{run();t.passed=true;report.passed++;}catch(Exception e){t.error=e.ToString();report.failed++;}report.tests.Add(t);}
    static byte[] Pixels(BitmapSource image){var data=new byte[image.PixelWidth*image.PixelHeight*4];image.CopyPixels(data,image.PixelWidth*4,0);return data;}
    static BitmapSource Image(byte[] v,int width,int height){var image=BitmapSource.Create(width,height,96,96,PixelFormats.Pbgra32,null,v,width*4);image.Freeze();return image;}
    static void Save(BitmapSource image,string path){var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(image));using(var f=File.Create(path))png.Save(f);}
    static BitmapSource RawFrame(BitmapSource atlas,MotionSpec spec,int frame){int cell=spec.frameMap==null?frame:spec.frameMap[frame];var crop=new CroppedBitmap(atlas,new Int32Rect(cell%spec.columns*spec.size,cell/spec.columns*spec.size,spec.size,spec.size));var converted=new FormatConvertedBitmap(crop,PixelFormats.Pbgra32,null,0);return Image(Pixels(converted),spec.size,spec.size);}
    static int Excess(byte[] v){int sum=0;for(int i=0;i<v.Length;i+=4){int a=v[i+3],b=v[i],g=v[i+1],r=v[i+2];if(a<24||b>r+10*a/255.0&&b>=g-2*a/255.0)continue;int cap=Math.Min(a,(72*r+28*b+(200*a+127)/255+50)/100);sum+=Math.Max(0,g-cap);}return sum;}
    public static int Run(string manifest,string output){
        Directory.CreateDirectory(output);PetSettings.UsePreviewProfile(Path.Combine(output,"isolated-state"));report=new LivingReport{scope="Ragdoll-only green-screen spill projection, stable premultiplied color, unchanged alpha/anatomy and natural eye/coat protection"};
        Check("绿色反色被移除，但透明度红蓝通道和位置不变",delegate{byte[] v={100,190,180,255,50,95,90,128};var before=(byte[])v.Clone();GreenSpill.Row(v);Assert(v[1]<before[1]-20&&v[5]<before[5]-10,"绿色反色未去除");for(int i=0;i<v.Length;i++)if(i%4!=1)Assert(v[i]==before[i],"去绿改变了透明度或其他颜色通道");});
        Check("蓝眼睛和原有奶白棕色保持原值",delegate{byte[] v={160,110,60,255,218,224,230,255,100,130,160,255};var before=(byte[])v.Clone();GreenSpill.Row(v);Assert(v.SequenceEqual(before),"蓝眼睛或自然毛色被整体去色");});
        Check("半透明毛发按透明度比例处理，不产生暗边或轮廓变化",delegate{byte[] v={100,190,180,255,50,95,90,128};GreenSpill.Row(v);Assert(Math.Abs(v[1]/2.0-v[5])<=1&&v[3]==255&&v[7]==128,"半透明边缘和实色毛发使用不同色相");});
        Check("重复导入或处理不会累积变色",delegate{var random=new Random(17);byte[] v=new byte[64*64*4];for(int i=0;i<v.Length;i+=4){int a=random.Next(256);v[i+3]=(byte)a;for(int j=0;j<3;j++)v[i+j]=(byte)random.Next(a+1);}GreenSpill.Row(v);var once=(byte[])v.Clone();GreenSpill.Row(v);Assert(v.SequenceEqual(once),"重复导入累积色差");});
        Check("原图与去绿图集有独立缓存，普通素材不会被误处理",delegate{string path=Path.Combine(output,"green-fixture.png");Save(Image(new byte[]{100,190,180,255},1,1),path);var cache=new MotionAtlasCache(1024);long version;var raw=cache.Get(path,1,1,out version);var fixedImage=cache.Get(path,1,1,out version,null,true);Assert(Pixels(raw)[1]==190&&Pixels(fixedImage)[1]<170&&cache.DecodeCount==2,"缓存混用了处理模式");Assert(Pixels(cache.Get(path,1,1,out version))[1]==190,"普通图集被覆盖");});
        var app=new Application();var skin=PetSkin.Load(manifest);
        Check("真实左右走路去掉绿色残留，同时每个像素轮廓完全保留",delegate{long before=0,after=0;var visual=new DrawingVisual();using(var d=visual.RenderOpen()){d.DrawRectangle(new SolidColorBrush(Color.FromRgb(243,242,238)),null,new Rect(0,0,1000,780));int row=0;foreach(string id in new[]{"walk-left","walk-right"}){var clip=skin.Motions.Clips[id];BitmapSource atlas;using(var f=File.OpenRead(Path.Combine(Path.GetDirectoryName(manifest),clip.Spec.sheet)))atlas=new PngBitmapDecoder(f,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnLoad).Frames[0];for(int n=0;n<3;n++){int at=clip.Spec.gaitEntryAt+16+n*20;var original=RawFrame(atlas,clip.Spec,at);var corrected=clip.Frames[at];byte[] x=Pixels(original),y=Pixels(corrected);before+=Excess(x);after+=Excess(y);for(int i=0;i<x.Length;i++)if(i%4!=1)Assert(x[i]==y[i],"去绿修改了身体轮廓或红蓝毛色");if(n==2){d.DrawImage(original,new Rect(50,row*380+35,360,360));d.DrawImage(corrected,new Rect(550,row*380+35,360,360));}}row++;}d.DrawText(new FormattedText("原始素材",System.Globalization.CultureInfo.GetCultureInfo("zh-CN"),FlowDirection.LeftToRight,new Typeface("Microsoft YaHei"),20,Brushes.DimGray),new Point(50,5));d.DrawText(new FormattedText("同帧去绿 · 轮廓不变",System.Globalization.CultureInfo.GetCultureInfo("zh-CN"),FlowDirection.LeftToRight,new Typeface("Microsoft YaHei"),20,Brushes.DimGray),new Point(550,5));}Assert(before>200&&after==0,"真实走路反色未清除");var picture=new RenderTargetBitmap(1000,780,96,96,PixelFormats.Pbgra32);picture.Render(visual);Save(picture,Path.Combine(output,"walk-before-after.png"));File.WriteAllText(Path.Combine(output,"color-metrics.json"),"{\"green_excess_before\":"+before+",\"green_excess_after\":"+after+",\"alpha_red_blue_identical\":true}");});
        using(var f=File.Create(Path.Combine(output,"color-test-report.json")))new DataContractJsonSerializer(typeof(LivingReport)).WriteObject(f,report);return report.failed==0?0:1;
    }
    public static int PreviewWalk(string manifest,string output){
        Directory.CreateDirectory(output);Directory.CreateDirectory(Path.Combine(output,"frames"));PetSettings.UsePreviewProfile(Path.Combine(output,"isolated-state"));var app=new Application();var c=ToyInteractionTests.Create(manifest);var area=SystemParameters.WorkArea;c.Pet.Left=area.Left+(area.Width-c.Pet.Width)/2;c.Pet.Top=area.Top+(area.Height-c.Pet.Height)/2;double originX=c.Pet.Left,originY=c.Pet.Top;int frame=0;
        foreach(string id in new[]{"walk-left","walk-right"}){var clip=c.Skin.Motions.Clips[id];var player=c.Skin.Motion;player.StartLivingWalk(clip,clip.Seconds);player.Tick(clip.Spec.standEntryAt/clip.Spec.fps);while(!player.AtStandingExit){for(int sub=0;sub<2;sub++){player.Tick(1.0/24);c.Pet.MoveForPlay(1.0/24);}var visual=new DrawingVisual();using(var d=visual.RenderOpen()){d.DrawRectangle(new SolidColorBrush(Color.FromRgb(243,242,238)),null,new Rect(0,0,840,520));var r=c.Pet.Surface.PetRect;r.Offset(180+c.Pet.Left-originX,30+c.Pet.Top-originY);c.Skin.Draw(d,0,frame/12.0,r);d.DrawText(new FormattedText("软件动作模拟 · 去绿后的真实步态与位移",System.Globalization.CultureInfo.GetCultureInfo("zh-CN"),FlowDirection.LeftToRight,new Typeface("Microsoft YaHei"),16,Brushes.DimGray),new Point(24,24));}var picture=new RenderTargetBitmap(840,520,96,96,PixelFormats.Pbgra32);picture.Render(visual);Save(picture,Path.Combine(output,"frames","frame-"+(frame++).ToString("D5")+".png"));}player.ContinueStanding(1);player.Cancel();for(int i=0;i<1000&&player.Active;i++)player.Tick(.02);}
        c.Pet.Close();c.Dispose();return 0;
    }
}
}
