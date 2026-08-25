using Dlo.Game.Carry;

using GdUnit4;

using static GdUnit4.Assertions;

namespace Dlo.Game.Tests;

/// <summary>
/// E1-04's validation, branch by branch. Pure, so every case is a call rather than a staged
/// physics scene — which is the only reason all five verdicts get asserted at all.
/// </summary>
[TestSuite]
public class GrabRulesTests
{
    private const float Near = 1.0f;
    private const float Far = GrabRules.Reach + 0.5f;

    [TestCase]
    public void A_load_in_reach_with_a_free_slot_is_granted() =>
        AssertObject(GrabRules.Evaluate(Near, heldBy: 0, carriersRequired: 1, locked: false))
            .IsEqual(GrabVerdict.Granted);

    [TestCase]
    public void A_load_out_of_reach_is_refused() =>
        AssertObject(GrabRules.Evaluate(Far, heldBy: 0, carriersRequired: 1, locked: false))
            .IsEqual(GrabVerdict.OutOfReach);

    [TestCase]
    public void Exactly_at_the_reach_limit_is_granted() =>
        AssertObject(GrabRules.Evaluate(GrabRules.Reach, heldBy: 0, carriersRequired: 1, locked: false))
            .IsEqual(GrabVerdict.Granted);

    [TestCase]
    public void A_one_person_load_already_held_has_no_slot_left() =>
        AssertObject(GrabRules.Evaluate(Near, heldBy: 1, carriersRequired: 1, locked: false))
            .IsEqual(GrabVerdict.NoSlotFree);

    [TestCase]
    public void A_two_person_load_still_has_a_slot_for_the_second_carrier() =>
        AssertObject(GrabRules.Evaluate(Near, heldBy: 1, carriersRequired: 2, locked: false))
            .IsEqual(GrabVerdict.Granted);

    [TestCase]
    public void A_two_person_load_is_full_at_two() =>
        AssertObject(GrabRules.Evaluate(Near, heldBy: 2, carriersRequired: 2, locked: false))
            .IsEqual(GrabVerdict.NoSlotFree);

    [TestCase]
    public void A_policy_locked_load_is_refused_even_in_reach_and_empty() =>
        AssertObject(GrabRules.Evaluate(Near, heldBy: 0, carriersRequired: 1, locked: true))
            .IsEqual(GrabVerdict.Locked);

    [TestCase]
    public void A_lock_is_reported_ahead_of_a_miss()
    {
        // Both wrong at once. The lock is the useful answer: "you cannot have this" tells a player
        // something, and "you missed" sends them walking closer for no reason.
        AssertObject(GrabRules.Evaluate(Far, heldBy: 0, carriersRequired: 1, locked: true))
            .IsEqual(GrabVerdict.Locked);
    }

    [TestCase]
    public void Grabbing_something_you_are_already_holding_is_its_own_answer()
    {
        // Not NoSlotFree, which is what a naive count would say. A second grab from the same hand
        // must not be read as contention, or E1-06's contention assertion passes on a self-collision.
        AssertObject(GrabRules.Evaluate(
                Near, heldBy: 1, carriersRequired: 2, locked: false, alreadyHolding: true))
            .IsEqual(GrabVerdict.AlreadyHolding);
    }

    [TestCase]
    public void A_zero_carrier_load_is_treated_as_needing_one()
    {
        // Authoring will produce a 0 eventually. It must mean "one person", not "nobody can ever
        // hold this" and not "unlimited carriers".
        AssertObject(GrabRules.Evaluate(Near, heldBy: 0, carriersRequired: 0, locked: false))
            .IsEqual(GrabVerdict.Granted);
        AssertObject(GrabRules.Evaluate(Near, heldBy: 1, carriersRequired: 0, locked: false))
            .IsEqual(GrabVerdict.NoSlotFree);
    }
}
