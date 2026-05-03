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
	private Node3D _ground;
	private Node3D _previewHouse;

	public override void _Ready()
	{
		_ground = GetNode<Node3D>(GroundPath);
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
				// CRITICAL: Disable all collisions on preview to prevent flicker!
				DisableCollisionRecursively(_previewHouse);
			}
			_previewHouse.Visible = true;
		}
		else if (_previewHouse != null)
		{
			_previewHouse.Visible = false;
		}
	}

	private void DisableCollisionRecursively(Node node)
	{
		if (node is CollisionObject3D co)
		{
			co.CollisionLayer = 0;
			co.CollisionMask = 0;
		}
		foreach (Node child in node.GetChildren())
		{
			DisableCollisionRecursively(child);
		}
	}

	public override void _Process(double delta)
	{
		if (_isBuilding && _previewHouse != null)
		{
			var res = GetMouseWorldPosition();
			if (res.Hit)
			{
				_previewHouse.GlobalPosition = res.Position;
				// Visual feedback: red if steep, white/normal if flat
				_previewHouse.Visible = res.Normal.Dot(Vector3.Up) > 0.9f;
			}
		}
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event is InputEventMouseButton mouseButton && mouseButton.Pressed && mouseButton.ButtonIndex == MouseButton.Left)
		{
			if (_isBuilding)
			{
				var res = GetMouseWorldPosition();
				if (res.Hit)
				{
					PlaceBuilding(res.Position, res.Normal);
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

	private struct RayResult
	{
		public Vector3 Position;
		public Vector3 Normal;
		public bool Hit;
	}

	private RayResult GetMouseWorldPosition()
	{
		var camera = GetViewport().GetCamera3D();
		Vector2 mousePos = GetViewport().GetMousePosition();
		
		Vector3 from = camera.ProjectRayOrigin(mousePos);
		Vector3 to = from + camera.ProjectRayNormal(mousePos) * 1000.0f;
		
		var spaceState = GetWorld3D().DirectSpaceState;
		// Layer 1 is Ground/Hills
		var query = PhysicsRayQueryParameters3D.Create(from, to, 1);
		var result = spaceState.IntersectRay(query);
		
		if (result.Count > 0)
		{
			return new RayResult { 
				Position = (Vector3)result["position"], 
				Normal = (Vector3)result["normal"],
				Hit = true 
			};
		}
		
		return new RayResult { Hit = false };
	}

	private void PlaceBuilding(Vector3 position, Vector3 normal)
	{
		if (_currentScene == null) return;

		// Surface Check: Only build on FLAT surfaces (not walls!)
		if (normal.Dot(Vector3.Up) < 0.9f)
		{
			GD.Print("Surface too steep for building!");
			return;
		}

		// Map Border Check
		if (Mathf.Abs(position.X) > 248 || Mathf.Abs(position.Z) > 248) return;

		// Deep Water Check
		if (IsPositionOnWater(position))
		{
			GD.Print("Cannot build on water!");
			return;
		}

		var building = _currentScene.Instantiate<Node3D>();
		GetTree().CurrentScene.AddChild(building);
		
		// Move to Layer 4 so future placements don't hit this building
		SetCollisionLayerRecursively(building, 4);
		
		building.GlobalPosition = position;
		building.RotateY((float)GD.RandRange(0, Mathf.Pi * 2));
	}

	private void SetCollisionLayerRecursively(Node node, uint layer)
	{
		if (node is CollisionObject3D co)
		{
			co.CollisionLayer = layer;
			// Buildings should still be clickable but not block terrain raycast
		}
		foreach (Node child in node.GetChildren())
		{
			SetCollisionLayerRecursively(child, layer);
		}
	}

	private bool IsPositionOnWater(Vector3 position)
	{
		var spaceState = GetWorld3D().DirectSpaceState;
		// Raycast from high above to deep below to catch water anywhere
		Vector3 from = position + Vector3.Up * 10.0f;
		Vector3 to = position + Vector3.Down * 20.0f;
		
		// Layer 2 is our River
		var query = PhysicsRayQueryParameters3D.Create(from, to, 2); 
		var result = spaceState.IntersectRay(query);
		
		return result.Count > 0;
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
}
