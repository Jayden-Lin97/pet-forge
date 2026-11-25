using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MaohaiziPet {
[DataContract] public sealed class MotionManifest {
    [DataMember] public int version=1;
    [DataMember] public MotionSpec[] clips=new MotionSpec[0];
}
[DataContract] public sealed class MotionSpec {
    [DataMember] public string id,sheet;
    [DataMember] public int columns,rows,frames;
    [DataMember] public double fps;
    [DataMember] public bool reviewed;
    [DataMember(EmitDefaultValue=false)] public int anchorFrames;
    [DataMember(EmitDefaultValue=false)] public int size,originX,originY;
    [DataMember(EmitDefaultValue=false)] public int loopStart,loopEnd,loopCycles;
    [DataMember(EmitDefaultValue=false)] public int holdAt;
    [DataMember(EmitDefaultValue=false)] public double holdSeconds,eyeLeftX,eyeLeftY,eyeRightX,eyeRightY;
    [DataMember(EmitDefaultValue=false)] public string transitionSource;
    [DataMember(EmitDefaultValue=false)] public bool physicalTransitionReviewed;
    [DataMember(EmitDefaultValue=false)] public double[] travelX,travelY,noseX,noseY;
    [DataMember(EmitDefaultValue=false)] public bool pathFollowsFrame;
    [DataMember(EmitDefaultValue=false)] public int[] frameMap;
    [DataMember(EmitDefaultValue=false)] public string derivedFrom,parentPose;
    [DataMember(EmitDefaultValue=false)] public int standEntryAt,standExitAt,sitEntryAt,sitExitAt;
    [DataMember(EmitDefaultValue=false)] public int gaitEntryAt,gaitExitAt;
    [DataMember(EmitDefaultValue=false)] public double[] pawX,pawY;
    [DataMember(EmitDefaultValue=false)] public int contactStart,contactEnd;
    [DataMember(EmitDefaultValue=false)] public int airborneStart,airborneEnd;
    [DataMember(EmitDefaultValue=false)] public double jumpTop;
}
public sealed class MotionClip {
    public readonly MotionSpec Spec;
    public readonly MotionFrames Frames;
    public MotionClip(MotionSpec spec,BitmapSource[] frames){Spec=spec;Frames=new MotionFrames(frames);}
    public MotionClip(MotionSpec spec,MotionFrames frames){Spec=spec;Frames=frames;}
    public int Size {get{return Spec.size==0?256:Spec.size;}}
    public bool HasHold {get{return Spec.holdSeconds>0;}}
    public double PlaybackFrames {get{return Frames.Length+Spec.holdSeconds*Spec.fps;}}
    public double Seconds {get{return PlaybackFrames/Spec.fps;}}
}
// Generated video is stored as dense transparent frames. Major body poses are
// not inferred by rotating the neutral photo or blending unrelated key poses.
public sealed class MotionLibrary {
    public readonly Dictionary<string,MotionClip> Clips=new Dictionary<string,MotionClip>(StringComparer.Ordinal);
    public string Root {get;private set;}
    public static bool IsWalk(string id){return id=="walk-left"||id=="walk-right";}
    public static bool IsToyReaction(string id){return id=="sit-play-left"||id=="sit-play-right"||id=="sit-jump";}
    public static bool IsPoseActivity(string id){return id=="sit-look"||id=="sit-settle"||id=="stand-look"||id=="stand-tail"||IsToyReaction(id);}
    public static bool ValidId(string id){return IsPoseActivity(id)|| id=="groom"||id=="yawn"||id=="belly"||id=="sit"||id=="stand"||id=="sleep"||IsWalk(id);}
    public static MotionLibrary Load(string root,bool removeGreen=false){
        var library=new MotionLibrary();library.Root=root;string path=Path.Combine(root,"motions.json");if(!File.Exists(path))return library;
        if(new FileInfo(path).Length>131072)throw new InvalidDataException("动作说明过大");
        MotionManifest manifest;using(var f=File.OpenRead(path))manifest=(MotionManifest)new DataContractJsonSerializer(typeof(MotionManifest)).ReadObject(f);
        if(manifest==null||manifest.version!=1||manifest.clips==null||manifest.clips.Length>15)throw new InvalidDataException("动作包格式无效");
        var ids=new HashSet<string>();
        var atlasCache=MotionAtlasCache.Shared;
        foreach(var spec in manifest.clips){
            if(spec==null||!ValidId(spec.id)||!ids.Add(spec.id))throw new InvalidDataException("动作名称无效或重复");
            string sheet=PetSkin.SafePart(spec.sheet);
            if(sheet.Contains("/")||!sheet.EndsWith(".png",StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("动作图集必须为同目录 PNG");
            if(sheet!="motion-"+spec.id+".png")throw new InvalidDataException("动作图集名称必须与动作匹配");
            if(IsPoseActivity(spec.id)&&(spec.parentPose!=(spec.id.StartsWith("sit-")?"sit":"stand")||spec.holdSeconds!=0||spec.anchorFrames<4))throw new InvalidDataException("姿态内活动缺少正确的姿态与连续锚点");
            if(!IsPoseActivity(spec.id)&&!string.IsNullOrEmpty(spec.parentPose))throw new InvalidDataException("父姿态无效");
            if(!string.IsNullOrEmpty(spec.derivedFrom))throw new InvalidDataException("动作来源引用无效");
            bool walking=IsWalk(spec.id);
            int maxFrames=walking?640:spec.id=="stand"?320:240;
            if(spec.columns<1||spec.columns>8||spec.rows<1||spec.rows>maxFrames/8||spec.frames<8||spec.frames>maxFrames||spec.frameMap==null&&spec.frames>spec.columns*spec.rows||double.IsNaN(spec.fps)||double.IsInfinity(spec.fps)||spec.fps<8||spec.fps>30)throw new InvalidDataException("动作帧数或帧率无效");
            if(spec.frameMap!=null&&(spec.frameMap.Length!=spec.frames||spec.frameMap.Any(index=>index<0||index>=spec.columns*spec.rows)))throw new InvalidDataException("动作帧索引无效");
            if(spec.anchorFrames<0||spec.anchorFrames>spec.frames/4)throw new InvalidDataException("起止姿态帧数无效");
            if(spec.gaitEntryAt!=0||spec.gaitExitAt!=0){if(!walking||spec.standEntryAt<=0||spec.gaitEntryAt<=spec.standEntryAt||spec.gaitExitAt<=spec.gaitEntryAt||spec.gaitExitAt>=spec.standExitAt)throw new InvalidDataException("步态分段接点无效");}
            int size=spec.size==0?256:spec.size;
            if((size!=256&&size!=320&&!((walking||spec.id=="belly")&&size==384)&&!((walking||spec.id=="stand"||IsPoseActivity(spec.id))&&size==416)&&!((spec.id=="belly"||spec.id=="sit-jump")&&size==480)&&!(spec.id=="sit-jump"&&size==576))||spec.originX!=-(size-256)/2||spec.originY!=-(size-256)/2)throw new InvalidDataException("动作画布或原点无效");
            if(spec.travelX!=null){if(!walking||spec.travelX.Length!=spec.frames||spec.travelX[0]!=0)throw new InvalidDataException("走动轨迹无效");double previous=0;int sign=spec.id=="walk-left"?-1:1;foreach(double x in spec.travelX){if(double.IsNaN(x)||double.IsInfinity(x)||Math.Abs(x)>400||Math.Abs(x-previous)>12||sign*(x-previous)<-.0001)throw new InvalidDataException("走动轨迹不连续");previous=x;}}
            if(walking&&(spec.travelX==null||Math.Abs(spec.travelX[spec.frames-1])<1))throw new InvalidDataException("走动素材需要真实步态和同步轨迹");
            if(spec.travelY!=null){if(!walking||spec.travelY.Length!=spec.frames||spec.travelY[0]!=0)throw new InvalidDataException("走动纵向轨迹无效");double previous=0;foreach(double y in spec.travelY){if(double.IsNaN(y)||double.IsInfinity(y)||Math.Abs(y)>200||Math.Abs(y-previous)>14)throw new InvalidDataException("走动纵向轨迹越界");previous=y;}}
            if((spec.noseX==null)!=(spec.noseY==null))throw new InvalidDataException("面部跟踪坐标缺失");
            if(spec.noseX!=null){if(spec.noseX.Length!=spec.frames||spec.noseY.Length!=spec.frames)throw new InvalidDataException("面部跟踪坐标数量不符");foreach(double coordinate in spec.noseX.Concat(spec.noseY))if(double.IsNaN(coordinate)||double.IsInfinity(coordinate)||coordinate<0||coordinate>=size)throw new InvalidDataException("面部跟踪坐标无效");}
            bool play=IsToyReaction(spec.id);
            if((spec.pawX==null)!=(spec.pawY==null)||play&&(spec.pawX==null||spec.pawX.Length!=spec.frames||spec.pawY.Length!=spec.frames)||!play&&spec.pawX!=null)throw new InvalidDataException("玩具爪部坐标缺失或数量无效");
            if(play&&(spec.contactStart<spec.anchorFrames||spec.contactEnd<=spec.contactStart||spec.contactEnd>=spec.frames-spec.anchorFrames))throw new InvalidDataException("爪部接触区间无效");
            if(spec.pawX!=null)foreach(double coordinate in spec.pawX.Concat(spec.pawY))if(double.IsNaN(coordinate)||double.IsInfinity(coordinate)||coordinate<0||coordinate>=size)throw new InvalidDataException("玩具爪部跟踪无效");
            if(spec.id=="sit-jump"){if(spec.airborneStart<spec.anchorFrames||spec.airborneEnd<=spec.airborneStart||spec.airborneEnd>=spec.frames-spec.anchorFrames||double.IsNaN(spec.jumpTop)||double.IsInfinity(spec.jumpTop)||spec.jumpTop<spec.originY||spec.jumpTop>0)throw new InvalidDataException("起跳落地或头顶空间标记无效");}
            else if(spec.airborneStart!=0||spec.airborneEnd!=0||spec.jumpTop!=0)throw new InvalidDataException("非跳跃动作含有腾空标记");
            if(spec.loopCycles>1)throw new InvalidDataException("完整姿态不支持反复循环，请使用单次动作与自然停留");
            if(double.IsNaN(spec.holdSeconds)||double.IsInfinity(spec.holdSeconds)||spec.holdSeconds<0||spec.holdSeconds>60||spec.holdSeconds>0&&(spec.holdAt<Math.Max(1,spec.anchorFrames)||spec.holdAt>=spec.frames-Math.Max(1,spec.anchorFrames)||(spec.frames/spec.fps+spec.holdSeconds)>90))throw new InvalidDataException("姿态停留时间或位置无效");
            foreach(double eye in new[]{spec.eyeLeftX,spec.eyeLeftY,spec.eyeRightX,spec.eyeRightY})if(double.IsNaN(eye)||double.IsInfinity(eye)||eye<0||eye>=size)throw new InvalidDataException("姿态眼睛位置无效");
            if(!spec.reviewed)continue; // Draft generations never become available by merely existing on disk.
            // Sparse independent poses plus flow are not proof of articulated
            // locomotion. A separate visual gate protects large posture changes.
            if((spec.id=="sit"||spec.id=="stand"||spec.id=="sleep"||spec.id=="belly"||walking||IsPoseActivity(spec.id))&&(!spec.physicalTransitionReviewed||(spec.transitionSource!="continuous-video"&&spec.transitionSource!="skeletal-animation"&&spec.transitionSource!="generated-gif")))continue;
            string full=Path.Combine(root,sheet);if(!File.Exists(full)||new FileInfo(full).Length>32*1024*1024)throw new InvalidDataException("动作图集缺失或过大");
            MotionAtlasCache.CheckDimensions(full,spec.columns*size,spec.rows*size);
            var frames=new MotionFrames(spec,full,atlasCache,removeGreen);
            byte[] corner=new byte[4];frames[0].CopyPixels(new Int32Rect(0,0,1,1),corner,4,0);if(corner[3]>5)throw new InvalidDataException("宠物动作文件不完整，请重新下载");
            if(IsPoseActivity(spec.id)){MotionClip parent;if(!library.Clips.TryGetValue(spec.parentPose,out parent))continue;if(!parent.HasHold||(parent.Size>size||size>(spec.id=="sit-jump"?576:416))||!SamePose(frames[0],parent.Frames[parent.Spec.holdAt])||!SamePose(frames[frames.Length-1],parent.Frames[parent.Spec.holdAt]))throw new InvalidDataException("姿态内活动无法连续衔接原姿态");}
            if(spec.standEntryAt!=0||spec.standExitAt!=0){MotionClip standing;if(!walking||!library.Clips.TryGetValue("stand",out standing)||spec.standEntryAt<spec.anchorFrames||spec.standExitAt<=spec.standEntryAt||spec.standExitAt>=spec.frames-spec.anchorFrames||!SamePose(frames[spec.standEntryAt],standing.Frames[standing.Spec.holdAt])||!SamePose(frames[spec.standExitAt],standing.Frames[standing.Spec.holdAt]))throw new InvalidDataException("走动与站姿接点无效");}
            if(spec.sitEntryAt!=0||spec.sitExitAt!=0){MotionClip sitting;if((spec.id!="stand"&&!walking)||!library.Clips.TryGetValue("sit",out sitting)||spec.sitEntryAt<spec.anchorFrames||spec.sitEntryAt>=spec.frames-spec.anchorFrames||!SamePose(frames[spec.sitEntryAt],sitting.Frames[sitting.Spec.holdAt])||spec.id=="stand"&&(spec.sitExitAt<=spec.holdAt||spec.sitExitAt>=spec.frames-spec.anchorFrames||!SamePose(frames[spec.sitExitAt],sitting.Frames[sitting.Spec.holdAt])))throw new InvalidDataException("坐站连续接点无效");}
            library.Clips.Add(spec.id,new MotionClip(spec,frames));
        }
        return library;
    }
    internal static bool SamePose(BitmapSource a,BitmapSource b){int size=Math.Min(a.PixelWidth,b.PixelWidth);if(a.PixelWidth!=size)a=new CroppedBitmap(a,new Int32Rect((a.PixelWidth-size)/2,(a.PixelHeight-size)/2,size,size));if(b.PixelWidth!=size)b=new CroppedBitmap(b,new Int32Rect((b.PixelWidth-size)/2,(b.PixelHeight-size)/2,size,size));byte[] x=new byte[size*size*4],y=new byte[x.Length];a.CopyPixels(x,size*4,0);b.CopyPixels(y,size*4,0);return x.SequenceEqual(y);}
    public void Export(ZipArchive archive){
        if(Clips.Count==0)return;
        using(var s=archive.CreateEntry("motions.json").Open())new DataContractJsonSerializer(typeof(MotionManifest)).WriteObject(s,new MotionManifest{clips=Clips.Values.Select(c=>c.Spec).ToArray()});
        foreach(string name in Clips.Values.Select(c=>c.Spec.sheet).Distinct(StringComparer.OrdinalIgnoreCase))using(var src=File.OpenRead(Path.Combine(Root,name)))using(var dst=archive.CreateEntry(name).Open())src.CopyTo(dst);
    }
}
public sealed class MotionPlayer {
    public MotionClip Clip {get;private set;}
    public bool Active {get{return Clip!=null;}}
    public bool Recovering {get;private set;}
    public double FramePosition {get;private set;}
    public int FrameIndex {get{return Clip==null?0:Math.Max(0,Math.Min(Clip.Frames.Length-1,(int)FramePosition));}}
    public bool HoldingPose {get;private set;}
    public double Displacement {get{if(Clip==null||Clip.Spec.travelX==null)return 0;int a=FrameIndex,b=Math.Min(a+1,Clip.Frames.Length-1);return Clip.Spec.pathFollowsFrame?Clip.Spec.travelX[a]:Clip.Spec.travelX[a]+(Clip.Spec.travelX[b]-Clip.Spec.travelX[a])*(FramePosition-a);}}
    public double DisplacementY {get{if(Clip==null||Clip.Spec.travelY==null)return 0;int a=FrameIndex,b=Math.Min(a+1,Clip.Frames.Length-1);return Clip.Spec.pathFollowsFrame?Clip.Spec.travelY[a]:Clip.Spec.travelY[a]+(Clip.Spec.travelY[b]-Clip.Spec.travelY[a])*(FramePosition-a);}}
    public Point TouchNose {
        get {
            var rest=new Point(140,113);if(poseLife!=null&&poseLife.Active&&poseLife.Clip.Spec.noseX!=null){var activity=poseLife.Clip.Spec;return new Point(activity.noseX[poseLife.FrameIndex]+activity.originX,activity.noseY[poseLife.FrameIndex]+activity.originY);}
            if(Clip!=null&&Clip.Spec.noseX!=null)return new Point(Clip.Spec.noseX[FrameIndex]+Clip.Spec.originX,Clip.Spec.noseY[FrameIndex]+Clip.Spec.originY);if(Clip==null||!Clip.HasHold)return rest;
            Point end=Clip.Spec.id=="sleep"?new Point(94,214):new Point((Clip.Spec.eyeLeftX+Clip.Spec.eyeRightX)/2+Clip.Spec.originX,(Clip.Spec.eyeLeftY+Clip.Spec.eyeRightY)/2+Clip.Spec.originY+19);
            double anchor=Math.Max(1,Clip.Spec.anchorFrames);
            double entry=Math.Max(1,Clip.Spec.holdAt-anchor);
            double exit=Math.Max(1,Clip.Frames.Length-1-anchor-Clip.Spec.holdAt);
            double amount=Smooth((FramePosition-anchor)/entry)*(1-Smooth((FramePosition-Clip.Spec.holdAt)/exit));
            return rest+(end-rest)*amount;
        }
    }
    public MotionSpec VisualSpec {get{return poseLife!=null&&poseLife.Active?poseLife.Clip.Spec:Clip==null?null:Clip.Spec;}}
    public int VisualSize {get{return VisualSpec==null?256:VisualSpec.size==0?256:VisualSpec.size;}}
    public BitmapSource Image {get{return Clip==null?null:HoldingPose&&poseLife!=null&&poseLife.Active?poseLife.Image:HoldingPose&&idle!=null?idle.Render(holdAge,holdWeight*(poseLife==null?1:poseLife.BaseWeight)):Clip.Frames[FrameIndex];}}
    public string PostureActivity {get{return poseLife!=null&&poseLife.Active?poseLife.Clip.Spec.id:null;}}
    internal int ActivityFrame {get{return poseLife==null?0:poseLife.FrameIndex;}}
    public int PostureActivityCount {get{return poseLife==null?0:poseLife.ActivityCount;}}
    public bool PlayReady {get{return Active&&!Recovering&&HoldingPose&&Clip.Spec.id=="sit"&&poseLife!=null&&poseLife.CanPlay;}}
    public Point? ToyPaw {get{return poseLife==null?null:poseLife.ToyPaw;}}
    public bool PlayPaw(bool left){return PlayReady&&poseLife.Play(left,effectiveHoldSeconds-holdAge);}
    public bool PlayJump(Point target){return PlayReady&&poseLife.Jump(target,effectiveHoldSeconds-holdAge);}
    public void AttendToy(Point? target){
        if(idle!=null)idle.LookAt(target);if(poseLife!=null)poseLife.SetPlaying(target.HasValue);
        if(HoldingPose&&!Recovering&&target.HasValue)effectiveHoldSeconds=Math.Max(effectiveHoldSeconds,holdAge+8);
    }
    public double ResidenceSeconds {get{return effectiveHoldSeconds;}}
    public bool AtStandingExit {get{return Active&&!Recovering&&waitStanding&&FramePosition>=Clip.Spec.standExitAt;}}
    MotionLibrary library;
    PoseLife poseLife;
    double effectiveHoldSeconds,queuedHold=-1;
    bool waitStanding,queuedWalking;
    MotionClip landing;double landingHold;int landingAt;
    public void ConfigureLibrary(MotionLibrary value){library=value;}
    public bool ContactInPose(PetTouchZone zone,out bool responded){responded=false;if(!HoldingPose||Recovering||poseLife==null||!poseLife.HasActivities)return false;responded=poseLife.Contact(zone,effectiveHoldSeconds-holdAge);return true;}
    public double PoseWeight {get{if(Clip==null)return 0;if(Clip.Spec.anchorFrames>0)return FramePosition<Clip.Spec.anchorFrames||FramePosition>=Clip.Frames.Length-Clip.Spec.anchorFrames?0:1;double edge=Math.Min(8,Clip.Frames.Length*.15);double a=Math.Max(0,Math.Min(1,FramePosition/edge)),b=Math.Max(0,Math.Min(1,(Clip.Frames.Length-1-FramePosition)/edge));return a*a*(3-2*a)*b*b*(3-2*b);}}
    double frameRate;
    double playbackPosition;
    bool recoverForward;
    string recoveryPose;
    int recoveryTarget=-1;
    bool recoveryReady;
    public string RecoveredPose {get{return recoveryReady?recoveryPose:null;}}
    PostureIdle idle;
    double holdAge,holdWeight,recoverySettle;
    MotionClip queued;
    double queuedDuration;
    byte[] hitPixels;int hitAt=-1;
    public void Start(MotionClip clip,double duration){
        if(clip==null)throw new ArgumentNullException("clip");
        if(Active){queued=clip;queuedDuration=duration;queuedHold=-1;queuedWalking=false;BeginRecovery();return;}
        Begin(clip,duration,clip.Spec.holdSeconds,0);
    }
    void Begin(MotionClip clip,double duration,double hold,int start){
        Clip=clip;effectiveHoldSeconds=hold;FramePosition=playbackPosition=start;frameRate=(clip.HasHold?clip.Frames.Length+hold*clip.Spec.fps:clip.Frames.Length-1)/Math.Max(.5,duration);Recovering=false;recoverForward=false;hitAt=-1;HoldingPose=clip.HasHold&&start==clip.Spec.holdAt;holdAge=holdWeight=recoverySettle=0;queued=null;queuedHold=-1;
        idle=clip.HasHold?new PostureIdle(clip.Frames[clip.Spec.holdAt],clip.Spec):null;poseLife=clip.HasHold?new PoseLife(library==null?Enumerable.Empty<MotionClip>():library.Clips.Values.Where(c=>c.Spec.parentPose==clip.Spec.id),Environment.TickCount):null;
        waitStanding=false;queuedWalking=false;landing=null;recoveryPose=null;recoveryTarget=-1;recoveryReady=false;
    }
    public void StartResidence(MotionClip clip,double hold){if(!clip.HasHold||hold<1||hold>90)throw new ArgumentException("Invalid residence");double duration=clip.Frames.Length/clip.Spec.fps+hold;if(Active){queued=clip;queuedDuration=duration;queuedHold=hold;BeginRecovery();return;}Begin(clip,duration,hold,0);}
    bool HasStandingExit(MotionClip clip){return library!=null&&library.Clips.ContainsKey("stand")&&MotionLibrary.IsWalk(clip.Spec.id)&&clip.Spec.standExitAt>0;}
    public void StartLivingWalk(MotionClip clip,double duration){if(Active){queued=clip;queuedDuration=duration;queuedHold=-1;queuedWalking=true;BeginRecovery();return;}Begin(clip,duration,0,0);waitStanding=HasStandingExit(clip);}
    public bool CanEnterWalk(MotionClip clip){if(!Active||Recovering||!HoldingPose||poseLife!=null&&poseLife.Active)return false;int entry=Clip.Spec.id=="stand"?clip.Spec.standEntryAt:Clip.Spec.id=="sit"?clip.Spec.sitEntryAt:0;return entry>0&&MotionLibrary.SamePose(Clip.Frames[Clip.Spec.holdAt],clip.Frames[entry]);}
    public void EnterWalk(MotionClip clip,double duration){if(!CanEnterWalk(clip))throw new InvalidOperationException("Walk requires the same standing pose");int entry=Clip.Spec.id=="sit"?clip.Spec.sitEntryAt:clip.Spec.standEntryAt;Begin(clip,duration,0,entry);waitStanding=HasStandingExit(clip);}
    public bool CanEnterStanding(MotionClip standing){return Active&&!Recovering&&HoldingPose&&Clip.Spec.id=="sit"&&(poseLife==null||!poseLife.Active)&&standing.Spec.sitEntryAt>0&&MotionLibrary.SamePose(Clip.Frames[Clip.Spec.holdAt],standing.Frames[standing.Spec.sitEntryAt]);}
    public bool CanEnterSitting(MotionClip sitting){return Active&&!Recovering&&HoldingPose&&Clip.Spec.id=="stand"&&(poseLife==null||!poseLife.Active)&&Clip.Spec.sitExitAt>0&&MotionLibrary.SamePose(Clip.Frames[Clip.Spec.sitExitAt],sitting.Frames[sitting.Spec.holdAt]);}
    public double SecondsToStanding(MotionClip standing){return (standing.Spec.holdAt-standing.Spec.sitEntryAt)/standing.Spec.fps;}
    public double SecondsToSitting {get{return (Clip.Spec.sitExitAt-Clip.Spec.holdAt)/Clip.Spec.fps;}}
    public void EnterStanding(MotionClip standing,double hold){if(!CanEnterStanding(standing))throw new InvalidOperationException("Standing requires the same sitting pose");Begin(standing,standing.Frames.Length/standing.Spec.fps+hold,hold,standing.Spec.sitEntryAt);}
    public void EnterSitting(MotionClip sitting,double hold){if(!CanEnterSitting(sitting))throw new InvalidOperationException("Sitting requires the standing transition");var source=Clip;int destination=source.Spec.sitExitAt;Begin(source,source.Frames.Length/source.Spec.fps,0,source.Spec.holdAt);HoldingPose=false;landing=sitting;landingHold=hold;landingAt=destination;}
    public void ContinueStanding(double hold){if(!AtStandingExit)throw new InvalidOperationException("Walk has not reached standing");var standing=library.Clips["stand"];if(!MotionLibrary.SamePose(Clip.Frames[FrameIndex],standing.Frames[standing.Spec.holdAt]))throw new InvalidOperationException("Standing handoff pose mismatch");Begin(standing,standing.Frames.Length/standing.Spec.fps+hold,hold,standing.Spec.holdAt);}
    void BeginRecovery(){
        if(Recovering)return;landing=null;Recovering=true;recoverForward=MotionLibrary.IsWalk(Clip.Spec.id)||Clip.HasHold&&FramePosition>=Clip.Spec.holdAt;recoverySettle=HoldingPose?.25:0;if(poseLife!=null)poseLife.Exit();
        // Turn-only frames can return to their standing anchor. Once paws
        // actually advance, finish FORWARD to the next valid standing pose.
        if(queued==null&&library!=null&&MotionLibrary.IsWalk(Clip.Spec.id)&&Clip.Spec.gaitEntryAt>0){
            if(FramePosition>=Clip.Spec.standEntryAt&&FramePosition<Clip.Spec.gaitEntryAt){recoveryTarget=Clip.Spec.standEntryAt;recoveryPose="stand";recoverForward=false;}
            else if(FramePosition>=Clip.Spec.gaitEntryAt&&FramePosition<Clip.Spec.standExitAt){recoveryTarget=Clip.Spec.standExitAt;recoveryPose="stand";recoverForward=true;}
            else if(FramePosition>=Clip.Spec.sitEntryAt&&FramePosition<Clip.Spec.standEntryAt&&Clip.Spec.sitEntryAt>0){recoveryTarget=Clip.Spec.sitEntryAt;recoveryPose="sit";recoverForward=false;}
        }
    }
    public void LandRecoveredPose(double hold){if(!recoveryReady||library==null)throw new InvalidOperationException("No recovered pose");var next=library.Clips[recoveryPose];if(!MotionLibrary.SamePose(Image,next.Frames[next.Spec.holdAt]))throw new InvalidOperationException("Recovery pose mismatch");Begin(next,next.Frames.Length/next.Spec.fps+hold,hold,next.Spec.holdAt);if(hold<=0)HoldingPose=false;}
    public void Cancel(){queued=null;if(Active)BeginRecovery();}
    void Finish(){var next=queued;double duration=queuedDuration,hold=queuedHold;bool walking=queuedWalking;Clip=null;queued=null;idle=null;poseLife=null;HoldingPose=false;Recovering=false;recoverForward=false;FramePosition=0;hitAt=-1;if(next!=null){Begin(next,duration,hold>=0?hold:next.Spec.holdSeconds,0);waitStanding=walking&&HasStandingExit(next);}}
    public void Tick(double seconds){
        if(!Active)return;double dt=Math.Max(0,Math.Min(86400,seconds));
        if(Recovering){
            if(recoveryTarget>=0){FramePosition=recoverForward?Math.Min(recoveryTarget,FramePosition+dt*Clip.Spec.fps*1.8):Math.Max(recoveryTarget,FramePosition-dt*Clip.Spec.fps*1.8);if(Math.Abs(FramePosition-recoveryTarget)<.00001)recoveryReady=true;return;}
            if(poseLife!=null&&poseLife.Active){poseLife.Tick(dt,0);if(poseLife.Active)return;}
            if(recoverySettle>0){double used=Math.Min(dt,recoverySettle);holdWeight*=Math.Max(0,(recoverySettle-used)/recoverySettle);recoverySettle-=used;holdAge+=used;dt-=used;if(recoverySettle>0)return;HoldingPose=false;}
            // Recover through the actual preceding poses, rather than hard-cut
            // from an upside-down body to the upright neutral texture.
            if(recoverForward){FramePosition+=dt*Clip.Spec.fps*1.8;if(FramePosition>=Clip.Frames.Length)Finish();}
            else{FramePosition=Math.Max(0,FramePosition-dt*Math.Max(Clip.Spec.fps*1.8,FramePosition));if(FramePosition==0)Finish();}
        }else{
            playbackPosition+=dt*frameRate;
            if(playbackPosition>=Clip.Frames.Length+effectiveHoldSeconds*Clip.Spec.fps){Finish();return;}
            double holdFrames=effectiveHoldSeconds*Clip.Spec.fps;HoldingPose=Clip.HasHold&&playbackPosition>=Clip.Spec.holdAt&&playbackPosition<Clip.Spec.holdAt+holdFrames;
            if(HoldingPose){FramePosition=Clip.Spec.holdAt;holdAge=(playbackPosition-Clip.Spec.holdAt)/Clip.Spec.fps;holdWeight=Smooth(Math.Min(holdAge,effectiveHoldSeconds-holdAge)/.5);if(poseLife!=null)poseLife.Tick(dt,effectiveHoldSeconds-holdAge);}
            else FramePosition=playbackPosition-(Clip.HasHold&&playbackPosition>=Clip.Spec.holdAt+holdFrames?holdFrames:0);
            if(waitStanding&&FramePosition>=Clip.Spec.standExitAt)FramePosition=playbackPosition=Clip.Spec.standExitAt;
            if(landing!=null&&FramePosition>=landingAt){var target=landing;double hold=landingHold;Begin(target,target.Frames.Length/target.Spec.fps+hold,hold,target.Spec.holdAt);}
        }
    }
    static double Smooth(double value){double u=Math.Max(0,Math.Min(1,value));return u*u*(3-2*u);}
    public bool Hit(int x,int y){
        if(!Active)return false;int size=VisualSize;x-=VisualSpec.originX;y-=VisualSpec.originY;if(x<0||y<0||x>=size||y>=size)return false;
        if(hitAt!=FrameIndex||HoldingPose){if(hitPixels==null||hitPixels.Length!=size*size*4)hitPixels=new byte[size*size*4];Image.CopyPixels(hitPixels,size*4,0);hitAt=FrameIndex;}
        return hitPixels[(y*size+x)*4+3]>18;
    }
}
}
