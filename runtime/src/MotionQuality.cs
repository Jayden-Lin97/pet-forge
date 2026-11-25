using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MaohaiziPet {
// Quantitative screening assists visual review. It cannot certify cat identity
// or replace checking the original poses and the interpolated animation.
public static class MotionQuality {
    public static double TorsoWidth(BitmapSource image){
        int size=image.PixelWidth,origin=(size-256)/2;var b=new FormatConvertedBitmap(image,PixelFormats.Pbgra32,null,0);byte[] p=new byte[size*size*4];b.CopyPixels(p,size*4,0);var widths=new List<int>();
        for(int y=175+origin;y<=210+origin;y++){int left=size,right=-1;for(int x=0;x<size;x++)if(p[(y*size+x)*4+3]>=128){left=Math.Min(left,x);right=Math.Max(right,x);}if(right>=left)widths.Add(right-left+1);}
        return widths.Count==0?0:widths.OrderBy(v=>v).ElementAt(widths.Count/2);
    }
    public static double SeatedArea(BitmapSource image){
        int size=image.PixelWidth,origin=(size-256)/2;var b=new FormatConvertedBitmap(image,PixelFormats.Pbgra32,null,0);byte[] p=new byte[size*size*4];b.CopyPixels(p,size*4,0);double area=0;
        for(int y=170+origin;y<237+origin;y++)for(int x=0;x<size;x++)area+=p[(y*size+x)*4+3]/255.0;return area;
    }
    public static int MouthAperture(BitmapSource image){int size=image.PixelWidth,o=(size-256)/2;var b=new FormatConvertedBitmap(image,PixelFormats.Pbgra32,null,0);byte[] p=new byte[size*size*4];b.CopyPixels(p,size*4,0);int count=0;for(int y=90+o;y<153+o;y++)for(int x=116+o;x<166+o;x++){int i=(y*size+x)*4;if(p[i+3]>200&&p[i]<100&&p[i+1]<100&&p[i+2]<110)count++;}return count;}
}
}
