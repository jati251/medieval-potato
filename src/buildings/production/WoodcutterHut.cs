using Godot;
using System;
using System.Collections.Generic;

public partial class WoodcutterHut : StaticBody3D
{
    [Export] public PackedScene WoodcutterScene { get; set; }
    [Export] public float SearchRadius { get; set; } = 40.0f;

    private int _assignedWorkers = 0;
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
        if (_assignedWorkers < 2)
        {
            int needed = 2 - _assignedWorkers;
            int assigned = _sim.RequestWorkers(needed);
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
            Vector3[] toHut = roadMgr.GetRoadPath(target.GlobalPosition, GlobalPosition);
            
            List<Vector3> fullTrip = new List<Vector3>(toTree);
            for (int i = 1; i < toHut.Length; i++)
            {
                fullTrip.Add(toHut[i]);
            }

            woodcutter.WalkPath(fullTrip.ToArray());
            
            // Trigger chopping on the tree when the worker reaches it
            // For now, we'll just chop it immediately for the visual effect
            // In a more complex sim, we'd wait for the worker to arrive.
            target.Chop();
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
            if (node is Tree tree)
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
                if (subChild is Tree subTree)
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
