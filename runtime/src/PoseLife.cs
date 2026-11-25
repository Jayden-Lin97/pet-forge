using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media.Imaging;

namespace MaohaiziPet {
// Small, reviewed native actions belong to a lasting posture. They do not
// send the cat back to its neutral pose, and never loop a recorded sequence.
public sealed class PoseLife {
    readonly MotionClip[] choices;
    readonly Random random;
    readonly Dictionary<string,double> last=new Dictionary<string,double>();
    string previous;
    MotionClip current;
    MotionClip requested;
    double age,nextAt,ended=-10,position;
    bool closing,playing;
    double lastContact=-100;
    public bool HasActivities {get{return choices.Length>0;}}
    public bool Active {get{return current!=null;}}
    public MotionClip Clip {get{return current;}}
    public int FrameIndex {get{return current==null?0:Math.Min(current.Frames.Length-1,(int)position);}}
    public BitmapSource Image {get{return current==null?null:current.Frames[FrameIndex];}}
    public int ActivityCount {get;private set;}
    public bool CanPlay {get{return !closing&&!Active&&choices.Any(c=>MotionLibrary.IsToyReaction(c.Spec.id));}}
    public void SetPlaying(bool value){playing=value;if(value)requested=null;}
    public bool Play(bool left,double remaining){
        if(!CanPlay)return false;string id=left?"sit-play-left":"sit-play-right";
        var clip=choices.FirstOrDefault(c=>c.Spec.id==id);if(clip==null||remaining<clip.Seconds+.5||last.ContainsKey(id)&&age-last[id]<2.6)return false;
        current=clip;position=0;ActivityCount++;return true;
    }
    public bool Jump(Point target,double remaining){
        if(!CanPlay||double.IsNaN(target.X)||double.IsNaN(target.Y)||double.IsInfinity(target.X)||double.IsInfinity(target.Y))return false;
        var clip=choices.FirstOrDefault(c=>c.Spec.id=="sit-jump");if(clip==null||remaining<clip.Seconds+.5||last.ContainsKey(clip.Spec.id)&&age-last[clip.Spec.id]<3)return false;
        bool reachable=false;for(int i=clip.Spec.contactStart;i<=clip.Spec.contactEnd;i++){var paw=new Point(clip.Spec.pawX[i]+clip.Spec.originX,clip.Spec.pawY[i]+clip.Spec.originY);if((target-paw).Length<70){reachable=true;break;}}
        if(!reachable)return false;current=clip;position=0;ActivityCount++;return true;
    }
    public Point? ToyPaw {get{if(!Active||current.Spec.pawX==null||FrameIndex<current.Spec.contactStart||FrameIndex>current.Spec.contactEnd)return null;return new Point(current.Spec.pawX[FrameIndex]+current.Spec.originX,current.Spec.pawY[FrameIndex]+current.Spec.originY);}}
    public double BaseWeight {get {if(Active)return 0;return Smooth((nextAt-age)/.35)*Smooth((age-ended)/.35);}}
    static double Smooth(double n){double x=Math.Max(0,Math.Min(1,n));return x*x*(3-2*x);}
    public PoseLife(IEnumerable<MotionClip> clips,int seed){choices=clips.ToArray();random=new Random(seed);nextAt=choices.Length==0?double.PositiveInfinity:3+random.NextDouble()*3;}
    public void Exit(){closing=true;requested=null;}
    public bool Contact(PetTouchZone zone,double remaining){
        if(closing||Active||requested!=null||age-lastContact<6)return false;
        bool head=zone==PetTouchZone.Head||zone==PetTouchZone.Chin||zone==PetTouchZone.CheekLeft||zone==PetTouchZone.CheekRight||zone==PetTouchZone.Nose;
        var candidates=choices.Where(c=>!MotionLibrary.IsToyReaction(c.Spec.id)&&c.Spec.id!=previous&&remaining>c.Seconds+.8&&(!last.ContainsKey(c.Spec.id)||age-last[c.Spec.id]>=18)).OrderBy(c=>c.Spec.id.EndsWith("look")==head?0:1).ToArray();
        if(candidates.Length==0)return false;requested=candidates[0];nextAt=age+.35;lastContact=age;return true;
    }
    public void Tick(double seconds,double remaining){
        double dt=Math.Max(0,seconds);age+=dt;
        if(current!=null){position+=dt*current.Spec.fps*(closing&&current.Spec.id!="sit-jump"?1.5:1);if(position>=current.Frames.Length){last[current.Spec.id]=age;previous=current.Spec.id;current=null;ended=age;nextAt=age+4+random.NextDouble()*5;}return;}
        if(closing||playing||age<nextAt)return;
        var available=requested==null?choices.Where(c=>!MotionLibrary.IsToyReaction(c.Spec.id)&&c.Spec.id!=previous&&remaining>c.Seconds+.8&&(!last.ContainsKey(c.Spec.id)||age-last[c.Spec.id]>=18)).ToArray():new[]{requested};
        if(available.Length==0){nextAt=age+1;return;}
        current=available[random.Next(available.Length)];requested=null;position=0;ActivityCount++;
    }
}
}
