using UnityEngine;

/// <summary>
/// Sits next to a player's Animator and swallows the clips' root motion (CharacterAnimator adds it).
/// Having an OnAnimatorMove means the root movement is handed to this script instead of being put
/// into the body, and doing nothing with it means the clips never move or lift the character:
/// the CharacterController alone decides where the player is.
/// </summary>
[RequireComponent(typeof(Animator))]
public class RootMotionSink : MonoBehaviour
{
    void OnAnimatorMove() { }
}
