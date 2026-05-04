using Godot;
using System;
using System.Collections.Generic;

public partial class HunterHut : StaticBody3D
{
    [Export] public PackedScene HunterScene { get; set; }
    [Export] public float SearchRadius { get; set; } = 60.0f;

    private int _assignedWorkers = 0;
    private GlobalSimulation _sim;
    private Timer _assignmentTimer;
    private Timer _spawnTimer;
    private bool _isWorkerActive = false;

    public bool IsPreview { get; set; } = false;

    public override void _Ready()
    {
        _sim = GetNode<GlobalSimulation>("/root/GlobalSimulation");
        
        if (IsPreview) return;

        _assignmentTimer = new Timer();
        _assignmentTimer.WaitTime = 5.0f;
        _assignmentTimer.Autostart = true;
        _assignmentTimer.Timeout += CheckWorkerAssignment;
        AddChild(_assignmentTimer);

        _spawnTimer = new Timer();
        _spawnTimer.WaitTime = 15.0f;
        _spawnTimer.Timeout += SpawnHunter;
        AddChild(_spawnTimer);
    }

    private void CheckWorkerAssignment()
    {
        if (_assignedWorkers < 1)
        {
            int assigned = _sim.RequestWorkers(1 - _assignedWorkers);
            _assignedWorkers += assigned;
            
            if (_assignedWorkers > 0 && _spawnTimer.IsStopped())
            {
                _spawnTimer.Start();
            }
        }
    }

    private void SpawnHunter()
    {
        if (_assignedWorkers <= 0 || _isWorkerActive) return;

        Animal target = FindNearestAnimal();
        if (target == null) return;

        _isWorkerActive = true;
        target.IsTargeted = true;

        var hunter = HunterScene.Instantiate<VisualPop>();
        GetTree().CurrentScene.AddChild(hunter);
        hunter.GlobalPosition = GlobalPosition;

        var roadMgr = GetTree().Root.FindChild("RoadManager", true, false) as RoadManager;
        if (roadMgr != null)
        {
            Vector3[] toAnimal = roadMgr.GetRoadPath(GlobalPosition, target.GlobalPosition);
            Vector3[] toHut = roadMgr.GetRoadPath(target.GlobalPosition, GlobalPosition);
            
            List<Vector3> fullTrip = new List<Vector3>(toAnimal);
            for (int i = 1; i < toHut.Length; i++)
            {
                fullTrip.Add(toHut[i]);
            }

            hunter.WalkPath(fullTrip.ToArray());
            
            // Hunter catch logic
            target.Hunt();
            
            // Allow next worker after trip duration (estimated)
            GetTree().CreateTimer(10.0f).Timeout += () => { _isWorkerActive = false; };
        }
        else
        {
            _isWorkerActive = false;
            target.IsTargeted = false;
        }
    }

    private Animal FindNearestAnimal()
    {
        Animal nearest = null;
        float minDist = SearchRadius;

        foreach (Node node in GetTree().CurrentScene.GetChildren())
        {
            if (node is Animal animal && !animal.IsTargeted)
            {
                float dist = GlobalPosition.DistanceTo(animal.GlobalPosition);
                if (dist < minDist)
                {
                    minDist = dist;
                    nearest = animal;
                }
            }
        }
        return nearest;
    }

    public override void _ExitTree()
    {
        if (_sim != null && _assignedWorkers > 0)
        {
            _sim.ReturnWorkers(_assignedWorkers);
        }
    }
}
