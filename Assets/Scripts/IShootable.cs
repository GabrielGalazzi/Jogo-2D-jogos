using UnityEngine;

namespace RicochetPoC
{
    public enum Team { Player, Enemy }

    /// <summary>Anything a projectile can hit that isn't level geometry.</summary>
    public interface IShootable
    {
        Team Team { get; }

        /// <summary>Called when a projectile from the opposing team touches this target. The projectile is consumed afterwards.</summary>
        void OnShot(Projectile projectile);
    }
}
