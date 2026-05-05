using Godot;
using System;
using System.Collections.Generic;

public partial class HUD : CanvasLayer
{
	private Label _popLabel;
	private Label _foodLabel;
	private Label _woodLabel;
	private Label _stoneLabel;
	private GlobalSimulation _sim;
	private RoadManager _roadManager;
	private BuildingManager _buildingManager;
	private bool _isRoadMode = false;
	private bool _isHouseMode = false;
	private bool _isBulldozeMode = false;
	private bool _isZoningMode = false;
	private string _currentBuildingType = "";

	private Node3D _followTarget;
	private Control _infoPopup;
	private Control _pauseMenu;
	private Control _saveDialog;
	private Camera3D _camera;

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event is InputEventKey keyEvent && keyEvent.Pressed && keyEvent.Keycode == Key.Escape)
		{
			if (_saveDialog != null && _saveDialog.Visible) _saveDialog.Visible = false;
			else TogglePause();
		}
	}

	private void TogglePause()
	{
		if (_pauseMenu == null) return;
		_pauseMenu.Visible = !_pauseMenu.Visible;
		GetTree().Paused = _pauseMenu.Visible;
		Input.MouseMode = Input.MouseModeEnum.Visible; 
	}

	public void ShowBuildingInfo(Node3D target, string title)
	{
		if (_infoPopup == null) return;
		_followTarget = target;
		_infoPopup.GetNode<Label>("Panel/VBoxContainer/TitleLabel").Text = title;
		UpdateBuildingInfoDisplay();
		_infoPopup.Visible = true;
	}

	private void UpdateBuildingInfoDisplay()
	{
		if (_followTarget == null || _infoPopup == null) return;

		string info = "";
		string name = _followTarget.Name.ToString();

		if (_followTarget is ResidencePlot house)
		{
			string status = house.IsConstructed ? "Established" : "Under Construction";
			float hap = house.Happiness * 100f;
			info = $"[ RESIDENCE ]\nStatus: {status}\nResidents: {house.ResidentCount}\n\n[ WELLBEING ]\nHappiness: {hap:F0}%\nNeeds: {house.NeedsStatus}";
		}
		else if (_followTarget is TownCenter)
		{
			float current = _sim.Food + _sim.Wood;
			info = $"[ STORAGE ]\nFood: {_sim.Food:F1}\nWood: {_sim.Wood:F1}\nTotal: {Mathf.RoundToInt(current)} / {Mathf.RoundToInt(_sim.StorageCapacity)}\n\n[ STATUS ]\nVillage Heart";
		}
		else if (name.Contains("Woodcutter"))
		{
			info = "[ PRODUCTION ]\nHarvesting Trees\nWorkers: 2\nStatus: Active";
		}
		else if (name.Contains("StoneMine"))
		{
			info = "[ PRODUCTION ]\nMining Stone\nWorkers: 2\nStatus: Active";
		}
		else if (name.Contains("Forager"))
		{
			// Using dynamic/reflection to get the harvest total if possible, or just checking the type
			float total = 0;
			if (_followTarget is ForagerHut hut)
			{
				// We added this field to ForagerHut.cs
				var field = hut.GetType().GetField("_totalBerriesHarvested", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
				if (field != null) total = (float)field.GetValue(hut);
			}
			info = $"[ PRODUCTION ]\nGathering Berries\nWorkers: 2\n\n[ DETAILS ]\nTotal Harvested: {total:F1}\nEfficiency: 100%";
		}
		else if (name.Contains("Well"))
		{
			info = "[ PRODUCTION ]\nDrawing Fresh Water\nStatus: Active\n\n[ DETAILS ]\nVital for Survival";
		}
		else if (name.Contains("Fishing"))
		{
			info = "[ PRODUCTION ]\nGathering Fish\nWorkers: 2\nStatus: Active";
		}
		else if (name.Contains("Hunter"))
		{
			info = "[ PRODUCTION ]\nHunting Game\nWorkers: 1\nStatus: Active";
		}
		else if (name.Contains("MeatShop"))
		{
			info = "[ COMMERCE ]\nSelling Fine Meats\nStatus: Open for Business";
		}
		else if (name.Contains("Stable"))
		{
			info = "[ SERVICE ]\nHousing Noble Steeds\nStatus: Operational";
		}
		else if (name.Contains("Guild"))
		{
			info = "[ SERVICE ]\nActive Builders: 2\nStatus: Employed";
		}
		else
		{
			// Check for LocalStorage via reflection for generic production buildings
			var storageProp = _followTarget.GetType().GetProperty("LocalStorage");
			if (storageProp != null)
			{
				float amount = (float)storageProp.GetValue(_followTarget);
				string resType = "Resources";
				var typeProp = _followTarget.GetType().GetProperty("ResourceType");
				if (typeProp != null) resType = (string)typeProp.GetValue(_followTarget);
				
				info = $"[ PRODUCTION ]\nWorkers: 2\nStatus: Active\n\n[ STORAGE ]\n{resType} inside: {amount:F1}\nWaiting for Transporters";
			}
			else
			{
				info = "A fine addition to the village.\nStatus: Standing";
			}
		}

		_infoPopup.GetNode<Label>("Panel/VBoxContainer/InfoLabel").Text = info;
	}

	private void OnClosePopup()
	{
		if (_infoPopup != null) _infoPopup.Visible = false;
		_followTarget = null;
	}

	public override void _Ready()
	{
		_infoPopup = GetNodeOrNull<Control>("BuildingInfoPopup");
		_pauseMenu = GetNodeOrNull<Control>("PauseMenu");
		_saveDialog = GetNodeOrNull<Control>("SaveLoadDialog");
		_camera = GetViewport().GetCamera3D();
		
		_sim = GetNode<GlobalSimulation>("/root/GlobalSimulation");
		_sim.SimulationTicked += UpdateDisplay;

		// Setup Pause Menu
		if (_pauseMenu != null)
		{
			_pauseMenu.GetNode<Button>("VBoxContainer/Resume").Pressed += TogglePause;
			_pauseMenu.GetNode<Button>("VBoxContainer/MainMenu").Pressed += () => {
				GetTree().Paused = false;
				GetTree().ChangeSceneToFile("res://src/ui/MainMenu.tscn");
			};
			_pauseMenu.GetNode<Button>("VBoxContainer/Quit").Pressed += () => GetTree().Quit();
			_pauseMenu.GetNode<Button>("VBoxContainer/Save").Pressed += () => {
				if (_saveDialog != null) {
					UpdateSlotLabels();
					_saveDialog.Visible = true;
					_pauseMenu.Visible = false;
				}
			};
		}

		// Setup Save Dialog Slots
		if (_saveDialog != null)
		{
			for (int i = 1; i <= 5; i++)
			{
				int slot = i;
				var btn = _saveDialog.GetNode<Button>($"Panel/VBoxContainer/Slot{i}");
				btn.Pressed += () => OnSlotSelected(slot);
			}
			_saveDialog.GetNode<Button>("Panel/VBoxContainer/CancelButton").Pressed += () => _saveDialog.Visible = false;
		}

		_popLabel = GetNodeOrNull<Label>("MarginContainer/VBoxContainer/PopLabel");
		_foodLabel = GetNodeOrNull<Label>("MarginContainer/VBoxContainer/FoodLabel");
		_woodLabel = GetNodeOrNull<Label>("MarginContainer/VBoxContainer/WoodLabel");
		_stoneLabel = GetNodeOrNull<Label>("MarginContainer/VBoxContainer/StoneLabel");

		// Managers
		_roadManager = GetTree().Root.FindChild("RoadManager", true, false) as RoadManager;
		_buildingManager = GetTree().Root.FindChild("BuildingManager", true, false) as BuildingManager;

		SetupButtonHandlers();
		
		// Trigger Loading if a file is pending
		if (!string.IsNullOrEmpty(GlobalSimulation.PendingLoadFile))
		{
			string fileToLoad = GlobalSimulation.PendingLoadFile;
			GlobalSimulation.PendingLoadFile = ""; 
			CallDeferred(nameof(DeferredLoad), fileToLoad);
		}

		UpdateDisplay();
		UpdateButtons(); // Fix: Hide 'Build House' immediately
		SelectCategory("General");
	}

	private void DeferredLoad(string fileName)
	{
		_sim.LoadGame(fileName);
	}

	private void UpdateSlotLabels()
	{
		for (int i = 1; i <= 5; i++)
		{
			string path = $"user://slot{i}.json";
			var btn = _saveDialog.GetNode<Button>($"Panel/VBoxContainer/Slot{i}");
			btn.Text = FileAccess.FileExists(path) ? $"Slot {i} - Village" : $"Slot {i} - Empty";
		}
	}

	private void OnSlotSelected(int slot)
	{
		_sim.SaveGame($"slot{slot}");
		_saveDialog.Visible = false;
		TogglePause();
	}

	private void SetupButtonHandlers()
	{
		GD.Print("HUD: SetupButtonHandlers START");
		// Category Buttons
		GetNode<Button>("BottomBar/VBoxContainer/CategoryBar/CatGeneral").Pressed += () => SelectCategory("General");
		GetNode<Button>("BottomBar/VBoxContainer/CategoryBar/CatFood").Pressed += () => SelectCategory("Food");
		GetNode<Button>("BottomBar/VBoxContainer/CategoryBar/CatTech").Pressed += () => SelectCategory("Tech");
		
		var catDisasterBtn = GetNodeOrNull<Button>("BottomBar/VBoxContainer/CategoryBar/CatDisaster");
		if (catDisasterBtn != null) catDisasterBtn.Pressed += () => SelectCategory("Disaster");
		
		var catZonesBtn = GetNodeOrNull<Button>("BottomBar/VBoxContainer/CategoryBar/CatZones");
		if (catZonesBtn != null) {
			GD.Print("HUD: Found CatZones button, connecting...");
			catZonesBtn.Pressed += () => {
				GD.Print("HUD: CatZones button Pressed!");
				SelectCategory("Zones");
			};
		} else {
			GD.PrintErr("HUD: CatZones button NOT FOUND in HUD.tscn!");
		}

		var btns = new Dictionary<string, string> {
			// {"GeneralGroup/BuildRoad", null}, // Natural roads now!
			{"GeneralGroup/BuildTownCenter", "TownCenter"},
			{"FoodGroup/BuildMeatShop", "MeatShop"},
			{"TechGroup/BuildStable", "HorseStable"},
			{"TechGroup/BuildGuild", "Guild"},
			{"FoodGroup/BuildForager", "ForagerHut"},
			{"FoodGroup/BuildFishing", "FishingHut"},
			{"FoodGroup/BuildWoodcutter", "WoodcutterHut"},
			{"FoodGroup/BuildHunter", "HunterHut"},
			{"FoodGroup/BuildWell", "Well"},
			{"TechGroup/BuildStoneMine", "StoneMine"},
			{"DisasterGroup/BuildFire", "Fire"}
		};

		foreach (var pair in btns)
		{
			string nodePath = pair.Key;
			string buildingType = pair.Value;
			
			var btn = GetNodeOrNull<Button>("BottomBar/VBoxContainer/MarginContainer/" + nodePath);
			if (btn == null)
			{
				GD.PrintErr($"HUD: Could not find button at path: BottomBar/VBoxContainer/MarginContainer/{nodePath}");
				continue;
			}
			
			if (nodePath.EndsWith("BuildRoad")) 
				btn.Pressed += OnRoadButtonPressed;
			else 
				btn.Pressed += () => OnBuildingSelected(buildingType);
		}

		if (GetNodeOrNull<Button>("BottomBar/VBoxContainer/MarginContainer/GeneralGroup/Bulldoze") is Button bulldozeBtn)
			bulldozeBtn.Pressed += OnBulldozeButtonPressed;

		if (GetNodeOrNull<Button>("BottomBar/VBoxContainer/MarginContainer/ZonesGroup/ZoneRes") is Button zoneBtn) {
			GD.Print("HUD: Found ZoneRes button!");
			zoneBtn.Pressed += () => {
				GD.Print("HUD: ZoneRes button Pressed!");
				OnZoneButtonPressed(false);
			};
		} else {
			GD.PrintErr("HUD: ZoneRes button NOT FOUND!");
		}

		if (GetNodeOrNull<Button>("BottomBar/VBoxContainer/MarginContainer/ZonesGroup/EraseZone") is Button eraseZoneBtn) {
			GD.Print("HUD: Found EraseZone button!");
			eraseZoneBtn.Pressed += () => {
				GD.Print("HUD: EraseZone button Pressed!");
				OnZoneButtonPressed(true);
			};
		} else {
			GD.PrintErr("HUD: EraseZone button NOT FOUND!");
		}

		if (GetNodeOrNull<Button>("BuildingInfoPopup/Panel/VBoxContainer/CloseButton") is Button b) b.Pressed += OnClosePopup;

		foreach (int i in new int[] {0, 1, 2, 4}) {
			var speedBtn = GetNodeOrNull<Button>($"TimeContainer/HBoxContainer/Speed{i}");
			if (speedBtn != null) {
				float s = (float)i;
				speedBtn.Pressed += () => SetGameSpeed(s);
			}
		}
		GD.Print("HUD: SetupButtonHandlers END");
	}

	private void SelectCategory(string cat)
	{
		GD.Print($"HUD: Selecting Category: {cat}");
		GetNode<Control>("BottomBar/VBoxContainer/MarginContainer/GeneralGroup").Visible = (cat == "General");
		GetNode<Control>("BottomBar/VBoxContainer/MarginContainer/FoodGroup").Visible = (cat == "Food");
		GetNode<Control>("BottomBar/VBoxContainer/MarginContainer/TechGroup").Visible = (cat == "Tech");
		var zonesGroup = GetNodeOrNull<Control>("BottomBar/VBoxContainer/MarginContainer/ZonesGroup");
		if (zonesGroup != null) zonesGroup.Visible = (cat == "Zones");
		var disasterGroup = GetNodeOrNull<Control>("BottomBar/VBoxContainer/MarginContainer/DisasterGroup");
		if (disasterGroup != null) disasterGroup.Visible = (cat == "Disaster");
		
		// Update category button appearance
		GetNode<Button>("BottomBar/VBoxContainer/CategoryBar/CatGeneral").Modulate = (cat == "General") ? Colors.Yellow : Colors.White;
		GetNode<Button>("BottomBar/VBoxContainer/CategoryBar/CatFood").Modulate = (cat == "Food") ? Colors.Yellow : Colors.White;
		GetNode<Button>("BottomBar/VBoxContainer/CategoryBar/CatTech").Modulate = (cat == "Tech") ? Colors.Yellow : Colors.White;
		var catZones = GetNodeOrNull<Button>("BottomBar/VBoxContainer/CategoryBar/CatZones");
		if (catZones != null) catZones.Modulate = (cat == "Zones") ? Colors.Yellow : Colors.White;
		var catDisaster = GetNodeOrNull<Button>("BottomBar/VBoxContainer/CategoryBar/CatDisaster");
		if (catDisaster != null) catDisaster.Modulate = (cat == "Disaster") ? Colors.Yellow : Colors.White;
		
		// If switching categories, cancel any active placement/bulldoze mode
		CancelPlacement();
	}

	public void CancelPlacement()
	{
		_isHouseMode = false;
		_isRoadMode = false;
		_isBulldozeMode = false;
		_isZoningMode = false;
		
		if (_buildingManager != null)
		{
			_buildingManager.ToggleBuilding(false);
			_buildingManager.SetBulldozeMode(false);
			_buildingManager.SetZoneMode(false);
		}
		if (_roadManager != null) _roadManager.ToggleBuilding(false);
		
		UpdateButtons();
	}

	public override void _ExitTree()
	{
		if (_sim != null)
			_sim.SimulationTicked -= UpdateDisplay;
	}

	private void SetGameSpeed(float speed)
	{
		Engine.TimeScale = speed;
		UpdateSpeedButtons(speed);
	}

	private void UpdateSpeedButtons(float currentSpeed)
	{
		if (!IsInstanceValid(this)) return;
		string[] speeds = {"0", "1", "2", "4"};
		foreach (var s in speeds) {
			var btn = GetNodeOrNull<Button>($"TimeContainer/HBoxContainer/Speed{s}");
			if (btn != null) {
				float val = float.Parse(s);
				btn.Modulate = currentSpeed == val ? Colors.Yellow : Colors.White;
			}
		}
	}

	private void OnBuildingSelected(string type)
	{
		GD.Print($"HUD: Building selected: {type}");
		_isHouseMode = false;
		_isRoadMode = false;
		_isBulldozeMode = false;
		_isZoningMode = false;
		
		GD.Print($"HUD: OnBuildingSelected - Type: {type}");
		if (_buildingManager == null) 
		{
			GD.PrintErr("HUD: BuildingManager is null!");
			return;
		}
		
		if (_isHouseMode && _currentBuildingType == type)
		{
			GD.Print($"HUD: Toggling OFF {type}");
			_isHouseMode = false;
			_buildingManager.ToggleBuilding(false);
		}
		else
		{
			GD.Print($"HUD: Toggling ON {type}");
			_isHouseMode = true;
			_currentBuildingType = type;
			_isRoadMode = false;
			_isBulldozeMode = false;
			_isZoningMode = false;
			if (_roadManager != null) _roadManager.ToggleBuilding(false);
			_buildingManager.SetBuildingType(type);
			_buildingManager.ToggleBuilding(true);
			_buildingManager.SetBulldozeMode(false);
			_buildingManager.SetZoneMode(false);
		}
		UpdateButtons();
	}

	private void OnRoadButtonPressed()
	{
		if (_roadManager == null) return;
		_isRoadMode = !_isRoadMode;
		if (_isRoadMode) { 
			_isHouseMode = false; 
			_isBulldozeMode = false;
			if (_buildingManager != null) 
			{
				_buildingManager.ToggleBuilding(false); 
				_buildingManager.SetBulldozeMode(false);
				_buildingManager.SetZoneMode(false);
			}
		}
		_roadManager.ToggleBuilding(_isRoadMode);
		UpdateButtons();
	}

	private void OnBulldozeButtonPressed()
	{
		_isBulldozeMode = !_isBulldozeMode;
		if (_isBulldozeMode)
		{
			_isHouseMode = false;
			_isRoadMode = false;
			_isZoningMode = false;
			if (_buildingManager != null) 
			{
				_buildingManager.ToggleBuilding(false);
				_buildingManager.SetZoneMode(false);
			}
			if (_roadManager != null) _roadManager.ToggleBuilding(false);
		}
		
		if (_buildingManager != null) _buildingManager.SetBulldozeMode(_isBulldozeMode);
		UpdateButtons();
	}

	private void OnZoneButtonPressed(bool erase)
	{
		if (erase && _isZoningMode && _buildingManager != null)
		{
			// Check if we are toggling ERASE specifically
			var field = _buildingManager.GetType().GetField("_isZoningErase", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
			bool currentlyErase = field != null && (bool)field.GetValue(_buildingManager);
			if (currentlyErase) { _isZoningMode = false; } // Toggle OFF
			else { _isZoningMode = true; } // Toggle ON Erase
		}
		else if (!erase && _isZoningMode && _buildingManager != null)
		{
			// Check if we are toggling PAINT specifically
			var field = _buildingManager.GetType().GetField("_isZoningErase", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
			bool currentlyErase = field != null && (bool)field.GetValue(_buildingManager);
			if (!currentlyErase) { _isZoningMode = false; } // Toggle OFF
			else { _isZoningMode = true; } // Toggle ON Paint
		}
		else
		{
			_isZoningMode = !_isZoningMode;
		}

		if (_isZoningMode)
		{
			_isHouseMode = false;
			_isRoadMode = false;
			_isBulldozeMode = false;
			if (_buildingManager != null) 
			{
				_buildingManager.ToggleBuilding(false);
				_buildingManager.SetBulldozeMode(false);
				if (erase) _buildingManager.SetZoneEraseMode(true);
				else _buildingManager.SetZoneMode(true);
			}
			if (_roadManager != null) _roadManager.ToggleBuilding(false);
		}
		else if (_buildingManager != null)
		{
			_buildingManager.SetZoneMode(false);
		}
		UpdateButtons();
	}

	private void UpdateButtons()
	{
		var btnGroups = new string[] {"GeneralGroup", "FoodGroup", "TechGroup", "DisasterGroup"};
		var btnNames = new Dictionary<string, string> {
			{"BuildRoad", "Build Road"},
			{"BuildTownCenter", "Town Center"},
			{"BuildMeatShop", "Meat Shop"},
			{"BuildStable", "Stable"},
			{"BuildGuild", "Builder Guild"},
			{"BuildForager", "Forager Hut"},
			{"BuildFishing", "Fishing Hut"},
			{"BuildWoodcutter", "Woodcutter Hut"},
			{"BuildHunter", "Hunter Hut"},
			{"BuildWell", "Well"},
			{"BuildStoneMine", "Stone Mine"},
			{"BuildFire", "Fire Disaster"}
		};

		foreach (var group in btnGroups)
		{
			foreach (var pair in btnNames)
			{
				var btn = GetNodeOrNull<Button>($"BottomBar/VBoxContainer/MarginContainer/{group}/{pair.Key}");
				if (btn != null) btn.Text = pair.Value;
			}
		}
		// Road button hidden
		var roadBtn = GetNodeOrNull<Button>("BottomBar/VBoxContainer/MarginContainer/GeneralGroup/BuildRoad");
		if (roadBtn != null) roadBtn.Visible = false;
		
		if (_isHouseMode)
		{
			string group = "GeneralGroup";
			string btnKey = _currentBuildingType;
			
			// Map building types back to button names
			if (_currentBuildingType == "ForagerHut") { btnKey = "Forager"; group = "FoodGroup"; }
			else if (_currentBuildingType == "FishingHut") { btnKey = "Fishing"; group = "FoodGroup"; }
			else if (_currentBuildingType == "WoodcutterHut") { btnKey = "Woodcutter"; group = "FoodGroup"; }
			else if (_currentBuildingType == "StoneMine") { btnKey = "StoneMine"; group = "TechGroup"; }
			else if (_currentBuildingType == "HunterHut") { btnKey = "Hunter"; group = "FoodGroup"; }
			else if (_currentBuildingType == "MeatShop") group = "FoodGroup";
			else if (_currentBuildingType == "Guild" || _currentBuildingType == "HorseStable") group = "TechGroup";
			else if (_currentBuildingType == "Fire") group = "DisasterGroup";
			
			string btnPath = $"BottomBar/VBoxContainer/MarginContainer/{group}/Build{btnKey}";
			var btn = GetNodeOrNull<Button>(btnPath);
			if (btn != null) btn.Text = "CANCEL";
		}
		
		var bulldozeBtn = GetNodeOrNull<Button>("BottomBar/VBoxContainer/MarginContainer/GeneralGroup/Bulldoze");
		if (bulldozeBtn != null) 
		{
			bulldozeBtn.Text = _isBulldozeMode ? "STOP BULLDOZE" : "BULLDOZE";
			bulldozeBtn.Modulate = _isBulldozeMode ? Colors.Red : Colors.White;
		}

		var zoneResBtn = GetNodeOrNull<Button>("BottomBar/VBoxContainer/MarginContainer/ZonesGroup/ZoneRes");
		var eraseZoneBtn = GetNodeOrNull<Button>("BottomBar/VBoxContainer/MarginContainer/ZonesGroup/EraseZone");
		
		if (zoneResBtn != null && eraseZoneBtn != null && _buildingManager != null)
		{
			var field = _buildingManager.GetType().GetField("_isZoningErase", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
			bool isErase = field != null && (bool)field.GetValue(_buildingManager);
			
			zoneResBtn.Text = (_isZoningMode && !isErase) ? "STOP PAINT" : "ZONE PAINT";
			zoneResBtn.Modulate = (_isZoningMode && !isErase) ? Colors.Yellow : Colors.White;
			
			eraseZoneBtn.Text = (_isZoningMode && isErase) ? "STOP ERASE" : "ZONE ERASER";
			eraseZoneBtn.Modulate = (_isZoningMode && isErase) ? Colors.Red : Colors.White;
		}

		// Hide manual house build to encourage zoning
		var houseBtn = GetNodeOrNull<Button>("BottomBar/VBoxContainer/MarginContainer/GeneralGroup/BuildHouse");
		if (houseBtn != null) houseBtn.Visible = false;
	}

	public override void _Process(double delta)
	{
		if (_followTarget != null && _infoPopup != null && _infoPopup.Visible)
		{
			Vector3 worldPos = _followTarget.GlobalPosition + Vector3.Up * 2.5f;
			Vector2 screenPos = _camera.UnprojectPosition(worldPos);
			_infoPopup.Position = screenPos - (_infoPopup.GetNode<Control>("Panel").Size / 2.0f);
		}
	}

	private void UpdateDisplay()
	{
		if (_popLabel != null) 
		{
			string popText = $"Population: {_sim.Population} ({_sim.UnemployedPopulation} Unemployed)";
			var timeMgr = GetNodeOrNull<TimeManager>("/root/TimeManager");
			if (timeMgr != null)
			{
				popText = timeMgr.GetFormattedTime() + "\n" + popText;
			}
			_popLabel.Text = popText;
		}
		
		if (_foodLabel != null) _foodLabel.Text = $"Food: {_sim.Food:F1}";
		if (_woodLabel != null) _woodLabel.Text = $"Wood: {_sim.Wood:F0}";
		if (_stoneLabel != null) _stoneLabel.Text = $"Stone: {_sim.Stone:F0}";

		// Update popup info if visible
		if (_infoPopup != null && _infoPopup.Visible)
		{
			UpdateBuildingInfoDisplay();
		}
	}
}
