public interface IDamageable { void TakeDamage(float amount); }

public interface IInteractable
{
    string Prompt { get; }
    void Interact(Injector user);
}