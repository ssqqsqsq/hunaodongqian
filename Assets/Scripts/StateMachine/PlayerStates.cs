// Assets/Scripts/StateMachine/PlayerStates.cs
public enum PlayerState { Dead, Idle, Swim, Sprint }

public interface IPlayerState
{
    float GasRate { get; }      // pt/s
    float CO2Rate { get; }      // /s
    float StaminaRate { get; }  // /s
}

public class DeadState : IPlayerState
{
    public float GasRate => 0f;
    public float CO2Rate => -3f;
    public float StaminaRate => 0f;
}

public class IdleState : IPlayerState
{
    public float GasRate => 1f;
    public float CO2Rate => -2f;
    public float StaminaRate => 2f;
}

public class SwimState : IPlayerState
{
    public float GasRate => 2f;
    public float CO2Rate => -1f;
    public float StaminaRate => 1f;
}

public class SprintState : IPlayerState
{
    public float GasRate => 4f;
    public float CO2Rate => 1f;
    public float StaminaRate => -1f;
}