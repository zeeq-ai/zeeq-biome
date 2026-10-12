using Zeeq.Biome;

namespace Zeeq.Biome.Tests.Commerce;

internal static class Sugar
{
    // Root/input sugar works from any anchor; no implicit ancestor construction is needed.
    extension<TAnchor>(BiomeBuilder<CommerceSeed, TAnchor> b)
    {
        /// <summary>Builds one default Customer.</summary>
        public BiomeBuilder<CommerceSeed, Customer[]> AddCustomer(out Customer entity) =>
            b.AddOne(Blueprints.Customer, out entity);

        /// <summary>Customizes one output before fill-only derives; the call site wins.</summary>
        public BiomeBuilder<CommerceSeed, Customer[]> AddCustomer(
            out Customer entity,
            Action<Customer> customize
        ) => b.AddOne(Blueprints.Customer, out entity, customize);

        /// <summary>Builds count outputs.</summary>
        public BiomeBuilder<CommerceSeed, Customer[]> AddCustomers(
            out Customer[] entities,
            int count
        ) => b.Add(Blueprints.Customer, out entities, count);

        /// <summary>Builds one output per delegate; use + to stack traits.</summary>
        public BiomeBuilder<CommerceSeed, Customer[]> AddCustomers(
            out Customer[] entities,
            params Action<Customer>[] customize
        ) => b.Add(Blueprints.Customer, out entities, customize);
    }

    // Only the matching parent anchor offers these child methods; each Add returns a new output anchor.
    extension(BiomeBuilder<CommerceSeed, Customer[]> b)
    {
        /// <summary>Builds one default Order under exactly one Customer.</summary>
        public BiomeBuilder<CommerceSeed, Order[]> AddOrder(out Order entity) =>
            b.AddOne(Blueprints.Order, out entity);

        /// <summary>Customizes one output before fill-only derives; the call site wins.</summary>
        public BiomeBuilder<CommerceSeed, Order[]> AddOrder(
            out Order entity,
            Action<Order> customize
        ) => b.AddOne(Blueprints.Order, out entity, customize);

        /// <summary>Builds count outputs per parent, flattened in parent order.</summary>
        public BiomeBuilder<CommerceSeed, Order[]> AddOrders(out Order[] entities, int count) =>
            b.Add(Blueprints.Order, out entities, count);

        /// <summary>Builds one output per delegate per parent; use + to stack traits.</summary>
        public BiomeBuilder<CommerceSeed, Order[]> AddOrders(
            out Order[] entities,
            params Action<Order>[] customize
        ) => b.Add(Blueprints.Order, out entities, customize);
    }

    // Only the matching parent anchor offers these child methods; each Add returns a new output anchor.
    extension(BiomeBuilder<CommerceSeed, Order[]> b)
    {
        /// <summary>Builds one default LineItem under exactly one Order.</summary>
        public BiomeBuilder<CommerceSeed, LineItem[]> AddLineItem(out LineItem entity) =>
            b.AddOne(Blueprints.Line, out entity);

        /// <summary>Customizes one output before fill-only derives; the call site wins.</summary>
        public BiomeBuilder<CommerceSeed, LineItem[]> AddLineItem(
            out LineItem entity,
            Action<LineItem> customize
        ) => b.AddOne(Blueprints.Line, out entity, customize);

        /// <summary>Builds count outputs per parent, flattened in parent order.</summary>
        public BiomeBuilder<CommerceSeed, LineItem[]> AddLineItems(
            out LineItem[] entities,
            int count
        ) => b.Add(Blueprints.Line, out entities, count);

        /// <summary>Builds one output per delegate per parent; use + to stack traits.</summary>
        public BiomeBuilder<CommerceSeed, LineItem[]> AddLineItems(
            out LineItem[] entities,
            params Action<LineItem>[] customize
        ) => b.Add(Blueprints.Line, out entities, customize);
    }

    // Only the matching parent anchor offers these child methods; each Add returns a new output anchor.
    extension(BiomeBuilder<CommerceSeed, LineItem[]> b)
    {
        /// <summary>Builds one default Shipment under exactly one LineItem.</summary>
        public BiomeBuilder<CommerceSeed, Shipment[]> AddShipment(out Shipment entity) =>
            b.AddOne(Blueprints.Shipment, out entity);

