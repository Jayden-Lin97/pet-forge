using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MaohaiziPet {
[DataContract] public sealed class LivingReport {
 [DataMember]public int passed,failed;
 [DataMember]public double mean_render_ms;
 [DataMember]public string scope="Continuous 2-D character rig; not a 3-D reconstruction or walking cycle";
 [DataMember]public List<TestResult> tests=new List<TestResult>();
}
public static class LivingPetTests {
 static LivingReport report;
 static void Check(string name,Action test){var r=new TestResult{name=name};try{test();r.passed=true;report.passed++;}catch(Exception e){r.error=e.Message;report.failed++;}report.tests.Add(r);}
 static void Assert(bool value,string error){if(!value)throw new Exception(error);}
 static byte[] Read(BitmapSource b){var f=new FormatConvertedBitmap(b,PixelFormats.Pbgra32,null,0);byte[] p=new byte[256*256*4];f.CopyPixels(p,1024,0);return p;}
 static void Save(BitmapSource b,string p){var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(b));using(var f=File.Create(p))encoder.Save(f);}
 static BitmapSource Snapshot(LivingPet rig){var c=BitmapFrame.Create(rig.Render());var pixels=Read(c);return BitmapSource.Create(256,256,96,96,PixelFormats.Pbgra32,null,pixels,1024);}
 static void Advance(LivingPet r,double seconds,PetAction action,Point? gaze){for(int i=0;i<(int)Math.Ceiling(seconds*120);i++)r.Advance(1.0/120,action,PetMode.Companion,gaze);}
 public static int Run(string manifest,string output){
  Directory.CreateDirectory(output);report=new LivingReport();var skin=PetSkin.Load(manifest);var frames=new List<BitmapSource>();
  Check("角色保持同一纹理并启用连续驱动",delegate{Assert(skin.Living!=null&&skin.Frames.Length==1,"没有启用连续角色");});
  if(skin.Living==null)return 1;
  Check("无人操作时仍自主呼吸、观察与眨眼",delegate{var r=new LivingPet(skin.Frames[0],113,skin.Living.ModernTexture);double initial=r.Pose.Breath;Advance(r,30,PetAction.Idle,null);Assert(r.BlinkCount>=3&&Math.Abs(r.Pose.Breath-initial)>.05,"待机没有持续行为");Assert(Math.Abs(r.Pose.GazeX)>.01||Math.Abs(r.Pose.GazeY)>.01,"没有自主观察");});
  Check("眼睛确实闭合，非整图位移代替眨眼",delegate{
   var r=new LivingPet(skin.Frames[0],113,skin.Living.ModernTexture);var open=Snapshot(r);frames.Add(open);byte[] a=Read(open);
   for(int i=0;i<500;i++){r.Advance(.005,PetAction.Idle,PetMode.Companion,new Point(0,0));if(r.Pose.Blink>.985)break;}
   Assert(r.Pose.Blink>.985,"没有捕获闭眼姿态");var closed=Snapshot(r);frames.Add(closed);byte[] b=Read(closed);long difference=0;
   for(int y=(int)r.EyeLeft.Y-9;y<(int)r.EyeLeft.Y+9;y++)for(int x=(int)r.EyeLeft.X-12;x<(int)r.EyeRight.X+12;x++)for(int k=0;k<3;k++)difference+=Math.Abs(a[(y*256+x)*4+k]-b[(y*256+x)*4+k]);
   Assert(difference>2500,"眼部像素没有独立变化");Save(open,Path.Combine(output,"open.png"));Save(closed,Path.Combine(output,"blink.png"));
  });
  Check("胸口呼吸时脚底保持固定",delegate{var r=new LivingPet(skin.Frames[0],7,skin.Living.ModernTexture);var foot=new Point(130,237);double max=0;for(int i=0;i<600;i++){r.Advance(1.0/60,PetAction.Idle,PetMode.Companion,new Point(1,-.5));max=Math.Max(max,(r.MapToSource(foot)-foot).Length);}Assert(max<.001,"整只角色仍在漂浮");});
  Check("视线转向和摸头结束均平滑过渡",delegate{
   var r=new LivingPet(skin.Frames[0],3,skin.Living.ModernTexture);Advance(r,.8,PetAction.Idle,new Point(-1,0));double before=r.Pose.GazeX;r.Advance(1.0/60,PetAction.Idle,PetMode.Companion,new Point(1,0));Assert(Math.Abs(r.Pose.GazeX-before)<.15,"视线瞬间跳变");Advance(r,1.2,PetAction.Idle,new Point(1,0));Assert(r.Pose.GazeX>.6,"视线未跟随");
   Advance(r,.8,PetAction.Pet,new Point(0,0));Assert(r.Pose.Relax>.7,"摸头未放松");double y=r.Pose.HeadY;r.Advance(1.0/60,PetAction.Idle,PetMode.Companion,new Point(0,0));Assert(Math.Abs(r.Pose.HeadY-y)<.5,"松手瞬间换姿势");frames.Add(Snapshot(r));Advance(r,1.5,PetAction.Idle,null);Assert(r.Pose.Relax<.1,"没有恢复待机");
  });
  Check("连续驱动渲染有透明边缘且帧内容持续变化",delegate{
   var r=new LivingPet(skin.Frames[0],113,skin.Living.ModernTexture);byte[] previous=Read(r.Render());int changed=0;var watch=Stopwatch.StartNew();for(int i=0;i<90;i++){r.Advance(1.0/30,PetAction.Idle,PetMode.Companion,new Point(Math.Sin(i*.07),-.2));var b=r.Render();if(i%15==0){byte[] current=Read(b);if(!current.SequenceEqual(previous))changed++;previous=current;}}
   watch.Stop();report.mean_render_ms=watch.Elapsed.TotalMilliseconds/90;Assert(changed>=4,"连续帧没有实际变化");Assert(previous.Where((p,i)=>i%4==3&&p==0).Count()>2000,"透明背景丢失");
  });
  Check("进入生成动作先缓慢归位，退出后恢复微动",delegate{var r=new LivingPet(skin.Frames[0],3,skin.Living.ModernTexture);Advance(r,1,PetAction.Idle,new Point(1,-.5));double x=r.Pose.HeadX;r.Advance(1.0/60,PetAction.Idle,PetMode.Companion,null,null,true);Assert(Math.Abs(r.Pose.HeadX-x)<.3,"第一帧瞬间归位");for(int i=0;i<59;i++)r.Advance(1.0/60,PetAction.Idle,PetMode.Companion,null,null,true);Assert(Math.Abs(r.Pose.HeadX)<.04&&Math.Abs(r.Pose.HeadY)<.04&&r.Pose.Neutral>.99,"没有在共用姿态中归位");double before=r.Pose.HeadX;r.Advance(1.0/60,PetAction.Idle,PetMode.Companion,new Point(1,0),null,false);Assert(Math.Abs(r.Pose.HeadX-before)<.15,"退出动作瞬间摇头");Advance(r,1.5,PetAction.Idle,new Point(1,0));Assert(r.Pose.Neutral<.01&&r.Pose.HeadX>2,"没有恢复自主微动");});
  Check("导出导入保留连续驱动及原始角色纹理",delegate{string path=Path.Combine(output,"living-pet.zip");skin.Export(path);var restored=PetSkin.Load(PetSkin.Import(path,Path.Combine(output,"imported")));Assert(restored.Living!=null&&restored.Frames.Length==1,"连续驱动配置丢失");byte[] a=Read(skin.Frames[0]),b=Read(restored.Frames[0]);double error=a.Zip(b,(x,y)=>Math.Abs(x-y)).Average();Assert(error<1.1,"导出烘焙了瞬时表情，原始纹理被改变");});
  var left=new LivingPet(skin.Frames[0],9,skin.Living.ModernTexture);Advance(left,1,PetAction.Idle,new Point(-1,.2));frames.Add(Snapshot(left));var right=new LivingPet(skin.Frames[0],9,skin.Living.ModernTexture);Advance(right,1,PetAction.Idle,new Point(1,-.2));frames.Add(Snapshot(right));
  var v=new DrawingVisual();using(var d=v.RenderOpen()){d.DrawRectangle(new SolidColorBrush(Color.FromRgb(242,240,236)),null,new Rect(0,0,frames.Count*256,285));for(int i=0;i<frames.Count;i++){d.DrawImage(frames[i],new Rect(i*256,0,256,256));d.DrawText(new FormattedText(new[]{"待机","自然闭眼","摸头过渡","看左","看右"}[i],System.Globalization.CultureInfo.GetCultureInfo("zh-CN"),FlowDirection.LeftToRight,new Typeface("Microsoft YaHei"),13,Brushes.DimGray),new Point(i*256+100,263));}}
  var sheet=new RenderTargetBitmap(frames.Count*256,285,96,96,PixelFormats.Pbgra32);sheet.Render(v);Save(sheet,Path.Combine(output,"continuous-rig-review.png"));
  using(var f=File.Create(Path.Combine(output,"living-test-report.json")))new DataContractJsonSerializer(typeof(LivingReport)).WriteObject(f,report);
  return report.failed==0?0:1;
 }
}
}
