using System;

namespace MaohaiziPet {
// Plan with measured native displacements. Never stretch a stride or slide a
// stationary image to make an unreachable destination look reachable.
public sealed class MovementGoals {
    bool toyUsed;
    public bool ToyWalk {get;private set;}
    public void NewTool(){toyUsed=false;ToyWalk=false;}
    public void Finished(){ToyWalk=false;}
    public static CatHabit Choose(double start,double target,double? leftDistance,double? rightDistance){
        if(double.IsNaN(target)||double.IsInfinity(target)||Math.Abs(target-start)<24)return CatHabit.None;
        double best=Math.Abs(target-start);CatHabit result=CatHabit.None;
        if(leftDistance.HasValue){double error=Math.Abs(target-(start+leftDistance.Value));if(error<best){best=error;result=CatHabit.WalkLeft;}}
        if(rightDistance.HasValue){double error=Math.Abs(target-(start+rightDistance.Value));if(error<best){result=CatHabit.WalkRight;}}
        return result;
    }
    public CatHabit Toy(double start,double target,double? leftDistance,double? rightDistance){if(toyUsed)return CatHabit.None;var action=Choose(start,target,leftDistance,rightDistance);if(action!=CatHabit.None){toyUsed=ToyWalk=true;}return action;}
}
}
