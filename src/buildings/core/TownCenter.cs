using Godot;
using System;

public partial class TownCenter : StaticBody3D
{
    private GlobalSimulation _sim;
    private Label3D _storageLabel;

    public override void _Ready()
    {
        _sim = GetNode<GlobalSimulation>("/root/GlobalSimulation");
        _storageLabel = new Label3D();
        _storageLabel.Text = "Storage: 0/0";
        _storageLabel.FontSize = 32;
        _storageLabel.Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
        _storageLabel.Position = new Vector3(0, 5, 0);
        AddChild(_storageLabel);
        
        // Update label on ticks
        _sim.Connect("SimulationTicked", Callable.From(UpdateLabel));
        UpdateLabel();
    }

    private void UpdateLabel()
    {
        if (_sim != null && _storageLabel != null)
        {
            float current = _sim.Food + _sim.Wood;
            _storageLabel.Text = $"Storage: {Mathf.RoundToInt(current)}/{Mathf.RoundToInt(_sim.StorageCapacity)}";
        }
    }
}
