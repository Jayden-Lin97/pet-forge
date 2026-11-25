using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace MaohaiziPet {
public enum PetTool { Hand, Feather, Brush, Yarn }
// Only real local interactions are recorded. No invented trips or absence debt.
public sealed class PetDay {
    public string Date="";
    public int Touches,Games,Treats;
}
public sealed class PetLifeRecord {
    public string Started="";
    public double TogetherSeconds;
    public int Touches,Games,Treats;
    public int[] TouchCounts=new int[10];
    public List<PetDay> Days=new List<PetDay>();
    public void Normalize(){
        if(TouchCounts==null||TouchCounts.Length!=10)TouchCounts=new int[10];
        for(int i=0;i<TouchCounts.Length;i++)TouchCounts[i]=Math.Max(0,Math.Min(1000000,TouchCounts[i]));
        Touches=Math.Max(0,Math.Min(1000000,Touches));Games=Math.Max(0,Math.Min(1000000,Games));Treats=Math.Max(0,Math.Min(1000000,Treats));
        if(double.IsNaN(TogetherSeconds)||double.IsInfinity(TogetherSeconds)||TogetherSeconds<0)TogetherSeconds=0;TogetherSeconds=Math.Min(315360000, TogetherSeconds);
        if(Days==null)Days=new List<PetDay>();Days=Days.Where(d=>d!=null&&d.Date!=null&&d.Date.Length==10).GroupBy(d=>d.Date).Select(g=>g.First()).OrderBy(d=>d.Date,StringComparer.Ordinal).Reverse().Take(14).ToList();
        foreach(var day in Days){day.Touches=Math.Max(0,Math.Min(1000000,day.Touches));day.Games=Math.Max(0,Math.Min(1000000,day.Games));day.Treats=Math.Max(0,Math.Min(1000000,day.Treats));}
    }
}
public sealed class PetPlaySession {
    public PetTool Tool {get;private set;}
    public bool Active {get{return Tool!=PetTool.Hand;}}
    public bool Interactive {get{return Tool==PetTool.Feather||Tool==PetTool.Yarn;}}
    public readonly YarnBall Ball=new YarnBall();
    public Func<bool,bool> RequestPaw;
    public Func<Point,bool> RequestJump;
    public int Jumps {get;private set;}
    public Point Target {get{return Tool==PetTool.Yarn?Ball.Position:Pointer;}}
    public bool TargetVisible {get{return Tool==PetTool.Yarn||PointerVisible;}}
    public int Contacts {get;private set;}
    public double ContactGlow {get{return Math.Max(0,1-(clock-lastContact)/.5);}}
    public bool PointerVisible {get;private set;}
    public Point Pointer {get;private set;}
    public double Remaining {get;private set;}
    public int Swats {get;private set;}
    public bool CompletedGame {get;private set;}
    double clock,lastMove=-100,lastSwat=-100,swatStart=-100,lastContact=-100,lastJump=-100,travel;
    Point previous;
    bool hasPrevious,leftPaw,contactPending,jumpArmed=true;
    public void Select(PetTool tool){Tool=tool;Remaining=tool==PetTool.Hand?0:45;PointerVisible=false;hasPrevious=false;travel=0;Swats=Contacts=Jumps=0;CompletedGame=false;swatStart=lastSwat=lastContact=lastJump=-100;contactPending=false;jumpArmed=true;Ball.Reset();}
    public void Stop(){Select(PetTool.Hand);}
    public void Tick(double seconds){double dt=Math.Max(0,Math.Min(86400,seconds));clock+=dt;if(Active){Remaining=Math.Max(0,Remaining-dt);if(Remaining==0)Stop();else if(Tool==PetTool.Yarn)Ball.Tick(dt);}if(clock-lastMove>.3)PointerVisible=false;}
    public bool Observe(Point canonical,bool eligible){
        return Observe(canonical,eligible,true);
    }
    public bool Observe(Point canonical,bool eligible,bool canReact){
        if(!Active||!eligible||double.IsNaN(canonical.X)||double.IsNaN(canonical.Y)||double.IsInfinity(canonical.X)||double.IsInfinity(canonical.Y)){PointerVisible=false;hasPrevious=false;return false;}
        Pointer=canonical;PointerVisible=true;lastMove=clock;
        if(canonical.Y>=165)jumpArmed=true;
        if(!hasPrevious){previous=canonical;hasPrevious=true;return false;}
        travel+=Math.Min(60,(canonical-previous).Length);previous=canonical;
        return TryReach(canReact);
    }
    public bool TryReach(bool canReact){
        if(!Interactive||!canReact||!TargetVisible||Tool==PetTool.Yarn&&Ball.Held||Tool==PetTool.Feather&&travel<8||clock-lastSwat<(RequestPaw==null?1.5:3.1))return false;
        Point aim=Target;
        if(Tool==PetTool.Feather&&aim.Y<155){
            if(!jumpArmed||RequestJump==null||aim.Y< -130||aim.Y>145||Math.Abs(aim.X-140)>65||travel<12||clock-lastJump<6.5)return false;
            if(!RequestJump(aim))return false;
            travel=0;lastJump=lastSwat=clock;Jumps++;contactPending=true;jumpArmed=false;return false;
        }
        double left=(aim-new Point(106,214)).Length,right=(aim-new Point(160,214)).Length;
        if(Math.Min(left,right)>58||aim.Y<155)return false;
        bool paw=left<right;if(RequestPaw!=null&&!RequestPaw(paw))return false;
        travel=0;leftPaw=paw;swatStart=lastSwat=clock;Swats++;contactPending=true;
        if(RequestPaw==null&&Swats>=3&&!CompletedGame){CompletedGame=true;return true;}return false;
    }
    public bool Contact(Point? paw){
        if(!Interactive||!contactPending||!paw.HasValue||!TargetVisible||(Target-paw.Value).Length>30)return false;
        if(Tool==PetTool.Yarn&&!Ball.Bat(paw.Value))return false;
        contactPending=false;Contacts++;lastContact=clock;
        if(Contacts>=3&&!CompletedGame){CompletedGame=true;return true;}return false;
    }
    public void Press(Point canonical){if(Tool==PetTool.Yarn)Ball.Press(canonical,clock);}
    public void Move(Point canonical){if(Tool==PetTool.Yarn)Ball.Move(canonical,clock);}
    public void Release(){Ball.Release(clock);}
    public void Apply(CompanionSignals s){
        s.PointerToy=Interactive&&TargetVisible?1:0;
        s.PointerToyX=Math.Max(-1,Math.Min(1,(Target.X-140)/85));s.PointerToyY=Math.Max(-.8,Math.Min(.8,(Target.Y-110)/120));
        double age=clock-swatStart,u=age>=0&&age<.65?Math.Sin(age/.65*Math.PI):0;
        s.PointerPawLeft=Active&&leftPaw?u:0;s.PointerPawRight=Active&&!leftPaw?u:0;
    }
}
public sealed class PetLife {
    public readonly PetLifeRecord Record;
    public readonly PetPlaySession Play=new PetPlaySession();
    public bool Dirty {get;private set;}
    public int Mood {get;private set;} // 0 calm, 1 content, 2 playful
    public string MoodLabel {get{return Mood==2?"玩得很开心":Mood==1?"舒服又安心":"安静陪着你";}}
    public double Clock {get;private set;}
    double moodUntil,lastTouch=-1000,lastTreat=-1000;
    readonly Dictionary<PetTouchZone,double> lastZone=new Dictionary<PetTouchZone,double>();
    DateTime today;
    public PetLife(PetLifeRecord record,DateTime date){Record=record??new PetLifeRecord();Record.Normalize();today=date.Date;if(string.IsNullOrEmpty(Record.Started)){Record.Started=date.ToString("yyyy-MM-dd");Dirty=true;}}
    PetDay Day(DateTime date){string key=date.ToString("yyyy-MM-dd");var day=Record.Days.FirstOrDefault(d=>d.Date==key);if(day==null){day=new PetDay{Date=key};Record.Days.Insert(0,day);if(Record.Days.Count>14)Record.Days.RemoveRange(14,Record.Days.Count-14);}return day;}
    public void Tick(double seconds,bool visible,bool quiet,DateTime date){double dt=Math.Max(0,Math.Min(86400,seconds));Clock+=dt;today=date.Date;Play.Tick(dt);if(quiet)Play.Stop();if(visible){Record.TogetherSeconds+=Math.Min(2,dt);Dirty=true;}if(Clock>=moodUntil)Mood=0;}
    public void MarkSaved(){Dirty=false;}
    public void Touch(PetTouchZone zone){
        if(!PetTouchMap.Continuous(zone))return;
        Mood=1;moodUntil=Clock+40;
        if(!lastZone.ContainsKey(zone)||Clock-lastZone[zone]>=15){lastZone[zone]=Clock;Record.TouchCounts[(int)zone]=Math.Min(1000000,Record.TouchCounts[(int)zone]+1);Dirty=true;}
        if(Clock-lastTouch<10)return;lastTouch=Clock;Record.Touches=Math.Min(1000000,Record.Touches+1);Day(today).Touches++;Dirty=true;
    }
    public bool CanTreat {get{return Clock-lastTreat>=10;}}
    public bool Treat(){if(!CanTreat)return false;lastTreat=Clock;Record.Treats=Math.Min(1000000,Record.Treats+1);Day(today).Treats++;Mood=1;moodUntil=Clock+60;Dirty=true;return true;}
    public void Game(){Record.Games=Math.Min(1000000,Record.Games+1);Day(today).Games++;Mood=2;moodUntil=Clock+80;Dirty=true;}
    public PetTouchZone Favorite {get{PetTouchZone result=PetTouchZone.None;int highest=2;foreach(var zone in new[]{PetTouchZone.Head,PetTouchZone.Chin,PetTouchZone.Back,PetTouchZone.CheekLeft,PetTouchZone.CheekRight})if(Record.TouchCounts[(int)zone]>highest){highest=Record.TouchCounts[(int)zone];result=zone;}return result;}}
    public string FavoriteLabel {get{return Favorite==PetTouchZone.None?"还在慢慢熟悉你的摸法":"最喜欢你"+PetTouchMap.Label(Favorite);}}
    public string TogetherLabel {get{int minutes=(int)(Record.TogetherSeconds/60);return minutes<60?"一起待了 "+minutes+" 分钟":"一起待了 "+(minutes/60)+" 小时 "+(minutes%60)+" 分钟";}}
    public string Summary {get{return TogetherLabel+" · 摸摸 "+Record.Touches+" 次 · 玩耍 "+Record.Games+" 轮 · 零食 "+Record.Treats+" 份";}}
    public string TodayNote {get{var day=Record.Days.FirstOrDefault(d=>d.Date==today.ToString("yyyy-MM-dd"));return day==null?"今天也在这里，陪你慢慢过。":"今天被摸摸 "+day.Touches+" 次，陪你玩了 "+day.Games+" 轮，收到 "+day.Treats+" 份小零食。";}}
    public string Memories {get{return (Record.Touches>0?"♡ 已经有了第一次摸摸":"♡ 等一次温柔的摸摸")+(Record.Games>0?"\n♡ 一起玩过逗猫棒":"\n♡ 等你拿起逗猫棒")+(Favorite!=PetTouchZone.None?"\n♡ 已经记住喜欢的摸法":"\n♡ 还在认识彼此");}}
    public void Apply(CompanionSignals s,PetTouchZone touching){if(touching==Favorite&&Favorite!=PetTouchZone.None){double strength=Math.Max(s.TouchHead,Math.Max(s.TouchChin,Math.Max(s.TouchCheek,s.TouchBack)));s.Nuzzle=Math.Max(s.Nuzzle,strength*.25);}Play.Apply(s);}
}
}
