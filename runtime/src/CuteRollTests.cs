using System;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MaohaiziPet {
public static class CuteRollTests {
    static LivingReport report;
    static void Assert(bool ok,string text){if(!ok)throw new Exception(text);}
    static void Check(string name,Action action){var t=new TestResult{name=name};try{action();t.passed=true;report.passed++;}catch(Exception e){t.error=e.ToString();report.failed++;}report.tests.Add(t);}
    static bool BlueEye(BitmapSource f,double x,double y){byte[] v=new byte[f.PixelWidth*f.PixelHeight*4];f.CopyPixels(v,f.PixelWidth*4,0);for(int yy=(int)y-4;yy<=(int)y+4;yy++)for(int xx=(int)x-4;xx<=(int)x+4;xx++){int p=(yy*f.PixelWidth+xx)*4;if(v[p+3]>180&&v[p]>v[p+2]+3&&v[p]>=v[p+1])return true;}return false;}
    public static int Run(string manifest,string output){
        Directory.CreateDirectory(output);PetSettings.UsePreviewProfile(Path.Combine(output,"isolated-"+Guid.NewGuid().ToString("N")));report=new LivingReport{scope="Cute side-cuddle asset, visible face, brief dwell and forward return; visual appeal reviewed separately"};var app=new Application();var skin=PetSkin.Load(manifest);var clip=skin.Motions.Clips["belly"];
        Check("撒娇停留姿势的两只蓝眼睛可见，头脸触摸跟随真实鼻尖",delegate{Assert(clip.Spec.reviewed&&clip.Spec.physicalTransitionReviewed&&clip.Spec.transitionSource=="continuous-video","动作未审");var s=clip.Spec;Assert(s.eyeRightX>s.eyeLeftX&&s.eyeLeftY>0&&s.eyeRightY>0&&BlueEye(clip.Frames[s.holdAt],s.eyeLeftX,s.eyeLeftY)&&BlueEye(clip.Frames[s.holdAt],s.eyeRightX,s.eyeRightY),"脸或蓝眼睛不可见");Assert(s.noseX!=null&&s.noseY!=null,"侧躺时头脸触摸仍留在原位置");double nx=s.noseX[s.holdAt],ny=s.noseY[s.holdAt];Assert(Math.Abs(nx-(s.eyeLeftX+s.eyeRightX)/2)<22&&Math.Abs(ny-(s.eyeLeftY+s.eyeRightY)/2-20)<18,"鼻尖跟踪偏离脸部");});
        Check("默认撒娇只短暂停留，其他常驻姿态保留正常休息时长",delegate{var policy=BehaviorPolicy.Load(Path.Combine(Path.GetDirectoryName(manifest),"behaviors.json"));Assert(!policy.UsedFallback,"配置回退");var random=new Random(39);for(int i=0;i<100;i++){double cute=policy.Dwell(CatHabit.BellyRoll,random);Assert(cute>=1&&cute<=2.3,"撒娇僵躺过久");Assert(policy.Dwell(CatHabit.SitUp,random)>=24,"坐着陪伴被缩短");}Assert(clip.Spec.holdSeconds<=2.3&&clip.Seconds<=9,"动作总时长过长");});
        Check("桌面入口叫撒个娇，播放完整原生侧躺过程",delegate{Assert(DesktopPetPocket.Label(DesktopPetAction.Belly)=="撒个娇","入口仍是翻肚皮");var p=new MotionPlayer();p.ConfigureLibrary(skin.Motions);p.StartResidence(clip,1.8);int previous=0;for(int i=0;i<1200&&p.Active;i++){p.Tick(.01);if(p.Active){Assert(p.FrameIndex>=previous,"完整动作反复倒放");previous=p.FrameIndex;}}Assert(!p.Active&&previous>=clip.Frames.Length-2,"没有走完侧躺和返回");});
        Check("撒娇停留被打断时沿原生返回过程前进并收尾",delegate{var p=new MotionPlayer();p.ConfigureLibrary(skin.Motions);p.StartResidence(clip,1.8);p.Tick(clip.Spec.holdAt/clip.Spec.fps+.4);Assert(p.HoldingPose,"未进入侧躺");p.Cancel();int previous=p.FrameIndex;for(int i=0;i<1200&&p.Active;i++){p.Tick(.01);if(p.Active){Assert(p.FrameIndex>=previous,"打断后倒放侧躺进入");previous=p.FrameIndex;}}Assert(!p.Active&&previous>=clip.Frames.Length-2,"撒娇没有自然返回");});
        using(var f=File.Create(Path.Combine(output,"cute-test-report.json")))new DataContractJsonSerializer(typeof(LivingReport)).WriteObject(f,report);return report.failed==0?0:1;
    }
    public static int Preview(string manifest,string output){
        Directory.CreateDirectory(output);Directory.CreateDirectory(Path.Combine(output,"frames"));PetSettings.UsePreviewProfile(Path.Combine(output,"isolated-state"));var app=new Application();var c=ToyInteractionTests.Create(manifest);var clip=c.Skin.Motions.Clips["belly"];c.Skin.Motion.StartResidence(clip,1.8);
        for(int i=0;i<120;i++){c.Skin.Motion.Tick(1.0/12);ToyInteractionTests.Render(c,Path.Combine(output,"frames","frame-"+i.ToString("D5")+".png"),"侧躺撒娇 · 侧躺歪头看看你，再翻回来");}
        c.Pet.Close();c.Dispose();return 0;
    }
}
}
