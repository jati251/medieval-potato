using Godot;
using System;
using System.Collections.Generic;

public partial class RoadManager : Node3D
{
	[Export] public float GridSize { get; set; } = 0.6f; // Smaller grid for finer, more detailed paths
	[Export] public float UsageThreshold { get; set; } = 2.0f;
	[Export] public float DecayRate { get; set; } = 0.03f; // Slower decay
	[Export] public int MaxRoadTiles { get; set; } = 10000; // Increased to allow for smaller tiles

	private Dictionary<Vector2I, float> _heatmap = new Dictionary<Vector2I, float>();
	private Dictionary<Vector2I, int> _tileToIndex = new Dictionary<Vector2I, int>();
	private List<Vector2I> _activeTiles = new List<Vector2I>();
	
	private AStarGrid2D _astarGrid = new AStarGrid2D();
	private Rect2I _gridRegion = new Rect2I(-400, -400, 800, 800); // 480x480m area at 0.6m grid

	private MultiMeshInstance3D _multiMesh;
	private GlobalSimulation _sim;
	private bool _needsVisualUpdate = false;

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
		_multiMesh.Multimesh.UseColors = true;
		_multiMesh.Multimesh.InstanceCount = MaxRoadTiles;
		_multiMesh.Multimesh.VisibleInstanceCount = 0;

		// Use a very low-profile cylinder (basically a disc)
		var mesh = new CylinderMesh();
		mesh.TopRadius = 0.5f; // Base radius
		mesh.BottomRadius = 0.5f;
		mesh.Height = 0.005f; // Very thin
		mesh.RadialSegments = 6;
		
		var mat = new StandardMaterial3D();
		// Darker, desaturated brown for a more realistic dirt look
		mat.AlbedoColor = new Color(0.25f, 0.18f, 0.12f); 
		mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
		mat.VertexColorUseAsAlbedo = true;
		mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
		
		mesh.Material = mat;
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

		// Pathfinding cost reduction
		float usage = _heatmap[coords];
		float weight = Mathf.Max(0.3f, 1.0f - (usage / 10.0f));
		_astarGrid.SetPointWeightScale(coords, weight);

		if (usage >= UsageThreshold && !_tileToIndex.ContainsKey(coords))
		{
			if (_activeTiles.Count < MaxRoadTiles)
			{
				int index = _activeTiles.Count;
				_tileToIndex[coords] = index;
				_activeTiles.Add(coords);
				
				Vector3 worldPos = GridToWorld(coords);
				// Random jitter to make it look scattered
				float jitter = GridSize * 0.4f;
				worldPos.X += (float)GD.RandRange(-jitter, jitter);
				worldPos.Z += (float)GD.RandRange(-jitter, jitter);
				
				Transform3D t = new Transform3D(Basis.Identity, worldPos + Vector3.Up * 0.015f);
				// Add random tilt and rotation for variation
				t.Basis = t.Basis.Rotated(Vector3.Up, (float)GD.RandRange(0, Mathf.Pi * 2));
				
				_multiMesh.Multimesh.SetInstanceTransform(index, t);
				_multiMesh.Multimesh.VisibleInstanceCount = _activeTiles.Count;
			}
		}
		
		_needsVisualUpdate = true;
	}

	public override void _Process(double delta)
	{
		if (_needsVisualUpdate)
		{
			UpdateAllVisuals();
			_needsVisualUpdate = false;
		}
	}

	private void OnSimulationTick()
	{
		List<Vector2I> toRemove = new List<Vector2I>();
		foreach (var key in _heatmap.Keys)
		{
			_heatmap[key] -= DecayRate;
			if (_heatmap[key] <= 0) toRemove.Add(key);
		}

		foreach (var key in toRemove)
		{
			_heatmap.Remove(key);
			_astarGrid.SetPointWeightScale(key, 1.0f);
			if (_tileToIndex.ContainsKey(key))
			{
				RemoveTileVisual(key);
			}
		}
		
		_needsVisualUpdate = true;
	}

	private void RemoveTileVisual(Vector2I coords)
	{
		int indexToRemove = _tileToIndex[coords];
		int lastIndex = _activeTiles.Count - 1;
		
		if (indexToRemove != lastIndex)
		{
			Vector2I lastCoords = _activeTiles[lastIndex];
			Transform3D lastTransform = _multiMesh.Multimesh.GetInstanceTransform(lastIndex);
			Color lastColor = _multiMesh.Multimesh.GetInstanceColor(lastIndex);
			
			_multiMesh.Multimesh.SetInstanceTransform(indexToRemove, lastTransform);
			_multiMesh.Multimesh.SetInstanceColor(indexToRemove, lastColor);
			
			_tileToIndex[lastCoords] = indexToRemove;
			_activeTiles[indexToRemove] = lastCoords;
		}
		
		_activeTiles.RemoveAt(lastIndex);
		_tileToIndex.Remove(coords);
		_multiMesh.Multimesh.VisibleInstanceCount = _activeTiles.Count;
	}

	private void UpdateAllVisuals()
	{
		for (int i = 0; i < _activeTiles.Count; i++)
		{
			Vector2I coords = _activeTiles[i];
			float usage = _heatmap[coords];
			
			// Smaller, tighter scaling
			float scaleFactor = Mathf.Clamp(usage / 8.0f, 0.3f, 1.0f) * GridSize * 1.5f;
			float alpha = Mathf.Clamp(usage / UsageThreshold, 0.1f, 0.9f);
			
			Transform3D t = _multiMesh.Multimesh.GetInstanceTransform(i);
			// Keep the random rotation but update the scale
			Basis b = t.Basis.Orthonormalized();
			t.Basis = b.Scaled(new Vector3(scaleFactor, 1, scaleFactor));
			
			_multiMesh.Multimesh.SetInstanceTransform(i, t);
			_multiMesh.Multimesh.SetInstanceColor(i, new Color(1, 1, 1, alpha));
		}
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
