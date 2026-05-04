using Godot;
using System;

public partial class TimeManager : Node
{
    [Export] public float DayDurationSeconds { get; set; } = 120.0f; // 2 minutes for a full day
    
    public float TimeOfDay { get; private set; } = 8.0f; // Start at 8 AM
    public int DayCount { get; private set; } = 1;

    private DirectionalLight3D _sun;
    private WorldEnvironment _environment;

    public override void _Ready()
    {
        _sun = GetTree().CurrentScene.FindChild("DirectionalLight3D", true, false) as DirectionalLight3D;
        _environment = GetTree().CurrentScene.FindChild("WorldEnvironment", true, false) as WorldEnvironment;
    }

    public override void _Process(double delta)
    {
        float timeStep = (24.0f / DayDurationSeconds) * (float)delta;
        TimeOfDay += timeStep;

        if (TimeOfDay >= 24.0f)
        {
            TimeOfDay -= 24.0f;
            DayCount++;
        }

        UpdateLighting();
    }

    private void UpdateLighting()
    {
        if (GetTree() == null || GetTree().CurrentScene == null) return;
        
        if (_sun == null) _sun = GetTree().CurrentScene.FindChild("DirectionalLight3D", true, false) as DirectionalLight3D;
        if (_sun == null) return;

        // Rotate sun based on time (0-24)
        float angle = (TimeOfDay / 24.0f) * 360.0f - 90.0f;
        _sun.RotationDegrees = new Vector3(-angle, 170.0f, 0);

        // Adjust energy based on time
        float energy = 0.0f;
        if (TimeOfDay > 5.5f && TimeOfDay < 18.5f) // Daytime
        {
            energy = Mathf.Clamp(Mathf.Sin((TimeOfDay - 6.0f) / 12.0f * Mathf.Pi), 0.0f, 1.0f);
        }
        _sun.LightEnergy = energy;
    }

    public string GetFormattedTime()
    {
        int hours = (int)TimeOfDay;
        int minutes = (int)((TimeOfDay - hours) * 60);
        return $"Day {DayCount} - {hours:D2}:{minutes:D2}";
    }
}
