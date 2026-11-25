using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MaohaiziPet {
// A complete generated resting pose remains the source. Only subpixel breathing
// and irregular eyelid motion run here; no major action segment is replayed.
public sealed class PostureIdle {
    readonly byte[] source,pixels;
    readonly int size,stride;
    readonly WriteableBitmap bitmap;
    readonly MotionSpec spec;
    readonly Random random=new Random(Guid.NewGuid().GetHashCode());
    readonly double phase;
    double nextBlink,blinkStart=-10,lastRender=-10,lastWeight=-1;
    Point? toy;
    double gazeX,gazeY;
    public void LookAt(Point? target){toy=target;}
    public PostureIdle(BitmapSource frame,MotionSpec motion){spec=motion;size=frame.PixelWidth;stride=size*4;source=new byte[size*stride];pixels=new byte[source.Length];new FormatConvertedBitmap(frame,PixelFormats.Pbgra32,null,0).CopyPixels(source,stride,0);bitmap=new WriteableBitmap(size,size,96,96,PixelFormats.Pbgra32,null);phase=random.NextDouble()*6.28;nextBlink=2.5+random.NextDouble()*4.5;}
    static double Clamp(double n,double a,double b){return Math.Max(a,Math.Min(b,n));}
    static double Smooth(double a,double b,double n){double u=Clamp((n-a)/(b-a),0,1);return u*u*(3-2*u);}
    struct Pixel {public double B,G,R,A;public Pixel(double b,double g,double r,double a){B=b;G=g;R=r;A=a;}}
    Pixel Sample(double x,double y){if(x<0||y<0||x>=size-1||y>=size-1)return new Pixel();int xx=(int)x,yy=(int)y,at=yy*stride+xx*4;double fx=x-xx,fy=y-yy,a=(1-fx)*(1-fy),b=fx*(1-fy),c=(1-fx)*fy,d=fx*fy;return new Pixel(source[at]*a+source[at+4]*b+source[at+stride]*c+source[at+stride+4]*d,source[at+1]*a+source[at+5]*b+source[at+stride+1]*c+source[at+stride+5]*d,source[at+2]*a+source[at+6]*b+source[at+stride+2]*c+source[at+stride+6]*d,source[at+3]*a+source[at+7]*b+source[at+stride+3]*c+source[at+stride+7]*d);}
    Pixel Eye(Pixel pixel,double x,double y,double cx,double cy,double blink){double u=(x-cx)/9.4;if(Math.Abs(u)>=1||blink<.002)return pixel;double radius=6.6*Math.Sqrt(1-u*u),top=cy-radius,bottom=cy+radius,bandTop=top-9,bandBottom=bottom+8;if(y<bandTop||y>bandBottom)return pixel;double amount=Math.Min(.999,blink),newTop=top+amount*radius*1.65,newBottom=bottom-amount*radius*.35,mapped;if(y<newTop)mapped=bandTop+(y-bandTop)*(top-bandTop)/(newTop-bandTop);else if(y>newBottom)mapped=bottom+(y-newBottom)*(bandBottom-bottom)/(bandBottom-newBottom);else mapped=top+(y-newTop)*(bottom-top)/Math.Max(.02,newBottom-newTop);double feather=(1-Smooth(.8,1,Math.Abs(u)))*Smooth(bandTop,bandTop+2,y)*(1-Smooth(bandBottom-2,bandBottom,y));return Sample(x,y+(mapped-y)*feather);}
    Pixel Look(Pixel pixel,double x,double y,double cx,double cy,double weight){
        double q=Math.Pow((x-cx)/8.0,2)+Math.Pow((y-cy)/5.8,2);if(q>=1)return pixel;
        double amount=(1-Smooth(.35,1,q))*weight;return Sample(x-gazeX*1.5*amount,y-gazeY*.9*amount);
    }
    public BitmapSource Render(double time,double weight){
        weight=Clamp(weight,0,1);if(Math.Abs(time-lastRender)<1.0/40&&Math.Abs(weight-lastWeight)<.001)return bitmap;
        double dt=lastRender<0?1.0/30:Clamp(time-lastRender,0,.1),blend=1-Math.Exp(-dt*9);
        double tx=toy.HasValue?Clamp((toy.Value.X-spec.originX-(spec.eyeLeftX+spec.eyeRightX)/2)/95,-1,1):0,ty=toy.HasValue?Clamp((toy.Value.Y-spec.originY-spec.eyeLeftY)/160,-.7,.7):0;
        gazeX+=(tx-gazeX)*blend;gazeY+=(ty-gazeY)*blend;
        if(weight<.0001){Buffer.BlockCopy(source,0,pixels,0,source.Length);}
        else {
            if(time>=nextBlink){blinkStart=time;nextBlink=time+3.2+random.NextDouble()*5.8;}
            double blinkAge=time-blinkStart,blink=(Smooth(0,.12,blinkAge)*(1-Smooth(.17,.34,blinkAge)))*weight;
            // Two incommensurate low-amplitude rhythms avoid a repeated keyframe
            // sequence. The support surface and paws stay fixed.
            double breath=(Math.Sin(time*1.48+phase)*.36+Math.Sin(time*.39+phase*.7)*.12)*weight;
            double padding=(size-320)/2.0;
            double cx=spec.id=="belly"?240:(spec.id=="sleep"?176:spec.id=="stand"?175:161)+padding;
            double cy=spec.id=="belly"?320:(spec.id=="sleep"?180:spec.id=="stand"?157:170)+padding;
            for(int y=0;y<size;y++)for(int x=0;x<size;x++){
                int at=y*stride+x*4;double torso=Math.Exp(-Math.Pow((x-cx)/78,2)-Math.Pow((y-cy)/70,2))*(1-Smooth(245+padding,265+padding,y));
                double sx=x-torso*(x-cx)*breath*.004,sy=y+torso*breath;
                Pixel pixel=Sample(sx,sy);
                if(spec.id!="sleep"&&spec.eyeRightX>spec.eyeLeftX){pixel=Look(pixel,sx,sy,spec.eyeLeftX,spec.eyeLeftY,weight);pixel=Look(pixel,sx,sy,spec.eyeRightX,spec.eyeRightY,weight);pixel=Eye(pixel,sx,sy,spec.eyeLeftX,spec.eyeLeftY,blink);pixel=Eye(pixel,sx,sy,spec.eyeRightX,spec.eyeRightY,blink);}
                pixels[at]=(byte)Clamp(pixel.B,0,255);pixels[at+1]=(byte)Clamp(pixel.G,0,255);pixels[at+2]=(byte)Clamp(pixel.R,0,255);pixels[at+3]=(byte)Clamp(pixel.A,0,255);
            }
        }
        bitmap.WritePixels(new Int32Rect(0,0,size,size),pixels,stride,0);lastRender=time;lastWeight=weight;return bitmap;
    }
}
}
