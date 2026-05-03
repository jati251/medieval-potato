using Godot;
using System;

public partial class MainMenu : CanvasLayer
{
	public override void _Ready()
	{
		GetNode<Button>("CenterContainer/VBoxContainer/StartButton").Pressed += OnStartPressed;
		GetNode<Button>("CenterContainer/VBoxContainer/QuitButton").Pressed += OnQuitPressed;
	}

	private void OnStartPressed()
	{
		GetTree().ChangeSceneToFile("res://scenes/main.tscn");
	}

	private void OnQuitPressed()
	{
		GetTree().Quit();
	}
}
