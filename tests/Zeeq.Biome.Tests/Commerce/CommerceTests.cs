using Microsoft.Extensions.Time.Testing;

namespace Zeeq.Biome.Tests.Commerce;

public sealed class CommerceTests
{
    [Test]
    public async Task Fulfilment_tree_fans_out_and_checkout_inputs_are_sibling_transients()
    {
        // A busy store: a five-level graph, historical tracking, and transient request DTOs.
        // Arrange: keep the starting builder for inspection after the anchor reaches a leaf.
        var store = new RecordingStore();
        var biome = TestBiome.Start(out _, store);

        // Act: count is PER parent; every Add re-anchors to its own output array.
        await biome
            .AddCustomers(out var customers, 2)
            // 2 customers × 3 orders = 6 orders.
            .AddOrders(out var orders, 3)
            // 6 orders × 4 lines = 24 lines.
            .AddLineItems(out var lines, 4)
            // 24 lines × 1 shipment = 24 shipments.
            .AddShipments(out var shipments, 1)
            // 24 shipments × 3 events = 72 chronological checkpoints.
            .AddTrackingEvents(out var events, 3)
            // Jump back to orders: checkout inputs are siblings of lines, not event children.
            .Under(orders)
            // AsTransient lives on this blueprint: inputs are returned but never saved.
            .AddCheckoutInputs(out var inputs, 1)
            .BuildAsync();

        // Assert: All includes transient outputs; the recording store includes only saved rows.
        await Assert.That(biome.All<Customer>().Count).IsEqualTo(2);
        await Assert.That(biome.All<Order>().Count).IsEqualTo(2 * 3);
        await Assert.That(biome.All<LineItem>().Count).IsEqualTo(2 * 3 * 4);
        await Assert.That(biome.All<Shipment>().Count).IsEqualTo(2 * 3 * 4);
        await Assert.That(biome.All<TrackingEvent>().Count).IsEqualTo(2 * 3 * 4 * 3);
        await Assert.That(biome.All<CheckoutInput>().Count).IsEqualTo(6);
        await Assert.That(store.Entities.Count).IsEqualTo(2 + 6 + 24 + 24 + 72);
        await Assert.That(store.Entities.OfType<CheckoutInput>().Any()).IsFalse();
        await Assert.That(store.Entities[0]).IsSameReferenceAs(customers[0]);
        await Assert.That(store.Entities[2]).IsSameReferenceAs(orders[0]);
        await Assert.That(lines[4].Number).IsEqualTo(1);
        await Assert.That(shipments[4].CustomerNumber).IsEqualTo(5);
        await Assert.That(shipments[12].CustomerNumber).IsEqualTo(1);
        await Assert.That(events[71].Customer).IsSameReferenceAs(customers[1]);
        await Assert
            .That(events[1].OccurredAt - events[0].OccurredAt)
            .IsEqualTo(TimeSpan.FromMinutes(1));
        await Assert.That(events[3].OccurredAt).IsEqualTo(events[0].OccurredAt);
        await Assert.That(inputs[0].Reference).IsEqualTo(orders[0].Reference);
    }

    [Test]
    public async Task Cancelled_orders_and_explicit_numbers_keep_customer_and_parent_scopes_distinct()
    {
        // A cancelled priority order alongside normal orders: traits, skipping, and resumed scopes.
        // Arrange: one session owns progression even when Under revisits an earlier branch.
        var biome = TestBiome.Start(out _);

        // Act: comma means another order; + combines traits on just one order.
        await biome
            .AddCustomer(out var customer)
            .AddOrders(
                out var orders,
                Orders.Cancelled + Orders.Priority,
                // Call site wins: 1 is explicit, so the first order skips to 2.
                o => o.Number = 1
            )
            .AddLineItems(out var lines, 2)
            .AddShipments(out var shipments, 1)
            .AddTrackingEvents(out _, 2)
            // Resume the customer's order sequence rather than starting from a leaf anchor.
            .Under(customer)
            .AddOrder(out var next)
            .BuildAsync();

        // Assert: local line numbers reset; customer-scoped shipment numbers cross order boundaries.
        await Assert.That(orders[0].Cancelled && orders[0].Priority).IsTrue();
        await Assert.That(orders[0].Number).IsEqualTo(2);
        await Assert.That(orders[0].Reference).IsEqualTo("ORDER-2");
        await Assert.That(orders[1].Number).IsEqualTo(1);
        await Assert.That(next.Number).IsEqualTo(3);
        await Assert.That(lines[2].Number).IsEqualTo(1);
        await Assert.That(lines[2].Sku).IsEqualTo("SKU-1");
        await Assert.That(shipments[2].CustomerNumber).IsEqualTo(3);
        await Assert.That(shipments[2].Label).IsEqualTo("PARCEL-3");
        await Assert.That(biome.All<TrackingEvent>().Count).IsEqualTo(1 * 2 * 2 * 1 * 2);
    }

    [Test]
    public async Task Tracked_order_helper_and_live_clock_build_a_later_checkpoint()
    {
        // A tiny helper hides a default five-level branch; a live clock then records a later event.
        // Arrange: pass the provider once to the domain wrapper, not to each blueprint.
        var instant = new DateTimeOffset(2026, 10, 11, 12, 0, 0, TimeSpan.Zero);
        var time = new FakeTimeProvider(instant);
        var biome = TestBiome.Start(out _, time: time);
        biome.AddTrackedOrder(out var first);

        // Act: eager construction lets a test advance time between Add calls before the single save.
        time.Advance(TimeSpan.FromHours(2));
        await biome
            .Under(first.Shipment)
            // Same parent means the event sequence resumes at 2.
            .AddTrackingEvent(out var later)
            .BuildAsync();

        // Assert: Ago and FromNow use the same provider; previously created values stay unchanged.
        await Assert.That(first.OccurredAt).IsEqualTo(instant.AddHours(-24).AddMinutes(1));
        await Assert.That(later.OccurredAt).IsEqualTo(instant.AddHours(-22).AddMinutes(2));
        await Assert.That(first.Shipment.ExpectedAt).IsEqualTo(instant.AddDays(1));
        await Assert.That(biome.All<TrackingEvent>().Count).IsEqualTo(2);
    }
}
