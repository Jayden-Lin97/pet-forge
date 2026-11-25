using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MaohaiziPet {
// A continuously driven 2-D character rig. This is not a reconstructed 3-D cat,
// and does not invent unavailable standing/walking/lying-down poses.
public sealed class LivingPose {
    public double Time, GazeX, GazeY, HeadX, HeadY, Tilt, Blink, Relax, Sleep, Breath, EarLeft, EarRight;
    public double BlinkLeft,BlinkRight,PawLeft,PawRight;
    public double Yawn,Stretch,Neutral,BackRub;
}
public sealed class LivingPet {
    public readonly LivingPose Pose=new LivingPose();
    readonly Random random;
    readonly byte[] source,pixels;
    readonly double[] head,body,leftEar,rightEar,leftPaw,rightPaw,jaw;
    readonly int width,height,stride;
    readonly WriteableBitmap bitmap;
    double nextBlink=1.4,blinkStart=-100,blinkDuration=.24,nextAttention=1,nextEar=2.2,earStart=-100;
    double attentionX,attentionY,gazeVX,gazeVY,tiltV,headXV,headYV,relaxV,sleepV,lastRender=-100;
    bool flickLeft=true;
    double pawLV,pawRV;
    double yawnV,stretchV,neutralV,backRubV;
    public int RenderCount {get;private set;}
    public int BlinkCount {get;private set;}
    public readonly bool ModernTexture;
    public string AnimationId {get{return ModernTexture?"ragdoll-live-v2":"ragdoll-live-v1";}}
    public Point EyeLeft {get{return ModernTexture?new Point(123.2,95.9):new Point(115.4,85.7);}}
    public Point EyeRight {get{return ModernTexture?new Point(158.8,96.3):new Point(156,86.1);}}
    public Point Nose {get{return ModernTexture?new Point(140,113):new Point(135,111);}}
    public LivingPet(BitmapSource texture,int seed):this(texture,seed,false){}
    public LivingPet(BitmapSource texture,int seed,bool modern){
        ModernTexture=modern;
        width=256;height=256;stride=width*4;random=new Random(seed);
        var visual=new DrawingVisual();using(var d=visual.RenderOpen()){RenderOptions.SetBitmapScalingMode(visual,BitmapScalingMode.HighQuality);d.DrawImage(texture,new Rect(0,0,256,256));}
        var image=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);image.Render(visual);
        source=new byte[stride*height];image.CopyPixels(source,stride,0);pixels=new byte[source.Length];
        head=new double[width*height];body=new double[head.Length];leftEar=new double[head.Length];rightEar=new double[head.Length];leftPaw=new double[head.Length];rightPaw=new double[head.Length];jaw=new double[head.Length];
        for(int y=0;y<height;y++)for(int x=0;x<width;x++){
            int i=y*width+x;
            head[i]=(1-Smooth(119,168,y))*Smooth(32,75,x)*(1-Smooth(202,242,x));
            body[i]=Smooth(89,139,y)*(1-Smooth(202,228,y))*Math.Exp(-Math.Pow((x-132)/105.0,4));
            leftEar[i]=Math.Exp(-Math.Pow((x-91)/15.0,2)-Math.Pow((y-38)/23.0,2));
            rightEar[i]=Math.Exp(-Math.Pow((x-184)/15.0,2)-Math.Pow((y-37)/23.0,2));
            leftPaw[i]=Math.Exp(-Math.Pow((x-106)/19.0,2)-Math.Pow((y-219)/15.0,2));
            rightPaw[i]=Math.Exp(-Math.Pow((x-160)/19.0,2)-Math.Pow((y-219)/15.0,2));
            jaw[i]=Smooth(109,120,y)*Math.Exp(-Math.Pow((x-135)/20.0,2)-Math.Pow((y-125)/21.0,2));
        }
        bitmap=new WriteableBitmap(width,height,96,96,PixelFormats.Pbgra32,null);Render();
    }
    static double Clamp(double x,double a,double b){return Math.Max(a,Math.Min(b,x));}
    static double Smooth(double a,double b,double x){double u=Clamp((x-a)/(b-a),0,1);return u*u*(3-2*u);}
    static void Spring(ref double value,ref double velocity,double target,double dt,double stiffness){velocity+=(target-value)*stiffness*dt;velocity*=Math.Exp(-Math.Sqrt(stiffness)*1.9*dt);value+=velocity*dt;}
    public void Advance(double dt,PetAction action,PetMode mode,Point? mouse){Advance(dt,action,mode,mouse,null);}
    public void Advance(double dt,PetAction action,PetMode mode,Point? mouse,CompanionSignals companion){Advance(dt,action,mode,mouse,companion,false);}
    public void Advance(double dt,PetAction action,PetMode mode,Point? mouse,CompanionSignals companion,bool neutral){
        dt=Clamp(dt,0,.1);if(dt==0)return;int steps=(int)Math.Ceiling(dt/.0167);double step=dt/steps;
        for(int k=0;k<steps;k++)AdvanceStep(step,action,mode,mouse,companion,neutral);
    }
    void AdvanceStep(double dt,PetAction action,PetMode mode,Point? mouse,CompanionSignals companion,bool neutral){
        Pose.Time+=dt;double t=Pose.Time;
        Spring(ref Pose.Neutral,ref neutralV,neutral?1:0,dt,100);
        var friend=mode==PetMode.Quiet?null:companion;
        double nuzzle=friend==null?0:friend.Nuzzle,bow=friend==null?0:friend.Bow,curiosity=friend==null?0:friend.Curiosity;
        double touchHead=friend==null?0:friend.TouchHead,touchChin=friend==null?0:friend.TouchChin,touchCheek=friend==null?0:friend.TouchCheek,touchBack=friend==null?0:friend.TouchBack,touchNose=friend==null?0:friend.TouchNose,touchSide=friend==null?0:friend.TouchSide;
        Spring(ref Pose.BackRub,ref backRubV,neutral?0:touchBack,dt,35);
        Spring(ref Pose.Yawn,ref yawnV,friend==null?0:friend.Yawn,dt,48);
        Spring(ref Pose.Stretch,ref stretchV,friend==null?0:friend.FrontStretch,dt,35);
        double hunt=friend==null?0:friend.Hunt;
        if(t>=nextAttention){attentionX=random.NextDouble()*1.2-.6;attentionY=random.NextDouble()*.55-.25;nextAttention=t+2.5+random.NextDouble()*4.5;}
        double targetX=attentionX,targetY=attentionY;
        if(mouse.HasValue&&mode!=PetMode.Quiet){targetX=Clamp(mouse.Value.X,-1,1);targetY=Clamp(mouse.Value.Y,-.8,.8);}
        if(friend!=null&&friend.Play>0){targetX=friend.ToyX;targetY=.65;}
        if(friend!=null&&friend.PointerToy>0){targetX=friend.PointerToyX;targetY=friend.PointerToyY;}
        if(friend!=null&&friend.LookWeight>0){targetX=targetX*(1-friend.LookWeight)+friend.LookX*friend.LookWeight;targetY=targetY*(1-friend.LookWeight)+friend.LookY*friend.LookWeight;}
        if(curiosity>0){targetX*=1-curiosity;targetY-=curiosity*.25;}
        bool pet=action==PetAction.Pet&&mode!=PetMode.Quiet&&!neutral;
        double targetSleep=action==PetAction.Sleep&&(friend==null||friend.Play+nuzzle+curiosity+friend.PawRight+bow+friend.Yawn+friend.FrontStretch==0)?1:0;
        if(friend!=null)targetSleep=Math.Max(targetSleep,friend.Rest);if(neutral)targetSleep=0;
        Spring(ref Pose.Sleep,ref sleepV,targetSleep,dt,20);
        Spring(ref Pose.Relax,ref relaxV,pet?1:Math.Max(touchHead*.85,Math.Max(touchCheek*.7,touchBack*.5)),dt,38);
        targetX*=1-Pose.Sleep*.96;targetY*=1-Pose.Sleep*.96;
        if(neutral){targetX=0;targetY=0;}
        Spring(ref Pose.GazeX,ref gazeVX,targetX,dt,42);
        Spring(ref Pose.GazeY,ref gazeVY,targetY,dt,38);
        double tilt=Pose.GazeX*.025+Math.Sin(t*.42)*.006+Pose.Relax*.025+nuzzle*.045-curiosity*.04+touchSide*.065-touchNose*.035;
        Spring(ref Pose.Tilt,ref tiltV,neutral?0:tilt,dt,neutral?80:35);
        Spring(ref Pose.HeadX,ref headXV,neutral?0:Pose.GazeX*2.9+Pose.Relax*.9+nuzzle*2.4+touchSide*3.2,dt,neutral?80:37);
        Spring(ref Pose.HeadY,ref headYV,neutral?0:Pose.GazeY*1.8-Pose.Relax*1.8+Pose.Sleep*1.2-nuzzle*1.5-curiosity*2+bow*3.8-Pose.Yawn*2.4+Pose.Stretch*5+hunt*2-touchChin*5.5-touchHead*1.5+touchBack*1.1+touchNose*2.4,dt,neutral?80:30);
        if(t>=nextBlink){blinkStart=t;blinkDuration=.18+random.NextDouble()*.1;nextBlink=t+(random.NextDouble()<.12?.45:2.5+random.NextDouble()*4.3);BlinkCount++;}
        double age=(t-blinkStart)/blinkDuration,blink=0;
        if(age>=0&&age<1){if(age<.32)blink=Smooth(0,.32,age);else if(age<.44)blink=1;else blink=1-Smooth(.44,1,age);}
        Pose.Blink=Clamp(Math.Max(blink,Math.Max(Pose.Relax*.86,Pose.Sleep*.99)),0,1);
        Pose.BlinkLeft=Math.Max(Pose.Yawn*.99,Math.Max(Pose.Blink,friend==null?0:Math.Max(friend.SlowBlink,friend.Wink)));
        Pose.BlinkRight=Math.Max(Pose.Yawn*.99,Math.Max(Pose.Blink,friend==null?0:friend.SlowBlink*(1-friend.Wink*.65)));
        double touchBlink=Math.Max(touchChin*.94,Math.Max(touchHead*.8,Math.Max(touchCheek*.86,touchNose*.9)));Pose.BlinkLeft=Math.Max(Pose.BlinkLeft,touchBlink);Pose.BlinkRight=Math.Max(Pose.BlinkRight,touchBlink);
        Pose.BlinkLeft*=1-Pose.Neutral;Pose.BlinkRight*=1-Pose.Neutral;
        double rate=.27-Pose.Sleep*.07;
        Pose.Breath=Math.Sin(t*Math.PI*2*rate)*(.9+Pose.Relax*.1)+Math.Sin(t*.31)*.08;
        Pose.Breath*=1-Pose.Neutral;
        if(t>=nextEar){earStart=t;flickLeft=random.NextDouble()<.5;nextEar=t+4+random.NextDouble()*7;}
        double earAge=t-earStart;double flick=earAge>=0&&earAge<.55?Math.Sin(earAge/.55*Math.PI*2)*Math.Sin(earAge/.55*Math.PI):0;
        Pose.EarLeft=(flickLeft?flick:0)*1.9-Pose.Relax*.5-curiosity*.8;Pose.EarRight=(flickLeft?0:flick)*1.9+Pose.Relax*.4+curiosity*.8;
        Pose.EarLeft*=1-Pose.Neutral;Pose.EarRight*=1-Pose.Neutral;
        double knead=friend==null?0:friend.Knead;
        Spring(ref Pose.PawLeft,ref pawLV,neutral?0:(friend==null?0:friend.PawLeft*5+friend.TouchPawLeft*7.5+friend.PointerPawLeft*7)+knead*Math.Max(0,Math.Sin(t*4.2))*2.6,dt,60);
        Spring(ref Pose.PawRight,ref pawRV,neutral?0:(friend==null?0:friend.PawRight*5+friend.TouchPawRight*7.5+friend.PointerPawRight*7)+knead*Math.Max(0,-Math.Sin(t*4.2))*2.6,dt,60);
    }
    public Point MapToSource(Point point){
        int x=(int)Clamp(point.X,0,255),y=(int)Clamp(point.Y,0,255);int i=y*width+x;
        double hw=head[i],bw=body[i],rx=point.X-134,ry=point.Y-141,sin=Math.Sin(Pose.Tilt),cos=Math.Cos(Pose.Tilt);
        double dx=hw*(Pose.HeadX+(cos-1)*rx-sin*ry)+bw*(point.X-132)*(Pose.Breath*.006+Pose.Stretch*.025)+leftEar[i]*Pose.EarLeft+rightEar[i]*Pose.EarRight;
        double dy=hw*(Pose.HeadY+sin*rx+(cos-1)*ry)-bw*(228-point.Y)*Pose.Breath*.008-leftPaw[i]*Pose.PawLeft-rightPaw[i]*Pose.PawRight+jaw[i]*Pose.Yawn*10-bw*Pose.BackRub*1.8;
        return new Point(point.X-dx,point.Y-dy);
    }
    struct Pixel {public double B,G,R,A;public Pixel(double b,double g,double r,double a){B=b;G=g;R=r;A=a;}}
    Pixel Sample(double x,double y){
        if(x<0||y<0||x>=width-1||y>=height-1)return new Pixel();
        int xx=(int)x,yy=(int)y,i=yy*stride+xx*4;double fx=x-xx,fy=y-yy,a=(1-fx)*(1-fy),b=fx*(1-fy),c=(1-fx)*fy,d=fx*fy;
        return new Pixel(source[i]*a+source[i+4]*b+source[i+stride]*c+source[i+stride+4]*d,
          source[i+1]*a+source[i+5]*b+source[i+stride+1]*c+source[i+stride+5]*d,
          source[i+2]*a+source[i+6]*b+source[i+stride+2]*c+source[i+stride+6]*d,
          source[i+3]*a+source[i+7]*b+source[i+stride+3]*c+source[i+stride+7]*d);
    }
    static Pixel Mix(Pixel a,Pixel b,double u){u=Clamp(u,0,1);return new Pixel(a.B+(b.B-a.B)*u,a.G+(b.G-a.G)*u,a.R+(b.R-a.R)*u,a.A+(b.A-a.A)*u);}
    Pixel Mouth(Pixel pixel,double x,double y){
        if(Pose.Yawn<.02)return pixel;
        double amount=Pose.Yawn,cx=135,cy=118+amount*8,rx=2+amount*9.5,ry=.7+amount*16;
        double v=(y-cy)/ry,u=(x-cx)/(rx*(.82+.18*(1-v))),q=u*u+v*v;
        if(q>1.13)return pixel;
        double edge=1-Smooth(.84,1.13,q),light=Clamp((v+1)*.5,0,1),grain=(Math.Sin(x*2.17+y*1.13)+Math.Sin(x*.73-y*2.39))*1.6;
        Pixel inside=new Pixel(27+light*7+grain,20+light*5+grain,44+light*11+grain,255);
        // Soft lip rim and mouth shading, not a flat dark oval with white stickers.
        double rim=Smooth(.62,.98,q)*.7;
        inside=Mix(inside,new Pixel(66+grain,48+grain,89+grain,255),rim);
        double tongue=Math.Pow(u/.64,2)+Math.Pow((v-.58)/.46,2);
        if(tongue<1.15){double tu=(1-Smooth(.75,1.15,tongue))*.92,highlight=(1-Math.Min(1,tongue))*15;
            double groove=Math.Exp(-u*u*160)*Smooth(.12,.4,v)*(1-Smooth(.75,.9,v))*15;
            inside=Mix(inside,new Pixel(103+highlight+grain-groove,84+highlight+grain-groove,164+highlight+grain-groove,255),tu);}
        // Small softly shaded canines at the upper sides; taper to a rounded tip.
        if(amount>.65&&v>-.66&&v<-.05){double center=.64-(v+.66)*.13,half=.095*(1-Smooth(-.66,-.05,v));
            double tooth=(1-Smooth(half,half+.055,Math.Abs(Math.Abs(u)-center)))*(1-Smooth(-.2,-.05,v));
            inside=Mix(inside,new Pixel(165+grain,181+grain,190+grain,255),tooth*.8);}
        return Mix(pixel,inside,edge*Smooth(.02,.15,amount));
    }
    Pixel Eye(Pixel pixel,double x,double y,double cx,double cy,double blink){
        const double rx=9.7,ry=7.0;double u=(x-cx)/rx;if(Math.Abs(u)>=1)return pixel;
        double irisQ=Math.Pow((x-cx)/7.4,2)+Math.Pow((y-cy)/5.4,2);
        if(irisQ<.86&&blink<.95){double weight=Clamp((.86-irisQ)/.35,0,1);pixel=Mix(pixel,Sample(x-Pose.GazeX*1.1,y-Pose.GazeY*.55),weight);}
        if(blink>.002){
            // Deform the eyelid texture continuously instead of painting patches over the iris.
            double radius=ry*Math.Sqrt(1-u*u),oldTop=cy-radius,oldBottom=cy+radius;
            double bandTop=oldTop-10,bandBottom=oldBottom+8;
            if(y<bandTop||y>bandBottom)return pixel;
            double amount=Math.Min(.999,blink),newTop=oldTop+amount*radius*1.65,newBottom=oldBottom-amount*radius*.35,mapped;
            if(y<newTop)mapped=bandTop+(y-bandTop)*(oldTop-bandTop)/(newTop-bandTop);
            else if(y>newBottom)mapped=oldBottom+(y-newBottom)*(bandBottom-oldBottom)/(bandBottom-newBottom);
            else mapped=oldTop+(y-newTop)*(oldBottom-oldTop)/Math.Max(.02,newBottom-newTop);
            double feather=(1-Smooth(.83,1,Math.Abs(u)))*Smooth(bandTop,bandTop+2,y)*(1-Smooth(bandBottom-2,bandBottom,y));
            pixel=Sample(x,y+(mapped-y)*feather);
            if(amount>.88){double line=(newTop+newBottom)*.5,crease=Clamp(1-Math.Abs(y-line)/.55,0,1)*feather*Smooth(.88,1,amount);
                Pixel fur=Sample(x,oldTop-2);Pixel dark=new Pixel(fur.B*.38,fur.G*.37,fur.R*.38,fur.A);pixel=Mix(pixel,dark,crease*.85);}
        }
        return pixel;
    }
    public BitmapSource Render(){
        if(Pose.Time-lastRender<1.0/40)return bitmap;
        double sin=Math.Sin(Pose.Tilt),cos=Math.Cos(Pose.Tilt),hx=Pose.HeadX,hy=Pose.HeadY,breath=Pose.Breath;
        for(int y=0;y<height;y++)for(int x=0;x<width;x++){
            int i=y*width+x,at=i*4;double hw=head[i],bw=body[i],rx=x-134,ry=y-141;
            double sx=x-hw*(hx+(cos-1)*rx-sin*ry)-bw*(x-132)*(breath*.006+Pose.Stretch*.025)-leftEar[i]*Pose.EarLeft-rightEar[i]*Pose.EarRight;
            double sy=y-hw*(hy+sin*rx+(cos-1)*ry)+bw*(228-y)*breath*.008+leftPaw[i]*Pose.PawLeft+rightPaw[i]*Pose.PawRight-jaw[i]*Pose.Yawn*10+bw*Pose.BackRub*1.8;
            Pixel p=Sample(sx,sy);
            if(sy>EyeLeft.Y-22&&sy<EyeRight.Y+22&&sx>EyeLeft.X-12&&sx<EyeRight.X+12){p=Eye(p,sx,sy,EyeLeft.X,EyeLeft.Y,Pose.BlinkLeft);p=Eye(p,sx,sy,EyeRight.X,EyeRight.Y,Pose.BlinkRight);}
            if(sy>106&&sy<153&&sx>119&&sx<151)p=Mouth(p,sx,sy);
            pixels[at]=(byte)Clamp(p.B,0,255);pixels[at+1]=(byte)Clamp(p.G,0,255);pixels[at+2]=(byte)Clamp(p.R,0,255);pixels[at+3]=(byte)Clamp(p.A,0,255);
        }
        bitmap.WritePixels(new Int32Rect(0,0,width,height),pixels,stride,0);lastRender=Pose.Time;RenderCount++;return bitmap;
    }
    public bool Hit(int x,int y){Render();return x>=0&&y>=0&&x<width&&y<height&&pixels[y*stride+x*4+3]>18;}
}
}
