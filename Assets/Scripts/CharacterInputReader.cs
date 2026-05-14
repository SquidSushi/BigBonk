using System;
using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(-2)]
public class CharacterInputReader : MonoBehaviour
{
    public Vector2 MovementInput {get; protected set;}
    

    public virtual void Update()
    {
        
    }
}
