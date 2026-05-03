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
	private MeshInstance3D _previewSegment;
	
	private AStar3D _astar = new AStar3D();
	private long _pointCounter = 0;

	public override void _Ready()
	{
		_ground = GetNode<MeshInstance3D>(GroundPath);
		_camera = GetViewport().GetCamera3D();
	}

	public void ToggleBuilding(bool active)
	{
		_isBuilding = active;
		_lastPoint = null;
		_lastPointId = -1;

		if (_isBuilding)
		{
			if (_previewSegment == null)
			{
				_previewSegment = RoadSegmentScene.Instantiate<MeshInstance3D>();
				AddChild(_previewSegment);
			}
			_previewSegment.Visible = true;
		}
		else if (_previewSegment != null)
		{
			_previewSegment.Visible = false;
		}
	}

	public override void _Process(double delta)
	{
		if (_isBuilding && _previewSegment != null)
		{
			Vector3? mousePos = GetMouseWorldPosition();
			if (mousePos.HasValue)
			{
				_previewSegment.GlobalPosition = new Vector3(mousePos.Value.X, 0.05f, mousePos.Value.Z);
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
			
			// Rotate to align with road direction
			Vector3 lookAtPos = end;
			if (start != end)
			{
				segment.LookAt(lookAtPos, Vector3.Up);
				segment.RotateY(Mathf.Pi / 2); // Planes are oriented differently
			}
		}
	}
}
