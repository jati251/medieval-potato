using Godot;
using System;
using System.Collections.Generic;

public partial class WoodcutterHut : StaticBody3D
{
    [Export] public PackedScene WoodcutterScene { get; set; }
    [Export] public float SearchRadius { get; set; } = 40.0f;

    private int _assignedWorkers = 0;
    public float LocalStorage { get; set; } = 0;
    public string ResourceType => "Wood";
    private GlobalSimulation _sim;
    private Timer _assignmentTimer;
    private Timer _spawnTimer;

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
        _spawnTimer.WaitTime = 12.0f;
        _spawnTimer.Timeout += SpawnWoodcutter;
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

    private void SpawnWoodcutter()
    {
        if (_assignedWorkers <= 0) 
        {
            _spawnTimer.Stop();
            return;
        }

        Tree target = FindNearestTree();
        if (target == null) return;

        var woodcutter = WoodcutterScene.Instantiate<VisualPop>();
        GetTree().CurrentScene.AddChild(woodcutter);
        woodcutter.GlobalPosition = GlobalPosition;

        var roadMgr = GetTree().Root.FindChild("RoadManager", true, false) as RoadManager;
        if (roadMgr != null)
        {
            Vector3[] toTree = roadMgr.GetRoadPath(GlobalPosition, target.GlobalPosition);
            
            // Return Home logic
            ResidencePlot home = _sim.GetRandomHouse();
            Vector3 finalDest = GlobalPosition; // Default back to hut
            if (home != null) finalDest = home.GlobalPosition;

            Vector3[] toDest = roadMgr.GetRoadPath(target.GlobalPosition, finalDest);
            
            List<Vector3> fullTrip = new List<Vector3>(toTree);
            for (int i = 1; i < toDest.Length; i++)
            {
                fullTrip.Add(toDest[i]);
            }

            woodcutter.WalkPath(fullTrip.ToArray());
            
            // Mark target as targeted so other huts don't go there
            if (target is Tree tree) tree.IsTargeted = true;

            // Trigger chopping on the tree when the worker reaches it
            target.Chop(this);
            
            // Release targeting after a delay or when tree falls (simplified for now)
        }
    }

    private Tree FindNearestTree()
    {
        Tree nearest = null;
        float minDist = SearchRadius;

        // Trees are likely children of the root or a forest node
        // Let's search the whole scene or look for a group
        foreach (Node node in GetTree().CurrentScene.GetChildren())
        {
            if (node is Tree tree && !tree.IsTargeted)
            {
                float dist = GlobalPosition.DistanceTo(tree.GlobalPosition);
                if (dist < minDist)
                {
                    minDist = dist;
                    nearest = tree;
                }
            }
            // Also check children of children (for forest groups)
            foreach (Node subChild in node.GetChildren())
            {
                if (subChild is Tree subTree && !subTree.IsTargeted)
                {
                    float dist = GlobalPosition.DistanceTo(subTree.GlobalPosition);
                    if (dist < minDist)
                    {
                        minDist = dist;
                        nearest = subTree;
                    }
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
