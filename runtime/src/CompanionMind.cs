using System;

namespace MaohaiziPet {
public enum CompanionGesture { None, Nuzzle, Boop, Paw, Play, Treat, Comfort, Greet }
public sealed class CompanionSignals {
    public double Nuzzle, Curiosity, Wink, PawLeft, PawRight, Knead, Bow, SlowBlink, Play, ToyX, ToyY;
    public double Yawn,FrontStretch,Groom,RollOver,Hunt;
    public double Rest,LookWeight,LookX,LookY;
    public double TouchHead,TouchChin,TouchCheek,TouchBack,TouchNose,TouchPawLeft,TouchPawRight,TouchSide;
    public double PointerToy,PointerToyX,PointerToyY,PointerPawLeft,PointerPawRight;
}
// Local companionship state. No network, attention debt, hunger penalty or streaks.
public sealed class CompanionMind {
    public readonly CompanionSignals Signals=new CompanionSignals();
    public readonly TouchFeedback Touch=new TouchFeedback();
    public double Clock {get;private set;}
    public double Warmth {get;private set;}
    public double FocusRemaining {get;private set;}
    public CompanionGesture Gesture {get;private set;}
    public bool WarmWords=true;
    public Personality Personality;
    public bool IsFocusing {get{return FocusRemaining>0;}}
    public bool Dirty {get;private set;}
    public int Greetings {get;private set;}
    double gestureStart=-100,duration,strokeUntil=-100,strokeTime,lastStroke=-100;
    double lastBond=-100,lastLine=-100,lastGreeting=-1000,lineUntil,nextAffection=80;
    double strokeAcknowledged=-100;
    bool wasAway;
    string line="";
    int turn;
    public CompanionMind(double warmth,Personality personality) {
        Warmth=double.IsNaN(warmth)||double.IsInfinity(warmth)?0:Math.Max(0,Math.Min(100,warmth));Personality=personality;
    }
    static double Clamp(double x,double a,double b){return Math.Max(a,Math.Min(b,x));}
    static double Smooth(double a,double b,double x){double u=Clamp((x-a)/(b-a),0,1);return u*u*(3-2*u);}
    static double Pulse(double t,double begin,double end){return Smooth(begin,begin+.32,t)*(1-Smooth(end-.55,end,t));}
    public string Familiarity {get{return Warmth<10?"慢慢熟悉你":Warmth<35?"越来越亲近":Warmth<70?"喜欢待在你身边":"在你身边很安心";}}
    public string Line {get{return WarmWords&&Clock<lineUntil?line:"";}}
    public double LineOpacity {get{return Line.Length==0?0:Math.Min(Smooth(lastLine,lastLine+.25,Clock),1-Smooth(lineUntil-.6,lineUntil,Clock));}}
    public void MarkSaved(){Dirty=false;}
    public void Remark(string message){Say(message,false);}
    void Bond(double amount){if(Clock-lastBond<8)return;Warmth=Math.Min(100,Warmth+amount);lastBond=Clock;Dirty=true;}
    void Say(string text,bool automatic){
        if(!WarmWords||Clock-lastLine<(automatic?30:1.5))return;
        line=text;lastLine=Clock;lineUntil=Clock+5;
    }
    public void Stroke(PetMode mode){
        if(mode==PetMode.Quiet)return;
        if(Clock-lastStroke>1)strokeTime=0;
        lastStroke=Clock;strokeUntil=Clock+.45;Bond(.5);
    }
    public bool Contact(PetTouchZone zone,PetMode mode,bool stroking,double side){
        bool words=!Touch.Active||Touch.Zone!=zone;
        if(mode==PetMode.Quiet||!Touch.Contact(zone,stroking,side))return false;
        Gesture=CompanionGesture.None;Bond(.5);nextAffection=Clock+100;
        if(zone==PetTouchZone.Head)Stroke(mode);
        if(!words)return true;
        TouchWords(zone);
        return true;
    }
    public void ContactInPose(PetTouchZone zone,PetMode mode){if(mode==PetMode.Quiet)return;Bond(.5);nextAffection=Clock+100;TouchWords(zone);}
    void TouchWords(PetTouchZone zone){
        if(zone==PetTouchZone.Head)Say("呼噜……喜欢这样摸摸头。",false);
        else if(zone==PetTouchZone.Chin)Say("下巴这里……好舒服呀。",false);
        else if(zone==PetTouchZone.CheekLeft||zone==PetTouchZone.CheekRight)Say("蹭蹭你的手。",false);
        else if(zone==PetTouchZone.Back)Say("顺顺毛，慢慢放松下来。",false);
        else if(zone==PetTouchZone.Nose)Say("咦，碰到鼻尖啦。",false);
        else Say("小爪爪给你碰一下。",false);
    }
    public bool Trigger(CompanionGesture gesture,PetMode mode){return Trigger(gesture,mode,true);}
    bool Trigger(CompanionGesture gesture,PetMode mode,bool words){
        if(mode==PetMode.Quiet||gesture==CompanionGesture.None)return false;
        // A held mouse or rapidly repeated clicks cannot continually restart a pose.
        if(Gesture==gesture&&Clock-gestureStart<.55)return false;
        Touch.Clear();
        Gesture=gesture;gestureStart=Clock;duration=gesture==CompanionGesture.Play?9:gesture==CompanionGesture.Comfort?6:gesture==CompanionGesture.Treat?4:3.5;
        nextAffection=Clock+90+(Personality==Personality.Shy?55:Personality==Personality.Aloof?110:0);
        if(gesture!=CompanionGesture.Greet)Bond(1);
        if(words){
            turn++;
            if(gesture==CompanionGesture.Nuzzle)Say(Personality==Personality.Aloof?"就贴一下下。":turn%2==0?"贴贴，今天也喜欢你。":"这里是我的安心位置。",false);
            if(gesture==CompanionGesture.Boop)Say(turn%2==0?"碰！鼻尖收到啦。":"咦，你在跟我打招呼呀。",false);
            if(gesture==CompanionGesture.Paw)Say(turn%2==0?"小爪爪给你，勇气也给你。":"碰个爪，我们是一队的。",false);
            if(gesture==CompanionGesture.Play)Say("陪我玩一小会儿吧。",false);
            if(gesture==CompanionGesture.Treat)Say("谢谢你，今天的快乐加一。",false);
            if(gesture==CompanionGesture.Comfort)Say(turn%2==0?"今天已经很努力了，歇会儿吧。":"不用一直很厉害，我陪着你。",false);
        }
        return true;
    }
    public void StartFocus(double seconds){
        FocusRemaining=Clamp(seconds,1,10800);Gesture=CompanionGesture.None;lineUntil=0;strokeTime=0;
        Say("你忙你的，我在旁边陪着。",false);
    }
    public void CancelFocus(){FocusRemaining=0;lineUntil=0;nextAffection=Clock+90;}
    public void Silence(){Gesture=CompanionGesture.None;lineUntil=0;strokeUntil=-100;strokeTime=0;Touch.Clear();}
    public void Tick(double seconds,PetMode mode,double idleSeconds){Tick(seconds,mode,idleSeconds,false);}
    public void Tick(double seconds,PetMode mode,double idleSeconds,bool busy){
        double dt=Clamp(seconds,0,86400);Clock+=dt;
        Touch.Tick(dt,mode==PetMode.Quiet);
        if(FocusRemaining>0){
            FocusRemaining=Math.Max(0,FocusRemaining-dt);
            if(FocusRemaining==0&&mode!=PetMode.Quiet){lastLine=-100;Say("这一小段完成啦，伸个懒腰吧。",false);Trigger(CompanionGesture.Greet,mode,false);}
        }
        if(idleSeconds>=60)wasAway=true;
        if(wasAway&&idleSeconds>=0&&idleSeconds<3){
            wasAway=false;
            if(mode!=PetMode.Quiet&&!IsFocusing&&Clock-lastGreeting>300){
                lastGreeting=Clock;Greetings++;if(!busy&&!Touch.Active)Trigger(CompanionGesture.Greet,mode,false);Say("回来啦，我还在这里。",true);
            }
        }
        if(mode==PetMode.Quiet){Silence();UpdateSignals();return;}
        if(Clock<strokeUntil){
            strokeTime+=Math.Min(dt,.15);
            if(strokeTime>2.7&&Clock-strokeAcknowledged>30&&!IsFocusing){strokeAcknowledged=Clock;Say(Personality==Personality.Shy?"是熟悉的手，可以再摸摸。":"呼噜……这样摸摸好舒服。",true);}
        }else strokeTime=Math.Max(0,strokeTime-dt*.8);
        if(Gesture!=CompanionGesture.None&&Clock>=gestureStart+duration)Gesture=CompanionGesture.None;
        if(!busy&&!Touch.Active&&Gesture==CompanionGesture.None&&!IsFocusing&&Clock>=nextAffection&&idleSeconds>=0&&idleSeconds<30){
            // A quiet, wordless slow blink; no popups demanding a response.
            Trigger(CompanionGesture.Greet,mode,false);
        }
        UpdateSignals();
    }
    void UpdateSignals(){
        Touch.Apply(Signals);
        double t=Clock-gestureStart;
        Signals.Nuzzle=Signals.Curiosity=Signals.Wink=Signals.PawLeft=Signals.PawRight=Signals.Bow=Signals.SlowBlink=Signals.Play=Signals.Yawn=Signals.FrontStretch=Signals.Groom=Signals.RollOver=Signals.Hunt=Signals.Rest=Signals.LookWeight=0;
        Signals.Knead=Smooth(1.2,3,strokeTime);
        if(Gesture==CompanionGesture.Nuzzle||Gesture==CompanionGesture.Comfort){
            Signals.Nuzzle=Pulse(t,0,duration);
            Signals.SlowBlink=Pulse(t,.35,duration-.5)*.86;
            if(Gesture==CompanionGesture.Nuzzle)Signals.Wink=Pulse(t,.2,1.4);
        }
        if(Gesture==CompanionGesture.Boop){Signals.Curiosity=Pulse(t,0,2.7);Signals.SlowBlink=Pulse(t,1.9,3.3)*.9;}
        if(Gesture==CompanionGesture.Paw){Signals.PawRight=Pulse(t,.1,3.2);Signals.SlowBlink=Pulse(t,.7,2.9)*.65;}
        if(Gesture==CompanionGesture.Play){
            Signals.Play=Pulse(t,0,duration);Signals.ToyX=Math.Sin(t*1.9)*.72;Signals.ToyY=.45+Math.Abs(Math.Sin(t*2.2))*.16;
            double attempt=Pulse(t,1.1,3.1)+Pulse(t,4.2,6.6);
            Signals.Hunt=Math.Min(1,attempt)*.8;
            Signals.PawLeft=Math.Max(0,Math.Sin((t-1.1)*3.5))*Signals.Play*.9*Math.Min(1,attempt);
            Signals.PawRight=Math.Max(0,-Math.Sin((t-1.1)*3.5))*Signals.Play*.9*Math.Min(1,attempt);
        }
        if(Gesture==CompanionGesture.Treat){Signals.Bow=Pulse(t,.1,2.3);Signals.SlowBlink=Pulse(t,2,3.8)*.9;}
        if(Gesture==CompanionGesture.Greet){Signals.SlowBlink=Pulse(t,.2,2.2)*.95;Signals.Nuzzle=Pulse(t,.2,3)*.25;}
    }
}
}
