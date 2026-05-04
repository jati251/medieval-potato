using Godot;
using System;

public partial class LightToggler : Node3D
{
    private OmniLight3D _light;

    public override void _Ready()
    {
        _light = GetNodeOrNull<OmniLight3D>("OmniLight3D");
    }

    public override void _Process(double delta)
    {
        var timeMgr = GetNodeOrNull<TimeManager>("/root/TimeManager");
        if (timeMgr != null && _light != null)
        {
            bool isNight = timeMgr.TimeOfDay >= 18.5f || timeMgr.TimeOfDay <= 5.5f;
            _light.Visible = isNight;
            
            var windows = GetParent().GetNodeOrNull<Node3D>("Windows");
            if (windows != null) windows.Visible = isNight;
        }
    }
}
