using Godot;
using System;
using System.Collections.Generic;

public partial class ZoneManager : Node3D
{
	[Export] public float GridSize { get; set; } = 0.4f; // Even denser
	[Export] public float BrushRadius { get; set; } = 3.0f;
	[Export] public Color ResidentialZoneColor { get; set; } = new Color(0.3f, 1.0f, 0.3f, 0.15f);

	private Dictionary<Vector2I, bool> _residentialZones = new Dictionary<Vector2I, bool>();
	private MultiMeshInstance3D _multiMesh;
	private List<Vector2I> _activeZones = new List<Vector2I>();

	private bool _isPainting = false;
	private bool _visualsDirty = false;

	public bool IsPainting 
	{ 
		get => _isPainting; 
		set {
			_isPainting = value;
			if (_multiMesh != null) _multiMesh.Visible = _isPainting;
		}
	}

	public override void _Ready()
	{
		SetupMultiMesh();
		
		// Auto-spawn timer
		var timer = new Timer();
		timer.WaitTime = 3.0f; // Faster check for fluid zoning
		timer.Autostart = true;
		timer.Timeout += OnAutoSpawnCheck;
		AddChild(timer);
	}

	private void SetupMultiMesh()
	{
		_multiMesh = new MultiMeshInstance3D();
		_multiMesh.Multimesh = new MultiMesh();
		_multiMesh.Multimesh.TransformFormat = MultiMesh.TransformFormatEnum.Transform3D;
		_multiMesh.Multimesh.InstanceCount = 8000; // Double the density for richness
		_multiMesh.Multimesh.VisibleInstanceCount = 0;

		var mesh = new PlaneMesh();
		mesh.Size = new Vector2(GridSize * 2.5f, GridSize * 2.5f); // Larger overlap for blending
		
		var mat = new ShaderMaterial();
		mat.Shader = new Shader();
		mat.Shader.Code = @"
shader_type spatial;
render_mode unshaded, blend_add, depth_draw_never, cull_disabled;

uniform vec4 zone_color : source_color = vec4(0.3, 1.0, 0.3, 0.1);

void fragment() {
    float dist = distance(UV, vec2(0.5));
    
    // Very soft circle base
    float circle = smoothstep(0.5, 0.0, dist);
    
    // Dynamic noise grain
    vec2 world_uv = (INV_VIEW_MATRIX * vec4(VERTEX, 1.0)).xz * 3.0;
    float n = fract(sin(dot(world_uv, vec2(12.9898, 78.233))) * 43758.5453);
    
    ALBEDO = zone_color.rgb * (0.5 + n * 0.5);
    ALPHA = circle * zone_color.a * (0.3 + n * 0.7);
}";
		mat.SetShaderParameter("zone_color", ResidentialZoneColor);
		
		mesh.Material = mat;
		_multiMesh.Multimesh.Mesh = mesh;
		_multiMesh.Visible = false;
		AddChild(_multiMesh);
	}

	public void PaintZone(Vector3 worldPos)
	{
		// Paint in a radius
		for (float x = -BrushRadius; x <= BrushRadius; x += GridSize)
		{
			for (float z = -BrushRadius; z <= BrushRadius; z += GridSize)
			{
				Vector3 offset = new Vector3(x, 0, z);
				if (offset.Length() <= BrushRadius)
				{
					Vector2I coords = WorldToGrid(worldPos + offset);
					if (!_residentialZones.ContainsKey(coords) || !_residentialZones[coords])
					{
						_residentialZones[coords] = true;
						_visualsDirty = true;
					}
				}
			}
		}
	}

	public void EraseZone(Vector3 worldPos)
	{
		for (float x = -BrushRadius; x <= BrushRadius; x += GridSize)
		{
			for (float z = -BrushRadius; z <= BrushRadius; z += GridSize)
			{
				Vector3 offset = new Vector3(x, 0, z);
				if (offset.Length() <= BrushRadius)
				{
					Vector2I coords = WorldToGrid(worldPos + offset);
					if (_residentialZones.ContainsKey(coords) && _residentialZones[coords])
					{
						_residentialZones[coords] = false;
						_visualsDirty = true;
					}
				}
			}
		}
	}

