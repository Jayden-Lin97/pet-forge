using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;

namespace MaohaiziPet {
// Geometry and ownership are separate: this code moves only the pet window.
public sealed class WindowPerch {
    public bool Attached {get;private set;}
    public IntPtr Window {get;private set;}
    public Point Original {get;private set;}
    double horizontal;
    bool topEdge;
    public static bool Place(Rect window,Rect work,double spriteWidth,double headClear,double footOffset,double spriteOffsetX,double fraction,bool top,out Point position){
        position=new Point();if(window.IsEmpty||work.IsEmpty||window.Width<spriteWidth+16||window.Height<100)return false;
        // Maximized windows include invisible resize borders beyond the work
        // area. Keep the perch on the visible lower edge, above the taskbar.
        double floor=top?window.Top:Math.Min(window.Bottom,work.Bottom)-8;
        if(floor-headClear<work.Top+4||floor>work.Bottom-4)return false;
        double left=Math.Max(work.Left+4,window.Left+8),right=Math.Min(work.Right-4,window.Right-8)-spriteWidth;
        if(right<left)return false;position=new Point(left+Math.Max(0,Math.Min(1,fraction))*(right-left)-spriteOffsetX,floor-footOffset);return true;
    }
    public bool Attach(IntPtr window,Rect bounds,Rect work,Point original,double spriteWidth,double headClear,double footOffset,double spriteOffsetX,out Point position){
        horizontal=bounds.Width<=spriteWidth?0.5:Math.Max(0,Math.Min(1,(original.X+spriteOffsetX-bounds.Left)/(bounds.Width-spriteWidth)));
        topEdge=bounds.Top-headClear>=work.Top+4;
        if(window==IntPtr.Zero||!Place(bounds,work,spriteWidth,headClear,footOffset,spriteOffsetX,horizontal,topEdge,out position)){position=new Point();return false;}
        Original=original;Window=window;Attached=true;return true;
    }
    public bool Follow(Rect bounds,Rect work,double spriteWidth,double headClear,double footOffset,double spriteOffsetX,out Point position){
        if(Attached&&Place(bounds,work,spriteWidth,headClear,footOffset,spriteOffsetX,horizontal,topEdge,out position))return true;
        position=new Point();Detach();return false;
    }
    public void Detach(){Attached=false;Window=IntPtr.Zero;}
}
internal static class DesktopWindowProbe {
    [StructLayout(LayoutKind.Sequential)] struct Box {public int Left,Top,Right,Bottom;}
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] static extern bool IsIconic(IntPtr h);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h,out Box r);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h,out uint pid);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetClassName(IntPtr h,StringBuilder name,int size);
    static readonly uint ownPid=(uint)Process.GetCurrentProcess().Id;
    public static IntPtr Foreground(){var h=GetForegroundWindow();uint pid;GetWindowThreadProcessId(h,out pid);if(h==IntPtr.Zero||pid==ownPid)return IntPtr.Zero;var cls=new StringBuilder(128);GetClassName(h,cls,128);return cls.ToString()=="Progman"||cls.ToString()=="WorkerW"?IntPtr.Zero:h;}
    public static bool Bounds(IntPtr h,out Rect rect){Box r;rect=Rect.Empty;if(h==IntPtr.Zero||!IsWindowVisible(h)||IsIconic(h)||!GetWindowRect(h,out r)||r.Right<=r.Left||r.Bottom<=r.Top)return false;rect=new Rect(r.Left,r.Top,r.Right-r.Left,r.Bottom-r.Top);return true;}
}
}
