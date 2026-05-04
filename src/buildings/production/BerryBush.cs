using Godot;
using System;

public partial class BerryBush : StaticBody3D
{
    [Export] public float BerryAmount { get; set; } = 100.0f;
    [Export] public float RegrowRate { get; set; } = 0.5f;

    public float Harvest(float amount)
    {
        float actual = Mathf.Min(amount, BerryAmount);
        BerryAmount -= actual;
        return actual;
    }

    public override void _Process(double delta)
    {
        BerryAmount = Mathf.Min(100.0f, BerryAmount + RegrowRate * (float)delta);
    }
}
