namespace MagicCSharp.Tests;

/// <summary>A warehouse rule kept as an override: a parcel that has shipped is never deleted, hard or soft.</summary>
public class ShippedParcelsKeptRepository(TimeProvider timeProvider) : ParcelsRepository(timeProvider)
{
    public override async Task<int> Delete(long key)
    {
        var parcel = await Get(key);
        if (parcel?.Status == ParcelStatus.Shipped)
        {
            return 0;
        }

        return await base.Delete(key);
    }

    public override async Task<Parcel> SoftDelete(long key)
    {
        var parcel = await Get(key);
        if (parcel?.Status == ParcelStatus.Shipped)
        {
            return parcel;
        }

        return await base.SoftDelete(key);
    }
}
