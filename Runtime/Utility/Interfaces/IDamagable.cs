namespace Utility {
    public enum DamagableTeam {
        Player,
        Opponent,
    }
    
    public interface IDamagable {
        DamagableTeam team { get; }

        int health { get; set; }
        void TakeDamage(int damageAmount);
    }
}