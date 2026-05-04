using Godot;
using System;
using System.Collections.Generic;

public partial class RoadManager : Node3D
{
	[Export] public float GridSize { get; set; } = 0.8f; 
	[Export] public float UsageThreshold { get; set; } = 2.0f;
	[Export] public float DecayRate { get; set; } = 0.05f;
	[Export] public int MaxRoadTiles { get; set; } = 8000; 

	private Dictionary<Vector2I, float> _heatmap = new Dictionary<Vector2I, float>();
	private Dictionary<Vector2I, int> _tileToIndex = new Dictionary<Vector2I, int>();
	private List<Vector2I> _activeTiles = new List<Vector2I>();
	
	private AStarGrid2D _astarGrid = new AStarGrid2D();
	private Rect2I _gridRegion = new Rect2I(-250, -250, 500, 500); 

	private MultiMeshInstance3D _multiMesh;
	private GlobalSimulation _sim;

	public override void _Ready()
	{
		_sim = GetNodeOrNull<GlobalSimulation>("/root/GlobalSimulation");
		if (_sim != null)
		{
			_sim.SimulationTicked += OnSimulationTick;
		}

		SetupMultiMesh();
		SetupAStar();
	}

	private void SetupMultiMesh()
	{
		_multiMesh = new MultiMeshInstance3D();
		_multiMesh.Multimesh = new MultiMesh();
		_multiMesh.Multimesh.TransformFormat = MultiMesh.TransformFormatEnum.Transform3D;
		
		// We avoid ColorFormat and CustomDataFormat to maximize compatibility across Godot 4 versions.
		// We will pass the 'usage' score through the Scale of the Transform.
		
		_multiMesh.Multimesh.InstanceCount = MaxRoadTiles;
		_multiMesh.Multimesh.VisibleInstanceCount = 0;

		var mesh = new CylinderMesh();
		mesh.TopRadius = 0.6f;
		mesh.BottomRadius = 0.6f;
		mesh.Height = 0.01f;
		mesh.RadialSegments = 6;
		
		var shaderMaterial = new ShaderMaterial();
		shaderMaterial.Shader = new Shader();
		shaderMaterial.Shader.Code = @"
			shader_type spatial;
			render_mode blend_mix, depth_draw_opaque, cull_back, diffuse_lambert, specular_schlick_ggx;

			void vertex() {
				// We passed the usage score into the X-scale of the instance transform
				float usage = length(MODEL_MATRIX[0].xyz);
				
				float visual_scale = clamp(usage / 10.0, 0.3, 1.2);
				float alpha_val = clamp(usage / 3.0, 0.0, 0.85);
				
				mat4 base_wm = MODELVIEW_MATRIX;
				base_wm[0] = normalize(base_wm[0]) * visual_scale;
				base_wm[1] = normalize(base_wm[1]);
				base_wm[2] = normalize(base_wm[2]) * visual_scale;
				MODELVIEW_MATRIX = base_wm;
				
				COLOR.a = alpha_val;
			}

			void fragment() {
				ALBEDO = vec3(0.25, 0.18, 0.12); // Dirt color
				ALPHA = COLOR.a;
				ROUGHNESS = 0.9; // Rough dirt surface
				SPECULAR = 0.1;
			}
		";
		
		mesh.Material = shaderMaterial;
		_multiMesh.Multimesh.Mesh = mesh;
		
		AddChild(_multiMesh);
		_multiMesh.GlobalPosition = Vector3.Zero;
	}

	private void SetupAStar()
	{
		_astarGrid.Region = _gridRegion;
		_astarGrid.CellSize = new Vector2(GridSize, GridSize);
		_astarGrid.DefaultComputeHeuristic = AStarGrid2D.Heuristic.Euclidean;
		_astarGrid.DefaultEstimateHeuristic = AStarGrid2D.Heuristic.Euclidean;
		_astarGrid.DiagonalMode = AStarGrid2D.DiagonalModeEnum.AtLeastOneWalkable;
		_astarGrid.Update();
	}

	public void ToggleBuilding(bool active) { }

	public void RegisterFootprint(Vector3 position)
	{
		Vector2I coords = WorldToGrid(position);
		if (!_gridRegion.HasPoint(coords)) return;

		if (!_heatmap.ContainsKey(coords))
			_heatmap[coords] = 0;

		_heatmap[coords] += 1.0f;

		float usage = _heatmap[coords];
		
		if (Mathf.RoundToInt(usage) % 5 == 0)
		{
			float weight = Mathf.Max(0.3f, 1.0f - (usage / 15.0f));
			_astarGrid.SetPointWeightScale(coords, weight);
		}

		if (usage >= UsageThreshold)
		{
			if (!_tileToIndex.ContainsKey(coords))
			{
				SpawnRoadTile(coords);
			}
			else
			{
				int index = _tileToIndex[coords];
				UpdateInstanceTransform(index, coords, usage);
			}
		}
	}

