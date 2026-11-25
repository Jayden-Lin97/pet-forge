using System;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MaohaiziPet {
public static class ToyInteractionTests {
    static LivingReport report;
    static void Assert(bool ok,string text){if(!ok)throw new Exception(text);}
    static void Check(string name,Action run){var t=new TestResult{name=name};try{run();t.passed=true;report.passed++;}catch(Exception e){t.error=e.ToString();report.failed++;}report.tests.Add(t);}
    internal static Point Local(PetController c,Point p){var r=c.Pet.Surface.PetRect;return new Point(r.X+p.X*r.Width/256,r.Y+p.Y*r.Height/256);}
    internal static void Step(PetController c,double dt){
        c.Life.Tick(dt,!c.Hidden,c.Engine.Mode==PetMode.Quiet,new DateTime(2026,11,25));c.Companion.Tick(dt,c.Engine.Mode,0,true);
        c.UpdateNeeds(dt,12);c.UpdateWalkRoom();c.TickPlayBehavior(dt);
        if(c.PlayingOnDesktop)c.Habits.PauseForPlay(dt);else c.Habits.Tick(dt,c.Engine.Mode,c.Skin.Motion.Recovering,false);
        c.Habits.Apply(c.Companion.Signals);c.AdvanceMotions(dt);c.UpdateToyContact();c.Life.Apply(c.Companion.Signals,PetTouchZone.None);
        c.Skin.Living.Advance(dt,PetAction.Idle,c.Engine.Mode,null,c.Companion.Signals,c.Skin.Motion.Active);c.Pet.MoveForPlay(dt);
    }
    internal static void Pose(PetController c,CatHabit habit){
        c.SelectTool(PetTool.Hand);c.TickPlayBehavior(0);c.Hidden=true;c.Habits.Interrupt();c.Skin.Motion.Cancel();
        for(int i=0;i<3000&&c.Skin.Motion.Active;i++)c.AdvanceMotions(.05);
        c.Hidden=false;c.StartHabit(habit);c.AdvanceMotions(0);for(int i=0;i<1500&&!c.Skin.Motion.HoldingPose;i++)c.Skin.Motion.Tick(.01);
        Assert(c.Skin.Motion.HoldingPose,"无法进入测试姿势");
    }
    internal static PetController Create(string manifest){
        var c=new PetController(Application.Current);c.Settings.Mode=PetMode.Companion;c.Engine.SetMode(PetMode.Companion);c.Settings.Size=210;
        c.Skin=PetSkin.Load(manifest);c.ConfigureMotions();c.Pet=new PetWindow(c);c.Pet.Surface.Measure(new Size(c.Pet.Width,c.Pet.Height));c.Pet.Surface.Arrange(new Rect(0,0,c.Pet.Width,c.Pet.Height));return c;
    }
    internal static void Render(PetController c,string path,string label){
        c.Pet.Surface.Measure(new Size(c.Pet.Width,c.Pet.Height));c.Pet.Surface.Arrange(new Rect(0,0,c.Pet.Width,c.Pet.Height));c.Pet.Surface.InvalidateVisual();c.Pet.Surface.UpdateLayout();
        var surface=new RenderTargetBitmap((int)c.Pet.Width,(int)c.Pet.Height,96,96,PixelFormats.Pbgra32);surface.Render(c.Pet.Surface);var visual=new DrawingVisual();
        using(var d=visual.RenderOpen()){d.DrawRectangle(new SolidColorBrush(Color.FromRgb(243,242,238)),null,new Rect(0,0,840,480));d.DrawImage(surface,new Rect(180,52,c.Pet.Width,c.Pet.Height));d.DrawText(new FormattedText("软件交互模拟 · "+label,System.Globalization.CultureInfo.GetCultureInfo("zh-CN"),FlowDirection.LeftToRight,new Typeface("Microsoft YaHei"),16,Brushes.DimGray),new Point(24,24));}
        var image=new RenderTargetBitmap(840,480,96,96,PixelFormats.Pbgra32);image.Render(visual);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(image));using(var file=File.Create(path))png.Save(file);
    }
    public static int Preview(string manifest,string output){
        Directory.CreateDirectory(output);Directory.CreateDirectory(Path.Combine(output,"frames"));PetSettings.UsePreviewProfile(Path.Combine(output,"isolated-state"));var app=new Application();var c=Create(manifest);Pose(c,CatHabit.SitUp);c.SelectTool(PetTool.Feather);
        int swats=0,contacts=0;for(int i=0;i<288;i++){
            double t=i/12.0;if(i==144){swats=c.Life.Play.Swats;contacts=c.Life.Play.Contacts;c.SelectTool(PetTool.Yarn);}
            for(int sub=0;sub<5;sub++){
                double now=t+sub/60.0;
                if(i<144)c.ObserveTool(Local(c,new Point((t<6?110:174)+Math.Sin(now*8)*13,222)),true);
                else if(i==174&&sub==0)c.PressTool(Local(c,c.Life.Play.Ball.Position));
                else if(i>=174&&i<184)c.MoveTool(Local(c,new Point(190-(i-174)*9,180-(i-174)*4)));
                else if(i==184&&sub==0)c.ReleaseTool();
                Step(c,1.0/60);
            }
            Render(c,Path.Combine(output,"frames","frame-"+i.ToString("D5")+".png"),i<144?"逗猫棒：左右晃动，原生拨爪":"毛线球：拖动、抛出、反弹与拨球");
        }
        File.WriteAllText(Path.Combine(output,"interaction-metrics.json"),"{\"feather_swats\":"+swats+",\"feather_contacts\":"+contacts+",\"ball_swats\":"+c.Life.Play.Swats+",\"ball_contacts\":"+c.Life.Play.Contacts+",\"ball_bats\":"+c.Life.Play.Ball.Bats+"}");c.Pet.Close();c.Dispose();return 0;
    }
    public static int Run(string manifest,string output){
        Directory.CreateDirectory(output);PetSettings.UsePreviewProfile(Path.Combine(output,"isolated-state"));report=new LivingReport{scope="Actual native pose/tool integration, direct ball input and contact-driven physics, not just detached signal checks"};
        Check("球拖动后释放保留惯性并落回地面",delegate{var b=new YarnBall();b.Press(b.Position,0);b.Move(new Point(150,140),.08);b.Move(new Point(110,100),.16);b.Release(.17);Point before=b.Position;b.Tick(.12);Assert(!b.Held&&(b.Position-before).Length>10,"没有抛出或仍然粘在手上");for(int i=0;i<1500;i++)b.Tick(.01);Assert(Math.Abs(b.Position.Y-225)<.01&&Math.Abs(b.Velocity.X)<1,"没有落地停稳");});
        Check("远处和手里拿着的球不会被猫隔空拨动",delegate{var b=new YarnBall();Assert(!b.Bat(new Point(-100,0)),"隔空碰球");b.Press(b.Position,0);Assert(!b.Bat(b.Position)&&b.Bats==0,"从手里抢走球");});
        Check("滚球边界反弹且无效坐标不会污染位置",delegate{var b=new YarnBall();b.Press(b.Position,0);b.Move(new Point(10000,-10000),.1);b.Release(.1);b.Move(new Point(double.NaN,0),.2);for(int i=0;i<500;i++){b.Tick(.02);Assert(b.Position.X>=-85&&b.Position.X<=341&&b.Position.Y>=-25&&b.Position.Y<=225,"球飞出可见区域");}});
        Check("动作收尾期间仍接收羽毛位置，但不虚构拨爪",delegate{var p=new PetPlaySession();p.Select(PetTool.Feather);p.Observe(new Point(100,220),true,false);p.Observe(new Point(120,220),true,false);var s=new CompanionSignals();p.Apply(s);Assert(p.PointerVisible&&s.PointerToy==1&&p.Swats==0,"输入被动画屏蔽或提前记为出爪");});
        Check("素材拒绝出爪时不会虚增玩耍次数",delegate{var p=new PetPlaySession();p.RequestPaw=left=>false;p.Select(PetTool.Feather);for(int i=0;i<300;i++){p.Tick(.05);p.Observe(new Point(110+Math.Sin(i)*14,220),true);}Assert(p.Swats==0&&!p.CompletedGame,"未播放动作却计数");});
        Check("羽毛移开时实际爪子可以扑空，静止指针不反复触发",delegate{var p=new PetPlaySession();p.RequestPaw=left=>true;p.Select(PetTool.Feather);p.Observe(new Point(150,220),true);p.Observe(new Point(170,220),true);p.Observe(new Point(290,180),true);p.Contact(new Point(170,220));for(int i=0;i<120;i++){p.Tick(.05);p.Observe(new Point(290,180),true);}Assert(p.Swats==1&&p.Contacts==0&&!p.CompletedGame,"凭空接到玩具或静止重复动作");});
        Check("一次真实接触只拨球一次，球会被拨开",delegate{var p=new PetPlaySession();p.RequestPaw=left=>true;p.Select(PetTool.Yarn);p.TryReach(true);Point before=p.Ball.Position;for(int i=0;i<10;i++)p.Contact(before);p.Tick(.15);Assert(p.Contacts==1&&p.Ball.Bats==1&&(p.Ball.Position-before).Length>10,"没有接触反馈或一次触碰反复加速");});
        var app=new Application();var c=Create(manifest);
        Check("设置面板旧玩球入口也进入同一可操作会话",delegate{c.Interact(CompanionGesture.Play);Assert(c.Life.Play.Tool==PetTool.Yarn&&c.Companion.Gesture==CompanionGesture.None,"设置入口仍播放旧的固定动画");c.SelectTool(PetTool.Hand);});
        Check("坐姿中拿起羽毛保留姿态，输入确实播放左右原生拨爪",delegate{foreach(bool left in new[]{true,false}){Pose(c,CatHabit.SitUp);c.SelectTool(PetTool.Feather);Assert(!c.Skin.Motion.Recovering&&c.Skin.Motion.HoldingPose,"拿起工具强制退出姿势");for(int i=0;i<80&&c.Life.Play.Swats==0;i++){Step(c,.03);c.ObserveTool(Local(c,new Point((left?110:172)+Math.Sin(i*.5)*11,222)),true);}Assert(c.Life.Play.Swats>0&&c.Skin.Motion.PostureActivity=="sit-play-"+(left?"left":"right"),"真实坐姿未触发对应原生动作");Render(c,Path.Combine(output,left?"feather-left.png":"feather-right.png"),"原生拨爪已触发");}});
        Check("原生爪接近球之前球不动，接近后实际滚开",delegate{Pose(c,CatHabit.SitUp);c.SelectTool(PetTool.Yarn);Point start=c.Life.Play.Ball.Position;bool sawNative=false;int i=0;while(c.Life.Play.Contacts==0&&i++<180){Step(c,.02);sawNative|=c.Skin.Motion.PostureActivity=="sit-play-right";if(c.Life.Play.Contacts==0)Assert((c.Life.Play.Ball.Position-start).Length<.01,"爪未到球已自己飞走");}Assert(sawNative&&c.Life.Play.Contacts==1&&c.Life.Play.Ball.Bats==1,"原生动作没有接通碰球");for(int k=0;k<10;k++)Step(c,.02);Assert((c.Life.Play.Ball.Position-start).Length>10,"球没有被拨走");Render(c,Path.Combine(output,"ball-contact.png"),"球被实际拨开");});
        Check("持续玩具会话不会每帧取消坐姿或自动切到别的活动",delegate{Pose(c,CatHabit.SitUp);c.SelectTool(PetTool.Feather);for(int i=0;i<600;i++){Step(c,.03);Assert(c.Skin.Motion.Active&&c.Skin.Motion.Clip.Spec.id=="sit"&&c.Skin.Motion.HoldingPose&&!c.Skin.Motion.Recovering,"玩具会话不断重置姿态");}});
        Check("站姿拿起玩具经原生坐下过渡，输入期间仍显示羽毛",delegate{Pose(c,CatHabit.StandUp);c.SelectTool(PetTool.Feather);Step(c,.02);c.ObserveTool(Local(c,new Point(170,220)),true);Assert(c.Life.Play.PointerVisible&&c.Skin.Motion.Active,"过渡中玩具消失");for(int i=0;i<400&&!c.Skin.Motion.HoldingPose;i++)Step(c,.02);Assert(c.Skin.Motion.HoldingPose&&c.Skin.Motion.Clip.Spec.id=="sit","没有自然转入玩耍姿势");});
        Check("拖球操作不改变猫咪位置，丢失捕获时释放球",delegate{Pose(c,CatHabit.SitUp);c.SelectTool(PetTool.Yarn);Point before=new Point(c.Pet.Left,c.Pet.Top);c.PressTool(Local(c,c.Life.Play.Ball.Position));c.Life.Play.Tick(.05);c.MoveTool(Local(c,new Point(120,160)));c.ReleaseTool();Assert(!c.Life.Play.Ball.Held&&new Point(c.Pet.Left,c.Pet.Top)==before,"拖走猫或球一直粘手");});
        Check("工具超时和勿扰收回玩具，并解除玩耍姿势锁定",delegate{c.Life.Play.Tick(46);c.TickPlayBehavior(0);Assert(!c.PlayingOnDesktop&&!c.Life.Play.Active,"超时仍锁定");c.SelectTool(PetTool.Yarn);c.Mode(PetMode.Quiet);c.TickPlayBehavior(0);Assert(!c.Life.Play.Active&&!c.PlayingOnDesktop,"勿扰仍玩球");c.Mode(PetMode.Companion);});
        Check("放下玩具后扩大互动区域恢复透明穿透",delegate{c.SelectTool(PetTool.Feather);var r=c.Pet.Surface.PetRect;var point=new Point(r.Left-65,r.Top+60);Assert(c.Pet.Surface.HitPet(point),"玩具可操作范围仍过窄");c.SelectTool(PetTool.Hand);Assert(!c.Pet.Surface.HitPet(point),"工具放下后仍占用桌面");});
        c.Pet.Close();c.Dispose();using(var f=File.Create(Path.Combine(output,"toy-test-report.json")))new DataContractJsonSerializer(typeof(LivingReport)).WriteObject(f,report);return report.failed==0?0:1;
    }
}
}
