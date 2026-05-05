using Godot;
using System;

public partial class Fire : Node3D
{
    [Export] public float DamageRadius { get; set; } = 25.0f;
    [Export] public float Lifetime { get; set; } = 20.0f;

    private Timer _panicTimer;

    public override void _Ready()
    {
        _panicTimer = new Timer();
        _panicTimer.WaitTime = 0.5f;
        _panicTimer.Autostart = true;
        _panicTimer.Timeout += BroadcastPanic;
        AddChild(_panicTimer);

        // Auto-extinguish
        GetTree().CreateTimer(Lifetime).Timeout += () => QueueFree();

        // Visual juice: Scale up
        Scale = Vector3.Zero;
        Tween tween = CreateTween();
        tween.TweenProperty(this, "scale", Vector3.One * 3.0f, 1.0f).SetTrans(Tween.TransitionType.Back);
    }

    private void BroadcastPanic()
    {
        var pops = GetTree().GetNodesInGroup("VisualPops");
        GD.Print($"Fire: Broadcasting panic to {pops.Count} villagers...");
        foreach (Node p in pops)
        {
            if (p is VisualPop pop)
            {
                float dist = GlobalPosition.DistanceTo(pop.GlobalPosition);
                if (dist < DamageRadius)
                {
                    GD.Print($"Fire: Panicking villager at distance {dist}");
                    pop.Panic(GlobalPosition);
                }
            }
        }
    }
}