	private void OnSimulationTick()
	{
		List<Vector2I> toRemove = new List<Vector2I>();
		
		for (int i = 0; i < _activeTiles.Count; i++)
		{
			Vector2I key = _activeTiles[i];
			_heatmap[key] -= DecayRate;
			float usage = _heatmap[key];
			
			if (usage <= 0)
			{
				toRemove.Add(key);
			}
			else
			{
				UpdateInstanceTransform(i, key, usage);
			}
		}

		foreach (var key in toRemove)
		{
			_heatmap.Remove(key);
			_astarGrid.SetPointWeightScale(key, 1.0f);
			RemoveTileVisual(key);
		}
	}

	private void SpawnRoadTile(Vector2I coords)
	{
		if (_activeTiles.Count >= MaxRoadTiles) return;

		int index = _activeTiles.Count;
		_tileToIndex[coords] = index;
		_activeTiles.Add(coords);
		
		UpdateInstanceTransform(index, coords, _heatmap[coords]);
		_multiMesh.Multimesh.VisibleInstanceCount = _activeTiles.Count;
	}

	private void UpdateInstanceTransform(int index, Vector2I coords, float usage)
	{
		Vector3 worldPos = GridToWorld(coords);
		// Note: We use a fixed seed based on coords so jitter is consistent
		GD.Seed((ulong)(coords.X * 1000 + coords.Y));
		float jitter = GridSize * 0.4f;
		worldPos.X += (float)GD.RandRange(-jitter, jitter);
		worldPos.Z += (float)GD.RandRange(-jitter, jitter);
		float rot = (float)GD.RandRange(0, Mathf.Pi * 2);
		
		Transform3D t = new Transform3D(Basis.Identity, worldPos + Vector3.Up * 0.015f);
		// Important: Pass 'usage' score into the scale
		t.Basis = t.Basis.Rotated(Vector3.Up, rot).Scaled(new Vector3(usage, 1, usage));
		
		_multiMesh.Multimesh.SetInstanceTransform(index, t);
	}

	private void RemoveTileVisual(Vector2I coords)
	{
		int indexToRemove = _tileToIndex[coords];
		int lastIndex = _activeTiles.Count - 1;
		
		if (indexToRemove != lastIndex)
		{
			Vector2I lastCoords = _activeTiles[lastIndex];
			Transform3D lastTransform = _multiMesh.Multimesh.GetInstanceTransform(lastIndex);
			
			_multiMesh.Multimesh.SetInstanceTransform(indexToRemove, lastTransform);
			
			_tileToIndex[lastCoords] = indexToRemove;
			_activeTiles[indexToRemove] = lastCoords;
		}
		
		_activeTiles.RemoveAt(lastIndex);
		_tileToIndex.Remove(coords);
		_multiMesh.Multimesh.VisibleInstanceCount = _activeTiles.Count;
	}

	public Vector3[] GetRoadPath(Vector3 start, Vector3 end)
	{
		Vector2I startGrid = WorldToGrid(start);
		Vector2I endGrid = WorldToGrid(end);

		startGrid.X = Mathf.Clamp(startGrid.X, _gridRegion.Position.X, _gridRegion.End.X - 1);
		startGrid.Y = Mathf.Clamp(startGrid.Y, _gridRegion.Position.Y, _gridRegion.End.Y - 1);
		endGrid.X = Mathf.Clamp(endGrid.X, _gridRegion.Position.X, _gridRegion.End.X - 1);
		endGrid.Y = Mathf.Clamp(endGrid.Y, _gridRegion.Position.Y, _gridRegion.End.Y - 1);

		var path = _astarGrid.GetIdPath(startGrid, endGrid);
		
		List<Vector3> worldPath = new List<Vector3>();
		worldPath.Add(start);
		foreach (var p in path)
		{
			worldPath.Add(GridToWorld(p));
		}
		worldPath.Add(end);

		return worldPath.ToArray();
	}

	private Vector2I WorldToGrid(Vector3 pos)
	{
		return new Vector2I(
			Mathf.RoundToInt(pos.X / GridSize),
			Mathf.RoundToInt(pos.Z / GridSize)
		);
	}

	private Vector3 GridToWorld(Vector2I coords)
	{
		return new Vector3(coords.X * GridSize, 0, coords.Y * GridSize);
	}
}
