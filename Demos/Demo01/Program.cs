using SoftwareRR.BuffeRR;
using SoftwareRR.Demos.FoodTruck;

var cache = new MemoryCache<MenuItem>();

Console.WriteLine("=== Day 1: opening menu (events applied one by one) ===");

// "MealAdded" events - Apply is a pure fold: (current, event) -> next state.
// The cache itself has zero knowledge of "events" - that concept lives in the caller.
cache.Apply(1001, _ => Extensions.Create(1001, "Cevapi", 650, isAvailable: true));
cache.Apply(1002, _ => Extensions.Create(1002, "Burek", 400, isAvailable: true));
cache.Apply(1003, _ => Extensions.Create(1003, "Pljeskavica", 750, isAvailable: true));
cache.Apply(1004, _ => Extensions.Create(1004, "Pomfri", 300, isAvailable: true));

PrintMenu(cache, "Opening menu");

Console.WriteLine("\n=== Later that day: a couple of events land ===");

// "PriceChanged" event - current is a value-type copy inside the lambda,
// so mutating it locally and returning it is a plain, allocation-free fold.
// (MenuItem is a plain struct, not a record struct, so no `with` expression here.)
cache.Apply(1001, current =>
{
    current.PriceCents = 700;
    current.LastUpdatedTicks = DateTime.UtcNow.Ticks;
    return current;
});

// "MealSoldOut" event
cache.Apply(1003, current =>
{
    current.IsAvailable = false;
    current.LastUpdatedTicks = DateTime.UtcNow.Ticks;
    return current;
});

PrintMenu(cache, "After price change + sellout");

Console.WriteLine($"\nVersion of item 1001 (Cevapi): {cache.GetVersion(1001)}  (1 = created, 2 = price changed)");

Console.WriteLine("\n=== Query: available items under 7.00 ===");
foreach (var item in cache.Where(m => m.IsAvailable && m.PriceCents < 700))
{
    Console.WriteLine($"  {Extensions.GetName(item),-16} {item.PriceCents / 100.0:0.00}");
}

Console.WriteLine("\n=== In-place update via TryUpdate: mark Burek unavailable ===");
cache.TryUpdate(1002, (ref MenuItem item) => item.IsAvailable = false);
PrintMenu(cache, "After Burek sold out via TryUpdate");

Console.WriteLine("\n=== Week 2: full menu rotation (ReplaceAll, not per-item events) ===");

ReadOnlySpan<(ulong, MenuItem)> newWeekMenu = new (ulong, MenuItem)[]
{
    (2001, Extensions.Create(2001, "Kukuruz", 350, true)),
    (2002, Extensions.Create(2002, "Kebab", 900, true)),
    (2003, Extensions.Create(2003, "Limunada", 250, true)),
};

cache.ReplaceAll(newWeekMenu);
PrintMenu(cache, "New weekly menu (old items are gone)");

Console.WriteLine($"\nOld key 1001 still present? {cache.ContainsKey(1001)}");
Console.WriteLine($"New key 2002 version: {cache.GetVersion(2002)} (fresh table, so back to 1)");

static void PrintMenu(MemoryCache<MenuItem> cache, string title)
{
    Console.WriteLine($"-- {title} ({cache.Count} items) --");
    foreach (var item in cache.AsSpan())
    {
        string status = item.IsAvailable ? "available" : "SOLD OUT";
        Console.WriteLine($"  [{item.Id}] {Extensions.GetName(item),-16} {item.PriceCents / 100.0,6:0.00}  {status}");
    }
}
