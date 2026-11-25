using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml.Serialization;

namespace MaohaiziPet {
public static class CompanionTests {
    static LivingReport report;
    static void Assert(bool ok,string message){if(!ok)throw new Exception(message);}
    static void Check(string name,Action run){var item=new TestResult{name=name};try{run();item.passed=true;report.passed++;}catch(Exception e){item.error=e.Message;report.failed++;}report.tests.Add(item);}
    static CompanionMind Mind(){return new CompanionMind(0,Personality.Clingy);}
    static void Step(CompanionMind m,double seconds,double idle){for(int i=0;i<(int)Math.Round(seconds*60);i++)m.Tick(1.0/60,PetMode.Companion,idle);}
    static byte[] Pixels(BitmapSource b){byte[] pixels=new byte[256*256*4];b.CopyPixels(pixels,1024,0);return pixels;}
    static BitmapSource Copy(BitmapSource b){return BitmapSource.Create(256,256,96,96,PixelFormats.Pbgra32,null,Pixels(b),1024);}
    static void Save(BitmapSource b,string path){var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(b));using(var f=File.Create(path))png.Save(f);}
    public static int Run(string manifest,string output){
        Directory.CreateDirectory(output);report=new LivingReport();report.scope="Local companionship timing, user controls, persistence, continuous gesture rendering";
        Check("首次运行不制造离开后的招呼",delegate{var m=Mind();Step(m,10,0);Assert(m.Greetings==0&&m.Line=="","误报回归");});
        Check("七种主动互动均有连续身体信号并自然结束",delegate{
            foreach(CompanionGesture g in Enum.GetValues(typeof(CompanionGesture))){if(g==CompanionGesture.None)continue;var m=Mind();Assert(m.Trigger(g,PetMode.Companion),"互动未接受");Step(m,.9,0);var s=m.Signals;double amount=s.Nuzzle+s.Curiosity+s.Wink+s.PawRight+s.Play+s.Bow+s.SlowBlink;Assert(amount>.1,g+"无身体回应");Step(m,12,0);Assert(m.Gesture==CompanionGesture.None&&m.Signals.Play==0,"互动没有退出");}
        });
        Check("连续摸头才进入轻踩奶，停手平滑减弱",delegate{var m=Mind();for(int i=0;i<240;i++){m.Stroke(PetMode.Companion);m.Tick(1.0/60,PetMode.Companion,0);}Assert(m.Signals.Knead>.9,"长期抚摸无踩奶信号");Step(m,7,0);Assert(m.Signals.Knead==0,"停手仍在踩奶");});
        Check("勿扰拒绝互动并移除文字和玩具",delegate{var m=Mind();m.Trigger(CompanionGesture.Play,PetMode.Companion);Step(m,.8,0);m.Tick(.01,PetMode.Quiet,0);Assert(!m.Trigger(CompanionGesture.Boop,PetMode.Quiet)&&m.Line==""&&m.Signals.Play==0&&m.Signals.Knead==0,"勿扰仍打扰");});
        Check("关闭暖心话后身体互动继续",delegate{var m=Mind();m.WarmWords=false;m.Trigger(CompanionGesture.Comfort,PetMode.Companion);Step(m,1,0);Assert(m.Line==""&&m.Signals.Nuzzle>.5,"关闭文字同时关闭了动作");});
        Check("快速重复点击不重启动画且不会刷好感",delegate{var m=Mind();m.Trigger(CompanionGesture.Boop,PetMode.Companion);for(int i=0;i<20;i++){m.Tick(.01,PetMode.Companion,0);Assert(!m.Trigger(CompanionGesture.Boop,PetMode.Companion),"动作被重复重启");}Assert(m.Warmth==1,"好感被重复刷入");});
        Check("离开一分钟再回来仅招呼一次且有冷却",delegate{var m=Mind();Step(m,70,70);m.Tick(.1,PetMode.Companion,0);Assert(m.Greetings==1&&m.Line.Contains("回来"),"未欢迎回来");Step(m,5,0);Step(m,70,70);m.Tick(.1,PetMode.Companion,0);Assert(m.Greetings==1,"短时间重复欢迎");});
        Check("专注期间不主动招呼，计时到点只温和提醒一次",delegate{var m=Mind();m.StartFocus(1500);m.Tick(80,PetMode.Companion,80);m.Tick(.1,PetMode.Companion,0);Assert(m.IsFocusing&&m.Greetings==0&&m.Gesture==CompanionGesture.None,"专注被打扰");m.Tick(1420,PetMode.Companion,0);Assert(!m.IsFocusing&&m.Line.Contains("完成"),"未按实际时间完成");Step(m,8,0);Assert(m.Line=="","完成提示未消失");});
        Check("专注可取消且勿扰不显示完成提示",delegate{var m=Mind();m.StartFocus(5);m.CancelFocus();Step(m,6,0);Assert(m.Line==""&&!m.IsFocusing,"取消后仍提醒");m.StartFocus(5);m.Tick(6,PetMode.Quiet,0);Assert(m.Line==""&&!m.IsFocusing,"勿扰响起提醒");});
        Check("自主亲近保持无字且不频繁催促",delegate{var m=Mind();m.Tick(100,PetMode.Companion,0);Assert(m.Gesture==CompanionGesture.Greet&&m.Line=="","主动行为出现打扰文案");Step(m,5,0);Assert(m.Gesture==CompanionGesture.None,"主动行为持续占用");});
        Check("熟悉感有界，离开不扣分并可本地保存",delegate{
            var m=new CompanionMind(99.8,Personality.Shy);m.Trigger(CompanionGesture.Nuzzle,PetMode.Companion);Assert(m.Warmth==100&&m.Dirty,"熟悉感上界错误");m.Tick(86400,PetMode.Quiet,86400);Assert(m.Warmth==100,"离开被扣分");
            var settings=new PetSettings{Familiarity=m.Warmth,WarmWords=false};using(var stream=new MemoryStream()){var xml=new XmlSerializer(typeof(PetSettings));xml.Serialize(stream,settings);stream.Position=0;var restored=(PetSettings)xml.Deserialize(stream);Assert(restored.Familiarity==100&&!restored.WarmWords,"持久化丢失");}m.MarkSaved();Assert(!m.Dirty,"已保存仍标脏");
            Assert(new CompanionMind(double.NaN,Personality.Aloof).Warmth==0,"无效保存数据未清理");
        });
        var skin=PetSkin.Load(manifest);var frames=new List<BitmapSource>();var labels=new List<string>();
        Check("贴贴、碰鼻、抬爪与轻点头真正改变局部像素",delegate{
            foreach(var g in new[]{CompanionGesture.None,CompanionGesture.Nuzzle,CompanionGesture.Boop,CompanionGesture.Paw,CompanionGesture.Treat}){
                var m=Mind();var rig=new LivingPet(skin.Frames[0],113,skin.Living.ModernTexture);if(g!=CompanionGesture.None)m.Trigger(g,PetMode.Companion);
                for(int i=0;i<60;i++){m.Tick(1.0/60,PetMode.Companion,0);rig.Advance(1.0/60,PetAction.Idle,PetMode.Companion,new Point(0,0),m.Signals);}
                var frame=Copy(rig.Render());frames.Add(frame);labels.Add(g==CompanionGesture.None?"安静待机":g==CompanionGesture.Nuzzle?"贴贴眨眼":g==CompanionGesture.Boop?"碰鼻尖":g==CompanionGesture.Paw?"轻抬爪":"收到小零食");
                if(g!=CompanionGesture.None){double difference=Pixels(frame).Zip(Pixels(frames[0]),(a,b)=>Math.Abs(a-b)).Average();Assert(difference>.3,g+"没有实际渲染变化");}
                if(g==CompanionGesture.Nuzzle)Assert(rig.Pose.BlinkLeft-rig.Pose.BlinkRight>.25,"贴贴没有单眼变化");
                if(g==CompanionGesture.Paw)Assert(rig.Pose.PawRight>3,"碰爪没有局部抬起");
                Save(frame,Path.Combine(output,g+".png"));
            }
        });
        Check("快速切换互动及结束无硬切、无无效像素",delegate{
            var m=Mind();var rig=new LivingPet(skin.Frames[0],113,skin.Living.ModernTexture);var all=(CompanionGesture[])Enum.GetValues(typeof(CompanionGesture));double maxHeadStep=0;
            for(int i=0;i<1200;i++){if(i%41==0)m.Trigger(all[1+i/41%(all.Length-1)],PetMode.Companion);if(i%3==0)m.Stroke(PetMode.Companion);double y=rig.Pose.HeadY;m.Tick(1.0/60,PetMode.Companion,0);rig.Advance(1.0/60,PetAction.Idle,PetMode.Companion,new Point(Math.Sin(i*.07),0),m.Signals);maxHeadStep=Math.Max(maxHeadStep,Math.Abs(rig.Pose.HeadY-y));if(i%30==0){var b=Pixels(rig.Render());for(int p=0;p<b.Length;p+=4)Assert(b[p]<=b[p+3]&&b[p+1]<=b[p+3]&&b[p+2]<=b[p+3],"透明像素出现色边");}}
            Assert(maxHeadStep<.5,"头部动作硬切");m.Silence();for(int i=0;i<180;i++){m.Tick(1.0/60,PetMode.Companion,0);rig.Advance(1.0/60,PetAction.Idle,PetMode.Companion,null,m.Signals);}Assert(rig.Pose.PawLeft<.1&&rig.Pose.PawRight<.1,"松手后爪子未归位");
        });
        if(frames.Count>0){var visual=new DrawingVisual();using(var d=visual.RenderOpen()){d.DrawRectangle(new SolidColorBrush(Color.FromRgb(245,243,236)),null,new Rect(0,0,frames.Count*256,288));for(int i=0;i<frames.Count;i++){d.DrawImage(frames[i],new Rect(i*256,0,256,256));d.DrawText(new FormattedText(labels[i],System.Globalization.CultureInfo.GetCultureInfo("zh-CN"),FlowDirection.LeftToRight,new Typeface("Microsoft YaHei"),13,Brushes.DimGray),new Point(i*256+80,266));}}var sheet=new RenderTargetBitmap(frames.Count*256,288,96,96,PixelFormats.Pbgra32);sheet.Render(visual);Save(sheet,Path.Combine(output,"companionship-review.png"));}
        using(var f=File.Create(Path.Combine(output,"companion-test-report.json")))new DataContractJsonSerializer(typeof(LivingReport)).WriteObject(f,report);
        return report.failed==0?0:1;
    }
}
}
