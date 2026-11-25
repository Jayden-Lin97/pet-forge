using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace MaohaiziPet {
public sealed class PetLifeWindow:Window {
    readonly PetController controller;
    readonly TextBlock mood,favorite,summary,today,memories;
    readonly DispatcherTimer timer=new DispatcherTimer();
    static Brush Brush(string color){return (Brush)new BrushConverter().ConvertFromString(color);}
    static TextBlock Text(string value,double size,string color){return new TextBlock{Text=value,FontSize=size,Foreground=Brush(color),TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,14)};}
    public PetLifeWindow(PetController c){
        controller=c;Title=c.Settings.Name+" · 相处小记";Width=520;Height=590;MinHeight=470;WindowStartupLocation=WindowStartupLocation.CenterOwner;Background=Brush("#F5F3EC");FontFamily=new FontFamily("Microsoft YaHei");
        var content=new StackPanel{Margin=new Thickness(32)};Content=new ScrollViewer{Content=content,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
        content.Children.Add(Text(c.Settings.Name+"和你的相处小记",23,"#3E403A"));content.Children.Add(Text("一点点熟悉，一起攒下小小的快乐。",12,"#818276"));
        mood=Text("",16,"#667660");content.Children.Add(mood);favorite=Text("",13,"#667660");content.Children.Add(favorite);
        content.Children.Add(Text("今天的小记",15,"#3E403A"));today=Text("",13,"#67695F");content.Children.Add(today);
        content.Children.Add(Text("一起攒下的回忆",15,"#3E403A"));memories=Text("",13,"#67695F");content.Children.Add(memories);summary=Text("",12,"#818276");content.Children.Add(summary);
        content.Children.Add(Text("这些小记来自实际互动，保存在这台电脑上。",11,"#818276"));
        var close=new Button{Content="继续陪着你",Padding=new Thickness(18,9,18,9),HorizontalAlignment=HorizontalAlignment.Left,Background=Brush("#667660"),Foreground=Brushes.White,BorderThickness=new Thickness(0),Cursor=System.Windows.Input.Cursors.Hand};close.Click+=delegate{Close();};content.Children.Add(close);
        timer.Interval=TimeSpan.FromSeconds(1);timer.Tick+=delegate{Refresh();};Closed+=delegate{timer.Stop();c.Save();};Refresh();timer.Start();
    }
    void Refresh(){mood.Text=controller.Life.MoodLabel+" · "+controller.Companion.Familiarity;favorite.Text=controller.Life.FavoriteLabel;today.Text=controller.Life.TodayNote;memories.Text=controller.Life.Memories;summary.Text=controller.Life.Summary;}
}
}
