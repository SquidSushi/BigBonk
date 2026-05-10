using System;
using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(-2)]
public class CharacterInputProvider : MonoBehaviour
{
    public Vector2 MovementInput {get; protected set;}
    

    public virtual void Update()
    {
        
    }
}
