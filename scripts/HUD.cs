using Godot;
using System;

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
	private Camera3D _camera;

	public void ShowBuildingInfo(Node3D target, string title, string info)
	{
		_followTarget = target;
		_infoPopup.GetNode<Label>("Panel/VBoxContainer/TitleLabel").Text = title;
		_infoPopup.GetNode<Label>("Panel/VBoxContainer/InfoLabel").Text = info;
		_infoPopup.Visible = true;
	}

	private void OnClosePopup()
	{
		_infoPopup.Visible = false;
		_followTarget = null;
	}

	public override void _Ready()
	{
		_infoPopup = GetNode<Control>("BuildingInfoPopup");
		_camera = GetViewport().GetCamera3D();
		
		_popLabel = GetNode<Label>("MarginContainer/VBoxContainer/PopLabel");
		_foodLabel = GetNode<Label>("MarginContainer/VBoxContainer/FoodLabel");
		_woodLabel = GetNode<Label>("MarginContainer/VBoxContainer/WoodLabel");
		
		_sim = GetNode<GlobalSimulation>("/root/GlobalSimulation");
		_sim.SimulationTicked += UpdateDisplay;

		// UI Node References
		var root = GetTree().Root.GetNode("root");
		_roadManager = root.GetNode<RoadManager>("RoadManager");
		_buildingManager = root.GetNode<BuildingManager>("BuildingManager");

		// Buttons
		GetNode<Button>("BottomBar/MarginContainer/HBoxContainer/BuildRoad").Pressed += OnRoadButtonPressed;
		GetNode<Button>("BottomBar/MarginContainer/HBoxContainer/BuildHouse").Pressed += () => OnBuildingSelected("House");
		GetNode<Button>("BottomBar/MarginContainer/HBoxContainer/BuildTownCenter").Pressed += () => OnBuildingSelected("TownCenter");
		GetNode<Button>("BottomBar/MarginContainer/HBoxContainer/BuildMeatShop").Pressed += () => OnBuildingSelected("MeatShop");
		GetNode<Button>("BottomBar/MarginContainer/HBoxContainer/BuildStable").Pressed += () => OnBuildingSelected("HorseStable");

		// Popup
		GetNode<Button>("BuildingInfoPopup/Panel/VBoxContainer/CloseButton").Pressed += OnClosePopup;
		
		UpdateDisplay();
	}

	private void OnBuildingSelected(string type)
	{
		if (_isHouseMode && _currentBuildingType == type)
		{
			// Cancel
			_isHouseMode = false;
			_buildingManager.ToggleBuilding(false);
		}
		else
		{
			_isHouseMode = true;
			_currentBuildingType = type;
			_isRoadMode = false;
			_roadManager.ToggleBuilding(false);
			
			_buildingManager.SetBuildingType(type);
			_buildingManager.ToggleBuilding(true);
		}
		UpdateButtons();
	}

	private void OnRoadButtonPressed()
	{
		_isRoadMode = !_isRoadMode;
		if (_isRoadMode) { _isHouseMode = false; _buildingManager.ToggleBuilding(false); }
		
		_roadManager.ToggleBuilding(_isRoadMode);
		UpdateButtons();
	}

	private void UpdateButtons()
	{
		GetNode<Button>("BottomBar/MarginContainer/HBoxContainer/BuildRoad").Text = _isRoadMode ? "STOP ROAD" : "Build Road";
		
		// Reset building buttons
		GetNode<Button>("BottomBar/MarginContainer/HBoxContainer/BuildHouse").Text = "Build House";
		GetNode<Button>("BottomBar/MarginContainer/HBoxContainer/BuildTownCenter").Text = "Town Center";
		GetNode<Button>("BottomBar/MarginContainer/HBoxContainer/BuildMeatShop").Text = "Meat Shop";
		GetNode<Button>("BottomBar/MarginContainer/HBoxContainer/BuildStable").Text = "Stable";

		if (_isHouseMode)
		{
			string btnPath = "BottomBar/MarginContainer/HBoxContainer/Build" + _currentBuildingType;
			if (HasNode(btnPath))
			{
				GetNode<Button>(btnPath).Text = "CANCEL";
			}
		}
	}

	public override void _Process(double delta)
	{
		if (_followTarget != null && _infoPopup.Visible)
		{
			// Convert 3D world position to 2D screen position
			Vector3 worldPos = _followTarget.GlobalPosition + Vector3.Up * 2.5f;
			Vector2 screenPos = _camera.UnprojectPosition(worldPos);
			
			// Adjust so the center of the popup is over the house
			_infoPopup.Position = screenPos - (_infoPopup.GetNode<Control>("Panel").Size / 2.0f);
		}
	}

	private void UpdateDisplay()
	{
		_popLabel.Text = $"Population: {_sim.Population}";
		_foodLabel.Text = $"Food: {_sim.Food:F1}";
		_woodLabel.Text = $"Wood: {_sim.Wood}";
	}
}
