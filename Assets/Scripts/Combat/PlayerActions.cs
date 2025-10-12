using UnityEngine;

/// <summary>
/// High level entry point for UI buttons or input bindings to trigger the player's abilities.
/// The component simply forwards calls to the specialised ability components which now manage
/// their own animation and fallback behaviour.
/// </summary>
[DisallowMultipleComponent]
public class PlayerActions : MonoBehaviour
{
    [SerializeField] private SpellCaster spellCaster;
    [SerializeField] private PlayerSummoner summoner;
    [SerializeField] private PlayerMelee melee;

    private void Awake()
    {
        if (spellCaster == null) spellCaster = GetComponent<SpellCaster>();
        if (summoner == null) summoner = GetComponent<PlayerSummoner>();
        if (melee == null) melee = GetComponent<PlayerMelee>();
    }

    public void CastSpell()
    {
        spellCaster?.RequestCast();
    }

    public void SummonLegionary()
    {
        summoner?.RequestSummon();
    }

    public void Melee()
    {
        melee?.TriggerMelee();
    }
}