	public override void _Process(double delta)
	{
		if (_visualsDirty)
		{
			UpdateVisuals();
			_visualsDirty = false;
		}
	}

	private void UpdateVisuals()
	{
		_activeZones.Clear();
		foreach (var pair in _residentialZones)
		{
			if (pair.Value) _activeZones.Add(pair.Key);
		}

		int count = Mathf.Min(_activeZones.Count, 8000);
		_multiMesh.Multimesh.VisibleInstanceCount = count;
		for (int i = 0; i < count; i++)
		{
			Vector3 pos = GridToWorld(_activeZones[i]);
			pos.Y = 0.12f; // Low to ground
			
			// Seed-based randoms for stability
			float seed = (float)(_activeZones[i].X * 1337 + _activeZones[i].Y);
			float jitterX = (Mathf.PosMod(seed, 10) / 10.0f - 0.5f) * 0.4f;
			float jitterZ = (Mathf.PosMod(seed * 7, 10) / 10.0f - 0.5f) * 0.4f;
			float rot = (Mathf.PosMod(seed * 3, 10) / 10.0f) * Mathf.Pi * 2.0f;
			
			pos.X += jitterX;
			pos.Z += jitterZ;

			Transform3D xform = new Transform3D(new Basis(Vector3.Up, rot), pos);
			_multiMesh.Multimesh.SetInstanceTransform(i, xform);
		}
	}

	private void OnAutoSpawnCheck()
	{
		var sim = GetNode<GlobalSimulation>("/root/GlobalSimulation");
		// Spawn houses if there's demand (low unemployment) OR if we're just starting (pop < 25)
		// We want to keep a small pool of unemployed people for new jobs
		if (_activeZones.Count > 0 && (sim.UnemployedPopulation < 5 || sim.Population < 25))
		{
			TrySpawnHouse();
		}
	}

	private void TrySpawnHouse()
	{
		var buildingMgr = GetTree().Root.FindChild("BuildingManager", true, false) as BuildingManager;
		if (buildingMgr == null) {
			GD.Print("ZoneManager: BuildingManager not found!");
			return;
		}

		if (_activeZones.Count == 0) return;

		// Find a cluster: Pick a random spot and check if neighbors are also zoned
		int attempts = 50; // More attempts for larger maps
		int failWell = 0;
		int failOccupied = 0;

		while (attempts > 0)
		{
			attempts--;
			int idx = (int)(GD.Randi() % _activeZones.Count);
			Vector2I coords = _activeZones[idx];
			Vector3 worldPos = GridToWorld(coords);

			// Well Requirement: Must be near a Well
			if (!IsNearWell(worldPos)) 
			{
				failWell++;
				continue;
			}

			// Check if already has a building
			if (!buildingMgr.IsSpotOccupied(worldPos, 2.0f))
			{
				buildingMgr.ForcePlaceBuilding("House", worldPos, (float)GD.RandRange(0, Mathf.Pi * 2));
				GD.Print($"ZoneManager: Successfully spawned house at {worldPos}");
				return; 
			}
			else {
				failOccupied++;
			}
		}

		if (failWell > 0 && failOccupied == 0)
		{
			GD.Print($"ZoneManager: Cannot spawn house - {failWell} spots checked were too far from a Well!");
		}
	}

	private bool IsNearWell(Vector3 pos)
	{
		var wells = GetTree().GetNodesInGroup("Wells");
		foreach (Node b in wells)
		{
			if (b is Node3D b3d)
			{
				float dist = pos.DistanceTo(b3d.GlobalPosition);
				if (dist < 60.0f) // Increased radius to 60.0f
					return true;
			}
		}
		return false;
	}

	private Vector2I WorldToGrid(Vector3 pos)
	{
		return new Vector2I(Mathf.RoundToInt(pos.X / GridSize), Mathf.RoundToInt(pos.Z / GridSize));
	}

	private Vector3 GridToWorld(Vector2I coords)
	{
		return new Vector3(coords.X * GridSize, 0, coords.Y * GridSize);
	}
}
