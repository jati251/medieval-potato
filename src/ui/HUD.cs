using Godot;
using System;
using System.Collections.Generic;

public partial class HUD : CanvasLayer
{
	private Label _popLabel;
	private Label _foodLabel;
	private Label _woodLabel;
	private GlobalSimulation _sim;
	private RoadManager _roadManager;
	private BuildingManager _buildingManager;
	private bool _isRoadMode = false;
	private bool _isHouseMode = false;
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

	public void ShowBuildingInfo(Node3D target, string title, string info)
	{
		if (_infoPopup == null) return;
		_followTarget = target;
		_infoPopup.GetNode<Label>("Panel/VBoxContainer/TitleLabel").Text = title;
		_infoPopup.GetNode<Label>("Panel/VBoxContainer/InfoLabel").Text = info;
		_infoPopup.Visible = true;
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
		// Category Buttons
		GetNode<Button>("BottomBar/VBoxContainer/CategoryBar/CatGeneral").Pressed += () => SelectCategory("General");
		GetNode<Button>("BottomBar/VBoxContainer/CategoryBar/CatFood").Pressed += () => SelectCategory("Food");
		GetNode<Button>("BottomBar/VBoxContainer/CategoryBar/CatTech").Pressed += () => SelectCategory("Tech");

		var btns = new Dictionary<string, string> {
			{"GeneralGroup/BuildRoad", null},
			{"GeneralGroup/BuildHouse", "House"},
			{"GeneralGroup/BuildTownCenter", "TownCenter"},
			{"FoodGroup/BuildMeatShop", "MeatShop"},
			{"TechGroup/BuildStable", "HorseStable"},
			{"TechGroup/BuildGuild", "Guild"},
			{"FoodGroup/BuildForager", "ForagerHut"},
			{"FoodGroup/BuildFishing", "FishingHut"}
		};

		foreach (var pair in btns)
		{
			var btn = GetNodeOrNull<Button>("BottomBar/VBoxContainer/MarginContainer/" + pair.Key);
			if (btn == null) continue;
			
			if (pair.Key.EndsWith("BuildRoad")) btn.Pressed += OnRoadButtonPressed;
			else btn.Pressed += () => OnBuildingSelected(pair.Value);
		}

		if (GetNodeOrNull<Button>("BuildingInfoPopup/Panel/VBoxContainer/CloseButton") is Button b) b.Pressed += OnClosePopup;

		foreach (int i in new int[] {0, 1, 2, 4}) {
			var speedBtn = GetNodeOrNull<Button>($"TimeContainer/HBoxContainer/Speed{i}");
			if (speedBtn != null) {
				float s = (float)i;
				speedBtn.Pressed += () => SetGameSpeed(s);
			}
		}
	}

	private void SelectCategory(string cat)
	{
		GetNode<Control>("BottomBar/VBoxContainer/MarginContainer/GeneralGroup").Visible = (cat == "General");
		GetNode<Control>("BottomBar/VBoxContainer/MarginContainer/FoodGroup").Visible = (cat == "Food");
		GetNode<Control>("BottomBar/VBoxContainer/MarginContainer/TechGroup").Visible = (cat == "Tech");
		
		// Update category button appearance
		GetNode<Button>("BottomBar/VBoxContainer/CategoryBar/CatGeneral").Modulate = (cat == "General") ? Colors.Yellow : Colors.White;
		GetNode<Button>("BottomBar/VBoxContainer/CategoryBar/CatFood").Modulate = (cat == "Food") ? Colors.Yellow : Colors.White;
		GetNode<Button>("BottomBar/VBoxContainer/CategoryBar/CatTech").Modulate = (cat == "Tech") ? Colors.Yellow : Colors.White;
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
		if (_buildingManager == null) return;
		if (_isHouseMode && _currentBuildingType == type)
		{
			_isHouseMode = false;
			_buildingManager.ToggleBuilding(false);
		}
		else
		{
			_isHouseMode = true;
			_currentBuildingType = type;
			_isRoadMode = false;
			if (_roadManager != null) _roadManager.ToggleBuilding(false);
			_buildingManager.SetBuildingType(type);
			_buildingManager.ToggleBuilding(true);
		}
		UpdateButtons();
	}

