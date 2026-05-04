using Godot;
using System;

public partial class BuildingManager : Node3D
{
	[Export] public PackedScene HouseScene { get; set; }
	[Export] public PackedScene TownCenterScene { get; set; }
	[Export] public PackedScene MeatShopScene { get; set; }
	[Export] public PackedScene HorseStableScene { get; set; }
	[Export] public PackedScene BuilderGuildScene { get; set; }
	[Export] public PackedScene ForagerHutScene { get; set; }
	[Export] public PackedScene FishingHutScene { get; set; }
	[Export] public PackedScene WoodcutterHutScene { get; set; }
	[Export] public NodePath GroundPath { get; set; }
	
	private bool _isBuilding = false;
	private bool _isBulldozing = false;
	private PackedScene _currentScene;
	private Camera3D _camera;
	private Node3D _ground;
	private Node3D _previewHouse;
	private Node3D _hoveredBuilding;
	private StandardMaterial3D _highlightMaterial;

	public override void _Ready()
	{
		_ground = GetNode<Node3D>(GroundPath);
		_camera = GetViewport().GetCamera3D();
		_currentScene = HouseScene;
		
		_highlightMaterial = new StandardMaterial3D();
		_highlightMaterial.AlbedoColor = new Color(1, 0, 0, 0.3f);
		_highlightMaterial.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
		_highlightMaterial.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
		_highlightMaterial.CullMode = BaseMaterial3D.CullModeEnum.Disabled;

		// Initial Census: Move all pre-placed buildings to Layer 4 (bit mask 8)
		CallDeferred(nameof(InitialCensus));
	}

	private void InitialCensus()
	{
		// Look through the scene for any nodes that might be pre-placed buildings
		foreach (Node child in GetTree().CurrentScene.GetChildren())
		{
			if (child.Name.ToString().Contains("House") || 
			    child.Name.ToString().Contains("Shop") || 
			    child.Name.ToString().Contains("Guild") ||
			    child.Name.ToString().Contains("Stable") ||
			    child.Name.ToString().Contains("TownCenter") ||
			    child.Name.ToString().Contains("Forager") ||
			    child.Name.ToString().Contains("Fishing") ||
			    child.Name.ToString().Contains("Woodcutter"))
			{
				SetCollisionLayerRecursively(child, 8);
				child.AddToGroup("Buildings");
			}
		}
	}

	public void SetBuildingType(string type)
	{
		GD.Print($"Setting building type: {type}");
		switch (type)
		{
			case "House": _currentScene = HouseScene; break;
			case "TownCenter": _currentScene = TownCenterScene; break;
			case "MeatShop": _currentScene = MeatShopScene; break;
			case "HorseStable": _currentScene = HorseStableScene; break;
			case "Guild": _currentScene = BuilderGuildScene; break;
			case "ForagerHut": _currentScene = ForagerHutScene; break;
			case "FishingHut": _currentScene = FishingHutScene; break;
			case "WoodcutterHut": _currentScene = WoodcutterHutScene; break;
		}
		
		if (_currentScene == null) 
		{
			GD.PrintErr($"CRITICAL: Scene for {type} is null!");
		}
		else
		{
			GD.Print($"BuildingManager: Set current scene to {_currentScene.ResourcePath}");
		}

		// Reset preview
		if (_previewHouse != null) { _previewHouse.QueueFree(); _previewHouse = null; }
		if (_isBuilding) ToggleBuilding(true);
		
		// Exit bulldoze mode if entering building mode
		_isBulldozing = false;
	}

	public void SetBulldozeMode(bool active)
	{
		_isBulldozing = active;
		if (_isBulldozing)
		{
			// Exit building mode if entering bulldoze mode
			ToggleBuilding(false);
		}
		else
		{
			ClearHoverHighlight();
		}
	}

	public void ToggleBuilding(bool active)
	{
		_isBuilding = active;
		
		if (_isBuilding)
		{
			if (_previewHouse == null)
			{
				GD.Print($"BuildingManager: Instantiating preview for {_currentScene.ResourcePath}");
				_previewHouse = _currentScene.Instantiate<Node3D>();
				
				// Use reflection or dynamic to set IsPreview if it exists
				var previewProp = _previewHouse.GetType().GetProperty("IsPreview");
				if (previewProp != null) previewProp.SetValue(_previewHouse, true);

				AddChild(_previewHouse);
				// CRITICAL: Disable all collisions on preview to prevent flicker!
				DisableCollisionRecursively(_previewHouse);
				GD.Print("BuildingManager: Preview instantiated and added to tree.");
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
				float groundY = GetGroundYAt(res.Position);
				_previewHouse.GlobalPosition = new Vector3(res.Position.X, groundY, res.Position.Z);
				
				_previewHouse.GlobalRotation = Vector3.Zero; // Reset to avoid tilt
				_previewHouse.Visible = res.Normal.Dot(Vector3.Up) > 0.9f;
			}
		}

		if (_isBulldozing)
		{
			UpdateBulldozeHover();
		}
	}

