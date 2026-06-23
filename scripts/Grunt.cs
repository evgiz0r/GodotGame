using Godot;

public partial class Grunt : Node3D
{
    public override void _Ready()
    {
        // AnimationPlayer lives inside the instanced gltf child
        var anim = FindChild("AnimationPlayer", true, false) as AnimationPlayer;
        if (anim != null)
            anim.Play("Walk");
        else
            GD.PrintErr("AnimationPlayer not found in Grunt");
    }
}
