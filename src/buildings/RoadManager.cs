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
	private Node3D _ground;
	private Camera3D _camera;
	private Node3D _previewContainer;
	
	private AStar3D _astar = new AStar3D();
	private long _pointCounter = 0;

	public override void _Ready()
	{
		_ground = GetNode<Node3D>(GroundPath);
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
			segment.GlobalPosition = pos + Vector3.Up * 0.05f;
			
			if (distance > 0.1f)
			{
				Vector3 direction = (end - start);
				direction.Y = 0;
				
				if (direction.Length() > 0.01f)
				{
					Vector3 lookTarget = segment.GlobalPosition + direction.Normalized();
					segment.LookAt(lookTarget, Vector3.Up);
					segment.RotateY(Mathf.Pi / 2);
				}
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

				// Water Check: Don't allow roads in the river!
				if (IsPositionOnWater(pos))
				{
					GD.Print("Cannot build roads on water!");
					return;
				}
				
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
		
		Vector3 from = _camera.ProjectRayOrigin(mousePos);
		Vector3 to = from + _camera.ProjectRayNormal(mousePos) * 1000.0f;
		
		var spaceState = GetWorld3D().DirectSpaceState;
		// Layer 1 is Ground, Layer 2 is Water. We want to hit the ground.
		var query = PhysicsRayQueryParameters3D.Create(from, to, 1);
		var result = spaceState.IntersectRay(query);
		
		if (result.Count > 0)
		{
			return (Vector3)result["position"];
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
			
			// Slightly above terrain to avoid Z-fighting
			segment.GlobalPosition = pos + Vector3.Up * 0.05f;
			
			if (distance > 0.1f)
			{
				Vector3 direction = (end - start);
				direction.Y = 0; // Lock to horizontal plane
				
				if (direction.Length() > 0.01f)
				{
					Vector3 lookTarget = segment.GlobalPosition + direction.Normalized();
					segment.LookAt(lookTarget, Vector3.Up);
					segment.RotateY(Mathf.Pi / 2); 
				}
			}
		}
	}

	private bool IsPositionOnWater(Vector3 position)
	{
		var spaceState = GetWorld3D().DirectSpaceState;
		Vector3 from = position + Vector3.Up * 10.0f;
		Vector3 to = position + Vector3.Down * 20.0f;
		
		var query = PhysicsRayQueryParameters3D.Create(from, to, 2); 
		var result = spaceState.IntersectRay(query);
		
		return result.Count > 0;
	}
}
