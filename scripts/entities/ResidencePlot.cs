using Godot;
using System;
using System.Collections.Generic;

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
		SpawnVisualPop();
		
		// Randomize next spawn time
		_spawnTimer.WaitTime = GD.RandRange(10.0, 30.0);
	}

	private void SpawnVisualPop()
	{
		if (PopScene == null) return;

		var pop = PopScene.Instantiate<VisualPop>();
		GetTree().Root.AddChild(pop);
		pop.GlobalPosition = GlobalPosition;

		// Find Market and RoadManager
		var root = GetTree().Root.GetNode("root");
		var market = root.GetNode<Marker3D>("MarketMarker");
		var roadMgr = root.GetNode<RoadManager>("RoadManager");

		if (market != null && roadMgr != null)
		{
			// 1. Path to Market
			Vector3[] toMarket = roadMgr.GetRoadPath(GlobalPosition, market.GlobalPosition);
			
			// 2. Path back home
			Vector3[] toHome = roadMgr.GetRoadPath(market.GlobalPosition, GlobalPosition);
			
			// 3. Combine paths for a round trip
			List<Vector3> fullTrip = new List<Vector3>(toMarket);
			// Skip the first point of the return trip as it's the same as the last point of toMarket
			for (int i = 1; i < toHome.Length; i++)
			{
				fullTrip.Add(toHome[i]);
			}

			pop.WalkPath(fullTrip.ToArray());
		}
	}
}
