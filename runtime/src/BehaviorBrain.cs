using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace MaohaiziPet {
// Local, gentle motivations. Absence never incurs a care debt or illness.
public sealed class PetNeedsState {
    public double Energy=78,Appetite=25,Affection=70,Curiosity=50;
    public void Normalize(){Energy=Bound(Energy,78);Appetite=Bound(Appetite,25);Affection=Bound(Affection,70);Curiosity=Bound(Curiosity,50);}
    internal static double Bound(double n,double fallback){return double.IsNaN(n)||double.IsInfinity(n)?fallback:Math.Max(0,Math.Min(100,n));}
}
public sealed class PetNeeds {
    public readonly PetNeedsState State;
    public double Clock {get;private set;}
    double lastTreat=-1000,lastPlay=-1000;
    public int Hour=12;
    public PetNeeds(PetNeedsState state){State=state??new PetNeedsState();State.Normalize();}
    public void Tick(double seconds,bool sleeping,bool walking,bool visible,int hour){
        double minutes=Math.Max(0,Math.Min(86400,seconds))/60;Clock+=minutes*60;Hour=Math.Max(0,Math.Min(23,hour));
        if(!visible)return;
        State.Energy+=minutes*(sleeping?3.8:walking?-.65:-.28);
        State.Appetite+=minutes*(sleeping?.10:.22);
        State.Affection=Math.Max(30,State.Affection-minutes*.08);
        State.Curiosity+=minutes*(sleeping?-.35:.85);State.Normalize();
    }
    public void Touch(){State.Affection+=3;State.Normalize();}
    public void Treat(){State.Appetite-=24;State.Affection+=5;lastTreat=Clock;State.Normalize();}
    public void Play(){State.Energy-=3;State.Curiosity-=18;State.Affection+=7;lastPlay=Clock;State.Normalize();}
    public void Started(CatHabit habit){if(CatHabits.IsWalk(habit))State.Curiosity=Math.Max(0,State.Curiosity-12);}
    public string Feeling {get{return State.Energy<30?"想打个盹":State.Appetite>70?"有点馋小零食":State.Affection<40?"想和你贴贴":State.Curiosity>70?"想四处看看":"放松地陪着你";}}
    public double Weight(CatHabit habit){
        if(habit==CatHabit.CurlSleep)return State.Energy<30?5:State.Energy<55?2:Hour>=23||Hour<6?1.4:.65;
        if(habit==CatHabit.Yawn)return State.Energy<45?3:.75;
        if(CatHabits.IsWalk(habit))return (.5+State.Curiosity/55)*Math.Max(.35,State.Energy/75);
        if(habit==CatHabit.PawPlay)return State.Energy<35?.25:State.Curiosity>65?2:Clock-lastPlay<35?.4:1;
        if(habit==CatHabit.Groom)return Clock-lastTreat<75?2.6:1;
        if(habit==CatHabit.BellyRoll)return State.Affection>65?1.5:.65;
        if(habit==CatHabit.LookAround)return .6+State.Curiosity/90;
        if(habit==CatHabit.SitUp)return State.Energy<45?1.7:1;
        if(habit==CatHabit.StandUp)return State.Energy>55?1.35:.65;
        return 1;
    }
}
[DataContract] public sealed class BehaviorRule {
    [DataMember] public string action;
    [DataMember] public double weight=1;
    [DataMember(EmitDefaultValue=false)] public double cooldown,minDwell,maxDwell;
}
[DataContract] public sealed class BehaviorRulesFile {
    [DataMember] public int version=1;
    [DataMember] public BehaviorRule[] rules=new BehaviorRule[0];
}
public sealed class BehaviorPolicy {
    readonly Dictionary<CatHabit,BehaviorRule> rules=new Dictionary<CatHabit,BehaviorRule>();
    public bool UsedFallback {get;private set;}
    static readonly Dictionary<string,CatHabit> names=new Dictionary<string,CatHabit>(StringComparer.OrdinalIgnoreCase){
        {"sit",CatHabit.SitUp},{"stand",CatHabit.StandUp},{"sleep",CatHabit.CurlSleep},{"walk-left",CatHabit.WalkLeft},{"walk-right",CatHabit.WalkRight},
        {"groom",CatHabit.Groom},{"yawn",CatHabit.Yawn},{"belly",CatHabit.BellyRoll},{"look",CatHabit.LookAround},{"paw-play",CatHabit.PawPlay},{"stretch",CatHabit.FrontStretch},{"doze",CatHabit.Doze}};
    public static BehaviorPolicy Load(string path){
        var p=new BehaviorPolicy();if(!File.Exists(path))return p;
        try{
            if(new FileInfo(path).Length>32768)throw new InvalidDataException();
            BehaviorRulesFile file;using(var f=File.OpenRead(path))file=(BehaviorRulesFile)new DataContractJsonSerializer(typeof(BehaviorRulesFile)).ReadObject(f);
            if(file==null||file.version!=1||file.rules==null||file.rules.Length>16)throw new InvalidDataException();
            foreach(var r in file.rules){CatHabit h;if(r==null||r.action==null||!names.TryGetValue(r.action,out h)||p.rules.ContainsKey(h)||new[]{r.weight,r.cooldown,r.minDwell,r.maxDwell}.Any(v=>double.IsNaN(v)||double.IsInfinity(v))||r.weight<0||r.weight>8||r.cooldown<0||r.cooldown>600||r.minDwell<0||r.maxDwell<r.minDwell||r.maxDwell>60||r.maxDwell>0&&r.minDwell<(h==CatHabit.BellyRoll?1:6))throw new InvalidDataException();p.rules[h]=r;}
        }catch{p=new BehaviorPolicy{UsedFallback=true};}
        return p;
    }
    public double Weight(CatHabit h){BehaviorRule r;return rules.TryGetValue(h,out r)?r.weight:1;}
    public double Cooldown(CatHabit h){BehaviorRule r;return rules.TryGetValue(h,out r)&&r.cooldown>0?Math.Max(5,r.cooldown):CatHabits.Cooldown(h);}
    public double Dwell(CatHabit h,Random random){BehaviorRule r;if(rules.TryGetValue(h,out r)&&r.maxDwell>0)return r.minDwell+random.NextDouble()*(r.maxDwell-r.minDwell);return h==CatHabit.SitUp?24+random.NextDouble()*24:h==CatHabit.StandUp?14+random.NextDouble()*18:h==CatHabit.CurlSleep?28+random.NextDouble()*28:h==CatHabit.BellyRoll?1.6+random.NextDouble()*.7:6+random.NextDouble()*6;}
}
}
