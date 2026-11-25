using System;
using System.Collections.Generic;

namespace MaohaiziPet {
public enum CatHabit { None, Yawn, FrontStretch, Groom, BellyRoll, LookAround, PawPlay, Doze, SitUp, StandUp, CurlSleep, WalkLeft, WalkRight }
// Major behavior scheduling is separate from gentle affection. Large poses are
// gated on validated pose assets; a rotated frontal texture is not a belly roll.
public sealed class CatHabits {
    public CatHabit Current {get;private set;}
    public double Age {get;private set;}
    public double Clock {get;private set;}
    public double Duration {get;private set;}
    public double DwellSeconds {get;private set;}
    public double EntrySeconds {get;private set;}
    public bool YawnReady,GroomReady,BellyReady;
    public bool SitReady,StandReady,SleepReady;
    public bool WalkLeftReady,WalkRightReady;
    public double Familiarity;
    public int Mood;
    public PetNeeds Needs;
    public BehaviorPolicy Policy=new BehaviorPolicy();
    public int AutomaticCount {get;private set;}
    public int Revision {get;private set;}
    public bool Interrupted {get;private set;}
    public string Phase {get;private set;}
    public bool Active {get{return Current!=CatHabit.None;}}
    readonly Random random;
    readonly Dictionary<CatHabit,double> motionDurations=new Dictionary<CatHabit,double>();
    readonly Dictionary<CatHabit,double[]> residenceTimings=new Dictionary<CatHabit,double[]>();
    readonly Dictionary<CatHabit,double> lastStarted=new Dictionary<CatHabit,double>();
    double lastInteraction,nextAutomatic;
    double lookDirection,lookHeight;
    double lastPosture=-100;
    CatHabit previousHabit=CatHabit.None;
    CatHabit following=CatHabit.None;
    bool departureChosen;
    readonly HashSet<CatHabit> seenPostures=new HashSet<CatHabit>();
    public CatHabits(int seed){random=new Random(seed);nextAutomatic=7+random.NextDouble()*5;Phase="安静观察";}
    public void SetMotionDuration(CatHabit habit,double seconds){if(double.IsNaN(seconds)||double.IsInfinity(seconds)||seconds<.5||seconds>90)throw new ArgumentException("Invalid motion duration");motionDurations[habit]=seconds;}
    public void SetResidenceTiming(CatHabit habit,double entry,double transition){if(entry<0||transition<entry||transition>30)throw new ArgumentException("Invalid posture timing");residenceTimings[habit]=new[]{entry,transition};nextAutomatic=Math.Min(nextAutomatic,3+random.NextDouble()*3);}
    public void EnterAt(double transitionRemaining){double target=Math.Max(0,EntrySeconds-transitionRemaining);nextAutomatic+=Age-target;Age=target;}
    public bool AdoptStanding(){if(!Start(CatHabit.StandUp,PetMode.Companion,false))return false;EnterAt(0);return true;}
    static double Smooth(double a,double b,double x){double u=Math.Max(0,Math.Min(1,(x-a)/(b-a)));return u*u*(3-2*u);}
    static double Envelope(double t,double start,double peak,double release,double end){return Smooth(start,peak,t)*(1-Smooth(release,end,t));}
    public bool Available(CatHabit habit){return habit==CatHabit.Yawn&&YawnReady||habit==CatHabit.FrontStretch||habit==CatHabit.Groom&&GroomReady||habit==CatHabit.BellyRoll&&BellyReady||habit==CatHabit.LookAround||habit==CatHabit.PawPlay||habit==CatHabit.Doze||habit==CatHabit.SitUp&&SitReady||habit==CatHabit.StandUp&&StandReady||habit==CatHabit.CurlSleep&&SleepReady||habit==CatHabit.WalkLeft&&WalkLeftReady||habit==CatHabit.WalkRight&&WalkRightReady;}
    public static bool IsWalk(CatHabit habit){return habit==CatHabit.WalkLeft||habit==CatHabit.WalkRight;}
    static bool IsPosture(CatHabit habit){return habit==CatHabit.SitUp||habit==CatHabit.StandUp||habit==CatHabit.CurlSleep||IsWalk(habit);}
    public static double Cooldown(CatHabit habit){return habit==CatHabit.LookAround?18:habit==CatHabit.FrontStretch?50:habit==CatHabit.Groom?80:habit==CatHabit.Yawn?100:habit==CatHabit.SitUp?38:habit==CatHabit.StandUp?42:habit==CatHabit.CurlSleep?120:IsWalk(habit)?55:habit==CatHabit.PawPlay?65:90;}
    bool CanAutoStart(CatHabit habit){return Available(habit)&&Policy.Weight(habit)>0&&habit!=previousHabit&&(!lastStarted.ContainsKey(habit)||Clock-lastStarted[habit]>=Policy.Cooldown(habit));}
    public bool CanStartGoal(CatHabit habit){return CanAutoStart(habit);}
    public void PauseForPlay(double seconds){Clock+=Math.Max(0,Math.Min(86400,seconds));lastInteraction=Clock;nextAutomatic=Clock+8;}
    public bool Start(CatHabit habit,PetMode mode,bool focus){
        if(mode==PetMode.Quiet||focus||!Available(habit))return false;
        if(Current==habit)return false;
        Current=habit;Age=0;Interrupted=false;Revision++;Duration=habit==CatHabit.Yawn?5:habit==CatHabit.FrontStretch?4:habit==CatHabit.Groom?8:10;
        if(habit==CatHabit.LookAround)Duration=3;if(habit==CatHabit.PawPlay)Duration=6;if(habit==CatHabit.Doze)Duration=8+random.NextDouble()*5;
        if(habit==CatHabit.SitUp)Duration=18;if(habit==CatHabit.StandUp)Duration=12;if(habit==CatHabit.CurlSleep)Duration=28;
        if(motionDurations.ContainsKey(habit))Duration=motionDurations[habit];
        DwellSeconds=EntrySeconds=0;departureChosen=false;
        bool resident=residenceTimings.ContainsKey(habit);
        if(resident){var timing=residenceTimings[habit];EntrySeconds=timing[0];DwellSeconds=Policy.Dwell(habit,random);Duration=timing[1]+DwellSeconds;}
        bool posture=IsPosture(habit);if(posture){seenPostures.Add(habit);lastPosture=Clock;}
        if(posture&&!resident)Duration*=.9+random.NextDouble()*.2;
        lastStarted[habit]=Clock;lookDirection=(random.Next(2)==0?-1:1)*(.35+random.NextDouble()*.45);lookHeight=-.22+random.NextDouble()*.3;
        if(IsWalk(habit)){lastStarted[CatHabit.WalkLeft]=Clock;lastStarted[CatHabit.WalkRight]=Clock;}
        if(Needs!=null)Needs.Started(habit);
        previousHabit=habit;following=CatHabit.None;nextAutomatic=Clock+Duration+(resident?.6+random.NextDouble()*1.8:4+random.NextDouble()*7);Phase=habit==CatHabit.Yawn?"有点困了":habit==CatHabit.FrontStretch?"伸伸前爪":habit==CatHabit.Groom?"整理毛毛":habit==CatHabit.BellyRoll?"慢慢侧躺":habit==CatHabit.LookAround?"看看四周":habit==CatHabit.PawPlay?"自己玩一会儿":habit==CatHabit.SitUp?"慢慢坐起来":habit==CatHabit.StandUp?"站起来活动一下":habit==CatHabit.CurlSleep?"趴着打盹":habit==CatHabit.WalkRight?"起来走两步":habit==CatHabit.WalkLeft?"起来走两步":"眯一小会儿";return true;
    }
    public void Interrupt(){Current=CatHabit.None;following=CatHabit.None;Age=0;Interrupted=true;lastInteraction=Clock;nextAutomatic=Clock+7+random.NextDouble()*5;Phase="看向你";}
    public void Tick(double seconds,PetMode mode,bool focus,bool interacting){
        double dt=Math.Max(0,Math.Min(86400,seconds));Clock+=dt;
        if(mode==PetMode.Quiet||focus){if(Active)Interrupt();nextAutomatic=Clock+8;return;}
        if(interacting){lastInteraction=Clock;if(Active)Interrupt();return;}
        if(Active){
            Age+=dt;
            if(Current==CatHabit.StandUp&&DwellSeconds>0&&!departureChosen&&Age>=EntrySeconds+DwellSeconds-.12){departureChosen=true;var walks=new List<CatHabit>();if(CanAutoStart(CatHabit.WalkLeft))walks.Add(CatHabit.WalkLeft);if(CanAutoStart(CatHabit.WalkRight))walks.Add(CatHabit.WalkRight);if(walks.Count>0&&random.Next(4)!=0){if(Start(walks[random.Next(walks.Count)],mode,false))AutomaticCount++;return;}if(CanAutoStart(CatHabit.SitUp)){if(Start(CatHabit.SitUp,mode,false))AutomaticCount++;return;}}
            if(Current==CatHabit.SitUp&&DwellSeconds>0&&!departureChosen&&Age>=EntrySeconds+DwellSeconds-.12){departureChosen=true;bool needsSleepIntro=SleepReady&&seenPostures.Contains(CatHabit.StandUp)&&!seenPostures.Contains(CatHabit.CurlSleep);if(!needsSleepIntro&&CanAutoStart(CatHabit.StandUp)&&(random.Next(3)!=0||!seenPostures.Contains(CatHabit.StandUp))){if(Start(CatHabit.StandUp,mode,false))AutomaticCount++;return;}}
            if(Age>=Duration){CatHabit finished=Current;Current=CatHabit.None;Age=0;Phase="安静观察";
                if(finished==CatHabit.CurlSleep)following=StandReady?CatHabit.StandUp:CatHabit.FrontStretch;
                else if(finished==CatHabit.Yawn&&SleepReady&&random.Next(3)!=0)following=CatHabit.CurlSleep;
                else if(finished==CatHabit.PawPlay&&GroomReady&&random.Next(2)==0)following=CatHabit.Groom;
                else if(finished==CatHabit.SitUp&&DwellSeconds>0&&GroomReady&&random.Next(2)==0)following=CatHabit.Groom;
                else if(finished==CatHabit.StandUp&&DwellSeconds>0&&YawnReady&&random.Next(4)==0)following=CatHabit.Yawn;
                if(following!=CatHabit.None&&CanAutoStart(following))nextAutomatic=Clock+5+random.NextDouble()*5;return;}
            double phaseAge=Age*(Current==CatHabit.Yawn?5:Current==CatHabit.Groom?8:Current==CatHabit.BellyRoll?10:4)/Duration;
            if(Current==CatHabit.Yawn)Phase=phaseAge<1?"慢慢眯眼":phaseAge<2.7?"打个哈欠":phaseAge<4?"慢慢合嘴":"放松下来";
            if(Current==CatHabit.FrontStretch)Phase=Age<1?"身体前倾":Age<2.8?"伸伸前爪":"收回小爪";
            if(Current==CatHabit.Groom)Phase=phaseAge<1.4?"抬起前爪":phaseAge<4.6?"舔舔毛":phaseAge<6.6?"擦擦脸":"放下前爪";
            if(Current==CatHabit.BellyRoll)Phase=phaseAge<2?"侧身撒个娇":phaseAge<7?"蜷蜷小爪看看你":"翻回来陪你";
            if(Current==CatHabit.SitUp)Phase=Age<2.5?"慢慢坐起来":Age<Duration-2.5?"坐着看看四周":"慢慢伏下来";
            if(Current==CatHabit.StandUp)Phase=Age<2.5?"站起来活动一下":Age<Duration-2.5?"站着陪你一会儿":"放松下来";
            if(Current==CatHabit.CurlSleep)Phase=Age<2.5?"找个舒服的睡姿":Age<Duration-2.5?"趴着小睡一会儿":"慢慢醒来";
        }else if(Clock>=nextAutomatic&&Clock-lastInteraction>2.5){
            if(Needs!=null&&Needs.State.Energy<22&&CanAutoStart(CatHabit.CurlSleep)){if(Start(CatHabit.CurlSleep,mode,false))AutomaticCount++;return;}
            var postures=new[]{CatHabit.SitUp,CatHabit.StandUp,CatHabit.WalkLeft,CatHabit.WalkRight,CatHabit.CurlSleep};
            var fresh=new List<CatHabit>();foreach(var h in postures)if(CanAutoStart(h)&&!seenPostures.Contains(h))fresh.Add(h);
            if(fresh.Count>0){CatHabit first=fresh.Contains(CatHabit.SitUp)?CatHabit.SitUp:fresh[random.Next(fresh.Count)];if(Start(first,mode,false))AutomaticCount++;return;}
            if(residenceTimings.Count>0&&following!=CatHabit.None){CatHabit next=following;following=CatHabit.None;if(CanAutoStart(next)){if(Start(next,mode,false))AutomaticCount++;return;}}
            // Large pose variety must not be starved by repeated minor looks.
            if(Clock-lastPosture>=24){var overdue=new List<CatHabit>();foreach(var h in postures)if(CanAutoStart(h))overdue.Add(h);if(overdue.Count>0){overdue.Sort((a,b)=>(lastStarted.ContainsKey(a)?lastStarted[a]:-100).CompareTo(lastStarted.ContainsKey(b)?lastStarted[b]:-100));if(Start(overdue[0],mode,false))AutomaticCount++;return;}}
            if(following!=CatHabit.None){CatHabit next=following;following=CatHabit.None;if(CanAutoStart(next)){if(Start(next,mode,false))AutomaticCount++;return;}}
            var choices=new List<CatHabit>{CatHabit.LookAround,CatHabit.FrontStretch,CatHabit.PawPlay};
            if(GroomReady){choices.Add(CatHabit.Groom);choices.Add(CatHabit.Groom);}if(YawnReady)choices.Add(CatHabit.Yawn);
            if(SitReady)choices.Add(CatHabit.SitUp);if(StandReady)choices.Add(CatHabit.StandUp);if(SleepReady)choices.Add(CatHabit.CurlSleep);
            if(WalkLeftReady)choices.Add(CatHabit.WalkLeft);if(WalkRightReady)choices.Add(CatHabit.WalkRight);
            if(Mood==2){choices.Add(CatHabit.PawPlay);choices.Add(CatHabit.LookAround);}else if(Mood==1&&SleepReady)choices.Add(CatHabit.CurlSleep);
            if(previousHabit==CatHabit.Yawn&&!SleepReady)choices.Add(CatHabit.Doze);if(BellyReady&&Familiarity>=8&&random.Next(5)==0)choices.Add(CatHabit.BellyRoll);
            choices.RemoveAll(h=>!CanAutoStart(h));if(choices.Count==0){nextAutomatic=Clock+5+random.NextDouble()*5;return;}
            double total=0;foreach(var h in choices)total+=Policy.Weight(h)*(Needs==null?1:Needs.Weight(h));double pick=random.NextDouble()*total;CatHabit selected=choices[choices.Count-1];foreach(var h in choices){pick-=Policy.Weight(h)*(Needs==null?1:Needs.Weight(h));if(pick<=0){selected=h;break;}}
            if(Start(selected,mode,false))AutomaticCount++;
        }
    }
    public void Apply(CompanionSignals signals){
        signals.Yawn=signals.FrontStretch=signals.Groom=signals.RollOver=signals.Rest=signals.LookWeight=0;
        if(Current==CatHabit.Yawn)signals.Yawn=Envelope(Age,.45,2.0,2.65,4.3);
        if(Current==CatHabit.FrontStretch)signals.FrontStretch=Envelope(Age,.2,1.4,2.5,3.8);
        if(Current==CatHabit.Groom)signals.Groom=Envelope(Age,0,1,6.8,8);
        if(Current==CatHabit.BellyRoll)signals.RollOver=Envelope(Age,0,3.6,6.5,10);
        if(Current==CatHabit.LookAround){signals.LookWeight=Envelope(Age,0,.6,2.1,3);signals.LookX=lookDirection;signals.LookY=lookHeight;}
        if(Current==CatHabit.Doze)signals.Rest=Envelope(Age,0,1.5,Duration-1.5,Duration);
        if(Current==CatHabit.PawPlay){double play=Envelope(Age,0,.5,5.3,6);signals.Play=play;signals.ToyX=Math.Sin(Age*.8)*.65;signals.ToyY=.45+Math.Sin(Age*.65)*.09;double attempt=Envelope(Age,.8,1.25,1.85,2.8);signals.Hunt=attempt*.8;signals.PawLeft=lookDirection<0?attempt*.8:0;signals.PawRight=lookDirection>=0?attempt*.8:0;}
    }
}
}
