using System;

public interface IHealth
{
    int CurrentHealth { get; }
    int MaxHealth { get; }
    event Action<int, int> OnHealthChanged;
    event Action OnDied;
}
