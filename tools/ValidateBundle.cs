using System;using MaohaiziPet;
public static class ValidateBundle {
 [STAThread]public static int Main(string[] args){try{var pet=PetSkin.Load(args[0]);return pet.Motions!=null&&pet.Motions.Clips.Count==int.Parse(args[1])?0:1;}catch{return 1;}}
}
