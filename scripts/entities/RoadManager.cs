using Godot;
using System;
using System.Collections.Generic;

public partial class RoadManager : Node3D
{
	[Export] public PackedScene RoadSegmentScene { get; set; }
	[Export] public NodePath GroundPath { get; set; }
	
	private bool _isBuilding = false;
	private Vector3? _lastPoint = null;
	private long _lastPointId = -1;
	private MeshInstance3D _ground;
	private Camera3D _camera;
	private Node3D _previewContainer;
	
	private AStar3D _astar = new AStar3D();
	private long _pointCounter = 0;

	public override void _Ready()
	{
		_ground = GetNode<MeshInstance3D>(GroundPath);
		_camera = GetViewport().GetCamera3D();
		
		_previewContainer = new Node3D();
		AddChild(_previewContainer);
	}

	public void ToggleBuilding(bool active)
	{
		_isBuilding = active;
		_lastPoint = null;
		_lastPointId = -1;
		ClearPreview();
	}

	public override void _Process(double delta)
	{
		if (!_isBuilding) return;

		ClearPreview();
		Vector3? mousePos = GetMouseWorldPosition();
		if (!mousePos.HasValue) return;

		Vector3 currentPos = mousePos.Value;
		currentPos.Y = 0.05f;

		if (_lastPoint.HasValue)
		{
			// Preview a line of segments from last point to mouse
			ShowRoadPreview(_lastPoint.Value, currentPos);
		}
		else
		{
			// Just a single cursor preview
			var segment = RoadSegmentScene.Instantiate<MeshInstance3D>();
			_previewContainer.AddChild(segment);
			segment.GlobalPosition = currentPos;
		}
	}

	private void ClearPreview()
	{
		foreach (var child in _previewContainer.GetChildren())
		{
			child.QueueFree();
		}
	}

	private void ShowRoadPreview(Vector3 start, Vector3 end)
	{
		float distance = start.DistanceTo(end);
		int segments = Mathf.CeilToInt(distance / 2.0f);
		
		for (int i = 0; i <= segments; i++)
		{
			float t = (float)i / segments;
			Vector3 pos = start.Lerp(end, t);
			
			var segment = RoadSegmentScene.Instantiate<MeshInstance3D>();
			_previewContainer.AddChild(segment);
			segment.GlobalPosition = new Vector3(pos.X, 0.05f, pos.Z);
			
			if (distance > 0.1f)
			{
				// Look ahead in the direction of the road, not just at the end point
				// This prevents the last segment from looking at its own position
				segment.LookAt(segment.GlobalPosition + (end - start), Vector3.Up);
				segment.RotateY(Mathf.Pi / 2);
			}
		}
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (!_isBuilding) return;

		if (@event is InputEventMouseButton mouseButton && mouseButton.Pressed && mouseButton.ButtonIndex == MouseButton.Left)
		{
			Vector3? currentPoint = GetMouseWorldPosition();
			if (currentPoint.HasValue)
			{
				Vector3 pos = currentPoint.Value;
				pos.Y = 0; // Keep on ground
				
				long currentId = _pointCounter++;
				_astar.AddPoint(currentId, pos);

				if (_lastPoint.HasValue)
				{
					_astar.ConnectPoints(_lastPointId, currentId);
					CreateRoadBetween(_lastPoint.Value, pos);
				}
				
				_lastPoint = pos;
				_lastPointId = currentId;
			}
		}
	}

	public Vector3[] GetRoadPath(Vector3 start, Vector3 end)
	{
		if (_astar.GetPointCount() < 2) return new Vector3[] { start, end };

		long startId = _astar.GetClosestPoint(start);
		long endId = _astar.GetClosestPoint(end);

		long[] pathIds = _astar.GetIdPath(startId, endId);
		
		// Convert IDs to positions
		List<Vector3> worldPath = new List<Vector3>();
		worldPath.Add(start);
		foreach (long id in pathIds)
		{
			worldPath.Add(_astar.GetPointPosition(id));
		}
		worldPath.Add(end);
		
		return worldPath.ToArray();
	}

	private Vector3? GetMouseWorldPosition()
	{
		_camera = GetViewport().GetCamera3D();
		Vector2 mousePos = GetViewport().GetMousePosition();
		
		Vector3 rayOrigin = _camera.ProjectRayOrigin(mousePos);
		Vector3 rayDirection = _camera.ProjectRayNormal(mousePos);
		
		// Simple Plane-Ray intersection (y=0 plane)
		float t = -rayOrigin.Y / rayDirection.Y;
		if (t > 0)
		{
			return rayOrigin + rayDirection * t;
		}
		
		return null;
	}

	private void CreateRoadBetween(Vector3 start, Vector3 end)
	{
		float distance = start.DistanceTo(end);
		int segments = Mathf.CeilToInt(distance / 2.0f);
		
		for (int i = 0; i <= segments; i++)
		{
			float t = (float)i / segments;
			Vector3 pos = start.Lerp(end, t);
			
			var segment = RoadSegmentScene.Instantiate<MeshInstance3D>();
			AddChild(segment);
			segment.GlobalPosition = new Vector3(pos.X, 0.02f, pos.Z);
			
			if (distance > 0.1f)
			{
				// Look ahead in the direction of the road
				segment.LookAt(segment.GlobalPosition + (end - start), Vector3.Up);
				segment.RotateY(Mathf.Pi / 2); 
			}
		}
	}
}
