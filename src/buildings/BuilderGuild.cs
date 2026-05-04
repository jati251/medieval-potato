using Godot;
using System;

public partial class BuilderGuild : Node3D
{
	[Export] public PackedScene BuilderScene { get; set; }
	private GlobalSimulation _sim;
	private Timer _checkTimer;

	public bool IsPreview { get; set; } = false;

	public override void _Ready()
	{
		_sim = GetNode<GlobalSimulation>("/root/GlobalSimulation");
		
		if (IsPreview) return;

		// Wait a tiny bit for the building to be placed before spawning
		GetTree().CreateTimer(0.5f).Timeout += SpawnInitialBuilders;
	}

	private void SpawnInitialBuilders()
	{
		for (int i = 0; i < 2; i++)
		{
			SpawnBuilder();
		}
	}

	private void SpawnBuilder()
	{
		if (BuilderScene == null) return;
		
		var builder = BuilderScene.Instantiate<BuilderAgent>();
		AddChild(builder);
		builder.GlobalPosition = GlobalPosition;
	}
}
