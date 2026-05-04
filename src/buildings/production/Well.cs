using Godot;
using System;

public partial class Well : Node3D
{
	// Well is now a passive infrastructure marker for houses
	public override void _Ready()
	{
		// No tick needed, houses check for nearby Wells
	}
}
