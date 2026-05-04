using Godot;
using System;
using System.Collections.Generic;

public partial class TownCenter : StaticBody3D
{
    [Export] public PackedScene TransporterScene { get; set; }
    
    private int _assignedTransporters = 0;
    private GlobalSimulation _sim;
    private Timer _transportTimer;
    private Timer _assignmentTimer;

    public override void _Ready()
    {
        _sim = GetNode<GlobalSimulation>("/root/GlobalSimulation");

        // Assignment timer: Try to get 4 transporters
        _assignmentTimer = new Timer();
        _assignmentTimer.WaitTime = 10.0f;
        _assignmentTimer.Autostart = true;
        _assignmentTimer.Timeout += CheckTransporterAssignment;
        AddChild(_assignmentTimer);

        // Transport timer: Periodically check for resources to collect
        _transportTimer = new Timer();
        _transportTimer.WaitTime = 12.0f;
        _transportTimer.Autostart = true;
        _transportTimer.Timeout += PerformTransportCheck;
        AddChild(_transportTimer);

        // Load default transporter scene if null
        if (TransporterScene == null)
        {
            TransporterScene = GD.Load<PackedScene>("res://src/agents/core/VisualPop.tscn");
        }
    }

    private void CheckTransporterAssignment()
    {
        if (_assignedTransporters < 4)
        {
            int needed = 4 - _assignedTransporters;
            int assigned = _sim.RequestWorkers(needed);
            _assignedTransporters += assigned;
            
            if (assigned > 0)
            {
                GD.Print($"TownCenter: Assigned {assigned} new transporters. Total: {_assignedTransporters}");
            }
        }
    }

    private void PerformTransportCheck()
    {
        if (_assignedTransporters < 4) return; // Transporters only work as a team of 4

        var buildings = GetTree().GetNodesInGroup("Buildings");
        foreach (Node b in buildings)
        {
            if (b == this) continue;

            // Use reflection to check for LocalStorage property
            var prop = b.GetType().GetProperty("LocalStorage");
            if (prop != null)
            {
                float amount = (float)prop.GetValue(b);
                if (amount > 0)
                {
                    SpawnTransporter(b as Node3D, amount);
                    prop.SetValue(b, 0.0f); // Collect everything
                    return; // One transport at a time per check
                }
            }
        }
    }

    private void SpawnTransporter(Node3D source, float amount)
    {
        if (source == null) return;

        var transporter = TransporterScene.Instantiate<VisualPop>();
        GetTree().CurrentScene.AddChild(transporter);
        transporter.GlobalPosition = GlobalPosition;
        
        // Slower for carrying heavy loads
        transporter.WalkSpeed = 1.5f;

        var roadMgr = GetTree().Root.FindChild("RoadManager", true, false) as RoadManager;
        if (roadMgr != null)
        {
            Vector3[] toSource = roadMgr.GetRoadPath(GlobalPosition, source.GlobalPosition);
            Vector3[] backToTC = roadMgr.GetRoadPath(source.GlobalPosition, GlobalPosition);
            
            List<Vector3> fullTrip = new List<Vector3>(toSource);
            for (int i = 1; i < backToTC.Length; i++)
            {
                fullTrip.Add(backToTC[i]);
            }

            transporter.WalkPath(fullTrip.ToArray());
            
            // Resource type check via reflection
            string resType = "Food";
            var typeProp = source.GetType().GetProperty("ResourceType");
            if (typeProp != null) resType = (string)typeProp.GetValue(source);

            // Add resources to global pool when they return
            // For simplicity, we add them at the end of the trip
            if (resType == "Wood") _sim.Wood += amount;
            else _sim.Food += amount;

            GD.Print($"TownCenter: Transporter collecting {amount} {resType} from {source.Name}");
        }
    }

    public override void _ExitTree()
    {
        if (_sim != null && _assignedTransporters > 0)
        {
            _sim.ReturnWorkers(_assignedTransporters);
        }
    }
}
