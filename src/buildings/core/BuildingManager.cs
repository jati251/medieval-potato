using Godot;
using System;
using System.Collections.Generic;

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
	[Export] public PackedScene StoneMineScene { get; set; }
	[Export] public PackedScene HunterHutScene { get; set; }
	[Export] public PackedScene WellScene { get; set; }
	[Export] public PackedScene FireScene { get; set; }
	[Export] public NodePath GroundPath { get; set; }
	
	private bool _isBuilding = false;
	private bool _isBulldozing = false;
	private bool _isZoning = false;
	private bool _isZoningErase = false;
	private string _currentBuildingType = "";
	private PackedScene _currentScene;
	private Camera3D _camera;
	private Node3D _ground;
	private Node3D _previewHouse;
	private Node3D _hoveredBuilding;
	private StandardMaterial3D _highlightMaterial;
	private ZoneManager _zoneManager;
	private MeshInstance3D _zoneBrushPreview;
	private List<MeshInstance3D> _previewMeshes = new List<MeshInstance3D>();
	private bool _lastCanPlace = true;
	private Vector2 _lastMousePos = Vector2.Zero;

	public override void _Ready()
	{
		_ground = GetNode<Node3D>(GroundPath);
		_camera = GetViewport().GetCamera3D();
		_currentScene = HouseScene;
		
		// Load Well scene if not assigned in Inspector
		if (WellScene == null)
		{
			WellScene = GD.Load<PackedScene>("res://src/buildings/production/Well.tscn");
		}

		_highlightMaterial = new StandardMaterial3D();
		_highlightMaterial.AlbedoColor = new Color(1, 0, 0, 0.3f);
		_highlightMaterial.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
		_highlightMaterial.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
		_highlightMaterial.CullMode = BaseMaterial3D.CullModeEnum.Disabled;

		// Ensure ZoneManager exists
		var zoneMgr = GetTree().Root.FindChild("ZoneManager", true, false);
		if (zoneMgr == null)
		{
			GD.Print("BuildingManager: Creating ZoneManager...");
			var newZoneMgr = new ZoneManager();
			newZoneMgr.Name = "ZoneManager";
			var root = GetTree().CurrentScene;
			if (root != null) root.CallDeferred(MethodName.AddChild, newZoneMgr);
			else GetTree().Root.CallDeferred(MethodName.AddChild, newZoneMgr);
		}

		// Initial Census: Move all pre-placed buildings to Layer 4 (bit mask 8)
		CallDeferred(nameof(InitialCensus));
		SetupZoneBrushPreview();
	}

	private void SetupZoneBrushPreview()
	{
		_zoneBrushPreview = new MeshInstance3D();
		var mesh = new CylinderMesh();
		mesh.TopRadius = 2.5f; // Match BrushRadius in ZoneManager
		mesh.BottomRadius = 2.5f;
		mesh.Height = 0.2f;
		_zoneBrushPreview.Mesh = mesh;
		
		var mat = new StandardMaterial3D();
		mat.AlbedoColor = new Color(0.2f, 1.0f, 0.2f, 0.2f);
		mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
		mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
		mat.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
		_zoneBrushPreview.MaterialOverride = mat;
		
		_zoneBrushPreview.Visible = false;
		AddChild(_zoneBrushPreview);
	}

	private void InitialCensus()
	{
		// Recursively look through the scene for any nodes that might be pre-placed buildings
		ProcessInitialCensusRecursively(GetTree().CurrentScene);
	}

	private void ProcessInitialCensusRecursively(Node node)
	{
		if (node == null) return;

		string name = node.Name.ToString();
		if (name.Contains("House") || 
		    name.Contains("Shop") || 
		    name.Contains("Guild") ||
		    name.Contains("Stable") ||
		    name.Contains("TownCenter") ||
		    name.Contains("Forager") ||
		    name.Contains("Fishing") ||
		    name.Contains("Woodcutter") ||
		    name.Contains("StoneMine") ||
		    name.Contains("Hunter") ||
		    name.Contains("Well"))
		{
			SetCollisionLayerRecursively(node, 8);
			node.AddToGroup("Buildings");
			if (name.Contains("Well")) node.AddToGroup("Wells");
		}

		foreach (Node child in node.GetChildren())
		{
			ProcessInitialCensusRecursively(child);
		}
	}

	public void SetBuildingType(string type)
	{
		_currentBuildingType = type;
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
			case "StoneMine": _currentScene = StoneMineScene; break;
			case "HunterHut": _currentScene = HunterHutScene; break;
			case "Well": _currentScene = WellScene; break;
			case "Fire": _currentScene = FireScene; break;
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

	public void SetZoneMode(bool active)
	{
		_isZoning = active;
		if (_isZoning)
		{
			ToggleBuilding(false);
			_isBulldozing = false;
		}
		var zoneMgr = GetTree().Root.FindChild("ZoneManager", true, false) as ZoneManager;
		if (zoneMgr != null) zoneMgr.IsPainting = active;
		_isZoningErase = false; 
	}

	public void SetZoneEraseMode(bool active)
	{
		_isZoning = active;
		if (_isZoning)
		{
			ToggleBuilding(false);
			_isBulldozing = false;
			_isZoningErase = true;
		}
		var zoneMgr = GetTree().Root.FindChild("ZoneManager", true, false) as ZoneManager;
		if (zoneMgr != null) zoneMgr.IsPainting = active;
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
				
				// Cache meshes for fast highlighting
				_previewMeshes.Clear();
				CacheMeshesRecursively(_previewHouse, _previewMeshes);

				// CRITICAL: Disable all collisions on preview to prevent flicker!
				DisableCollisionRecursively(_previewHouse);
				GD.Print("BuildingManager: Preview instantiated and added to tree.");
			}
			_previewHouse.Visible = true;
			_lastCanPlace = true; // Reset
		}
		else if (_previewHouse != null)
		{
			_previewHouse.Visible = false;
			_previewMeshes.Clear();
		}
	}

	private void CacheMeshesRecursively(Node node, List<MeshInstance3D> list)
	{
		if (node is MeshInstance3D mi) list.Add(mi);
		foreach (Node child in node.GetChildren()) CacheMeshesRecursively(child, list);
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
				
				bool isSurfaceValid = res.Normal.Dot(Vector3.Up) > 0.9f;
				bool isMapBorderValid = Mathf.Abs(res.Position.X) <= 248 && Mathf.Abs(res.Position.Z) <= 248;
				bool isWaterValid = !IsPositionOnWater(res.Position);
				
				// Special check for Fishing Hut (must be near water)
				bool isRequirementMet = true;
				if (_currentScene == FishingHutScene)
				{
					isRequirementMet = IsNearWater(res.Position);
				}

				float radius = GetBuildingRadius(_currentScene);
				bool isSpotFree = !IsSpotOccupied(res.Position, radius);
				
				bool canPlace = isSurfaceValid && isMapBorderValid && isWaterValid && isRequirementMet && isSpotFree;
				
				_previewHouse.Visible = true;
				if (canPlace != _lastCanPlace)
				{
					_lastCanPlace = canPlace;
					foreach (var mi in _previewMeshes)
					{
						mi.MaterialOverlay = canPlace ? null : _highlightMaterial;
					}
				}
			}
		}

		if (_isBulldozing)
		{
			UpdateBulldozeHover();
		}

		if (_isZoning)
		{
			UpdateZoning();
			var res = GetMouseWorldPosition();
			if (res.Hit && _zoneBrushPreview != null)
			{
				_zoneBrushPreview.GlobalPosition = res.Position + Vector3.Up * 0.2f;
				_zoneBrushPreview.Visible = true;
			}
			else if (_zoneBrushPreview != null)
			{
				_zoneBrushPreview.Visible = false;
			}
		}
		else if (_zoneBrushPreview != null)
		{
			_zoneBrushPreview.Visible = false;
		}
	}

	private void UpdateZoning()
	{
		if (_zoneManager == null)
		{
			_zoneManager = GetTree().Root.FindChild("ZoneManager", true, false) as ZoneManager;
		}

		if (Input.IsMouseButtonPressed(MouseButton.Left))
		{
			var res = GetMouseWorldPosition();
			if (res.Hit && _zoneManager != null)
			{
				if (_isZoningErase) _zoneManager.EraseZone(res.Position);
				else _zoneManager.PaintZone(res.Position);
			}
		}
		else if (Input.IsMouseButtonPressed(MouseButton.Right))
		{
			var res = GetMouseWorldPosition();
			if (res.Hit && _zoneManager != null)
			{
				_zoneManager.EraseZone(res.Position);
			}
		}
	}

	private void UpdateBulldozeHover()
	{
		Vector2 mousePos = GetViewport().GetMousePosition();
		if (mousePos.DistanceTo(_lastMousePos) < 1.0f) return;
		_lastMousePos = mousePos;

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
		if (@event is InputEventMouseButton mouseButton && mouseButton.Pressed)
		{
			if (mouseButton.ButtonIndex == MouseButton.Left)
			{
				if (_isBuilding)
				{
					var res = GetMouseWorldPosition();
					if (res.Hit)
					{
						PlaceBuilding(res.Position, res.Normal);
					}
				}
				else if (!_isZoning)
				{
					CheckForBuildingClick();
				}
			}
			else if (mouseButton.ButtonIndex == MouseButton.Right)
			{
				// Cancel building or zoning mode on right click
				if (_isBuilding || _isBulldozing || _isZoning)
				{
					var hud = GetTree().Root.FindChild("HUD", true, false) as HUD;
					if (hud != null) hud.CancelPlacement();
				}
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
		var hud = GetTree().Root.FindChild("HUD", true, false) as HUD;
		if (hud == null) return;

		string title = "Building";
		if (building is ResidencePlot) title = "Peasant House";
		else if (building is TownCenter) title = "Town Center";
		else if (building is BuilderGuild) title = "Builder's Guild";
		else if (building is ForagerHut) title = "Forager Hut";
		else if (building is FishingHut) title = "Fishing Hut";
		else if (building is WoodcutterHut) title = "Woodcutter Hut";
		else if (building is StoneMine) title = "Stone Mine";
		else if (building is HunterHut) title = "Hunter Hut";
		else if (building is Well) title = "Water Well";
		else title = building.Name.ToString();

		hud.ShowBuildingInfo(building, title);
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

		GD.Print($"BuildingManager: Placing - {_currentBuildingType} (Scene: {(_currentScene != null ? _currentScene.ResourcePath : "NULL")})");
		
		// Deep Water Check
		if (IsPositionOnWater(position)) return;

		// Occupied Check (Skip for disasters)
		float radius = GetBuildingRadius(_currentScene);
		if (_currentScene != FireScene && IsSpotOccupied(position, radius))
		{
			GD.Print("Cannot place building here - spot is occupied!");
			return;
		}

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
		GD.Print($"BuildingManager: Successfully instantiated {building.Name} at {position}");
		GetTree().CurrentScene.AddChild(building);
		
		// Move to Layer 4 (bit mask 8)
		SetCollisionLayerRecursively(building, 8);
		building.AddToGroup("Buildings");
		if (_currentScene == WellScene) building.AddToGroup("Wells");
		
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
			case "StoneMine": scene = StoneMineScene; break;
			case "HunterHut": scene = HunterHutScene; break;
			case "Well": scene = WellScene; break;
		}

		if (scene == null) return;

		var building = scene.Instantiate<Node3D>();
		GetTree().CurrentScene.AddChild(building);
		building.GlobalPosition = position;
		building.GlobalRotation = new Vector3(0, rotation, 0);
		SetCollisionLayerRecursively(building, 8);
		building.AddToGroup("Buildings");
		if (type.Contains("Well")) building.AddToGroup("Wells");
	}

	public bool IsSpotOccupied(Vector3 position, float radius)
	{
		var spaceState = GetWorld3D().DirectSpaceState;
		var query = new PhysicsShapeQueryParameters3D();
		var shape = new SphereShape3D();
		shape.Radius = radius;
		query.Shape = shape;
		query.Transform = new Transform3D(Basis.Identity, position + Vector3.Up * 1.0f);
		
		// Mask 8: Buildings
		query.CollisionMask = 8; 
		
		var results = spaceState.IntersectShape(query);
		return results.Count > 0;
	}

	private float GetBuildingRadius(PackedScene scene)
	{
		if (scene == TownCenterScene) return 3.0f;
		if (scene == HorseStableScene) return 2.5f;
		if (scene == MeatShopScene) return 2.0f;
		if (scene == BuilderGuildScene) return 2.5f;
		if (scene == WoodcutterHutScene) return 2.0f;
		if (scene == ForagerHutScene) return 2.0f;
		if (scene == FishingHutScene) return 2.0f;
		if (scene == HunterHutScene) return 2.0f;
		if (scene == WellScene) return 1.0f;
		
		return 1.5f; // Default for House and others
	}
}
