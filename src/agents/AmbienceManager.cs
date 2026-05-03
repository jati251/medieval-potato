using Godot;
using System;

public partial class AmbienceManager : Node
{
	[Export] public PackedScene PopScene { get; set; }
	[Export] public NodePath RoadPath { get; set; }
	[Export] public float BaseSpawnRate { get; set; } = 5.0f; // Seconds per spawn
	
	private Path3D _road;
	private GlobalSimulation _sim;
	private double _timeSinceLastSpawn = 0.0;

	public override void _Ready()
	{
		_road = GetNode<Path3D>(RoadPath);
		_sim = GetNode<GlobalSimulation>("/root/GlobalSimulation");
	}

	public override void _Process(double delta)
	{
		// Disabled: Villagers without houses are removed from the concept.
		/*
		_timeSinceLastSpawn += delta;
		float adjustedSpawnRate = BaseSpawnRate / Mathf.Max(1.0f, _sim.Population / 5.0f);
		if (_timeSinceLastSpawn >= adjustedSpawnRate)
		{
			SpawnAmbiencePop();
			_timeSinceLastSpawn = 0.0;
		}
		*/
	}

	private void SpawnAmbiencePop()
	{
		if (PopScene == null || _road == null) return;

		var roadMgr = GetTree().Root.GetNode<RoadManager>("root/RoadManager");
		if (roadMgr == null) return;

		// 1. Create the pop
		var pop = PopScene.Instantiate<VisualPop>();
		GetTree().Root.AddChild(pop);
		
		// 2. Set start and end points from the Path3D
		Curve3D curve = _road.Curve;
		Vector3 startPos = _road.ToGlobal(curve.GetPointPosition(0));
		Vector3 endPos = _road.ToGlobal(curve.GetPointPosition(curve.PointCount - 1));

		pop.GlobalPosition = startPos;
		
		// 3. Request a path through the road network
		Vector3[] path = roadMgr.GetRoadPath(startPos, endPos);
		
		// 4. Randomize speed and walk
		pop.WalkSpeed = (float)GD.RandRange(1.5, 3.0);
		pop.WalkPath(path);
	}
}
