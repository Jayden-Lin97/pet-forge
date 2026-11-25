using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace MaohaiziPet {
public enum DesktopPetAction { Feather, Brush, Treat, Yarn, Sit, Stand, Groom, Yawn, Comfort, Walk, Belly, Sleep, Perch, LeavePerch }

// A face press is petting; a body press can become a drag. Lock that intent
// until release so crossing another body region never switches to dragging.
public sealed class PetPointerPress {
    public bool Held {get;private set;}
    public bool Stroking {get;private set;}
    public bool Tool {get;private set;}
    public bool Dragging {get;private set;}
    public Point Down {get;private set;}
    public void Begin(PetTouchZone zone,Point screen,bool tool,bool living){
        Held=true;Down=screen;Tool=tool;Dragging=false;
        Stroking=living&&!tool&&(zone==PetTouchZone.Head||zone==PetTouchZone.Chin||zone==PetTouchZone.CheekLeft||zone==PetTouchZone.CheekRight);
    }
    public void Move(Point screen){if(Held&&!Stroking&&!Tool&&(screen-Down).Length>10)Dragging=true;}
    public void End(){Held=Stroking=Tool=Dragging=false;}
}

// The pocket is a transient popup attached to the cat, not a settings window.
// WPF dismisses it on an outside click; only this small popup captures input.
public sealed class DesktopPetPocket {
    readonly PetController c;
    readonly Popup popup;
    public readonly FrameworkElement Content;
    public bool IsOpen {get{return popup.IsOpen;}}
    static Brush Color(string value){return (Brush)new BrushConverter().ConvertFromString(value);}
    public DesktopPetPocket(PetController controller){
        c=controller;
        var paper=new Border{Width=324,Padding=new Thickness(14),CornerRadius=new CornerRadius(18),Background=Color("#FAF9F5"),BorderBrush=Color("#DDDCD4"),BorderThickness=new Thickness(1),Focusable=true};Content=paper;
        var body=new StackPanel();paper.Child=body;
        body.Children.Add(new TextBlock{Text=c.Engine.Mode==PetMode.Quiet?"现在是勿扰时间":"陪我玩一会儿",FontSize=15,FontWeight=FontWeights.SemiBold,Foreground=Color("#3D453C"),Margin=new Thickness(3,0,0,10)});
        var grid=new UniformGrid{Columns=3};body.Children.Add(grid);
        foreach(DesktopPetAction action in Enum.GetValues(typeof(DesktopPetAction))){
            if(action==DesktopPetAction.Perch||action==DesktopPetAction.LeavePerch)continue;
            if(!c.HasDesktopAction(action))continue;
            var captured=action;var content=new StackPanel{HorizontalAlignment=HorizontalAlignment.Center};
            content.Children.Add(new DesktopPetIcon(action){Width=30,Height=30,HorizontalAlignment=HorizontalAlignment.Center});
            content.Children.Add(new TextBlock{Text=Label(action),FontSize=12,HorizontalAlignment=HorizontalAlignment.Center,Margin=new Thickness(0,4,0,0)});
            var button=MakeButton(content);button.Height=68;button.Margin=new Thickness(3);button.IsEnabled=c.CanDesktopAction(action);button.ToolTip=Help(action);ToolTipService.SetShowOnDisabled(button,true);AutomationProperties.SetName(button,Label(action));AutomationProperties.SetHelpText(button,Help(action));
            button.Click+=delegate{Close();c.DoDesktopAction(captured);};grid.Children.Add(button);
        }
        body.Children.Add(new TextBlock{Text="按住脑袋轻轻摸 · 拖身体换位置\n滚轮挠挠 · 双击贴贴 · 右键放下玩具",FontSize=10,Foreground=Color("#7C8377"),Margin=new Thickness(4,10,0,8),LineHeight=16});
        var footer=new DockPanel();body.Children.Add(footer);
        var perchAction=c.Perch.Attached?DesktopPetAction.LeavePerch:DesktopPetAction.Perch;
        var perch=MakeButton(new TextBlock{Text=Label(perchAction),FontSize=11});perch.Padding=new Thickness(8,5,8,5);perch.IsEnabled=c.CanDesktopAction(perchAction);perch.ToolTip=Help(perchAction);ToolTipService.SetShowOnDisabled(perch,true);AutomationProperties.SetName(perch,Label(perchAction));perch.Click+=delegate{Close();c.DoDesktopAction(perchAction);};DockPanel.SetDock(perch,Dock.Right);footer.Children.Add(perch);
        var settings=MakeButton(new TextBlock{Text="设置",FontSize=11});settings.Padding=new Thickness(10,5,10,5);AutomationProperties.SetName(settings,"桌宠设置");settings.Click+=delegate{Close();c.OpenSettings();};DockPanel.SetDock(settings,Dock.Right);footer.Children.Add(settings);
        var quiet=MakeButton(new TextBlock{Text=c.Engine.Mode==PetMode.Quiet?"恢复陪伴":"安静陪伴",FontSize=11});quiet.Padding=new Thickness(10,5,10,5);quiet.Margin=new Thickness(0,0,6,0);AutomationProperties.SetName(quiet,c.Engine.Mode==PetMode.Quiet?"恢复陪伴":"切换勿扰");quiet.Click+=delegate{Close();c.ToggleQuiet();};DockPanel.SetDock(quiet,Dock.Right);footer.Children.Add(quiet);
        var close=MakeButton(new TextBlock{Text="收起",FontSize=11});close.Padding=new Thickness(10,5,10,5);AutomationProperties.SetName(close,"收起桌面玩具");close.Click+=delegate{Close();};footer.Children.Add(close);
        popup=new Popup{Child=paper,AllowsTransparency=true,StaysOpen=false,PopupAnimation=PopupAnimation.Fade,Placement=PlacementMode.Top};
        paper.PreviewKeyDown+=delegate(object sender,KeyEventArgs e){if(e.Key==Key.Escape){Close();e.Handled=true;}};
        popup.Opened+=delegate{paper.Focus();};
        popup.Closed+=delegate{if(c.Pet!=null)c.Pet.SuppressPointerPetting();};
    }
    public void Open(PetSurface surface){
        Rect target=surface.PetRect;
        if(c.Skin.Motion.Active){var spec=c.Skin.Motion.VisualSpec;target.Union(new Rect(target.X+spec.originX*target.Width/256,target.Y+spec.originY*target.Height/256,c.Skin.Motion.VisualSize*target.Width/256,c.Skin.Motion.VisualSize*target.Height/256));}
        popup.PlacementTarget=surface;popup.PlacementRectangle=target;popup.HorizontalOffset=(target.Width-324)/2;popup.VerticalOffset=-8;popup.IsOpen=true;
    }
    public void Close(){popup.IsOpen=false;}
    public static string Label(DesktopPetAction action){switch(action){case DesktopPetAction.Perch:return "陪在窗口边";case DesktopPetAction.LeavePerch:return "回到桌面";case DesktopPetAction.Feather:return "逗猫棒";case DesktopPetAction.Brush:return "小毛刷";case DesktopPetAction.Treat:return "小鱼零食";case DesktopPetAction.Yarn:return "毛线球";case DesktopPetAction.Sit:return "坐起来";case DesktopPetAction.Stand:return "站一会儿";case DesktopPetAction.Groom:return "舔舔毛";case DesktopPetAction.Yawn:return "打个哈欠";case DesktopPetAction.Walk:return "走两步";case DesktopPetAction.Belly:return "撒个娇";case DesktopPetAction.Sleep:return "打个盹";default:return "陪我歇歇";}}
    static string Help(DesktopPetAction action){switch(action){case DesktopPetAction.Perch:return "站稳或休息时，把猫放到刚才使用的窗口边；窗口移动时一起移动。";case DesktopPetAction.LeavePerch:return "离开窗口边，回到之前的桌面位置。";case DesktopPetAction.Feather:return "在前爪附近晃动，猫会看向羽毛并伸爪；远一点可引导走近，右键放下。";case DesktopPetAction.Yarn:return "拖住球后松手抛出，或点击方向滚球；滚到爪边会被猫拨开，右键收起。";case DesktopPetAction.Brush:return "拿起小毛刷，在身体上慢慢顺毛，右键放下。";case DesktopPetAction.Treat:return "直接喂一份小鱼零食，两份之间停顿十秒。";case DesktopPetAction.Sit:return "慢慢坐起来看看，停留后自然伏下。";case DesktopPetAction.Stand:return "四爪站起一会儿，再自然伏下。";case DesktopPetAction.Walk:return "转向要去的方向，往前走两步，停稳后继续站一会儿；需要桌面留有空间。";default:return "直接在桌面互动，不需要打开设置。";}}
    static Button MakeButton(object content){
        var b=new Button{Content=content,Padding=new Thickness(5),Cursor=Cursors.Hand,Foreground=Color("#41483D"),Background=Brushes.Transparent,BorderThickness=new Thickness(0)};
        var border=new FrameworkElementFactory(typeof(Border));border.Name="tile";border.SetValue(Border.CornerRadiusProperty,new CornerRadius(10));border.SetValue(Border.BackgroundProperty,Brushes.Transparent);
        var presenter=new FrameworkElementFactory(typeof(ContentPresenter));presenter.SetValue(ContentPresenter.HorizontalAlignmentProperty,HorizontalAlignment.Center);presenter.SetValue(ContentPresenter.VerticalAlignmentProperty,VerticalAlignment.Center);presenter.SetValue(ContentPresenter.MarginProperty,new Thickness(5));border.AppendChild(presenter);
        var template=new ControlTemplate(typeof(Button)){VisualTree=border};
        var hover=new Trigger{Property=Button.IsMouseOverProperty,Value=true};hover.Setters.Add(new Setter(Border.BackgroundProperty,Color("#E9EEE5"),"tile"));template.Triggers.Add(hover);
        var pressed=new Trigger{Property=Button.IsPressedProperty,Value=true};pressed.Setters.Add(new Setter(Border.BackgroundProperty,Color("#DCE6D6"),"tile"));template.Triggers.Add(pressed);
        var disabled=new Trigger{Property=Button.IsEnabledProperty,Value=false};disabled.Setters.Add(new Setter(UIElement.OpacityProperty,.38));template.Triggers.Add(disabled);b.Template=template;return b;
    }
}

// Original vector UI icons; these are tools, not substitutes for cat animation.
public sealed class DesktopPetIcon:FrameworkElement {
    readonly DesktopPetAction action;
    public DesktopPetIcon(DesktopPetAction value){action=value;IsHitTestVisible=false;}
    protected override void OnRender(DrawingContext d){
        base.OnRender(d);var pen=new Pen(new SolidColorBrush(Color.FromRgb(105,124,104)),1.6);pen.StartLineCap=pen.EndLineCap=PenLineCap.Round;
        var teal=new SolidColorBrush(Color.FromRgb(126,167,172));var cream=new SolidColorBrush(Color.FromRgb(220,194,151));var pink=new SolidColorBrush(Color.FromRgb(209,149,143));var ink=pen.Brush;
        if(action==DesktopPetAction.Feather){d.DrawLine(new Pen(cream,2.4),new Point(24,3),new Point(16,13));d.DrawLine(pen,new Point(16,13),new Point(12,21));d.PushTransform(new RotateTransform(28,11,21));d.DrawEllipse(teal,null,new Point(11,21),5,9);d.DrawLine(new Pen(Brushes.White,.8),new Point(11,13),new Point(11,28));d.Pop();}
        else if(action==DesktopPetAction.Brush){d.DrawRoundedRectangle(cream,pen,new Rect(12,2,6,15),2,2);d.DrawRoundedRectangle(cream,pen,new Rect(5,13,20,10),4,4);for(int i=0;i<5;i++)d.DrawLine(pen,new Point(7+i*4,23),new Point(7+i*4,28));}
        else if(action==DesktopPetAction.Treat){d.DrawGeometry(cream,pen,Geometry.Parse("M 22,15 L 29,8 L 29,22 Z"));d.DrawEllipse(cream,pen,new Point(13,15),11,7);d.DrawEllipse(ink,null,new Point(8,13),1.2,1.2);}
        else if(action==DesktopPetAction.Yarn){d.DrawEllipse(teal,null,new Point(14,14),11,11);d.DrawGeometry(null,new Pen(Brushes.White,1),Geometry.Parse("M 5,9 Q 16,10 21,20 M 5,15 Q 13,19 20,8 M 9,4 Q 6,15 19,23"));d.DrawGeometry(null,pen,Geometry.Parse("M 20,23 Q 23,30 29,25"));}
        else if(action==DesktopPetAction.Sit||action==DesktopPetAction.Stand){double y=action==DesktopPetAction.Stand?23:26;d.DrawRoundedRectangle(cream,pen,new Rect(7,9,17,y-9),6,6);d.DrawGeometry(cream,pen,Geometry.Parse("M 7,12 L 7,3 L 13,7 L 19,7 L 24,3 L 24,14 Z"));d.DrawEllipse(ink,null,new Point(12,12),1,1);d.DrawEllipse(ink,null,new Point(19,12),1,1);d.DrawLine(pen,new Point(12,20),new Point(12,28));d.DrawLine(pen,new Point(20,20),new Point(20,28));}
        else if(action==DesktopPetAction.Groom||action==DesktopPetAction.Walk){d.DrawEllipse(pink,null,new Point(15,21),7,6);foreach(var p in new[]{new Point(5,13),new Point(11,7),new Point(20,7),new Point(26,13)})d.DrawEllipse(pink,null,p,3,4);}
        else if(action==DesktopPetAction.Yawn){d.DrawGeometry(cream,null,Geometry.Parse("M 20,2 C 0,1 0,29 20,28 C 7,22 8,9 20,2 Z"));d.DrawLine(pen,new Point(24,6),new Point(24,14));d.DrawLine(pen,new Point(20,10),new Point(28,10));}
        else d.DrawGeometry(pink,null,Geometry.Parse("M 15,27 C 10,22 1,16 3,9 C 5,2 13,3 15,9 C 18,2 26,2 28,9 C 30,16 20,23 15,27 Z"));
    }
}
}
