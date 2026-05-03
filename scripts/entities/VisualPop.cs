using Godot;
using System;

public partial class VisualPop : Node3D
{
	[Export] public float WalkSpeed { get; set; } = 2.0f;
	
	public override void _Ready()
	{
		// We'll use this method to trigger the walk once properties are set
	}

	public void WalkToAndBack(Vector3 targetPosition)
	{
		Vector3 startPos = GlobalPosition;
		double duration = startPos.DistanceTo(targetPosition) / WalkSpeed;

		Tween tween = CreateTween();
		
		// 1. Walk to target
		tween.TweenProperty(this, "global_position", targetPosition, duration)
			 .SetTrans(Tween.TransitionType.Linear);
		
		// 2. Wait 2 seconds (Market shopping)
		tween.TweenInterval(2.0f);
		
		// 3. Walk back to start
		tween.TweenProperty(this, "global_position", startPos, duration)
			 .SetTrans(Tween.TransitionType.Linear);
		
		// 4. Delete self
		tween.TweenCallback(Callable.From(QueueFree));
	}
}