	private void UpdateBulldozeHover()
	{
		Node3D building = GetBuildingUnderMouse();
		if (building != _hoveredBuilding)
		{
			ClearHoverHighlight();
			_hoveredBuilding = building;
			if (_hoveredBuilding != null)
			{
				ApplyHoverHighlight(_hoveredBuilding);
			}
		}
	}

	private void ClearHoverHighlight()
	{
		if (_hoveredBuilding != null && IsInstanceValid(_hoveredBuilding))
		{
			RemoveHighlightRecursively(_hoveredBuilding);
		}
		_hoveredBuilding = null;
	}

	private void ApplyHoverHighlight(Node node)
	{
		if (node is MeshInstance3D mi)
		{
			mi.MaterialOverlay = _highlightMaterial;
		}
		foreach (Node child in node.GetChildren())
		{
			ApplyHoverHighlight(child);
		}
	}

	private void RemoveHighlightRecursively(Node node)
	{
		if (node is MeshInstance3D mi)
		{
			mi.MaterialOverlay = null;
		}
		foreach (Node child in node.GetChildren())
		{
			RemoveHighlightRecursively(child);
		}
	}

	private Node3D GetBuildingUnderMouse()
	{
		Vector2 mousePos = GetViewport().GetMousePosition();
		Vector3 rayOrigin = _camera.ProjectRayOrigin(mousePos);
		Vector3 rayDirection = _camera.ProjectRayNormal(mousePos);
		
		var spaceState = GetWorld3D().DirectSpaceState;
		var query = PhysicsRayQueryParameters3D.Create(rayOrigin, rayOrigin + rayDirection * 1000, 8);
		var result = spaceState.IntersectRay(query);
		
		if (result.Count > 0)
		{
			Node collider = (Node)result["collider"];
			return FindBuildingRoot(collider);
		}
		return null;
	}

	private Node3D FindBuildingRoot(Node node)
	{
		Node current = node;
		while (current != null && current != GetTree().Root)
		{
			if (current.IsInGroup("Buildings") || current is ResidencePlot || current is Tree)
			{
				return current as Node3D;
			}
			current = current.GetParent();
		}
		return null;
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
		// Mask 8 corresponds to Layer 4 where our buildings are
		var query = PhysicsRayQueryParameters3D.Create(rayOrigin, rayOrigin + rayDirection * 1000, 8);
		var result = spaceState.IntersectRay(query);
		
		if (result.Count > 0)
		{
			Node collider = (Node)result["collider"];
			Node parent = collider.GetParent();
			
			// We check both the parent and the parent's parent just in case
			Node3D building = FindBuildingRoot(collider);

			if (building != null)
			{
				if (_isBulldozing)
				{
					DemolishBuilding(building);
				}
				else
				{
					ShowBuildingInfo(building);
				}
			}
		}
	}

	private void DemolishBuilding(Node3D building)
	{
		if (building is Tree) return; // Trees should be chopped, not bulldozed
		
		GD.Print($"Demolishing building: {building.Name}");
		
		if (building is ResidencePlot house)
		{
			var sim = GetNode<GlobalSimulation>("/root/GlobalSimulation");
			if (house.IsConstructed)
			{
				sim.RemovePopulation(house.ResidentCount);
				GD.Print($"Removed {house.ResidentCount} residents.");
			}
		}
		
		if (building == _hoveredBuilding) _hoveredBuilding = null;
		building.QueueFree();
	}