        /// <summary>Customizes one output before fill-only derives; the call site wins.</summary>
        public BiomeBuilder<CommerceSeed, Shipment[]> AddShipment(
            out Shipment entity,
            Action<Shipment> customize
        ) => b.AddOne(Blueprints.Shipment, out entity, customize);

        /// <summary>Builds count outputs per parent, flattened in parent order.</summary>
        public BiomeBuilder<CommerceSeed, Shipment[]> AddShipments(
            out Shipment[] entities,
            int count
        ) => b.Add(Blueprints.Shipment, out entities, count);

        /// <summary>Builds one output per delegate per parent; use + to stack traits.</summary>
        public BiomeBuilder<CommerceSeed, Shipment[]> AddShipments(
            out Shipment[] entities,
            params Action<Shipment>[] customize
        ) => b.Add(Blueprints.Shipment, out entities, customize);
    }

    // Only the matching parent anchor offers these child methods; each Add returns a new output anchor.
    extension(BiomeBuilder<CommerceSeed, Shipment[]> b)
    {
        /// <summary>Builds one default TrackingEvent under exactly one Shipment.</summary>
        public BiomeBuilder<CommerceSeed, TrackingEvent[]> AddTrackingEvent(
            out TrackingEvent entity
        ) => b.AddOne(Blueprints.Event, out entity);

        /// <summary>Customizes one output before fill-only derives; the call site wins.</summary>
        public BiomeBuilder<CommerceSeed, TrackingEvent[]> AddTrackingEvent(
            out TrackingEvent entity,
            Action<TrackingEvent> customize
        ) => b.AddOne(Blueprints.Event, out entity, customize);

        /// <summary>Builds count outputs per parent, flattened in parent order.</summary>
        public BiomeBuilder<CommerceSeed, TrackingEvent[]> AddTrackingEvents(
            out TrackingEvent[] entities,
            int count
        ) => b.Add(Blueprints.Event, out entities, count);

        /// <summary>Builds one output per delegate per parent; use + to stack traits.</summary>
        public BiomeBuilder<CommerceSeed, TrackingEvent[]> AddTrackingEvents(
            out TrackingEvent[] entities,
            params Action<TrackingEvent>[] customize
        ) => b.Add(Blueprints.Event, out entities, customize);
    }

    // Only the matching parent anchor offers these child methods; each Add returns a new output anchor.
    extension(BiomeBuilder<CommerceSeed, Order[]> b)
    {
        /// <summary>Builds one default CheckoutInput under exactly one Order.</summary>
        public BiomeBuilder<CommerceSeed, CheckoutInput[]> AddCheckoutInput(
            out CheckoutInput entity
        ) => b.AddOne(Blueprints.Checkout, out entity);

        /// <summary>Customizes one output before fill-only derives; the call site wins.</summary>
        public BiomeBuilder<CommerceSeed, CheckoutInput[]> AddCheckoutInput(
            out CheckoutInput entity,
            Action<CheckoutInput> customize
        ) => b.AddOne(Blueprints.Checkout, out entity, customize);

        /// <summary>Builds count outputs per parent, flattened in parent order.</summary>
        public BiomeBuilder<CommerceSeed, CheckoutInput[]> AddCheckoutInputs(
            out CheckoutInput[] entities,
            int count
        ) => b.Add(Blueprints.Checkout, out entities, count);

        /// <summary>Builds one output per delegate per parent; use + to stack traits.</summary>
        public BiomeBuilder<CommerceSeed, CheckoutInput[]> AddCheckoutInputs(
            out CheckoutInput[] entities,
            params Action<CheckoutInput>[] customize
        ) => b.Add(Blueprints.Checkout, out entities, customize);
    }

    // Scenario helpers are explicit app recipes, available only from the seed anchor.
    extension(BiomeBuilder<CommerceSeed, CommerceSeed> b)
    {
        /// <summary>Builds a complete default 5-level branch and returns its leaf.</summary>
        public BiomeBuilder<CommerceSeed, TrackingEvent[]> AddTrackedOrder(
            out TrackingEvent leaf
        ) =>
            b.AddCustomer(out _)
                .AddOrder(out _)
                .AddLineItem(out _)
                .AddShipment(out _)
                .AddTrackingEvent(out leaf);
    }
}
