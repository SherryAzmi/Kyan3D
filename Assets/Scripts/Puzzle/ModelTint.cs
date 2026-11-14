using UnityEngine;

/// <summary>
/// Added by ModelDresser when a model replaces a primitive whose material a puzzle script swaps at
/// runtime (e.g. a dry sand mound turning wet). The primitive's renderer stays hidden; when its
/// material changes, the model is tinted to that material's colour.
/// </summary>
public class ModelTint : MonoBehaviour
{
    public Renderer source;
    public Renderer[] targets;

    Material last;
    MaterialPropertyBlock block;

    void Start() => last = source ? source.sharedMaterial : null;

    void Update()
    {
        if (!source || source.sharedMaterial == last) return;
        last = source.sharedMaterial;
        if (!last || !last.HasProperty("_BaseColor")) return;
        block ??= new MaterialPropertyBlock();
        foreach (var r in targets)
        {
            if (!r) continue;
            r.GetPropertyBlock(block);
            block.SetColor("_BaseColor", last.GetColor("_BaseColor"));
            r.SetPropertyBlock(block);
        }
    }
}
