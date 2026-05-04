using Godot;
using System;
using System.Collections.Generic;

public partial class ResidencePlot : Node3D
{
	[Export] public PackedScene PopScene { get; set; }
	[Export] public Vector3 MarketPosition { get; set; } = new Vector3(10, 0, 10);
	
	public int ResidentCount { get; private set; } = 0;
	public float ConstructionProgress { get; private set; } = 0.0f;
	public float Happiness { get; private set; } = 1.0f; // 0.0 to 1.0
	public bool IsConstructed => ConstructionProgress >= 100.0f;
	public bool IsAssigned { get; set; } = false;
	public bool IsPreview { get; set; } = false;
	public string NeedsStatus { get; private set; } = "All needs met";

	private Timer _spawnTimer;
	private Timer _moodTimer;
	private GlobalSimulation _sim;
	private Node3D _visuals;
	private Node3D _scaffolding;
	private Label3D _moodEmote;
	private List<VisualPop> _activeVisualPops = new List<VisualPop>();

	public override void _Ready()
	{
		_sim = GetNode<GlobalSimulation>("/root/GlobalSimulation");
		_spawnTimer = GetNode<Timer>("Timer");
		_spawnTimer.Timeout += OnSpawnTimerTimeout;

		_visuals = GetNode<Node3D>("Visuals");
		_scaffolding = GetNode<Node3D>("Scaffolding");

		SetupMoodEmote();

		_moodTimer = new Timer();
		_moodTimer.WaitTime = 10.0f; // Check needs every 10s
		_moodTimer.Autostart = true;
		_moodTimer.Timeout += UpdateNeedsAndMood;
		AddChild(_moodTimer);

		UpdateVisuals();

		if (!IsPreview)
		{
			_sim.RegisterConstructionSite(this);
		}
	}

	private void SetupMoodEmote()
	{
		_moodEmote = new Label3D();
		_moodEmote.Text = "😟"; 
		_moodEmote.FontSize = 180; // Bigger for better visibility
		_moodEmote.Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
		_moodEmote.Position = Vector3.Up * 4.5f; // Higher above roof
		_moodEmote.Visible = false;
		_moodEmote.OutlineRenderPriority = 1;
		_moodEmote.OutlineSize = 24;
		AddChild(_moodEmote);
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
		_sim.RegisterConstructedHouse(this);
		
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

	private void UpdateNeedsAndMood()
	{
		if (!IsConstructed) return;

		float targetHappiness = 1.0f;
		List<string> issues = new List<string>();

		// Requirement 1: Proximity to Well (Water)
		if (!IsNearWell())
		{
			targetHappiness -= 0.4f;
			issues.Add("Missing Water Access");
		}

		// Requirement 2: Food Consumption
		float foodNeeded = ResidentCount * 0.2f; // Reduced consumption slightly
		if (_sim.Food >= foodNeeded)
		{
			_sim.Food -= foodNeeded;
		}
		else
		{
			targetHappiness -= 0.5f; 
			issues.Add("Starving (No Food)");
		}

		// Smoothly transition happiness
		Happiness = Mathf.Lerp(Happiness, targetHappiness, 0.2f);
		
		// Update NeedsStatus string for HUD
		if (issues.Count == 0) NeedsStatus = "All needs met";
		else NeedsStatus = string.Join(", ", issues);

		// Show general unhappy emote
		if (_moodEmote != null)
		{
			if (Happiness < 0.7f)
			{
				_moodEmote.Text = "😟";
				_moodEmote.Visible = true;
				_moodEmote.Modulate = (Happiness < 0.4f) ? Colors.Red : Colors.Yellow;
			}
			else
			{
				_moodEmote.Visible = false;
			}
		}
	}

	private bool IsNearWell()
	{
		var buildings = GetTree().GetNodesInGroup("Buildings");
		foreach (Node b in buildings)
		{
			// More robust check
			string name = b.Name.ToString().ToLower();
			bool isWell = name.Contains("well") || b.IsInGroup("Wells");
			
			if (isWell && b is Node3D b3d)
			{
				if (GlobalPosition.DistanceTo(b3d.GlobalPosition) < 45.0f) // Increased from 15.0 to 45.0
					return true;
			}
		}
		return false;
	}

	private void SpawnVisualPop()
	{
		if (PopScene == null || !IsConstructed) return;

		// Unhappy people move slower or don't go out as much?
		if (Happiness < 0.4f && GD.Randf() > 0.5f) return; 

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
