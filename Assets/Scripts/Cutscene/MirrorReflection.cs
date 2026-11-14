using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A true mirror image of a character. This object sits in a "mirror space" (a parent flipped on Z
/// across the mirror plane), and every frame it copies the source character's root and full pose
/// (bone by bone, after animation and any scripted posing), so the reflection is an exact mirror
/// image, left and right included. Extra objects (a carried cup) can be mirrored the same way.
/// </summary>
[DefaultExecutionOrder(200)] // after CafeEnding has posed the character
public class MirrorReflection : MonoBehaviour
{
    [Tooltip("The real character's root.")]
    public Transform source;
    [Tooltip("The real character's model (the object with the Animator).")]
    public Transform sourceModel;
    [Tooltip("This reflection's model: the same character, its Animator turned off.")]
    public Transform model;

    [Tooltip("Other real objects to mirror (a cup)...")]
    public Transform[] mirrorObjects;
    [Tooltip("...and their copies, children of the same mirror space as this reflection.")]
    public Transform[] mirrorCopies;

    readonly List<Transform> src = new List<Transform>(), dst = new List<Transform>();
    readonly List<Transform> extraSrc = new List<Transform>(), extraDst = new List<Transform>();

    void Start()
    {
        if (mirrorObjects != null && mirrorCopies != null)
            for (int i = 0; i < Mathf.Min(mirrorObjects.Length, mirrorCopies.Length); i++) Track(mirrorObjects[i], mirrorCopies[i]);
        if (!sourceModel || !model) return;
        var anim = model.GetComponent<Animator>();
        if (anim) anim.enabled = false;
        Pair(sourceModel, model);
    }

    void Pair(Transform a, Transform b)
    {
        src.Add(a); dst.Add(b);
        int n = Mathf.Min(a.childCount, b.childCount);
        for (int i = 0; i < n; i++) Pair(a.GetChild(i), b.GetChild(i));
    }

    /// <summary>Mirror another object too (its mirror copy must be a child of the same mirror space).</summary>
    public void Track(Transform realObject, Transform mirrorCopy)
    {
        extraSrc.Add(realObject);
        extraDst.Add(mirrorCopy);
    }

    void LateUpdate()
    {
        if (!source) return;
        // the parent mirror space flips Z, so copying the world pose into local space mirrors it
        transform.localPosition = source.position;
        transform.localRotation = source.rotation;
        for (int i = 0; i < src.Count; i++)
        {
            if (!src[i] || !dst[i]) continue;
            dst[i].localPosition = src[i].localPosition;
            dst[i].localRotation = src[i].localRotation;
            dst[i].localScale = src[i].localScale;
        }
        for (int i = 0; i < extraSrc.Count; i++)
        {
            if (!extraSrc[i] || !extraDst[i]) continue;
            extraDst[i].gameObject.SetActive(extraSrc[i].gameObject.activeInHierarchy);
            extraDst[i].localPosition = extraSrc[i].position;
            extraDst[i].localRotation = extraSrc[i].rotation;
            extraDst[i].localScale = extraSrc[i].lossyScale;
        }
    }
}
