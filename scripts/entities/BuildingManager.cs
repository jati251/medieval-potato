using Godot;
using System;

public partial class BuildingManager : Node3D
{
	[Export] public PackedScene HouseScene { get; set; }
	[Export] public PackedScene TownCenterScene { get; set; }
	[Export] public PackedScene MeatShopScene { get; set; }
	[Export] public PackedScene HorseStableScene { get; set; }
	[Export] public PackedScene BuilderGuildScene { get; set; }
	[Export] public NodePath GroundPath { get; set; }
	
	private bool _isBuilding = false;
	private PackedScene _currentScene;
	private Camera3D _camera;
	private MeshInstance3D _ground;
	private Node3D _previewHouse;

	public override void _Ready()
	{
		_ground = GetNode<MeshInstance3D>(GroundPath);
		_camera = GetViewport().GetCamera3D();
		_currentScene = HouseScene;
	}

	public void SetBuildingType(string type)
	{
		switch (type)
		{
			case "House": _currentScene = HouseScene; break;
			case "TownCenter": _currentScene = TownCenterScene; break;
			case "MeatShop": _currentScene = MeatShopScene; break;
			case "HorseStable": _currentScene = HorseStableScene; break;
			case "Guild": _currentScene = BuilderGuildScene; break;
		}
		
		// Reset preview
		if (_previewHouse != null) { _previewHouse.QueueFree(); _previewHouse = null; }
		if (_isBuilding) ToggleBuilding(true);
	}

	public void ToggleBuilding(bool active)
	{
		_isBuilding = active;
		
		if (_isBuilding)
		{
			if (_previewHouse == null)
			{
				_previewHouse = _currentScene.Instantiate<Node3D>();
				AddChild(_previewHouse);
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
		if (@event is InputEventMouseButton mouseButton && mouseButton.Pressed && mouseButton.ButtonIndex == MouseButton.Left)
		{
			if (_isBuilding)
			{
				Vector3? spawnPos = GetMouseWorldPosition();
				if (spawnPos.HasValue)
				{
					PlaceBuilding(spawnPos.Value);
				}
			}
			else
			{
				CheckForBuildingClick();
			}
		}
	}

	private void CheckForBuildingClick()
	{
		Vector2 mousePos = GetViewport().GetMousePosition();
		Vector3 rayOrigin = _camera.ProjectRayOrigin(mousePos);
		Vector3 rayDirection = _camera.ProjectRayNormal(mousePos);
		
		var spaceState = GetWorld3D().DirectSpaceState;
		var query = PhysicsRayQueryParameters3D.Create(rayOrigin, rayOrigin + rayDirection * 1000);
		var result = spaceState.IntersectRay(query);
		
		if (result.Count > 0)
		{
			Node collider = (Node)result["collider"];
			Node parent = collider.GetParent();
			
			// We check both the parent and the parent's parent just in case
			if (parent is ResidencePlot house || parent.GetParent() is ResidencePlot house2)
			{
				var actualHouse = parent is ResidencePlot ? (ResidencePlot)parent : (ResidencePlot)parent.GetParent();
				var hud = GetTree().Root.FindChild("HUD", true, false) as HUD;
				if (hud != null) hud.ShowBuildingInfo(actualHouse, "Peasant House", $"Residents: {actualHouse.ResidentCount} / 5\nStatus: Cozy");
			}
			else if (parent is BuilderGuild guild)
			{
				var hud = GetTree().Root.FindChild("HUD", true, false) as HUD;
				if (hud != null) hud.ShowBuildingInfo(guild, "Builder's Guild", "Active Builders: 2\nStatus: Employed");
			}
			else
			{
				// Generic building info
				string bName = parent.Name.ToString();
				var hud = GetTree().Root.FindChild("HUD", true, false) as HUD;
				if (hud != null) hud.ShowBuildingInfo((Node3D)parent, bName, "A fine addition to the village.\nStatus: Standing");
			}
		}
	}

	private void PlaceBuilding(Vector3 position)
	{
		if (_currentScene == null) return;

		var building = _currentScene.Instantiate<Node3D>();
		GetTree().CurrentScene.AddChild(building);
		building.GlobalPosition = position;
		building.RotateY((float)GD.RandRange(0, Mathf.Pi * 2));
	}

	public void ForcePlaceBuilding(string type, Vector3 position, float rotation)
	{
		PackedScene scene = null;
		switch (type)
		{
			case "House": scene = HouseScene; break;
			case "TownCenter": scene = TownCenterScene; break;
			case "MeatShop": scene = MeatShopScene; break;
			case "HorseStable": scene = HorseStableScene; break;
			case "Guild": scene = BuilderGuildScene; break;
		}

		if (scene == null) return;

		var building = scene.Instantiate<Node3D>();
		GetTree().CurrentScene.AddChild(building);
		building.GlobalPosition = position;
		building.GlobalRotation = new Vector3(0, rotation, 0);
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
}
