using Microsoft.Extensions.Time.Testing;

namespace Zeeq.Biome.Tests.Commerce;

// The same scenarios as CommerceTests.WithBiome.cs, set up by hand.
// Compare how much of each test is setup bookkeeping instead of intent.
public sealed partial class CommerceTests
{
    [Test]
    public async Task Fulfilment_tree_fans_out_and_checkout_inputs_are_sibling_transients_typical()
    {
        // Arrange: five nested loops plus per-customer and per-parent counters.
        var now = DateTimeOffset.UtcNow;
        var customers = new List<Customer>();
        var orders = new List<Order>();
        var lines = new List<LineItem>();
        var shipments = new List<Shipment>();
        var events = new List<TrackingEvent>();
        var inputs = new List<CheckoutInput>();

        for (var c = 0; c < 2; c++)
        {
            var customer = new Customer { Name = "Example shop shopper" };
            customers.Add(customer);
            var orderNumber = 1; // per customer
            var shipmentNumber = 1; // per customer, across orders and lines
            for (var o = 0; o < 3; o++)
            {
                var order = new Order
                {
                    Customer = customer,
                    Number = orderNumber,
                    Reference = $"ORDER-{orderNumber++}",
                    PlacedAt = now.AddDays(-2),
                };
                orders.Add(order);
                for (var l = 1; l <= 4; l++)
                {
                    var line = new LineItem
                    {
                        Order = order,
                        Number = l,
                        Sku = $"SKU-{l}",
                    };
                    lines.Add(line);
                    var shipment = new Shipment
                    {
                        Line = line,
                        Customer = customer,
                        CustomerNumber = shipmentNumber,
                        Label = $"PARCEL-{shipmentNumber++}",
                        ExpectedAt = now.AddDays(1),
                    };
                    shipments.Add(shipment);
                    for (var e = 1; e <= 3; e++)
                    {
                        events.Add(
                            new TrackingEvent
                            {
                                Shipment = shipment,
                                Customer = customer,
                                Number = e,
                                Description = $"Checkpoint {e}",
                                OccurredAt = now.AddHours(-24).AddMinutes(e),
                            }
                        );
                    }
                }
                // A checkout input is a sibling of the lines and is never saved.
                inputs.Add(new CheckoutInput { Order = order, Reference = order.Reference });
            }
        }

        // Saved rows: level by level, excluding the transient inputs.
        var saved = new List<object>();
        saved.AddRange(customers);
        saved.AddRange(orders);
        saved.AddRange(lines);
        saved.AddRange(shipments);
        saved.AddRange(events);

        // Assert: identical to the Biome version.
        await Assert.That(customers.Count).IsEqualTo(2);
        await Assert.That(orders.Count).IsEqualTo(2 * 3);
        await Assert.That(lines.Count).IsEqualTo(2 * 3 * 4);
        await Assert.That(shipments.Count).IsEqualTo(2 * 3 * 4);
        await Assert.That(events.Count).IsEqualTo(2 * 3 * 4 * 3);
        await Assert.That(inputs.Count).IsEqualTo(6);
        await Assert.That(saved.Count).IsEqualTo(2 + 6 + 24 + 24 + 72);
        await Assert.That(saved.OfType<CheckoutInput>().Any()).IsFalse();
        await Assert.That(saved[0]).IsSameReferenceAs(customers[0]);
        await Assert.That(saved[2]).IsSameReferenceAs(orders[0]);
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
    public async Task Cancelled_orders_and_explicit_numbers_keep_customer_and_parent_scopes_distinct_typical()
    {
        // Arrange: reserve order number 1 by hand so the cancelled priority order takes 2.
        var now = DateTimeOffset.UtcNow;
        var customer = new Customer { Name = "Example shop shopper" };
        var orders = new[]
        {
            new Order
            {
                Customer = customer,
                Number = 2,
                Reference = "ORDER-2",
                Cancelled = true,
                Priority = true,
                PlacedAt = now.AddDays(-2),
            },
            new Order
            {
                Customer = customer,
                Number = 1,
                Reference = "ORDER-1",
                PlacedAt = now.AddDays(-2),
            },
        };

        // Line numbers restart per order; shipment numbers continue per customer.
        var lines = orders
            .SelectMany(o =>
                Enumerable
                    .Range(1, 2)
                    .Select(n => new LineItem
                    {
                        Order = o,
                        Number = n,
                        Sku = $"SKU-{n}",
                    })
            )
            .ToList();
        var shipmentNumber = 1;
        var shipments = lines
            .Select(l => new Shipment
            {
                Line = l,
                Customer = customer,
                CustomerNumber = shipmentNumber,
                Label = $"PARCEL-{shipmentNumber++}",
                ExpectedAt = now.AddDays(1),
            })
            .ToList();
        var events = shipments
            .SelectMany(s =>
                Enumerable
                    .Range(1, 2)
                    .Select(n => new TrackingEvent
                    {
                        Shipment = s,
                        Customer = customer,
                        Number = n,
                        Description = $"Checkpoint {n}",
                        OccurredAt = now.AddHours(-24).AddMinutes(n),
                    })
            )
            .ToList();
        // The next order resumes the customer's sequence.
        var next = new Order
        {
            Customer = customer,
            Number = 3,
            Reference = "ORDER-3",
            PlacedAt = now.AddDays(-2),
        };

        // Assert: identical to the Biome version.
        await Assert.That(orders[0].Cancelled && orders[0].Priority).IsTrue();
        await Assert.That(orders[0].Number).IsEqualTo(2);
        await Assert.That(orders[0].Reference).IsEqualTo("ORDER-2");
        await Assert.That(orders[1].Number).IsEqualTo(1);
        await Assert.That(next.Number).IsEqualTo(3);
        await Assert.That(lines[2].Number).IsEqualTo(1);
        await Assert.That(lines[2].Sku).IsEqualTo("SKU-1");
        await Assert.That(shipments[2].CustomerNumber).IsEqualTo(3);
        await Assert.That(shipments[2].Label).IsEqualTo("PARCEL-3");
        await Assert.That(events.Count).IsEqualTo(1 * 2 * 2 * 1 * 2);
    }

    [Test]
    public async Task Tracked_order_helper_and_live_clock_build_a_later_checkpoint_typical()
    {
        // Arrange: no helper, so the five-level branch is written out in the test.
        var instant = new DateTimeOffset(2026, 10, 11, 12, 0, 0, TimeSpan.Zero);
        var time = new FakeTimeProvider(instant);
        var customer = new Customer { Name = "Example shop shopper" };
        var order = new Order
        {
            Customer = customer,
            Number = 1,
            Reference = "ORDER-1",
            PlacedAt = time.GetUtcNow().AddDays(-2),
        };
        var line = new LineItem
        {
            Order = order,
            Number = 1,
            Sku = "SKU-1",
        };
        var shipment = new Shipment
        {
            Line = line,
            Customer = customer,
            CustomerNumber = 1,
            Label = "PARCEL-1",
            ExpectedAt = time.GetUtcNow().AddDays(1),
        };
        var first = new TrackingEvent
        {
            Shipment = shipment,
            Customer = customer,
            Number = 1,
            Description = "Checkpoint 1",
            OccurredAt = time.GetUtcNow().AddHours(-24).AddMinutes(1),
        };

        // Act: advance the clock, then add the second checkpoint by hand.
        time.Advance(TimeSpan.FromHours(2));
        var later = new TrackingEvent
        {
            Shipment = shipment,
            Customer = customer,
            Number = 2,
            Description = "Checkpoint 2",
            OccurredAt = time.GetUtcNow().AddHours(-24).AddMinutes(2),
        };
        var events = new[] { first, later };

        // Assert: identical to the Biome version.
        await Assert.That(first.OccurredAt).IsEqualTo(instant.AddHours(-24).AddMinutes(1));
        await Assert.That(later.OccurredAt).IsEqualTo(instant.AddHours(-22).AddMinutes(2));
        await Assert.That(first.Shipment.ExpectedAt).IsEqualTo(instant.AddDays(1));
        await Assert.That(events.Length).IsEqualTo(2);
    }
}
