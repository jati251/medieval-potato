using Godot;
using System;

public partial class FishArea : Node3D
{
    [Export] public float FishAmount { get; set; } = 200.0f;
    [Export] public float RegrowRate { get; set; } = 1.0f;

    public float Harvest(float amount)
    {
        float actual = Mathf.Min(amount, FishAmount);
        FishAmount -= actual;
        return actual;
    }

    public override void _Process(double delta)
    {
        FishAmount = Mathf.Min(200.0f, FishAmount + RegrowRate * (float)delta);
    }
}
