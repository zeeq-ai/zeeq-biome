using Zeeq.Biome;

namespace Zeeq.Biome.Tests.Commerce;

// The seed is store configuration; customers belong to the scenario, not the baseline.
internal sealed record CommerceSeed(string StoreName);

internal sealed class Customer
{
    public string Name { get; set; } = "shopper";
}

internal sealed class Order
{
    public Customer Customer { get; init; } = null!;
    public int Number { get; set; }
    public string? Reference { get; set; }
    public bool Cancelled { get; set; }
    public bool Priority { get; set; }
    public DateTimeOffset PlacedAt { get; set; }
}

internal sealed class LineItem
{
    public Order Order { get; init; } = null!;
    public int Number { get; set; }
    public string? Sku { get; set; }
}

internal sealed class Shipment
{
    public LineItem Line { get; init; } = null!;
    public Customer Customer { get; init; } = null!;
    public int CustomerNumber { get; set; }
    public string? Label { get; set; }
    public DateTimeOffset ExpectedAt { get; set; }
}

internal sealed class TrackingEvent
{
    public Shipment Shipment { get; init; } = null!;
    public Customer Customer { get; init; } = null!;
    public int Number { get; set; }
    public string? Description { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
}

internal sealed class CheckoutInput
{
    public Order Order { get; init; } = null!;
    public string? Reference { get; set; }
}

internal static class Orders
{
    // + composes both changes on one order; a comma creates another order.
    internal static readonly Action<Order> Cancelled = o => o.Cancelled = true;
    internal static readonly Action<Order> Priority = o => o.Priority = true;
}

internal static class Blueprints
{
    // Scope is declared on the sequence: siblings resume the customer's order numbering.
    internal static readonly Sequence<int> OrderNumbers = Sequence
        .Of(1, n => n + 1)
        .Per<Customer>();

    // Immediate parent is the order; each order starts its own line numbering.
    internal static readonly Sequence<int> LineNumbers = Sequence.Of(1, n => n + 1).PerParent();

    // Shipments have line parents, but numbering spans all lines/orders for the customer.
    internal static readonly Sequence<int> ShipmentNumbers = Sequence
        .Of(1, n => n + 1)
        .Per<Customer>();

    // Event ordering belongs to one shipment, independent of its fan-out position.
    internal static readonly Sequence<int> EventNumbers = Sequence.Of(1, n => n + 1).PerParent();

    // Root factories can use the seed without coupling the library to the domain.
    internal static readonly Blueprint<CommerceSeed, Customer> Customer = Blueprint.For<
        CommerceSeed,
        Customer
    >(ctx => new() { Name = $"{ctx.Seed.StoreName} shopper" });

    // Leave Number/Reference unset: call-site overrides run before these fill-only derives.
    internal static readonly ChildBlueprint<CommerceSeed, Customer, Order> Order = Blueprint
        .ChildOf<CommerceSeed, Customer, Order>(
            (customer, ctx) =>
                new() { Customer = customer, PlacedAt = ctx.Ago(TimeSpan.FromDays(2)) }
        )
        .Derive(o => o.Number, OrderNumbers)
        .Derive(o => o.Reference, (o, _) => $"ORDER-{o.Number}");

    // Sku must follow the number derive, so it uses the final customized/generated number.
    internal static readonly ChildBlueprint<CommerceSeed, Order, LineItem> Line = Blueprint
        .ChildOf<CommerceSeed, Order, LineItem>((order, _) => new() { Order = order })
        .Derive(l => l.Number, LineNumbers)
        .Derive(l => l.Sku, (l, _) => $"SKU-{l.Number}");

    // Reach through Line -> Order -> Customer; relationships never depend on Last<T>().
    internal static readonly ChildBlueprint<CommerceSeed, LineItem, Shipment> Shipment = Blueprint
        .ChildOf<CommerceSeed, LineItem, Shipment>(
            (line, ctx) =>
                new()
                {
                    Line = line,
                    Customer = ctx.Ancestor<Customer>(),
                    ExpectedAt = ctx.FromNow(TimeSpan.FromDays(1)),
                }
        )
        .Derive(s => s.CustomerNumber, ShipmentNumbers)
        .Derive(s => s.Label, (s, _) => $"PARCEL-{s.CustomerNumber}");

    // Derive time after the ordinal; Ago describes a chronologically ordered historical trail.
    internal static readonly ChildBlueprint<CommerceSeed, Shipment, TrackingEvent> Event = Blueprint
        .ChildOf<CommerceSeed, Shipment, TrackingEvent>(
            (shipment, ctx) => new() { Shipment = shipment, Customer = ctx.Ancestor<Customer>() }
        )
        .Derive(e => e.Number, EventNumbers)
        .Derive(e => e.Description, (e, _) => $"Checkpoint {e.Number}")
        .Derive(
            e => e.OccurredAt,
            (e, ctx) => ctx.Ago(TimeSpan.FromHours(24)) + TimeSpan.FromMinutes(e.Number)
        );

    // Request DTOs can be built alongside stored entities; transient is a blueprint variant.
    internal static readonly ChildBlueprint<CommerceSeed, Order, CheckoutInput> Checkout = Blueprint
        .ChildOf<CommerceSeed, Order, CheckoutInput>((order, _) => new() { Order = order })
        .Derive(i => i.Reference, (i, _) => i.Order.Reference)
        .AsTransient();
}

internal static class TestBiome
{
    private static readonly BiomeOptions<CommerceSeed> Options = new();

    // Share frozen options and definitions; create a fresh session for every scenario.
    internal static BiomeBuilder<CommerceSeed, CommerceSeed> Start(
        out CommerceSeed seed,
        IBiomeStore? store = null,
        TimeProvider? time = null
    )
    {
        seed = new("Example shop");
        return Biome.Start(seed, Options, store, time);
    }
}
