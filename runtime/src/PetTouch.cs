using System;
using System.Collections.Generic;
using System.Windows;

namespace MaohaiziPet {
public enum PetTouchZone { None, Head, CheekLeft, CheekRight, Chin, Back, Nose, PawLeft, PawRight, Toy }
public static class PetTouchMap {
    static bool Oval(Point p,Point center,double rx,double ry){return Math.Pow((p.X-center.X)/rx,2)+Math.Pow((p.Y-center.Y)/ry,2)<=1;}
    // Input is in the original 256-pixel character space, after alpha hit testing.
    // Nose follows the current complete pose, so a sleeping face is not mistaken
    // for a paw just because it is close to the floor.
    public static PetTouchZone At(Point p,Point nose){
        if(Oval(p,nose,11,9))return PetTouchZone.Nose;
        if(Oval(p,new Point(nose.X,nose.Y+19),24,13))return PetTouchZone.Chin;
        if(Oval(p,new Point(nose.X-29,nose.Y-1),17,22))return PetTouchZone.CheekLeft;
        if(Oval(p,new Point(nose.X+29,nose.Y-1),17,22))return PetTouchZone.CheekRight;
        if(p.Y<nose.Y-10&&Math.Abs(p.X-nose.X)<49&&p.Y>nose.Y-78)return PetTouchZone.Head;
        if(p.Y>=208)return p.X<nose.X?PetTouchZone.PawLeft:PetTouchZone.PawRight;
        return PetTouchZone.Back;
    }
    public static bool Continuous(PetTouchZone zone){return zone==PetTouchZone.Head||zone==PetTouchZone.Chin||zone==PetTouchZone.Back||zone==PetTouchZone.CheekLeft||zone==PetTouchZone.CheekRight;}
    public static string Label(PetTouchZone zone){return zone==PetTouchZone.Head?"摸摸头":zone==PetTouchZone.Chin?"挠挠下巴":zone==PetTouchZone.CheekLeft||zone==PetTouchZone.CheekRight?"蹭蹭脸颊":zone==PetTouchZone.Back?"顺顺背毛":zone==PetTouchZone.Nose?"碰鼻尖":zone==PetTouchZone.PawLeft?"碰碰左爪":zone==PetTouchZone.PawRight?"碰碰右爪":"";}
}
// Slow pointer motion counts as petting. A stationary pointer may greet once,
// but it cannot repeatedly trigger nose/paw reactions or restart animations.
public sealed class PettingTracker {
    PetTouchZone zone;
    Point previous;
    Point contactPoint;
    double entered,lastContact,travel;
    bool greeted;
    bool pointerLocked;
    public void Reset(){Suspend();pointerLocked=false;}
    public void Suspend(){zone=PetTouchZone.None;greeted=false;travel=0;}
    public void Suppress(Point p){Suspend();contactPoint=p;pointerLocked=true;}
    public bool Observe(PetTouchZone current,Point p,double now){
        if(current==PetTouchZone.None){Reset();return false;}
        if(pointerLocked){if((p-contactPoint).Length<3)return false;pointerLocked=false;}
        if(current!=zone){zone=current;entered=now;previous=p;greeted=false;travel=0;return false;}
        travel+=Math.Min(30,(p-previous).Length);previous=p;
        if(!greeted&&((now-entered>=.12&&travel>=3)||now-entered>=.45)||greeted&&PetTouchMap.Continuous(zone)&&now-lastContact>=.12&&travel>=3){greeted=true;lastContact=now;travel=0;contactPoint=p;pointerLocked=true;return true;}
        return false;
    }
}
// A cat walking under an idle cursor is not a stroke from the user.
public sealed class PetPointerActivity {
    Point previous;
    bool seen;
    double movedAt;
    public bool Observe(Point screen,double clock){if(!seen||(screen-previous).Length>=.4){previous=screen;movedAt=clock;seen=true;}return clock-movedAt<=1;}
}
public sealed class TouchFeedback {
    public PetTouchZone Zone {get;private set;}
    public double Clock {get;private set;}
    public bool Active {get{return Clock<until;}}
    double until,start,side;
    readonly double[] amounts=new double[10];
    readonly Dictionary<PetTouchZone,double> discreteStarts=new Dictionary<PetTouchZone,double>();
    public bool Contact(PetTouchZone zone,bool stroking,double direction){
        if(!Enum.IsDefined(typeof(PetTouchZone),zone)||zone==PetTouchZone.None||zone==PetTouchZone.Toy||double.IsNaN(direction)||double.IsInfinity(direction))return false;
        if(!PetTouchMap.Continuous(zone)&&discreteStarts.ContainsKey(zone)&&Clock-discreteStarts[zone]<2.2)return false;
        bool continuing=Active&&Zone==zone;
        if(!continuing)start=Clock;
        Zone=zone;side=Math.Max(-1,Math.Min(1,direction));until=Clock+(PetTouchMap.Continuous(zone)?(stroking?1.15:2.3):1.7);
        if(!PetTouchMap.Continuous(zone))discreteStarts[zone]=Clock;
        return true;
    }
    public void Clear(){until=Clock;Zone=PetTouchZone.None;}
    public void Tick(double seconds,bool quiet){
        double dt=Math.Max(0,Math.Min(86400,seconds));Clock+=dt;if(quiet)Clear();
        for(int i=1;i<amounts.Length;i++){double target=Active&&(int)Zone==i?1:0;if(Zone==PetTouchZone.Nose&&Clock-start>.8)target=0;amounts[i]+=(target-amounts[i])*(1-Math.Exp(-dt*(target>0?13:5)));if(quiet)amounts[i]=0;}
    }
    public void Apply(CompanionSignals s){s.TouchHead=amounts[(int)PetTouchZone.Head];s.TouchChin=amounts[(int)PetTouchZone.Chin];s.TouchCheek=Math.Min(1,amounts[(int)PetTouchZone.CheekLeft]+amounts[(int)PetTouchZone.CheekRight]);s.TouchSide=Math.Max(-1,Math.Min(1,amounts[(int)PetTouchZone.CheekLeft]*-1+amounts[(int)PetTouchZone.CheekRight]+amounts[(int)PetTouchZone.Back]*side*.45));s.TouchBack=amounts[(int)PetTouchZone.Back];s.TouchNose=amounts[(int)PetTouchZone.Nose];s.TouchPawLeft=amounts[(int)PetTouchZone.PawLeft];s.TouchPawRight=amounts[(int)PetTouchZone.PawRight];}
}
}
