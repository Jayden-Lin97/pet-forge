using System;
using System.Windows;

namespace MaohaiziPet {
// Physics is confined to the pet's temporary play area, never the whole desktop.
public sealed class YarnBall {
    public Point Position {get;private set;}
    public Vector Velocity {get;private set;}
    public bool Held {get;private set;}
    public double Angle {get;private set;}
    public int Bats {get;private set;}
    double movedAt;
    const double Floor=225;
    static double Clamp(double v,double a,double b){return Math.Max(a,Math.Min(b,v));}
    static bool Valid(Point p){return !double.IsNaN(p.X)&&!double.IsNaN(p.Y)&&!double.IsInfinity(p.X)&&!double.IsInfinity(p.Y);}
    public YarnBall(){Reset();}
    public void Reset(){Position=new Point(190,Floor);Velocity=new Vector();Held=false;Angle=0;Bats=0;}
    public void Press(Point p,double time){
        if(!Valid(p))return;
        if((p-Position).Length<=28){Held=true;Velocity=new Vector();movedAt=time;}
        else {double direction=p.X<Position.X?-1:1;Velocity=new Vector(direction*145,-65);}
    }
    public void Move(Point p,double time){
        if(!Held||!Valid(p))return;var next=new Point(Clamp(p.X,-85,341),Clamp(p.Y,-25,Floor));
        double dt=time-movedAt;if(dt>.001){Velocity=(next-Position)/Math.Max(.016,dt);if(Velocity.Length>440){var v=Velocity;v.Normalize();Velocity=v*440;}}
        Position=next;movedAt=time;
    }
    public void Release(double time){if(!Held)return;Held=false;if(time-movedAt>.15)Velocity=new Vector();}
    public bool Bat(Point paw){
        if(Held||!Valid(paw)||(Position-paw).Length>30)return false;
        double direction=Position.X>=paw.X?1:-1;Velocity=new Vector(direction*(145+Bats%3*18),-100);Bats++;return true;
    }
    public void Tick(double seconds){
        if(Held)return;double remaining=Math.Max(0,Math.Min(.5,seconds));
        while(remaining>0){double dt=Math.Min(1.0/120,remaining);remaining-=dt;var v=Velocity;v.Y+=570*dt;var p=Position+v*dt;
            if(p.Y>=Floor){p.Y=Floor;if(v.Y>30)v.Y=-v.Y*.48;else v.Y=0;v.X*=Math.Exp(-dt*3.2);}
            else v.X*=Math.Exp(-dt*.15);
            if(p.X<-85){p.X=-85;v.X=Math.Abs(v.X)*.58;}if(p.X>341){p.X=341;v.X=-Math.Abs(v.X)*.58;}
            if(p.Y<-25){p.Y=-25;v.Y=Math.Abs(v.Y)*.5;}
            if(Math.Abs(v.X)<.6)v.X=0;Angle+=v.X*dt/11*180/Math.PI;Position=p;Velocity=v;
        }
    }
}
}