	private void OnRoadButtonPressed()
	{
		if (_roadManager == null) return;
		_isRoadMode = !_isRoadMode;
		if (_isRoadMode) { 
			_isHouseMode = false; 
			if (_buildingManager != null) _buildingManager.ToggleBuilding(false); 
		}
		_roadManager.ToggleBuilding(_isRoadMode);
		UpdateButtons();
	}

	private void UpdateButtons()
	{
		var btnGroups = new string[] {"GeneralGroup", "FoodGroup", "TechGroup"};
		var btnNames = new Dictionary<string, string> {
			{"BuildRoad", "Build Road"},
			{"BuildHouse", "Build House"},
			{"BuildTownCenter", "Town Center"},
			{"BuildMeatShop", "Meat Shop"},
			{"BuildStable", "Stable"},
			{"BuildGuild", "Builder Guild"},
			{"BuildForager", "Forager Hut"},
			{"BuildFishing", "Fishing Hut"}
		};

		foreach (var group in btnGroups)
		{
			foreach (var pair in btnNames)
			{
				var btn = GetNodeOrNull<Button>($"BottomBar/VBoxContainer/MarginContainer/{group}/{pair.Key}");
				if (btn != null) btn.Text = pair.Value;
			}
		}

		var roadBtn = GetNodeOrNull<Button>("BottomBar/VBoxContainer/MarginContainer/GeneralGroup/BuildRoad");
		if (roadBtn != null) roadBtn.Text = _isRoadMode ? "STOP ROAD" : "Build Road";
		
		if (_isHouseMode)
		{
			string group = "GeneralGroup";
			string btnKey = _currentBuildingType;
			if (_currentBuildingType == "ForagerHut") { btnKey = "Forager"; group = "FoodGroup"; }
			if (_currentBuildingType == "FishingHut") { btnKey = "Fishing"; group = "FoodGroup"; }
			if (_currentBuildingType == "MeatShop") group = "FoodGroup";
			if (_currentBuildingType == "Guild" || _currentBuildingType == "HorseStable") group = "TechGroup";
			
			string btnPath = $"BottomBar/VBoxContainer/MarginContainer/{group}/Build{btnKey}";
			var btn = GetNodeOrNull<Button>(btnPath);
			if (btn != null) btn.Text = "CANCEL";
		}
	}

	public override void _Process(double delta)
	{
		if (_followTarget != null && _infoPopup != null && _infoPopup.Visible)
		{
			Vector3 worldPos = _followTarget.GlobalPosition + Vector3.Up * 2.5f;
			Vector2 screenPos = _camera.UnprojectPosition(worldPos);
			_infoPopup.Position = screenPos - (_infoPopup.GetNode<Control>("Panel").Size / 2.0f);
		}
		
		_popLabel.Text = $"Population: {_sim.Population} ({_sim.UnemployedPopulation} Unemployed)";
		_foodLabel.Text = $"Food: {_sim.Food:F1}";
		_woodLabel.Text = $"Wood: {_sim.Wood:F0}";

		var timeMgr = GetNodeOrNull<TimeManager>("/root/TimeManager");
		if (timeMgr != null)
		{
			_popLabel.Text = timeMgr.GetFormattedTime() + "\n" + _popLabel.Text;
		}
	}

	private void UpdateDisplay()
	{
		if (_popLabel != null) _popLabel.Text = $"Population: {_sim.Population} ({_sim.UnemployedPopulation} Unemployed)";
		if (_foodLabel != null) _foodLabel.Text = $"Food: {_sim.Food:F1}";
		if (_woodLabel != null) _woodLabel.Text = $"Wood: {_sim.Wood}";
	}
}
