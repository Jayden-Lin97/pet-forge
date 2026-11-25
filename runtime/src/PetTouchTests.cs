using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MaohaiziPet {
public static class PetTouchTests {
    static LivingReport report;
    static void Assert(bool ok,string reason){if(!ok)throw new Exception(reason);}
    static void Check(string name,Action run){var item=new TestResult{name=name};try{run();item.passed=true;report.passed++;}catch(Exception e){item.error=e.Message;report.failed++;}report.tests.Add(item);}
    static byte[] Pixels(BitmapSource image){var pixels=new byte[image.PixelWidth*image.PixelHeight*4];image.CopyPixels(pixels,image.PixelWidth*4,0);return pixels;}
    static void Save(BitmapSource image,string path){var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));using(var f=File.Create(path))encoder.Save(f);}
    static void Step(CompanionMind mind,LivingPet rig,double seconds){for(int i=0;i<(int)Math.Ceiling(seconds*60);i++){mind.Tick(1.0/60,PetMode.Companion,0);if(rig!=null)rig.Advance(1.0/60,PetAction.Idle,PetMode.Companion,new Point(0,0),mind.Signals);}}
    public static int Run(string manifest,string output){
        Directory.CreateDirectory(output);report=new LivingReport{scope="Body-region input, non-repeating touch reactions, hand cursor, alpha hit testing, quiet mode and actual continuous rendering"};
        var nose=new Point(140,113);
        Check("猫咪经过静止鼠标时不会被误当作主动抚摸",delegate{var activity=new PetPointerActivity();var point=new Point(1200,700);Assert(activity.Observe(point,0)&&activity.Observe(point,.5),"首次放到身体上不能短暂悬停");for(int i=2;i<60;i++)Assert(!activity.Observe(point,i),"静止鼠标持续触发活动窗口");Assert(activity.Observe(new Point(1204,700),61),"实际移动后没有恢复触摸");var tracker=new PettingTracker();int count=0;for(int i=0;i<600;i++)if(tracker.Observe(i<60?PetTouchZone.Head:i<180?PetTouchZone.Chin:PetTouchZone.Back,point,i/60.0))count++;Assert(count==1,"身体移动带来的分区变化反复触发");});
        Check("头顶、脸颊、鼻尖、下巴与背部能分开识别",delegate{Assert(PetTouchMap.At(new Point(141,70),nose)==PetTouchZone.Head,"头顶错误");Assert(PetTouchMap.At(new Point(111,112),nose)==PetTouchZone.CheekLeft,"左脸错误");Assert(PetTouchMap.At(new Point(169,112),nose)==PetTouchZone.CheekRight,"右脸错误");Assert(PetTouchMap.At(nose,nose)==PetTouchZone.Nose,"鼻尖错误");Assert(PetTouchMap.At(new Point(140,133),nose)==PetTouchZone.Chin,"下巴错误");Assert(PetTouchMap.At(new Point(62,177),nose)==PetTouchZone.Back,"背部错误");});
        Check("左右爪分别识别，睡姿鼻尖不会被当作爪子",delegate{Assert(PetTouchMap.At(new Point(106,219),nose)==PetTouchZone.PawLeft,"左爪错误");Assert(PetTouchMap.At(new Point(160,219),nose)==PetTouchZone.PawRight,"右爪错误");Assert(PetTouchMap.At(new Point(94,214),new Point(94,214))==PetTouchZone.Nose,"睡姿面部错位");});
        Check("静止悬停只回应一次，不在原地不断重播",delegate{var tracker=new PettingTracker();int contacts=0;for(int i=0;i<3600;i++)if(tracker.Observe(PetTouchZone.Nose,new Point(140,113),i/60.0))contacts++;Assert(contacts==1,"静止指针反复触发："+contacts);});
        Check("猫咪姿态在静止指针下移动不会误触其他部位",delegate{var tracker=new PettingTracker();var p=new Point(140,130);tracker.Suppress(p);for(int i=0;i<600;i++){if(i%60==0)tracker.Suspend();Assert(!tracker.Observe(i%100<50?PetTouchZone.Chin:PetTouchZone.Back,p,i/60.0),"未移动鼠标却触发了其他部位");}bool responded=false;for(int i=0;i<60;i++)responded|=tracker.Observe(PetTouchZone.Back,new Point(155,145),10+i/60.0);Assert(responded,"真正移动鼠标后没有恢复互动");});
        Check("不用按键，缓慢来回抚摸可以持续送入反馈",delegate{var tracker=new PettingTracker();int contacts=0;for(int i=0;i<300;i++)if(tracker.Observe(PetTouchZone.Head,new Point(140+Math.Sin(i*.1)*12,70),i/60.0))contacts++;Assert(contacts>10&&contacts<45,"慢速抚摸没有被识别或触发过密");});
        Check("快速掠过身体不会立即打断自主动作",delegate{var tracker=new PettingTracker();int contacts=0;for(int i=0;i<7;i++)if(tracker.Observe(PetTouchZone.Head,new Point(120+i*8,70),i/100.0))contacts++;Assert(contacts==0,"掠过被当作抚摸");});
        Check("持续摸同一部位不重置动作，不刷好感或文字",delegate{var m=new CompanionMind(0,Personality.Clingy);for(int i=0;i<330;i++){if(i%9==0)m.Contact(PetTouchZone.Chin,PetMode.Companion,true,0);m.Tick(1.0/60,PetMode.Companion,0);}Assert(m.Touch.Active&&m.Signals.TouchChin>.95,"连续抚摸没有保持");Assert(m.Warmth<=.5,"短时间刷入熟悉感");Assert(m.Line=="","持续抚摸反复刷新气泡");});
        Check("碰鼻尖有冷却，不因连点持续抽动",delegate{var m=new CompanionMind(0,Personality.Clingy);Assert(m.Contact(PetTouchZone.Nose,PetMode.Companion,false,0),"首碰被拒绝");Step(m,null,.65);Assert(!m.Contact(PetTouchZone.Nose,PetMode.Companion,false,0),"连点重启反应");Step(m,null,.9);Assert(m.Signals.TouchNose<.1,"鼻尖反应一直不收回");});
        Check("勿扰拒绝各部位触摸并清除反馈",delegate{var m=new CompanionMind(0,Personality.Clingy);m.Contact(PetTouchZone.Chin,PetMode.Companion,false,0);Step(m,null,.5);m.Tick(.02,PetMode.Quiet,0);foreach(PetTouchZone zone in Enum.GetValues(typeof(PetTouchZone)))Assert(!m.Contact(zone,PetMode.Quiet,false,0),"勿扰接受抚摸");Assert(!m.Touch.Active&&m.Signals.TouchChin==0&&m.Line=="","勿扰未清除");});
        Check("关闭暖心话仍保留身体回应",delegate{var m=new CompanionMind(0,Personality.Clingy){WarmWords=false};m.Contact(PetTouchZone.Back,PetMode.Companion,false,1);Step(m,null,.6);Assert(m.Line==""&&m.Signals.TouchBack>.9,"关闭文字影响触摸动作");});
        Check("离开后回来直接摸猫，欢迎动作不会覆盖触摸反馈",delegate{var m=new CompanionMind(0,Personality.Clingy);m.Tick(65,PetMode.Companion,65);m.Contact(PetTouchZone.Chin,PetMode.Companion,false,0);m.Tick(.6,PetMode.Companion,0);Assert(m.Greetings==1&&m.Touch.Active&&m.Signals.TouchChin>.9&&m.Gesture==CompanionGesture.None,"欢迎动作抢占了正在进行的抚摸");});
        var skin=PetSkin.Load(manifest);var frames=new List<BitmapSource>();var labels=new List<string>();
        Check("各部位触摸真正改变表情和姿势，保持同一猫咪纹理",delegate{
            byte[] neutral=null;
            foreach(var zone in new[]{PetTouchZone.None,PetTouchZone.Head,PetTouchZone.Chin,PetTouchZone.CheekLeft,PetTouchZone.CheekRight,PetTouchZone.Back,PetTouchZone.Nose,PetTouchZone.PawLeft,PetTouchZone.PawRight}){
                var m=new CompanionMind(0,Personality.Clingy){WarmWords=false};var rig=new LivingPet(skin.Frames[0],113,skin.Living.ModernTexture);if(zone!=PetTouchZone.None)m.Contact(zone,PetMode.Companion,false,1);Step(m,rig,zone==PetTouchZone.Nose?.55:.85);
                byte[] pixels=Pixels(rig.Render());if(zone==PetTouchZone.None)neutral=pixels;else Assert(pixels.Zip(neutral,(a,b)=>Math.Abs(a-b)).Average()>.1,"没有实际像素变化："+zone);
                var image=BitmapSource.Create(256,256,96,96,PixelFormats.Pbgra32,null,pixels,1024);frames.Add(image);labels.Add(zone==PetTouchZone.None?"安静待机":PetTouchMap.Label(zone));Save(image,Path.Combine(output,zone+".png"));
                if(zone==PetTouchZone.Chin)Assert(rig.Pose.HeadY<-4&&rig.Pose.BlinkLeft>.85,"挠下巴没有抬头眯眼");
                if(zone==PetTouchZone.CheekLeft)Assert(rig.Pose.HeadX<-2&&rig.Pose.Tilt<-.02,"左脸没有向手靠近");
                if(zone==PetTouchZone.CheekRight)Assert(rig.Pose.HeadX>2&&rig.Pose.Tilt>.02,"右脸没有向手靠近");
                if(zone==PetTouchZone.PawLeft)Assert(rig.Pose.PawLeft>5&&rig.Pose.PawRight<.1,"碰左爪却动错爪");
                if(zone==PetTouchZone.PawRight)Assert(rig.Pose.PawRight>5&&rig.Pose.PawLeft<.1,"碰右爪却动错爪");
                if(zone==PetTouchZone.Back)Assert(rig.Pose.BackRub>.8&&(rig.MapToSource(new Point(130,239))-new Point(130,239)).Length<.001,"摸背漂浮或没有放松");
            }
        });
        Check("快速切换部位与停手恢复连续，不瞬间换姿势",delegate{var m=new CompanionMind(0,Personality.Clingy);var rig=new LivingPet(skin.Frames[0],31,skin.Living.ModernTexture);var zones=new[]{PetTouchZone.Head,PetTouchZone.Chin,PetTouchZone.CheekLeft,PetTouchZone.Back,PetTouchZone.PawRight};double largest=0;for(int i=0;i<600;i++){if(i%23==0)m.Contact(zones[i/23%zones.Length],PetMode.Companion,true,1);double y=rig.Pose.HeadY;Step(m,rig,1.0/60);largest=Math.Max(largest,Math.Abs(rig.Pose.HeadY-y));}Assert(largest<.65,"切换时出现硬切");Step(m,rig,6);Assert(m.Signals.TouchHead<.001&&m.Signals.TouchChin<.001&&rig.Pose.PawRight<.05&&rig.Pose.BackRub<.05,"停手后没有归位");});
        Check("手形光标与透明命中区域一致，空白处仍穿透",delegate{using(var controller=new PetController(new Application())){controller.Skin=skin;controller.Settings.Size=180;var surface=new PetSurface(controller);surface.Measure(new Size(360,300));surface.Arrange(new Rect(0,0,360,300));Rect r=surface.PetRect;Assert(surface.Cursor==Cursors.Hand,"没有手形光标");Assert(surface.TouchAt(new Point(r.X+r.Width*140/256,r.Y+r.Height*113/256))==PetTouchZone.Nose,"实际界面没有绑定鼻尖分区");Assert(surface.TouchAt(new Point(r.X,r.Y))==PetTouchZone.None&&!surface.HitPet(new Point(2,2)),"透明空白拦截指针");}});
        if(skin.Motions.Clips.Values.Any(c=>c.HasHold))Check("已开放完整姿态的面部触摸位置随姿态移动",delegate{foreach(var clip in skin.Motions.Clips.Values.Where(c=>c.HasHold)){var player=new MotionPlayer();player.Start(clip,clip.Seconds);Assert((player.TouchNose-nose).Length<.001,"起手面部定位偏移");player.Tick(clip.Spec.holdAt/clip.Spec.fps+.01);var p=player.TouchNose;Assert((p-nose).Length>20&&PetTouchMap.At(p,p)==PetTouchZone.Nose,"姿态面部未跟随："+clip.Spec.id);}});
        else Check("未验收姿态不会进入触摸播放分支",delegate{Assert(!skin.Motions.Clips.ContainsKey("sit")&&!skin.Motions.Clips.ContainsKey("stand")&&!skin.Motions.Clips.ContainsKey("sleep"),"撤回的姿态仍在播放库");});
        Check("无效触摸输入不会进入动画状态",delegate{var t=new TouchFeedback();Assert(!t.Contact((PetTouchZone)999,false,0)&&!t.Contact(PetTouchZone.Head,false,double.NaN),"无效数据被接受");});
        var review=new DrawingVisual();using(var d=review.RenderOpen()){d.DrawRectangle(new SolidColorBrush(Color.FromRgb(245,243,236)),null,new Rect(0,0,768,870));for(int i=0;i<frames.Count;i++){d.DrawImage(frames[i],new Rect(i%3*256,i/3*290,256,256));d.DrawText(new FormattedText(labels[i],System.Globalization.CultureInfo.GetCultureInfo("zh-CN"),FlowDirection.LeftToRight,new Typeface("Microsoft YaHei"),14,Brushes.DimGray),new Point(i%3*256+90,i/3*290+263));}}var sheet=new RenderTargetBitmap(768,870,96,96,PixelFormats.Pbgra32);sheet.Render(review);Save(sheet,Path.Combine(output,"touch-review.png"));
        using(var f=File.Create(Path.Combine(output,"touch-test-report.json")))new DataContractJsonSerializer(typeof(LivingReport)).WriteObject(f,report);return report.failed==0?0:1;
    }
}
}
