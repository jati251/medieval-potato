using Godot;
using System;

public partial class MainMenu : Control
{
	private Control _loadDialog;

	public override void _Ready()
	{
		_loadDialog = GetNode<Control>("SaveLoadDialog");

		// Setup Dialog UI for Loading
		_loadDialog.GetNode<Label>("Panel/VBoxContainer/Title").Text = "Load Village";
		UpdateSlotLabels();

		GetNode<Button>("VBoxContainer/NewGame").Pressed += () => {
			GlobalSimulation.PendingLoadFile = ""; 
			GetTree().ChangeSceneToFile("res://scenes/main.tscn");
		};

		GetNode<Button>("VBoxContainer/LoadGame").Pressed += () => {
			UpdateSlotLabels();
			_loadDialog.Visible = true;
		};

		GetNode<Button>("VBoxContainer/Quit").Pressed += () => GetTree().Quit();

		// Dialog Slot Buttons
		for (int i = 1; i <= 5; i++)
		{
			int slot = i;
			var btn = _loadDialog.GetNode<Button>($"Panel/VBoxContainer/Slot{i}");
			btn.Pressed += () => OnSlotSelected(slot);
		}
		
		_loadDialog.GetNode<Button>("Panel/VBoxContainer/CancelButton").Pressed += () => _loadDialog.Visible = false;
	}

	private void UpdateSlotLabels()
	{
		for (int i = 1; i <= 5; i++)
		{
			string path = $"user://slot{i}.json";
			var btn = _loadDialog.GetNode<Button>($"Panel/VBoxContainer/Slot{i}");
			btn.Text = FileAccess.FileExists(path) ? $"Slot {i} - Village" : $"Slot {i} - Empty";
			btn.Disabled = !FileAccess.FileExists(path); // Can't load empty slots
		}
	}

	private void OnSlotSelected(int slot)
	{
		GlobalSimulation.PendingLoadFile = $"slot{slot}";
		GetTree().ChangeSceneToFile("res://scenes/main.tscn");
	}
}
