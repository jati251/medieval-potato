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

	public override void _Ready()
	{
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
		GetNode<Button>("BottomBar/MarginContainer/HBoxContainer/BuildHouse").Pressed += OnHouseButtonPressed;
		
		UpdateDisplay();
	}

	private void OnHouseButtonPressed()
	{
		_isHouseMode = !_isHouseMode;
		if (_isHouseMode) { _isRoadMode = false; _roadManager.ToggleBuilding(false); }
		
		_buildingManager.ToggleBuilding(_isHouseMode);
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
		GetNode<Button>("BottomBar/MarginContainer/HBoxContainer/BuildHouse").Text = _isHouseMode ? "CANCEL HOUSE" : "Build House";
	}

	private void UpdateDisplay()
	{
		_popLabel.Text = $"Population: {_sim.Population}";
		_foodLabel.Text = $"Food: {_sim.Food:F1}";
		_woodLabel.Text = $"Wood: {_sim.Wood}";
	}
}
