/// <summary>
/// Interface for any component that can modify temporal stability.
/// This provides a common contract for anomaly zones, healing zones, 
/// AI fragments, shields, and any other temporal effects.
/// 
/// NOT networked - this is just a contract/interface.
/// Implementations will handle their own networking as needed.
/// </summary>
public interface ITemporalEffect
{
    /// <summary>
    /// Apply this effect to the target's temporal stability.
    /// </summary>
    /// <param name="target">The TemporalStability component to affect</param>
    /// <param name="deltaTime">Time delta for effects that apply over time (e.g., degradation per second)</param>
    void ApplyEffect(TemporalStability target, float deltaTime);
}