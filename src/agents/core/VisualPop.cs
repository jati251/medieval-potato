using Godot;
using System;

public partial class VisualPop : Node3D
{
	[Export] public float WalkSpeed { get; set; } = 2.0f;
	
	public override void _Ready()
	{
		// We'll use this method to trigger the walk once properties are set
	}

	public void WalkPath(Vector3[] path)
	{
		if (path == null || path.Length < 2) return;

		Tween tween = CreateTween();
		
		// Move through each point in the path
		for (int i = 1; i < path.Length; i++)
		{
			Vector3 start = path[i - 1];
			Vector3 end = path[i];
			double duration = start.DistanceTo(end) / WalkSpeed;

			tween.TweenProperty(this, "global_position", end, duration)
				 .SetTrans(Tween.TransitionType.Linear);
			
			// If it's the middle of the path (the target point), wait 2 seconds
			if (i == path.Length / 2)
			{
				tween.TweenInterval(2.0f);
			}
		}

		// Delete self when finished
		tween.TweenCallback(Callable.From(QueueFree));
	}
}
