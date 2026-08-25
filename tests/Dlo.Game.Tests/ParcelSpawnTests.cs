using System;

using Dlo.Domain;
using Dlo.Game.Carry;

using GdUnit4;
using Godot;

using static GdUnit4.Assertions;

namespace Dlo.Game.Tests;

/// <summary>
/// E2-04. A client builds a parcel from arch §5.2's four values and nothing else, and the things
/// it has not earned do not travel (arch §5.3).
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ParcelSpawnTests
{
    [TestCase]
    public void A_client_builds_the_right_box_from_the_arguments_alone()
    {
        var registry = new ParcelRegistry();
        var record = registry.Register(archetype: 6, size: 4, condition: 200, isLocked: true);

        // Through the wire and back, so nothing reaches the builder except what was sent. A
        // builder that read the record directly would pass a weaker version of this test forever.
        var sent = GD.VarToBytes(ParcelSpawn.ToPayload(ParcelSpawnArgs.From(record)));
        var built = (Carryable)ParcelSpawn.Build(GD.BytesToVar(sent));

        try
        {
            AssertBool(built.Id == record.Id).IsTrue();
            AssertInt(built.Archetype).IsEqual(6);
            AssertInt(built.Size).IsEqual(4);
            AssertInt(built.Condition).IsEqual(200);

            // Capacity is not sent and does not need to be: both sides read it off the size byte
            // (arch §3.3), so a client knows it needs help before it asks for any.
            AssertInt(built.CarriersRequired).IsEqual(record.CarriersRequired);
            AssertInt(built.CarriersRequired).IsEqual(2);
        }
        finally
        {
            built.Free();
        }
    }

    [TestCase]
    public void A_policy_lock_leaves_no_trace_in_the_bytes_that_travel()
    {
        var open = new ParcelRecord(new ParcelId(77), Archetype: 3, Size: 5, Condition: 9, IsLocked: false);
        var locked = open with { IsLocked = true };

        var withoutLock = GD.VarToBytes(ParcelSpawn.ToPayload(ParcelSpawnArgs.From(open)));
        var withLock = GD.VarToBytes(ParcelSpawn.ToPayload(ParcelSpawnArgs.From(locked)));

        // The negative assertion arch §5.3 asks for, taken at the bytes rather than at the type.
        // Base64 rather than SequenceEqual because a failure here should show which bytes differ,
        // not merely that they did.
        AssertString(Convert.ToBase64String(withLock))
            .IsEqual(Convert.ToBase64String(withoutLock));
    }

    [TestCase]
    public void The_payload_is_four_numbers_and_stays_four_numbers()
    {
        var record = new ParcelRecord(new ParcelId(12), Archetype: 1, Size: 2, Condition: 3, IsLocked: false);

        var round = GD.BytesToVar(
            GD.VarToBytes(ParcelSpawn.ToPayload(ParcelSpawnArgs.From(record)))).AsGodotArray();

        // E2-03's manifest is the thing this is guarding against, and it does not exist yet —
        // which is exactly when to write the guard, because afterwards it is a code review.
        AssertInt(round.Count).IsEqual(ParcelSpawn.Fields);
        AssertInt(round[0].AsInt32()).IsEqual(12);
        AssertInt(round[1].AsInt32()).IsEqual(1);
        AssertInt(round[2].AsInt32()).IsEqual(2);
        AssertInt(round[3].AsInt32()).IsEqual(3);
    }

    [TestCase]
    public void A_payload_of_the_wrong_length_is_refused_rather_than_half_built()
    {
        // A protocol mismatch that built three-quarters of a parcel would surface three rooms
        // away as a box with the wrong contents, and nothing would point back to here.
        AssertThrown(() => ParcelSpawn.FromPayload(new Godot.Collections.Array { 1, 2 }))
            .IsInstanceOf<ArgumentException>();
    }
}
