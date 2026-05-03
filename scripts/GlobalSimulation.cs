using Godot;
using System;

public partial class GlobalSimulation : Node
{
	// --- Resources ---
	[Export] public int Population { get; set; } = 5;
	[Export] public float Food { get; set; } = 100.0f;
	[Export] public float Wood { get; set; } = 50.0f;
	
	// --- Simulation Settings ---
	[Export] public float TickRate { get; set; } = 2.0f; // Seconds per tick
	[Export] public float FoodConsumptionPerPop { get; set; } = 0.5f;

	private double _timeSinceLastTick = 0.0;

	public override void _Ready()
	{
		GD.Print("Global Simulation Initialized.");
	}

	public override void _Process(double delta)
	{
		_timeSinceLastTick += delta;
		
		if (_timeSinceLastTick >= TickRate)
		{
			PerformTick();
			_timeSinceLastTick = 0.0;
		}
	}

	private void PerformTick()
	{
		// 1. Consume Food
		float foodNeeded = Population * FoodConsumptionPerPop;
		Food -= foodNeeded;
		
		// 2. Clamp values
		if (Food < 0) Food = 0;
		
		// 3. Log (Optional: For debugging)
		GD.Print($"Tick: Pop={Population}, Food={Food:F1}, Wood={Wood}");
		
		// 4. Emit signal if needed (e.g. for UI updates)
		EmitSignal(SignalName.SimulationTicked);
	}

	[Signal]
	public delegate void SimulationTickedEventHandler();
}
