using Godot;
using System;
using System.Collections.Generic;

public partial class ResidencePlot : Node3D
{
	[Export] public PackedScene PopScene { get; set; }
	[Export] public Vector3 MarketPosition { get; set; } = new Vector3(10, 0, 10);
	
	public int ResidentCount { get; private set; } = 0;
	public float ConstructionProgress { get; private set; } = 0.0f;
	public bool IsConstructed => ConstructionProgress >= 100.0f;

	private Timer _spawnTimer;
	private GlobalSimulation _sim;
	private Node3D _visuals;
	private Node3D _scaffolding;
	private List<VisualPop> _activeVisualPops = new List<VisualPop>();

	public bool IsPreview { get; set; } = false;

	public override void _Ready()
	{
		_sim = GetNode<GlobalSimulation>("/root/GlobalSimulation");
		_spawnTimer = GetNode<Timer>("Timer");
		_spawnTimer.Timeout += OnSpawnTimerTimeout;

		_visuals = GetNode<Node3D>("Visuals");
		_scaffolding = GetNode<Node3D>("Scaffolding");

		UpdateVisuals();

		if (!IsPreview)
		{
			// Register with simulation as a pending construction site
			_sim.RegisterConstructionSite(this);
		}
	}

	public override void _ExitTree()
	{
		foreach (var pop in _activeVisualPops)
		{
			if (IsInstanceValid(pop)) pop.QueueFree();
		}
		_activeVisualPops.Clear();
	}

	public void AddProgress(float amount)
	{
		if (IsConstructed) return;

		ConstructionProgress += amount;
		if (ConstructionProgress >= 100.0f)
		{
			ConstructionProgress = 100.0f;
			FinishConstruction();
		}
	}

	private void FinishConstruction()
	{
		UpdateVisuals();
		_spawnTimer.WaitTime = GD.RandRange(5.0, 15.0);
		_spawnTimer.Start();
		
		ResidentCount = 5;
		_sim.AddPopulation(ResidentCount);
		
		// 75% Representative: Spawn 4 visual agents for 5 residents
		for (int i = 0; i < 4; i++)
		{
			CallDeferred(nameof(SpawnVisualPop));
		}
		
		GD.Print($"House construction finished! Population: {ResidentCount}");
	}

	private void UpdateVisuals()
	{
		if (_visuals != null) _visuals.Visible = IsConstructed;
		if (_scaffolding != null) _scaffolding.Visible = !IsConstructed;
	}

	public override void _Process(double delta)
	{
		if (!IsConstructed) return;

		var timeMgr = GetNodeOrNull<TimeManager>("/root/TimeManager");
		if (timeMgr != null)
		{
			var light = GetNodeOrNull<OmniLight3D>("Visuals/NightLight");
			if (light != null)
			{
				// Lights on from 18:00 to 06:00
				bool isNight = timeMgr.TimeOfDay >= 18.5f || timeMgr.TimeOfDay <= 5.5f;
				light.Visible = isNight;
				
				// Toggle windows too
				var w1 = GetNodeOrNull<MeshInstance3D>("Visuals/Window1");
				var w2 = GetNodeOrNull<MeshInstance3D>("Visuals/Window2");
				if (w1 != null) w1.Visible = isNight;
				if (w2 != null) w2.Visible = isNight;
			}
		}
	}

	private void OnSpawnTimerTimeout()
	{
		SpawnVisualPop();
		_spawnTimer.WaitTime = GD.RandRange(15.0, 45.0);
	}

	private void SpawnVisualPop()
	{
		if (PopScene == null || !IsConstructed) return;

		var pop = PopScene.Instantiate<VisualPop>();
		GetTree().Root.GetNode("root").AddChild(pop);
		pop.GlobalPosition = GlobalPosition;
		_activeVisualPops.Add(pop);

		// Clean up invalid references
		_activeVisualPops.RemoveAll(p => !IsInstanceValid(p));

		var root = GetTree().Root.GetNode("root");
		var market = root.GetNode<Marker3D>("MarketMarker");
		var roadMgr = root.GetNode<RoadManager>("RoadManager");

		if (market != null && roadMgr != null)
		{
			Vector3[] toMarket = roadMgr.GetRoadPath(GlobalPosition, market.GlobalPosition);
			Vector3[] toHome = roadMgr.GetRoadPath(market.GlobalPosition, GlobalPosition);
			
			List<Vector3> fullTrip = new List<Vector3>(toMarket);
			for (int i = 1; i < toHome.Length; i++)
			{
				fullTrip.Add(toHome[i]);
			}

			pop.WalkPath(fullTrip.ToArray());
		}
	}
}
