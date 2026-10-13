using System.Runtime.InteropServices;
using System.Text;

namespace SoftwareRR.Demos.FoodTruck;

// TValue : unmanaged means no string Name — a managed reference isn't blittable.
// This is a good moment to bring in [InlineArray] (.NET 8),
// which lets you define a fixed-size inline buffer of any element type
// on a struct — perfect for a small bounded name that still keeps the struct unmanaged.
// That's a direct, practical Span use — writing a bounded UTF8 string into inline struct memory with zero heap allocation.
// NOTE: look into c# record struct?

[System.Runtime.CompilerServices.InlineArray(32)]
public struct Name32
{
    private byte _element0;   // compiler generates the fixed-size buffer from this
};

public struct MenuItem
{
    public ulong Id;
    public Name32 Name;        // UTF8 bytes, inline, unmanaged
    public int PriceCents;
    public bool IsAvailable;
    public long LastUpdatedTicks;
};


public static class Extensions
{
    extension(MenuItem source)
    {
        /// <summary>Encodes a name into the item's inline byte buffer, truncating if it doesn't fit.</summary>
        public static void SetName(ref MenuItem item, string name)
        {
            Span<byte> dest = MemoryMarshal.CreateSpan(ref item.Name[0], 32);
            dest.Clear();

            Span<byte> temp = stackalloc byte[64];
            int byteCount = Encoding.UTF8.GetBytes(name, temp);

            int copyLen = Math.Min(byteCount, dest.Length);
            temp[..copyLen].CopyTo(dest);
        }

        /// <summary>Decodes the inline byte buffer back into a string, stopping at the first zero byte.</summary>
        public static string GetName(MenuItem item)
        {
            Span<byte> src = MemoryMarshal.CreateSpan(ref item.Name[0], 32);
            int nullIndex = src.IndexOf((byte)0);
            ReadOnlySpan<byte> trimmed = nullIndex >= 0 ? src[..nullIndex] : src;
            return Encoding.UTF8.GetString(trimmed);
        }

        public static MenuItem Create(ulong id, string name, int priceCents, bool isAvailable)
        {
            var item = new MenuItem
            {
                Id = id,
                PriceCents = priceCents,
                IsAvailable = isAvailable,
                LastUpdatedTicks = DateTime.UtcNow.Ticks
            };
            SetName(ref item, name);
            return item;
        }
    }
}

public static class NewWeekMenu
{
    // 7 items for example — adjust as needed
    public static readonly (ulong Key, MenuItem Value)[] Items = Create();

    private static (ulong Key, MenuItem Value)[] Create()
    {
        var arr = new (ulong Key, MenuItem Value)[7];

        var item1 = Extensions.Create(2001, "Ćevapi", 650, true);
        var item2 = Extensions.Create(2002, "Piletina", 550, true);
        var item3 = Extensions.Create(2003, "Grah", 450, true);
        var item4 = Extensions.Create(2004, "Lasagna", 450, true);
        var item5 = Extensions.Create(2005, "Pizza Margherita", 450, true);
        var item6 = Extensions.Create(2006, "Riblji Fileti", 450, true);
        var item7 = Extensions.Create(2007, "Šnicla", 450, true);

        arr[0] = (item1.Id, item1);
        arr[1] = (item2.Id, item2);
        arr[2] = (item3.Id, item3);
        arr[3] = (item4.Id, item3);
        arr[4] = (item5.Id, item3);
        arr[5] = (item6.Id, item3);
        arr[6] = (item7.Id, item3);

        return arr;
    }

    public static ReadOnlySpan<(ulong Key, MenuItem Value)> AsSpan() => Items;
}
