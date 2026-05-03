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
	private Label3D _statusLabel;
	private List<VisualPop> _activePops = new List<VisualPop>();

	public override void _Ready()
	{
		_sim = GetNode<GlobalSimulation>("/root/GlobalSimulation");
		_spawnTimer = GetNode<Timer>("Timer");
		_spawnTimer.Timeout += OnSpawnTimerTimeout;

		_visuals = GetNode<Node3D>("Visuals");
		_scaffolding = GetNode<Node3D>("Scaffolding");
		_statusLabel = GetNodeOrNull<Label3D>("Visuals/StatusLabel");

		UpdateVisuals();
		
		// Register with simulation as a pending construction site
		_sim.RegisterConstructionSite(this);
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
		UpdateStatusLabel();
		_spawnTimer.WaitTime = GD.RandRange(5.0, 15.0);
		_spawnTimer.Start();
		_sim.AddPopulation(5);
		ResidentCount = 5;
		GD.Print("House construction finished!");
	}

	private void UpdateStatusLabel()
	{
		if (_statusLabel != null)
		{
			_statusLabel.Text = $"Pops: {ResidentCount}";
		}
	}

	private void UpdateVisuals()
	{
		if (_visuals != null) _visuals.Visible = IsConstructed;
		if (_scaffolding != null) _scaffolding.Visible = !IsConstructed;
	}

	private void OnSpawnTimerTimeout()
	{
		int maxVisualPops = Mathf.FloorToInt(ResidentCount * 0.75f);
		if (_activePops.Count < maxVisualPops)
		{
			SpawnVisualPop();
		}
		_spawnTimer.WaitTime = GD.RandRange(10.0, 30.0);
	}

	private void SpawnVisualPop()
	{
		if (PopScene == null || !IsConstructed) return;

		var pop = PopScene.Instantiate<VisualPop>();
		GetTree().Root.GetNode("root").AddChild(pop);
		pop.GlobalPosition = GlobalPosition;

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

			_activePops.Add(pop);
			pop.TreeExiting += () => _activePops.Remove(pop);
			pop.WalkPath(fullTrip.ToArray());
		}
		else
		{
			// If no road/market, just let them walk around then disappear
			_activePops.Add(pop);
			pop.TreeExiting += () => _activePops.Remove(pop);
			pop.WalkPath(new Vector3[] { GlobalPosition, GlobalPosition + new Vector3(2, 0, 2) });
		}
	}
}