	private void ShowBuildingInfo(Node3D building)
	{
		if (building is ResidencePlot actualHouse)
		{
			var hud = GetTree().Root.FindChild("HUD", true, false) as HUD;
			string info = "";
			if (actualHouse.IsConstructed)
			{
				info = $"[ POPULATION ]\nResidents: {actualHouse.ResidentCount} / 5\n\n[ STATUS ]\nCozy and Warm\nContributing to Village";
			}
			else
			{
				info = $"[ CONSTRUCTION ]\nProgress: {actualHouse.ConstructionProgress:F1}%\n\n[ STATUS ]\nIn Progress\nWaiting for Builders";
			}
			if (hud != null) hud.ShowBuildingInfo(actualHouse, "Peasant House", info);
		}
		else if (building is BuilderGuild guild)
		{
			var hud = GetTree().Root.FindChild("HUD", true, false) as HUD;
			if (hud != null) hud.ShowBuildingInfo(guild, "Builder's Guild", "Active Builders: 2\nStatus: Employed");
		}
		else if (building is ForagerHut hut)
		{
			var hud = GetTree().Root.FindChild("HUD", true, false) as HUD;
			if (hud != null) hud.ShowBuildingInfo(hut, "Forager Hut", "Gathering Berries\nWorkers: 2\nStatus: Active");
		}
		else if (building is FishingHut fHut)
		{
			var hud = GetTree().Root.FindChild("HUD", true, false) as HUD;
			if (hud != null) hud.ShowBuildingInfo(fHut, "Fishing Hut", "Gathering Fish\nWorkers: 2\nStatus: Active");
		}
		else if (building is WoodcutterHut wHut)
		{
			var hud = GetTree().Root.FindChild("HUD", true, false) as HUD;
			if (hud != null) hud.ShowBuildingInfo(wHut, "Woodcutter Hut", "Harvesting Trees\nWorkers: 2\nStatus: Active");
		}
		else
		{
			// Generic building info
			string bName = building.Name.ToString();
			var hud = GetTree().Root.FindChild("HUD", true, false) as HUD;
			if (hud != null) hud.ShowBuildingInfo(building, bName, "A fine addition to the village.\nStatus: Standing");
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

		// Surface Check
		if (normal.Dot(Vector3.Up) < 0.9f) return;

		// Map Border Check
		if (Mathf.Abs(position.X) > 248 || Mathf.Abs(position.Z) > 248) return;

		// Deep Water Check
		if (IsPositionOnWater(position)) return;

		// Fishing Hut Requirement: Must be near water
		if (_currentScene == FishingHutScene)
		{
			if (!IsNearWater(position))
			{
				GD.Print("Fishing Hut must be placed on the riverbank!");
				return;
			}
		}

		var building = _currentScene.Instantiate<Node3D>();
		GetTree().CurrentScene.AddChild(building);
		
		// Move to Layer 4 (bit mask 8)
		SetCollisionLayerRecursively(building, 8);
		building.AddToGroup("Buildings");
		
		// Gravity Snap
		float groundY = GetGroundYAt(position);
		building.GlobalPosition = new Vector3(position.X, groundY - 0.01f, position.Z);
		
		// Force upright rotation and FREEZE physics
		building.GlobalRotation = new Vector3(0, (float)GD.RandRange(0, Mathf.Pi * 2), 0);
		FreezePhysicsRecursively(building);
	}

	private void FreezePhysicsRecursively(Node node)
	{
		if (node is RigidBody3D rb)
		{
			rb.Freeze = true;
			rb.LinearVelocity = Vector3.Zero;
			rb.AngularVelocity = Vector3.Zero;
		}
		foreach (Node child in node.GetChildren())
		{
			FreezePhysicsRecursively(child);
		}
	}

	private float GetGroundYAt(Vector3 pos)
	{
		var spaceState = GetWorld3D().DirectSpaceState;
		Vector3 from = pos + Vector3.Up * 10.0f;
		Vector3 to = pos + Vector3.Down * 10.0f;
		
		var query = PhysicsRayQueryParameters3D.Create(from, to, 1); // Hit Ground/Hills
		var result = spaceState.IntersectRay(query);
		
		if (result.Count > 0)
		{
			return ((Vector3)result["position"]).Y;
		}
		return pos.Y;
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

	private bool IsNearWater(Vector3 position)
	{
		var spaceState = GetWorld3D().DirectSpaceState;
		// Check several points in a tight circle (max 4 units)
		for (float dist = 1.0f; dist <= 4.0f; dist += 1.0f)
		{
			for (int i = 0; i < 8; i++) // Check more directions
			{
				float angle = i * (Mathf.Pi / 4);
				Vector3 offset = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * dist;
				Vector3 testPos = position + offset;
				
				Vector3 from = testPos + Vector3.Up * 10.0f;
				Vector3 to = testPos + Vector3.Down * 20.0f;
				var query = PhysicsRayQueryParameters3D.Create(from, to, 2); 
				var result = spaceState.IntersectRay(query);
				if (result.Count > 0) return true;
			}
		}
		return false;
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
			case "ForagerHut": scene = ForagerHutScene; break;
			case "FishingHut": scene = FishingHutScene; break;
			case "WoodcutterHut": scene = WoodcutterHutScene; break;
		}

		if (scene == null) return;

		var building = scene.Instantiate<Node3D>();
		GetTree().CurrentScene.AddChild(building);
		building.GlobalPosition = position;
		building.GlobalRotation = new Vector3(0, rotation, 0);
	}
}
