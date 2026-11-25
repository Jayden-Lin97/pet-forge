using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MaohaiziPet {
// Green-screen decontamination for the calibrated cream/brown ragdoll only.
// Work in premultiplied BGRA: alpha, red, blue and pixel positions stay exact.
public static class GreenSpill {
    internal static void Row(byte[] pixels){
        for(int at=0;at+3<pixels.Length;at+=4){int b=pixels[at],g=pixels[at+1],r=pixels[at+2],a=pixels[at+3];if(a==0)continue;
            // Preserve naturally blue irises and cool highlights.
            if(b>r+10*a/255.0&&b>=g-2*a/255.0)continue;
            int limit=Math.Min(a,(72*r+28*b+(200*a+127)/255+50)/100);
            if(g>limit)pixels[at+1]=(byte)limit;
        }
    }
    internal static void Remove(WriteableBitmap image){
        if(image.Format!=PixelFormats.Pbgra32)throw new ArgumentException("需要预乘 BGRA 像素");
        int stride=image.PixelWidth*4;var row=new byte[stride];image.Lock();
        try{for(int y=0;y<image.PixelHeight;y++){var line=IntPtr.Add(image.BackBuffer,y*image.BackBufferStride);Marshal.Copy(line,row,0,stride);Row(row);Marshal.Copy(row,0,line,stride);}image.AddDirtyRect(new Int32Rect(0,0,image.PixelWidth,image.PixelHeight));}finally{image.Unlock();}
    }
    public static BitmapSource Correct(BitmapSource source){var converted=new FormatConvertedBitmap(source,PixelFormats.Pbgra32,null,0);var result=new WriteableBitmap(converted);Remove(result);result.Freeze();return result;}
}
}
