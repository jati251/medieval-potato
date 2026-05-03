using Godot;
using System;

public partial class ResidencePlot : Node3D
{
	[Export] public PackedScene PopScene { get; set; }
	[Export] public int ResidentCount { get; set; } = 5;
	[Export] public Vector3 MarketPosition { get; set; } = new Vector3(10, 0, 10);
	
	private Timer _spawnTimer;

	public override void _Ready()
	{
		// 1. Register our residents into the simulation
		var simulation = GetNode<GlobalSimulation>("/root/GlobalSimulation");
		simulation.Population += ResidentCount;
		
		// 2. Setup Spawn Timer
		_spawnTimer = GetNode<Timer>("Timer");
		_spawnTimer.WaitTime = GD.RandRange(5.0, 15.0);
		_spawnTimer.Timeout += OnSpawnTimerTimeout;
		_spawnTimer.Start();
		
		GD.Print($"Residence Plot ready with {ResidentCount} residents.");
	}

	private void OnSpawnTimerTimeout()
	{
		if (PopScene == null) return;

		// Spawn a visual agent
		var pop = PopScene.Instantiate<VisualPop>();
		GetTree().Root.AddChild(pop);
		pop.GlobalPosition = GlobalPosition;
		
		// Tell them where to go
		pop.WalkToAndBack(MarketPosition);
		
		// Randomize next spawn time
		_spawnTimer.WaitTime = GD.RandRange(10.0, 30.0);
	}
}
