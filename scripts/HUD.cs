using Godot;
using System;

public partial class HUD : CanvasLayer
{
	private Label _popLabel;
	private Label _foodLabel;
	private Label _woodLabel;
	private GlobalSimulation _sim;

	public override void _Ready()
	{
		_popLabel = GetNode<Label>("MarginContainer/VBoxContainer/PopLabel");
		_foodLabel = GetNode<Label>("MarginContainer/VBoxContainer/FoodLabel");
		_woodLabel = GetNode<Label>("MarginContainer/VBoxContainer/WoodLabel");
		
		_sim = GetNode<GlobalSimulation>("/root/GlobalSimulation");
		_sim.SimulationTicked += UpdateDisplay;
		
		UpdateDisplay();
	}

	private void UpdateDisplay()
	{
		_popLabel.Text = $"Population: {_sim.Population}";
		_foodLabel.Text = $"Food: {_sim.Food:F1}";
		_woodLabel.Text = $"Wood: {_sim.Wood}";
	}
}
