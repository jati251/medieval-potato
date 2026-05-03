using Godot;
using System;

public partial class BuildingManager : Node3D
{
	[Export] public PackedScene HouseScene { get; set; }
	[Export] public NodePath GroundPath { get; set; }
	
	private bool _isBuilding = false;
	private Camera3D _camera;
	private MeshInstance3D _ground;
	private Node3D _previewHouse;

	public override void _Ready()
	{
		_ground = GetNode<MeshInstance3D>(GroundPath);
		_camera = GetViewport().GetCamera3D();
	}

	public void ToggleBuilding(bool active)
	{
		_isBuilding = active;
		
		if (_isBuilding)
		{
			if (_previewHouse == null)
			{
				_previewHouse = HouseScene.Instantiate<Node3D>();
				AddChild(_previewHouse);
				// Make it look like a ghost (semi-transparent)
				// Note: Real transparency requires material override, but for now we'll just show it
			}
			_previewHouse.Visible = true;
		}
		else if (_previewHouse != null)
		{
			_previewHouse.Visible = false;
		}
	}

	public override void _Process(double delta)
	{
		if (_isBuilding && _previewHouse != null)
		{
			Vector3? mousePos = GetMouseWorldPosition();
			if (mousePos.HasValue)
			{
				_previewHouse.GlobalPosition = mousePos.Value;
			}
		}
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (!_isBuilding) return;

		if (@event is InputEventMouseButton mouseButton && mouseButton.Pressed && mouseButton.ButtonIndex == MouseButton.Left)
		{
			Vector3? spawnPos = GetMouseWorldPosition();
			if (spawnPos.HasValue)
			{
				PlaceHouse(spawnPos.Value);
			}
		}
	}

	private Vector3? GetMouseWorldPosition()
	{
		_camera = GetViewport().GetCamera3D();
		Vector2 mousePos = GetViewport().GetMousePosition();
		
		Vector3 rayOrigin = _camera.ProjectRayOrigin(mousePos);
		Vector3 rayDirection = _camera.ProjectRayNormal(mousePos);
		
		float t = -rayOrigin.Y / rayDirection.Y;
		if (t > 0)
		{
			return rayOrigin + rayDirection * t;
		}
		
		return null;
	}

	private void PlaceHouse(Vector3 position)
	{
		if (HouseScene == null) return;

		var house = HouseScene.Instantiate<Node3D>();
		GetTree().Root.GetNode("root").AddChild(house);
		house.GlobalPosition = position;
		
		// Optional: Random rotation for organic feel
		house.RotateY((float)GD.RandRange(0, Mathf.Pi * 2));
		
		GD.Print("House placed at: " + position);
	}
}
