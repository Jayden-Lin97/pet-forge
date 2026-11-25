using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;
using Forms=System.Windows.Forms;

namespace MaohaiziPet {
public static class Program {
    internal static string[] WithBundledPet(string[] args,string directory){
        if(args.Length!=0&&!(args.Length==1&&args[0]=="--background"))return args;
#if CUSTOMER_RELEASE
        string[] candidates={Path.Combine("桌宠资料","pet.json"),"pet.json"};
#else
        string[] candidates={Path.Combine("桌宠资料","pet.json"),"pet.json","preview-pet.json"};
#endif
        foreach(string relative in candidates){
            string path=Path.Combine(directory,relative);
#if CUSTOMER_RELEASE
            if(File.Exists(path))return new[]{"--pet",path};
#else
            if(File.Exists(path))return new[]{relative=="preview-pet.json"?"--preview-pet":"--pet",path};
#endif
        }
        return args;
    }
    [STAThread] public static int Main(string[] args) {
        try {
#if CUSTOMER_RELEASE
            if(args.Length!=0&&!(args.Length==1&&args[0]=="--background"))return 2;
#endif
#if !CUSTOMER_RELEASE
            if(args.Length>2&&args[0]=="--test-customer")return CustomerEditionTests.Run(args[1],args[2]);
            if(args.Length>2&&args[0]=="--test-cute")return CuteRollTests.Run(args[1],args[2]);
            if(args.Length>2&&args[0]=="--preview-cute")return CuteRollTests.Preview(args[1],args[2]);
            if(args.Length>2&&args[0]=="--test-color")return GreenSpillTests.Run(args[1],args[2]);
            if(args.Length>2&&args[0]=="--preview-walk")return GreenSpillTests.PreviewWalk(args[1],args[2]);
            if(args.Length>2&&args[0]=="--test-jump")return JumpPlayTests.Run(args[1],args[2]);
            if(args.Length>2&&args[0]=="--preview-jump")return JumpPlayTests.Preview(args[1],args[2]);
            if(args.Length>2&&args[0]=="--test-toys")return ToyInteractionTests.Run(args[1],args[2]);
            if(args.Length>2&&args[0]=="--preview-toys")return ToyInteractionTests.Preview(args[1],args[2]);
            if(args.Length>2&&args[0]=="--preview-residence")return ResidenceTests.Preview(args[1],args[2]);
            if(args.Length>2&&args[0]=="--preview-standing")return ResidenceTests.Preview(args[1],args[2],true);
            if(args.Length>2&&args[0]=="--test-residence")return ResidenceTests.Run(args[1],args[2]);
            if(args.Length>2&&args[0]=="--test-behavior")return BehaviorUpgradeTests.Run(args[1],args[2]);
            if(args.Length>2&&args[0]=="--test-desktop")return DesktopPlayTests.Run(args[1],args[2]);
            if(args.Length>2&&args[0]=="--test-life")return PetLifeTests.Run(args[1],args[2]);
            if(args.Length>2&&args[0]=="--test-touch")return PetTouchTests.Run(args[1],args[2]);
            if(args.Length>2&&args[0]=="--test-motions")return MotionClipTests.Run(args[1],args[2]);
            if(args.Length>2&&args[0]=="--test-habits")return CatHabitsTests.Run(args[1],args[2]);
            if(args.Length>2&&args[0]=="--test-companion")return CompanionTests.Run(args[1],args[2]);
            if(args.Length>2&&args[0]=="--test-living")return LivingPetTests.Run(args[1],args[2]);
#endif
            string previewManifest=null;
            bool backgroundStart=args.Length==1&&args[0]=="--background";
            args=WithBundledPet(args,AppDomain.CurrentDomain.BaseDirectory);
#if CUSTOMER_RELEASE
            if(args.Length!=2||args[0]!="--pet")throw new InvalidDataException("请保留完整的桌宠资料文件夹");
#endif
#if CUSTOMER_RELEASE
            if(args.Length==2&&args[0]=="--pet"){
#else
            if(args.Length==2&&(args[0]=="--preview-pet"||args[0]=="--pet")){
#endif
                previewManifest=Path.GetFullPath(args[1]);
                SkinManifest bundled;using(var f=File.OpenRead(previewManifest))bundled=PetSkin.ReadManifest(f);
#if CUSTOMER_RELEASE
                if(!bundled.customerEdition||string.IsNullOrEmpty(bundled.profileId))throw new InvalidDataException("宠物信息不完整");
#endif
                if(bundled.customerEdition)PetSettings.UseCustomerProfile(bundled.profileId??"companion");
#if !CUSTOMER_RELEASE
                else PetSettings.UsePreviewProfile(Path.Combine(Path.GetDirectoryName(previewManifest),"preview-state"));
#endif
            }
#if !CUSTOMER_RELEASE
            if(args.Length>0&&args[0]=="--self-test") return SelfTest.Run(args.Length>1?args[1]:"artifacts");
            if(args.Length>1&&args[0]=="--render-ui") {
                var previewApp=new Application();var previewController=new PetController(previewApp);
                previewController.Settings.Mode=PetMode.Companion;previewController.Engine.SetMode(PetMode.Companion);previewController.Engine.React(PetAction.Pet);previewController.Engine.Tick(.6);
                previewController.Pet=new PetWindow(previewController);var panel=new SettingsWindow(previewController);panel.RefreshStatus();
                var content=(FrameworkElement)panel.Content;content.Measure(new Size(960,700));content.Arrange(new Rect(0,0,960,700));content.UpdateLayout();
                var bitmap=new RenderTargetBitmap(1440,1050,144,144,PixelFormats.Pbgra32);var background=new DrawingVisual();using(var dc=background.RenderOpen())dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(245,243,236)),null,new Rect(0,0,960,700));bitmap.Render(background);bitmap.Render(content);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));using(var file=File.Create(args[1]))png.Save(file);return 0;
            }
#endif
            var application=new Application();application.ShutdownMode=ShutdownMode.OnExplicitShutdown;
#if CUSTOMER_RELEASE
            string instanceKey="Local\\PetForge-Customer-v1";
#else
            string instanceKey=previewManifest==null?"Local\\MaohaiziPet-v1":"Local\\MaohaiziPet-RagdollPreview-v1";
#endif
            bool first;using(var mutex=new Mutex(true,instanceKey,out first)) {
                if(!first) {Native.PostMessage(new IntPtr(0xffff),Native.ActivateMessage,IntPtr.Zero,IntPtr.Zero);return 0;}
                var controller=new PetController(application);
                if(previewManifest!=null){bool sameProfile=string.Equals(controller.Settings.SkinPath,previewManifest,StringComparison.OrdinalIgnoreCase);bool firstUse=!File.Exists(Path.Combine(PetSettings.Root,"settings.xml"));if(!sameProfile||controller.Skin.Living==null)controller.Skin=PetSkin.Load(previewManifest);controller.Settings.SkinPath=previewManifest;if(!sameProfile&&(firstUse||!controller.Skin.CustomerEdition)){controller.Settings.Name=controller.Skin.Name;controller.Settings.Size=200;}}
                controller.Start(backgroundStart||!controller.Skin.CustomerEdition&&controller.Skin.Living!=null);
                application.Run();controller.Dispose();
            }
            return 0;
        } catch(Exception e) {
            try{Directory.CreateDirectory(PetSettings.Root);File.WriteAllText(Path.Combine(PetSettings.Root,"运行记录.txt"),e.ToString());}catch{}
            MessageBox.Show("暂时无法打开桌宠。请先完整解压文件夹，再重新打开。\n如果仍有问题，请查看项目使用说明或 GitHub Issues。","毛孩子桌宠");return 1;
        }
    }
}
public static class Native {
    [StructLayout(LayoutKind.Sequential)] public struct Point {public int X,Y;}
    [DllImport("user32.dll")] public static extern bool GetCursorPos(out Point p);
    [DllImport("user32.dll")] public static extern IntPtr WindowFromPoint(Point point);
    [DllImport("user32.dll")] public static extern IntPtr LoadCursor(IntPtr instance,IntPtr resource);
    [DllImport("user32.dll")] public static extern IntPtr SetCursor(IntPtr cursor);
    [StructLayout(LayoutKind.Sequential)] struct LastInput {public uint Size,Time;}
    [DllImport("user32.dll")] static extern bool GetLastInputInfo(ref LastInput input);
    public static double IdleSeconds(){var input=new LastInput{Size=(uint)Marshal.SizeOf(typeof(LastInput))};return GetLastInputInfo(ref input)?unchecked((uint)Environment.TickCount-input.Time)/1000.0:-1;}
    [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr hwnd,int id,uint modifiers,uint key);
    [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr hwnd,int id);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] public static extern uint RegisterWindowMessage(string message);
    [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr hwnd,uint message,IntPtr wparam,IntPtr lparam);
    public static readonly uint ActivateMessage=RegisterWindowMessage("PetForge.Activate.v1");
}
public sealed class PetController:IDisposable {
    public readonly Application Application;
    public readonly PetSettings Settings;
    public readonly PetEngine Engine;
    public readonly CompanionMind Companion;
    public readonly PetLife Life;
    public readonly PetNeeds Needs;
    public readonly WindowPerch Perch=new WindowPerch();
    public readonly MovementGoals Goals=new MovementGoals();
    bool toyDriving;
    public readonly CatHabits Habits=new CatHabits(Environment.TickCount);
    public PetSkin Skin;
    public PetWindow Pet;
    public SettingsWindow Panel;
    private Forms.NotifyIcon tray;
    private readonly Stopwatch clock=Stopwatch.StartNew();
    private readonly DispatcherTimer timer=new DispatcherTimer();
    private double previous,painted,savedAt;
    private int lastHabitRevision=-1;
    PetTouchZone pendingTouch=PetTouchZone.None;
    CatHabit pendingHabit=CatHabit.None;
    double pendingSide;
    bool pendingStroke;
    public bool Hidden;
    public PetController(Application a) {
        Application=a;Settings=PetSettings.Load();Engine=new PetEngine(Settings.Personality,Settings.Mode,Environment.TickCount);Skin=new PetSkin();
        Companion=new CompanionMind(Settings.Familiarity,Settings.Personality){WarmWords=Settings.WarmWords};
        Life=new PetLife(Settings.Life,DateTime.Now);
        Needs=new PetNeeds(Settings.Needs);Habits.Needs=Needs;Habits.Policy=BehaviorPolicy.Load(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"behaviors.json"));
        if(!string.IsNullOrEmpty(Settings.SkinPath))try{Skin=PetSkin.Load(Settings.SkinPath);}catch{Settings.SkinPath="";}
    }
    public void Start(bool background) {
        ConfigureMotions();
        Pet=new PetWindow(this);Pet.Show();Pet.Clamp();
        tray=new Forms.NotifyIcon();tray.Icon=System.Drawing.SystemIcons.Information;tray.Text="毛孩子桌宠 · 右键打开设置";tray.Visible=true;
        var menu=new Forms.ContextMenuStrip();
        menu.Items.Add("打开设置",null,delegate{Application.Dispatcher.Invoke(new Action(OpenSettings));});
        menu.Items.Add("隐藏 / 显示",null,delegate{Application.Dispatcher.Invoke(new Action(ToggleHidden));});
        menu.Items.Add("切换勿扰",null,delegate{Application.Dispatcher.Invoke(new Action(ToggleQuiet));});
        menu.Items.Add(new Forms.ToolStripSeparator());menu.Items.Add("退出桌宠",null,delegate{Application.Dispatcher.Invoke(new Action(Exit));});
        tray.ContextMenuStrip=menu;tray.DoubleClick+=delegate{Application.Dispatcher.Invoke(new Action(OpenSettings));};
        timer.Interval=TimeSpan.FromMilliseconds(Skin.Living!=null?16:50);timer.Tick+=Tick;timer.Start();
        if(!background)OpenSettings();
    }
    void Tick(object sender,EventArgs e) {
        double now=clock.Elapsed.TotalSeconds;double elapsed=now-previous;double delta=Math.Min(.5,elapsed);previous=now;Engine.Tick(delta);
        if(Skin.Living!=null){
            Pet.ObserveWindowPerch(now);
            Life.Tick(elapsed,!Hidden,Engine.Mode==PetMode.Quiet,DateTime.Now);Habits.Mood=Life.Mood;
            UpdateNeeds(elapsed,DateTime.Now.Hour);
            Companion.Personality=Settings.Personality;Companion.WarmWords=Settings.WarmWords;Companion.Tick(elapsed,Engine.Mode,Native.IdleSeconds(),Habits.Active||Skin.Motion.Active||Life.Play.Active);Habits.Familiarity=Companion.Warmth;
            UpdateWalkRoom();
            TickPlayBehavior(elapsed);
            if(PlayingOnDesktop)Habits.PauseForPlay(elapsed);
            else Habits.Tick(elapsed,Engine.Mode,Companion.IsFocusing||Hidden||Life.Play.Active||Skin.Motion.Recovering,Companion.Gesture!=CompanionGesture.None||Companion.Touch.Active||pendingTouch!=PetTouchZone.None||Engine.Action==PetAction.Pet||Engine.Action==PetAction.Drag);Habits.Apply(Companion.Signals);
            string motionId=AdvanceMotions(elapsed);
            UpdateToyContact();
            if(motionId=="yawn"&&Skin.Motions!=null&&Skin.Motions.Clips.ContainsKey(motionId))Companion.Signals.Yawn=0;
            Point? attention=null;Native.Point cursor;
            if(Native.GetCursorPos(out cursor)){
                Point p=Pet.PointFromScreen(new Point(cursor.X,cursor.Y));Rect r=Pet.Surface.PetRect;
                if(new Rect(r.X-430,r.Y-330,r.Width+860,r.Height+660).Contains(p))attention=new Point((p.X-r.X-r.Width*.52)/180,(p.Y-r.Y-r.Height*.35)/180);
                if(!Hidden)Pet.ObservePointer(cursor,p,Companion.Clock);
                TryToyWalk(Pet.Left+p.X);
            }
            Life.Apply(Companion.Signals,Companion.Touch.Zone);
            Skin.Living.Advance(delta,Engine.Action,Engine.Mode,attention,Companion.Signals,Skin.Motion.Active);
            if((Companion.Dirty||Life.Dirty)&&now-savedAt>60){Save();savedAt=now;}
        }
        if(!Hidden) {
            Pet.MoveForPlay(delta);
            double interval=Skin.Living!=null?1.0/30:(Engine.Mode==PetMode.Quiet||Engine.Action==PetAction.Sleep)?.6:Engine.Action==PetAction.Idle?.18:.05;
            if(now-painted>=interval) {painted=now;Pet.Surface.InvalidateVisual();}
        }
        if(Panel!=null&&Panel.IsVisible)Panel.RefreshStatus();
    }
    public void OpenSettings() {
        if(Pet!=null)Pet.CloseDesktopActions();
        if(Panel==null) {Panel=new SettingsWindow(this);Panel.Closed+=delegate{Panel=null;};}
        Panel.Show();Panel.WindowState=WindowState.Normal;Panel.Activate();
    }
    internal void UpdateNeeds(double seconds,int hour){Needs.Tick(seconds,Habits.Current==CatHabit.CurlSleep&&Skin.Motion.HoldingPose,CatHabits.IsWalk(Habits.Current)&&Skin.Motion.Active,!Hidden&&Engine.Mode!=PetMode.Quiet,hour);}
    internal bool PlayingOnDesktop {get{return Life.Play.Interactive&&!Hidden&&Engine.Mode!=PetMode.Quiet&&!Companion.IsFocusing;}}
    internal void TickPlayBehavior(double elapsed){
        if(!PlayingOnDesktop){if(toyDriving){Skin.Motion.AttendToy(null);Habits.Interrupt();Skin.Motion.Cancel();Goals.Finished();}toyDriving=false;return;}
        toyDriving=true;
        var player=Skin.Motion;var play=Life.Play;
        // Keep a live seated stance instead of cancelling every pose each tick.
        // Existing transitions always finish through their native recovery.
        if(!player.Active&&Habits.SitReady)StartHabit(CatHabit.SitUp);
        else if(player.HoldingPose&&!player.Recovering&&player.Clip.Spec.id=="stand"&&Habits.SitReady&&CanJoinHabit(CatHabit.SitUp))StartHabit(CatHabit.SitUp);
        Point? target=play.TargetVisible?(Point?)play.Target:new Point(140,210);
        player.AttendToy(target);
        if(play.Tool==PetTool.Yarn&&play.TryReach(player.PlayReady||!player.Active))RecordToyGame();
    }
    internal void UpdateToyContact(){if(PlayingOnDesktop&&Life.Play.Contact(Skin.Motion.ToyPaw))RecordToyGame();}
    void RecordToyGame(){Life.Game();Needs.Play();Companion.Remark("接住啦，再陪我玩一会儿。");}
    void TryToyWalk(double pointerX){
        if(Goals.ToyWalk&&!CatHabits.IsWalk(Habits.Current))Goals.Finished();
        if(Life.Play.Tool!=PetTool.Feather||!Life.Play.TargetVisible||Skin.Motion.Active&&(!Skin.Motion.HoldingPose||Skin.Motion.PostureActivity!=null||Skin.Motion.Recovering)||Engine.Mode==PetMode.Quiet||Hidden||Companion.IsFocusing||Perch.Attached||Needs.State.Energy<25||Math.Abs(pointerX-(Pet.Left+Pet.Width/2))<85*Settings.Size/256)return;
        Func<string,CatHabit,double?> distance=(id,habit)=>Skin.Motions!=null&&Skin.Motions.Clips.ContainsKey(id)&&Habits.CanStartGoal(habit)&&WalkFits(id)?(double?)(Skin.Motions.Clips[id].Spec.travelX.Last()*Settings.Size/256):null;
        var walk=Goals.Toy(Pet.Left+Pet.Width/2,pointerX,distance("walk-left",CatHabit.WalkLeft),distance("walk-right",CatHabit.WalkRight));
        if(walk!=CatHabit.None)StartHabit(walk);
    }
    static string HabitMotion(CatHabit habit){return habit==CatHabit.Groom?"groom":habit==CatHabit.Yawn?"yawn":habit==CatHabit.BellyRoll?"belly":habit==CatHabit.SitUp?"sit":habit==CatHabit.StandUp?"stand":habit==CatHabit.CurlSleep?"sleep":habit==CatHabit.WalkLeft?"walk-left":habit==CatHabit.WalkRight?"walk-right":null;}
    internal string AdvanceMotions(double elapsed){
        string motionId=HabitMotion(Habits.Current);
        if(Habits.Revision!=lastHabitRevision&&motionId!=null&&Skin.Motions!=null&&Skin.Motions.Clips.ContainsKey(motionId)){
            var clip=Skin.Motions.Clips[motionId];
            if(Habits.Current==CatHabit.StandUp&&Skin.Motion.CanEnterStanding(clip)){Habits.EnterAt(Skin.Motion.SecondsToStanding(clip));Skin.Motion.EnterStanding(clip,Habits.DwellSeconds);}
            else if(Habits.Current==CatHabit.SitUp&&Skin.Motion.CanEnterSitting(clip)){Habits.EnterAt(Skin.Motion.SecondsToSitting);Skin.Motion.EnterSitting(clip,Habits.DwellSeconds);}
            else if(CatHabits.IsWalk(Habits.Current)&&Skin.Motion.CanEnterWalk(clip))Skin.Motion.EnterWalk(clip,Habits.Duration);
            else if(CatHabits.IsWalk(Habits.Current))Skin.Motion.StartLivingWalk(clip,Habits.Duration);
            else if(clip.HasHold&&Habits.DwellSeconds>0)Skin.Motion.StartResidence(clip,Habits.DwellSeconds);
            else Skin.Motion.Start(clip,Habits.Duration);
        }
        lastHabitRevision=Habits.Revision;if(!Habits.Active&&Habits.Interrupted)Skin.Motion.Cancel();Skin.Motion.Tick(elapsed);
        if(Skin.Motion.RecoveredPose!=null){
            var settled=Skin.Motion.RecoveredPose=="stand"?CatHabit.StandUp:CatHabit.SitUp;
            bool resume=Habits.Start(settled,Engine.Mode,Companion.IsFocusing||Hidden);
            if(resume)Habits.EnterAt(0);Skin.Motion.LandRecoveredPose(resume?Habits.DwellSeconds:0);lastHabitRevision=Habits.Revision;
            if(pendingHabit!=CatHabit.None){var next=pendingHabit;pendingHabit=CatHabit.None;StartHabit(next);}
            else if(pendingTouch!=PetTouchZone.None){var zone=pendingTouch;pendingTouch=PetTouchZone.None;Touch(zone,pendingStroke,pendingSide);}
        }
        if(Skin.Motion.AtStandingExit&&CatHabits.IsWalk(Habits.Current)&&pendingHabit==CatHabit.None&&pendingTouch==PetTouchZone.None&&Habits.AdoptStanding()){Skin.Motion.ContinueStanding(Habits.DwellSeconds);lastHabitRevision=Habits.Revision;}
        if(!Skin.Motion.Active&&pendingHabit!=CatHabit.None){var next=pendingHabit;pendingHabit=CatHabit.None;StartHabit(next);}
        if(!Skin.Motion.Active&&pendingTouch!=PetTouchZone.None){var zone=pendingTouch;pendingTouch=PetTouchZone.None;if(Companion.Contact(zone,Engine.Mode,pendingStroke,pendingSide)){Life.Touch(zone);Needs.Touch();}}
        return HabitMotion(Habits.Current);
    }
    internal void ConfigureMotions(){
        Engine.AutonomousIdle=Skin.Living==null;Skin.Motion.ConfigureLibrary(Skin.Motions);
        Life.Play.RequestPaw=left=>Skin.Motion.Active?Skin.Motion.PlayPaw(left):true;
        Life.Play.RequestJump=target=>Pet!=null&&HasMotion("sit-jump")&&Pet.HasJumpingRoom(Skin.Motions.Clips["sit-jump"].Spec.jumpTop)&&Skin.Motion.PlayJump(target);
        Habits.GroomReady=HasMotion("groom");Habits.YawnReady=HasMotion("yawn");Habits.BellyReady=HasMotion("belly");Habits.SitReady=HasMotion("sit");Habits.StandReady=HasMotion("stand");Habits.SleepReady=HasMotion("sleep");
        if(Skin.Motions!=null)foreach(var pair in Skin.Motions.Clips){CatHabit habit=pair.Key=="groom"?CatHabit.Groom:pair.Key=="yawn"?CatHabit.Yawn:pair.Key=="belly"?CatHabit.BellyRoll:pair.Key=="sit"?CatHabit.SitUp:pair.Key=="stand"?CatHabit.StandUp:pair.Key=="walk-left"?CatHabit.WalkLeft:pair.Key=="walk-right"?CatHabit.WalkRight:pair.Key=="sleep"?CatHabit.CurlSleep:CatHabit.None;if(habit==CatHabit.None)continue;Habits.SetMotionDuration(habit,pair.Value.Seconds);if(pair.Value.HasHold)Habits.SetResidenceTiming(habit,pair.Value.Spec.holdAt/pair.Value.Spec.fps,pair.Value.Spec.frames/pair.Value.Spec.fps);}
    }
    bool HasMotion(string id){return Skin.Motions!=null&&Skin.Motions.Clips.ContainsKey(id);}

    internal void UpdateWalkRoom(){Habits.WalkLeftReady=!Perch.Attached&&WalkFits("walk-left");Habits.WalkRightReady=!Perch.Attached&&WalkFits("walk-right");}
    bool WalkFits(string id){if(Pet==null||Skin.Motions==null||!Skin.Motions.Clips.ContainsKey(id))return false;var spec=Skin.Motions.Clips[id].Spec;return spec.travelX!=null&&Pet.HasWalkingRoom(spec.travelX[spec.frames-1],spec.travelY==null?0:spec.travelY.Min(),spec.travelY==null?0:spec.travelY.Max());}
    CatHabit PreferredWalk(){UpdateWalkRoom();if(Habits.WalkLeftReady&&Habits.WalkRightReady)return Pet.Left>SystemParameters.WorkArea.Width/2?CatHabit.WalkLeft:CatHabit.WalkRight;return Habits.WalkLeftReady?CatHabit.WalkLeft:Habits.WalkRightReady?CatHabit.WalkRight:CatHabit.None;}
    public void React(PetAction action) {pendingHabit=CatHabit.None;pendingTouch=PetTouchZone.None;Engine.Personality=Settings.Personality;if(Engine.React(action)&&Skin.Living!=null){Skin.Motion.Cancel();Habits.Interrupt();if(action==PetAction.Pet){Companion.Stroke(Engine.Mode);Life.Touch(PetTouchZone.Head);Needs.Touch();}if(action==PetAction.Drag)Companion.Silence();}Pet.Surface.InvalidateVisual();}
    public void Interact(CompanionGesture gesture){if(gesture==CompanionGesture.Play&&Skin.Living!=null){SelectTool(PetTool.Yarn);return;}if(gesture==CompanionGesture.Treat&&!Life.CanTreat)return;pendingHabit=CatHabit.None;pendingTouch=PetTouchZone.None;if(Companion.Trigger(gesture,Engine.Mode)){Life.Play.Stop();if(gesture==CompanionGesture.Treat){Life.Treat();Needs.Treat();}if(gesture==CompanionGesture.Play)Needs.Play();Skin.Motion.Cancel();Habits.Interrupt();Engine.React(PetAction.Idle);Pet.Surface.InvalidateVisual();}}
    public void SelectTool(PetTool tool){
        if(Engine.Mode==PetMode.Quiet&&tool!=PetTool.Hand)return;
        if(Companion.IsFocusing&&(tool==PetTool.Feather||tool==PetTool.Yarn))return;
        pendingHabit=CatHabit.None;
        pendingTouch=PetTouchZone.None;Life.Play.Select(tool);Goals.NewTool();bool preserve=Life.Play.Interactive&&Skin.Motion.Active&&!Skin.Motion.Recovering&&(Skin.Motion.Clip.Spec.id=="sit"||Skin.Motion.Clip.Spec.id=="stand");if(!preserve){Skin.Motion.Cancel();Habits.Interrupt();}Companion.Silence();Engine.React(PetAction.Idle);
        if(Panel!=null&&tool!=PetTool.Hand)Panel.Close();Pet.Surface.InvalidateVisual();
    }
    public void ObserveTool(Point local,bool eligible){
        Rect rect=Pet.Surface.PetRect;var normalized=new Point((local.X-rect.X)/rect.Width*256,(local.Y-rect.Y)/rect.Height*256);
        bool game=Life.Play.Observe(normalized,eligible,PlayingOnDesktop&&(!Skin.Motion.Active||Skin.Motion.PlayReady));if(game)RecordToyGame();
    }
    internal Point ToolPoint(Point local){Rect r=Pet.Surface.PetRect;return new Point((local.X-r.X)/r.Width*256,(local.Y-r.Y)/r.Height*256);}
    public void PressTool(Point local){Life.Play.Press(ToolPoint(local));}
    public void MoveTool(Point local){Life.Play.Move(ToolPoint(local));}
    public void ReleaseTool(){Life.Play.Release();}
    public void OpenMemories(){var window=new PetLifeWindow(this);window.Owner=Panel??(Window)Pet;window.ShowDialog();}
    public void Touch(PetTouchZone zone,bool stroking,double side){
        if(Engine.Mode==PetMode.Quiet||zone==PetTouchZone.None)return;
        pendingHabit=CatHabit.None;
        if(zone==PetTouchZone.Toy){Interact(CompanionGesture.Play);return;}
        bool responded;if(Skin.Motion.ContactInPose(zone,out responded)){if(responded){Life.Touch(zone);Needs.Touch();Companion.ContactInPose(zone,Engine.Mode);}Pet.Surface.InvalidateVisual();return;}
        if(Skin.Motion.Active){if(pendingTouch==PetTouchZone.None||!stroking){pendingTouch=zone;pendingStroke=stroking;pendingSide=side;}Skin.Motion.Cancel();Habits.Interrupt();Engine.React(PetAction.Idle);return;}
        if(Companion.Contact(zone,Engine.Mode,stroking,side)){Life.Touch(zone);Needs.Touch();Habits.Interrupt();Engine.React(PetAction.Idle);Pet.Surface.InvalidateVisual();}
    }
    bool CanJoinHabit(CatHabit habit){string id=HabitMotion(habit);if(id==null||Skin.Motions==null||!Skin.Motions.Clips.ContainsKey(id))return false;var clip=Skin.Motions.Clips[id];return CatHabits.IsWalk(habit)&&Skin.Motion.CanEnterWalk(clip)||habit==CatHabit.StandUp&&Skin.Motion.CanEnterStanding(clip)||habit==CatHabit.SitUp&&Skin.Motion.CanEnterSitting(clip);}
    public void StartHabit(CatHabit habit){if(habit==CatHabit.None||Engine.Mode==PetMode.Quiet||Companion.IsFocusing||!Habits.Available(habit)||Habits.Current==habit)return;if(Skin.Motion.Active&&!CanJoinHabit(habit)){pendingHabit=habit;pendingTouch=PetTouchZone.None;Habits.Interrupt();Skin.Motion.Cancel();Companion.Silence();Engine.React(PetAction.Idle);return;}if(Habits.Start(habit,Engine.Mode,false)){pendingTouch=PetTouchZone.None;Companion.Silence();Engine.React(PetAction.Idle);Pet.Surface.InvalidateVisual();}}
    CatHabit DesktopHabit(DesktopPetAction action){return action==DesktopPetAction.Sit?CatHabit.SitUp:action==DesktopPetAction.Stand?CatHabit.StandUp:action==DesktopPetAction.Groom?CatHabit.Groom:action==DesktopPetAction.Yawn?CatHabit.Yawn:action==DesktopPetAction.Walk?PreferredWalk():action==DesktopPetAction.Belly?CatHabit.BellyRoll:action==DesktopPetAction.Sleep?CatHabit.CurlSleep:CatHabit.None;}
    public bool HasDesktopAction(DesktopPetAction action){
        if(Skin.Living==null||!Enum.IsDefined(typeof(DesktopPetAction),action))return false;
        if(action==DesktopPetAction.LeavePerch)return Perch.Attached;
        if((action==DesktopPetAction.Feather||action==DesktopPetAction.Yarn)&&Companion.IsFocusing)return false;
        if(action==DesktopPetAction.Perch)return !Perch.Attached;
        if(action==DesktopPetAction.Walk)return Skin.Motions!=null&&(Skin.Motions.Clips.ContainsKey("walk-left")||Skin.Motions.Clips.ContainsKey("walk-right"));
        CatHabit habit=DesktopHabit(action);return habit==CatHabit.None||Habits.Available(habit);
    }
    public bool CanDesktopAction(DesktopPetAction action){
        if(!HasDesktopAction(action)||Engine.Mode==PetMode.Quiet)return false;
        if(action==DesktopPetAction.Perch)return Pet!=null&&!Perch.Attached&&Pet.CanPerch&&(!Skin.Motion.Active||Skin.Motion.HoldingPose&&!Skin.Motion.Recovering);
        if(action==DesktopPetAction.LeavePerch)return Perch.Attached;
        if(action==DesktopPetAction.Walk&&PreferredWalk()==CatHabit.None)return false;
        if(action==DesktopPetAction.Treat&&!Life.CanTreat)return false;
        CatHabit habit=DesktopHabit(action);return habit==CatHabit.None||!Companion.IsFocusing&&Habits.Current!=habit;
    }
    public bool DoDesktopAction(DesktopPetAction action){
        if(!CanDesktopAction(action))return false;
        if(action==DesktopPetAction.Perch)return Pet.AttachPerch();
        if(action==DesktopPetAction.LeavePerch){Pet.LeavePerch();return true;}
        CatHabit habit=DesktopHabit(action);
        if(habit!=CatHabit.None){Life.Play.Stop();StartHabit(habit);}
        else if(action==DesktopPetAction.Feather)SelectTool(PetTool.Feather);
        else if(action==DesktopPetAction.Brush)SelectTool(PetTool.Brush);
        else if(action==DesktopPetAction.Yarn)SelectTool(PetTool.Yarn);
        else Interact(action==DesktopPetAction.Treat?CompanionGesture.Treat:CompanionGesture.Comfort);
        return true;
    }
    public void ToggleFocus(){pendingHabit=CatHabit.None;pendingTouch=PetTouchZone.None;if(Companion.IsFocusing)Companion.CancelFocus();else if(Engine.Mode!=PetMode.Quiet){Skin.Motion.Cancel();Habits.Interrupt();Companion.StartFocus(25*60);Engine.React(PetAction.Idle);}}
    public void Mode(PetMode mode) {Pet.CloseDesktopActions();Settings.Mode=mode;Engine.SetMode(mode);if(mode==PetMode.Quiet){Life.Play.Stop();pendingTouch=PetTouchZone.None;pendingHabit=CatHabit.None;Skin.Motion.Cancel();Companion.Silence();Habits.Interrupt();}Pet.Surface.InvalidateVisual();Save();}
    public void ToggleQuiet(){Mode(Settings.Mode==PetMode.Quiet?PetMode.Companion:PetMode.Quiet);}
    public void ToggleHidden(){Pet.CloseDesktopActions();Hidden=!Hidden;if(Hidden){Life.Play.Stop();pendingHabit=CatHabit.None;pendingTouch=PetTouchZone.None;Skin.Motion.Cancel();Habits.Interrupt();Pet.Hide();}else{Pet.Show();Pet.Clamp();}}
    public void Save(){if(Skin.Living!=null){Settings.Familiarity=Companion.Warmth;Settings.Life=Life.Record;}Settings.Save();Companion.MarkSaved();Life.MarkSaved();}
    public void Exit(){Save();timer.Stop();if(Panel!=null)Panel.Close();Pet.Close();Application.Shutdown();}
    public void Dispose(){timer.Stop();if(tray!=null){tray.Visible=false;tray.Dispose();}}
    public static string StartupCommand(string executable){return "\""+executable+"\" --background";}
    public static bool IsStartup(){using(var key=Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run"))return key!=null&&key.GetValue("MaohaiziPet")!=null;}
    public static void Startup(bool enable){using(var key=Registry.CurrentUser.CreateSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run")){if(enable)key.SetValue("MaohaiziPet",StartupCommand(Process.GetCurrentProcess().MainModule.FileName));else key.DeleteValue("MaohaiziPet",false);}}
    public void ImportPack() {
        var dialog=new OpenFileDialog{Title="导入定制宠物包",Filter="宠物素材包 (*.zip)|*.zip"};if(dialog.ShowDialog()!=true)return;
        try {Pet.CloseDesktopActions();string path=PetSkin.Import(dialog.FileName,Path.Combine(PetSettings.Root,"pets"));Skin=PetSkin.Load(path);ConfigureMotions();lastHabitRevision=-1;Settings.SkinPath=path;Settings.Name=Skin.Name;timer.Interval=TimeSpan.FromMilliseconds(Skin.Living!=null?16:50);Save();Pet.Surface.ClearHitCache();if(Panel!=null){Panel.Close();Panel=null;}React(PetAction.Greet);}
        catch{MessageBox.Show("这个宠物包暂时无法打开。请使用完整的宠物包，或在 GitHub Issues 反馈。","导入未完成");}
    }
    public void ExportPack() {
        var dialog=new SaveFileDialog{Title="保存宠物素材包",Filter="宠物素材包 (*.zip)|*.zip",FileName=Settings.Name+"-宠物包.zip"};if(dialog.ShowDialog()!=true)return;
        try{Skin.Export(dialog.FileName);MessageBox.Show("宠物包已导出，可在另一份程序中导入。","导出完成");}catch{MessageBox.Show("暂时无法保存，请选择有写入权限的文件夹后重试。","导出未完成");}
    }
    public void ExportGifs() {
        using(var dialog=new Forms.FolderBrowserDialog{Description="选择表情包保存目录",ShowNewFolderButton=true}) {
            if(dialog.ShowDialog()!=Forms.DialogResult.OK)return;
            try {string folder=Path.Combine(dialog.SelectedPath,"毛孩子表情包-"+DateTime.Now.ToString("yyyyMMdd-HHmmss"));Skin.ExportGifs(folder);MessageBox.Show("已生成 4 个动态 GIF：摸摸我、下班啦、困了、收到。\n"+folder,"表情包已导出");}
            catch(Exception e){MessageBox.Show(e.Message,"导出未完成");}
        }
    }
}
public sealed class PetWindow:Window {
    public readonly PetSurface Surface;
    private readonly PetController controller;
    readonly PetPointerPress press=new PetPointerPress();
    IntPtr perchCandidate;
    Rect perchBounds=Rect.Empty;
    double perchObserved=-1;
    bool doubleClick;
    DesktopPetPocket pocket;
    private Point last;
    private double left,top,lastWalk;
    private HwndSource source;
    readonly PettingTracker petting=new PettingTracker();
    readonly PetPointerActivity pointerActivity=new PetPointerActivity();
    public PetWindow(PetController c) {
        controller=c;Title=c.Settings.Name+" · 桌面伙伴";Width=c.Skin.Living!=null?480:360;Height=c.Skin.Living!=null?440:300;WindowStyle=WindowStyle.None;AllowsTransparency=true;Background=Brushes.Transparent;ShowInTaskbar=false;ShowActivated=false;ResizeMode=ResizeMode.NoResize;Topmost=c.Settings.Topmost;
        Surface=new PetSurface(c);Content=Surface;
        var area=SystemParameters.WorkArea;Left=(double.IsNaN(c.Settings.Left)?area.Right-400:c.Settings.Left)-RestOffsetX;Top=(double.IsNaN(c.Settings.Top)?area.Bottom-310:c.Settings.Top)-RestOffsetY;
        SourceInitialized+=delegate{source=HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);source.AddHook(Hook);Native.RegisterHotKey(source.Handle,1,0x4003,0x50);Native.RegisterHotKey(source.Handle,2,0x4003,0x51);};
        Closed+=delegate{CloseDesktopActions();if(source!=null){Native.UnregisterHotKey(source.Handle,1);Native.UnregisterHotKey(source.Handle,2);source.RemoveHook(Hook);}};
        Surface.MouseLeftButtonDown+=delegate(object s,MouseButtonEventArgs e){CloseDesktopActions();last=e.GetPosition(this);left=Left;top=Top;
            press.Begin(Surface.TouchAt(last),PointToScreen(last),c.Life.Play.Active,c.Skin.Living!=null);doubleClick=e.ClickCount>=2;Surface.CaptureMouse();
            petting.Suppress(PointToScreen(last));
            if(c.Life.Play.Active){c.PressTool(last);c.ObserveTool(last,true);if(c.Life.Play.Tool==PetTool.Brush)TouchAt(last,false);e.Handled=true;return;}
            if(c.Skin.Living!=null){if(doubleClick)c.Interact(CompanionGesture.Nuzzle);else if(press.Stroking)TouchAt(last,false);}else c.React(PetAction.Pet);e.Handled=true;};
        Surface.MouseMove+=delegate(object s,MouseEventArgs e){
            Point p=e.GetPosition(this);
            if(press.Held&&press.Tool&&!c.Life.Play.Active){last=p;return;}
            if(press.Held&&c.Life.Play.Active){c.MoveTool(p);c.ObserveTool(p,true);if(c.Life.Play.Tool==PetTool.Brush)StrokeAt(p);last=p;return;}
            if(press.Held&&!doubleClick){
                if(press.Stroking)StrokeAt(p);
                else{Point screen=PointToScreen(p);press.Move(screen);if(press.Dragging){controller.Perch.Detach();Vector delta=screen-press.Down;var matrix=source.CompositionTarget.TransformFromDevice;var dip=matrix.Transform(delta);Left=left+dip.X;Top=top+dip.Y;controller.React(PetAction.Drag);}}
            }
            last=p;
        };
        Surface.MouseLeftButtonUp+=delegate(object sender,MouseButtonEventArgs e){
            if(!press.Held)return;bool dragged=press.Dragging;bool tap=!doubleClick&&!press.Stroking&&!press.Tool&&!dragged;Point p=e.GetPosition(this);
            c.ReleaseTool();press.End();Surface.ReleaseMouseCapture();if(dragged){Clamp();controller.Settings.Left=Left+RestOffsetX;controller.Settings.Top=Top+RestOffsetY;controller.Save();}else if(tap&&c.Skin.Living!=null)TouchAt(p,false);
            doubleClick=false;e.Handled=true;
        };
        Surface.MouseRightButtonUp+=delegate(object sender,MouseButtonEventArgs e){if(controller.Life.Play.Active)controller.SelectTool(PetTool.Hand);else if(c.Skin.Living!=null)OpenDesktopActions();else controller.OpenSettings();e.Handled=true;};
        Surface.MouseWheel+=delegate(object sender,MouseWheelEventArgs e){if(c.Skin.Living!=null&&!c.Life.Play.Active&&PetTouchMap.Continuous(Surface.TouchAt(e.GetPosition(this)))){TouchAt(e.GetPosition(this),true);e.Handled=true;}};
        Surface.LostMouseCapture+=delegate{bool dragged=press.Dragging;c.ReleaseTool();press.End();doubleClick=false;if(dragged){Clamp();controller.Settings.Left=Left+RestOffsetX;controller.Settings.Top=Top+RestOffsetY;controller.Save();}};
    }
    public void OpenDesktopActions(){CloseDesktopActions();petting.Suspend();pocket=new DesktopPetPocket(controller);pocket.Open(Surface);}
    public void CloseDesktopActions(){if(pocket!=null){pocket.Close();pocket=null;}}
    public void SuppressPointerPetting(){Native.Point cursor;if(source!=null&&Native.GetCursorPos(out cursor))petting.Suppress(new Point(cursor.X,cursor.Y));}
    void StrokeAt(Point p){if(petting.Observe(Surface.TouchAt(p),PointToScreen(p),controller.Companion.Clock))TouchAt(p,true);}
    IntPtr Hook(IntPtr hwnd,int msg,IntPtr wp,IntPtr lp,ref bool handled) {
        if(msg==0x20){Native.Point cursor;if(Native.GetCursorPos(out cursor)&&Surface.HitPet(PointFromScreen(new Point(cursor.X,cursor.Y)))){Native.SetCursor(Native.LoadCursor(IntPtr.Zero,new IntPtr(32649)));handled=true;return new IntPtr(1);}}
        if(msg==0x21){handled=true;return new IntPtr(3);}
        if(msg==0x312){if(wp.ToInt32()==1)controller.ToggleHidden();if(wp.ToInt32()==2)controller.ToggleQuiet();handled=true;}
        if((uint)msg==Native.ActivateMessage){controller.OpenSettings();handled=true;}
        if(msg==0x84&&!press.Held){long value=lp.ToInt64();Point local=PointFromScreen(new Point((short)(value&0xffff),(short)((value>>16)&0xffff)));if(!Surface.HitPet(local)){handled=true;return new IntPtr(-1);}}
        return IntPtr.Zero;
    }
    void TouchAt(Point p,bool stroking){Rect r=Surface.PetRect;controller.Touch(Surface.TouchAt(p),stroking,((p.X-r.X)/r.Width-.5)*2);}
    public void ObservePointer(Native.Point screen,Point local,double now){
        bool recentPointer=pointerActivity.Observe(new Point(screen.X,screen.Y),now);
        if(controller.Life.Play.Active){bool eligible=source!=null&&(Native.WindowFromPoint(screen)==source.Handle||press.Held&&press.Tool)&&!(pocket!=null&&pocket.IsOpen);controller.ObserveTool(local,eligible);if(controller.Life.Play.Interactive)return;}
        if(press.Held||pocket!=null&&pocket.IsOpen||controller.Skin.Motion.Recovering)return;
        if(source==null||Native.WindowFromPoint(screen)!=source.Handle){petting.Reset();return;}
        if(!recentPointer){petting.Suspend();return;}
        PetTouchZone zone=Surface.TouchAt(local);
        if(petting.Observe(zone,new Point(screen.X,screen.Y),now))TouchAt(local,true);
    }
    public static Point ClampPosition(Point point,Rect work,double width,double height) {
        return new Point(Math.Max(work.Left,Math.Min(work.Right-width,point.X)),Math.Max(work.Top,Math.Min(work.Bottom-height,point.Y)));
    }
    Rect WorkArea() {
        if(source==null)return SystemParameters.WorkArea;
        var pixel=PointToScreen(new Point(Width/2,Height/2));var screen=Forms.Screen.FromPoint(new System.Drawing.Point((int)pixel.X,(int)pixel.Y));var r=screen.WorkingArea;
        var a=PointFromScreen(new Point(r.Left,r.Top));var b=PointFromScreen(new Point(r.Right,r.Bottom));return new Rect(new Point(Left+a.X,Top+a.Y),new Point(Left+b.X,Top+b.Y));
    }
    double RestOffsetX {get{return (Width-360)/2;}}
    double RestOffsetY {get{return Height-300;}}
    Rect WindowInDips(Rect r){Point a=PointFromScreen(r.TopLeft),b=PointFromScreen(r.BottomRight);return new Rect(new Point(Left+a.X,Top+a.Y),new Point(Left+b.X,Top+b.Y));}
    double PerchWidth {get{return controller.Settings.Size*416/256;}}
    double PerchFoot {get{return Surface.PetRect.Top+235*controller.Settings.Size/256;}}
    double PerchLeft {get{return Surface.PetRect.Left-80*controller.Settings.Size/256;}}
    public bool CanPerch {get{Point p;return source!=null&&perchCandidate!=IntPtr.Zero&&WindowPerch.Place(perchBounds,WorkArea(),PerchWidth,315*controller.Settings.Size/256,PerchFoot,PerchLeft,.5,perchBounds.Top-315*controller.Settings.Size/256>=WorkArea().Top+4,out p);}}
    public void ObserveWindowPerch(double now){
        if(source==null||now-perchObserved<.2)return;perchObserved=now;
        var candidate=DesktopWindowProbe.Foreground();Rect rect;
        if(candidate!=IntPtr.Zero&&DesktopWindowProbe.Bounds(candidate,out rect)){perchCandidate=candidate;perchBounds=WindowInDips(rect);}
        if(controller.Perch.Attached){Point position;if(DesktopWindowProbe.Bounds(controller.Perch.Window,out rect)&&controller.Perch.Follow(WindowInDips(rect),WorkArea(),PerchWidth,315*controller.Settings.Size/256,PerchFoot,PerchLeft,out position)){Left=position.X;Top=position.Y;}else controller.Perch.Detach();}
    }
    public bool AttachPerch(){Point p;Rect bounds;if(!DesktopWindowProbe.Bounds(perchCandidate,out bounds))return false;perchBounds=WindowInDips(bounds);if(!controller.Perch.Attach(perchCandidate,perchBounds,WorkArea(),new Point(Left,Top),PerchWidth,315*controller.Settings.Size/256,PerchFoot,PerchLeft,out p))return false;Left=p.X;Top=p.Y;controller.StartHabit(CatHabit.CurlSleep);controller.Companion.Remark("就在这里，陪你忙一会儿。");return true;}
    public void LeavePerch(){Point p=controller.Perch.Original;controller.Perch.Detach();Left=p.X;Top=p.Y;Clamp();controller.Companion.Remark("回到桌面，继续陪着你。");}
    public void Clamp(){Point p=ClampPosition(new Point(Left+RestOffsetX,Top+RestOffsetY),WorkArea(),360,300);Left=p.X-RestOffsetX;Top=p.Y-RestOffsetY;}
    MotionClip travelClip;
    double travelPosition,travelPositionY;
    public bool HasWalkingRoom(double logicalDistance,double minY=0,double maxY=0){var area=WorkArea();double scale=controller.Settings.Size/256,destination=Left+RestOffsetX+logicalDistance*scale;return destination>=area.Left+4&&destination+360<=area.Right-4&&Top+minY*scale>=area.Top+4&&Top+Surface.PetRect.Top+(235+maxY)*scale<=area.Bottom-4;}
    public bool HasJumpingRoom(double logicalTop){return !double.IsNaN(logicalTop)&&!double.IsInfinity(logicalTop)&&Top+Surface.PetRect.Top+logicalTop*controller.Settings.Size/256>=WorkArea().Top+4;}
    public void MoveForPlay(double delta) {
        if(controller.Perch.Attached)return;
        if(controller.Skin.Living!=null){
            var player=controller.Skin.Motion;
            if(!player.Active||player.Clip.Spec.travelX==null){travelClip=null;travelPosition=travelPositionY=0;return;}
            if(travelClip!=player.Clip){travelClip=player.Clip;travelPosition=travelPositionY=0;}
            double target=player.Displacement,targetY=player.DisplacementY,step=target-travelPosition,stepY=targetY-travelPositionY;travelPosition=target;travelPositionY=targetY;
            if(!press.Held&&(controller.Settings.Mode!=PetMode.Quiet||player.Recovering)&&(Math.Abs(step)>0||Math.Abs(stepY)>0)){Left+=step*controller.Settings.Size/256;Top+=stepY*controller.Settings.Size/256;var area=WorkArea();Left=Math.Max(area.Left+4-RestOffsetX,Math.Min(area.Right-360-4-RestOffsetX,Left));double maxTop=area.Bottom-4-Surface.PetRect.Top-235*controller.Settings.Size/256;Top=Math.Max(area.Top+4,Math.Min(maxTop,Top));}
            return;
        }
        if(press.Held||controller.Settings.Mode==PetMode.Quiet)return;
        var e=controller.Engine;bool chase=e.Action==PetAction.Tease;bool roam=e.Mode==PetMode.Free&&e.Action==PetAction.Idle;
        if(chase){Native.Point cursor;if(Native.GetCursorPos(out cursor)){Point local=PointFromScreen(new Point(cursor.X,cursor.Y));double target=local.X-Width/2;double speed=e.Personality==Personality.Shy?45:e.Personality==Personality.Aloof?65:105;if(Math.Abs(target)>40){Left+=Math.Sign(target)*Math.Min(Math.Abs(target),delta*speed);Clamp();}}}
        else if(roam&&e.Clock-lastWalk>12){Left+=Math.Sin(e.Clock*.2)*18*delta;Clamp();if(e.Clock-lastWalk>17)lastWalk=e.Clock;}
    }
}
public sealed class PetSurface:FrameworkElement {
    private readonly PetController c;
    private byte[] hitPixels;private int hitFrame=-1;
    public PetSurface(PetController controller){c=controller;Cursor=Cursors.Hand;}
    protected override void OnQueryCursor(QueryCursorEventArgs e){e.Cursor=HitPet(Mouse.GetPosition(this))?Cursors.Hand:Cursors.Arrow;e.Handled=true;base.OnQueryCursor(e);}
    public Rect PetRect {get{double size=c.Settings.Size;return new Rect((ActualWidth-size)/2,ActualHeight-size-14,size,size);}}
    public Rect ToolRect {get{Rect r=PetRect;double top=r.Top;r.Inflate(c.Life.Play.Interactive?85:28,c.Life.Play.Interactive?48:25);if(c.Life.Play.Tool==PetTool.Feather){double reach=top-130*c.Settings.Size/256;r=new Rect(r.Left,Math.Min(r.Top,reach),r.Width,r.Bottom-Math.Min(r.Top,reach));}r.Intersect(new Rect(5,55,Math.Max(0,ActualWidth-10),Math.Max(0,ActualHeight-60)));return r;}}
    public bool InHead(Point p){Rect r=PetRect;return new Rect(r.Left+r.Width*.24,r.Top+r.Height*.18,r.Width*.52,r.Height*.43).Contains(p);}
    Point ToyCenter {get{Rect r=PetRect;return new Point(r.X+r.Width*(.5+c.Companion.Signals.ToyX*.34),r.Bottom-12-(c.Companion.Signals.ToyY-.45)*65);}}
    bool OnToy(Point p){return c.Skin.Living!=null&&c.Companion.Signals.Play>.1&&(p-ToyCenter).Length<12;}
    public CompanionGesture GestureAt(Point p){
        if(c.Skin.Motion.Active)return CompanionGesture.None;
        if(OnToy(p))return CompanionGesture.Play;Rect r=PetRect;
        var source=c.Skin.Living.MapToSource(new Point((p.X-r.X)/r.Width*256,(p.Y-r.Y)/r.Height*256));
        if((source-c.Skin.Living.Nose).Length<14)return CompanionGesture.Boop;
        if((source-new Point(106,219)).Length<20||(source-new Point(160,219)).Length<20)return CompanionGesture.Paw;
        return CompanionGesture.None;
    }
    public PetTouchZone TouchAt(Point p){
        if(OnToy(p))return PetTouchZone.Toy;if(c.Skin.Living==null||!HitBody(p))return PetTouchZone.None;
        Rect r=PetRect;Point point=new Point((p.X-r.X)/r.Width*256,(p.Y-r.Y)/r.Height*256);
        Point nose=c.Skin.Motion.Active?c.Skin.Motion.TouchNose:c.Skin.Living.Nose;
        if(!c.Skin.Motion.Active)point=c.Skin.Living.MapToSource(point);
        return PetTouchMap.At(point,nose);
    }
    public void ClearHitCache(){hitPixels=null;hitFrame=-1;}
    public bool HitPet(Point p){return c.Life.Play.Active&&ToolRect.Contains(p)||HitBody(p);}
    public bool HitBody(Point p){
        if(OnToy(p))return true;Rect r=PetRect;Rect hitRect=r;if(c.Skin.Motion.Active){var spec=c.Skin.Motion.VisualSpec;hitRect=new Rect(r.X+spec.originX*r.Width/256,r.Y+spec.originY*r.Height/256,c.Skin.Motion.VisualSize*r.Width/256,c.Skin.Motion.VisualSize*r.Height/256);}if(!hitRect.Contains(p))return false;int frame=c.Engine.Frame();
        if(c.Skin.Living!=null){int xx=(int)((p.X-r.X)/r.Width*256),yy=(int)((p.Y-r.Y)/r.Height*256);return c.Skin.Motion.Active?c.Skin.Motion.Hit(xx,yy):c.Skin.Living.Hit(xx,yy);}
        if(hitPixels==null||frame!=hitFrame){var bitmap=c.Skin.Render(frame,0,null);hitPixels=new byte[256*256*4];bitmap.CopyPixels(hitPixels,256*4,0);hitFrame=frame;}
        int x=Math.Max(0,Math.Min(255,(int)((p.X-r.X)/r.Width*256)));int y=Math.Max(0,Math.Min(255,(int)((p.Y-r.Y)/r.Height*256)));return hitPixels[(y*256+x)*4+3]>18;
    }
    protected override HitTestResult HitTestCore(PointHitTestParameters p){return HitPet(p.HitPoint)?new PointHitTestResult(this,p.HitPoint):null;}
    protected override void OnRender(DrawingContext d){
        base.OnRender(d);Rect r=PetRect;var e=c.Engine;double phase=e.Clock;
        double bob=c.Skin.Living!=null?0:e.Action==PetAction.Tease?Math.Sin(phase*9)*6:e.Action==PetAction.Feed?Math.Sin(phase*7)*2:0;
        r.Y+=bob;d.PushOpacity(c.Skin.Living!=null?1:e.Mode==PetMode.Quiet?.75:1);c.Skin.Draw(d,e.Frame(),phase,r);d.Pop();
        if(c.Skin.Living!=null){DrawCompanion(d,r);return;}
        string message=e.Message(c.Settings.Name);
        if(message.Length>0){var text=new FormattedText(message,System.Globalization.CultureInfo.GetCultureInfo("zh-CN"),FlowDirection.LeftToRight,new Typeface("Microsoft YaHei"),12,new SolidColorBrush(Color.FromRgb(86,67,53)));text.MaxTextWidth=246;
            double width=Math.Min(270,text.Width+24);double yy=Math.Max(8,r.Y-47);d.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(246,255,252,245)),new Pen(new SolidColorBrush(Color.FromRgb(228,217,199)),1),new Rect((ActualWidth-width)/2,yy,width,text.Height+16),12,12);d.DrawText(text,new Point((ActualWidth-width)/2+12,yy+8));}
        if(e.Action==PetAction.Pet){for(int i=0;i<3;i++){double x=r.X+r.Width*(.2+i*.3),y=r.Y+18-Math.Sin(phase*2+i)*10;d.DrawText(new FormattedText("♥",System.Globalization.CultureInfo.InvariantCulture,FlowDirection.LeftToRight,new Typeface("Segoe UI Symbol"),12,new SolidColorBrush(Color.FromRgb(213,136,126))),new Point(x,y));}}
        if(e.Action==PetAction.Feed){d.DrawEllipse(new SolidColorBrush(Color.FromRgb(178,130,88)),null,new Point(r.X+r.Width*.6,r.Bottom-13),15,4);}
        if(e.Action==PetAction.Tease){double tx=r.X+r.Width*.8+Math.Sin(phase*4)*18,ty=r.Y+r.Height*.4;d.DrawLine(new Pen(new SolidColorBrush(Color.FromRgb(160,119,85)),2),new Point(tx+22,ty-45),new Point(tx,ty));d.DrawEllipse(new SolidColorBrush(Color.FromRgb(195,139,142)),null,new Point(tx,ty),7,13);}
    }
    void DrawCompanion(DrawingContext d,Rect r){
        var friend=c.Companion;
        if(c.Life.Play.Active)DrawTool(d,r);
        if(friend.Gesture==CompanionGesture.Treat&&friend.Signals.Bow>.05){
            d.PushOpacity(Math.Min(1,friend.Signals.Bow*2));Point fish=new Point(r.X+r.Width*.64,r.Bottom-12);d.DrawEllipse(new SolidColorBrush(Color.FromRgb(205,163,112)),null,fish,10,5);var tail=new StreamGeometry();using(var g=tail.Open()){g.BeginFigure(new Point(fish.X+7,fish.Y),true,true);g.LineTo(new Point(fish.X+15,fish.Y-6),true,false);g.LineTo(new Point(fish.X+15,fish.Y+6),true,false);}d.DrawGeometry(new SolidColorBrush(Color.FromRgb(205,163,112)),null,tail);d.DrawEllipse(new SolidColorBrush(Color.FromRgb(102,84,63)),null,new Point(fish.X-4,fish.Y-1),1,1);d.Pop();
        }
        if(friend.Signals.Play>.01){
            Point p=ToyCenter;d.PushOpacity(friend.Signals.Play);d.DrawEllipse(new SolidColorBrush(Color.FromArgb(28,55,65,80)),null,new Point(p.X,p.Y+10),11,2);
            d.DrawEllipse(new RadialGradientBrush(Color.FromRgb(199,214,222),Color.FromRgb(116,147,163)),null,p,10,10);
            var thread=new Pen(new SolidColorBrush(Color.FromArgb(180,232,239,242)),1);
            for(int i=-1;i<=1;i++){double shift=i*4;d.DrawLine(thread,new Point(p.X-7,p.Y+shift-3),new Point(p.X+7,p.Y+shift+3));}
            d.DrawLine(new Pen(new SolidColorBrush(Color.FromRgb(137,159,168)),1),new Point(p.X+8,p.Y+5),new Point(p.X+23,p.Y+8));d.Pop();
        }
        string message=friend.Line;if(message.Length==0)return;
        var text=new FormattedText(message,System.Globalization.CultureInfo.GetCultureInfo("zh-CN"),FlowDirection.LeftToRight,new Typeface("Microsoft YaHei"),12,new SolidColorBrush(Color.FromRgb(78,80,72)));text.MaxTextWidth=246;
        double width=Math.Min(270,text.Width+24),yy=Math.Max(8,r.Y-text.Height-25);
        d.PushOpacity(friend.LineOpacity);d.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(246,255,253,246)),new Pen(new SolidColorBrush(Color.FromRgb(224,225,214)),1),new Rect((ActualWidth-width)/2,yy,width,text.Height+16),12,12);d.DrawText(text,new Point((ActualWidth-width)/2+12,yy+8));d.Pop();
    }
    void DrawTool(DrawingContext d,Rect r){
        var play=c.Life.Play;string label=play.Tool==PetTool.Feather?"晃一晃羽毛，靠近前爪试试 · 右键放下":play.Tool==PetTool.Yarn?"拖住球，松手抛出 · 点击方向滚球 · 右键放下":"小毛刷 · 顺着毛慢慢摸，右键放下";
        if(play.Interactive&&c.Skin.Motion.Active&&(!c.Skin.Motion.HoldingPose||c.Skin.Motion.Clip.Spec.id!="sit"))label="正在转身坐好，玩具已拿起 · 右键放下";
        var text=new FormattedText(label,System.Globalization.CultureInfo.GetCultureInfo("zh-CN"),FlowDirection.LeftToRight,new Typeface("Microsoft YaHei"),10,new SolidColorBrush(Color.FromRgb(103,112,102)));d.DrawText(text,new Point(Math.Max(8,(ActualWidth-text.Width)/2),c.Skin.Motion.PostureActivity=="sit-jump"?5:Math.Max(5,r.Top-45)));
        if(play.Tool==PetTool.Yarn){DrawYarn(d,r);return;}
        if(!play.PointerVisible)return;Point p=new Point(r.X+r.Width*play.Pointer.X/256,r.Y+r.Height*play.Pointer.Y/256);
        if(play.Tool==PetTool.Feather){d.DrawLine(new Pen(new SolidColorBrush(Color.FromRgb(139,111,83)),2.5),new Point(p.X+21,p.Y-36),new Point(p.X+8,p.Y-10));d.DrawLine(new Pen(new SolidColorBrush(Color.FromRgb(174,185,183)),1),new Point(p.X+8,p.Y-10),p);d.PushTransform(new RotateTransform(-25,p.X,p.Y));d.DrawEllipse(new SolidColorBrush(Color.FromRgb(129,169,181)),new Pen(new SolidColorBrush(Color.FromRgb(102,140,152)),.7),p,6,12);d.DrawLine(new Pen(new SolidColorBrush(Color.FromRgb(224,234,233)),.8),new Point(p.X,p.Y-10),new Point(p.X,p.Y+10));d.Pop();}
        else {d.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(169,139,107)),null,new Rect(p.X-3,p.Y-22,6,17),2,2);d.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(218,194,161)),new Pen(new SolidColorBrush(Color.FromRgb(160,135,108)),.7),new Rect(p.X-11,p.Y-9,22,12),4,4);for(int i=-3;i<=3;i++)d.DrawLine(new Pen(new SolidColorBrush(Color.FromRgb(116,111,99)),1),new Point(p.X+i*2.4,p.Y+2),new Point(p.X+i*2.4,p.Y+7));}
    }
    void DrawYarn(DrawingContext d,Rect r){
        var play=c.Life.Play;var ball=play.Ball;double scale=r.Width/256;
        Point p=new Point(r.X+ball.Position.X*scale,r.Y+ball.Position.Y*scale);double radius=11*scale;
        double height=Math.Max(0,225-ball.Position.Y),shadow=Math.Max(.2,1-height/220);
        d.DrawEllipse(new SolidColorBrush(Color.FromArgb((byte)(30*shadow),55,65,80)),null,new Point(p.X,r.Y+237*scale),radius*(1+.2*shadow),2.4*scale);
        d.PushTransform(new RotateTransform(ball.Angle,p.X,p.Y));
        d.DrawEllipse(new RadialGradientBrush(Color.FromRgb(218,181,138),Color.FromRgb(167,112,79)),new Pen(new SolidColorBrush(Color.FromRgb(151,106,79)),.6),p,radius,radius);
        var thread=new Pen(new SolidColorBrush(Color.FromArgb(190,246,218,180)),.85*scale);
        for(int i=-1;i<=1;i++)d.DrawGeometry(null,thread,Geometry.Parse(string.Format(System.Globalization.CultureInfo.InvariantCulture,"M {0},{1} Q {2},{3} {4},{5}",p.X-radius*.75,p.Y+i*radius*.42,p.X,p.Y+i*radius*.42-radius*.4,p.X+radius*.75,p.Y+i*radius*.42+radius*.15)));
        d.Pop();d.DrawGeometry(null,new Pen(new SolidColorBrush(Color.FromRgb(176,128,92)),scale),Geometry.Parse(string.Format(System.Globalization.CultureInfo.InvariantCulture,"M {0},{1} Q {2},{3} {4},{5}",p.X+radius*.7,p.Y+radius*.6,p.X+radius*1.7,p.Y+radius*1.7,p.X+radius*2.1,p.Y+radius*.6)));
        if(play.ContactGlow>0){double glow=play.ContactGlow;var pen=new Pen(new SolidColorBrush(Color.FromArgb((byte)(150*glow),137,163,115)),1.5);for(int i=0;i<3;i++){double x=p.X+(i-1)*8,y=p.Y-radius-5;d.DrawLine(pen,new Point(x,y-4*(1-glow)),new Point(x+(i-1)*3,y-6-4*(1-glow)));}}
    }
}
public sealed class SettingsWindow:Window {
    readonly PetController c;readonly Image preview;readonly TextBlock status,petTitle,kind,lifeMood;readonly TextBox name;readonly ComboBox personality;Button focus;double previewAt=-999;
    static readonly Brush Ink=BrushOf("#3E403A"),Muted=BrushOf("#818276"),Green=BrushOf("#667660"),Cream=BrushOf("#F5F3EC");
    static Brush BrushOf(string hex){return (Brush)new BrushConverter().ConvertFromString(hex);}
    static TextBlock Text(string s,double size,Brush color){return new TextBlock{Text=s,FontSize=size,Foreground=color,TextWrapping=TextWrapping.Wrap};}
    static Button ButtonOf(string s,Action action,bool primary){var b=new Button{Content=s,Padding=new Thickness(16,9,16,9),Margin=new Thickness(0,0,8,8),Background=primary?Green:Brushes.White,Foreground=primary?Brushes.White:Ink,BorderBrush=primary?Green:BrushOf("#E0E2D8"),BorderThickness=new Thickness(1),Cursor=Cursors.Hand,FontSize=13};b.Click+=delegate{action();};return b;}
    public SettingsWindow(PetController controller){
        c=controller;Title=c.Settings.Name+" · 设置";Width=900;Height=Math.Min(680,SystemParameters.WorkArea.Height-24);MinWidth=800;MinHeight=540;WindowStartupLocation=WindowStartupLocation.CenterScreen;Background=Cream;FontFamily=new FontFamily("Microsoft YaHei");
        var root=new Grid{Margin=new Thickness(32)};Content=root;root.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});root.RowDefinitions.Add(new RowDefinition());root.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});
        var header=new DockPanel{Margin=new Thickness(0,0,0,26)};
        var badge=Text(c.Settings.Name+" · 桌面陪伴",12,Muted);DockPanel.SetDock(badge,Dock.Right);header.Children.Add(badge);
        var heading=new StackPanel();heading.Children.Add(Text("毛孩子桌宠",25,Ink));var sub=Text("把熟悉的小家伙，留在身边。",12,Muted);sub.Margin=new Thickness(0,8,0,0);heading.Children.Add(sub);header.Children.Add(heading);root.Children.Add(header);
        var body=new Grid();body.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(280)});body.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(24)});body.ColumnDefinitions.Add(new ColumnDefinition());var scroller=new ScrollViewer{Content=body,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};Grid.SetRow(scroller,1);root.Children.Add(scroller);
        var card=new Border{Background=Brushes.White,CornerRadius=new CornerRadius(20),Padding=new Thickness(22),BorderBrush=BrushOf("#E6E7DD"),BorderThickness=new Thickness(1)};body.Children.Add(card);
        var left=new StackPanel();card.Child=left;left.Children.Add(Text("YOUR LITTLE COMPANION",10,Muted));preview=new Image{Width=225,Height=225,Margin=new Thickness(0,20,0,0)};left.Children.Add(preview);
        petTitle=Text(c.Settings.Name,23,Ink);petTitle.HorizontalAlignment=HorizontalAlignment.Center;left.Children.Add(petTitle);kind=Text(c.Skin.Description,11,Muted);kind.TextAlignment=TextAlignment.Center;kind.Margin=new Thickness(0,8,0,15);left.Children.Add(kind);
        if(c.Skin.Living!=null){lifeMood=Text(c.Life.MoodLabel,12,Green);lifeMood.TextAlignment=TextAlignment.Center;lifeMood.Margin=new Thickness(0,0,0,8);left.Children.Add(lifeMood);}
        var tip=new Border{Background=Cream,CornerRadius=new CornerRadius(12),Padding=new Thickness(14),Margin=new Thickness(0,10,0,0)};tip.Child=Text("在桌面上摸摸它，或右键宠物拿起玩具。\n按住身体拖动，可以换个位置。",11,Muted);left.Children.Add(tip);
        var right=new StackPanel();Grid.SetColumn(right,2);body.Children.Add(right);
        right.Children.Add(Text("陪伴偏好",18,Ink));
        right.Children.Add(Text("陪伴方式",12,Muted));var modes=new WrapPanel{Margin=new Thickness(0,9,0,12)};modes.Children.Add(ButtonOf("陪伴",delegate{c.Mode(PetMode.Companion);},false));
        if(c.Skin.Living==null)modes.Children.Add(ButtonOf("自由活动",delegate{c.Mode(PetMode.Free);},false));modes.Children.Add(ButtonOf("勿扰",delegate{c.Mode(PetMode.Quiet);},false));right.Children.Add(modes);
        if(c.Skin.Living!=null){
            focus=ButtonOf("陪我专注 25 分钟",c.ToggleFocus,false);right.Children.Add(focus);
            var words=new CheckBox{Content="偶尔说句暖心话",IsChecked=c.Settings.WarmWords,FontSize=12,Margin=new Thickness(0,0,0,14),Foreground=Ink};words.Click+=delegate{c.Settings.WarmWords=words.IsChecked==true;c.Companion.WarmWords=c.Settings.WarmWords;c.Save();};right.Children.Add(words);
        }
        var fields=new Grid{Margin=new Thickness(0,0,0,14)};fields.ColumnDefinitions.Add(new ColumnDefinition());fields.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(15)});fields.ColumnDefinitions.Add(new ColumnDefinition());
        var nameStack=new StackPanel();nameStack.Children.Add(Text("它的名字",12,Muted));name=new TextBox{Text=c.Settings.Name,Padding=new Thickness(8),Margin=new Thickness(0,7,0,0),MaxLength=24,FontSize=13};nameStack.Children.Add(name);fields.Children.Add(nameStack);name.LostFocus+=delegate{c.Settings.Name=PetSettings.CleanName(name.Text);petTitle.Text=c.Settings.Name;c.Save();};
        var moodStack=new StackPanel();Grid.SetColumn(moodStack,2);moodStack.Children.Add(Text("它的小性格",12,Muted));personality=new ComboBox{Margin=new Thickness(0,7,0,0),Padding=new Thickness(6),FontSize=13};personality.Items.Add("黏人 · 主动贴贴");personality.Items.Add("胆小 · 慢慢熟悉");personality.Items.Add("高冷 · 偶尔回应");personality.SelectedIndex=(int)c.Settings.Personality;personality.SelectionChanged+=delegate{c.Settings.Personality=(Personality)personality.SelectedIndex;c.Engine.Personality=c.Settings.Personality;c.Save();};moodStack.Children.Add(personality);fields.Children.Add(moodStack);right.Children.Add(fields);
        right.Children.Add(Text("桌宠大小",12,Muted));var size=new Slider{Minimum=90,Maximum=240,Value=c.Settings.Size,Margin=new Thickness(0,8,0,13)};size.ValueChanged+=delegate{c.Settings.Size=size.Value;c.Pet.Surface.InvalidateVisual();};size.PreviewMouseLeftButtonUp+=delegate{c.Save();};right.Children.Add(size);
        if(!PetSettings.IsPreviewProfile){var startup=new CheckBox{Content="开机时自动陪伴",IsChecked=PetController.IsStartup(),FontSize=12,Margin=new Thickness(0,0,0,12),Foreground=Ink};startup.Click+=delegate{try{PetController.Startup(startup.IsChecked==true);}catch{startup.IsChecked=PetController.IsStartup();MessageBox.Show("暂时无法保存开机设置，请稍后重试。","设置未完成");}};right.Children.Add(startup);}
        var tools=new WrapPanel();if(c.Skin.Living!=null)tools.Children.Add(ButtonOf("我们的相处",c.OpenMemories,false));tools.Children.Add(ButtonOf("导入宠物包",c.ImportPack,false));tools.Children.Add(ButtonOf("导出宠物包",c.ExportPack,false));if(c.Skin.Living==null)tools.Children.Add(ButtonOf("导出表情包",c.ExportGifs,false));right.Children.Add(tools);
        status=Text("",11,Green);status.Margin=new Thickness(0,9,0,0);right.Children.Add(status);
        var footer=new DockPanel{Margin=new Thickness(0,22,0,0)};var hide=ButtonOf("收起面板",delegate{Close();},true);DockPanel.SetDock(hide,Dock.Right);footer.Children.Add(hide);footer.Children.Add(Text("离线运行 · 无广告 · 照片不上传\nCtrl+Alt+P 隐藏/显示    Ctrl+Alt+Q 勿扰    右键猫咪拿玩具",11,Muted));Grid.SetRow(footer,2);root.Children.Add(footer);
        Closed+=delegate{c.Settings.Name=PetSettings.CleanName(name.Text);c.Save();};RefreshStatus();
    }
    public void ReloadPet(){name.Text=c.Settings.Name;petTitle.Text=c.Settings.Name;kind.Text=c.Skin.Description;previewAt=-999;}
    public void RefreshStatus(){
        status.Text="当前："+(c.Settings.Mode==PetMode.Quiet?"勿扰 · 安静陪伴":c.Settings.Mode==PetMode.Free?"自由活动":"陪伴 · 留在附近")+"  /  "+(c.Hidden?"桌宠已隐藏":"桌宠显示中");
        if(c.Skin.Living!=null){status.Text+="\n"+c.Companion.Familiarity+(c.Companion.Touch.Active?" · "+PetTouchMap.Label(c.Companion.Touch.Zone):c.Habits.Active?" · "+c.Habits.Phase:"");if(lifeMood!=null)lifeMood.Text=c.Needs.Feeling+"\n"+c.Life.FavoriteLabel;if(focus!=null){focus.IsEnabled=c.Settings.Mode!=PetMode.Quiet||c.Companion.IsFocusing;focus.Content=c.Companion.IsFocusing?"结束专注 · 剩余 "+TimeSpan.FromSeconds(c.Companion.FocusRemaining).ToString(@"mm\:ss"):"陪我专注 25 分钟";}}
        if(c.Engine.Clock-previewAt>(c.Skin.Living!=null?1.0/20:.18)){previewAt=c.Engine.Clock;preview.Source=c.Skin.Render(c.Engine.Frame(),c.Engine.Clock,null);}
    }
}
}
