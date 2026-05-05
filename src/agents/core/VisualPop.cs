using Godot;
using System;

public partial class VisualPop : Node3D
{
	[Export] public float WalkSpeed { get; set; } = 2.0f;
	
	private RoadManager _roadManager;
	private float _footprintTimer = 0.0f;

	public override void _Ready()
	{
		_roadManager = GetTree().Root.FindChild("RoadManager", true, false) as RoadManager;
		AddToGroup("VisualPops");
	}

	private Tween _activeTween;
	private bool _isPanicking = false;

	public void WalkPath(Vector3[] path)
	{
		if (path == null || path.Length < 2 || _isPanicking) return;

		if (_activeTween != null) _activeTween.Kill();
		_activeTween = CreateTween();
		
		// Move through each point in the path
		for (int i = 1; i < path.Length; i++)
		{
			Vector3 start = path[i - 1];
			Vector3 end = path[i];
			double duration = start.DistanceTo(end) / WalkSpeed;
			if (duration <= 0.001) continue;

			_activeTween.TweenProperty(this, "global_position", end, duration)
				 .SetTrans(Tween.TransitionType.Linear);
			
			// If it's the middle of the path (the target point), wait 2 seconds
			if (i == path.Length / 2)
			{
				_activeTween.TweenInterval(2.0f);
			}
		}

		// Delete self when finished
		_activeTween.TweenCallback(Callable.From(QueueFree));
	}

	public void Panic(Vector3 source)
	{
		if (_isPanicking) return;
		_isPanicking = true;

		if (_activeTween != null) _activeTween.Kill();
		_activeTween = CreateTween();

		Vector3 dir = (GlobalPosition - source).Normalized();
		// Add some randomness to escape direction
		dir = dir.Rotated(Vector3.Up, (float)GD.RandRange(-Mathf.Pi / 4, Mathf.Pi / 4));
		
		Vector3 target = GlobalPosition + dir * 15.0f;
		double duration = 15.0f / (WalkSpeed * 2.5f); // Run fast!

		_activeTween.TweenProperty(this, "global_position", target, duration)
			 .SetTrans(Tween.TransitionType.Quad)
			 .SetEase(Tween.EaseType.Out);
		
		_activeTween.TweenCallback(Callable.From(QueueFree));
		
		// Visual feedback: Shake/Jump
		var mesh = GetNodeOrNull<Node3D>("Potato3D"); // Fix: Node name is Potato3D
		if (mesh != null)
		{
			Tween jump = CreateTween();
			jump.SetLoops(5);
			jump.TweenProperty(mesh, "position:y", 0.5f, 0.1f);
			jump.TweenProperty(mesh, "position:y", 0.0f, 0.1f);
		}
	}
}
